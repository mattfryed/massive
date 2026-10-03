using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    public partial class EnemyDirector
    {
        [Header("Authored encounters (optional; replaces random/batch scheduling)")]
        public EnemyEncounterTimeline encounterTimeline;
        public EnemyArenaLayout arenaLayout;
        public int encounterSeed = 1;
        [Tooltip("-1 plays the complete timeline. Otherwise previews one cue after its full warning.")]
        public int previewCue = -1;
        [NonSerialized] public bool timelinePaused;
        public Transform SimulationRoot { get; private set; }
        public event Action<EnemyBase> TimelineEnemySpawned;
        public IReadOnlyList<CueStatus> CueStates => cueStates;
        public int TimelinePendingCount => members.Count;
        public bool TimelineFinished => encounterTimeline &&
            cueStates.TrueForAll(s => s.finished) && members.Count == 0 &&
            GameplayAge >= (previewCue >= 0 ? PreviewEnd : encounterTimeline.duration);
        private Massive.Demonstrations.EnemyEncounterLab populationPreview;
        public bool IgnoreTimelineBudgetsForPreview => Application.isPlaying && populationPreview &&
            populationPreview.isActiveAndEnabled && populationPreview.director == this &&
            populationPreview.OwnsSession(SimulationRoot) && populationPreview.ignorePopulationLimits;
        public float CurrentPressure => encounterTimeline ? CapturePopulationBudget().Pressure : 0;
        public EnemyPopulationBudget CapturePopulationBudget() => CapturePopulationBudget(null);
        private EnemyPopulationBudget CapturePopulationBudget(ReservedSpawn consuming)
        {
            var budget = new EnemyPopulationBudget();
            if (!encounterTimeline) return budget;
            foreach (var enemy in _alive) if (enemy && !enemy.IsDead)
                budget.Add(encounterTimeline, enemy.Definition, EnemyPopulationBudget.Kind.Alive);
            foreach (var member in members)
                budget.Add(encounterTimeline, member.slot.enemy, EnemyPopulationBudget.Kind.Reserved);
            foreach (var reservation in _reservedSpawns) if (reservation != consuming)
                budget.Add(encounterTimeline, reservation.enemy, EnemyPopulationBudget.Kind.Reserved);
            return budget;
        }
        [Serializable] public sealed class CueStatus
        {
            public int index;
            public string label, state = "Waiting", reason;
            public float arrival, expiry, nextAttempt, lastRelease = -1f;
            public bool mirror, finished;
            public EnemyFormation formation;
            public int spawned;
            public int skipped;
            public int WaitingCount => Mathf.Max(0, slots.Count - spawned - skipped);
            public string Progress => $"{spawned} spawned, {WaitingCount} waiting, {skipped} skipped";
            public readonly List<SlotStatus> slots = new();
            internal readonly List<FormationUnit> units = new();
        }
        [Serializable] public sealed class SlotStatus
        {
            public int index;
            public Vector3 authored, position, clearance;
            public Quaternion rotation;
            public float radius;
            public string state = "Waiting", reason;
            public bool adjusted, announced;
        }
        internal sealed class FormationMember
        {
            public CueStatus cue;
            public EnemyFormation.Slot slot;
            public EnemyArenaLayout.Pose pose;
            public float arrival, warningAt, deadline;
            public EnemySpawnTelegraph warning;
            public EnemyArenaLayout.Pose authored;
            public SlotStatus status;
            public bool placed;
        }
        internal sealed class FormationUnit
        {
            public readonly List<FormationMember> members = new();
            public float arrival, warningAt, deadline, nextAttempt, releaseDelay;
            public bool announced, finished, blocked, wasBlocked;
            public string reason;
        }
        private readonly List<CueStatus> cueStates = new();
        private readonly List<FormationMember> members = new();
        private readonly Dictionary<EnemyArenaLayout.Socket, EnemyBase> occupiedSockets = new();
        private bool timelineInitialized, appliedTimelinePause;
        private float PreviewEnd => cueStates.Count == 0 ? 0 : cueStates[0].expiry + 20f;

        public void ConfigureDemonstration(Transform scope)
        {
            SimulationRoot = scope;
            populationPreview = GetComponent<Massive.Demonstrations.EnemyEncounterLab>();
        }
        public EnemyFormation ChooseFormation(int cueIndex, out bool mirror)
        {
            var cue = encounterTimeline.cues[cueIndex];
            var random = new System.Random(unchecked(encounterSeed * 397 ^ cueIndex * 7919));
            var options = new List<EnemyFormation>();
            if (cue.formation) options.Add(cue.formation);
            foreach (var item in cue.variants) if (item) options.Add(item);
            var chosen = options.Count == 0 ? null : options[random.Next(options.Count)];
            mirror = cue.allowHorizontalMirror && random.Next(2) == 1; return chosen;
        }
        public void RestartTimeline(bool clearEnemies = false)
        {
            CancelTimeline(); CancelBatchTelegraphs();
            if (clearEnemies)
            {
                foreach (var enemy in _alive.ToArray()) if (enemy) { NotifyEnemyDestroyed(enemy); Destroy(enemy.gameObject); }
                occupiedSockets.Clear();
            }
            GameplayAge = 0f; TotalSpawned = 0; timelineInitialized = false;
            InitializeTimeline();
        }
        private void InitializeTimeline()
        {
            cueStates.Clear(); timelineInitialized = true;
            if (!encounterTimeline) return;
            for (int i = 0; i < encounterTimeline.cues.Count; i++)
            {
                if (previewCue >= 0 && i != previewCue) continue;
                var cue = encounterTimeline.cues[i];
                var formation = ChooseFormation(i, out bool mirror);
                float arrival = previewCue >= 0 ? (formation ? formation.warningSeconds : 2f) + 1f : cue.arrivalSeconds;
                cueStates.Add(new CueStatus { index = i, label = cue.label, formation = formation, mirror = mirror,
                    arrival = arrival, expiry = arrival + Mathf.Max(0, cue.allowedLateness) });
            }
        }
        private void CancelTimeline()
        {
            foreach (var cue in cueStates) if (!cue.finished) FinishCue(cue, "Cancelled", "Preview stopped");
            members.Clear();
        }
        private void TickTimeline(float delta)
        {
            if (!timelineInitialized) InitializeTimeline();
            if (appliedTimelinePause != timelinePaused)
            { appliedTimelinePause = timelinePaused; PauseAll(timelinePaused); }
            if (timelinePaused) return;
            GameplayAge += delta; _matchStartTime = Time.time - GameplayAge;
            for (int i = _telegraphs.Count - 1; i >= 0; i--)
                if (!_telegraphs[i] || !_telegraphs[i].Advance(delta)) _telegraphs.RemoveAt(i);
            if (placementRegion) placementRegion.RefreshPlacementCache(resonanceSpawner ? resonanceSpawner.ActivePattern : placementRegion.previewPattern);
            foreach (var state in cueStates)
            {
                if (state.finished) continue;
                if (!state.formation) { FinishCue(state, "Skipped", "Missing formation"); continue; }
                if (state.state == "Waiting" || state.state == "Blocked")
                {
                    if (GameplayAge < state.arrival - state.formation.warningSeconds || GameplayAge < state.nextAttempt) continue;
                    if (GameplayAge + state.formation.warningSeconds > state.expiry + .02f)
                    { FinishCue(state, "Skipped", state.reason ?? "Insufficient warning lead"); continue; }
                    if (!TryReserveFormation(state, state.formation, out string reason))
                    {
                        var fallback = encounterTimeline.cues[state.index].fallback;
                        if (!fallback || GameplayAge + fallback.warningSeconds > state.expiry + .02f || !TryReserveFormation(state, fallback, out _))
                        { state.state = "Blocked"; state.reason = reason; state.nextAttempt = GameplayAge + .25f; continue; }
                    }
                }
                if (encounterTimeline.cues[state.index].IntegrityFor(state.formation) == EnemyFormation.Integrity.Flexible)
                    TickFlexibleFormation(state);
                else TickFormation(state);
            }
        }
        private bool TryReserveFormation(CueStatus cue, EnemyFormation formation, out string reason)
        {
            reason = null;
            if (!arenaLayout || formation.slots.Count == 0) { reason = "Missing layout or empty formation"; return false; }
            var proposed = new List<FormationMember>();
            var policy = encounterTimeline.cues[cue.index];
            bool flexible = policy.IntegrityFor(formation) == EnemyFormation.Integrity.Flexible;
            cue.slots.Clear(); cue.units.Clear();
            float arrival = Mathf.Max(cue.arrival, GameplayAge + Mathf.Max(.1f, formation.warningSeconds));
            foreach (var slot in formation.slots)
            {
                if (!arenaLayout.Resolve(slot, cue.mirror, out var pose, out reason)) return false;
                var status = new SlotStatus { index = proposed.Count, authored = pose.position };
                cue.slots.Add(status);
                var member = new FormationMember { cue = cue, slot = slot, pose = pose, authored = pose, status = status,
                    deadline = (flexible ? cue.arrival + policy.GraceFor(formation) : cue.expiry) + Mathf.Max(0f, slot.releaseDelay),
                    arrival = arrival + Mathf.Max(0f, slot.releaseDelay), warningAt = arrival + Mathf.Max(0f, slot.releaseDelay) - formation.warningSeconds };
                UpdateSlotPose(member);
                proposed.Add(member);
            }
            if (!TimelineBudgetAllows(proposed, null, out reason)) return false;
            ResolvePlacement(proposed, formation, policy.AdjustmentFor(formation), flexible);
            if (flexible && policy.fallback && formation != policy.fallback && proposed.TrueForAll(m => !m.placed))
            { reason = proposed[0].status.reason; return false; }
            if (!flexible)
                foreach (var m in proposed) if (!m.placed) { reason = m.status.reason; return false; }
            // A flexible batch reserves its full budget, but only clear footprints claim space.
            if (flexible) BuildFlexibleUnits(cue, proposed, formation);
            members.AddRange(proposed); cue.formation = formation; cue.state = "Reserved"; cue.reason = null;
            return true;
        }
        private bool Overlaps(FormationMember a, FormationMember b)
        {
            if (a.pose.socket != null && a.pose.socket == b.pose.socket) return true;
            float r = Mathf.Max(spawnCheckRadiusWorld, a.slot.enemy.GetSpawnRadiusWorld()) + Mathf.Max(spawnCheckRadiusWorld, b.slot.enemy.GetSpawnRadiusWorld());
            return (a.pose.clearance - b.pose.clearance).sqrMagnitude < r * r;
        }
        private bool MemberClear(FormationMember member, CueStatus ownCue, out string reason)
        {
            var pose = member.pose;
            float radius = Mathf.Max(spawnCheckRadiusWorld, member.slot.enemy.GetSpawnRadiusWorld());
            if (!arenaLayout.Clear(pose, radius, borderBufferWorld, out reason)) return false;
            if (pose.socket != null && (!pose.socket.enabled || occupiedSockets.TryGetValue(pose.socket, out var occupant) && occupant && !occupant.IsDead))
            { reason = "Wall socket occupied or disabled"; return false; }
            // Authored timeline regions own territory/height. Reuse Amplifier exclusions,
            // not its power-up-only neutral stripe or goal attraction. Scoped player clearance is checked below.
            if (pose.socket == null && placementRegion &&
                !placementRegion.IsClearOfExclusionsCached(pose.position, radius, out reason,
                    excludePlayers: false, excludeGoalAttraction: false)) return false;
            int hitCount = Physics.OverlapSphereNonAlloc(pose.clearance, radius, placementHits, spawnBlockMask, QueryTriggerInteraction.Collide);
            if (hitCount == placementHits.Length) { reason = "Placement query full"; return false; }
            for (int i = 0; i < hitCount; i++)
                if (placementHits[i] != pose.socket?.support && !arenaLayout.AllowsSpawnOverlap(placementHits[i]))
                { reason = "Collider: " + placementHits[i].name; return false; }
            foreach (var player in PlayerControllerScript.ActivePlayers)
            {
                if (!player || !player.isActiveAndEnabled || player.gameObject.scene != gameObject.scene) continue;
                if (SimulationRoot ? player.SimulationRoot != SimulationRoot : player.IsPseudoPlayer) continue;
                float distance = Mathf.Max(minDistanceFromPlayers, radius + .4f);
                if ((player.transform.position - pose.clearance).sqrMagnitude < distance * distance)
                { reason = "Player occupies arrival"; return false; }
            }
            foreach (var enemy in _alive)
                if (enemy && !enemy.IsDead && (enemy.transform.position - pose.clearance).sqrMagnitude < Mathf.Pow(radius + enemy.Definition.GetSpawnRadiusWorld(enemy.transform), 2))
                { reason = "Enemy occupies arrival"; return false; }
            foreach (var other in members)
                if (other.placed && other.cue != ownCue && Overlaps(member, other)) { reason = "Another formation reserved this space"; return false; }
            return true;
        }
        private bool TimelineBudgetAllows(List<FormationMember> extra, EnemyDefinition additional, out string reason, ReservedSpawn consuming = null)
        {
            reason = null;
            if (!encounterTimeline || IgnoreTimelineBudgetsForPreview) return true;
            var budget = CapturePopulationBudget(consuming);
            if (extra != null) foreach (var member in extra)
                budget.Add(encounterTimeline, member.slot.enemy, EnemyPopulationBudget.Kind.Requested);
            budget.Add(encounterTimeline, additional, EnemyPopulationBudget.Kind.Requested);
            return budget.Allows(encounterTimeline, out reason);
        }
        private void TickFormation(CueStatus cue)
        {
            var pending = members.FindAll(m => m.cue == cue);
            if (pending.Count == 0) { FinishCue(cue, "Complete", null); return; }
            foreach (var m in pending)
                if (!m.warning && GameplayAge >= m.warningAt)
                {
                    // Even a delayed frame must give a newly shown warning its complete lead.
                    m.arrival = Mathf.Max(m.arrival, GameplayAge + Mathf.Max(.1f, cue.formation.warningSeconds));
                    m.warning = Instantiate(m.slot.telegraph, m.pose.position, m.pose.rotation, transform);
                    m.warning.Begin(Mathf.Max(.1f, cue.formation.warningSeconds), SpawnWorldScale(m.slot.enemy), SpawnOutline(m.slot.enemy)); _telegraphs.Add(m.warning);
                    m.status.announced = true; m.status.state = "Warning";
                }
            float next = float.MaxValue;
            foreach (var m in pending) next = Mathf.Min(next, m.arrival);
            if (GameplayAge < next) return;
            // Recheck the entire remaining formation. A paired row cannot appear as a broken half-row.
            foreach (var m in pending)
                if (!MemberClear(m, cue, out var reason))
                {
                    cue.state = "Holding"; cue.reason = reason;
                    m.status.state = "Blocked"; m.status.reason = reason;
                    if (pending.Exists(m => GameplayAge > m.deadline)) FinishCue(cue, "Expired", reason);
                    return;
                }
            if (!TimelineBudgetAllows(null, null, out string budget))
            { FinishCue(cue, "Expired", budget); return; }
            if (pending.Exists(m => m.arrival <= next + .001f && GameplayAge > m.deadline + .05f))
            { FinishCue(cue, "Expired", "Release deadline passed"); return; }
            float drift = Mathf.Max(0f, GameplayAge - next);
            foreach (var m in pending)
            {
                if (m.arrival > next + .001f)
                {
                    // Shift later releases by the actual delay; never dump overdue staggered members in one frame.
                    m.arrival += drift; m.warningAt += drift; continue;
                }
                members.Remove(m); SpawnFormationMember(m); cue.spawned++;
                m.status.state = "Spawned"; m.status.reason = null;
                if (m.warning) m.warning.Complete();
            }
            cue.lastRelease = GameplayAge; cue.state = "Releasing"; cue.reason = null;
            if (!members.Exists(m => m.cue == cue)) FinishCue(cue, "Complete", null);
        }
        private void FinishCue(CueStatus cue, string state, string reason)
        {
            for (int i = members.Count - 1; i >= 0; i--)
                if (members[i].cue == cue)
                {
                    var m = members[i]; if (m.warning) m.warning.Cancel();
                    m.status.state = state == "Cancelled" ? "Cancelled" : "Skipped"; m.status.reason = reason;
                    cue.skipped++; members.RemoveAt(i);
                }
            foreach (var unit in cue.units) unit.finished = true;
            foreach (var slot in cue.slots)
                if (slot.state != "Spawned" && slot.state != "Skipped" && slot.state != "Cancelled")
                { slot.state = state == "Cancelled" ? "Cancelled" : "Skipped"; slot.reason = reason; cue.skipped++; }
            cue.state = state; cue.reason = reason; cue.finished = true;
        }
        private void SpawnFormationMember(FormationMember member)
        {
            var staging = new GameObject("Formation staging"); staging.transform.SetParent(transform, false); staging.SetActive(false);
            var go = Instantiate(member.slot.enemy.prefab, member.pose.position, member.pose.rotation, staging.transform);
            var enemy = go.GetComponent<EnemyBase>(); enemy.Init(member.slot.enemy, this);
            if (SimulationRoot) enemy.ConfigureDemonstration(SimulationRoot, null);
            if (go.TryGetComponent<ParticleBeamTurretController>(out var turret))
            {
                turret.mount = ParticleBeamTurretController.WallMount.Authored; turret.arenaBounds = arenaBounds;
                turret.spawnWarningSeconds = 0; turret.sceneDirector = this;
                if (member.pose.socket != null) turret.maxPivotAngle = member.pose.socket.aimArc;
            }
            if (go.TryGetComponent<CarrierController>(out var carrier)) { carrier.spawnWarningSeconds = 0; carrier.sceneDirector = this; }
            if (go.TryGetComponent<SeekerController>(out var seeker)) { seeker.spawnWarningSeconds = 0; seeker.sceneDirector = this; }
            if (SimulationRoot && go.TryGetComponent<RangedDroneController>(out var ranged))
                ranged.ShotFired += shot => { if (shot && enemyRoot) shot.transform.SetParent(enemyRoot, true); };
            if (member.slot.entrySeconds > 0 && go.GetComponent<DroneController>())
            {
                var entry = go.AddComponent<EnemyFormationEntry>(); entry.seconds = member.slot.entrySeconds;
                entry.speed = member.slot.entrySpeed; entry.direction = member.pose.rotation * Vector3.forward;
            }
            RegisterEnemy(enemy); TotalSpawned++;
            if (member.pose.socket != null) occupiedSockets[member.pose.socket] = enemy;
            go.transform.SetParent(enemyRoot ? enemyRoot : transform, true); Destroy(staging);
            TimelineEnemySpawned?.Invoke(enemy);
        }
    }
}
