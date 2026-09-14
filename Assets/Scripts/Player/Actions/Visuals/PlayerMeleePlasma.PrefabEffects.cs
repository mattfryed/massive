using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Player
{
    [Serializable]
    public sealed class MeleePrefabEffectSettings
    {
        public GameObject prefab;
        [Tooltip("Rotates the authored effect onto the playing plane. The adapter's forward is the live sword direction.")]
        public Vector3 rotationOffset;
        [Tooltip("Offset in metres: X across the attack, Y height, Z forward.")]
        public Vector3 positionOffset;
        [Min(.01f), Tooltip("Authored effect reach before adapting it to the existing sword capsule.")]
        public float referenceReach = 5f;
        [Range(.25f, 2f)] public float reachScale = 1f;
        [Range(.25f, 2f)] public float widthScale = 1f;
        [Min(0), Tooltip("Source time at emission onset; skips the pack's built-in anticipation delay.")]
        public float sourceStartTime;
        [Min(0), Tooltip("Source frame reached at the end of the existing damage window.")]
        public float sourceActiveEndTime = .3f;
        [Min(0), Tooltip("Source frame reached at the end of recovery. Playback is cleared when the stage ends.")]
        public float sourceRecoveryEndTime = .8f;
        [Tooltip("Multiplies the original pack colors; white preserves them.")]
        public Color tint = Color.white;
        [Range(0, 3)] public float intensity = 1f;
        public bool smoke;
        public bool accentParticles = true;
        public bool groundImpact;
        public bool distortion;
    }

    public sealed partial class PlayerMeleePlasma
    {
        [Header("Asset slashes — thrust / Prick 5")]
        public MeleePrefabEffectSettings thrustPrefabEffect = new MeleePrefabEffectSettings
        {
            referenceReach = 5f, positionOffset = new Vector3(0, 0, .25f),
            sourceStartTime = .32f, sourceActiveEndTime = .65f, sourceRecoveryEndTime = .95f
        };

        [Header("Asset slashes — second arc / Sword Slash 5")]
        public MeleePrefabEffectSettings swipePrefabEffect = new MeleePrefabEffectSettings
        {
            rotationOffset = new Vector3(-90, -90, 0), referenceReach = 2.5f,
            sourceActiveEndTime = .18f, sourceRecoveryEndTime = .65f
        };

        sealed class PrefabEffectInstance
        {
            public GameObject source, anchor, effect;
            public ParticleSystem[] systems;
            public ParticleSystemRenderer[] renderers;
            public ParticleSystem.Particle[][] particles;
            public EffectLayer[] layers;
            public MaterialPropertyBlock[] properties;
            public bool visible;
        }

        [Flags] enum EffectLayer { Body = 0, Smoke = 1, Accent = 2, Ground = 4, Distortion = 8 }
        PrefabEffectInstance thrustInstance, swipeInstance;
        bool PrefabEffectIsRendering => (thrustInstance != null && thrustInstance.visible) || (swipeInstance != null && swipeInstance.visible);

        void RenderPrefabStage(AttackStage stage, float t, Vector3 direction, int swipeSign)
        {
            bool swipe = stage.StageType == AttackStageType.ComboSwipe;
            var settings = swipe ? swipePrefabEffect : thrustPrefabEffect;
            if (settings == null || !settings.prefab || t >= 1f) { HidePrefabEffects(); return; }
            direction.y = 0;
            direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.right;
            float start = stage.ActivationStartNormalized, end = stage.ActivationEndNormalized;
            float forming = start > .0001f ? Mathf.Clamp01(t / start) : 1;
            float recovering = Mathf.Clamp01((t - end) / Mathf.Max(.001f, 1 - end));
            float opacity = t < start ? windupOpacity * forming : t <= end ? 1f : recoveryOpacity * (1 - recovering);
            if (opacity * settings.intensity * settings.tint.a <= .001f) { HidePrefabEffects(); return; }
            var instance = swipe ? EnsurePrefabEffect(settings.prefab, ref swipeInstance) : EnsurePrefabEffect(settings.prefab, ref thrustInstance);
            HidePrefabEffect(swipe ? thrustInstance : swipeInstance);

            Vector3 origin = playerVisuals && playerVisuals.visuals ? playerVisuals.visuals.position : transform.position;
            origin.y += surfaceHeight;
            float reach = 1.725f;
            if (meleeExtent)
            {
                Vector3 axis = meleeExtent.direction == 0 ? Vector3.right : meleeExtent.direction == 1 ? Vector3.up : Vector3.forward;
                Vector3 outer = meleeExtent.transform.TransformPoint(meleeExtent.center) +
                    meleeExtent.transform.TransformVector(axis * Mathf.Max(meleeExtent.radius, meleeExtent.height * .5f));
                Vector3 planar = outer - transform.position; planar.y = 0; reach = planar.magnitude;
            }
            Quaternion facing = Quaternion.LookRotation(direction, Vector3.up);
            instance.anchor.transform.SetPositionAndRotation(origin + facing * settings.positionOffset, facing);
            // Compensate the player's scale: calibration and offsets are world metres.
            Vector3 ownerScale = transform.lossyScale;
            instance.anchor.transform.localScale = new Vector3(1 / Mathf.Max(.0001f, Mathf.Abs(ownerScale.x)),
                1 / Mathf.Max(.0001f, Mathf.Abs(ownerScale.y)), 1 / Mathf.Max(.0001f, Mathf.Abs(ownerScale.z)));
            float scale = Mathf.Max(.01f, reach * settings.reachScale / Mathf.Max(.01f, settings.referenceReach));
            instance.effect.transform.localRotation = Quaternion.Euler(settings.rotationOffset);
            instance.effect.transform.localScale = Vector3.one * scale;
            // Mirror in attack space so successive left/right sweeps retain the same handedness as the sword.
            instance.anchor.transform.localScale = Vector3.Scale(instance.anchor.transform.localScale,
                new Vector3(settings.widthScale * (swipe && swipeSign < 0 ? -1 : 1), 1, 1));

            float onset = Mathf.Max(0, settings.sourceStartTime);
            float activeEnd = Mathf.Max(onset + .021f, settings.sourceActiveEndTime);
            float recoveryEnd = Mathf.Max(activeEnd, settings.sourceRecoveryEndTime);
            float activeT = Mathf.Clamp01((t - start) / Mathf.Max(.001f, end - start));
            float sourceTime = t < start ? onset + .02f * forming :
                t <= end ? Mathf.Lerp(onset + .02f, activeEnd, activeT) : Mathf.Lerp(activeEnd, recoveryEnd, recovering);
            instance.anchor.SetActive(true);
            instance.visible = false;
            Color tint = settings.tint; tint.a *= opacity;
            float brightness = Mathf.Max(0, settings.intensity);
            float emissionBoost = Mathf.Max(1, Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b)) * brightness);

            for (int s = 0; s < instance.systems.Length; s++)
            {
                var ps = instance.systems[s];
                var renderer = instance.renderers[s];
                if (!renderer || !renderer.sharedMaterial) { ps.Clear(false); continue; }
                bool enabledLayer = LayerEnabled(instance.layers[s], settings);
                renderer.enabled = enabledLayer;
                if (!enabledLayer) { ps.Clear(false); continue; }
                // Particle colors are bytes. Put HDR gain in the material's emission instead
                // of silently clipping the intensity dial at white.
                var block = instance.properties[s];
                var material = renderer.sharedMaterial;
                block.Clear();
                foreach (int property in EmissionProperties)
                    if (material.HasProperty(property)) block.SetFloat(property, material.GetFloat(property) * emissionBoost);
                if (material.HasProperty(NoiseEmissionProperty))
                {
                    Vector4 noise = material.GetVector(NoiseEmissionProperty); noise.z *= emissionBoost;
                    block.SetVector(NoiseEmissionProperty, noise);
                }
                renderer.SetPropertyBlock(block);
                // Deterministic short source sampling supports scrubbing, cancellation, aim changes,
                // slow motion and repeated attacks without free-running/left-over particle timelines.
                ps.Simulate(sourceTime, false, true, true);
                int count = ps.GetParticles(instance.particles[s]);
                for (int i = 0; i < count; i++)
                {
                    Color color = instance.particles[s][i].startColor;
                    color.r *= tint.r * brightness / emissionBoost;
                    color.g *= tint.g * brightness / emissionBoost;
                    color.b *= tint.b * brightness / emissionBoost;
                    color.a *= tint.a * Mathf.Min(1, brightness);
                    instance.particles[s][i].startColor = color;
                }
                ps.SetParticles(instance.particles[s], count);
                instance.visible |= count > 0;
            }
        }

        PrefabEffectInstance EnsurePrefabEffect(GameObject prefab, ref PrefabEffectInstance cache)
        {
            if (cache != null && cache.anchor && cache.source == prefab) return cache;
            ReleasePrefabEffect(ref cache);
            var instance = new PrefabEffectInstance { source = prefab };
            instance.anchor = new GameObject("Melee asset effect (temporary)") { hideFlags = HideFlags.HideAndDontSave };
            instance.anchor.transform.SetParent(transform, false);
            instance.anchor.SetActive(false);
            instance.effect = Instantiate(prefab, instance.anchor.transform, false);
            instance.effect.name = prefab.name + " (adapted visual)";
            instance.effect.transform.localPosition = Vector3.zero;
            foreach (var child in instance.anchor.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags = HideFlags.HideAndDontSave;
                child.gameObject.layer = gameObject.layer;
            }
            // Presentation assets must not bring demo logic or collision into the attack.
            foreach (var behaviour in instance.effect.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var collider in instance.effect.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var light in instance.effect.GetComponentsInChildren<Light>(true)) light.enabled = false;
            instance.systems = instance.effect.GetComponentsInChildren<ParticleSystem>(true);
            int length = instance.systems.Length;
            instance.renderers = new ParticleSystemRenderer[length];
            instance.particles = new ParticleSystem.Particle[length][];
            instance.layers = new EffectLayer[length];
            instance.properties = new MaterialPropertyBlock[length];
            for (int s = 0; s < length; s++)
            {
                var ps = instance.systems[s];
                var main = ps.main;
                main.playOnAwake = false; main.loop = false; main.stopAction = ParticleSystemStopAction.None;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                var collision = ps.collision; collision.enabled = false;
                var trigger = ps.trigger; trigger.enabled = false;
                ps.useAutoRandomSeed = false; ps.randomSeed = (uint)(7103 + s * 137);
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer) { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; }
                instance.renderers[s] = renderer;
                instance.particles[s] = new ParticleSystem.Particle[main.maxParticles];
                instance.layers[s] = ClassifyLayer(ps.transform, instance.effect.transform);
                instance.properties[s] = new MaterialPropertyBlock();
            }
            cache = instance;
            return cache;
        }

        static readonly int[] EmissionProperties = { Shader.PropertyToID("_Emission"), Shader.PropertyToID("_FresnelEmission"), Shader.PropertyToID("_BackFresnelEmission") };
        static readonly int NoiseEmissionProperty = Shader.PropertyToID("_NoisespeedXYEmissonZPowerW");

        static EffectLayer ClassifyLayer(Transform part, Transform root)
        {
            EffectLayer flags = EffectLayer.Body;
            for (var node = part; node != null && node != root; node = node.parent)
            {
                string name = node.name.ToLowerInvariant();
                if (name.Contains("distortion")) flags |= EffectLayer.Distortion;
                if (name.Contains("smoke") || name == "wind" || name == "fire") flags |= EffectLayer.Smoke;
                if (name.Contains("sparks") || name == "flowers" || name == "petals" || name == "leaves") flags |= EffectLayer.Accent;
                if (name.Contains("ground") || name.Contains("derbis") || name.Contains("debris")) flags |= EffectLayer.Ground;
            }
            return flags;
        }

        static bool LayerEnabled(EffectLayer layer, MeleePrefabEffectSettings settings) =>
            ((layer & EffectLayer.Smoke) == 0 || settings.smoke) &&
            ((layer & EffectLayer.Accent) == 0 || settings.accentParticles) &&
            ((layer & EffectLayer.Ground) == 0 || settings.groundImpact) &&
            ((layer & EffectLayer.Distortion) == 0 || settings.distortion);

        static void HidePrefabEffect(PrefabEffectInstance instance)
        {
            if (instance == null || !instance.anchor) return;
            if (instance.anchor.activeSelf)
            {
                foreach (var ps in instance.systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                instance.anchor.SetActive(false);
            }
            instance.visible = false;
        }
        void HidePrefabEffects() { HidePrefabEffect(thrustInstance); HidePrefabEffect(swipeInstance); }
        void ReleasePrefabEffects() { ReleasePrefabEffect(ref thrustInstance); ReleasePrefabEffect(ref swipeInstance); }
        static void ReleasePrefabEffect(ref PrefabEffectInstance instance)
        {
            if (instance == null) return;
            if (instance.anchor)
            {
                if (Application.isPlaying) Destroy(instance.anchor); else DestroyImmediate(instance.anchor);
            }
            instance = null;
        }
    }
}
