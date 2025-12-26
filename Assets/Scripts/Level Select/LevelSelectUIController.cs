using System.Collections;
using TMPro;
using UnityEngine;

public class LevelSelectUIController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text levelNumberText;
    [SerializeField] private TMP_Text levelTitleText;

    [Header("Timing")]
    [SerializeField] private float fadeOutSeconds = 0.12f;
    [SerializeField] private float hiddenHoldSeconds = 0.06f;
    [SerializeField] private float fadeInSeconds = 0.18f;
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine _routine;

    private void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 1f;
    }

    public void Bind(LevelCarouselController carousel)
    {
        if (carousel == null) return;

        carousel.OnSelectionChanged += HandleSelectionChanged;

        // Prime immediately
        if (carousel.SelectedLevel != null)
            SetText(carousel.SelectedLevel);

        // Ensure visible
        if (group != null) group.alpha = 1f;
    }

    private void HandleSelectionChanged(LevelDefinition def, int index)
    {
        if (def == null) return;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(TransitionRoutine(def));
    }

    private IEnumerator TransitionRoutine(LevelDefinition def)
    {
        yield return FadeTo(0f, fadeOutSeconds);

        if (hiddenHoldSeconds > 0f)
            yield return new WaitForSecondsRealtime(hiddenHoldSeconds);

        SetText(def);

        yield return FadeTo(1f, fadeInSeconds);
    }

    private void SetText(LevelDefinition def)
    {
        if (levelNumberText != null) levelNumberText.text = $"{def.levelNumber:00}";
        if (levelTitleText != null) levelTitleText.text = def.levelTitle;
    }

    private IEnumerator FadeTo(float target, float seconds)
    {
        if (group == null) yield break;

        float start = group.alpha;

        if (seconds <= 0f)
        {
            group.alpha = target;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / seconds;
            float e = ease != null ? ease.Evaluate(Mathf.Clamp01(t)) : Mathf.SmoothStep(0, 1, t);
            group.alpha = Mathf.Lerp(start, target, e);
            yield return null;
        }

        group.alpha = target;
    }
}