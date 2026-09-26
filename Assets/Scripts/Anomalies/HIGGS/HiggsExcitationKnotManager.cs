using System;
using Massive.Multiplier;
using Massive.Scoring;
using UnityEngine;

/// <summary>Owns one excitation/Knot pair. Field decoration never schedules gameplay.</summary>
[DefaultExecutionOrder(600), DisallowMultipleComponent, RequireComponent(typeof(AmplifierSpawnRegion))]
public class HiggsExcitationKnotManager : MonoBehaviour
{
    public enum EncounterPhase { WaitingForMatch, Delay, Telegraph, Knot, Tail, Finished }
    [Header("References")]
    [SerializeField] private HiggsFieldGPU higgsField;
    [SerializeField] private SymmetryKnotController knotPrefab;
    [SerializeField] private Transform knotsParent;
    [SerializeField] private AmplifierSpawnRegion spawnRegion;
    [SerializeField] private AmplifierResonanceSpawner amplifierEncounter;
    [Header("One opportunity at a time (scaled match seconds)")]
    [SerializeField, Min(0)] private float initialKnotSpawnDelaySeconds = 8f;
    [SerializeField, Min(.1f)] private float telegraphSeconds = 1.5f;
    [SerializeField, Min(0)] private float respawnCooldownSeconds = 10f;
    [SerializeField] private Vector2 respawnVariation = new Vector2(-2, 2);
    [SerializeField, Min(.05f)] private float placementRetrySeconds = .5f;
    [SerializeField, Min(.1f)] private float excitationTailSeconds = .75f;
    [SerializeField, Min(0)] private float introductionSeparationSeconds = 3f;
    [Header("Reproducible tuning")]
    [SerializeField] private bool fixedRandomSeed;
    [SerializeField] private int randomSeed = 912;
    public EncounterPhase Phase { get; private set; } = EncounterPhase.WaitingForMatch;
    public SymmetryKnotController ActiveKnot { get; private set; }
    public string Status { get; private set; } = "Waiting for match";
    public int OpportunitiesSpawned { get; private set; }
    public int PlacementRetries { get; private set; }
    public float MatchSeconds { get; private set; }
    public float UncontestedSeconds { get; private set; }
    public float ContestedSeconds { get; private set; }
    public int OwnershipChanges { get; private set; }
    public long KnotEnergy { get; private set; }
    public float LastFirstArrivalSeconds { get; private set; } = -1;
    public Vector3 ReservedPosition { get; private set; }

    private System.Random random;
    private MatchScoreService subscribedScores;
    private float timer, phaseAge, lastCoreIntroduction = -999f;
    private bool observedOpen;
    private Func<bool> introductionGate;

    private void OnEnable()
    {
        spawnRegion = spawnRegion != null ? spawnRegion : GetComponent<AmplifierSpawnRegion>();
        random = new System.Random(fixedRandomSeed ? randomSeed : Guid.NewGuid().GetHashCode());
        introductionGate = CanIntroduceCore;
        if (amplifierEncounter != null) amplifierEncounter.CanIntroduceEncounter = introductionGate;
        ResetEncounter();
    }

    private void Update()
    {
        var scores = MatchScoreService.Instance;
        if (subscribedScores != scores)
        {
            if (subscribedScores != null) subscribedScores.ScoresReset -= ResetEncounter;
            subscribedScores = scores;
            if (subscribedScores != null) subscribedScores.ScoresReset += ResetEncounter;
        }
        if (higgsField != null) higgsField.SetGameplayExcitationsPerSecond(0);
        if (scores == null || !scores.IsScoringOpen)
        {
            if (observedOpen) Finish();
            return;
        }
        if (Phase == EncounterPhase.Finished) return;
        if (!observedOpen)
        {
            observedOpen = true; timer = initialKnotSpawnDelaySeconds;
            Phase = EncounterPhase.Delay; Status = "Initial opportunity delay";
        }
        if (!scores.IsChainClockRunning || Time.timeScale <= 0) return;
        float dt = Time.deltaTime;
        MatchSeconds += dt;
        bool introducing = CoreIsIntroducing();
        if (introducing) lastCoreIntroduction = MatchSeconds;

        switch (Phase)
        {
            case EncounterPhase.Delay:
                timer -= dt;
                if (timer <= 0 && !introducing && MatchSeconds - lastCoreIntroduction >= introductionSeparationSeconds)
                    TryReserve();
                break;
            case EncounterPhase.Telegraph:
                phaseAge += dt;
                higgsField.SetReservedExcitationStrength(Mathf.SmoothStep(0, 1, phaseAge / telegraphSeconds));
                if (phaseAge >= telegraphSeconds) ManifestKnot();
                break;
            case EncounterPhase.Knot:
                if (ActiveKnot == null)
                {
                    Phase = EncounterPhase.Tail; phaseAge = 0;
                }
                else
                {
                    // Keep the source fixed and fully visible through the complete Knot outro.
                    higgsField.SetReservedExcitationStrength(1);
                    if (ActiveKnot.IsRetiring) CaptureTelemetry();
                }
                break;
            case EncounterPhase.Tail:
                phaseAge += dt;
                higgsField.SetReservedExcitationStrength(1 - Mathf.SmoothStep(0, 1, phaseAge / excitationTailSeconds));
                if (phaseAge >= excitationTailSeconds)
                {
                    higgsField.ReleaseReservedExcitation();
                    Phase = EncounterPhase.Delay;
                    timer = AmplifierResonanceSpawner.SampleDelay(respawnCooldownSeconds, respawnVariation, random.NextDouble());
                    Status = "Waiting for next excitation";
                }
                break;
        }
    }

    private bool CanIntroduceCore() => Phase != EncounterPhase.Telegraph &&
        !(ActiveKnot != null && ActiveKnot.State != null && ActiveKnot.State.Phase == SymmetryKnotCaptureState.Stage.Forming);

    private bool CoreIsIntroducing()
    {
        if (amplifierEncounter == null || !amplifierEncounter.isActiveAndEnabled) return false;
        return amplifierEncounter.Phase == AmplifierEncounterPhase.FormingPattern ||
            amplifierEncounter.Phase == AmplifierEncounterPhase.FindingPlacement ||
            (amplifierEncounter.ActiveCore != null && amplifierEncounter.ActiveCore.IsSpawning);
    }

    private void TryReserve()
    {
        if (higgsField == null || knotPrefab == null || spawnRegion == null)
        { Status = "Assign field, Knot prefab and spawn region"; timer = placementRetrySeconds; return; }
        var pattern = amplifierEncounter != null && amplifierEncounter.ActivePattern != null
            ? amplifierEncounter.ActivePattern : spawnRegion.previewPattern;
        if (!spawnRegion.TryFindSpawn(random, knotPrefab.CaptureRadius, pattern, out var point, out var reason) ||
            !ClearOfCore(point) || !higgsField.TryReserveExcitation(point, knotPrefab.CaptureRadius))
        {
            PlacementRetries++; timer = placementRetrySeconds;
            Status = string.IsNullOrEmpty(reason) ? "Waiting for clear Core/field space" : reason;
            return;
        }
        ReservedPosition = point; phaseAge = 0; Phase = EncounterPhase.Telegraph;
        Status = "Field excitation — a Knot is forming";
    }

    private bool ClearOfCore(Vector3 point)
    {
        if (amplifierEncounter == null || amplifierEncounter.ActiveCore == null) return true;
        Vector3 delta = point - amplifierEncounter.ActiveCore.transform.position; delta.y = 0;
        float radius = knotPrefab.CaptureRadius + amplifierEncounter.CorePlacementRadius + spawnRegion.clearanceWorld;
        return delta.sqrMagnitude > radius * radius;
    }

    private void ManifestKnot()
    {
        var pattern = amplifierEncounter != null && amplifierEncounter.ActivePattern != null
            ? amplifierEncounter.ActivePattern : spawnRegion.previewPattern;
        // Players may approach a telegraph; only its initial selection excludes them.
        if (!spawnRegion.IsValidSpawnPoint(ReservedPosition, knotPrefab.CaptureRadius, pattern, out var reason, false) ||
            !ClearOfCore(ReservedPosition))
        {
            PlacementRetries++; Phase = EncounterPhase.Tail; phaseAge = 0;
            Status = "Placement changed during warning; retiring without a Knot"; return;
        }
        ActiveKnot = Instantiate(knotPrefab, ReservedPosition, Quaternion.identity, knotsParent != null ? knotsParent : transform);
        ActiveKnot.Initialize(ReservedPosition);
        recordedKnot = false; OpportunitiesSpawned++;
        Phase = EncounterPhase.Knot; Status = "Occupy uncontested to earn team energy";
    }

    private bool recordedKnot;
    private void CaptureTelemetry()
    {
        if (recordedKnot || ActiveKnot == null || ActiveKnot.State == null) return;
        recordedKnot = true;
        UncontestedSeconds += ActiveKnot.State.UncontestedSeconds;
        ContestedSeconds += ActiveKnot.State.ContestedSeconds;
        OwnershipChanges += ActiveKnot.State.OwnershipChanges;
        LastFirstArrivalSeconds = ActiveKnot.State.FirstArrivalSeconds;
        KnotEnergy = EnergyScoreMath.SaturatingAdd(KnotEnergy, ActiveKnot.AwardedMilliElectronVolts);
    }

    private void Finish()
    {
        CaptureTelemetry();
        if (ActiveKnot != null) Destroy(ActiveKnot.gameObject);
        ActiveKnot = null;
        if (higgsField != null) higgsField.ReleaseReservedExcitation();
        Phase = EncounterPhase.Finished; Status = "Match ended";
    }

    private void ResetEncounter()
    {
        Finish(); observedOpen = false; MatchSeconds = 0;
        OpportunitiesSpawned = PlacementRetries = OwnershipChanges = 0;
        UncontestedSeconds = ContestedSeconds = 0; KnotEnergy = 0; LastFirstArrivalSeconds = -1;
        lastCoreIntroduction = -999;
        Phase = EncounterPhase.WaitingForMatch; Status = "Waiting for match scoring";
    }

    private void OnDisable()
    {
        if (subscribedScores != null) subscribedScores.ScoresReset -= ResetEncounter;
        subscribedScores = null;
        if (amplifierEncounter != null && amplifierEncounter.CanIntroduceEncounter == introductionGate)
            amplifierEncounter.CanIntroduceEncounter = null;
        Finish();
        // Keep decorative random excitations off: disabling gameplay must not leave false objectives.
        if (higgsField != null) higgsField.SetGameplayExcitationsPerSecond(0);
    }
}
