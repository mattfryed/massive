using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Massive.Scoring
{
    /// <summary>
    /// Applies the persistent look and one-shot promotion response for the
    /// scoreboard's active energy class.
    ///
    /// ScoreboardManagerScript remains responsible for score formatting and
    /// tier-state text. This component is deliberately presentation-only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnergyTierVisualController : MonoBehaviour
    {
        [Serializable]
        public sealed class TierSceneBinding
        {
            public EnergyUnit unit;

            [Tooltip("Optional scene object enabled only while this tier is active.")]
            public GameObject activeRoot;

            [Tooltip("Pre-placed particle systems played when this tier is reached.")]
            public ParticleSystem[] promotionParticles;

            [Tooltip("Optional additional scene animators triggered on promotion.")]
            public Animator[] promotionAnimators;

            public string promotionTrigger = "Promote";
            public UnityEvent onBecameActive;
            public UnityEvent onPromoted;
        }

        private sealed class RuntimeTextState
        {
            public TMP_Text text;
            public Material material;
            public Color originalVertexColor;

            public bool hasFaceColor;
            public Color faceColor;
            public bool hasOutlineColor;
            public Color outlineColor;
            public bool hasOutlineWidth;
            public float outlineWidth;
            public bool hasFaceDilate;
            public float faceDilate;
            public bool hasSoftness;
            public float softness;
            public bool hasGlowColor;
            public Color glowColor;
            public bool hasGlowOffset;
            public float glowOffset;
            public bool hasGlowInner;
            public float glowInner;
            public bool hasGlowOuter;
            public float glowOuter;
            public bool hasGlowPower;
            public float glowPower;
            public bool glowKeyword;
        }

        private sealed class MotionTargetState
        {
            public Transform target;
            public Vector3 baseLocalPosition;
            public Quaternion baseLocalRotation;
            public Vector3 baseLocalScale;

            public bool IsValid => target != null;

            public static MotionTargetState Capture(Transform target)
            {
                if (target == null)
                    return null;

                return new MotionTargetState
                {
                    target = target,
                    baseLocalPosition = target.localPosition,
                    baseLocalRotation = target.localRotation,
                    baseLocalScale = target.localScale
                };
            }

            public void Restore()
            {
                if (target == null)
                    return;

                target.localPosition = baseLocalPosition;
                target.localRotation = baseLocalRotation;
                target.localScale = baseLocalScale;
            }
        }

        private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int FaceDilateId = Shader.PropertyToID("_FaceDilate");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowOffsetId = Shader.PropertyToID("_GlowOffset");
        private static readonly int GlowInnerId = Shader.PropertyToID("_GlowInner");
        private static readonly int GlowOuterId = Shader.PropertyToID("_GlowOuter");
        private static readonly int GlowPowerId = Shader.PropertyToID("_GlowPower");
        private const string GlowKeyword = "GLOW_ON";

        [Header("Profile")]
        [SerializeField] private EnergyTierVisualProfile profile;

        [Header("TMP Targets")]
        [Tooltip("These are normally assigned automatically by ScoreboardManagerScript.")]
        [SerializeField] private TMP_Text scoreValueText;
        [SerializeField] private TMP_Text activeUnitText;
        [Tooltip("Exact order: meV, eV, keV, MeV, GeV, TeV.")]
        [SerializeField] private TMP_Text[] tierLabels;
        [SerializeField] private TMP_Text[] extraStyledTexts;

        [Header("Which TMP Targets Receive SDF Styling")]
        [SerializeField] private bool styleScoreValue = true;
        [SerializeField] private bool styleActiveUnit = true;
        [SerializeField] private bool styleActiveTierIndicator = true;
        [SerializeField] private bool styleExtraTexts = true;

        [Header("Tier-Specific Promotion Motion")]
        [Tooltip("Exact order: meV, eV, keV, MeV, GeV, TeV. Assign a dedicated motion wrapper for each tier so layout components can continue to own the outer slots.")]
        [SerializeField] private Transform[] tierMotionRoots = new Transform[6];
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Optional Whole-Scoreboard Motion")]
        [FormerlySerializedAs("effectsRoot")]
        [Tooltip("Optional root for a smaller reaction across the entire score display. The old Effects Root assignment migrates here automatically.")]
        [SerializeField] private Transform sharedEffectsRoot;

        [Tooltip("When disabled, only the destination tier's motion root animates.")]
        [SerializeField] private bool animateSharedEffectsRoot;

        [Tooltip("Scales the profile's punch amount for the shared root. 0 = none, 1 = full profile punch.")]
        [Min(0f)] [SerializeField] private float sharedPunchInfluence = 0.15f;

        [Tooltip("Scales the profile's positional shake for the shared root.")]
        [Min(0f)] [SerializeField] private float sharedPositionShakeInfluence = 0.10f;

        [Tooltip("Scales the profile's rotational shake for the shared root.")]
        [Min(0f)] [SerializeField] private float sharedRotationShakeInfluence = 0.15f;

        [Header("Shared Promotion Outputs")]
        [SerializeField] private Animator sharedAnimator;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private Transform vfxAnchor;
        [SerializeField] private bool animateEverySkippedTier;

        [Header("Optional Scene-Specific Tier Hooks")]
        [SerializeField] private TierSceneBinding[] sceneBindings;

        private readonly Dictionary<TMP_Text, RuntimeTextState> _textStates =
            new Dictionary<TMP_Text, RuntimeTextState>();

        private readonly Queue<EnergyUnit> _promotionQueue = new Queue<EnergyUnit>();

        private Coroutine _promotionRoutine;
        private bool _hasActiveUnit;
        private EnergyUnit _activeUnit;

        private MotionTargetState _activeTierMotionState;
        private MotionTargetState _activeSharedMotionState;

        private GameObject _activeLoopVfx;
        private EnergyUnit _activeLoopUnit;
        private bool _hasActiveLoopUnit;

        public EnergyTierVisualProfile Profile => profile;
        public EnergyUnit ActiveUnit => _activeUnit;
        public bool HasActiveUnit => _hasActiveUnit;

        private void Awake()
        {
            EnsureTierMotionRootArray();
            CacheConfiguredTextTargets();
        }

        private void OnEnable()
        {
            EnsureTierMotionRootArray();

            if (_hasActiveUnit)
                ApplyTier(_activeUnit, immediate: true);
        }

        private void OnValidate()
        {
            EnsureTierMotionRootArray();
            sharedPunchInfluence = Mathf.Max(0f, sharedPunchInfluence);
            sharedPositionShakeInfluence = Mathf.Max(0f, sharedPositionShakeInfluence);
            sharedRotationShakeInfluence = Mathf.Max(0f, sharedRotationShakeInfluence);
        }

        private void OnDisable()
        {
            if (_promotionRoutine != null)
            {
                StopCoroutine(_promotionRoutine);
                _promotionRoutine = null;
            }

            _promotionQueue.Clear();
            RestoreActiveMotionTargets();
            DestroyActiveLoopVfx();
        }

        private void OnDestroy()
        {
            DestroyActiveLoopVfx();

            foreach (RuntimeTextState state in _textStates.Values)
            {
                if (state == null || state.material == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(state.material);
                else
                    DestroyImmediate(state.material);
            }

            _textStates.Clear();
        }

        /// <summary>
        /// Lets ScoreboardManagerScript provide its existing TMP references so
        /// they do not have to be assigned twice in the Inspector.
        /// </summary>
        public void ConfigureTargets(
            TMP_Text scoreValue,
            TMP_Text activeUnit,
            TMP_Text[] indicators)
        {
            scoreValueText = scoreValue;
            activeUnitText = activeUnit;
            tierLabels = indicators;
            CacheConfiguredTextTargets();

            if (_hasActiveUnit)
                ApplyTier(_activeUnit, immediate: true);
        }

        public void SetProfile(EnergyTierVisualProfile newProfile, bool reapply = true)
        {
            profile = newProfile;
            if (reapply && _hasActiveUnit)
                ApplyTier(_activeUnit, immediate: true);
        }

        /// <summary>
        /// Applies the persistent style for the currently active tier. It does
        /// not play the one-shot promotion response.
        /// </summary>
        public void ApplyTier(EnergyUnit unit, bool immediate)
        {
            bool changed = !_hasActiveUnit || _activeUnit != unit;
            _activeUnit = unit;
            _hasActiveUnit = true;

            EnergyTierVisualProfile.TierStyle style = profile != null
                ? profile.GetStyle(unit)
                : null;

            ResetTierLabelMaterials();

            if (style != null)
            {
                if (styleScoreValue)
                    ApplyTextStyle(scoreValueText, style.scoreColor, style);

                if (styleActiveUnit)
                    ApplyTextStyle(activeUnitText, style.activeUnitColor, style);

                if (styleActiveTierIndicator && tierLabels != null)
                {
                    int index = (int)unit;
                    if (index >= 0 && index < tierLabels.Length)
                        ApplyTextStyle(tierLabels[index], style.activeIndicatorColor, style);
                }

                if (styleExtraTexts && extraStyledTexts != null)
                {
                    for (int i = 0; i < extraStyledTexts.Length; i++)
                        ApplyTextStyle(extraStyledTexts[i], style.scoreColor, style);
                }

                UpdateActiveLoopVfx(style, unit);
            }
            else
            {
                RestoreTextMaterial(scoreValueText, restoreVertexColor: true);
                RestoreTextMaterial(activeUnitText, restoreVertexColor: true);

                if (extraStyledTexts != null)
                {
                    for (int i = 0; i < extraStyledTexts.Length; i++)
                        RestoreTextMaterial(extraStyledTexts[i], restoreVertexColor: true);
                }

                DestroyActiveLoopVfx();
            }

            ApplySceneActiveState(unit, changed);
        }

        /// <summary>
        /// Queues a destination-tier promotion response. If one award jumps
        /// across several engineering units, the controller can either play only
        /// the destination tier or each crossed tier in sequence.
        /// </summary>
        public void PlayPromotion(EnergyTierPromotion promotion)
        {
            int previous = Mathf.Clamp((int)promotion.previousUnit, 0, EnergyScoreMath.UnitCount - 1);
            int current = Mathf.Clamp((int)promotion.currentUnit, 0, EnergyScoreMath.UnitCount - 1);

            if (current <= previous)
                return;

            if (animateEverySkippedTier)
            {
                for (int i = previous + 1; i <= current; i++)
                    _promotionQueue.Enqueue((EnergyUnit)i);
            }
            else
            {
                _promotionQueue.Enqueue((EnergyUnit)current);
            }

            if (_promotionRoutine == null && isActiveAndEnabled)
                _promotionRoutine = StartCoroutine(PlayPromotionQueue());
        }

        private IEnumerator PlayPromotionQueue()
        {
            while (_promotionQueue.Count > 0)
            {
                EnergyUnit destination = _promotionQueue.Dequeue();
                ApplyTier(destination, immediate: false);

                EnergyTierVisualProfile.TierStyle style = profile != null
                    ? profile.GetStyle(destination)
                    : null;

                TriggerPromotionOutputs(destination, style);

                float duration = style != null
                    ? Mathf.Max(0f, style.promotionSeconds)
                    : 0f;

                if (duration > 0f)
                    yield return AnimatePromotionMotion(destination, style, duration);
                else
                    yield return null;
            }

            RestoreActiveMotionTargets();
            _promotionRoutine = null;
        }

        private IEnumerator AnimatePromotionMotion(
            EnergyUnit destination,
            EnergyTierVisualProfile.TierStyle style,
            float duration)
        {
            RestoreActiveMotionTargets();

            Transform tierTarget = GetTierMotionRoot(destination);
            _activeTierMotionState = MotionTargetState.Capture(tierTarget);

            Transform sharedTarget = animateSharedEffectsRoot
                ? sharedEffectsRoot
                : null;

            // Applying both motion passes to the same Transform would double the
            // offsets and scale. Treat it as the tier target only in that case.
            if (sharedTarget == tierTarget)
                sharedTarget = null;

            _activeSharedMotionState = MotionTargetState.Capture(sharedTarget);

            if (style == null ||
                (_activeTierMotionState == null && _activeSharedMotionState == null))
            {
                float wait = 0f;
                while (wait < duration)
                {
                    wait += DeltaTime();
                    yield return null;
                }

                RestoreActiveMotionTargets();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += DeltaTime();
                float t = Mathf.Clamp01(elapsed / duration);
                float envelope = style.promotionEnvelope != null
                    ? Mathf.Max(0f, style.promotionEnvelope.Evaluate(t))
                    : Mathf.Sin(t * Mathf.PI);

                float phase = elapsed * Mathf.Max(0.01f, style.shakeFrequency) * Mathf.PI * 2f;

                ApplyMotion(
                    _activeTierMotionState,
                    style,
                    envelope,
                    phase,
                    punchInfluence: 1f,
                    positionShakeInfluence: 1f,
                    rotationShakeInfluence: 1f);

                ApplyMotion(
                    _activeSharedMotionState,
                    style,
                    envelope,
                    phase,
                    punchInfluence: sharedPunchInfluence,
                    positionShakeInfluence: sharedPositionShakeInfluence,
                    rotationShakeInfluence: sharedRotationShakeInfluence);

                yield return null;
            }

            RestoreActiveMotionTargets();
        }

        private static void ApplyMotion(
            MotionTargetState state,
            EnergyTierVisualProfile.TierStyle style,
            float envelope,
            float phase,
            float punchInfluence,
            float positionShakeInfluence,
            float rotationShakeInfluence)
        {
            if (state == null || !state.IsValid || style == null)
                return;

            float positionAmplitude = style.shakePosition * Mathf.Max(0f, positionShakeInfluence);
            float rotationAmplitude = style.shakeRotationDegrees * Mathf.Max(0f, rotationShakeInfluence);

            float shakeX = Mathf.Sin(phase) * positionAmplitude * envelope;
            float shakeY = Mathf.Sin(phase * 1.371f + 1.7f) * positionAmplitude * envelope;
            float shakeZ = Mathf.Sin(phase * 1.117f + 0.8f) * rotationAmplitude * envelope;

            state.target.localPosition = state.baseLocalPosition + new Vector3(shakeX, shakeY, 0f);
            state.target.localRotation =
                state.baseLocalRotation * Quaternion.Euler(0f, 0f, shakeZ);

            float punchAmount =
                (Mathf.Max(1f, style.punchScale) - 1f) * Mathf.Max(0f, punchInfluence);
            float punch = 1f + punchAmount * envelope;

            state.target.localScale = Vector3.Scale(
                state.baseLocalScale,
                Vector3.one * punch);
        }

        private void TriggerPromotionOutputs(
            EnergyUnit destination,
            EnergyTierVisualProfile.TierStyle style)
        {
            if (style != null)
            {
                if (sharedAnimator != null && !string.IsNullOrWhiteSpace(style.animatorTrigger))
                    sharedAnimator.SetTrigger(style.animatorTrigger);

                if (audioSource != null && style.promotionAudio != null)
                    audioSource.PlayOneShot(style.promotionAudio);

                if (style.promotionVfxPrefab != null)
                {
                    Transform anchor = vfxAnchor != null ? vfxAnchor : transform;
                    GameObject instance = Instantiate(
                        style.promotionVfxPrefab,
                        anchor.position,
                        anchor.rotation,
                        anchor);

                    if (style.promotionVfxLifetime > 0f)
                        Destroy(instance, style.promotionVfxLifetime);
                }
            }

            TierSceneBinding binding = FindSceneBinding(destination);
            if (binding == null)
                return;

            if (binding.promotionParticles != null)
            {
                for (int i = 0; i < binding.promotionParticles.Length; i++)
                {
                    ParticleSystem ps = binding.promotionParticles[i];
                    if (ps != null)
                        ps.Play(true);
                }
            }

            if (binding.promotionAnimators != null)
            {
                for (int i = 0; i < binding.promotionAnimators.Length; i++)
                {
                    Animator animator = binding.promotionAnimators[i];
                    if (animator != null && !string.IsNullOrWhiteSpace(binding.promotionTrigger))
                        animator.SetTrigger(binding.promotionTrigger);
                }
            }

            binding.onPromoted?.Invoke();
        }

        private void ApplySceneActiveState(EnergyUnit unit, bool changed)
        {
            if (sceneBindings == null)
                return;

            for (int i = 0; i < sceneBindings.Length; i++)
            {
                TierSceneBinding binding = sceneBindings[i];
                if (binding == null) continue;

                bool active = binding.unit == unit;
                if (binding.activeRoot != null)
                    binding.activeRoot.SetActive(active);

                if (active && changed)
                    binding.onBecameActive?.Invoke();
            }
        }

        private TierSceneBinding FindSceneBinding(EnergyUnit unit)
        {
            if (sceneBindings == null)
                return null;

            for (int i = 0; i < sceneBindings.Length; i++)
            {
                TierSceneBinding binding = sceneBindings[i];
                if (binding != null && binding.unit == unit)
                    return binding;
            }

            return null;
        }

        private void UpdateActiveLoopVfx(
            EnergyTierVisualProfile.TierStyle style,
            EnergyUnit unit)
        {
            if (!Application.isPlaying)
                return;

            if (_hasActiveLoopUnit && _activeLoopUnit == unit)
                return;

            DestroyActiveLoopVfx();
            _activeLoopUnit = unit;
            _hasActiveLoopUnit = true;

            if (style == null || style.activeLoopVfxPrefab == null)
                return;

            Transform anchor = vfxAnchor != null ? vfxAnchor : transform;
            _activeLoopVfx = Instantiate(
                style.activeLoopVfxPrefab,
                anchor.position,
                anchor.rotation,
                anchor);
        }

        private void DestroyActiveLoopVfx()
        {
            if (_activeLoopVfx != null)
            {
                if (Application.isPlaying)
                    Destroy(_activeLoopVfx);
                else
                    DestroyImmediate(_activeLoopVfx);
            }

            _activeLoopVfx = null;
            _hasActiveLoopUnit = false;
        }

        private void CacheConfiguredTextTargets()
        {
            CacheText(scoreValueText);
            CacheText(activeUnitText);

            if (tierLabels != null)
            {
                for (int i = 0; i < tierLabels.Length; i++)
                    CacheText(tierLabels[i]);
            }

            if (extraStyledTexts != null)
            {
                for (int i = 0; i < extraStyledTexts.Length; i++)
                    CacheText(extraStyledTexts[i]);
            }
        }

        private RuntimeTextState CacheText(TMP_Text text)
        {
            if (text == null)
                return null;

            if (_textStates.TryGetValue(text, out RuntimeTextState existing))
                return existing;

            Material source = text.fontSharedMaterial;
            if (source == null)
                source = text.fontMaterial;

            if (source == null)
                return null;

            var runtime = new Material(source)
            {
                name = source.name + " (Energy Tier Instance)"
            };

            text.fontMaterial = runtime;

            var state = new RuntimeTextState
            {
                text = text,
                material = runtime,
                originalVertexColor = text.color,
                hasFaceColor = runtime.HasProperty(FaceColorId),
                hasOutlineColor = runtime.HasProperty(OutlineColorId),
                hasOutlineWidth = runtime.HasProperty(OutlineWidthId),
                hasFaceDilate = runtime.HasProperty(FaceDilateId),
                hasSoftness = runtime.HasProperty(SoftnessId),
                hasGlowColor = runtime.HasProperty(GlowColorId),
                hasGlowOffset = runtime.HasProperty(GlowOffsetId),
                hasGlowInner = runtime.HasProperty(GlowInnerId),
                hasGlowOuter = runtime.HasProperty(GlowOuterId),
                hasGlowPower = runtime.HasProperty(GlowPowerId),
                glowKeyword = runtime.IsKeywordEnabled(GlowKeyword)
            };

            if (state.hasFaceColor) state.faceColor = runtime.GetColor(FaceColorId);
            if (state.hasOutlineColor) state.outlineColor = runtime.GetColor(OutlineColorId);
            if (state.hasOutlineWidth) state.outlineWidth = runtime.GetFloat(OutlineWidthId);
            if (state.hasFaceDilate) state.faceDilate = runtime.GetFloat(FaceDilateId);
            if (state.hasSoftness) state.softness = runtime.GetFloat(SoftnessId);
            if (state.hasGlowColor) state.glowColor = runtime.GetColor(GlowColorId);
            if (state.hasGlowOffset) state.glowOffset = runtime.GetFloat(GlowOffsetId);
            if (state.hasGlowInner) state.glowInner = runtime.GetFloat(GlowInnerId);
            if (state.hasGlowOuter) state.glowOuter = runtime.GetFloat(GlowOuterId);
            if (state.hasGlowPower) state.glowPower = runtime.GetFloat(GlowPowerId);

            _textStates.Add(text, state);
            return state;
        }

        private void ResetTierLabelMaterials()
        {
            if (tierLabels == null)
                return;

            for (int i = 0; i < tierLabels.Length; i++)
                RestoreTextMaterial(tierLabels[i], restoreVertexColor: false);
        }

        private void ApplyTextStyle(
            TMP_Text text,
            Color vertexColor,
            EnergyTierVisualProfile.TierStyle style)
        {
            if (text == null || style == null)
                return;

            RuntimeTextState state = CacheText(text);
            if (state == null || state.material == null)
                return;

            RestoreTextMaterial(text, restoreVertexColor: false);

            Color current = vertexColor;
            current.a = text.color.a;
            text.color = current;

            Material material = state.material;

            // Keep the material face neutral and use TMP vertex color as the
            // tier color. This prevents the two color layers multiplying into
            // unexpectedly dark results.
            if (state.hasFaceColor)
                material.SetColor(FaceColorId, Color.white);

            if (state.hasOutlineColor)
                material.SetColor(OutlineColorId, style.outlineColor);

            if (state.hasOutlineWidth)
                material.SetFloat(OutlineWidthId, style.outlineWidth);

            if (state.hasFaceDilate)
                material.SetFloat(FaceDilateId, style.faceDilate);

            if (state.hasSoftness)
                material.SetFloat(SoftnessId, style.softness);

            if (style.enableGlow)
            {
                material.EnableKeyword(GlowKeyword);
                if (state.hasGlowColor) material.SetColor(GlowColorId, style.glowColor);
                if (state.hasGlowOffset) material.SetFloat(GlowOffsetId, style.glowOffset);
                if (state.hasGlowInner) material.SetFloat(GlowInnerId, style.glowInner);
                if (state.hasGlowOuter) material.SetFloat(GlowOuterId, style.glowOuter);
                if (state.hasGlowPower) material.SetFloat(GlowPowerId, style.glowPower);
            }
            else
            {
                material.DisableKeyword(GlowKeyword);
            }

            text.UpdateMeshPadding();
            text.ForceMeshUpdate();
        }

        private void RestoreTextMaterial(TMP_Text text, bool restoreVertexColor)
        {
            if (text == null || !_textStates.TryGetValue(text, out RuntimeTextState state))
                return;

            Material material = state.material;
            if (material == null)
                return;

            if (restoreVertexColor)
                text.color = state.originalVertexColor;

            if (state.hasFaceColor) material.SetColor(FaceColorId, state.faceColor);
            if (state.hasOutlineColor) material.SetColor(OutlineColorId, state.outlineColor);
            if (state.hasOutlineWidth) material.SetFloat(OutlineWidthId, state.outlineWidth);
            if (state.hasFaceDilate) material.SetFloat(FaceDilateId, state.faceDilate);
            if (state.hasSoftness) material.SetFloat(SoftnessId, state.softness);
            if (state.hasGlowColor) material.SetColor(GlowColorId, state.glowColor);
            if (state.hasGlowOffset) material.SetFloat(GlowOffsetId, state.glowOffset);
            if (state.hasGlowInner) material.SetFloat(GlowInnerId, state.glowInner);
            if (state.hasGlowOuter) material.SetFloat(GlowOuterId, state.glowOuter);
            if (state.hasGlowPower) material.SetFloat(GlowPowerId, state.glowPower);

            if (state.glowKeyword)
                material.EnableKeyword(GlowKeyword);
            else
                material.DisableKeyword(GlowKeyword);

            text.UpdateMeshPadding();
        }

        private Transform GetTierMotionRoot(EnergyUnit unit)
        {
            int index = (int)unit;
            if (tierMotionRoots == null || index < 0 || index >= tierMotionRoots.Length)
                return null;

            return tierMotionRoots[index];
        }

        private void EnsureTierMotionRootArray()
        {
            int count = Mathf.Max(1, EnergyScoreMath.UnitCount);
            if (tierMotionRoots == null)
            {
                tierMotionRoots = new Transform[count];
                return;
            }

            if (tierMotionRoots.Length != count)
                Array.Resize(ref tierMotionRoots, count);
        }

        private void RestoreActiveMotionTargets()
        {
            _activeTierMotionState?.Restore();
            _activeSharedMotionState?.Restore();
            _activeTierMotionState = null;
            _activeSharedMotionState = null;
        }

        private float DeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}
