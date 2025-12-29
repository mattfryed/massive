using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SymmetryKnotVisual))]
public class SymmetryKnotController : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField] private float lifetimeSeconds = 12f;
    [SerializeField] private float despawnFadeSeconds = 1.0f;

    [Header("Capture Zone")]
    [SerializeField] private float captureRadius = 3.0f;
    [SerializeField] private LayerMask playerLayerMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Tooltip("If no hits are found with Ignore, try again with Collide (useful if player hitboxes are triggers).")]
    [SerializeField] private bool fallbackToTriggerCollide = true;

    [SerializeField] private bool ignoreEliminatedPlayers = true;
    [SerializeField] private bool ignoreInactivePlayers = true;

    [Header("Scoring Stream")]
    [SerializeField] private float massPerSecond = 0.06f;
    [SerializeField] private float captureRampSeconds = 2.0f;
    [SerializeField] private bool contestedPausesFlow = true;

    [Header("Goal Mouths")]
    [SerializeField] private List<SymmetryKnotGoalMouth> goalMouths = new();

    [Tooltip("If list is empty, auto-find all SymmetryKnotGoalMouths in the scene on Start.")]
    [SerializeField] private bool autoFindGoalMouthsIfEmpty = true;

    [Header("Messaging")]
    [SerializeField] private string goalMouthMessage = "OnSymmetryKnotMass";

    private SymmetryKnotVisual visual;
    private Vector3 sourcePos;
    private float startTime;
    private bool initialized;

    private int ownerTeamID = -1;
    private bool isContested;
    private float ownerHoldTime;

    private readonly Collider[] overlap = new Collider[64];
    private readonly Dictionary<int, int> teamCounts = new Dictionary<int, int>(8);

    public void Initialize(Vector3 sourceWorldPos)
    {
        sourcePos = sourceWorldPos;
        startTime = Time.time;
        initialized = true;

        visual = GetComponent<SymmetryKnotVisual>();
        visual.Initialize(sourcePos, captureRadius);
        visual.SetOwner(-1, null, contested: false, hold01: 0f);
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
        // If user didn’t call Initialize, initialize once using current transform.
        if (!initialized)
            Initialize(transform.position);

        // Lifetime
        float age = Time.time - startTime;
        if (age >= lifetimeSeconds)
        {
            visual.BeginDespawn(despawnFadeSeconds);
            Destroy(gameObject, despawnFadeSeconds + 0.05f);
            return;
        }

        // Capture evaluation
        EvaluateCapture(out int candidateOwner, out bool contestedNow);

        if (!contestedNow && candidateOwner != ownerTeamID)
        {
            ownerTeamID = candidateOwner;
            ownerHoldTime = 0f;
        }

        isContested = contestedNow;

        float hold01 = 0f;

        // Award mass only when claimed and not contested (per your spec)
        if (ownerTeamID != -1 && !(isContested && contestedPausesFlow))
        {
            ownerHoldTime += Time.deltaTime;
            hold01 = (captureRampSeconds <= 0.001f) ? 1f : Mathf.Clamp01(ownerHoldTime / captureRampSeconds);

            float delta = (massPerSecond * hold01) * Time.deltaTime;

            var mouth = GetGoalMouth(ownerTeamID);
            if (mouth != null && delta > 0f)
                mouth.gameObject.SendMessage(goalMouthMessage, delta, SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            if (!(isContested && contestedPausesFlow))
                ownerHoldTime = 0f;
        }

        var ownerMouth = GetGoalMouth(ownerTeamID);
        visual.SetOwner(ownerTeamID, ownerMouth ? ownerMouth.transform : null, contested: isContested, hold01: hold01);
    }

    private void AutoPopulateGoalMouths()
    {
        goalMouths = new List<SymmetryKnotGoalMouth>(FindObjectsOfType<SymmetryKnotGoalMouth>(includeInactive: true));
    }

    private SymmetryKnotGoalMouth GetGoalMouth(int teamID)
    {
        if (goalMouths == null) return null;

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

        // If we ignored triggers and got nothing, try again including triggers (common setup)
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

        candidateOwner = ownerTeamID; // keep last owner visually, but treat as contested
        contestedNow = true;
    }
}
