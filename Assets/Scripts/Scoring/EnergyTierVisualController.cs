using System;
using System.Collections;
using System.Collections.Generic;
using Massive.TextAnimation;
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
            public TMPMaterialStateController materialState;
            public Color originalVertexColor;
            public float originalFontSize;
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

        [Header("Tier Text Animation")]
        [Tooltip("Exact order: meV, eV, keV, MeV, GeV, TeV. Empty entries are resolved from the corresponding tier-label GameObject.")]
        [SerializeField] private TMPTextAnimator[] tierTextAnimators = new TMPTextAnimator[6];
        [SerializeField] private bool autoFindTierTextAnimators = true;
        [Tooltip("At runtime, add a TMPTextAnimator to a tier label when no assigned or existing animator can be found. This keeps presets usable during the parallel migration without modifying legacy TMPTextTransition components.")]
        [SerializeField] private bool autoCreateMissingTierTextAnimators = true;

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
        private Coroutine _tierShowcaseRoutine;
        private bool _hasActiveUnit;
        private EnergyUnit _activeUnit;

        private MotionTargetState _activeTierMotionState;
        private MotionTargetState _activeSharedMotionState;
        private TextAnimationPlaybackHandle _activeTierTextPlayback;
        private TextAnimationPlaybackHandle _activeTierLoopTextPlayback;
        private bool _tierShowcaseOwnsLoop;

        private GameObject _activeLoopVfx;
        private EnergyUnit _activeLoopUnit;
        private bool _hasActiveLoopUnit;

        public EnergyTierVisualProfile Profile => profile;
        public EnergyUnit ActiveUnit => _activeUnit;
        public bool HasActiveUnit => _hasActiveUnit;

        private void Awake()
        {
            EnsureTierMotionRootArray();
            EnsureTierTextAnimatorArray();
            ResolveTierTextAnimators();
            CacheConfiguredTextTargets();
        }

        private void OnEnable()
        {
            EnsureTierMotionRootArray();
            EnsureTierTextAnimatorArray();
            ResolveTierTextAnimators();

            if (_hasActiveUnit)
                ApplyTier(_activeUnit, immediate: true);
        }

        private void OnValidate()
        {
            EnsureTierMotionRootArray();
            EnsureTierTextAnimatorArray();
            // Auto-creation is valid from Awake/OnEnable, but Unity forbids it
            // during OnValidate (including the Play Mode transition window).
            if (!Application.isPlaying)
                ResolveTierTextAnimators();
            sharedPunchInfluence = Mathf.Max(0f, sharedPunchInfluence);
            sharedPositionShakeInfluence = Mathf.Max(0f, sharedPositionShakeInfluence);
            sharedRotationShakeInfluence = Mathf.Max(0f, sharedRotationShakeInfluence);
        }

        private void OnDisable()
        {
            StopTierTextShowcase();

            if (_promotionRoutine != null)
            {
                StopCoroutine(_promotionRoutine);
                _promotionRoutine = null;
            }

            _promotionQueue.Clear();
            StopActiveTierTextAnimation();
            StopActiveTierLoopTextAnimation();
            RestoreActiveMotionTargets();
            RestoreTierLabelFontSizes();
            DestroyActiveLoopVfx();
        }

        private void OnDestroy()
        {
            DestroyActiveLoopVfx();
            RestoreTierLabelFontSizes();

            foreach (RuntimeTextState state in _textStates.Values)
            {
                if (state == null || state.materialState == null)
                    continue;

                state.materialState.ClearTransient();
                state.materialState.ResetPersistentToOriginal();
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
            EnsureTierTextAnimatorArray();
            ResolveTierTextAnimators();
            CacheConfiguredTextTargets();

            if (_hasActiveUnit)
                ApplyTier(_activeUnit, immediate: true);
        }

        public void ConfigureTierTextAnimators(TMPTextAnimator[] animators)
        {
            tierTextAnimators = animators;
            EnsureTierTextAnimatorArray();
            ResolveTierTextAnimators();
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
            if (changed || immediate)
                StopActiveTierLoopTextAnimation();

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
                    {
                        ApplyTierLabelFontSize(
                            tierLabels[index],
                            style.activeIndicatorFontSizeMultiplier);
                        ApplyTextStyle(tierLabels[index], style.activeIndicatorColor, style);
                    }
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

            if (immediate)
                TryStartTierLoopTextAnimation(unit, style, warnWhenUnavailable: false);
        }

        /// <summary>
        /// Queues a destination-tier promotion response. If one award jumps
        /// across several engineering units, the controller can either play only
        /// the destination tier or each crossed tier in sequence.
        /// </summary>
        public void PlayPromotion(EnergyTierPromotion promotion)
        {
            StopTierTextShowcase();
            StopActiveTierLoopTextAnimation();

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

                bool hasConfiguredTextPreset = style != null &&
                                               style.promotionTextPreset != null;
                bool textPresetStarted = TryStartTierTextAnimation(
                    destination,
                    style,
                    out TMPTextAnimator textAnimator,
                    warnWhenUnavailable: hasConfiguredTextPreset);

                bool playGenericTierMotion = !textPresetStarted ||
                                             style == null ||
                                             !style.promotionTextPresetReplacesGenericMotion;
                bool playGenericSharedMotion = animateSharedEffectsRoot;

                PreventTransformOwnershipConflicts(
                    destination,
                    textAnimator,
                    textPresetStarted,
                    ref playGenericTierMotion,
                    ref playGenericSharedMotion);

                float duration = style != null
                    ? Mathf.Max(0f, style.promotionSeconds)
                    : 0f;

                if (duration > 0f &&
                    (playGenericTierMotion || playGenericSharedMotion))
                {
                    yield return AnimatePromotionMotion(
                        destination,
                        style,
                        duration,
                        playGenericTierMotion,
                        playGenericSharedMotion);
                }
                else if (!textPresetStarted)
                {
                    yield return null;
                }

                while (_activeTierTextPlayback.IsActive)
                    yield return null;

                _activeTierTextPlayback = default;
                TryStartTierLoopTextAnimation(
                    destination,
                    style,
                    warnWhenUnavailable: false);
            }

            RestoreActiveMotionTargets();
            _promotionRoutine = null;
        }

        /// <summary>
        /// Plays the six configured tier-label presets in engineering-unit order.
        /// Intended for the Play Mode preview window; gameplay promotion outputs
        /// and generic promotion motion are deliberately not triggered.
        /// </summary>
        public void PlayTierTextShowcase(float pauseBetweenTiers = 0.65f)
        {
            if (!isActiveAndEnabled)
                return;

            StopPromotionQueueForPreview();
            StopTierTextShowcase();
            StopActiveTierLoopTextAnimation();
            RestoreTierLabelFontSizes();
            _tierShowcaseOwnsLoop = true;
            _tierShowcaseRoutine = StartCoroutine(
                PlayTierTextShowcaseRoutine(Mathf.Max(0f, pauseBetweenTiers)));
        }

        public void StopTierTextShowcase()
        {
            bool wasRunning = _tierShowcaseRoutine != null;
            if (_tierShowcaseRoutine != null)
            {
                StopCoroutine(_tierShowcaseRoutine);
                _tierShowcaseRoutine = null;
            }

            if (wasRunning)
                StopActiveTierTextAnimation();

            if (_tierShowcaseOwnsLoop)
            {
                StopActiveTierLoopTextAnimation();
                _tierShowcaseOwnsLoop = false;
            }
        }

        private IEnumerator PlayTierTextShowcaseRoutine(float pauseBetweenTiers)
        {
            for (int i = 0; i < EnergyScoreMath.UnitCount; i++)
            {
                EnergyUnit unit = (EnergyUnit)i;
                ApplyTier(unit, immediate: false);

                EnergyTierVisualProfile.TierStyle style = profile != null
                    ? profile.GetStyle(unit)
                    : null;

                bool started = TryStartTierTextAnimation(
                    unit,
                    style,
                    out _,
                    warnWhenUnavailable: true);

                if (started)
                {
                    while (_activeTierTextPlayback.IsActive)
                        yield return null;

                    _activeTierTextPlayback = default;
                }
                else
                {
                    yield return null;
                }

                TryStartTierLoopTextAnimation(
                    unit,
                    style,
                    warnWhenUnavailable: false);

                if (i >= EnergyScoreMath.UnitCount - 1 || pauseBetweenTiers <= 0f)
                    continue;

                float elapsed = 0f;
                while (elapsed < pauseBetweenTiers)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            _tierShowcaseRoutine = null;
        }

        private bool TryStartTierTextAnimation(
            EnergyUnit unit,
            EnergyTierVisualProfile.TierStyle style,
            out TMPTextAnimator textAnimator,
            bool warnWhenUnavailable)
        {
            textAnimator = GetTierTextAnimator(unit);
            if (style == null || style.promotionTextPreset == null)
            {
                if (warnWhenUnavailable)
                {
                    Debug.LogWarning(
                        $"[{nameof(EnergyTierVisualController)}] No promotion text " +
                        $"preset is configured for {unit}.",
                        this);
                }

                return false;
            }

            if (textAnimator == null)
            {
                if (warnWhenUnavailable)
                {
                    Debug.LogWarning(
                        $"[{nameof(EnergyTierVisualController)}] No active " +
                        $"{nameof(TMPTextAnimator)} is available for {unit}. " +
                        "Generic tier motion remains available as the gameplay fallback.",
                        this);
                }

                return false;
            }

            TextAnimationContext context = TextAnimationContext.Default
                .WithIntensity(style.promotionTextIntensity)
                .WithDirection(style.promotionTextDirection);

            if (style.useTierColorAsAnimationAccent)
                context = context.WithAccentColor(style.scoreColor);

            _activeTierTextPlayback = textAnimator.Play(
                style.promotionTextPreset,
                context);
            return _activeTierTextPlayback.IsValid;
        }

        private bool TryStartTierLoopTextAnimation(
            EnergyUnit unit,
            EnergyTierVisualProfile.TierStyle style,
            bool warnWhenUnavailable)
        {
            StopActiveTierLoopTextAnimation();

            if (style == null || style.activeLoopTextPreset == null)
                return false;

            TMPTextAnimator textAnimator = GetTierTextAnimator(unit);
            if (textAnimator == null)
            {
                if (warnWhenUnavailable)
                {
                    Debug.LogWarning(
                        $"[{nameof(EnergyTierVisualController)}] No active " +
                        $"{nameof(TMPTextAnimator)} is available for the {unit} " +
                        "baseline animation.",
                        this);
                }

                return false;
            }

            TextAnimationContext context = TextAnimationContext.Default
                .WithIntensity(style.activeLoopTextIntensity)
                .WithDirection(style.activeLoopTextDirection);

            if (style.useTierColorAsActiveLoopAccent)
                context = context.WithAccentColor(style.scoreColor);

            _activeTierLoopTextPlayback = textAnimator.Play(
                style.activeLoopTextPreset,
                context);
            return _activeTierLoopTextPlayback.IsValid;
        }

        private void StopPromotionQueueForPreview()
        {
            if (_promotionRoutine != null)
            {
                StopCoroutine(_promotionRoutine);
                _promotionRoutine = null;
            }

            _promotionQueue.Clear();
            RestoreActiveMotionTargets();
            StopActiveTierTextAnimation();
        }

        private IEnumerator AnimatePromotionMotion(
            EnergyUnit destination,
            EnergyTierVisualProfile.TierStyle style,
            float duration,
            bool animateTierTarget,
            bool animateSharedTarget)
        {
            RestoreActiveMotionTargets();

            Transform tierTarget = animateTierTarget
                ? GetTierMotionRoot(destination)
                : null;
            _activeTierMotionState = MotionTargetState.Capture(tierTarget);

            Transform sharedTarget = animateSharedTarget
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

            TMPMaterialStateController materialState =
                text.GetComponent<TMPMaterialStateController>();

            if (materialState == null && Application.isPlaying)
                materialState = text.gameObject.AddComponent<TMPMaterialStateController>();

            if (materialState != null)
                materialState.Configure(text);

            var state = new RuntimeTextState
            {
                text = text,
                materialState = materialState,
                originalVertexColor = text.color,
                originalFontSize = text.fontSize
            };

            _textStates.Add(text, state);
            return state;
        }

        private void ApplyTierLabelFontSize(TMP_Text text, float multiplier)
        {
            RuntimeTextState state = CacheText(text);
            if (state == null || state.text == null)
                return;

            state.text.fontSize =
                state.originalFontSize * Mathf.Max(0.01f, multiplier);
            state.text.ForceMeshUpdate();
        }

        private void RestoreTierLabelFontSizes()
        {
            if (tierLabels == null)
                return;

            for (int i = 0; i < tierLabels.Length; i++)
            {
                TMP_Text label = tierLabels[i];
                if (label == null ||
                    !_textStates.TryGetValue(label, out RuntimeTextState state) ||
                    state == null)
                {
                    continue;
                }

                label.fontSize = state.originalFontSize;
                label.ForceMeshUpdate();
            }
        }

        private void ResetTierLabelMaterials()
        {
            if (tierLabels == null)
                return;

            Color inactiveColor = profile != null
                ? profile.InactiveTierIndicatorColor
                : new Color(0.30f, 0.32f, 0.36f, 1f);

            for (int i = 0; i < tierLabels.Length; i++)
            {
                TMP_Text label = tierLabels[i];
                if (label == null)
                    continue;

                RestoreTierLabelFontSize(label);
                RestoreTextMaterial(label, restoreVertexColor: false);

                Color currentInactiveColor = inactiveColor;
                currentInactiveColor.a = label.color.a;
                label.color = currentInactiveColor;
                label.ForceMeshUpdate();
            }
        }

        private void RestoreTierLabelFontSize(TMP_Text label)
        {
            if (label == null ||
                !_textStates.TryGetValue(label, out RuntimeTextState state) ||
                state == null)
            {
                return;
            }

            label.fontSize = state.originalFontSize;
        }

        private void ApplyTextStyle(
            TMP_Text text,
            Color vertexColor,
            EnergyTierVisualProfile.TierStyle style)
        {
            if (text == null || style == null)
                return;

            RuntimeTextState state = CacheText(text);
            if (state == null || state.materialState == null)
                return;

            RestoreTextMaterial(text, restoreVertexColor: false);

            Color current = vertexColor;
            current.a = text.color.a;
            text.color = current;

            // Keep the material face neutral and use TMP vertex color as the
            // tier color. The material-state controller is the single SDF
            // authority and composes this persistent style with preset offsets.
            state.materialState.SetPersistentStyle(new TMPMaterialPersistentStyle
            {
                faceColor = Color.white,
                outlineColor = style.outlineColor,
                outlineWidth = style.outlineWidth,
                faceDilate = style.faceDilate,
                softness = style.softness,
                glowEnabled = style.enableGlow,
                glowColor = style.glowColor,
                glowOffset = style.glowOffset,
                glowInner = style.glowInner,
                glowOuter = style.glowOuter,
                glowPower = style.glowPower
            });

            text.ForceMeshUpdate();
        }

        private void RestoreTextMaterial(TMP_Text text, bool restoreVertexColor)
        {
            if (text == null || !_textStates.TryGetValue(text, out RuntimeTextState state))
                return;

            if (state.materialState == null)
                return;

            if (restoreVertexColor)
                text.color = state.originalVertexColor;

            state.materialState.ClearTransient();
            state.materialState.ResetPersistentToOriginal();
        }

        private TMPTextAnimator GetTierTextAnimator(EnergyUnit unit)
        {
            int index = (int)unit;
            if (tierTextAnimators == null ||
                index < 0 ||
                index >= tierTextAnimators.Length)
            {
                return null;
            }

            TMPTextAnimator animator = tierTextAnimators[index];
            if (animator == null && HasTierLabel(index))
            {
                TMP_Text label = tierLabels[index];

                if (autoFindTierTextAnimators)
                    animator = label.GetComponent<TMPTextAnimator>();

                if (animator == null &&
                    autoCreateMissingTierTextAnimators &&
                    Application.isPlaying)
                {
                    animator = label.gameObject.AddComponent<TMPTextAnimator>();
                    Transform motionRoot = GetTierMotionRoot(unit);
                    animator.Configure(label, motionRoot != null ? motionRoot : label.transform);
                }

                tierTextAnimators[index] = animator;
            }

            return animator;
        }

        private void EnsureTierTextAnimatorArray()
        {
            int count = Mathf.Max(1, EnergyScoreMath.UnitCount);
            if (tierTextAnimators == null)
            {
                tierTextAnimators = new TMPTextAnimator[count];
                return;
            }

            if (tierTextAnimators.Length != count)
                Array.Resize(ref tierTextAnimators, count);
        }

        private void ResolveTierTextAnimators()
        {
            if ((!autoFindTierTextAnimators &&
                 (!autoCreateMissingTierTextAnimators || !Application.isPlaying)) ||
                tierLabels == null)
                return;

            EnsureTierTextAnimatorArray();
            int count = Mathf.Min(tierLabels.Length, tierTextAnimators.Length);
            for (int i = 0; i < count; i++)
            {
                if (tierTextAnimators[i] == null)
                    GetTierTextAnimator((EnergyUnit)i);
            }
        }

        private bool HasTierLabel(int index)
        {
            return tierLabels != null &&
                   index >= 0 &&
                   index < tierLabels.Length &&
                   tierLabels[index] != null;
        }

        private void PreventTransformOwnershipConflicts(
            EnergyUnit destination,
            TMPTextAnimator textAnimator,
            bool textPresetStarted,
            ref bool playGenericTierMotion,
            ref bool playGenericSharedMotion)
        {
            if (!textPresetStarted || textAnimator == null)
                return;

            Transform presetRoot = textAnimator.MotionRoot;
            if (presetRoot == null)
                return;

            if (playGenericTierMotion && presetRoot == GetTierMotionRoot(destination))
            {
                playGenericTierMotion = false;
                Debug.LogWarning(
                    $"[{nameof(EnergyTierVisualController)}] The {destination} " +
                    "preset and generic promotion are assigned to the same tier " +
                    "motion root. Generic tier motion was skipped for this play.",
                    this);
            }

            if (playGenericSharedMotion && presetRoot == sharedEffectsRoot)
            {
                playGenericSharedMotion = false;
                Debug.LogWarning(
                    $"[{nameof(EnergyTierVisualController)}] The {destination} " +
                    "preset and shared promotion are assigned to the same motion " +
                    "root. Generic shared motion was skipped for this play.",
                    this);
            }
        }

        private void StopActiveTierTextAnimation()
        {
            if (_activeTierTextPlayback.IsValid)
                _activeTierTextPlayback.Cancel(restoreBaseline: true);

            _activeTierTextPlayback = default;
        }

        private void StopActiveTierLoopTextAnimation()
        {
            if (_activeTierLoopTextPlayback.IsValid)
                _activeTierLoopTextPlayback.Cancel(restoreBaseline: true);

            _activeTierLoopTextPlayback = default;
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
