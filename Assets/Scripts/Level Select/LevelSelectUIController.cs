using System.Collections;
using Massive.TextAnimation;
using TMPro;
using UnityEngine;

public class LevelSelectUIController : MonoBehaviour
{
    [Header("Bind")]
    [SerializeField] private LevelCarouselController carousel;

    [Header("Texts")]
    [SerializeField] private TMP_Text levelNumberText;
    [SerializeField] private TMP_Text levelTitleText;

    [Header("Anomaly Type")]
    [Tooltip("Optional: set this text to a constant label (ex: ANOMALY TYPE).")]
    [SerializeField] private TMP_Text anomalyTypeLabelText;

    [Tooltip("The dynamic anomaly type name pulled from LevelDefinition.")]
    [SerializeField] private TMP_Text anomalyTypeNameText;

    [SerializeField] private string anomalyTypeLabel = "ANOMALY TYPE";
    [SerializeField] private string anomalyTypeFallback = "UNKNOWN";

    [Header("Formatting")]
    [SerializeField] private string stagePrefix = "STAGE_";
    [SerializeField] private int stageDigits = 3;

    [Header("Preset Animation System (Preferred)")]
    [Tooltip("Scene bindings for reusable slots: NUMBER, TITLE, ANOMALY_LABEL, and ANOMALY_VALUE.")]
    [SerializeField] private TMPTextAnimationGroup animationGroup;
    [SerializeField] private TextAnimationSequence selectionOutSequence;
    [SerializeField] private TextAnimationSequence selectionInSequence;
    [SerializeField] private bool preferPresetSystem = true;

    [Header("Legacy TMPTextTransition Fallback")]
    [Tooltip("Used only when the preset group and both sequences are not assigned.")]
    [SerializeField] private TMPTextTransition groupTransition;

    [Tooltip("Optional legacy per-text transitions.")]
    [SerializeField] private TMPTextTransition numberTransition;
    [SerializeField] private TMPTextTransition titleTransition;

    [Header("CanvasGroup Fallback")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeOutSeconds = 0.12f;
    [SerializeField] private float hiddenHoldSeconds = 0.06f;
    [SerializeField] private float fadeInSeconds = 0.18f;
    [SerializeField] private AnimationCurve fadeEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Safety")]
    [Tooltip("If a preset or legacy OUT never completes, continue after this many seconds.")]
    [SerializeField] private float transitionTimeoutSeconds = 1.5f;

    private Coroutine _routine;
    private int _token;
    private int _lastSelectedIndex = -1;

    private bool _groupOutDone;
    private bool _numOutDone;
    private bool _titleOutDone;

    private void Awake()
    {
        if (carousel == null)
            carousel = FindFirstObjectByType<LevelCarouselController>();
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        BindLegacyTransitionEvents();

        if (carousel == null)
            return;

        carousel.OnSelectionChanged += OnSelectionChanged;

        if (carousel.SelectedLevel == null)
            return;

        _lastSelectedIndex = carousel.SelectedIndex;
        SetText(carousel.SelectedLevel);

        if (UsesPresetSystem())
        {
            animationGroup.RefreshBaselines();
            _routine = StartCoroutine(InitialPresetInRoutine());
        }
        else if (HasAnyLegacyTransition())
        {
            PlayLegacyInNow();
        }
        else if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }

    private void OnDestroy()
    {
        if (carousel != null)
            carousel.OnSelectionChanged -= OnSelectionChanged;

        UnbindLegacyTransitionEvents();

        if (animationGroup != null)
            animationGroup.StopCurrent(restoreBaselines: true);
    }

    private void BindLegacyTransitionEvents()
    {
        if (groupTransition != null)
            groupTransition.onOutComplete.AddListener(OnLegacyGroupOutComplete);

        if (numberTransition != null)
            numberTransition.onOutComplete.AddListener(OnLegacyNumberOutComplete);

        if (titleTransition != null)
            titleTransition.onOutComplete.AddListener(OnLegacyTitleOutComplete);
    }

    private void UnbindLegacyTransitionEvents()
    {
        if (groupTransition != null)
            groupTransition.onOutComplete.RemoveListener(OnLegacyGroupOutComplete);

        if (numberTransition != null)
            numberTransition.onOutComplete.RemoveListener(OnLegacyNumberOutComplete);

        if (titleTransition != null)
            titleTransition.onOutComplete.RemoveListener(OnLegacyTitleOutComplete);
    }

    private void OnLegacyGroupOutComplete() => _groupOutDone = true;
    private void OnLegacyNumberOutComplete() => _numOutDone = true;
    private void OnLegacyTitleOutComplete() => _titleOutDone = true;

    private IEnumerator InitialPresetInRoutine()
    {
        int token = ++_token;
        TextAnimationContext context = BuildContext(Vector2.right);
        yield return PlayPresetSequenceWithTimeout(selectionInSequence, context, token);

        if (token == _token)
            _routine = null;
    }

    private void OnSelectionChanged(LevelDefinition def, int index)
    {
        if (def == null)
            return;

        int token = ++_token;

        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        if (animationGroup != null)
            animationGroup.StopCurrent(restoreBaselines: true);

        Vector2 direction = DetermineNavigationDirection(_lastSelectedIndex, index);
        _lastSelectedIndex = index;
        _routine = StartCoroutine(ChangeRoutine(def, direction, token));
    }

    private IEnumerator ChangeRoutine(LevelDefinition def, Vector2 direction, int token)
    {
        if (UsesPresetSystem())
        {
            TextAnimationContext context = BuildContext(direction);
            yield return PlayPresetSequenceWithTimeout(selectionOutSequence, context, token);

            if (token != _token)
                yield break;

            SetText(def);

            // Rebuild clean geometry for the newly assigned text while the OUT
            // sequence is still holding the targets hidden.
            animationGroup.RefreshBaselines();

            if (token != _token)
                yield break;

            yield return PlayPresetSequenceWithTimeout(selectionInSequence, context, token);
        }
        else if (HasAnyLegacyTransition())
        {
            yield return PlayLegacyOutAndWait(token);

            if (token != _token)
                yield break;

            SetText(def);

            if (token != _token)
                yield break;

            PlayLegacyInNow();
        }
        else
        {
            yield return FadeTo(0f, fadeOutSeconds);
            if (hiddenHoldSeconds > 0f)
                yield return new WaitForSecondsRealtime(hiddenHoldSeconds);

            if (token != _token)
                yield break;

            SetText(def);
            yield return FadeTo(1f, fadeInSeconds);
        }

        if (token == _token)
            _routine = null;
    }

    private IEnumerator PlayPresetSequenceWithTimeout(
        TextAnimationSequence sequence,
        TextAnimationContext context,
        int token)
    {
        if (animationGroup == null || sequence == null)
            yield break;

        int playbackId = animationGroup.Play(sequence, context);
        float started = Time.unscaledTime;

        while (token == _token && animationGroup.IsPlaybackActive(playbackId))
        {
            if (transitionTimeoutSeconds > 0f &&
                Time.unscaledTime - started >= transitionTimeoutSeconds)
            {
                animationGroup.CompleteCurrent();

                // Wait briefly for the group coroutine to launch and resolve any
                // delayed tracks. This keeps the text swap from racing a final
                // OUT track on an unusually ordered frame.
                float settleStarted = Time.unscaledTime;
                while (token == _token &&
                       animationGroup.IsPlaybackActive(playbackId) &&
                       Time.unscaledTime - settleStarted < 0.25f)
                {
                    yield return null;
                }
                break;
            }

            yield return null;
        }
    }

    private static TextAnimationContext BuildContext(Vector2 direction)
    {
        return TextAnimationContext.Default.WithDirection(direction);
    }

    private static Vector2 DetermineNavigationDirection(int previousIndex, int newIndex)
    {
        if (previousIndex < 0 || previousIndex == newIndex)
            return Vector2.right;

        return newIndex > previousIndex ? Vector2.right : Vector2.left;
    }

    private void SetText(LevelDefinition def)
    {
        if (def == null)
            return;

        if (levelNumberText != null)
        {
            string digits = def.levelNumber.ToString(
                new string('0', Mathf.Max(1, stageDigits)));
            levelNumberText.text = $"{stagePrefix}{digits}";
        }

        if (levelTitleText != null)
            levelTitleText.text = def.levelTitle;

        if (anomalyTypeLabelText != null)
            anomalyTypeLabelText.text = anomalyTypeLabel;

        if (anomalyTypeNameText != null)
        {
            string value = def.anomalyTypeName;
            anomalyTypeNameText.text = string.IsNullOrEmpty(value)
                ? anomalyTypeFallback
                : value;
        }
    }

    private bool UsesPresetSystem()
    {
        return preferPresetSystem &&
               animationGroup != null &&
               selectionOutSequence != null &&
               selectionInSequence != null;
    }

    private bool HasAnyLegacyTransition()
    {
        return groupTransition != null ||
               numberTransition != null ||
               titleTransition != null;
    }

    private void PlayLegacyInNow()
    {
        if (groupTransition != null)
        {
            groupTransition.PlayIn();
        }
        else
        {
            if (numberTransition != null)
                numberTransition.PlayIn();
            if (titleTransition != null)
                titleTransition.PlayIn();
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    private IEnumerator PlayLegacyOutAndWait(int token)
    {
        float started = Time.unscaledTime;
        _groupOutDone = _numOutDone = _titleOutDone = false;

        if (groupTransition != null)
        {
            groupTransition.PlayOut();
        }
        else
        {
            if (numberTransition != null)
                numberTransition.PlayOut();
            if (titleTransition != null)
                titleTransition.PlayOut();
        }

        while (token == _token)
        {
            if (groupTransition != null)
            {
                if (_groupOutDone)
                    break;
            }
            else
            {
                bool numberDone = numberTransition == null || _numOutDone;
                bool titleDone = titleTransition == null || _titleOutDone;
                if (numberDone && titleDone)
                    break;
            }

            if (transitionTimeoutSeconds > 0f &&
                Time.unscaledTime - started >= transitionTimeoutSeconds)
                break;

            yield return null;
        }
    }

    private IEnumerator FadeTo(float targetAlpha, float seconds)
    {
        if (canvasGroup == null)
            yield break;

        float startAlpha = canvasGroup.alpha;

        if (seconds <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / seconds;
            float eased = fadeEase != null
                ? fadeEase.Evaluate(Mathf.Clamp01(t))
                : Mathf.SmoothStep(0f, 1f, t);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }
}
