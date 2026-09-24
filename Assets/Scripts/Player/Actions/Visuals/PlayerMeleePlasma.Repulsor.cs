using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Player
{
    public enum RepulsorVisualTreatment { VolumetricPulse, PlasmaRing, Off, SeismicDetonation }

    public sealed partial class PlayerMeleePlasma
    {
        [Header("Third stage — outward repulsor pulse")]
        public RepulsorVisualTreatment repulsorTreatment = RepulsorVisualTreatment.SeismicDetonation;
        [Tooltip("Optional. An included shader supplies the pulse when this is empty.")]
        public Material repulsorPulseMaterial;
        [Range(.035f, .5f)] public float repulsorPulseWidth = .32f;
        [Range(.025f, .6f)] public float repulsorPulseThickness = .28f;
        [Range(.1f, 6)] public float repulsorPulseDensity = 2.8f;
        [Range(0, .4f)] public float repulsorPulseTurbulence = .30f;
        [Range(0, 1)] public float repulsorPulseBreakup = .7f;
        [Range(0, 5)] public float repulsorPulseFlow = 1.3f;
        [Range(0, 1)] public float repulsorPulseLinger = .42f;
        [Range(.5f, 4)] public float repulsorPulseFade = 1.25f;
        public bool repulsorPulseBlackBody = true;
        public bool repulsorPulseWhiteEdge = true;
        public bool repulsorPulseFilaments = true;
        public bool repulsorPulseWisps = true;
        [Range(0, 2)] public float repulsorPulseEdgeIntensity = .9f;
        [Range(0, 2)] public float repulsorPulseFilamentIntensity = 1f;
        [Range(0, 1)] public float repulsorPulseWispIntensity = .3f;
        [Header("Shockwave burst")]
        public bool repulsorReleaseFlash = true;
        public bool repulsorTurbulentWake = true;
        [Range(0f, 1f)] public float repulsorDissolveBreakup = .92f;
        [Header("Seismic detonation")]
        public bool repulsorImplosion = true;
        public bool repulsorBlastVolume = true;
        public bool repulsorSeismicStreak = true;
        [Range(0f, 3f)] public float repulsorRadiance = 1.35f;
        [Range(0f, 1f)] public float repulsorBlastOpacity = .38f;
        [Range(.1f, 1f)] public float repulsorBlastDepth = .55f;
        [Range(0f, 2f)] public float repulsorChargeIntensity = .8f;

        sealed class RepulsorPulse
        {
            public GameObject root;
            public MeshRenderer renderer;
            public MaterialPropertyBlock properties;
            public AttackStage stage;
            public Vector3 origin;
            public float born, activationStart, activationEnd, size = 1f, startRadius, maximumRadius;
            public float lastElapsed, lastRadius, stopTime, stoppedRadius, seed;
            public bool alive, tracking, emitted, preview;
        }

        readonly RepulsorPulse[] repulsorPulses = new RepulsorPulse[3];
        RepulsorPulse activeRepulsorPulse;
        int nextRepulsorPulse;
        float lastRepulsorStageT = -1;
        Mesh repulsorPulseProxy;
        Material ownedRepulsorPulseMaterial;
        bool repulsorPulseMaterialResolved;
        Material ownedDetonationMaterial;
        PlayerRepulsorFeedback repulsorBodyFeedback;
        PlayerRepulsorGridPulse repulsorGridFeedback;
        static readonly int PulseOriginId = Shader.PropertyToID("_PulseOrigin");
        static readonly int PulseBoundsId = Shader.PropertyToID("_PulseBounds");
        static readonly int PulseShapeId = Shader.PropertyToID("_PulseShape");
        static readonly int PulseMotionId = Shader.PropertyToID("_PulseMotion");
        static readonly int PulseLayersId = Shader.PropertyToID("_PulseLayers");
        static readonly int PulseLightId = Shader.PropertyToID("_PulseLight");
        static readonly int PulsePhaseId = Shader.PropertyToID("_PulsePhase");
        static readonly int PulseBurstId = Shader.PropertyToID("_PulseBurst");
        static readonly int PulseRangeId = Shader.PropertyToID("_PulseRange");
        static readonly int DetonationLayersId = Shader.PropertyToID("_DetonationLayers");
        static readonly int DetonationArtId = Shader.PropertyToID("_DetonationArt");

        bool UsesRepulsorPulse => (Effective_repulsorTreatment == RepulsorVisualTreatment.VolumetricPulse || Effective_repulsorTreatment == RepulsorVisualTreatment.SeismicDetonation) &&
            (Effective_visualStyle == MeleeVisualStyle.Plasma || Effective_visualStyle == MeleeVisualStyle.SwordSlashes);
        public bool IsRepulsorPulsePreview => UsesRepulsorPulse && PreviewAttackStage != null &&
            PreviewAttackStage.StageType == AttackStageType.FinisherRepulsor;
        bool RepulsorPulseIsRendering
        {
            get
            {
                foreach (var pulse in repulsorPulses)
                    if (pulse != null && pulse.renderer && pulse.renderer.enabled) return true;
                return false;
            }
        }

        float RepulsorActivationStart(AttackStage stage) => repulsor ? repulsor.EffectiveActivationStart(stage) : stage.ActivationStartNormalized;
        float RepulsorActivationEnd(AttackStage stage) => repulsor ? repulsor.EffectiveActivationEnd(stage) : stage.ActivationEndNormalized;

        void PreviewRepulsorBody(AttackStage stage)
        {
            if (Application.isPlaying || !repulsorBodyFeedback) return;
            if (stage != null && stage.StageType == AttackStageType.FinisherRepulsor &&
                (Effective_visualStyle == MeleeVisualStyle.Plasma || Effective_visualStyle == MeleeVisualStyle.SwordSlashes))
                repulsorBodyFeedback.Preview(previewClock, stage);
            else repulsorBodyFeedback.StopPreview();
        }

        void PreviewRepulsorGrid(AttackStage stage)
        {
            if (Application.isPlaying || !repulsorGridFeedback) return;
            if (stage != null && stage.StageType == AttackStageType.FinisherRepulsor &&
                (Effective_visualStyle == MeleeVisualStyle.Plasma || Effective_visualStyle == MeleeVisualStyle.SwordSlashes))
            {
                float elapsed = previewClock - RepulsorActivationStart(stage) * stage.Duration;
                if (elapsed >= 0)
                {
                    Vector3 origin = activeRepulsorPulse != null && activeRepulsorPulse.emitted ?
                        activeRepulsorPulse.origin : RepulsorVisualOrigin(PlayerVisualSize);
                    repulsorGridFeedback.PreviewPulse(elapsed, -1f, origin);
                    return;
                }
            }
            repulsorGridFeedback.StopPreview();
        }

        void StopRepulsorPresentationPreview()
        {
            if (Application.isPlaying) return;
            if (repulsorBodyFeedback) repulsorBodyFeedback.StopPreview();
            if (repulsorGridFeedback) repulsorGridFeedback.StopPreview();
        }

        Material ResolveRepulsorPulseMaterial()
        {
            if (Effective_repulsorPulseMaterial) return Effective_repulsorPulseMaterial;
            if (Effective_repulsorTreatment == RepulsorVisualTreatment.SeismicDetonation)
            {
                if (!ownedDetonationMaterial)
                {
                    var shader = Resources.Load<Shader>("MeleeRepulsorDetonation");
                    if (shader) ownedDetonationMaterial = new Material(shader)
                    { name = "Repulsor detonation (temporary)", hideFlags = HideFlags.HideAndDontSave };
                }
                return ownedDetonationMaterial;
            }
            if (!repulsorPulseMaterialResolved)
            {
                repulsorPulseMaterialResolved = true;
                var shader = Resources.Load<Shader>("MeleeRepulsor");
                if (!shader) shader = Shader.Find("MASSIVE/MeleeRepulsor");
                if (shader) ownedRepulsorPulseMaterial = new Material(shader)
                { name = "Repulsor pulse (temporary)", hideFlags = HideFlags.HideAndDontSave };
            }
            return ownedRepulsorPulseMaterial;
        }

        Vector3 RepulsorVisualOrigin(float size)
        {
            Vector3 origin = playerVisuals && playerVisuals.visuals ? playerVisuals.visuals.position : transform.position;
            if (playerVisuals) origin += playerVisuals.RepulsorVisualOffsetWS;
            return origin + Vector3.up * (Effective_surfaceHeight * size);
        }

        float RepulsorOutlineRadius(AttackStage previewStage = null)
        {
            float bodyScale = playerVisuals ? playerVisuals.RepulsorVisualScale : 1f;
            if (previewStage != null)
                bodyScale = repulsorBodyFeedback && repulsorBodyFeedback.isActiveAndEnabled &&
                    repulsorBodyFeedback.bodyPulseEnabled && RepulsorActivationStart(previewStage) > 0f ?
                    1f - repulsorBodyFeedback.contraction : 1f;
            if (playerVisuals)
            {
                Transform body = playerVisuals.visuals ? playerVisuals.visuals : playerVisuals.transform;
                Vector3 scale = body.lossyScale;
                return Mathf.Max(.001f, (playerVisuals.baseRadius + playerVisuals.outlineHalf) *
                    Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) * bodyScale);
            }
            return Mathf.Max(.001f, owner ? PlayerScaleAdjuster.BodyRadiusOf(owner) : .5f * PlayerVisualSize);
        }

        void RenderRepulsorPulse(AttackStage stage, float t, float clock, bool preview)
        {
            if (!UsesRepulsorPulse || stage == null || (!preview && (!repulsor || !repulsor.isActiveAndEnabled)))
            { HideRepulsorPulses(); return; }
            bool beginning = activeRepulsorPulse == null || activeRepulsorPulse.stage != stage ||
                (preview && (!Mathf.Approximately(activeRepulsorPulse.size, PlayerVisualSize * stage.RepulsorScale) ||
                    !Mathf.Approximately(activeRepulsorPulse.maximumRadius, stage.GetRepulsorRadius(PlayerVisualSize, RepulsorOutlineRadius(stage))) ||
                    clock + .0001f < activeRepulsorPulse.lastElapsed)) ||
                (!preview && t + .001f < lastRepulsorStageT);
            if (beginning)
            {
                EndRepulsorPulseTracking();
                var pulse = AcquireRepulsorPulse(preview ? 0 : nextRepulsorPulse++ % repulsorPulses.Length);
                activeRepulsorPulse = pulse;
                pulse.stage = stage;
                pulse.born = preview ? 0 : clock - t * stage.Duration;
                pulse.preview = preview;
                pulse.size = PlayerVisualSize * stage.RepulsorScale;
                pulse.activationStart = RepulsorActivationStart(stage) * stage.Duration;
                pulse.activationEnd = Mathf.Max(pulse.activationStart + .001f, RepulsorActivationEnd(stage) * stage.Duration);
                pulse.emitted = false;
                pulse.alive = pulse.tracking = true;
                pulse.stopTime = float.PositiveInfinity;
                pulse.lastElapsed = 0;
                pulse.seed = preview ? .618034f : Mathf.Repeat(nextRepulsorPulse * .381966f, 1f);
                pulse.renderer.enabled = false;
            }
            var current = activeRepulsorPulse;
            float elapsed = Mathf.Max(0, preview ? clock : clock - current.born);
            if (!current.emitted && elapsed < current.activationStart)
            {
                current.origin = RepulsorVisualOrigin(PlayerVisualSize);
                current.startRadius = RepulsorOutlineRadius(stage);
                current.maximumRadius = stage.GetRepulsorRadius(PlayerVisualSize, current.startRadius);
            }
            if (!current.emitted && elapsed >= current.activationStart)
            {
                if (!preview && !repulsor.IsPulseActive)
                { current.lastElapsed = elapsed; current.renderer.enabled = false; HideRepulsorBurst(current); return; }
                // Capture a world frame exactly when the stage releases its force.
                // Direct scrubs must sample the launch pose, not the later body kick.
                if (preview && repulsorBodyFeedback) repulsorBodyFeedback.Preview(current.activationStart, stage);
                current.size = PlayerVisualSize * (preview ? stage.RepulsorScale : repulsor.PulseScale);
                current.origin = RepulsorVisualOrigin(PlayerVisualSize);
                current.startRadius = RepulsorOutlineRadius(preview ? stage : null);
                current.maximumRadius = stage.GetRepulsorRadius(PlayerVisualSize, current.startRadius);
                if (!preview)
                {
                    current.origin = repulsor.OriginWorld + Vector3.up * (Effective_surfaceHeight * PlayerVisualSize);
                    current.startRadius = repulsor.StartRadiusWorld;
                    current.maximumRadius = repulsor.EndRadiusWorld;
                }
                if (preview && repulsorBodyFeedback) repulsorBodyFeedback.Preview(elapsed, stage);
                current.lastRadius = current.startRadius;
                current.emitted = true;
            }
            lastRepulsorStageT = t;
            UpdateRepulsorPulse(current, elapsed);
        }

        float RepulsorPulseRadius(RepulsorPulse pulse, float elapsed)
        {
            float progress = Mathf.Clamp01((elapsed - pulse.activationStart) /
                Mathf.Max(.001f, pulse.activationEnd - pulse.activationStart));
            float shaped = pulse.stage.RepulsorRadiusCurve != null ? pulse.stage.RepulsorRadiusCurve.Evaluate(progress) : progress;
            return Mathf.Lerp(pulse.startRadius, pulse.maximumRadius, Mathf.Clamp01(shaped));
        }

        void EndRepulsorPulseTracking()
        {
            var pulse = activeRepulsorPulse;
            if (pulse != null)
            {
                float elapsed = pulse.preview ? pulse.lastElapsed : Mathf.Max(0, Time.time - pulse.born);
                if (!pulse.emitted)
                { pulse.alive = false; if (pulse.renderer) pulse.renderer.enabled = false; HideRepulsorBurst(pulse); }
                else if (elapsed < pulse.activationEnd)
                {
                    pulse.stopTime = elapsed;
                    pulse.stoppedRadius = pulse.lastRadius;
                }
                pulse.tracking = false;
            }
            activeRepulsorPulse = null;
            lastRepulsorStageT = -1;
        }

        void TickRepulsorPulseAftermath(float clock)
        {
            if (!UsesRepulsorPulse) { HideRepulsorPulses(); return; }
            foreach (var pulse in repulsorPulses)
                if (pulse != null && pulse.alive && !pulse.tracking) UpdateRepulsorPulse(pulse, Mathf.Max(0, clock - pulse.born));
        }

        void UpdateRepulsorPulse(RepulsorPulse pulse, float elapsed)
        {
            pulse.lastElapsed = elapsed;
            float stop = Mathf.Min(pulse.activationEnd, pulse.stopTime);
            float linger = Mathf.Max(0, Effective_repulsorPulseLinger);
            bool detonation = Effective_repulsorTreatment == RepulsorVisualTreatment.SeismicDetonation;
            bool charging = !pulse.emitted && elapsed < pulse.activationStart && detonation && pulse.tracking;
            if ((!pulse.emitted || elapsed < pulse.activationStart) && !charging)
            { pulse.renderer.enabled = false; HideRepulsorBurst(pulse); return; }
            if (elapsed >= stop + linger)
            { pulse.alive = false; pulse.renderer.enabled = false; HideRepulsorBurst(pulse); return; }
            float fade = Mathf.Clamp01((elapsed - stop) / Mathf.Max(.001f, linger));
            float onset = charging ? Mathf.SmoothStep(0, 1, elapsed / Mathf.Max(.001f, pulse.activationStart)) :
                Mathf.SmoothStep(0, 1, (elapsed - pulse.activationStart) / (detonation ? .008f : .025f));
            float opacity = onset * (1 - Mathf.SmoothStep(0, 1, Mathf.Pow(fade, Mathf.Max(.5f, Effective_repulsorPulseFade))));
            bool layers = Effective_repulsorPulseBlackBody || Effective_repulsorPulseWhiteEdge || Effective_repulsorPulseFilaments || Effective_repulsorPulseWisps || Effective_repulsorTurbulentWake ||
                (detonation && (Effective_repulsorImplosion || Effective_repulsorBlastVolume || Effective_repulsorSeismicStreak || Effective_repulsorReleaseFlash));
            Material material = ResolveRepulsorPulseMaterial();
            pulse.renderer.enabled = layers && opacity > .001f && material;
            float size = Mathf.Max(.001f, pulse.size);
            float radius = charging ? pulse.startRadius : float.IsPositiveInfinity(pulse.stopTime) ? RepulsorPulseRadius(pulse, elapsed) : pulse.stoppedRadius;
            if (!pulse.preview && pulse.tracking && pulse == activeRepulsorPulse && repulsor && repulsor.IsPulseActive)
                radius = repulsor.RadiusWorld;
            pulse.lastRadius = radius;
            UpdateRepulsorBurst(pulse, elapsed, radius, fade, opacity);
            if (!pulse.renderer.enabled) return;
            // Only the increasingly broken visual residue drifts beyond the last
            // contact radius. The readable active crest uses the collider exactly.
            float expansion = Mathf.Clamp01((elapsed - pulse.activationStart) / Mathf.Max(.001f, pulse.activationEnd - pulse.activationStart));
            float visualRadius = radius + radius * .065f * Mathf.SmoothStep(0, 1, fade);
            float width = Mathf.Max(.035f, Effective_repulsorPulseWidth) * size * Mathf.Lerp(.52f, 1f, expansion);
            float height = Mathf.Max(.025f, Effective_repulsorPulseThickness) * size * Mathf.Lerp(.65f, 1f, expansion);
            Vector3 bounds = new Vector3(visualRadius + width * 1.7f, height * 2.3f, visualRadius + width * 1.7f);
            if (detonation)
            {
                float footprint = charging ? pulse.startRadius * 2.7f : Mathf.Max(visualRadius + Mathf.Max(width * 1.7f, .4f * size), pulse.startRadius * 3.2f);
                bounds = new Vector3(footprint, Mathf.Max(height * 2.3f, visualRadius * Effective_repulsorBlastDepth + .18f * size), footprint);
                if (charging) bounds.y = Mathf.Max(bounds.y, pulse.startRadius * 2.7f / 1.7f + .1f * size);
            }
            pulse.root.transform.SetPositionAndRotation(pulse.origin, Quaternion.identity);
            pulse.root.transform.localScale = bounds;
            var p = pulse.properties;
            p.SetVector(PulseOriginId, pulse.origin);
            p.SetVector(PulseBoundsId, bounds);
            p.SetVector(PulseShapeId, new Vector4(visualRadius, width, height, size));
            p.SetVector(PulseMotionId, new Vector4((elapsed - pulse.activationStart) * Effective_repulsorPulseFlow,
                pulse.seed * 6.2831853f, Mathf.Clamp01(Effective_repulsorPulseTurbulence), Mathf.Clamp01(Effective_repulsorPulseBreakup)));
            p.SetVector(PulseLayersId, new Vector4(Effective_repulsorPulseBlackBody ? 1 : 0, Effective_repulsorPulseWhiteEdge ? 1 : 0,
                Effective_repulsorPulseFilaments ? 1 : 0, Effective_repulsorPulseWisps ? 1 : 0));
            p.SetVector(PulseLightId, new Vector4(Mathf.Max(.1f, Effective_repulsorPulseDensity) / size,
                Effective_repulsorPulseEdgeIntensity, Effective_repulsorPulseFilamentIntensity, Effective_repulsorPulseWispIntensity));
            p.SetVector(PulsePhaseId, new Vector4(opacity, Mathf.Clamp01((elapsed - pulse.activationStart) /
                Mathf.Max(.001f, pulse.activationEnd - pulse.activationStart)), fade, 0));
            p.SetVector(PulseBurstId, new Vector4(Effective_repulsorReleaseFlash ? 1 : 0,
                Effective_repulsorTurbulentWake ? 1 : 0, Effective_repulsorDissolveBreakup, elapsed - pulse.activationStart));
            p.SetVector(PulseRangeId, new Vector4(pulse.startRadius, pulse.maximumRadius, 0, 0));
            if (detonation)
            {
                p.SetVector(PulseRangeId, new Vector4(pulse.startRadius, pulse.maximumRadius,
                    charging ? Mathf.Clamp01(elapsed / Mathf.Max(.001f, pulse.activationStart)) : 1,
                    pulse.activationEnd - pulse.activationStart));
                p.SetVector(DetonationLayersId, new Vector4(Effective_repulsorImplosion ? 1 : 0,
                    Effective_repulsorBlastVolume ? 1 : 0, Effective_repulsorSeismicStreak ? 1 : 0, Effective_repulsorBlastOpacity));
                p.SetVector(DetonationArtId, new Vector4(Effective_repulsorRadiance, Effective_repulsorBlastDepth, Effective_repulsorChargeIntensity, 0));
            }
            pulse.renderer.sharedMaterial = material;
            pulse.renderer.SetPropertyBlock(p);
        }

        RepulsorPulse AcquireRepulsorPulse(int index)
        {
            var pulse = repulsorPulses[index];
            if (pulse != null && pulse.root) return pulse;
            if (!repulsorPulseProxy)
            {
                repulsorPulseProxy = new Mesh { name = "Repulsor volume bounds", hideFlags = HideFlags.HideAndDontSave };
                repulsorPulseProxy.vertices = new[] { new Vector3(-1,-1,-1), new Vector3(1,-1,-1), new Vector3(1,1,-1), new Vector3(-1,1,-1),
                    new Vector3(-1,-1,1), new Vector3(1,-1,1), new Vector3(1,1,1), new Vector3(-1,1,1) };
                repulsorPulseProxy.triangles = new[] { 0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,3,7,6,3,6,2,0,1,5,0,5,4 };
                repulsorPulseProxy.RecalculateBounds();
            }
            pulse = new RepulsorPulse();
            pulse.root = new GameObject("Repulsor pulse (temporary " + index + ")")
            { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            pulse.root.AddComponent<MeshFilter>().sharedMesh = repulsorPulseProxy;
            pulse.renderer = pulse.root.AddComponent<MeshRenderer>();
            pulse.renderer.enabled = false;
            pulse.renderer.shadowCastingMode = ShadowCastingMode.Off;
            pulse.renderer.receiveShadows = false;
            pulse.renderer.lightProbeUsage = LightProbeUsage.Off;
            pulse.renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            pulse.properties = new MaterialPropertyBlock();
            InitializeRepulsorBurst(pulse);
            repulsorPulses[index] = pulse;
            return pulse;
        }

        void HideRepulsorPulses()
        {
            foreach (var pulse in repulsorPulses)
                if (pulse != null) { pulse.alive = pulse.tracking = false; if (pulse.renderer) pulse.renderer.enabled = false; HideRepulsorBurst(pulse); }
            activeRepulsorPulse = null; lastRepulsorStageT = -1;
        }

        void ReleaseRepulsorPulses()
        {
            HideRepulsorPulses();
            for (int i = 0; i < repulsorPulses.Length; i++)
            {
                if (repulsorPulses[i] != null) ReleaseRepulsorBurst(repulsorPulses[i]);
                if (repulsorPulses[i] != null && repulsorPulses[i].root)
                { if (Application.isPlaying) Destroy(repulsorPulses[i].root); else DestroyImmediate(repulsorPulses[i].root); }
                repulsorPulses[i] = null;
            }
            if (repulsorPulseProxy) { if (Application.isPlaying) Destroy(repulsorPulseProxy); else DestroyImmediate(repulsorPulseProxy); }
            if (ownedRepulsorPulseMaterial) { if (Application.isPlaying) Destroy(ownedRepulsorPulseMaterial); else DestroyImmediate(ownedRepulsorPulseMaterial); }
            if (ownedDetonationMaterial) { if (Application.isPlaying) Destroy(ownedDetonationMaterial); else DestroyImmediate(ownedDetonationMaterial); }
            ownedDetonationMaterial = null;
            repulsorPulseProxy = null; ownedRepulsorPulseMaterial = null;
            repulsorPulseMaterialResolved = false; nextRepulsorPulse = 0;
        }
    }
}
