using Massive.Player;
using Massive.Scoring;
using UnityEngine;

namespace Massive.Emergence
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed partial class EmergenceField : MonoBehaviour
    {
        public EmergenceProfile profile;
        public Material particleMaterial;
        public ArenaBoundsFromVectorGrid arena;
        public MatchScoreService scores;
        [Tooltip("Scene match owner. Ecology starts during preparation/countdown; rewards and player interactions wait for the active match clock.")]
        public GameManagerScript match;
        public AnomalyManager anomalies;
        public PlayerControllerScript[] players;
        public EmergenceMassNugget nuggetPrefab;
        public EmergenceMassNugget matterNuggletPrefab;
        public EmergenceMassNugget amplifierNuggletPrefab;
        public EmergenceMassNugget amplifierNuggetPrefab;
        [Tooltip("Menus/instructions only: no players, pickups, score or match authority.")]
        public bool presentationOnly;
        public Vector2 presentationHalfSize = new Vector2(6, 4);
        [Min(0)] public float presentationWarmup = 5;

        public IEmergenceSimulation Simulation { get; private set; }
        public bool UsesGpu => Simulation is EmergenceGpuSimulation;
        bool IsPreparing => match && !match.IsStartupBlocked &&
            (match.Phase == MatchRuntimePhase.Preparing || match.Phase == MatchRuntimePhase.Countdown);
        bool GameplayClockRunning => !presentationOnly && !IsPreparing && scores && scores.IsScoringOpen && scores.IsChainClockRunning;
        public bool IsRunning => Application.isPlaying && isActiveAndEnabled &&
            (presentationOnly || (Time.timeScale > 0 && scores && (IsPreparing || GameplayClockRunning) && (!anomalies || !anomalies.IsAnomalyRunning)));
        public int NuggetsReleased { get; private set; }
        public int WakeContacts { get; private set; }
        public int AttackContacts { get; private set; }
        public int RepulsorContacts { get; private set; }
        public float LastStepMilliseconds { get; private set; }
        public int ActiveNuggets { get { int n = 0; if (nuggets != null) foreach (var p in nuggets) if (p && p.gameObject.activeSelf) n++; return n; } }
        public EmergenceMassNugget[] Nuggets => nuggets;
        float StepSeconds => 1f / Mathf.Clamp(profile.simulationHz, 30, 120);
        float accumulator;
        EmergenceMassNugget[] nuggets;
        EmergenceMassNugget[,] pickupPool;
        System.Random pickupRandom;
        PlayerBinding[] bindings;
        Vector3 center, axisX = Vector3.right, axisY = Vector3.forward;
        bool reportedError;
        bool initializedInPlayMode;
        readonly System.Diagnostics.Stopwatch stepWatch = new System.Diagnostics.Stopwatch();

        bool Initialize()
        {
            // ExecuteAlways instances can survive entering play when domain/scene reloads are disabled.
            if (Simulation != null && (initializedInPlayMode != Application.isPlaying || Simulation is EmergenceGpuSimulation { IsDisposed: true })) ResetSimulationState();
            if (Simulation != null) return true;
            if (reportedError) return false;
            if (!profile || !particleMaterial || (!presentationOnly && (!arena || !scores || !nuggetPrefab ||
                !matterNuggletPrefab || !amplifierNuggletPrefab || !amplifierNuggetPrefab)))
            {
                if (Application.isPlaying && !reportedError) { Debug.LogError("[EMERGENCE] Assign ecology, material, bounds, scoring and nugget references.", this); reportedError = true; }
                return false;
            }
            string error = profile.ConfigurationError();
            if (error != null) { if (!reportedError) Debug.LogError("[EMERGENCE] " + error, this); reportedError = true; return false; }
            Vector2 half = presentationHalfSize;
            if (!presentationOnly)
            {
                arena.RefreshNow(); var b = arena.Current;
                center = b.centerWS; center.y = .06f;
                axisX = b.axisX_WS.normalized; axisY = b.axisY_WS.normalized;
                // Use the playable arena edge; the solvers already keep each particle's radius inside it.
                half = Vector2.Scale(b.halfSizeLocalInset, new Vector2(arena.Grid.transform.TransformVector(Vector3.right).magnitude,
                    arena.Grid.transform.TransformVector(Vector3.up).magnitude));
            }
            try
            {
                Simulation = profile.backend == EmergenceBackend.GPU ? (IEmergenceSimulation)new EmergenceGpuSimulation(profile, half) : new EmergenceSimulation(profile, half, profile.seed);
                if (Simulation is EmergenceGpuSimulation gpu) gpu.SetPlane(presentationOnly ? transform.position : center,
                    presentationOnly ? transform.TransformVector(Vector3.right) : axisX, presentationOnly ? transform.TransformVector(Vector3.forward) : axisY);
            }
            catch (System.Exception exception) { reportedError = true; Debug.LogError("[EMERGENCE] " + exception.Message, this); return false; }
            initializedInPlayMode = Application.isPlaying;
            if (presentationOnly) for (int i = 0; i < presentationWarmup / StepSeconds; i++) Simulation.Step(StepSeconds, false);
            if (!presentationOnly)
            {
                nuggets = new EmergenceMassNugget[Mathf.Clamp(profile.nuggetCapacity, 4, 64)];
                pickupPool = new EmergenceMassNugget[4, nuggets.Length];
                pickupRandom = new System.Random(unchecked(profile.seed ^ 0x51ECA7));
                bindings = new PlayerBinding[!Application.isPlaying || players == null ? 0 : players.Length];
                for (int i = 0; i < bindings.Length; i++) if (players[i]) bindings[i] = new PlayerBinding(players[i], UsesGpu ? 0 : Simulation.Count);
            }
            CreateMesh(); return true;
        }
        void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && (!profile || profile.ConfigurationError() != null)) return;
#endif
            if (!Initialize()) return;
            if (IsRunning)
            {
                bool gameplay = GameplayClockRunning;
                accumulator = Mathf.Min(accumulator + (presentationOnly ? Time.unscaledDeltaTime : Time.deltaTime), StepSeconds * Mathf.Clamp(profile.maximumStepsPerFrame, 1, 6));
                while (accumulator >= StepSeconds)
                {
                    stepWatch.Restart();
                    if (gameplay) ApplyPlayerInteractions(StepSeconds);
                    else if (Simulation is EmergenceGpuSimulation warmup) warmup.SetInfluences(null, 0);
                    bool canEmit = gameplay && profile.pickupChances != null && profile.pickupChances.IsValid;
                    Simulation.EmissionSlots = gameplay ? nuggets.Length - ReleasedPickupCount : 0;
                    Simulation.Step(StepSeconds, canEmit && ActiveNuggets < nuggets.Length);
                    if (gameplay) { UpdateRewardBuds(); ReleaseNuggets(); }
                    accumulator -= StepSeconds; stepWatch.Stop(); LastStepMilliseconds = (float)stepWatch.Elapsed.TotalMilliseconds;
                }
            }
            if (Simulation is EmergenceGpuSimulation status)
            {
                status.RequestStatus(); WakeContacts = status.WakeContacts; AttackContacts = status.AttackContacts; RepulsorContacts = status.RepulsorContacts;
                if ((!Application.isPlaying || (IsRunning && GameplayClockRunning)) && !presentationOnly) { UpdateRewardBuds(); ReleaseNuggets(); }
            }
            RenderParticles();
        }
        public Vector3 WorldPosition(Vector2 p) => presentationOnly ? transform.TransformPoint(new Vector3(p.x, 0, p.y)) : center + axisX * p.x + axisY * p.y;
        public Vector2 PlanePosition(Vector3 p) { Vector3 d = p - center; return new Vector2(Vector3.Dot(d, axisX), Vector3.Dot(d, axisY)); }
        Vector2 PlaneVector(Vector3 v) => new Vector2(Vector3.Dot(v, axisX), Vector3.Dot(v, axisY));
        void ReleaseNuggets()
        {
            if (profile.pickupChances == null || !profile.pickupChances.IsValid) return;
            while (Simulation.TryDequeue(out var e))
            {
                if (e.budSerial != 0)
                {
                    var bud = Simulation.RewardBuds[e.budSlot];
                    var pickup = GetBudPickup(e.budSlot, bud);
                    Vector3 tip = BudWorldPosition(bud, pickup);
                    if (Application.isPlaying)
                    {
                        Vector2 velocity = e.velocity + bud.direction * profile.nuggetEjectionSpeed;
                        pickup.ReleaseAttachedPresentation(tip, axisX * velocity.x + axisY * velocity.y, profile.nuggetLifetime);
                        NuggetsReleased++;
                    }
                    else { pickup.UpdateAttachedPresentation(tip, 0); pickup.gameObject.SetActive(false); }
                    budPickups[e.budSlot] = null;
                    continue;
                }
                if (!Application.isPlaying) continue;
                int slot = -1;
                for (int i = 0; i < nuggets.Length; i++) if (!nuggets[i] || !nuggets[i].gameObject.activeSelf) { slot = i; break; }
                if (slot < 0) throw new System.InvalidOperationException("EMERGENCE emission exceeded its reserved pickup capacity.");
                var kind = profile.pickupChances.Select(pickupRandom.NextDouble());
                nuggets[slot] = RentPickup(kind);
                Vector2 direction = e.velocity.sqrMagnitude > .001f ? -e.velocity.normalized : new Vector2(Mathf.Cos(Simulation.Age * 2.4f), Mathf.Sin(Simulation.Age * 2.4f));
                Vector3 point = arena.ClampWorldPointInside(WorldPosition(e.position + direction * .45f), .35f); point.y = 0;
                Vector2 v = e.velocity + direction * profile.nuggetEjectionSpeed;
                nuggets[slot].Eject(point, axisX * v.x + axisY * v.y, profile.nuggetLifetime); NuggetsReleased++;
            }
        }
        EmergenceMassNugget RentPickup(EmergenceDropType kind)
        {
            int row = (int)kind;
            for (int i = 0; i < nuggets.Length; i++)
            {
                var pickup = pickupPool[row, i];
                if (pickup && pickup.gameObject.activeSelf) continue;
                if (!pickup)
                {
                    var prefab = kind == EmergenceDropType.MatterNugglet ? matterNuggletPrefab :
                        kind == EmergenceDropType.AmplifierNugglet ? amplifierNuggletPrefab :
                        kind == EmergenceDropType.AmplifierNugget ? amplifierNuggetPrefab : nuggetPrefab;
                    pickup = Instantiate(prefab, transform);
                    pickup.name = prefab.name;
                    pickupPool[row, i] = pickup;
                }
                // The same cached object may have occupied a different live slot last time.
                for (int previous = 0; previous < nuggets.Length; previous++)
                    if (nuggets[previous] == pickup) nuggets[previous] = null;
                return pickup;
            }
            throw new System.InvalidOperationException("The active pickup cap must be checked before renting a pickup.");
        }
        void OnDisable()
        {
            Camera.onPreCull -= RenderGpu;
            // Preserve the ecosystem on temporary disable; no fresh food or waste on re-enable.
            accumulator = 0;
            Simulation?.CancelRewardBuds();
            if (nuggets != null) foreach (var n in nuggets) if (n) n.gameObject.SetActive(false);
            ReleaseMesh();
        }
        void OnEnable() { Camera.onPreCull -= RenderGpu; Camera.onPreCull += RenderGpu; if (Simulation != null) CreateMesh(); }
        void OnDestroy() { Camera.onPreCull -= RenderGpu; ReleaseMesh(); Simulation?.Dispose(); Simulation = null; }
        void RenderGpu(Camera camera)
        {
            if (!isActiveAndEnabled || !(Simulation is EmergenceGpuSimulation gpu)) return;
            if (presentationOnly && !gpu.IsDisposed) gpu.SetPlane(transform.position, transform.TransformVector(Vector3.right), transform.TransformVector(Vector3.forward));
            gpu.Draw(gameObject.layer, camera);
        }
        void ResetSimulationState()
        {
            ReleaseMesh(); Simulation?.Dispose(); Simulation = null; reportedError = false; accumulator = 0;
            if (pickupPool != null) foreach (var n in pickupPool) if (n)
            {
                if (Application.isPlaying) Destroy(n.gameObject); else DestroyImmediate(n.gameObject);
            }
            nuggets = null; pickupPool = null; pickupRandom = null; bindings = null;
            System.Array.Clear(budPickups, 0, budPickups.Length); System.Array.Clear(budSerials, 0, budSerials.Length);
            NuggetsReleased = WakeContacts = AttackContacts = RepulsorContacts = 0; LastStepMilliseconds = 0;
        }
        public void RebuildPreview()
        {
            if (Application.isPlaying) return;
            ResetSimulationState(); Initialize();
            if (Simulation != null) RenderParticles();
        }
#if UNITY_EDITOR
        // The editor driver owns scheduling. Preview uses the same ecology, but never invokes gameplay.
        public void AdvanceEditorPreview(float dt, int steps)
        {
            if (Application.isPlaying || !isActiveAndEnabled || !Initialize()) return;
            if (Simulation is EmergenceGpuSimulation preview) preview.SetInfluences(null, 0);
            Simulation.EmissionSlots = presentationOnly ? 0 : nuggets.Length;
            stepWatch.Restart();
            for (int i = 0; i < steps; i++)
            {
                Simulation.Step(dt, !presentationOnly);
                if (!presentationOnly)
                {
                    UpdateRewardBuds();
                    foreach (var pickup in budPickups) if (pickup) pickup.AdvanceAttachedPreview(dt);
                    ReleaseNuggets(); // Attached preview objects never become collectible pickups.
                }
            }
            if (Simulation is EmergenceGpuSimulation gpu) gpu.RequestStatus();
            stepWatch.Stop(); LastStepMilliseconds = (float)stepWatch.Elapsed.TotalMilliseconds / Mathf.Max(1, steps);
            RenderParticles();
        }
        public void RenderEditorPreview()
        { if (!Application.isPlaying && Simulation != null && profile && profile.ConfigurationError() == null) RenderParticles(); }
        public void ClearEditorPreview()
        {
            if (Application.isPlaying) return;
            ResetSimulationState();
        }
#endif
    }
}
