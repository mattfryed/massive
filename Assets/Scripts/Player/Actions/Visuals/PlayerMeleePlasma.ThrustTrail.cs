using System.Collections.Generic;
using UnityEngine;

namespace Massive.Player
{
    public sealed partial class PlayerMeleePlasma
    {
        [Header("Thrust — emission and aftermath")]
        [Range(0, 1.5f), Tooltip("Visual fade after the attack stage. Does not extend damage or block the next attack.")]
        public float thrustLingerSeconds = .4f;
        [Range(.5f, 4f)] public float thrustFadeCurve = 1.3f;
        [Tooltip("Particles retain their birth position and aim. Only newly emitted particles follow the player.")]
        public bool thrustWorldEmission = true;
        [Range(1, 5), Tooltip("Body emission passes during one thrust. New passes follow current aim; previous particles stay in their birth frame. Applied on the next attack or restarted preview.")]
        public int thrustEmissionPasses = 3;

        struct ThrustPose
        {
            public float sourceTime;
            public Vector3 position, scale;
            public Quaternion rotation;
            public Matrix4x4 Matrix => Matrix4x4.TRS(position, rotation, scale);
        }

        struct ThrustBirth
        {
            public float sourceTime;
            public Matrix4x4 matrix;
            public Quaternion rotation;
        }

        sealed class ThrustTrail
        {
            public PrefabEffectInstance instance;
            public AttackStage stage;
            public readonly List<ThrustPose> poses = new List<ThrustPose>(128);
            public Dictionary<uint, ThrustBirth>[] births;
            public Matrix4x4[] systemToAnchor;
            public Quaternion[] systemRotation;
            public ParticleSystem.Burst[][] originalBursts;
            public bool[] repeatedBodySystems;
            public float[] firstBurstTimes;
            public ThrustEnergy energy;
            public ThrustPose emitterPose;
            public float born, activationStart, activationEnd, lastElapsed, lastSourceTime;
            public float recoveryStart, recoverySource, birthCutoff;
            public bool alive, tracking, activated, emitterFrozen, preview, rendererPoseCaptured;
        }

        readonly ThrustTrail[] thrustTrails = new ThrustTrail[3];
        ThrustTrail activeThrust;
        int nextThrustTrail;
        float lastThrustStageT = -1;

        bool ThrustTrailIsRendering
        {
            get
            {
                foreach (var trail in thrustTrails)
                    if (trail != null && trail.alive && trail.instance != null && trail.instance.visible) return true;
                return false;
            }
        }

        void RenderThrustTrail(AttackStage stage, float t, Vector3 direction, float clock, bool preview)
        {
            var settings = thrustPrefabEffect;
            if (visualStyle != MeleeVisualStyle.SwordSlashes || stage == null || settings == null || !settings.prefab)
            { HideThrustTrails(); return; }
            bool beginning = activeThrust == null || activeThrust.stage != stage ||
                activeThrust.instance == null || activeThrust.instance.source != settings.prefab ||
                (!preview && t + .001f < lastThrustStageT);
            if (beginning)
            {
                EndThrustAttackTracking();
                activeThrust = AcquireThrustTrail(preview ? 0 : nextThrustTrail++ % thrustTrails.Length, settings);
                var trail = activeThrust;
                trail.stage = stage;
                trail.preview = preview;
                trail.born = preview ? 0 : clock - t * stage.Duration;
                trail.activationStart = stage.ActivationStartNormalized * stage.Duration;
                trail.activationEnd = stage.ActivationEndNormalized * stage.Duration;
                trail.lastElapsed = trail.lastSourceTime = -1;
                trail.recoveryStart = float.PositiveInfinity;
                trail.birthCutoff = float.PositiveInfinity;
                trail.activated = trail.emitterFrozen = false;
                trail.rendererPoseCaptured = false;
                trail.alive = trail.tracking = true;
                trail.poses.Clear();
                foreach (var births in trail.births) births.Clear();
            }
            var current = activeThrust;
            float elapsed = preview ? clock : clock - current.born;
            // A backward scrub reconstructs the source in the current editor pose.
            if (preview && elapsed + .0001f < current.lastElapsed)
            {
                current.poses.Clear();
                foreach (var births in current.births) births.Clear();
                current.recoveryStart = float.PositiveInfinity;
                current.birthCutoff = float.PositiveInfinity;
                current.emitterFrozen = current.activated = false;
            }
            if (!current.emitterFrozen)
            {
                PositionThrustEmitter(current, direction, settings);
                if (elapsed >= current.stage.Duration) current.emitterFrozen = true;
            }
            if (elapsed >= current.activationStart) current.activated = true;
            lastThrustStageT = t;
            UpdateThrustTrail(current, Mathf.Max(0, elapsed), settings);
        }

        void PositionThrustEmitter(ThrustTrail trail, Vector3 direction, MeleePrefabEffectSettings settings)
        {
            direction.y = 0;
            direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.right;
            Quaternion facing = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 origin = playerVisuals && playerVisuals.visuals ? playerVisuals.visuals.position : transform.position;
            origin.y += surfaceHeight;
            var instance = trail.instance;
            trail.emitterPose = new ThrustPose
            {
                position = origin + facing * settings.positionOffset,
                rotation = facing,
                scale = new Vector3(Mathf.Max(.01f, settings.widthScale), 1, 1)
            };
            // Keep the renderer basis fixed as well as particle centers. Rotating a
            // nonuniformly scaled PS would otherwise shear already-emitted mesh particles.
            // The virtual emitter still moves and supplies the birth pose for new particles.
            if (!thrustWorldEmission || !trail.rendererPoseCaptured)
            {
                instance.anchor.transform.SetPositionAndRotation(trail.emitterPose.position, trail.emitterPose.rotation);
                instance.anchor.transform.localScale = trail.emitterPose.scale;
                trail.rendererPoseCaptured = thrustWorldEmission;
            }
            float reach = VolumePhysicalReach();
            instance.effect.transform.localPosition = Vector3.zero;
            instance.effect.transform.localRotation = Quaternion.Euler(settings.rotationOffset);
            instance.effect.transform.localScale = Vector3.one * Mathf.Max(.01f, reach * settings.reachScale / Mathf.Max(.01f, settings.referenceReach));
            for (int s = 0; s < instance.systems.Length; s++)
            {
                var psTransform = instance.systems[s].transform;
                trail.systemToAnchor[s] = instance.anchor.transform.worldToLocalMatrix * psTransform.localToWorldMatrix;
                trail.systemRotation[s] = Quaternion.Inverse(instance.anchor.transform.rotation) * psTransform.rotation;
            }
        }

        void EndThrustAttackTracking()
        {
            if (activeThrust != null)
            {
                var trail = activeThrust;
                // Windup cancellation must not let a hidden source launch after the attack.
                if (!trail.activated)
                { trail.alive = false; HidePrefabEffect(trail.instance); HideThrustEnergy(trail); }
                else if (float.IsPositiveInfinity(trail.recoveryStart))
                {
                    float elapsed = trail.preview ? Mathf.Max(0, trail.lastElapsed) : Mathf.Max(0, Time.time - trail.born);
                    if (elapsed < trail.stage.Duration - .0001f)
                    {
                        float source = ThrustSourceAt(trail, elapsed, thrustPrefabEffect);
                        trail.recoveryStart = elapsed;
                        trail.recoverySource = source;
                        // An interrupted attack may fade its existing particles, but must
                        // not reveal later source bursts as if the attack had completed.
                        trail.birthCutoff = source;
                    }
                }
                trail.tracking = false;
                trail.emitterFrozen = true;
            }
            activeThrust = null;
            lastThrustStageT = -1;
        }

        void TickThrustAftermath(float clock)
        {
            if (visualStyle != MeleeVisualStyle.SwordSlashes || thrustPrefabEffect == null || !thrustPrefabEffect.prefab)
            { HideThrustTrails(); return; }
            foreach (var trail in thrustTrails)
            {
                // The current attack samples after its live emitter pose is updated. Sampling
                // it here first would incorrectly anchor this frame's births to the last pose.
                if (trail == null || !trail.alive || trail.tracking) continue;
                UpdateThrustTrail(trail, Mathf.Max(0, clock - trail.born), thrustPrefabEffect);
            }
        }

        static float ThrustSourceDuringAttack(ThrustTrail trail, float elapsed, MeleePrefabEffectSettings settings)
        {
            if (settings == null) return 0;
            float onset = Mathf.Max(0, settings.sourceStartTime);
            float activeEnd = Mathf.Max(onset + .021f, settings.sourceActiveEndTime);
            if (elapsed < trail.activationStart)
                return onset + .02f * Mathf.Clamp01(elapsed / Mathf.Max(.001f, trail.activationStart));
            return Mathf.Lerp(onset + .02f, activeEnd,
                Mathf.Clamp01((elapsed - trail.activationStart) / Mathf.Max(.001f, trail.activationEnd - trail.activationStart)));
        }

        float ThrustSourceAt(ThrustTrail trail, float elapsed, MeleePrefabEffectSettings settings)
        {
            if (settings == null) return 0;
            if (!float.IsPositiveInfinity(trail.recoveryStart) && elapsed > trail.recoveryStart)
                return Mathf.Lerp(trail.recoverySource, Mathf.Max(trail.recoverySource, settings.sourceRecoveryEndTime),
                    Mathf.Clamp01((elapsed - trail.recoveryStart) / Mathf.Max(.0001f, thrustLingerSeconds)));
            if (elapsed <= trail.activationEnd) return ThrustSourceDuringAttack(trail, elapsed, settings);
            float from = ThrustSourceDuringAttack(trail, trail.activationEnd, settings);
            // Keep source onset and active-window timing. Stretch authored recovery over
            // the remaining stage plus aftermath, instead of exhausting it before the fade.
            float duration = Mathf.Max(.0001f, trail.stage.Duration - trail.activationEnd + Mathf.Max(0, thrustLingerSeconds));
            return Mathf.Lerp(from, Mathf.Max(from, settings.sourceRecoveryEndTime),
                Mathf.Clamp01((elapsed - trail.activationEnd) / duration));
        }

        void UpdateThrustTrail(ThrustTrail trail, float elapsed, MeleePrefabEffectSettings settings)
        {
            var instance = trail.instance;
            if (instance == null || !instance.anchor || settings == null) { HideThrustEnergy(trail); return; }
            float recoveryStart = Mathf.Min(trail.stage.Duration, trail.recoveryStart);
            float linger = Mathf.Max(0, thrustLingerSeconds);
            if (elapsed >= recoveryStart + linger)
            { trail.alive = false; HidePrefabEffect(instance); HideThrustEnergy(trail); return; }
            trail.alive = true;
            float recovery = Mathf.Clamp01((elapsed - recoveryStart) / Mathf.Max(.0001f, linger));
            float sourceTime = ThrustSourceAt(trail, elapsed, settings);
            float opacity = elapsed < trail.activationStart ? windupOpacity * Mathf.SmoothStep(0, 1,
                elapsed / Mathf.Max(.001f, trail.activationStart)) :
                1 - Mathf.SmoothStep(0, 1, Mathf.Pow(recovery, Mathf.Max(.5f, thrustFadeCurve)));
            RecordThrustPose(trail, sourceTime);
            instance.anchor.SetActive(true);
            instance.visible = false;
            BeginThrustEnergy(trail);
            Color tint = settings.tint;
            tint.a *= opacity;
            float brightness = Mathf.Max(0, settings.intensity);
            float boost = Mathf.Max(1, Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b)) * brightness);
            for (int s = 0; s < instance.systems.Length; s++)
            {
                var ps = instance.systems[s];
                var renderer = instance.renderers[s];
                if (!renderer || !renderer.sharedMaterial) { ps.Clear(false); continue; }
                bool layer = LayerEnabled(instance.layers[s], settings);
                // Organic mode owns glow, including intensity zero; the authored flat
                // Glow is available only through the explicit comparison toggle.
                bool sourceGlow = ps.name == "Glow";
                bool sourceFlash = ps.transform == instance.effect.transform && instance.source.name == "Prick 5";
                renderer.enabled = layer && !(thrustOrganicGlow && sourceGlow) && tint.a * brightness > .001f;
                var block = instance.properties[s];
                var material = renderer.sharedMaterial;
                block.Clear();
                foreach (int property in EmissionProperties)
                    if (material.HasProperty(property)) block.SetFloat(property, material.GetFloat(property) * boost);
                if (material.HasProperty(NoiseEmissionProperty))
                {
                    Vector4 noise = material.GetVector(NoiseEmissionProperty); noise.z *= boost;
                    block.SetVector(NoiseEmissionProperty, noise);
                }
                renderer.SetPropertyBlock(block);
                // Restarting a deterministic source restores the authored motion and lifetime.
                // The birth-frame remap below affects presentation only, never the source asset.
                ps.Simulate(sourceTime, false, true, true);
                int count = ps.GetParticles(instance.particles[s]);
                var inverse = ps.transform.worldToLocalMatrix;
                var inverseRotation = Quaternion.Inverse(ps.transform.rotation);
                int written = 0;
                for (int i = 0; i < count; i++)
                {
                    var particle = instance.particles[s][i];
                    float birthTime = sourceTime - (particle.startLifetime - particle.remainingLifetime);
                    ThrustBirth birth;
                    // The source is deterministic and non-looping. Bind each seed once:
                    // fixed-step age quantization must never rewrite an existing world frame.
                    if (!trail.births[s].TryGetValue(particle.randomSeed, out birth))
                    {
                        var pose = ThrustPoseAt(trail, birthTime);
                        birth = new ThrustBirth { sourceTime = birthTime,
                            matrix = pose.Matrix * trail.systemToAnchor[s], rotation = pose.rotation * trail.systemRotation[s] };
                        trail.births[s][particle.randomSeed] = birth;
                    }
                    if (birth.sourceTime > trail.birthCutoff + .002f) continue;
                    float passOpacity = trail.repeatedBodySystems[s] && birth.sourceTime > trail.firstBurstTimes[s] + .025f ? .65f : 1f;
                    if (layer && trail.repeatedBodySystems[s]) AddThrustEnergy(trail, ps, renderer, particle, birth, passOpacity);
                    if (thrustWorldEmission)
                    {
                        particle.position = inverse.MultiplyPoint3x4(birth.matrix.MultiplyPoint3x4(particle.position));
                        particle.velocity = inverse.MultiplyVector(birth.matrix.MultiplyVector(particle.velocity));
                        if (renderer.alignment == ParticleSystemRenderSpace.Local)
                            particle.rotation3D = (inverseRotation * birth.rotation * Quaternion.Euler(particle.rotation3D)).eulerAngles;
                    }
                    Color color = particle.startColor;
                    color.r *= tint.r * brightness / boost;
                    color.g *= tint.g * brightness / boost;
                    color.b *= tint.b * brightness / boost;
                    color.a *= tint.a * Mathf.Min(1, brightness);
                    color.a *= passOpacity;
                    // Prick's root is another large flat flash. In organic mode it only
                    // punctuates the onset; the energy volume carries the continuing light.
                    if (thrustOrganicGlow && sourceFlash)
                    {
                        float age = Mathf.Max(0, particle.startLifetime - particle.remainingLifetime);
                        color.a *= .18f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.015f, .045f, age)));
                    }
                    particle.startColor = color;
                    instance.particles[s][written++] = particle;
                }
                ps.SetParticles(instance.particles[s], written);
                instance.visible |= renderer.enabled && written > 0;
            }
            RenderThrustEnergy(trail, elapsed, tint.a * Mathf.Min(1, brightness));
            trail.lastElapsed = elapsed;
            trail.lastSourceTime = sourceTime;
        }

        static void RecordThrustPose(ThrustTrail trail, float sourceTime)
        {
            var pose = trail.emitterPose;
            pose.sourceTime = sourceTime;
            int count = trail.poses.Count;
            if (count > 0 && Mathf.Abs(trail.poses[count - 1].sourceTime - sourceTime) < .00001f)
                trail.poses[count - 1] = pose;
            else
            {
                // Cached births outlive this short history; 256 samples bound preview memory.
                if (count >= 256) trail.poses.RemoveAt(0);
                trail.poses.Add(pose);
            }
        }

        static ThrustPose ThrustPoseAt(ThrustTrail trail, float sourceTime)
        {
            var poses = trail.poses;
            if (poses.Count == 0) return new ThrustPose { rotation = Quaternion.identity, scale = Vector3.one };
            if (sourceTime <= poses[0].sourceTime) return poses[0];
            for (int i = poses.Count - 1; i > 0; i--)
            {
                if (sourceTime < poses[i - 1].sourceTime) continue;
                float t = Mathf.InverseLerp(poses[i - 1].sourceTime, poses[i].sourceTime, sourceTime);
                return new ThrustPose { sourceTime = sourceTime,
                    position = Vector3.Lerp(poses[i - 1].position, poses[i].position, t),
                    rotation = Quaternion.Slerp(poses[i - 1].rotation, poses[i].rotation, t),
                    scale = Vector3.Lerp(poses[i - 1].scale, poses[i].scale, t) };
            }
            return poses[poses.Count - 1];
        }

        ThrustTrail AcquireThrustTrail(int index, MeleePrefabEffectSettings settings)
        {
            var trail = thrustTrails[index];
            if (trail == null) trail = thrustTrails[index] = new ThrustTrail();
            HidePrefabEffect(trail.instance);
            HideThrustEnergy(trail);
            var previousInstance = trail.instance;
            EnsurePrefabEffect(settings.prefab, ref trail.instance);
            if (previousInstance != trail.instance) trail.originalBursts = null;
            // World ownership also applies to recovery, even after the player moves away.
            trail.instance.anchor.transform.SetParent(null, false);
            trail.instance.anchor.name = "Thrust particles (temporary " + index + ")";
            int length = trail.instance.systems.Length;
            if (trail.births == null || trail.births.Length != length)
            {
                trail.births = new Dictionary<uint, ThrustBirth>[length];
                trail.systemToAnchor = new Matrix4x4[length];
                trail.systemRotation = new Quaternion[length];
                for (int s = 0; s < length; s++) trail.births[s] = new Dictionary<uint, ThrustBirth>();
            }
            ConfigureThrustBodyBursts(trail, settings);
            return trail;
        }

        void ConfigureThrustBodyBursts(ThrustTrail trail, MeleePrefabEffectSettings settings)
        {
            var systems = trail.instance.systems;
            if (trail.originalBursts == null || trail.originalBursts.Length != systems.Length)
            {
                trail.originalBursts = new ParticleSystem.Burst[systems.Length][];
                trail.repeatedBodySystems = new bool[systems.Length];
                trail.firstBurstTimes = new float[systems.Length];
                for (int s = 0; s < systems.Length; s++)
                {
                    var ps = systems[s];
                    var emission = ps.emission;
                    var bursts = new ParticleSystem.Burst[emission.burstCount];
                    emission.GetBursts(bursts);
                    trail.originalBursts[s] = bursts;
                    bool rootBody = ps.transform == trail.instance.effect.transform &&
                        trail.instance.source.name == "Prick 5";
                    trail.repeatedBodySystems[s] = rootBody || ps.name == "RoseTrail" || ps.name == "Trail";
                    float first = float.PositiveInfinity;
                    foreach (var burst in bursts) first = Mathf.Min(first, burst.time);
                    trail.firstBurstTimes[s] = ps.main.startDelay.constant + first;
                }
            }
            int passes = Mathf.Clamp(thrustEmissionPasses, 1, 5);
            float onset = Mathf.Max(0, settings.sourceStartTime);
            float activeEnd = Mathf.Max(onset + .021f, settings.sourceActiveEndTime);
            float interval = Mathf.Max(.02f, (activeEnd - (onset + .02f)) / passes);
            for (int s = 0; s < systems.Length; s++)
            {
                if (!trail.repeatedBodySystems[s]) continue;
                var emission = systems[s].emission;
                var bursts = trail.originalBursts[s];
                // Only instance-owned emission modules change. Every reuse derives from
                // the untouched source settings, so repeats never multiply across attacks.
                for (int b = 0; b < bursts.Length; b++)
                {
                    var burst = bursts[b];
                    burst.cycleCount = passes;
                    burst.repeatInterval = interval;
                    emission.SetBurst(b, burst);
                }
            }
        }

        void HideThrustTrails()
        {
            foreach (var trail in thrustTrails)
                if (trail != null)
                { trail.alive = trail.tracking = false; HidePrefabEffect(trail.instance); HideThrustEnergy(trail); }
            activeThrust = null; lastThrustStageT = -1;
        }

        void ReleaseThrustTrails()
        {
            HideThrustTrails();
            for (int i = 0; i < thrustTrails.Length; i++)
            {
                if (thrustTrails[i] != null)
                {
                    ReleaseThrustEnergy(thrustTrails[i]);
                    ReleasePrefabEffect(ref thrustTrails[i].instance);
                }
                thrustTrails[i] = null;
            }
            nextThrustTrail = 0;
            ReleaseThrustEnergyResources();
        }
    }
}
