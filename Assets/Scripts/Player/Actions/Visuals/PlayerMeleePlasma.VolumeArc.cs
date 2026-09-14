using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Player
{
    public enum ArcSweepTreatment { AssetPrefab, Volumetric }

    public sealed partial class PlayerMeleePlasma
    {
        [Header("Second arc — comparison")]
        public ArcSweepTreatment arcSweepTreatment = ArcSweepTreatment.Volumetric;
        public Material volumetricArcMaterial;
        [Range(.5f, 1.5f)] public float volumeReachScale = 1f;
        [Range(60, 300)] public float volumeArcDegrees = 235f;
        [Range(.05f, .8f)] public float volumeRadialWidth = .28f;
        [Range(.03f, 1)] public float volumeThickness = .3f;
        [Range(.1f, 6)] public float volumeDensity = 2.8f;
        [Range(.3f, 3)] public float volumeTaper = .8f;
        [Range(0, .3f)] public float volumeTurbulence = .075f;
        [Range(1, 12)] public float volumeNoiseScale = 4.5f;
        [Range(0, 5)] public float volumeFlowSpeed = 1.15f;
        public bool volumeBlackBody = true;
        public bool volumeWhiteEdge = true;
        public bool volumeFilaments = true;
        public bool volumeSatelliteWisp = true;
        [Range(0, 3)] public float volumeEdgeIntensity = .9f;
        [Range(0, 2)] public float volumeFilamentIntensity = .6f;

        [Header("Travelling tendrils and world-space aftermath")]
        [Range(2, 10)] public int volumeTendrilCount = 4;
        [Range(0, 6)] public int volumeExtraTendrils = 3;
        [Range(.12f, 1)] public float volumeTravelDuration = .30f;
        [Range(0, .12f)] public float volumeEmissionSpacing = .028f;
        [Range(.08f, .8f)] public float volumeTailLength = .40f;
        [Range(0, 1.5f)] public float volumeBranching = .8f;
        [Range(.25f, 6)] public float volumeConvergenceRate = 2.2f;
        [Range(0, 1.5f), Tooltip("Visual lifetime AFTER the collider window. Does not extend damage.")]
        public float volumeLingerSeconds = .55f;
        [Range(.5f, 4)] public float volumeDissolve = 1.5f;
        public bool volumeSheath = true;
        public bool volumeCounterflow = true;
        public bool volumeWake = true;
        [Range(0, 1.5f)] public float volumeSheathIntensity = .55f;
        [Range(0, 1.5f)] public float volumeCounterflowIntensity = .45f;
        [Range(0, 1.5f)] public float volumeWakeIntensity = .35f;

        [Header("Organic volume and local energy crackle")]
        public bool volumeOrganicGlow = true;
        [Range(0, 1)] public float volumeGlowBreakup = .7f;
        [Range(0, 1)] public float volumeGlowDepth = .65f;
        [Range(0, 4)] public float volumeGlowFlow = 1.3f;
        public bool volumeCrackle = true;
        [Range(0, 1)] public float volumeCrackleAmount = .35f;
        [Range(2, 30)] public float volumeCrackleScale = 12f;
        [Range(0, 25)] public float volumeCrackleRate = 9f;

        // Fixed, bounded pool: earlier world-space trails can coexist with the next attack.
        // Objects have no parent/collider, and all are owned and released by this component.
        sealed class VolumeEmission
        {
            public GameObject root;
            public MeshRenderer renderer;
            public MaterialPropertyBlock properties;
            public Vector3 origin;
            public Quaternion facing;
            public float reach, born, activationStart, activationEnd, noiseSeed;
            public int handedness;
            public bool alive, anchored;
            public readonly Vector3[] centres = new Vector3[32];
            public readonly Quaternion[] headings = new Quaternion[32];
            public readonly Vector4[] path = new Vector4[32];
            public int committed;
            public float previousElapsed, stageDuration;
            public Vector3 previousCentre;
            public Quaternion previousHeading;
        }
        readonly VolumeEmission[] volumeEmissions = new VolumeEmission[4];
        Mesh volumeProxy;
        VolumeEmission activeVolume;
        AttackStage volumeLastStage;
        float volumeLastStageT = -1;
        int nextVolumeEmission;

        bool VolumeArcIsRendering
        {
            get
            {
                foreach (var e in volumeEmissions)
                    if (e != null && e.renderer && e.renderer.enabled) return true;
                return false;
            }
        }
        bool UsesVolumeSweep => visualStyle == MeleeVisualStyle.SwordSlashes && arcSweepTreatment == ArcSweepTreatment.Volumetric;
        public bool IsVolumeSweepPreview => UsesVolumeSweep && PreviewAttackStage != null &&
            PreviewAttackStage.StageType == AttackStageType.ComboSwipe;
        public bool IsThrustTrailPreview => visualStyle == MeleeVisualStyle.SwordSlashes && PreviewAttackStage != null &&
            PreviewAttackStage.StageType == AttackStageType.PrimaryLunge;
        float PreviewAftermathStart => PreviewAttackStage == null ? 0 : IsThrustTrailPreview ?
            PreviewAttackStage.Duration : PreviewAttackStage.ActivationEndNormalized * PreviewAttackStage.Duration;
        float PreviewLinger => IsThrustTrailPreview ? Mathf.Max(0, thrustLingerSeconds) : Mathf.Max(0, volumeLingerSeconds);
        float PreviewVisualDuration => PreviewAttackStage == null ? 0 :
            IsVolumeSweepPreview || IsThrustTrailPreview ? Mathf.Max(PreviewAttackStage.Duration, PreviewAftermathStart + PreviewLinger) :
                PreviewAttackStage.Duration;
        public float PreviewAftermathNormalizedTime => IsVolumeSweepPreview || IsThrustTrailPreview ?
            Mathf.Clamp01((previewClock - PreviewAftermathStart) / Mathf.Max(.0001f, PreviewLinger)) : 0;

        public void ScrubPreviewAftermath(int stageIndex, float normalizedTime)
        {
            if (Application.isPlaying) return;
            if (!PreviewActive || PreviewStage != stageIndex) Hide();
            ResolveReferences();
            PreviewStage = stageIndex;
            PreviewActive = true; PreviewAnimating = PreviewLoop = false;
            var stage = PreviewAttackStage;
            previewClock = stage == null ? 0 : PreviewAftermathStart + Mathf.Clamp01(normalizedTime) * PreviewLinger;
            PreviewNormalizedTime = stage == null ? 0 : Mathf.Clamp01(previewClock / stage.Duration);
            UpdatePreviewVisual();
        }

        static readonly int VolumeBoundsId = Shader.PropertyToID("_VolumeBounds");
        static readonly int WorldToVolumeId = Shader.PropertyToID("_WorldToVolume");
        static readonly int VolumeToWorldId = Shader.PropertyToID("_VolumeToWorld");
        static readonly int ArcShapeId = Shader.PropertyToID("_ArcShape");
        static readonly int ArcMotionId = Shader.PropertyToID("_ArcMotion");
        static readonly int ArcLayersId = Shader.PropertyToID("_ArcLayers");
        static readonly int ArcLightId = Shader.PropertyToID("_ArcLight");
        static readonly int ArcPhaseId = Shader.PropertyToID("_ArcPhase");
        static readonly int ArcTransportId = Shader.PropertyToID("_ArcTransport");
        static readonly int ArcTendrilsId = Shader.PropertyToID("_ArcTendrils");
        static readonly int ArcPathId = Shader.PropertyToID("_ArcPath");
        static readonly int ArcPathCountId = Shader.PropertyToID("_ArcPathCount");
        static readonly int VolumeBoundsCenterId = Shader.PropertyToID("_VolumeBoundsCenter");
        static readonly int ArcWeightId = Shader.PropertyToID("_ArcWeight");
        static readonly int ArcOrganicId = Shader.PropertyToID("_ArcOrganic");
        static readonly int ArcCrackleId = Shader.PropertyToID("_ArcCrackle");

        Vector3 VolumeOrigin
        {
            get
            {
                return transform.position + Vector3.up * surfaceHeight;
            }
        }
        float VolumePhysicalReach()
        {
            if (!meleeExtent) return 1.725f;
            Vector3 axis = meleeExtent.direction == 0 ? Vector3.right : meleeExtent.direction == 1 ? Vector3.up : Vector3.forward;
            Vector3 outer = meleeExtent.transform.TransformPoint(meleeExtent.center) +
                meleeExtent.transform.TransformVector(axis * Mathf.Max(meleeExtent.radius, meleeExtent.height * .5f));
            Vector3 delta = outer - transform.position; delta.y = 0;
            return delta.magnitude;
        }

        void RenderVolumeArc(AttackStage stage, float t, Vector3 direction, int swipeSign, float clock, bool preview)
        {
            if (!volumetricArcMaterial) { HideVolumeArc(); return; }
            bool beginning = activeVolume == null || (!preview && (volumeLastStage != stage || t + .001f < volumeLastStageT));
            if (beginning)
            {
                EndVolumeAttackTracking();
                activeVolume = AcquireVolumeEmission(preview ? 0 : nextVolumeEmission++ % volumeEmissions.Length);
                var e = activeVolume;
                e.facing = VolumeEmitterHeading(preview, direction);
                e.origin = VolumeOrigin; e.reach = VolumePhysicalReach();
                e.born = preview ? 0 : clock - t * stage.Duration;
                // Stable during this emission, including preview scrubs; no global Random state.
                e.noiseSeed = preview ? .381966f : Mathf.Repeat(nextVolumeEmission * .61803398875f, 1f);
                e.handedness = swipeSign < 0 ? -1 : 1;
                e.anchored = false;
                e.alive = true;
                e.committed = -1;
                e.previousElapsed = preview ? 0 : t * stage.Duration;
                e.previousCentre = VolumeOrigin; e.previousHeading = e.facing;
            }
            activeVolume.activationStart = stage.ActivationStartNormalized * stage.Duration;
            activeVolume.activationEnd = stage.ActivationEndNormalized * stage.Duration;
            activeVolume.stageDuration = stage.Duration;
            float elapsed = preview ? clock : clock - activeVolume.born;
            RecordVolumePath(activeVolume, elapsed, VolumeOrigin, VolumeEmitterHeading(preview, direction));
            activeVolume.anchored |= t >= stage.ActivationStartNormalized;
            volumeLastStage = stage; volumeLastStageT = t;
            UpdateVolumeEmission(activeVolume, elapsed);
        }

        Quaternion VolumeEmitterHeading(bool preview, Vector3 fallback)
        {
            // Only NEW path points use current aim. Weapon yaw is already represented by
            // advancing along the arc, so applying it here would rotate the sweep twice.
            Vector3 direction = preview ?
                (playerVisuals && playerVisuals.gameplayFacing ? playerVisuals.gameplayFacing.right : transform.right) :
                (attackController ? attackController.CombatFacingDirectionWS : fallback);
            direction.y = 0;
            return Quaternion.LookRotation(direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.right, Vector3.up);
        }

        void RecordVolumePath(VolumeEmission e, float elapsed, Vector3 centre, Quaternion heading)
        {
            // Rewinding a preview starts a new deterministic emission history. Once a point
            // has formed, subsequent player movement cannot rewrite its position or heading.
            if (elapsed + .0001f < e.previousElapsed)
            { e.committed = -1; e.previousElapsed = 0; e.previousCentre = centre; e.previousHeading = heading; }
            if (e.previousElapsed >= e.stageDuration)
            { centre = e.previousCentre; heading = e.previousHeading; }
            float duration = Mathf.Max(.12f, volumeTravelDuration);
            float progress = Mathf.Clamp01((elapsed - e.activationStart) / duration);
            int reached = elapsed < e.activationStart ? -1 : Mathf.FloorToInt(progress * 31);
            for (int i = e.committed + 1; i < 32; i++)
            {
                float when = e.activationStart + i / 31f * duration;
                float blend = Mathf.Clamp01((when - e.previousElapsed) / Mathf.Max(.0001f, elapsed - e.previousElapsed));
                e.centres[i] = i <= reached ? Vector3.Lerp(e.previousCentre, centre, blend) : centre;
                e.headings[i] = i <= reached ? Quaternion.Slerp(e.previousHeading, heading, blend) : heading;
            }
            e.committed = Mathf.Max(e.committed, reached);
            e.previousElapsed = elapsed; e.previousCentre = centre; e.previousHeading = heading;
        }

        void EndVolumeAttackTracking()
        {
            // A cancelled windup has emitted nothing and must never wake up later.
            if (activeVolume != null && !activeVolume.anchored)
            { activeVolume.alive = false; if (activeVolume.renderer) activeVolume.renderer.enabled = false; }
            activeVolume = null; volumeLastStage = null; volumeLastStageT = -1;
        }
        void TickVolumeAftermath(float clock)
        {
            if (!UsesVolumeSweep || !volumetricArcMaterial) { HideVolumeArc(); return; }
            foreach (var e in volumeEmissions)
                if (e != null && e.alive) UpdateVolumeEmission(e, clock - e.born);
        }
        void UpdateVolumeEmission(VolumeEmission e, float elapsed)
        {
            float linger = Mathf.Max(0, volumeLingerSeconds);
            float aftermath = elapsed <= e.activationEnd ? 0 : Mathf.Clamp01((elapsed - e.activationEnd) / Mathf.Max(.0001f, linger));
            float forming = Mathf.Clamp01(elapsed / Mathf.Max(.001f, e.activationStart));
            float opacity = elapsed < e.activationStart ? windupOpacity * Mathf.SmoothStep(0, 1, forming) :
                1 - Mathf.SmoothStep(0, 1, Mathf.Pow(aftermath, Mathf.Max(.5f, volumeDissolve)));
            if (elapsed >= e.activationEnd + linger || !volumetricArcMaterial)
            {
                e.renderer.enabled = false; e.alive = false; return;
            }
            // Scrubbing back after the visual end revives this same emission without moving it.
            e.alive = true;
            bool layers = volumeBlackBody || volumeWhiteEdge || volumeFilaments || volumeSatelliteWisp ||
                (volumeSheath && volumeSheathIntensity > 0) || (volumeCounterflow && volumeCounterflowIntensity > 0) || (volumeWake && volumeWakeIntensity > 0);
            e.renderer.enabled = layers && opacity > .001f;
            if (!e.renderer.enabled) return;

            float radius = Mathf.Max(.2f, e.reach * volumeReachScale);
            float width = Mathf.Clamp(volumeRadialWidth, .05f, radius * .75f);
            float thickness = Mathf.Max(.03f, volumeThickness);
            float turbulence = Mathf.Clamp(volumeTurbulence, 0, .3f);
            var frame = Matrix4x4.TRS(e.origin, e.facing, Vector3.one);
            var inverseFrame = frame.inverse;
            Vector3 min = new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity), max = -min;
            for (int i = 0; i < 32; i++)
            {
                float along = i / 31f;
                float angle = (along - .5f) * Mathf.Clamp(volumeArcDegrees, 60, 300) * Mathf.Deg2Rad * e.handedness;
                Vector3 radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                Vector3 point = inverseFrame.MultiplyPoint3x4(e.centres[i] + e.headings[i] * radial * radius);
                e.path[i] = new Vector4(point.x, point.y, point.z, along);
                min = Vector3.Min(min, point); max = Vector3.Max(max, point);
            }
            Vector3 boundsCentre = (min + max) * .5f;
            Vector3 bounds = (max - min) * .5f + new Vector3(width * 2.5f + turbulence * 2 + .1f,
                thickness + turbulence + .04f, width * 2.5f + turbulence * 2 + .1f);
            e.root.transform.SetPositionAndRotation(frame.MultiplyPoint3x4(boundsCentre), e.facing);
            e.root.transform.localScale = bounds;
            var p = e.properties;
            p.SetMatrix(WorldToVolumeId, inverseFrame);
            p.SetMatrix(VolumeToWorldId, frame);
            p.SetVector(VolumeBoundsId, bounds);
            p.SetVector(VolumeBoundsCenterId, boundsCentre);
            p.SetVectorArray(ArcPathId, e.path);
            p.SetFloat(ArcPathCountId, 32);
            p.SetVector(ArcWeightId, new Vector4(volumeSheath ? volumeSheathIntensity : 0,
                volumeCounterflow ? volumeCounterflowIntensity : 0, volumeWake ? volumeWakeIntensity : 0, 0));
            p.SetVector(ArcOrganicId, new Vector4(volumeOrganicGlow ? Mathf.Clamp01(volumeGlowBreakup) : 0,
                volumeOrganicGlow ? Mathf.Clamp01(volumeGlowDepth) : 0, elapsed * Mathf.Clamp(volumeGlowFlow, 0, 4), e.noiseSeed));
            p.SetVector(ArcCrackleId, new Vector4(volumeCrackle ? Mathf.Clamp01(volumeCrackleAmount) : 0,
                Mathf.Clamp(volumeCrackleScale, 2, 30), elapsed * Mathf.Clamp(volumeCrackleRate, 0, 25), 0));
            p.SetVector(ArcShapeId, new Vector4(radius, width, thickness, Mathf.Clamp(volumeArcDegrees, 60, 300) * Mathf.Deg2Rad * .5f));
            p.SetVector(ArcMotionId, new Vector4(turbulence, Mathf.Max(1, volumeNoiseScale), elapsed * volumeFlowSpeed, e.handedness));
            p.SetVector(ArcLayersId, new Vector4(volumeBlackBody ? 1 : 0, volumeWhiteEdge ? 1 : 0, volumeFilaments ? 1 : 0, volumeSatelliteWisp ? 1 : 0));
            p.SetVector(ArcLightId, new Vector4(Mathf.Max(.1f, volumeDensity), volumeEdgeIntensity, volumeFilamentIntensity, Mathf.Max(.3f, volumeTaper)));
            p.SetVector(ArcPhaseId, new Vector4(opacity, forming, aftermath, 0));
            p.SetVector(ArcTransportId, new Vector4(elapsed, e.activationStart, Mathf.Max(.12f, volumeTravelDuration), Mathf.Max(0, volumeEmissionSpacing)));
            int tendrils = Mathf.Min(12, Mathf.Clamp(volumeTendrilCount, 2, 10) + Mathf.Clamp(volumeExtraTendrils, 0, 6));
            p.SetVector(ArcTendrilsId, new Vector4(tendrils, Mathf.Clamp(volumeTailLength, .08f, .8f), Mathf.Max(0, volumeBranching), Mathf.Max(.25f, volumeConvergenceRate)));
            e.renderer.sharedMaterial = volumetricArcMaterial;
            e.renderer.SetPropertyBlock(p);
        }

        VolumeEmission AcquireVolumeEmission(int index)
        {
            var e = volumeEmissions[index];
            if (e != null && e.root) return e;
            if (!volumeProxy)
            {
                volumeProxy = new Mesh { name = "Arc volume bounds", hideFlags = HideFlags.HideAndDontSave };
                volumeProxy.vertices = new[] { new Vector3(-1,-1,-1), new Vector3(1,-1,-1), new Vector3(1,1,-1), new Vector3(-1,1,-1),
                    new Vector3(-1,-1,1), new Vector3(1,-1,1), new Vector3(1,1,1), new Vector3(-1,1,1) };
                volumeProxy.triangles = new[] { 0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,3,7,6,3,6,2,0,1,5,0,5,4 };
                volumeProxy.RecalculateBounds();
            }
            e = new VolumeEmission();
            e.root = new GameObject("Melee 3D arc volume (temporary " + index + ")") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            e.root.AddComponent<MeshFilter>().sharedMesh = volumeProxy;
            e.renderer = e.root.AddComponent<MeshRenderer>();
            e.renderer.enabled = false;
            e.renderer.shadowCastingMode = ShadowCastingMode.Off;
            e.renderer.receiveShadows = false;
            e.renderer.lightProbeUsage = LightProbeUsage.Off;
            e.renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            e.properties = new MaterialPropertyBlock();
            volumeEmissions[index] = e;
            return e;
        }

        void HideVolumeArc()
        {
            foreach (var e in volumeEmissions)
                if (e != null) { if (e.renderer) e.renderer.enabled = false; e.alive = false; }
            EndVolumeAttackTracking();
        }
        void ReleaseVolumeArc()
        {
            HideVolumeArc();
            for (int i = 0; i < volumeEmissions.Length; i++)
            {
                var e = volumeEmissions[i];
                if (e != null && e.root) { if (Application.isPlaying) Destroy(e.root); else DestroyImmediate(e.root); }
                volumeEmissions[i] = null;
            }
            if (volumeProxy) { if (Application.isPlaying) Destroy(volumeProxy); else DestroyImmediate(volumeProxy); }
            volumeProxy = null; nextVolumeEmission = 0;
        }
    }
}
