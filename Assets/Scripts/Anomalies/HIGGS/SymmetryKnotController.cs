using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SymmetryKnotVisual))]
public class SymmetryKnotController : MonoBehaviour
{
    [Header("Lifetime (fallback)")]
    [SerializeField] private float lifetimeSeconds = 12f;

    [Tooltip("Knot outro duration (seconds). This is NOT part of Unclaimed/Claimed seconds.")]
    [SerializeField] private float despawnFadeSeconds = 1.0f;

    [Header("Excitation-driven Timing (preferred)")]
    [SerializeField] private bool useExcitationDrivenLifetime = true;

    [Tooltip("Stable time (seconds) AFTER the knot has fully animated in, BEFORE outro starts.")]
    [SerializeField] private float unclaimedSeconds = 4.0f;

    [Tooltip("Stable time (seconds) AFTER claim, BEFORE outro starts (only extends if longer than remaining).")]
    [SerializeField] private float claimedSeconds = 10.0f;

    [Tooltip("Seconds AFTER knot outro completes before the excitation dies (tail).")]
    [SerializeField] private float excitationTailSeconds = 0.75f;

    [Header("Capture Zone")]
    [SerializeField] private float captureRadius = 3.0f;
    [SerializeField] private LayerMask playerLayerMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private bool fallbackToTriggerCollide = true;
    [SerializeField] private bool ignoreEliminatedPlayers = true;
    [SerializeField] private bool ignoreInactivePlayers = true;

    [Header("Scoring Stream")]
    [SerializeField] private float massPerSecond = 0.06f;
    [SerializeField] private float captureRampSeconds = 2.0f;
    [SerializeField] private bool contestedPausesFlow = true;

    [Header("Goal Mouths")]
    [SerializeField] private List<SymmetryKnotGoalMouth> goalMouths = new();
    [SerializeField] private bool autoFindGoalMouthsIfEmpty = true;

    [Header("Messaging")]
    [SerializeField] private string goalMouthMessage = "OnSymmetryKnotMass";

    // Runtime
    private SymmetryKnotVisual visual;
    private Vector3 sourcePos;
    private bool initialized;

    private int ownerTeamID = -1;
    private bool isContested;
    private float ownerHoldTime;

    // Excitation binding
    public int ExcitationId { get; private set; } = -1;
    private float excitationEndTime = float.PositiveInfinity;   // absolute world time
    private float despawnStartTime = float.PositiveInfinity;    // absolute world time (outro begins)
    private bool despawnTriggered;

    private HiggsFieldGPU higgsField; // to extend excitation lifetime
    private float spawnTime;
    private float spawnInSeconds;

    private readonly Collider[] overlap = new Collider[64];
    private readonly Dictionary<int, int> teamCounts = new Dictionary<int, int>(8);

    public void Initialize(Vector3 sourceWorldPos)
    {
        sourcePos = sourceWorldPos;
        transform.position = sourceWorldPos;

        initialized = true;

        visual = GetComponent<SymmetryKnotVisual>();
        visual.Initialize(sourcePos, captureRadius);
        visual.SetOwner(-1, null, contested: false, hold01: 0f);

        spawnInSeconds = visual != null ? visual.SpawnFadeSeconds : 0f;

        if (autoFindGoalMouthsIfEmpty && (goalMouths == null || goalMouths.Count == 0))
            AutoPopulateGoalMouths();

        // fallback schedule if not bound
        if (despawnStartTime == float.PositiveInfinity)
            despawnStartTime = Time.time + spawnInSeconds + lifetimeSeconds;
    }

    /// <summary>
    /// Called by HiggsExcitationKnotManager when this knot is paired with a bubble excitation.
    /// </summary>
    public void BindToExcitation(
        HiggsFieldGPU higgsField,
        int excitationId,
        float currentExcitationEndTime,
        float unclaimedSeconds,
        float claimedSeconds,
        float excitationTailSeconds
    )
    {
        this.higgsField = higgsField;

        ExcitationId = excitationId;

        // Never shrink from readback. Readback might be short early; we compute our own desired.
        excitationEndTime = Mathf.Max(excitationEndTime, currentExcitationEndTime);

        this.unclaimedSeconds = unclaimedSeconds;
        this.claimedSeconds = claimedSeconds;
        this.excitationTailSeconds = excitationTailSeconds;

        useExcitationDrivenLifetime = true;

        spawnTime = Time.time;
        spawnInSeconds = (visual != null) ? visual.SpawnFadeSeconds : 0f;

        // Stable window starts AFTER spawn-in completes
        despawnStartTime = spawnTime + spawnInSeconds + this.unclaimedSeconds;

        // Ensure the excitation outlives the knot timeline
        RefreshExcitationHold();
    }

    /// <summary>
    /// Called by manager readback. Only ever EXTENDS (never shrinks) to avoid jitter shortening your planned lifetime.
    /// </summary>
    public void UpdateExcitationEndTime(float newExcitationEndTime)
    {
        excitationEndTime = Mathf.Max(excitationEndTime, newExcitationEndTime);
    }

    private void Awake()
    {
        visual = GetComponent<SymmetryKnotVisual>();
    }

    private void Start()
    {
        if (autoFindGoalMouthsIfEmpty && (goalMouths == null || goalMouths.Count == 0))
            AutoPopulateGoalMouths();
    }

    private void Update()
    {
        if (!initialized)
            Initialize(transform.position);

        // Keep capture center aligned if a manager ever moves the knot transform
        sourcePos = transform.position;

        bool excitationActive = (ExcitationId == -1) || (Time.time <= excitationEndTime);

        // Evaluate capture while excitation is active and knot is alive
        if (excitationActive && !despawnTriggered)
        {
            EvaluateCapture(out int candidateOwner, out bool contestedNow);

            // Claim transition
            if (!contestedNow && candidateOwner != ownerTeamID)
            {
                bool wasUnclaimed = (ownerTeamID == -1);

                ownerTeamID = candidateOwner;
                ownerHoldTime = 0f;

                // First claim extends stable lifetime
                if (useExcitationDrivenLifetime && wasUnclaimed && ownerTeamID != -1)
                {
                    // Extend stable phase from NOW (not counting any intro/outro)
                    despawnStartTime = Mathf.Max(despawnStartTime, Time.time + claimedSeconds);
                    RefreshExcitationHold();
                }
            }

            isContested = contestedNow;

            // Scoring only when claimed, not contested
            float hold01 = 0f;
            if (ownerTeamID != -1 && !(isContested && contestedPausesFlow))
            {
                if (autoFindGoalMouthsIfEmpty && (goalMouths == null || goalMouths.Count == 0))
                    AutoPopulateGoalMouths();

                ownerHoldTime += Time.deltaTime;
                hold01 = (captureRampSeconds <= 0.001f) ? 1f : Mathf.Clamp01(ownerHoldTime / captureRampSeconds);

                float delta = (massPerSecond * hold01) * Time.deltaTime;

                var mouth = GetGoalMouth(ownerTeamID);
                if (mouth != null && delta > 0f)
                    mouth.gameObject.SendMessage(goalMouthMessage, delta, SendMessageOptions.DontRequireReceiver);

                visual.SetOwner(ownerTeamID, mouth ? mouth.transform : null, contested: isContested, hold01: hold01);
            }
            else
            {
                if (!(isContested && contestedPausesFlow))
                    ownerHoldTime = 0f;

                visual.SetOwner(ownerTeamID, GetGoalMouth(ownerTeamID)?.transform, contested: isContested, hold01: 0f);
            }
        }
        else
        {
            // Excitation ended -> retract visuals / no flow
            if (ownerTeamID != -1 || isContested)
            {
                ownerTeamID = -1;
                isContested = false;
                ownerHoldTime = 0f;
            }
            visual.SetOwner(-1, null, contested: false, hold01: 0f);
        }

        // Despawn check AFTER capture (so late entry can still extend)
        if (!despawnTriggered && ShouldBeginDespawnNow())
        {
            despawnTriggered = true;
            visual.BeginDespawn(despawnFadeSeconds);
            Destroy(gameObject, despawnFadeSeconds + 0.05f);
        }
    }

    /// <summary>
    /// Ensures excitation outlives the full knot timeline:
    /// knotOutro begins at despawnStartTime,
    /// knot disappears at despawnStartTime + despawnFadeSeconds,
    /// excitation dies after that + excitationTailSeconds.
    /// </summary>
    private void RefreshExcitationHold()
    {
        if (ExcitationId == -1)
            return;

        float desiredExcitationEnd = despawnStartTime + despawnFadeSeconds + excitationTailSeconds;

        // Internal cap (knot logic uses this too)
        excitationEndTime = Mathf.Max(excitationEndTime, desiredExcitationEnd);

        // Drive the actual Higgs excitation lifetime (requires compute patch below)
        if (higgsField != null)
            higgsField.SetExcitationHoldUntil(ExcitationId, desiredExcitationEnd);
    }

    private bool ShouldBeginDespawnNow()
    {
        float start = despawnStartTime;

        // If bound, ensure we do not start despawn so late that the knot would outlive excitation
        if (useExcitationDrivenLifetime && ExcitationId != -1 && excitationEndTime < float.PositiveInfinity)
        {
            float capStart = excitationEndTime - (despawnFadeSeconds + excitationTailSeconds);
            start = Mathf.Min(start, capStart);
        }

        return Time.time >= start;
    }

    private void AutoPopulateGoalMouths()
    {
        goalMouths = new List<SymmetryKnotGoalMouth>(FindObjectsOfType<SymmetryKnotGoalMouth>(includeInactive: true));
    }

    private SymmetryKnotGoalMouth GetGoalMouth(int teamID)
    {
        if (teamID == -1) return null;
        if (goalMouths == null || goalMouths.Count == 0) return null;

        for (int i = 0; i < goalMouths.Count; i++)
        {
            if (goalMouths[i] != null && goalMouths[i].teamID == teamID)
                return goalMouths[i];
        }
        return null;
    }

    private void EvaluateCapture(out int candidateOwner, out bool contestedNow)
    {
        teamCounts.Clear();

        int hitCount = Physics.OverlapSphereNonAlloc(sourcePos, captureRadius, overlap, playerLayerMask, triggerInteraction);

        if (hitCount == 0 && fallbackToTriggerCollide && triggerInteraction == QueryTriggerInteraction.Ignore)
        {
            hitCount = Physics.OverlapSphereNonAlloc(sourcePos, captureRadius, overlap, playerLayerMask, QueryTriggerInteraction.Collide);
        }

        for (int i = 0; i < hitCount; i++)
        {
            var col = overlap[i];
            if (!col) continue;

            var pc = col.GetComponentInParent<PlayerControllerScript>();
            if (!pc) continue;

            if (ignoreEliminatedPlayers && pc.temporarilyEliminated) continue;
            if (ignoreInactivePlayers && !pc.isActive) continue;

            int team = pc.teamID;

            if (teamCounts.TryGetValue(team, out int c))
                teamCounts[team] = c + 1;
            else
                teamCounts[team] = 1;
        }

        if (teamCounts.Count == 0)
        {
            candidateOwner = -1;
            contestedNow = false;
            return;
        }

        if (teamCounts.Count == 1)
        {
            foreach (var kv in teamCounts)
            {
                candidateOwner = kv.Key;
                contestedNow = false;
                return;
            }
        }

        candidateOwner = ownerTeamID;
        contestedNow = true;
    }
}
