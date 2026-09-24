using System;
using System.Collections.Generic;
using Massive.Resonance;
using Massive.Scoring;
using UnityEngine;

namespace Massive.Multiplier
{
    public enum AmplifierEncounterPhase { Stopped, WaitingForMatch, Delay, FormingPattern, FindingPlacement, Active, Dissolving, Finished, WaitingForAmplifierCapacity }

    [Serializable]
    public sealed class ResonanceSpawnEntry
    {
        public string label;
        [Tooltip("A configured prefab, not a live scene instance. Reorder the list to choose the progression.")]
        public ResonancePatternController patternPrefab;
    }

    /// <summary>Owns one Core/pattern pair. Capture/scoring remain owned by the existing Core and goal.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(AmplifierSpawnRegion))]
    public sealed partial class AmplifierResonanceSpawner : MonoBehaviour
    {
        [Header("Pair configuration")]
        public AmplifierCoreGameplay corePrefab;
        public AmplifierSpawnRegion spawnRegion;
        public Transform patternAnchor;
        public List<ResonanceSpawnEntry> patternOrder = new List<ResonanceSpawnEntry>();
        [Min(0)] public int startingPattern;
        public bool loopPatternOrder = true;

        [Header("Timing (scaled gameplay seconds)")]
        public bool startAutomatically = true;
        public bool waitForScoring = true;
        [Tooltip("Optional explicit score service. Otherwise uses the existing scene score service.")]
        public MatchScoreService scoreService;
        [Min(0f)] public float initialSpawnDelay = 1f;
        [Min(0f)] public float respawnDelay = 8f;
        [Tooltip("Uniform additive variation: (-2,2) with delay 8 produces waits between 6 and 10 seconds. Final delay is never negative.")]
        public Vector2 respawnDelayVariation = new Vector2(-2f, 2f);
        [Min(.05f)] public float placementRetryDelay = .5f;
        [Tooltip("Time available after the Core has fully appeared. At expiry the Core and pattern dissolve without scoring. Zero disables the time-out.")]
        [InspectorName("Sequence Time-out (seconds)"), Min(0f)] public float maximumActiveSeconds = 30f;
        [Tooltip("During the final seconds the Core's surface becomes increasingly unstable. Zero disables this warning; it never changes physical motion.")]
        [Min(0f)] public float timeoutWarningSeconds = 5f;

        [Header("Placement")]
        [Tooltip("Lower bound for clearance radius. The prefab's actual collider footprint can only increase it.")]
        [Min(.01f)] public float minimumCoreRadius = .6f;
        public bool fixedRandomSeed;
        public int randomSeed = 345;

        [Header("Existing scene demonstrations — suspended only while this system owns the cycle")]
        public ResonanceRenderComparison comparison;
        [Tooltip("Only these explicitly assigned scene objects are suppressed. Never include HUD Core copies or the goals.")]
        public GameObject[] standaloneExamples = Array.Empty<GameObject>();

        public AmplifierEncounterPhase Phase { get; private set; } = AmplifierEncounterPhase.Stopped;
        public AmplifierCoreGameplay ActiveCore { get; private set; }
        public ResonancePatternController ActivePattern { get; private set; }
        public ResonanceManifestation ActiveManifestation { get; private set; }
        public string Status { get; private set; } = "Not running";
        public int CurrentPatternIndex { get; private set; } = -1;
        public int PairsSpawned { get; private set; }
        public int CapturesObserved { get; private set; }
        public float SecondsRemaining => Mathf.Max(0f, timer);
        public float CorePlacementRadius => Mathf.Max(minimumCoreRadius, EstimateCoreRadius(corePrefab));
        public float LastRespawnDelay { get; private set; }
        public float ActiveSecondsRemaining => Effective_maximumActiveSeconds > 0f ? Mathf.Max(0f, Effective_maximumActiveSeconds - activeAge) : float.PositiveInfinity;
        public float TimeoutWarning01 => coreVisual != null ? coreVisual.TimeoutWarning01 : 0f;

        private System.Random random;
        private GameObject ownedRoot;
        private float timer, activeAge;
        private int nextIndex;
        private bool running, cycleRequested, observedOpenScoring, examplesSuppressed, comparisonWasEnabled;
        private MatchScoreService subscribedScores;
        private AmplifierCoreVisual coreVisual;
        private readonly List<GameObject> suspended = new List<GameObject>();
        private readonly List<bool> suspendedStates = new List<bool>();

        private void OnEnable()
        {
            if (Application.isPlaying && startAutomatically) StartCycle();
        }
        private void OnDisable()
        {
            running = cycleRequested = false;
            if (subscribedScores != null) subscribedScores.ScoresReset -= OnScoresReset;
            subscribedScores = null;
            ClearPair();
            RestoreExamples();
            Phase = AmplifierEncounterPhase.Stopped;
        }
        private void Update()
        {
            if (!Application.isPlaying) return;
            RefreshScoreSubscription();
            if (!running) return;
            bool open = !waitForScoring || (scoreService != null ? scoreService : MatchScoreService.Instance)?.IsScoringOpen == true;
            if (!open)
            {
                if (observedOpenScoring) Finish("Match scoring closed; cycle stopped");
                else { Phase = AmplifierEncounterPhase.WaitingForMatch; Status = "Waiting for match scoring to open"; }
                return;
            }
            if (!observedOpenScoring)
            {
                observedOpenScoring = true;
                SetDelay(Effective_initialSpawnDelay, "Initial spawn delay");
            }
            if (WaitForAmplifierCapacity()) return;
            TickCycle(Time.deltaTime);
        }

        private MatchScoreService Scores => scoreService != null ? scoreService : MatchScoreService.Instance;
        private void RefreshScoreSubscription()
        {
            var scores = Scores;
            if (scores == subscribedScores) return;
            if (subscribedScores != null) subscribedScores.ScoresReset -= OnScoresReset;
            subscribedScores = scores;
            if (subscribedScores != null) subscribedScores.ScoresReset += OnScoresReset;
        }
        private void OnScoresReset()
        {
            // A match reset starts a fresh ordered cycle, but never overrides an explicit Stop Cycle.
            if (Application.isPlaying && isActiveAndEnabled && cycleRequested) StartCycle();
        }
        private bool WaitForAmplifierCapacity()
        {
            bool capped = Scores != null && Scores.AreAllTeamsAmplifierMaxed;
            if (!capped)
            {
                if (Phase == AmplifierEncounterPhase.WaitingForAmplifierCapacity)
                    SetDelay(timer, "Amplification available; resuming spawn wait");
                return false;
            }
            if (Phase == AmplifierEncounterPhase.Dissolving) return false;
            if (ActivePattern != null)
            {
                RetireCurrentPair();
                Status = "All teams at maximum amplification; dissolving the unused pair";
                return false;
            }
            Phase = AmplifierEncounterPhase.WaitingForAmplifierCapacity;
            Status = "All teams at maximum amplification; no Resonance sequence will spawn";
            return true;
        }

        public bool StartCycle()
        {
            if (!Application.isPlaying) { Status = "Enter Play Mode to run the paired cycle"; return false; }
            StopCycle();
            if (spawnRegion == null) spawnRegion = GetComponent<AmplifierSpawnRegion>();
            if (corePrefab == null || corePrefab.gameObject.scene.IsValid() || corePrefab.IsPresentationOnly || spawnRegion == null || !HasPattern())
            { Status = "Assign a gameplay Core prefab, spawn region, and at least one pattern prefab"; return false; }
            random = new System.Random(fixedRandomSeed ? randomSeed : Guid.NewGuid().GetHashCode());
            nextIndex = Mathf.Clamp(startingPattern, 0, patternOrder.Count - 1);
            CurrentPatternIndex = -1; PairsSpawned = CapturesObserved = 0; observedOpenScoring = false;
            running = cycleRequested = true; RefreshScoreSubscription(); SuppressExamples();
            Phase = AmplifierEncounterPhase.WaitingForMatch;
            Status = "Waiting for match";
            return true;
        }
        public void StopCycle()
        {
            running = cycleRequested = false; ClearPair(); RestoreExamples();
            Phase = AmplifierEncounterPhase.Stopped; Status = "Stopped; standalone examples restored";
        }
        public void SpawnNow()
        {
            if (!running && !StartCycle()) return;
            if (Phase == AmplifierEncounterPhase.Delay) timer = 0f;
        }
        public void RetireCurrentPair()
        {
            if (!running || ActivePattern == null || Phase == AmplifierEncounterPhase.Dissolving) return;
            // This is a preview/timeout retirement, never a synthetic score or goal capture.
            if (coreVisual != null) coreVisual.SetTimeoutWarning(0f);
            if (ActiveCore != null && !ActiveCore.HasBeenCaptured) ActiveCore.BeginTimeoutDespawn();
            BeginRetirement();
            Status = "Unclaimed pair dissolving; no score awarded";
        }
        private void TickCycle(float dt)
        {
            switch (Phase)
            {
                case AmplifierEncounterPhase.Delay:
                    timer -= dt;
                    if (timer <= 0f) SpawnPattern();
                    break;
                case AmplifierEncounterPhase.FormingPattern:
                    if (ActivePattern == null || ActiveManifestation == null) { RetryLostPair(); break; }
                    if (ActiveManifestation.IsIdle) { Phase = AmplifierEncounterPhase.FindingPlacement; timer = 0f; }
                    break;
                case AmplifierEncounterPhase.FindingPlacement:
                    timer -= dt;
                    if (timer <= 0f) TrySpawnCore();
                    break;
                case AmplifierEncounterPhase.Active:
                    if (ActivePattern == null || !ActivePattern.isActiveAndEnabled) { RetryLostPair(); break; }
                    if (ActiveCore == null || !ActiveCore.gameObject.activeInHierarchy) BeginRetirement();
                    else if (!ActiveCore.IsSpawning && !ActiveCore.HasBeenCaptured)
                    {
                        activeAge += Mathf.Max(0f, dt);
                        float warning = EvaluateTimeoutWarning(activeAge, Effective_maximumActiveSeconds, Effective_timeoutWarningSeconds);
                        if (coreVisual != null) coreVisual.SetTimeoutWarning(warning);
                        if (Effective_maximumActiveSeconds > 0f && activeAge >= Effective_maximumActiveSeconds) RetireCurrentPair();
                        else Status = warning > 0f ? "Core unstable — sequence nearing time-out" : "Pair active — capture the Core to advance";
                    }
                    break;
                case AmplifierEncounterPhase.Dissolving:
                    bool coreFinished = ActiveCore == null || !ActiveCore.gameObject.activeInHierarchy;
                    if ((ActiveManifestation == null || ActiveManifestation.IsHidden) && coreFinished)
                    {
                        ClearPair();
                        LastRespawnDelay = SampleDelay(Effective_respawnDelay, Effective_respawnDelayVariation, random.NextDouble());
                        SetDelay(LastRespawnDelay, "Waiting for the next pair");
                    }
                    break;
            }
        }
        private bool HasPattern()
        {
            if (patternOrder == null) return false;
            foreach (var entry in patternOrder) if (entry != null && entry.patternPrefab != null && entry.patternPrefab.definition != null) return true;
            return false;
        }
        private bool SelectNextPattern(out ResonancePatternController prefab)
        {
            prefab = null;
            if (patternOrder == null || patternOrder.Count == 0) return false;
            for (int attempt = 0; attempt < patternOrder.Count; attempt++)
            {
                if (nextIndex >= patternOrder.Count)
                {
                    if (!loopPatternOrder) return false;
                    nextIndex = 0;
                }
                int selected = nextIndex++;
                var entry = patternOrder[selected];
                if (entry == null || entry.patternPrefab == null || entry.patternPrefab.definition == null) continue;
                prefab = entry.patternPrefab; CurrentPatternIndex = selected; return true;
            }
            return false;
        }
        private void SpawnPattern()
        {
            if (WaitForAmplifierCapacity()) return;
            if (!SelectNextPattern(out var prefab)) { Finish("Pattern order complete (or no valid entries)"); return; }
            if (prefab.gameObject.scene.IsValid()) { Finish("Pattern Order requires prefab assets, not live scene instances"); return; }
            ClearPair();
            ownedRoot = new GameObject("Amplifier + Resonance — active pair") { hideFlags = HideFlags.DontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ownedRoot, gameObject.scene);
            ownedRoot.SetActive(false);
            Transform anchor = patternAnchor != null ? patternAnchor : transform;
            ActivePattern = Instantiate(prefab, anchor.position, anchor.rotation, ownedRoot.transform);
            ActivePattern.name = prefab.name + " (active pattern)";
            ActivePattern.gameObject.SetActive(true); ActivePattern.enabled = true;
            ActivePattern.arenaBounds = spawnRegion.arenaBounds;
            if (spawnRegion.arenaBounds != null) ActivePattern.grid = spawnRegion.arenaBounds.Grid;
            ActiveManifestation = ActivePattern.GetComponent<ResonanceManifestation>();
            if (ActiveManifestation == null) ActiveManifestation = ActivePattern.gameObject.AddComponent<ResonanceManifestation>();
            ActiveManifestation.enabled = true;
            ActiveManifestation.SetImmediate(false);
            ownedRoot.SetActive(true);
            ActiveManifestation.BeginSpawn();
            Phase = AmplifierEncounterPhase.FormingPattern; Status = "Condensing " + prefab.name;
        }
        private void TrySpawnCore()
        {
            if (WaitForAmplifierCapacity() || Phase == AmplifierEncounterPhase.Dissolving) return;
            if (ActivePattern == null || ownedRoot == null) { RetryLostPair(); return; }
            if (!spawnRegion.TryFindSpawn(random, CorePlacementRadius, ActivePattern, out var point, out var reason))
            {
                timer = Mathf.Max(.05f, Effective_placementRetryDelay);
                Status = "No safe Core position; retrying. " + reason;
                return;
            }
            ActiveCore = Instantiate(corePrefab, point, corePrefab.transform.rotation, ownedRoot.transform);
            ActiveCore.name = "Amplifier Core (managed neutral spawn)";
            ActiveCore.enabled = true;
            ActiveCore.SetExternalRespawnManaged(true);
            ActiveCore.Captured += OnCoreCaptured;
            coreVisual = ActiveCore.GetComponentInChildren<AmplifierCoreVisual>(true);
            if (coreVisual != null) coreVisual.SetTimeoutWarning(0f);
            if (!ActiveCore.gameObject.activeSelf) ActiveCore.gameObject.SetActive(true);
            activeAge = 0f; PairsSpawned++;
            Phase = AmplifierEncounterPhase.Active; Status = "Pair active — capture the Core to advance";
        }
        private void OnCoreCaptured(AmplifierCoreGameplay core)
        {
            if (!running || core != ActiveCore || Phase != AmplifierEncounterPhase.Active) return;
            CapturesObserved++;
            BeginRetirement();
        }
        private void BeginRetirement()
        {
            if (Phase == AmplifierEncounterPhase.Dissolving) return;
            if (coreVisual != null) coreVisual.SetTimeoutWarning(0f);
            ActiveManifestation?.BeginDespawn();
            Phase = AmplifierEncounterPhase.Dissolving;
            Status = "Dissolving pattern; allowing Core absorption to finish";
        }
        private void RetryLostPair()
        {
            ClearPair(); SetDelay(Mathf.Max(.05f, Effective_placementRetryDelay), "Pair interrupted; retrying next pattern");
        }
        private void SetDelay(float seconds, string message)
        { timer = Mathf.Max(0f, seconds); Phase = AmplifierEncounterPhase.Delay; Status = message; }
        private void Finish(string message)
        {
            running = false; ClearPair(); Phase = AmplifierEncounterPhase.Finished; Status = message;
            // Keep the standalone demo suspended until StopCycle/disable; do not spawn it at match end.
        }
        private void ClearPair()
        {
            if (ActiveCore != null) ActiveCore.Captured -= OnCoreCaptured;
            if (coreVisual != null) coreVisual.SetTimeoutWarning(0f);
            coreVisual = null; activeAge = 0f;
            ActiveCore = null; ActiveManifestation = null; ActivePattern = null;
            if (ownedRoot != null)
            {
                ownedRoot.SetActive(false);
                if (Application.isPlaying) Destroy(ownedRoot); else DestroyImmediate(ownedRoot);
                ownedRoot = null;
            }
        }
        private void SuppressExamples()
        {
            if (examplesSuppressed) return;
            examplesSuppressed = true;
            if (comparison != null)
            {
                comparisonWasEnabled = comparison.enabled; comparison.enabled = false;
                Suspend(comparison.optionA != null ? comparison.optionA.gameObject : null);
                Suspend(comparison.optionB != null ? comparison.optionB.gameObject : null);
            }
            if (standaloneExamples != null) foreach (var example in standaloneExamples) Suspend(example);
        }
        private void Suspend(GameObject value)
        {
            if (value == null || value == gameObject || transform.IsChildOf(value.transform) || suspended.Contains(value)) return;
            suspended.Add(value); suspendedStates.Add(value.activeSelf); value.SetActive(false);
        }
        private void RestoreExamples()
        {
            if (!examplesSuppressed) return;
            for (int i = 0; i < suspended.Count; i++) if (suspended[i] != null) suspended[i].SetActive(suspendedStates[i]);
            suspended.Clear(); suspendedStates.Clear(); examplesSuppressed = false;
            if (comparison != null) comparison.enabled = comparisonWasEnabled;
        }
        public static float SampleDelay(float baseline, Vector2 variation, double unitRandom)
        {
            if (float.IsNaN(baseline) || float.IsInfinity(baseline)) baseline = 0f;
            if (float.IsNaN(variation.x) || float.IsInfinity(variation.x)) variation.x = 0f;
            if (float.IsNaN(variation.y) || float.IsInfinity(variation.y)) variation.y = 0f;
            if (double.IsNaN(unitRandom) || double.IsInfinity(unitRandom)) unitRandom = 0;
            float low = Mathf.Min(variation.x, variation.y), high = Mathf.Max(variation.x, variation.y);
            return Mathf.Max(0f, baseline + Mathf.Lerp(low, high, Mathf.Clamp01((float)unitRandom)));
        }
        public static float EvaluateTimeoutWarning(float activeSeconds, float timeoutSeconds, float warningSeconds)
        {
            if (timeoutSeconds <= 0f || warningSeconds <= 0f) return 0f;
            float window = Mathf.Min(timeoutSeconds, warningSeconds);
            return Mathf.Clamp01((activeSeconds - (timeoutSeconds - window)) / window);
        }
        public static float EstimateCoreRadius(AmplifierCoreGameplay prefab)
        {
            if (prefab == null) return 0f;
            float result = 0f;
            foreach (var collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (collider.isTrigger || !collider.enabled) continue;
                Bounds local;
                if (collider is SphereCollider sphere)
                {
                    Vector3 scale = sphere.transform.lossyScale;
                    Vector3 center = sphere.transform.TransformPoint(sphere.center) - prefab.transform.position;
                    float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                    result = Mathf.Max(result, new Vector2(center.x, center.z).magnitude + radius);
                    continue;
                }
                else if (collider is BoxCollider box) local = new Bounds(box.center, box.size);
                else if (collider is CapsuleCollider capsule)
                {
                    Vector3 scale = capsule.transform.lossyScale;
                    int axis = capsule.direction;
                    float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale[(axis + 1) % 3]), Mathf.Abs(scale[(axis + 2) % 3]));
                    float halfSegment = Mathf.Max(0f, capsule.height * Mathf.Abs(scale[axis]) * .5f - radius);
                    Vector3 direction = Vector3.zero; direction[axis] = 1f;
                    Vector3 offset = capsule.transform.TransformDirection(direction).normalized * halfSegment;
                    Vector3 center = capsule.transform.TransformPoint(capsule.center) - prefab.transform.position;
                    result = Mathf.Max(result, new Vector2(center.x + offset.x, center.z + offset.z).magnitude + radius,
                        new Vector2(center.x - offset.x, center.z - offset.z).magnitude + radius);
                    continue;
                }
                else if (collider is MeshCollider mesh && mesh.sharedMesh != null) local = mesh.sharedMesh.bounds;
                else continue;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    Vector3 offset = collider.transform.TransformPoint(point) - prefab.transform.position;
                    result = Mathf.Max(result, new Vector2(offset.x, offset.z).magnitude);
                }
            }
            return result * prefab.PendingScaleRatio;
        }
    }
}
