using System.Collections.Generic;
using Massive.Scoring;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(SymmetryKnotVisual))]
public class SymmetryKnotController : MonoBehaviour
{
    [Header("Finite encounter (scaled gameplay seconds)")]
    [SerializeField, Min(.1f)] private float unclaimedSeconds = 6f;
    [SerializeField, Min(.1f)] private float claimedSeconds = 10f;
    [SerializeField, Min(.01f)] private float despawnFadeSeconds = 1f;
    [SerializeField, Min(0)] private float captureRampSeconds = 2f;
    [Header("Capture")]
    [SerializeField, Min(.1f)] private float captureRadius = 1.15f;
    [SerializeField] private LayerMask playerLayerMask = ~0;
    [Header("Energy (one team stream; no personal multiplier or charge)")]
    [SerializeField, ScoreRewardKey] private string rewardKey = ScoreRewardKeys.HiggsControlTick;
    [SerializeField, Min(.02f)] private float tickIntervalSeconds = .25f;
    [SerializeField] private List<SymmetryKnotGoalMouth> goalMouths = new();

    private readonly Collider[] overlap = new Collider[64];
    private SymmetryKnotVisual visual;
    private SymmetryKnotCaptureState state;
    private bool retiringVisual;
    private string sourceToken;
    public float CaptureRadius => captureRadius;
    public SymmetryKnotCaptureState State => state;
    public bool IsRetiring => state != null && (state.Phase == SymmetryKnotCaptureState.Stage.Retiring || state.Phase == SymmetryKnotCaptureState.Stage.Finished);
    public bool IsFlowing { get; private set; }
    public long AwardedMilliElectronVolts { get; private set; }

    public void Initialize(Vector3 position)
    {
        transform.position = position;
        visual = GetComponent<SymmetryKnotVisual>();
        visual.Initialize(position, captureRadius);
        visual.SetOwner(-1, null, false, 0);
        state = new SymmetryKnotCaptureState(visual.SpawnFadeSeconds, unclaimedSeconds,
            claimedSeconds, captureRampSeconds, tickIntervalSeconds, despawnFadeSeconds);
        sourceToken = "higgs:" + GetInstanceID();
        retiringVisual = false; AwardedMilliElectronVolts = 0;
        if (goalMouths == null) goalMouths = new List<SymmetryKnotGoalMouth>();
        if (goalMouths.Count == 0)
            foreach (var mouth in FindObjectsByType<SymmetryKnotGoalMouth>(FindObjectsSortMode.None))
                if (mouth.gameObject.scene == gameObject.scene) goalMouths.Add(mouth);
    }

    private void Update()
    {
        if (state == null) Initialize(transform.position);
        var scores = MatchScoreService.Instance;
        if (scores == null || !scores.IsScoringOpen) Retire();
        bool running = scores != null && scores.IsScoringOpen && scores.IsChainClockRunning && Time.timeScale > 0;
        TickGameplay(Time.deltaTime, running);
    }

    public void TickGameplay(float dt, bool clockRunning)
    {
        if (state == null) return;
        var scores = MatchScoreService.Instance;
        bool running = clockRunning && scores != null && scores.IsScoringOpen;
        int mask = running && !IsRetiring ? EvaluateTeamMask() : 0;
        bool closingMatch = scores == null || !scores.IsScoringOpen;
        int ticks = state.Tick(dt, mask, running || (IsRetiring && closingMatch && Time.timeScale > 0));
        var mouth = GetGoalMouth(state.OwnerTeam);
        if (ticks > 0 && running && scores.TryAwardToTeam(rewardKey, state.OwnerTeam, null,
            sourceToken, transform.position, out var award, ticks))
        {
            AwardedMilliElectronVolts = EnergyScoreMath.SaturatingAdd(AwardedMilliElectronVolts, award.finalMilliElectronVolts);
            if (mouth != null) mouth.ReceiveEnergy(award);
        }
        IsFlowing = running && state.Phase == SymmetryKnotCaptureState.Stage.Active &&
            !state.Contested && state.OwnerTeam > 0 && state.Hold01 >= 1;
        visual.SetOwner(IsRetiring ? -1 : state.OwnerTeam, mouth != null ? mouth.FlowAnchor : null,
            state.Contested, running && !IsRetiring ? state.Hold01 : 0);
        if (IsRetiring && !retiringVisual)
        {
            retiringVisual = true; visual.BeginDespawn(despawnFadeSeconds);
        }
        if (state.Phase == SymmetryKnotCaptureState.Stage.Finished) Destroy(gameObject);
    }

    public void Retire()
    {
        IsFlowing = false;
        state?.Retire();
    }

    private void OnDisable() => IsFlowing = false;

    private SymmetryKnotGoalMouth GetGoalMouth(int team)
    {
        foreach (var mouth in goalMouths)
            if (mouth != null && mouth.isActiveAndEnabled && mouth.teamID == team) return mouth;
        return null;
    }

    public int EvaluateTeamMask()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, captureRadius, overlap,
            playerLayerMask, QueryTriggerInteraction.Collide);
        if (count == overlap.Length) return 3; // Saturation must never grant uncontested income.
        int mask = 0;
        for (int i = 0; i < count; i++)
        {
            var player = overlap[i] != null ? overlap[i].GetComponentInParent<PlayerControllerScript>() : null;
            if (player == null || player.gameObject.scene != gameObject.scene || !player.isActiveAndEnabled ||
                !player.isActive || player.temporarilyEliminated || player.IsPseudoPlayer) continue;
            // Occupy with the player centre; an extended sword/shield must not claim from outside the ring.
            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0;
            if (offset.sqrMagnitude > captureRadius * captureRadius) continue;
            if (player.teamID == 1) mask |= 1;
            if (player.teamID == 2) mask |= 2;
        }
        return mask;
    }
}
