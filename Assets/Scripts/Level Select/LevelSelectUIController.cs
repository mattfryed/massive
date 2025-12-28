using System.Collections;
using TMPro;
using UnityEngine;

public class LevelSelectUIController : MonoBehaviour
{
    [Header("Bind")]
    [SerializeField] private LevelCarouselController carousel;

    [Header("Texts")]
    [SerializeField] private TMP_Text levelNumberText; // TextMeshProUGUI is fine
    [SerializeField] private TMP_Text levelTitleText;

    [Header("Formatting")]
    [SerializeField] private string stagePrefix = "STAGE_";
    [SerializeField] private int stageDigits = 3; // 3 => 001, 012, 123

    [Header("Animation (choose one approach)")]
    [Tooltip("If set, we'll use this to animate OUT->swap->IN (best: place on LevelLabel with includeChildren=true).")]
    [SerializeField] private TMPTextTransition groupTransition;

    [Tooltip("Optional per-text transitions if you want different effects.")]
    [SerializeField] private TMPTextTransition numberTransition;
    [SerializeField] private TMPTextTransition titleTransition;

    [Header("Fallback (only used if no TMPTextTransition is assigned)")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeOutSeconds = 0.12f;
    [SerializeField] private float hiddenHoldSeconds = 0.06f;
    [SerializeField] private float fadeInSeconds = 0.18f;
    [SerializeField] private AnimationCurve fadeEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Safety")]
    [Tooltip("If OUT never completes (because it got interrupted), we still proceed after this many seconds.")]
    [SerializeField] private float transitionTimeoutSeconds = 1.5f;

    private Coroutine _routine;
    private int _token;

    private bool _groupOutDone, _groupInDone;
    private bool _numOutDone, _numInDone;
    private bool _titleOutDone, _titleInDone;

    private void Awake()
    {
        if (carousel == null) carousel = FindFirstObjectByType<LevelCarouselController>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        // Hook transition events (if present)
        if (groupTransition != null)
        {
            groupTransition.onOutComplete.AddListener(() => _groupOutDone = true);
            groupTransition.onInComplete.AddListener(() => _groupInDone = true);
        }
        if (numberTransition != null)
        {
            numberTransition.onOutComplete.AddListener(() => _numOutDone = true);
            numberTransition.onInComplete.AddListener(() => _numInDone = true);
        }
        if (titleTransition != null)
        {
            titleTransition.onOutComplete.AddListener(() => _titleOutDone = true);
            titleTransition.onInComplete.AddListener(() => _titleInDone = true);
        }

        if (carousel != null)
        {
            carousel.OnSelectionChanged += OnSelectionChanged;

            // Prime immediately
            if (carousel.SelectedLevel != null)
                SetText(carousel.SelectedLevel);

            // Optional: play IN once at start if you're using transitions
            if (HasAnyTransition())
                PlayInNow();
            else if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }
    }

    private void OnDestroy()
    {
        if (carousel != null)
            carousel.OnSelectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged(LevelDefinition def, int index)
    {
        if (def == null) return;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ChangeRoutine(def));
    }

    private IEnumerator ChangeRoutine(LevelDefinition def)
    {
        int myToken = ++_token;

        // OUT
        if (HasAnyTransition())
        {
            yield return PlayOutAndWait(myToken);
        }
        else
        {
            yield return FadeTo(0f, fadeOutSeconds);
            if (hiddenHoldSeconds > 0f) yield return new WaitForSecondsRealtime(hiddenHoldSeconds);
        }

        if (myToken != _token) yield break;

        // Swap text while hidden
        SetText(def);

        if (myToken != _token) yield break;

        // IN
        if (HasAnyTransition())
        {
            PlayInNow();
        }
        else
        {
            yield return FadeTo(1f, fadeInSeconds);
        }
    }

    private void SetText(LevelDefinition def)
    {
        if (levelNumberText != null)
        {
            string digits = def.levelNumber.ToString(new string('0', Mathf.Max(1, stageDigits)));
            levelNumberText.text = $"{stagePrefix}{digits}";
        }

        if (levelTitleText != null)
            levelTitleText.text = def.levelTitle;
    }

    private bool HasAnyTransition()
    {
        return groupTransition != null || numberTransition != null || titleTransition != null;
    }

    private void PlayInNow()
    {
        _groupInDone = _numInDone = _titleInDone = false;

        if (groupTransition != null) groupTransition.PlayIn();
        else
        {
            if (numberTransition != null) numberTransition.PlayIn();
            if (titleTransition != null) titleTransition.PlayIn();
        }

        // If you're using TMPTextTransition alpha, don't fight it with CanvasGroup.
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    private IEnumerator PlayOutAndWait(int myToken)
    {
        float start = Time.unscaledTime;

        _groupOutDone = _numOutDone = _titleOutDone = false;

        if (groupTransition != null) groupTransition.PlayOut();
        else
        {
            if (numberTransition != null) numberTransition.PlayOut();
            if (titleTransition != null) titleTransition.PlayOut();
        }

        // Wait until OUT completes (or timeout, or interrupted by a new selection)
        while (myToken == _token)
        {
            if (groupTransition != null)
            {
                if (_groupOutDone) break;
            }
            else
            {
                bool numOk = (numberTransition == null) || _numOutDone;
                bool titleOk = (titleTransition == null) || _titleOutDone;
                if (numOk && titleOk) break;
            }

            if (Time.unscaledTime - start >= transitionTimeoutSeconds) break;
            yield return null;
        }
    }

    private IEnumerator FadeTo(float target, float seconds)
    {
        if (canvasGroup == null) yield break;

        float start = canvasGroup.alpha;

        if (seconds <= 0f)
        {
            canvasGroup.alpha = target;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / seconds;
            float e = fadeEase != null ? fadeEase.Evaluate(Mathf.Clamp01(t)) : Mathf.SmoothStep(0, 1, t);
            canvasGroup.alpha = Mathf.Lerp(start, target, e);
            yield return null;
        }

        canvasGroup.alpha = target;
    }
}
