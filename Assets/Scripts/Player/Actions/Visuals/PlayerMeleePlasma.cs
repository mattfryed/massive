using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Player
{
    public enum MeleeVisualStyle { OriginalParticles, Plasma, Off, SwordSlashes }

    /// <summary>Attack presentation only. Samples the controller; never starts attacks or changes hitboxes.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(10000)]
    [AddComponentMenu("MASSIVE/Player/Melee Plasma")]
    public sealed partial class PlayerMeleePlasma : MonoBehaviour
    {
        [Header("Treatment")]
        public MeleeVisualStyle visualStyle = MeleeVisualStyle.Plasma;
        [Tooltip("Shared asset. Per-player settings use a property block, not material instances.")]
        public Material plasmaMaterial;
        public PlayerAttackController attackController;
        [Tooltip("Used only if no controller profile is available for edit preview.")]
        public PlayerAttackProfile previewProfile;
        [Tooltip("The existing sword capsule supplies physical reach and thickness. No collider is changed.")]
        public CapsuleCollider meleeExtent;
        [Tooltip("Only these melee particle renderers are hidden for Plasma / Off. If empty, finds particles under Sword Arc Driver and Repulsor AOE only. Power-up particles stay available.")]
        public ParticleSystemRenderer[] legacyMeleeParticles = new ParticleSystemRenderer[0];

        [Header("Layers — independent toggles")]
        public bool solidBody = true;
        public bool brightRim = true;
        public bool flowingFilaments = true;
        public bool softHalo = true;
        [Range(0, 1)] public float bodyOpacity = 1f;
        [Range(0, 1)] public float rimIntensity = .95f;
        [Range(0, 1)] public float filamentIntensity = .8f;
        [Range(0, 1)] public float haloIntensity = .08f;
        [Range(.01f, .18f)] public float rimWidth = .045f;

        [Header("Plasma motion")]
        [Range(0, 1)] public float edgeWarble = .3f;
        [Range(.25f, 12)] public float noiseScale = 3f;
        [Range(0, 5)] public float flowSpeed = 1f;

        [Header("Shape — visual tuning only")]
        [Tooltip("1 matches the sword capsule's outer reach. Does not change attack range.")]
        [Range(.5f, 1.5f)] public float reachScale = 1f;
        [Range(.3f, 2)] public float widthScale = 1f;
        [Tooltip("Curvature behind the live sword direction during a swipe; degrees.")]
        [Range(0, 65)] public float sweepBend = 32f;
        [Range(.02f, .5f)] public float rootOffset = .22f;
        [Range(0, .3f)] public float surfaceHeight = .06f;
        [Range(.025f, .4f)] public float repulsorBandWidth = .14f;

        [Header("Attack phases")]
        [Tooltip("Dim forming blade before the existing damage window opens.")]
        [Range(0, .6f)] public float windupOpacity = .22f;
        [Tooltip("Faint receding energy after the damage window closes.")]
        [Range(0, .6f)] public float recoveryOpacity = .16f;

        [Header("Edit preview — animation rate, not gameplay speed")]
        [Range(.1f, 2)] public float previewPlaybackSpeed = .35f;
        [Min(0)] public float previewRepeatDelay = .45f;

        public int PreviewStage { get; private set; }
        public float PreviewNormalizedTime { get; private set; }
        public bool PreviewActive { get; private set; }
        public bool PreviewAnimating { get; private set; }
        public bool PreviewLoop { get; private set; }
        public bool HasRepulsor => repulsor != null;
        public string PreviewPhase
        {
            get
            {
                if (!PreviewActive) return "Stopped";
                var stage = PreviewAttackStage;
                if ((IsVolumeSweepPreview || IsThrustTrailPreview) && previewClock > PreviewAftermathStart)
                    return "World-space trail — no hitbox";
                if (stage == null) return "No attack profile";
                return PreviewNormalizedTime < stage.ActivationStartNormalized ? "Windup" :
                    PreviewNormalizedTime <= stage.ActivationEndNormalized ? "Active" : "Recovery";
            }
        }
        public bool IsRendering => (ribbonRenderer && ribbonRenderer.enabled) || PrefabEffectIsRendering || VolumeArcIsRendering || ThrustTrailIsRendering;

        PlayerControllerScript owner;
        PlayerVisualController playerVisuals;
        AttackTrailGPU legacyTrail;
        PlayerRepulsorAOE repulsor;
        SphereCollider repulsorCollider;
        bool[] savedParticleVisibility;
        ParticleSystemRenderer[] suppressedRenderers;
        bool suppressing;
        bool visibilityDirty;
        ParticleSystemRenderer[] detectedMeleeParticles;
        GameObject ribbonObject;
        Mesh ribbonMesh;
        MeshRenderer ribbonRenderer;
        MaterialPropertyBlock properties;
        const int Segments = 96;
        readonly Vector3[] vertices = new Vector3[(Segments + 1) * 2];
        readonly Vector2[] uvs = new Vector2[(Segments + 1) * 2];
        readonly int[] triangles = new int[Segments * 6];
        float previewClock;
#if UNITY_EDITOR
        double lastEditorTime;
#endif
        PlayerAttackProfile Profile => attackController && attackController.Profile ? attackController.Profile : previewProfile;
        AttackStage PreviewAttackStage => Profile ? Profile.GetStage(PreviewStage) : null;

        void OnEnable()
        {
            ResolveReferences();
            ApplyLegacyVisibility();
#if UNITY_EDITOR
            lastEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.update += EditorTick;
            UnityEditor.EditorApplication.playModeStateChanged += PlayModeChanged;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
#endif
        }

        void ResolveReferences()
        {
            if (!attackController) attackController = GetComponent<PlayerAttackController>();
            owner = GetComponent<PlayerControllerScript>();
            playerVisuals = GetComponent<PlayerVisualController>();
            legacyTrail = GetComponent<AttackTrailGPU>();
            repulsor = GetComponentInChildren<PlayerRepulsorAOE>(true);
            if (repulsor) repulsorCollider = repulsor.GetComponent<SphereCollider>();
            if (!meleeExtent)
                foreach (var melee in GetComponentsInChildren<PlayerMelee>(true))
                {
                    meleeExtent = melee.GetComponent<CapsuleCollider>();
                    if (meleeExtent) break;
                }
            var particles = new System.Collections.Generic.List<ParticleSystemRenderer>();
            foreach (var driver in GetComponentsInChildren<AttackSwordArcDriver>(true))
                particles.AddRange(driver.GetComponentsInChildren<ParticleSystemRenderer>(true));
            if (repulsor) particles.AddRange(repulsor.GetComponentsInChildren<ParticleSystemRenderer>(true));
            detectedMeleeParticles = particles.ToArray();
        }

        void OnValidate() { visibilityDirty = true; }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
            UnityEditor.EditorApplication.playModeStateChanged -= PlayModeChanged;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
#endif
            StopPreview();
            RestoreLegacyVisibility();
            ReleaseGraphics();
        }

        void LateUpdate()
        {
            ApplyLegacyVisibility();
            if (!Application.isPlaying) return;
            if (PreviewActive) StopPreview();
            if ((visualStyle != MeleeVisualStyle.Plasma && visualStyle != MeleeVisualStyle.SwordSlashes) || !attackController || !attackController.isActiveAndEnabled ||
                (owner && owner.temporarilyEliminated))
            { Hide(); return; }
            if (!attackController.IsAttacking || attackController.CurrentStage == null || attackController.CurrentStage.StageType != AttackStageType.ComboSwipe)
                EndVolumeAttackTracking();
            if (!attackController.IsAttacking || attackController.CurrentStage == null || attackController.CurrentStage.StageType != AttackStageType.PrimaryLunge)
                EndThrustAttackTracking();
            TickVolumeAftermath(Time.time);
            TickThrustAftermath(Time.time);
            if (!attackController.IsAttacking)
            { HideNonVolume(); return; }
            RenderStage(attackController.CurrentStage, attackController.StageNormalizedTime,
                attackController.CurrentAttackVisualDirectionWS, attackController.CurrentSwipeDirection, Time.time, false);
        }

        void ApplyLegacyVisibility()
        {
            if (visibilityDirty) { RestoreLegacyVisibility(); visibilityDirty = false; }
            bool suppress = isActiveAndEnabled && visualStyle != MeleeVisualStyle.OriginalParticles &&
                (visualStyle == MeleeVisualStyle.Off || plasmaMaterial != null ||
                 (visualStyle == MeleeVisualStyle.SwordSlashes && thrustPrefabEffect.prefab && swipePrefabEffect.prefab));
            if (legacyTrail) legacyTrail.SetMeleeVisualSuppressed(suppress);
            if (suppress == suppressing) return;
            if (!suppress) { RestoreLegacyVisibility(); return; }
            var sources = legacyMeleeParticles != null && legacyMeleeParticles.Length > 0 ? legacyMeleeParticles : detectedMeleeParticles;
            suppressedRenderers = sources != null ? (ParticleSystemRenderer[])sources.Clone() : new ParticleSystemRenderer[0];
            savedParticleVisibility = new bool[suppressedRenderers.Length];
            for (int i = 0; i < suppressedRenderers.Length; i++)
                if (suppressedRenderers[i])
                {
                    savedParticleVisibility[i] = suppressedRenderers[i].forceRenderingOff;
                    suppressedRenderers[i].forceRenderingOff = true;
                }
            suppressing = true;
        }

        void RestoreLegacyVisibility()
        {
            if (legacyTrail) legacyTrail.SetMeleeVisualSuppressed(false);
            if (suppressing && suppressedRenderers != null)
                for (int i = 0; i < suppressedRenderers.Length; i++)
                    if (suppressedRenderers[i]) suppressedRenderers[i].forceRenderingOff = savedParticleVisibility[i];
            suppressing = false;
        }

        void EnsureGraphics()
        {
            if (ribbonObject) return;
            ribbonObject = new GameObject("Melee plasma (temporary visual)") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            ribbonObject.transform.SetParent(transform, false);
            ribbonMesh = new Mesh { name = "Melee plasma ribbon", hideFlags = HideFlags.HideAndDontSave };
            ribbonMesh.MarkDynamic();
            for (int i = 0; i <= Segments; i++)
            {
                uvs[i * 2] = new Vector2(i / (float)Segments, -1);
                uvs[i * 2 + 1] = new Vector2(i / (float)Segments, 1);
                if (i == Segments) continue;
                int k = i * 6, v = i * 2;
                triangles[k] = v; triangles[k + 1] = v + 1; triangles[k + 2] = v + 2;
                triangles[k + 3] = v + 1; triangles[k + 4] = v + 3; triangles[k + 5] = v + 2;
            }
            ribbonMesh.vertices = vertices; ribbonMesh.uv = uvs; ribbonMesh.triangles = triangles;
            ribbonObject.AddComponent<MeshFilter>().sharedMesh = ribbonMesh;
            ribbonRenderer = ribbonObject.AddComponent<MeshRenderer>();
            ribbonRenderer.shadowCastingMode = ShadowCastingMode.Off;
            ribbonRenderer.receiveShadows = false;
            ribbonRenderer.lightProbeUsage = LightProbeUsage.Off;
            ribbonRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            properties = new MaterialPropertyBlock();
        }

        void RenderStage(AttackStage stage, float t, Vector3 direction, int swipeSign, float clock, bool preview)
        {
            if (stage != null && visualStyle == MeleeVisualStyle.SwordSlashes && stage.StageType == AttackStageType.PrimaryLunge)
            {
                HideNonVolume();
                if (preview) HideVolumeArc();
                RenderThrustTrail(stage, t, direction, clock, preview);
                return;
            }
            if (preview || visualStyle != MeleeVisualStyle.SwordSlashes) HideThrustTrails();
            if (stage != null && visualStyle == MeleeVisualStyle.SwordSlashes && stage.StageType == AttackStageType.ComboSwipe &&
                arcSweepTreatment == ArcSweepTreatment.Volumetric)
            {
                if (ribbonRenderer) ribbonRenderer.enabled = false;
                HidePrefabEffects();
                RenderVolumeArc(stage, t, direction, swipeSign, clock, preview);
                return;
            }
            if (preview || !UsesVolumeSweep) HideVolumeArc();
            if (stage != null && visualStyle == MeleeVisualStyle.SwordSlashes && stage.StageType != AttackStageType.FinisherRepulsor)
            {
                if (ribbonRenderer) ribbonRenderer.enabled = false;
                RenderPrefabStage(stage, t, direction, swipeSign);
                return;
            }
            HidePrefabEffects();
            if (stage == null || (visualStyle != MeleeVisualStyle.Plasma && visualStyle != MeleeVisualStyle.SwordSlashes) || !plasmaMaterial) { HideNonVolume(); return; }
            if (!preview && stage.StageType == AttackStageType.FinisherRepulsor && (!repulsor || !repulsor.isActiveAndEnabled)) { HideNonVolume(); return; }
            bool ring = stage.StageType == AttackStageType.FinisherRepulsor && (preview || repulsor);
            float start = stage.ActivationStartNormalized, end = stage.ActivationEndNormalized;
            float forming = start > .0001f ? Mathf.Clamp01(t / start) : 1;
            float recovering = Mathf.Clamp01((t - end) / Mathf.Max(.001f, 1 - end));
            float opacity = t < start ? windupOpacity * forming : t <= end ? 1 : recoveryOpacity * (1 - recovering);
            if (opacity <= .001f) { HideNonVolume(); return; }
            EnsureGraphics();
            direction.y = 0;
            direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.right;
            Vector3 origin = playerVisuals && playerVisuals.visuals ? playerVisuals.visuals.position : transform.position;
            origin.y += surfaceHeight;
            float reach = 1.725f, radius = .25f;
            if (meleeExtent)
            {
                var axis = meleeExtent.direction == 0 ? Vector3.right : meleeExtent.direction == 1 ? Vector3.up : Vector3.forward;
                Vector3 center = meleeExtent.transform.TransformPoint(meleeExtent.center);
                Vector3 outer = center + meleeExtent.transform.TransformVector(axis * Mathf.Max(meleeExtent.radius, meleeExtent.height * .5f));
                var planar = outer - transform.position; planar.y = 0;
                reach = planar.magnitude;
                var scale = meleeExtent.transform.lossyScale;
                radius = meleeExtent.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            }
            reach *= reachScale;
            float extent = Mathf.Lerp(.35f, 1, forming) * (1 - .3f * recovering);
            float halfWidth = radius * widthScale / .69f;
            float activeT = Mathf.Clamp01((t - start) / Mathf.Max(.001f, end - start));
            float ringRadius = ring ? stage.RepulsorMaxRadius * Mathf.Clamp01(stage.RepulsorRadiusCurve != null ? stage.RepulsorRadiusCurve.Evaluate(activeT) : activeT) : 0;
            if (ring && !preview && repulsorCollider)
            {
                if (!repulsorCollider.enabled) { HideNonVolume(); return; }
                origin = repulsorCollider.transform.TransformPoint(repulsorCollider.center) + Vector3.up * surfaceHeight;
                ringRadius = repulsorCollider.radius * Mathf.Max(Mathf.Abs(repulsorCollider.transform.lossyScale.x), Mathf.Abs(repulsorCollider.transform.lossyScale.z));
            }
            for (int i = 0; i <= Segments; i++)
            {
                float u = i / (float)Segments;
                Vector3 center, across;
                float width;
                if (ring)
                {
                    float a = u * Mathf.PI * 2;
                    across = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    center = origin + across * ringRadius;
                    width = Mathf.Min(repulsorBandWidth * widthScale / .69f, ringRadius * .4f);
                }
                else
                {
                    float bend = stage.StageType == AttackStageType.ComboSwipe ? -swipeSign * sweepBend * Mathf.Sin(u * Mathf.PI) : 0;
                    Vector3 radial = Quaternion.AngleAxis(bend, Vector3.up) * direction;
                    center = origin + radial * Mathf.Lerp(rootOffset, Mathf.Max(rootOffset, reach * extent), u);
                    across = Vector3.Cross(Vector3.up, radial);
                    // Taper is geometric: no floating billboard or abrupt rectangular end.
                    width = halfWidth * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * u)), .6f) * Mathf.Lerp(1, .45f, u);
                }
                vertices[i * 2] = transform.InverseTransformPoint(center - across * width);
                vertices[i * 2 + 1] = transform.InverseTransformPoint(center + across * width);
            }
            ribbonMesh.vertices = vertices;
            ribbonMesh.RecalculateBounds();
            ribbonRenderer.sharedMaterial = plasmaMaterial;
            properties.SetFloat("_EffectTime", clock);
            properties.SetFloat("_Opacity", opacity);
            properties.SetFloat("_DarkTeam", owner && owner.teamID != 1 ? 1 : 0);
            properties.SetFloat("_BodyEnabled", solidBody ? 1 : 0);
            properties.SetFloat("_RimEnabled", brightRim ? 1 : 0);
            properties.SetFloat("_FilamentsEnabled", flowingFilaments ? 1 : 0);
            properties.SetFloat("_HaloEnabled", softHalo ? 1 : 0);
            properties.SetFloat("_BodyOpacity", bodyOpacity);
            properties.SetFloat("_RimIntensity", rimIntensity);
            properties.SetFloat("_FilamentIntensity", filamentIntensity);
            properties.SetFloat("_HaloIntensity", haloIntensity);
            properties.SetFloat("_RimWidth", rimWidth);
            properties.SetFloat("_WarpAmplitude", edgeWarble);
            properties.SetFloat("_NoiseScale", noiseScale);
            properties.SetFloat("_FlowSpeed", flowSpeed);
            properties.SetFloat("_ClosedLoop", ring ? 1 : 0);
            ribbonRenderer.SetPropertyBlock(properties);
            ribbonRenderer.enabled = true;
        }

        public void StartPreview(int stageIndex, bool loop)
        {
            if (Application.isPlaying) return;
            Hide();
            ResolveReferences();
            PreviewStage = stageIndex; PreviewLoop = loop;
            PreviewActive = PreviewAnimating = true;
            previewClock = 0; PreviewNormalizedTime = 0;
            UpdatePreviewVisual();
        }
        public void PausePreview() { PreviewAnimating = false; }
        public void StopPreview() { PreviewActive = PreviewAnimating = PreviewLoop = false; previewClock = 0; Hide(); }
        public void ScrubPreview(int stageIndex, float normalizedTime)
        {
            if (Application.isPlaying) return;
            if (!PreviewActive || PreviewStage != stageIndex) Hide();
            ResolveReferences(); PreviewStage = stageIndex;
            PreviewNormalizedTime = Mathf.Clamp01(normalizedTime);
            PreviewActive = true; PreviewAnimating = false;
            previewClock = PreviewAttackStage != null ? PreviewNormalizedTime * PreviewAttackStage.Duration : 0;
            UpdatePreviewVisual();
        }
        public void TickPreview(float deltaTime)
        {
            if (Application.isPlaying || !PreviewActive) return;
            var stage = PreviewAttackStage;
            if (stage == null) { StopPreview(); return; }
            if (PreviewAnimating)
            {
                previewClock += Mathf.Max(0, deltaTime) * previewPlaybackSpeed;
                if (PreviewLoop && previewClock >= PreviewVisualDuration + previewRepeatDelay)
                { previewClock = 0; Hide(); }
                PreviewNormalizedTime = Mathf.Clamp01(previewClock / stage.Duration);
                if (!PreviewLoop && previewClock >= PreviewVisualDuration) { StopPreview(); return; }
            }
            UpdatePreviewVisual();
        }
        void UpdatePreviewVisual()
        {
            ApplyLegacyVisibility();
            Vector3 direction = playerVisuals && playerVisuals.gameplayFacing ? playerVisuals.gameplayFacing.right : transform.right;
            var stage = PreviewAttackStage;
            if (stage != null && stage.StageType == AttackStageType.ComboSwipe)
                direction = Quaternion.AngleAxis(Mathf.Lerp(-stage.RotationArc * .5f, stage.RotationArc * .5f,
                    Mathf.SmoothStep(0, 1, PreviewNormalizedTime)), Vector3.up) * direction;
            RenderStage(stage, PreviewNormalizedTime, direction, 1, previewClock, true);
        }
        void HideNonVolume() { if (ribbonRenderer) ribbonRenderer.enabled = false; HidePrefabEffects(); }
        void Hide() { HideNonVolume(); HideVolumeArc(); HideThrustTrails(); }
        void ReleaseGraphics()
        {
            ReleaseThrustTrails();
            ReleaseVolumeArc();
            ReleasePrefabEffects();
            if (ribbonObject) { if (Application.isPlaying) Destroy(ribbonObject); else DestroyImmediate(ribbonObject); }
            if (ribbonMesh) { if (Application.isPlaying) Destroy(ribbonMesh); else DestroyImmediate(ribbonMesh); }
            ribbonObject = null; ribbonMesh = null; ribbonRenderer = null;
        }
#if UNITY_EDITOR
        void EditorTick()
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            float dt = (float)(now - lastEditorTime); lastEditorTime = now;
            if (Application.isPlaying || !PreviewActive || UnityEditor.EditorApplication.isCompiling) return;
            TickPreview(Mathf.Min(dt, .1f));
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
        void PlayModeChanged(UnityEditor.PlayModeStateChange state) { StopPreview(); }
#endif
    }
}
