using Rewired;
using System.Collections;
using Shapes;
using TMPro;
using UnityEngine;

/// <summary>Cycles prefab instances in hierarchy order; the existing controller owns Begin.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-11000)]
public sealed class HowToPlayCarousel : MonoBehaviour
{
    [Header("Panes")]
    [Tooltip("Place HowToPlayPane prefab instances directly under this object. Sibling order is playback order.")]
    [SerializeField] private Transform paneRoot;
    [SerializeField] private Camera screenCamera;
    [Min(1f)] [SerializeField] private float secondsPerPane = 12f;
    [SerializeField] private HowToPlayDemoWipe demoTransition;

    [Header("Persistent navigation")]
    [SerializeField] private Rectangle progressFill;
    [SerializeField] private TMP_Text pageNumber;
    [SerializeField] private GameObject navigationRoot;
    [Min(.1f)] [SerializeField] private float barWidth = 28f;

    [Header("Any player's joystick")]
    [SerializeField] private string horizontalAction = "MoveH";
    [Range(.1f, 1f)] [SerializeField] private float pressThreshold = .55f;
    [Range(0f, .5f)] [SerializeField] private float releaseThreshold = .25f;
    [SerializeField] private bool allowKeyboardFallback = true;

    private HowToPlayPane[] panes;
    private int currentIndex = -1;
    private int heldDirection;
    private float elapsed;
    private Vector3 barCenter;
    private Coroutine transitionRoutine;
    private int requestedIndex;
    private int requestedDirection;
    private bool requestedManually;

    public int CurrentIndex => currentIndex;
    public int PaneCount => panes == null ? 0 : panes.Length;
    public bool IsTransitioning => transitionRoutine != null;
    public float Progress => PaneCount == 0 ? 0f : Mathf.Clamp01(elapsed / CurrentDuration);
    /// <summary>One notification per manual pane change, including wraparound.</summary>
    public event System.Action<int> Navigated;
    private float CurrentDuration => Mathf.Max(1f, panes[currentIndex].DurationOverride > 0f
        ? panes[currentIndex].DurationOverride : secondsPerPane);

    private void Awake()
    {
        SceneSfxPolicy.BeginSilence(this);
        if (!paneRoot || !screenCamera)
        {
            Debug.LogError("How To carousel needs a pane root and screen camera.", this);
            enabled = false;
            return;
        }

        var children = new System.Collections.Generic.List<HowToPlayPane>();
        foreach (Transform child in paneRoot)
        {
            if (!child.TryGetComponent<HowToPlayPane>(out var pane)) continue;
            pane.gameObject.SetActive(false);
            pane.Bind(screenCamera);
            children.Add(pane);
        }
        panes = children.ToArray();
        if (progressFill) barCenter = progressFill.transform.localPosition;
        if (navigationRoot) navigationRoot.SetActive(PaneCount > 1);
        if (PaneCount > 0) ShowPane(0);
        else
        {
            if (progressFill) progressFill.gameObject.SetActive(false);
            if (pageNumber) pageNumber.text = "";
            Debug.LogWarning("How To carousel has no pane prefabs under its pane root.", this);
        }
    }

    private void OnEnable() => heldDirection = 0;
    private void OnDisable()
    {
        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        transitionRoutine = null;
        if (demoTransition) demoTransition.Finish();
        if (currentIndex >= 0 && panes != null) panes[currentIndex].SetDemosHeld(false);
        requestedIndex = Mathf.Max(0, currentIndex);
    }
    private void OnDestroy() => SceneSfxPolicy.EndSilence(this);
    private void OnApplicationQuit() => SceneSfxPolicy.EndSilence(this);

    private void Update()
    {
        if (PaneCount == 0) return;
        int direction = ConsumeDirection(ReadHorizontal());
        if (direction != 0)
        {
            Navigate(direction);
            return;
        }

        if (IsTransitioning) return;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= CurrentDuration) RequestNavigation(1, false);
        UpdateProgress();
    }

    public void Navigate(int direction) => RequestNavigation(direction, true);

    private void RequestNavigation(int direction, bool manual)
    {
        if (PaneCount == 0 || direction == 0) return;
        int step = direction > 0 ? 1 : -1;
        requestedIndex = (requestedIndex + step + PaneCount) % PaneCount;
        requestedDirection = step;
        requestedManually = manual;
        if (IsTransitioning) return; // Coalesce rapid input into the next requested pane.
        if (!demoTransition || !demoTransition.CanCapture || PaneCount < 2)
        {
            bool changed = requestedIndex != currentIndex;
            ShowPane(requestedIndex);
            if (changed && manual) Navigated?.Invoke(step);
            return;
        }
        if (requestedIndex != currentIndex) transitionRoutine = StartCoroutine(TransitionPanes());
    }

    private IEnumerator TransitionPanes()
    {
        while (requestedIndex != currentIndex)
        {
            // Capture the completed camera stack, including all demo viewports and controls.
            yield return new WaitForEndOfFrame();
            if (requestedIndex == currentIndex) break;
            bool captured = demoTransition && demoTransition.CaptureOutgoing();
            int direction = requestedDirection;
            bool manual = requestedManually;
            ShowPane(requestedIndex, captured);
            if (captured)
            {
                float deadline = Time.unscaledTime + 2f;
                while (!panes[currentIndex].DemosReady && Time.unscaledTime < deadline) yield return null;
                if (demoTransition) demoTransition.Reveal(direction);
                if (manual) Navigated?.Invoke(direction);
                while (demoTransition && demoTransition.IsRevealing) yield return null;
                if (demoTransition) demoTransition.Finish();
            }
            else if (manual) Navigated?.Invoke(direction);
            // Queued navigation continues from this frozen presentation without starting an action.
            if (requestedIndex == currentIndex) panes[currentIndex].SetDemosHeld(false);
            elapsed = 0f;
            UpdateProgress();
        }
        panes[currentIndex].SetDemosHeld(false);
        transitionRoutine = null;
    }

    private void ShowPane(int index, bool holdDemos = false)
    {
        if (currentIndex >= 0)
        {
            panes[currentIndex].gameObject.SetActive(false);
            panes[currentIndex].SetDemosHeld(false);
        }
        currentIndex = index;
        elapsed = 0f;
        panes[currentIndex].SetDemosHeld(holdDemos);
        panes[currentIndex].gameObject.SetActive(true);
        if (pageNumber) pageNumber.text = $"{currentIndex + 1:00} / {PaneCount:00}";
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        if (!progressFill) return;
        float width = barWidth * Progress;
        progressFill.Width = Mathf.Max(.001f, width);
        progressFill.transform.localPosition = barCenter + Vector3.left * ((barWidth - width) * .5f);
    }

    private float ReadHorizontal()
    {
        bool left = false, right = false;
        float value = 0f;
        if (ReInput.isReady)
        {
            var players = ReInput.players.Players;
            for (int i = 0; i < players.Count; i++)
            {
                float axis = players[i].GetAxis(horizontalAction);
                left |= axis < -releaseThreshold;
                right |= axis > releaseThreshold;
                if (Mathf.Abs(axis) > Mathf.Abs(value)) value = axis;
            }
            // Opposing players cancel. A held stick cannot race through the panes.
            if (left && right) return 0f;
        }

        if (allowKeyboardFallback)
        {
            int keys = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            if (keys != 0) value = keys;
        }
        return value;
    }

    private int ConsumeDirection(float axis)
    {
        if (Mathf.Abs(axis) <= releaseThreshold) { heldDirection = 0; return 0; }
        if (Mathf.Abs(axis) < pressThreshold) return 0;
        int direction = axis > 0f ? 1 : -1;
        if (direction == heldDirection) return 0;
        heldDirection = direction;
        return direction;
    }

    private void OnValidate()
    {
        secondsPerPane = Mathf.Max(1f, secondsPerPane);
        releaseThreshold = Mathf.Min(releaseThreshold, pressThreshold - .05f);
    }
}
