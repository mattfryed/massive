using UnityEngine;

namespace Massive.Resonance
{
    public enum ResonanceBirthDistribution { FullField = 0, LocalBand = 1 }
    /// <summary>Optional presentation/lifecycle gate. Merely adding this component does not
    /// take over an existing prototype. BeginSpawn/BeginDespawn or SetImmediate opt in.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(ResonancePatternController))]
    [DefaultExecutionOrder(-200)]
    public sealed class ResonanceManifestation : MonoBehaviour
    {
        [Header("Sand-pattern formation")]
        [Min(0f)] public float spawnSeconds = 2.4f;
        [Min(0f)] public float despawnSeconds = 1.2f;
        [Tooltip("Lower values grow the grains earlier in the formation. Idle size is unchanged.")]
        [Range(.1f, 4f)] public float sizeGrowthPower = .65f;
        [Tooltip("Higher values keep the grains dispersed longer before they condense into the pattern.")]
        [Range(.1f, 4f)] public float condensationPower = 1.5f;
        [Header("Particle birth location")]
        [Tooltip("Local Band births each grain near its resting arc; Full Field retains the original rectangular scatter.")]
        public ResonanceBirthDistribution birthDistribution = ResonanceBirthDistribution.FullField;
        [Tooltip("Maximum initial offset from each grain's idle position, in pattern-local units. Vibration is a separate small additional motion.")]
        [Min(0f)] public float localSpawnSpread = .55f;
        [Tooltip("Fraction of Spawn Spread along the arc. Lower values favor a narrow band across its surface.")]
        [Range(0f, 1f)] public float alongArcSpread = .25f;
        [Tooltip("Blend from individual grain jitter to a shared standing-wave vibration around the pattern origin. Visual only; does not simulate or deform the grid.")]
        [Range(0f, 1f)] public float fieldCoherence = .9f;
        [Tooltip("Distance between repeating standing-wave bands, in pattern-local units.")]
        [Min(.05f)] public float fieldWavelength = 2f;
        [Tooltip("Gentle grain-size pulsing with the local standing wave during formation. Zero disables it; idle size is never changed.")]
        [Range(0f, 1f)] public float birthPulse = .15f;
        [Header("Dispersed field — pattern-local XZ")]
        public Vector2 dispersalCenter;
        public Vector2 dispersalHalfExtents = new Vector2(12f, 5f);
        [Tooltip("Local-unit vibration while condensing. Settles to zero at the exact idle position.")]
        [Min(0f)] public float vibrationStrength = .16f;
        [Min(0f)] public float vibrationFrequency = 10f;
        public bool showDispersalArea = true;

        [Header("Scene authoring")]
        [Tooltip("Optional prefab destination for the Inspector's explicit Save/Load Animation Tuning buttons. This link never changes gameplay or saves automatically.")]
        public ResonanceManifestation spawnPrefabTuningTarget;

        [System.NonSerialized] private ResonancePatternController pattern;
        [System.NonSerialized] private bool controlled, held;
        [System.NonSerialized] private float progress = 1f, vibrationClock;
        [System.NonSerialized] private int direction;
#if UNITY_EDITOR
        [System.NonSerialized] private double editorClock;
#endif
        public bool IsControlled => controlled;
        public bool IsIdle => !controlled || (progress >= 1f && direction == 0);
        public bool IsHidden => controlled && progress <= 0f && direction == 0;
        public bool IsTransitioning => (controlled && progress > 0f && progress < 1f) || direction != 0;
        public bool IsAnimating => controlled && direction != 0 && !held;
        public float NormalizedProgress => progress;

        private void OnEnable()
        {
            EnsurePattern();
#if UNITY_EDITOR
            editorClock = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.update -= EditorTick;
            UnityEditor.EditorApplication.update += EditorTick;
            UnityEditor.EditorApplication.playModeStateChanged -= EditorPlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += EditorPlayModeChanged;
#endif
            if (controlled) Apply();
        }
        private void OnValidate()
        {
            dispersalHalfExtents = new Vector2(Mathf.Max(.01f, dispersalHalfExtents.x), Mathf.Max(.01f, dispersalHalfExtents.y));
            // Delay component/presentation work to Update; OnValidate can run on an import thread.
        }
        private void Update()
        {
            if (!controlled) return;
            // ExecuteAlways Update is change-driven in Edit Mode, not a reliable animation clock.
            if (Application.isPlaying) Advance(Time.deltaTime);
            else Apply();
        }
        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
            UnityEditor.EditorApplication.playModeStateChanged -= EditorPlayModeChanged;
#endif
        }
#if UNITY_EDITOR
        private void EditorTick()
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now - editorClock < 1.0 / 30.0) return;
            float dt = (float)System.Math.Max(0, now - editorClock);
            editorClock = now;
            if (Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode
                || !isActiveAndEnabled || !gameObject.scene.IsValid() || !IsAnimating) return;
            Advance(dt);
            RepaintEditorPreview();
        }
        private void EditorPlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            // A held/dissolved editor pose must never become a hidden gameplay starting state
            // when Enter Play Mode skips domain/scene reload.
            if (state == UnityEditor.PlayModeStateChange.ExitingEditMode && controlled)
            { SetImmediate(true); controlled = false; }
        }
#endif
        private void RepaintEditorPreview()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
#endif
        }
        public void RefreshPreview() { if (controlled) Apply(); RepaintEditorPreview(); }
        public void CopyAnimationSettingsFrom(ResonanceManifestation source)
        {
            if (source == null || source == this) return;
            spawnSeconds = source.spawnSeconds; despawnSeconds = source.despawnSeconds;
            sizeGrowthPower = source.sizeGrowthPower; condensationPower = source.condensationPower;
            dispersalCenter = source.dispersalCenter; dispersalHalfExtents = source.dispersalHalfExtents;
            vibrationStrength = source.vibrationStrength; vibrationFrequency = source.vibrationFrequency;
            showDispersalArea = source.showDispersalArea;
            birthDistribution = source.birthDistribution; localSpawnSpread = source.localSpawnSpread;
            alongArcSpread = source.alongArcSpread; fieldCoherence = source.fieldCoherence;
            fieldWavelength = source.fieldWavelength; birthPulse = source.birthPulse;
            RefreshPreview();
        }

        public void BeginSpawn()
        {
#if UNITY_EDITOR
            editorClock = UnityEditor.EditorApplication.timeSinceStartup;
#endif
            if (!controlled) { controlled = true; progress = 0f; vibrationClock = 0f; }
            held = false; direction = progress < 1f ? 1 : 0;
            if (spawnSeconds <= 0f) { progress = 1f; direction = 0; }
            Apply();
            RepaintEditorPreview();
        }
        public void BeginDespawn()
        {
#if UNITY_EDITOR
            editorClock = UnityEditor.EditorApplication.timeSinceStartup;
#endif
            controlled = true; held = false; direction = progress > 0f ? -1 : 0;
            if (despawnSeconds <= 0f) { progress = 0f; direction = 0; }
            Apply();
            RepaintEditorPreview();
        }
        public void SetImmediate(bool visible)
        {
            controlled = true; held = false; direction = 0;
            progress = visible ? 1f : 0f; vibrationClock = 0f; Apply(); RepaintEditorPreview();
        }
        /// <summary>Hold a deterministic preview pose; BeginSpawn/BeginDespawn resumes from it.</summary>
        public void SetNormalizedProgress(float value)
        {
            controlled = true; held = true; direction = 0;
            progress = Mathf.Clamp01(float.IsNaN(value) ? 0f : value); Apply(); RepaintEditorPreview();
        }
        public void SetArea(Vector2 localCenter, Vector2 localHalfExtents)
        {
            dispersalCenter = localCenter;
            dispersalHalfExtents = new Vector2(Mathf.Max(.01f, localHalfExtents.x), Mathf.Max(.01f, localHalfExtents.y));
            if (controlled) Apply();
        }
        /// <summary>Uses scaled time. Zero delta pauses both formation and vibration; negative/invalid input is ignored.</summary>
        public void Advance(float scaledDeltaTime)
        {
            if (!controlled) return;
            if (!held && scaledDeltaTime > 0f && !float.IsInfinity(scaledDeltaTime) && !float.IsNaN(scaledDeltaTime))
            {
                if (direction != 0)
                {
                    vibrationClock += scaledDeltaTime;
                    float duration = direction > 0 ? spawnSeconds : despawnSeconds;
                    progress = duration <= 0f ? (direction > 0 ? 1f : 0f)
                        : Mathf.Clamp01(progress + direction * scaledDeltaTime / duration);
                    if (progress <= 0f || progress >= 1f) direction = 0;
                }
            }
            Apply();
        }
        public static float EasedProgress(float value, float power)
        {
            float t = Mathf.Clamp01(value);
            t = t * t * t * (t * (t * 6f - 15f) + 10f);
            return Mathf.Pow(t, Mathf.Clamp(power, .1f, 4f));
        }
        private void EnsurePattern() { if (pattern == null) pattern = GetComponent<ResonancePatternController>(); }
        private void Apply()
        {
            EnsurePattern();
            if (pattern == null) return;
            float condensation = EasedProgress(progress, condensationPower);
            float size = EasedProgress(progress, sizeGrowthPower);
            pattern.SetInteractionEnabled(IsIdle);
            pattern.SetManifestation(new Vector4(condensation, size, Mathf.Max(0f, vibrationStrength) * (1f - condensation),
                    vibrationClock * Mathf.Max(0f, vibrationFrequency)),
                new Vector4(dispersalCenter.x, dispersalCenter.y, Mathf.Max(.01f, dispersalHalfExtents.x), Mathf.Max(.01f, dispersalHalfExtents.y)),
                new Vector4((float)birthDistribution, Mathf.Max(0f, localSpawnSpread), Mathf.Clamp01(alongArcSpread), Mathf.Clamp01(fieldCoherence)),
                new Vector4(Mathf.Max(.05f, fieldWavelength), Mathf.Clamp01(birthPulse), 0f, 0f));
        }
        private void OnDrawGizmosSelected()
        {
            if (!showDispersalArea || birthDistribution != ResonanceBirthDistribution.FullField) return;
            Matrix4x4 previous = Gizmos.matrix; Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(.4f, .85f, 1f, .7f);
            Gizmos.DrawWireCube(new Vector3(dispersalCenter.x, .04f, dispersalCenter.y),
                new Vector3(dispersalHalfExtents.x * 2f, .02f, dispersalHalfExtents.y * 2f));
            Gizmos.matrix = previous;
        }
    }
}
