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
        public AnomalyManager anomalies;
        public PlayerControllerScript[] players;
        public EmergenceMassNugget nuggetPrefab;
        [Tooltip("Menus/instructions only: no players, pickups, score or match authority.")]
        public bool presentationOnly;
        public Vector2 presentationHalfSize = new Vector2(6, 4);
        [Min(0)] public float presentationWarmup = 5;

        public IEmergenceSimulation Simulation { get; private set; }
        public bool UsesGpu => Simulation is EmergenceGpuSimulation;
        public bool IsRunning => Application.isPlaying && isActiveAndEnabled &&
            (presentationOnly || (Time.timeScale > 0 && scores && scores.IsScoringOpen && scores.IsChainClockRunning && (!anomalies || !anomalies.IsAnomalyRunning)));
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
            if (!profile || !particleMaterial || (!presentationOnly && (!arena || !scores || !nuggetPrefab)))
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
                half = Vector2.Scale(b.halfSizeLocalInset, new Vector2(arena.Grid.transform.TransformVector(Vector3.right).magnitude,
                    arena.Grid.transform.TransformVector(Vector3.up).magnitude)) - Vector2.one * .4f;
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
            if (Application.isPlaying && !presentationOnly)
            {
                nuggets = new EmergenceMassNugget[Mathf.Clamp(profile.nuggetCapacity, 4, 64)];
                bindings = new PlayerBinding[players == null ? 0 : players.Length];
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
                accumulator = Mathf.Min(accumulator + (presentationOnly ? Time.unscaledDeltaTime : Time.deltaTime), StepSeconds * Mathf.Clamp(profile.maximumStepsPerFrame, 1, 6));
                while (accumulator >= StepSeconds)
                {
                    stepWatch.Restart();
                    if (!presentationOnly) ApplyPlayerInteractions(StepSeconds);
                    if (Simulation is EmergenceGpuSimulation gpu) gpu.EmissionSlots = presentationOnly ? 0 : nuggets.Length - ActiveNuggets;
                    Simulation.Step(StepSeconds, !presentationOnly && ActiveNuggets < nuggets.Length);
                    if (!presentationOnly) ReleaseNuggets();
                    accumulator -= StepSeconds; stepWatch.Stop(); LastStepMilliseconds = (float)stepWatch.Elapsed.TotalMilliseconds;
                }
            }
            if (Simulation is EmergenceGpuSimulation status)
            {
                status.RequestStatus(); WakeContacts = status.WakeContacts; AttackContacts = status.AttackContacts; RepulsorContacts = status.RepulsorContacts;
                if (IsRunning && !presentationOnly) ReleaseNuggets();
            }
            RenderParticles();
        }
        public Vector3 WorldPosition(Vector2 p) => presentationOnly ? transform.TransformPoint(new Vector3(p.x, 0, p.y)) : center + axisX * p.x + axisY * p.y;
        public Vector2 PlanePosition(Vector3 p) { Vector3 d = p - center; return new Vector2(Vector3.Dot(d, axisX), Vector3.Dot(d, axisY)); }
        Vector2 PlaneVector(Vector3 v) => new Vector2(Vector3.Dot(v, axisX), Vector3.Dot(v, axisY));
        void ReleaseNuggets()
        {
            while (ActiveNuggets < nuggets.Length && Simulation.TryDequeue(out var e))
            {
                int slot = -1;
                for (int i = 0; i < nuggets.Length; i++) if (!nuggets[i] || !nuggets[i].gameObject.activeSelf) { slot = i; break; }
                if (slot < 0) break;
                if (!nuggets[slot]) { nuggets[slot] = Instantiate(nuggetPrefab, transform); nuggets[slot].name = "Metabolic Mass Nugget"; }
                Vector2 direction = e.velocity.sqrMagnitude > .001f ? -e.velocity.normalized : new Vector2(Mathf.Cos(Simulation.Age * 2.4f), Mathf.Sin(Simulation.Age * 2.4f));
                Vector3 point = arena.ClampWorldPointInside(WorldPosition(e.position + direction * .45f), .35f); point.y = 0;
                Vector2 v = e.velocity + direction * profile.nuggetEjectionSpeed;
                nuggets[slot].Eject(point, axisX * v.x + axisY * v.y, profile.nuggetLifetime); NuggetsReleased++;
            }
        }
        void OnDisable()
        {
            Camera.onPreCull -= RenderGpu;
            // Preserve the ecosystem on temporary disable; no fresh food or waste on re-enable.
            accumulator = 0;
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
            if (nuggets != null) foreach (var n in nuggets) if (n)
            {
                if (Application.isPlaying) Destroy(n.gameObject); else DestroyImmediate(n.gameObject);
            }
            nuggets = null; bindings = null;
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
            if (Simulation is EmergenceGpuSimulation preview) { preview.SetInfluences(null, 0); preview.EmissionSlots = 32; }
            stepWatch.Restart();
            for (int i = 0; i < steps; i++)
            {
                Simulation.Step(dt);
                while (Simulation.TryDequeue(out _)) { } // Count simulated waste without spawning scene objects.
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
