using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Player
{
    public enum RepulsorVisualTreatment { VolumetricPulse, PlasmaRing, Off }

    public sealed partial class PlayerMeleePlasma
    {
        [Header("Third stage — outward repulsor pulse")]
        public RepulsorVisualTreatment repulsorTreatment = RepulsorVisualTreatment.VolumetricPulse;
        [Tooltip("Optional. An included shader supplies the pulse when this is empty.")]
        public Material repulsorPulseMaterial;
        [Range(.035f, .5f)] public float repulsorPulseWidth = .16f;
        [Range(.025f, .6f)] public float repulsorPulseThickness = .18f;
        [Range(.1f, 6)] public float repulsorPulseDensity = 2.4f;
        [Range(0, .4f)] public float repulsorPulseTurbulence = .12f;
        [Range(0, 1)] public float repulsorPulseBreakup = .7f;
        [Range(0, 5)] public float repulsorPulseFlow = 1.3f;
        [Range(0, 1)] public float repulsorPulseLinger = .28f;
        [Range(.5f, 4)] public float repulsorPulseFade = 1.25f;
        public bool repulsorPulseBlackBody = true;
        public bool repulsorPulseWhiteEdge = true;
        public bool repulsorPulseFilaments = true;
        public bool repulsorPulseWisps = true;
        [Range(0, 2)] public float repulsorPulseEdgeIntensity = .9f;
        [Range(0, 2)] public float repulsorPulseFilamentIntensity = 1f;
        [Range(0, 1)] public float repulsorPulseWispIntensity = .3f;

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
        PlayerRepulsorFeedback repulsorBodyFeedback;
        PlayerRepulsorGridPulse repulsorGridFeedback;
        static readonly int PulseOriginId = Shader.PropertyToID("_PulseOrigin");
        static readonly int PulseBoundsId = Shader.PropertyToID("_PulseBounds");
        static readonly int PulseShapeId = Shader.PropertyToID("_PulseShape");
        static readonly int PulseMotionId = Shader.PropertyToID("_PulseMotion");
        static readonly int PulseLayersId = Shader.PropertyToID("_PulseLayers");
        static readonly int PulseLightId = Shader.PropertyToID("_PulseLight");
        static readonly int PulsePhaseId = Shader.PropertyToID("_PulsePhase");

        bool UsesRepulsorPulse => repulsorTreatment == RepulsorVisualTreatment.VolumetricPulse &&
            (visualStyle == MeleeVisualStyle.Plasma || visualStyle == MeleeVisualStyle.SwordSlashes);
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
                (visualStyle == MeleeVisualStyle.Plasma || visualStyle == MeleeVisualStyle.SwordSlashes))
                repulsorBodyFeedback.Preview(previewClock, stage);
            else repulsorBodyFeedback.StopPreview();
        }

        void PreviewRepulsorGrid(AttackStage stage)
        {
            if (Application.isPlaying || !repulsorGridFeedback) return;
            if (stage != null && stage.StageType == AttackStageType.FinisherRepulsor &&
                (visualStyle == MeleeVisualStyle.Plasma || visualStyle == MeleeVisualStyle.SwordSlashes))
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
            if (repulsorPulseMaterial) return repulsorPulseMaterial;
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
            return origin + Vector3.up * (surfaceHeight * size);
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
                (preview && (!Mathf.Approximately(activeRepulsorPulse.size, PlayerVisualSize) || clock + .0001f < activeRepulsorPulse.lastElapsed)) ||
                (!preview && t + .001f < lastRepulsorStageT);
            if (beginning)
            {
                EndRepulsorPulseTracking();
                var pulse = AcquireRepulsorPulse(preview ? 0 : nextRepulsorPulse++ % repulsorPulses.Length);
                activeRepulsorPulse = pulse;
                pulse.stage = stage;
                pulse.born = preview ? 0 : clock - t * stage.Duration;
                pulse.preview = preview;
                pulse.size = PlayerVisualSize;
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
            if (!current.emitted && elapsed >= current.activationStart)
            {
                if (!preview && !repulsor.IsPulseActive)
                { current.lastElapsed = elapsed; current.renderer.enabled = false; return; }
                // Capture a world frame exactly when the stage releases its force.
                // Direct scrubs must sample the launch pose, not the later body kick.
                if (preview && repulsorBodyFeedback) repulsorBodyFeedback.Preview(current.activationStart, stage);
                current.size = PlayerVisualSize;
                current.origin = RepulsorVisualOrigin(current.size);
                current.startRadius = RepulsorOutlineRadius(preview ? stage : null);
                current.maximumRadius = Mathf.Max(current.startRadius, stage.RepulsorMaxRadius * current.size);
                if (!preview)
                {
                    current.origin = repulsor.OriginWorld + Vector3.up * (surfaceHeight * current.size);
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
                { pulse.alive = false; if (pulse.renderer) pulse.renderer.enabled = false; }
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
            float linger = Mathf.Max(0, repulsorPulseLinger);
            if (!pulse.emitted || elapsed < pulse.activationStart)
            { pulse.renderer.enabled = false; return; }
            if (elapsed >= stop + linger)
            { pulse.alive = false; pulse.renderer.enabled = false; return; }
            float fade = Mathf.Clamp01((elapsed - stop) / Mathf.Max(.001f, linger));
            float onset = Mathf.SmoothStep(0, 1, (elapsed - pulse.activationStart) / .025f);
            float opacity = onset * (1 - Mathf.SmoothStep(0, 1, Mathf.Pow(fade, Mathf.Max(.5f, repulsorPulseFade))));
            bool layers = repulsorPulseBlackBody || repulsorPulseWhiteEdge || repulsorPulseFilaments || repulsorPulseWisps;
            Material material = ResolveRepulsorPulseMaterial();
            pulse.renderer.enabled = layers && opacity > .001f && material;
            if (!pulse.renderer.enabled) return;
            float size = Mathf.Max(.001f, pulse.size);
            float radius = float.IsPositiveInfinity(pulse.stopTime) ? RepulsorPulseRadius(pulse, elapsed) : pulse.stoppedRadius;
            if (!pulse.preview && pulse.tracking && pulse == activeRepulsorPulse && repulsor && repulsor.IsPulseActive)
                radius = repulsor.RadiusWorld;
            pulse.lastRadius = radius;
            float width = Mathf.Max(.035f, repulsorPulseWidth) * size;
            float height = Mathf.Max(.025f, repulsorPulseThickness) * size;
            Vector3 bounds = new Vector3(radius + width * 2.5f, height * 2.5f, radius + width * 2.5f);
            pulse.root.transform.SetPositionAndRotation(pulse.origin, Quaternion.identity);
            pulse.root.transform.localScale = bounds;
            var p = pulse.properties;
            p.SetVector(PulseOriginId, pulse.origin);
            p.SetVector(PulseBoundsId, bounds);
            p.SetVector(PulseShapeId, new Vector4(radius, width, height, size));
            p.SetVector(PulseMotionId, new Vector4((elapsed - pulse.activationStart) * repulsorPulseFlow,
                pulse.seed * 6.2831853f, Mathf.Clamp01(repulsorPulseTurbulence), Mathf.Clamp01(repulsorPulseBreakup)));
            p.SetVector(PulseLayersId, new Vector4(repulsorPulseBlackBody ? 1 : 0, repulsorPulseWhiteEdge ? 1 : 0,
                repulsorPulseFilaments ? 1 : 0, repulsorPulseWisps ? 1 : 0));
            p.SetVector(PulseLightId, new Vector4(Mathf.Max(.1f, repulsorPulseDensity) / size,
                repulsorPulseEdgeIntensity, repulsorPulseFilamentIntensity, repulsorPulseWispIntensity));
            p.SetVector(PulsePhaseId, new Vector4(opacity, Mathf.Clamp01((elapsed - pulse.activationStart) /
                Mathf.Max(.001f, pulse.activationEnd - pulse.activationStart)), fade, 0));
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
            repulsorPulses[index] = pulse;
            return pulse;
        }

        void HideRepulsorPulses()
        {
            foreach (var pulse in repulsorPulses)
                if (pulse != null) { pulse.alive = pulse.tracking = false; if (pulse.renderer) pulse.renderer.enabled = false; }
            activeRepulsorPulse = null; lastRepulsorStageT = -1;
        }

        void ReleaseRepulsorPulses()
        {
            HideRepulsorPulses();
            for (int i = 0; i < repulsorPulses.Length; i++)
            {
                if (repulsorPulses[i] != null && repulsorPulses[i].root)
                { if (Application.isPlaying) Destroy(repulsorPulses[i].root); else DestroyImmediate(repulsorPulses[i].root); }
                repulsorPulses[i] = null;
            }
            if (repulsorPulseProxy) { if (Application.isPlaying) Destroy(repulsorPulseProxy); else DestroyImmediate(repulsorPulseProxy); }
            if (ownedRepulsorPulseMaterial) { if (Application.isPlaying) Destroy(ownedRepulsorPulseMaterial); else DestroyImmediate(ownedRepulsorPulseMaterial); }
            repulsorPulseProxy = null; ownedRepulsorPulseMaterial = null;
            repulsorPulseMaterialResolved = false; nextRepulsorPulse = 0;
        }
    }
}
