using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class VectorGridPresentationAnimator : MonoBehaviour
{
    [SerializeField] private VectorGridGPU grid;

    [Header("Intro: Animated Layout -> Authored Layout")]
    [Tooltip("Visual spacing multiplier at the beginning of the intro. " +
             "Values above 1 begin with larger squares; values below 1 begin with smaller squares.")]
    [Min(0.01f)]
    [SerializeField] private float introStartScale = 3f;

    [Min(0f)]
    [SerializeField] private float introDuration = 1.25f;

    [SerializeField] private AnimationCurve introEase =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Outro: Authored Layout -> Animated Layout")]
    [Tooltip("Visual spacing multiplier at the end of the outro.")]
    [Min(0.01f)]
    [SerializeField] private float outroEndScale = 3f;

    [Min(0f)]
    [SerializeField] private float outroDuration = 1f;

    [SerializeField] private AnimationCurve outroEase =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Behavior")]
    [Tooltip("Lets overscan lines render beyond VectorGridGPU.Size while a transition is running.")]
    [SerializeField] private bool ignoreOuterBoundsWhileAnimating = true;

    [Tooltip("Recommended for scene transitions so the zoom continues while gameplay is paused.")]
    [SerializeField] private bool useUnscaledTime = true;

    [SerializeField] private bool playIntroOnEnable;

    [Tooltip("Returns the grid to Presentation Scale 1 and restores clipping when this component disables.")]
    [SerializeField] private bool resetPresentationOnDisable = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onIntroComplete;
    [SerializeField] private UnityEvent onOutroComplete;

    private Coroutine _routine;

    public bool IsAnimating => _routine != null;

    private void Reset()
    {
        grid = GetComponent<VectorGridGPU>();
    }

    private void Awake()
    {
        AutoAssignGrid();
    }

    private void OnEnable()
    {
        AutoAssignGrid();

        if (playIntroOnEnable && Application.isPlaying)
            PlayIntro();
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        if (resetPresentationOnDisable && grid != null)
            grid.ResetPresentation();
    }

    private void OnValidate()
    {
        introStartScale = Mathf.Max(0.01f, introStartScale);
        outroEndScale = Mathf.Max(0.01f, outroEndScale);
        introDuration = Mathf.Max(0f, introDuration);
        outroDuration = Mathf.Max(0f, outroDuration);
        AutoAssignGrid();
    }

    public void PlayIntro()
    {
        if (!TryGetGrid())
            return;

        StartTransition(
            introStartScale,
            1f,
            introDuration,
            introEase,
            settleAtEnd: true,
            onIntroComplete);
    }

    public void PlayOutro()
    {
        if (!TryGetGrid())
            return;

        StartTransition(
            grid.PresentationScale,
            outroEndScale,
            outroDuration,
            outroEase,
            settleAtEnd: false,
            onOutroComplete);
    }

    /// <summary>
    /// Smoothly returns from the current presentation scale to the authored layout.
    /// Useful when another system set a custom starting scale first.
    /// </summary>
    public void PlayCurrentToSettled()
    {
        if (!TryGetGrid())
            return;

        StartTransition(
            grid.PresentationScale,
            1f,
            introDuration,
            introEase,
            settleAtEnd: true,
            onIntroComplete);
    }

    public void SnapToSettled()
    {
        StopCurrentTransition();
        if (grid != null)
            grid.ResetPresentation();
    }

    public void StopCurrentTransition()
    {
        if (_routine == null)
            return;

        StopCoroutine(_routine);
        _routine = null;
    }

    private void StartTransition(
        float from,
        float to,
        float duration,
        AnimationCurve ease,
        bool settleAtEnd,
        UnityEvent completionEvent)
    {
        StopCurrentTransition();

        from = Mathf.Clamp(
            from,
            grid.MinimumPresentationScale,
            grid.MaximumPresentationScale);
        to = Mathf.Clamp(
            to,
            grid.MinimumPresentationScale,
            grid.MaximumPresentationScale);

        grid.SetPresentation(
            from,
            ignoreOuterBoundsWhileAnimating);

        _routine = StartCoroutine(AnimateRoutine(
            from,
            to,
            duration,
            ease,
            settleAtEnd,
            completionEvent));
    }

    private IEnumerator AnimateRoutine(
        float from,
        float to,
        float duration,
        AnimationCurve ease,
        bool settleAtEnd,
        UnityEvent completionEvent)
    {
        if (duration <= 0f)
        {
            grid.SetPresentation(
                to,
                settleAtEnd ? false : ignoreOuterBoundsWhileAnimating);

            if (settleAtEnd)
                grid.ResetPresentation();

            _routine = null;
            completionEvent?.Invoke();
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = ease != null
                ? ease.Evaluate(normalized)
                : normalized;

            grid.SetPresentation(
                Mathf.LerpUnclamped(from, to, eased),
                ignoreOuterBoundsWhileAnimating);

            elapsed += useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            yield return null;
        }

        if (settleAtEnd)
        {
            // Exactly restore the authored presentation and clipping state.
            grid.ResetPresentation();
        }
        else
        {
            grid.SetPresentation(
                to,
                ignoreOuterBoundsWhileAnimating);
        }

        _routine = null;
        completionEvent?.Invoke();
    }

    private bool TryGetGrid()
    {
        AutoAssignGrid();

        if (grid != null)
            return true;

        Debug.LogError(
            "[VectorGridPresentationAnimator] No VectorGridGPU is assigned.",
            this);
        return false;
    }

    private void AutoAssignGrid()
    {
        if (grid == null)
            grid = GetComponent<VectorGridGPU>();
    }
}
