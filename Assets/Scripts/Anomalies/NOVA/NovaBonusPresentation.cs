using System.Globalization;
using Massive.Scoring;
using TMPro;
using UnityEngine;

/// <summary>Scene-owned bonus HUD. Values are receipts from the score authority.</summary>
[DisallowMultipleComponent]
public sealed class NovaBonusPresentation : MonoBehaviour
{
    [Header("Scene bindings")]
    public GameManagerScript match;
    public Camera worldCamera;
    public ArenaBoundsFromVectorGrid arenaBounds;
    public Transform lightGoal, darkGoal;
    public GameObject[] regulationHudRoots;

    [Header("Bonus footer")]
    public RectTransform canvasRect, footer;
    public TMP_Text lightCount, darkCount, lightEnergy, darkEnergy;
    public NovaCaptureFeedback captureFeedback;
    [Min(0f)] public float fieldGap = 12f;
    [Min(0f)] public float screenMargin = 18f;

    NovaCoreMinigame owner;
    RectTransform playArea;
    bool[] previousHudStates;
    bool hudHidden;
    readonly Vector3[] corners = new Vector3[4];

    public bool HasSession => owner != null;

    void Awake()
    {
        if (footer) footer.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (match) match.PhaseChanged += OnPhaseChanged;
    }

    public void BeginSession(NovaCoreMinigame minigame, RectTransform area)
    {
        owner = minigame;
        playArea = area;
        captureFeedback.Clear();
        if (!hudHidden)
        {
            previousHudStates = new bool[regulationHudRoots.Length];
            for (int i = 0; i < regulationHudRoots.Length; i++)
            {
                var root = regulationHudRoots[i];
                previousHudStates[i] = root && root.activeSelf;
                if (root) root.SetActive(false);
            }
            hudHidden = true;
        }
        SetTotals(0, 0, 0, 0);
        FitFooter();
        footer.gameObject.SetActive(true);
    }

    public void SetTotals(int lightParticles, int darkParticles, long lightPoints, long darkPoints)
    {
        lightCount.text = lightParticles.ToString("00", CultureInfo.InvariantCulture);
        darkCount.text = darkParticles.ToString("00", CultureInfo.InvariantCulture);
        lightEnergy.text = FormatEnergy(lightPoints);
        darkEnergy.text = FormatEnergy(darkPoints);
    }

    public static string FormatEnergy(long points) => points.ToString("N0", CultureInfo.InvariantCulture) + " meV";

    public void ShowCapture(int team, long points, Vector3 position)
    {
        if (!owner || points <= 0) return;
        var goal = team == 1 ? lightGoal : team == 2 ? darkGoal : null;
        if (goal) captureFeedback.Show(team, points, position, goal, worldCamera);
    }

    public void BeginOutro()
    {
        if (footer) footer.gameObject.SetActive(false);
        captureFeedback.Clear();
    }

    public void EndSession(NovaCoreMinigame minigame)
    {
        if (owner != minigame) return;
        BeginOutro();
        owner = null;
        playArea = null;
        // The terminal finale must not flash the regulation HUD before results.
        if (!match || !match.IsTerminalBonus) RestoreHud();
    }

    void LateUpdate()
    {
        if (owner && footer.gameObject.activeSelf) FitFooter();
    }

    void FitFooter()
    {
        if (!worldCamera || !canvasRect || !footer) return;
        float bottom = Screen.height;
        if (playArea)
        {
            playArea.GetWorldCorners(corners);
            foreach (var corner in corners) bottom = Mathf.Min(bottom, worldCamera.WorldToScreenPoint(corner).y);
        }
        // Use the actual grid footprint, not its much larger VFX draw bounds.
        if (arenaBounds && arenaBounds.IsValid)
        {
            var grid = arenaBounds.Grid;
            var half = grid.size * .5f;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    bottom = Mathf.Min(bottom, worldCamera.WorldToScreenPoint(
                        grid.transform.TransformPoint(new Vector3(half.x * x, half.y * y, 0f))).y);
        }
        float unitsPerPixel = canvasRect.rect.height / Mathf.Max(1, Screen.height);
        float height = Mathf.Max(0f, bottom * unitsPerPixel - fieldGap - screenMargin);
        footer.anchorMin = Vector2.zero;
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(.5f, 0f);
        footer.anchoredPosition = new Vector2(0f, screenMargin);
        footer.sizeDelta = new Vector2(-screenMargin * 2f, height);
    }

    void OnPhaseChanged(MatchRuntimePhase phase)
    {
        if (phase == MatchRuntimePhase.Preparing || phase == MatchRuntimePhase.Regulation)
            RestoreHud();
        else if (phase == MatchRuntimePhase.Resolving || phase == MatchRuntimePhase.Complete)
            BeginOutro();
    }

    void RestoreHud()
    {
        if (!hudHidden) return;
        for (int i = 0; i < regulationHudRoots.Length; i++)
            if (regulationHudRoots[i]) regulationHudRoots[i].SetActive(previousHudStates[i]);
        hudHidden = false;
    }

    void OnDisable()
    {
        if (match) match.PhaseChanged -= OnPhaseChanged;
        BeginOutro();
        RestoreHud();
        owner = null;
    }
}
