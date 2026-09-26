using System;
using System.Collections;
using System.Collections.Generic;
using Massive.Scoring;
using UnityEngine;

/// <summary>Four regulation stages and one terminal Core Collapse, driven by the shared match clock.</summary>
[RequireComponent(typeof(Collider))]
public class NovaStarController : MonoBehaviour, IMatchFinale
{
    public GameManagerScript match;
    public NovaStarRumble rumble;
    [Header("Final entry")]
    [Min(0f)] public float entryWindowDuration = 6f;
    public Collider entryCollider;
    public GameObject entryRingVisual;
    public PlayerRosterController roster;
    [Header("Mini nova matter")]
    public MatterNuggetScript nuggetPrefab;
    public MatterNuggetScript nuggletPrefab;
    public Transform matterRoot;
    [Min(0f)] public float ejectionRadius = 2.1f;
    [Min(0.1f)] public float pickupLifetime = 14f;
    public Vector2 ejectionSpeed = new Vector2(2.5f, 6f);
    [Min(0f)] public float transitionSeconds = 1.2f;

    public const int StageCount = 5;
    public int CurrentStage { get; private set; } = 1;
    public int BurstsReleased { get; private set; }
    public bool IsEntryWindowOpen { get; private set; }
    public bool FinaleStarted { get; private set; }
    public event Action<int> OnStageChanged;
    public event Action OnEntryWindowOpened;
    public event Action<List<PlayerControllerScript>> OnEntryWindowClosed;
    public event Action OnBounceTriggered;
    public event Action OnFinalSupernova;
    private int pendingStage = 1;
    private Coroutine progression;
    private bool supernovaSent;
    private readonly List<MatterNuggetScript> pickups = new();

    private void Awake()
    {
        if (!rumble) rumble = GetComponent<NovaStarRumble>();
        SetEntryOpen(false);
    }
    private void OnEnable() { if (match) match.RegulationTimeChanged += OnRegulationTime; }
    private void Start() { if (rumble) rumble.SetStage(1); }

    public static int StageAt(float remaining, float duration) =>
        Mathf.Clamp(1 + Mathf.FloorToInt((1f - Mathf.Clamp01(remaining / Mathf.Max(1f, duration))) * 4f), 1, 4);

    private void OnRegulationTime(float remaining)
    {
        if (!match || match.Phase != MatchRuntimePhase.Regulation || FinaleStarted || remaining <= 0f) return;
        pendingStage = StageAt(remaining, match.RegulationDurationSeconds);
        if (pendingStage > CurrentStage && progression == null)
            progression = StartCoroutine(AdvanceStages());
    }
    private IEnumerator AdvanceStages()
    {
        while (CurrentStage < pendingStage && !FinaleStarted)
        {
            int next = CurrentStage + 1;
            float seconds = Mathf.Min(transitionSeconds, match.RegulationDurationSeconds / 12f);
            if (rumble) yield return rumble.WindUp(next, seconds * .5f);
            if (FinaleStarted) yield break;
            CurrentStage = next;
            OnStageChanged?.Invoke(CurrentStage);
            ReleaseMatter(next);
            OnBounceTriggered?.Invoke();
            if (rumble) yield return rumble.GrowTo(next, seconds * .5f);
        }
        progression = null;
    }
    private void ReleaseMatter(int stage)
    {
        BurstsReleased++;
        EmitPairs(nuggetPrefab, 6 + (stage - 2) * 4, stage);
        EmitPairs(nuggletPrefab, 10 + (stage - 2) * 6, stage);
    }
    private void EmitPairs(MatterNuggetScript prefab, int count, int stage)
    {
        if (!prefab) return;
        int pairs = count / 2;
        float offset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        // Opposite pairs with angular jitter maintain broad coverage.
        for (int i = 0; i < pairs; i++)
        {
            float angle = offset + (i + UnityEngine.Random.Range(-.3f, .3f)) * Mathf.PI / pairs;
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float speed = UnityEngine.Random.Range(ejectionSpeed.x, ejectionSpeed.y) * (1f + (stage - 2) * .2f);
            for (int side = -1; side <= 1; side += 2)
            {
                var pickup = Instantiate(prefab, matterRoot);
                pickups.Add(pickup);
                pickup.Eject(transform.position + direction * (side * ejectionRadius), direction * (side * speed), pickupLifetime);
            }
        }
    }
    public void BeginMatchFinale()
    {
        if (FinaleStarted) return;
        FinaleStarted = true;
        StopAllCoroutines();
        progression = null;
        CurrentStage = StageCount;
        if (rumble) rumble.SetStage(4);
        OnStageChanged?.Invoke(CurrentStage);
        foreach (var pickup in pickups) if (pickup) Destroy(pickup.gameObject);
        pickups.Clear();
        StartCoroutine(FinalEntry());
    }
    private IEnumerator FinalEntry()
    {
        SetEntryOpen(true);
        OnEntryWindowOpened?.Invoke();
        if (rumble) yield return rumble.WindUp(5, Mathf.Max(0f, entryWindowDuration));
        else yield return new WaitForSecondsRealtime(Mathf.Max(0f, entryWindowDuration));
        if (rumble) rumble.SetStage(4);
        var entrants = new List<PlayerControllerScript>();
        foreach (var player in GetRosteredPlayers())
            if (player.gameObject.activeInHierarchy && entryCollider &&
                (entryCollider.ClosestPoint(player.transform.position) - player.transform.position).sqrMagnitude < .0001f)
                entrants.Add(player);
        SetEntryOpen(false);
        // Empty is meaningful: everyone still participates and receives the late penalty.
        OnEntryWindowClosed?.Invoke(entrants);
    }
    public List<PlayerControllerScript> GetRosteredPlayers()
    {
        var result = new List<PlayerControllerScript>();
        if (roster)
        {
            foreach (var root in new[] {roster.P1, roster.P2, roster.P3, roster.P4})
                if (roster.IsRostered(root)) AddRosterPlayer(root, result);
        }
        else
            foreach (var player in FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None))
                if (!player.IsPseudoPlayer) result.Add(player);
        result.Sort((a,b) => a.playerID.CompareTo(b.playerID));
        return result;
    }
    private static void AddRosterPlayer(GameObject root, List<PlayerControllerScript> result)
    {
        if (!root) return;
        var player = root.GetComponent<PlayerControllerScript>();
        if (player && !player.IsPseudoPlayer) result.Add(player);
    }
    public void CompleteFinale()
    {
        if (supernovaSent) return;
        supernovaSent = true;
        OnFinalSupernova?.Invoke();
    }
    private void SetEntryOpen(bool value)
    {
        IsEntryWindowOpen = value;
        if (entryCollider) { entryCollider.isTrigger = true; entryCollider.enabled = value; }
        if (entryRingVisual) entryRingVisual.SetActive(value);
    }
    private void OnDisable()
    {
        if (match) match.RegulationTimeChanged -= OnRegulationTime;
        StopAllCoroutines();
        progression = null;
        SetEntryOpen(false);
        foreach (var pickup in pickups) if (pickup) Destroy(pickup.gameObject);
        pickups.Clear();
    }
}
