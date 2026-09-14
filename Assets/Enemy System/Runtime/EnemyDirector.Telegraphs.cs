using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    public partial class EnemyDirector
    {
        // A warning reserves both space and population capacity until it spawns or is cancelled.
        private sealed class ReservedSpawn
        {
            public EnemyDefinition enemy;
            public EnemySpawnProfile profile;
            public int cap, ruleIndex;
            public Vector3 position;
            public float remaining, blocked, timeout;
            public EnemySpawnTelegraph warning;
            public BatchState batch;
        }
        private readonly List<ReservedSpawn> _reservedSpawns = new List<ReservedSpawn>();
        public int PendingSpawnCount => _reservedSpawns.Count;

        private void ReserveSpawn(EnemyDefinition enemy, int cap, Vector3 position, EnemySpawnTelegraph prefab,
            float seconds, float timeout, BatchState batch, int ruleIndex = -1)
        {
            var warning = Instantiate(prefab, position, enemy.prefab.transform.rotation, transform);
            warning.Begin(seconds, SpawnWorldScale(enemy)); _telegraphs.Add(warning);
            _reservedSpawns.Add(new ReservedSpawn { enemy = enemy, profile = spawnProfile, cap = cap, position = position,
                remaining = Mathf.Max(0f, seconds), timeout = Mathf.Max(.1f, timeout), warning = warning,
                batch = batch, ruleIndex = ruleIndex });
        }

        private void TickIndividualBatch(BatchState state, float delta)
        {
            var rule = state.rule;
            state.remaining -= delta;
            if (state.remaining > 0f) return;
            if (state.pending == 0)
            {
                if (!CanSpawnEnemy(rule.enemy, rule.EffectiveCap)) { state.remaining = .25f; return; }
                state.pending = state.unannounced = rule.SizeAt(GameplayAge);
                state.hasAnchor = false; state.blocked = 0f; state.nextMemberSpawnAge = GameplayAge;
            }
            if (state.unannounced == 0) return;
            if (CanSpawnEnemy(rule.enemy, rule.EffectiveCap) &&
                TryFindSpawnPosition(rule.enemy, state.hasAnchor ? (Vector3?)state.anchor : null, rule.batchRadius, out var position))
            {
                if (!state.hasAnchor) { state.anchor = position; state.hasAnchor = true; }
                ReserveSpawn(rule.enemy, rule.EffectiveCap, position, rule.telegraphPrefab, rule.WarningSeconds,
                    rule.blockedBatchTimeout, state);
                state.unannounced--; state.blocked = 0f;
                // Pipeline announcements: every unit gets its full lead without adding that lead to the batch interval.
                state.remaining = Mathf.Max(.05f, rule.intervalWithinBatch);
            }
            else
            {
                state.remaining = .25f; state.blocked += .25f;
                if (state.blocked < Mathf.Max(.1f, rule.blockedBatchTimeout)) return;
                state.pending -= state.unannounced; state.unannounced = 0;
                if (state.pending == 0) FinishBatch(state);
            }
        }

        private void TickReservedSpawns(float delta)
        {
            if (_reservedSpawns.Count == 0) return;
            if (placementRegion != null)
                placementRegion.RefreshPlacementCache(resonanceSpawner != null ? resonanceSpawner.ActivePattern : placementRegion.previewPattern);
            // Oldest first; a recovered batch never releases several overdue members in one frame.
            for (int i = 0; i < _reservedSpawns.Count;)
            {
                var request = _reservedSpawns[i];
                bool valid = request.profile == spawnProfile && request.enemy && request.enemy.prefab;
                if (request.batch != null) valid &= request.batch.rule.enabled && request.batch.rule.enemy == request.enemy;
                else valid &= request.ruleIndex >= 0 && request.ruleIndex < spawnProfile.rules.Count &&
                    spawnProfile.rules[request.ruleIndex].enabled && spawnProfile.rules[request.ruleIndex].enemy == request.enemy;
                if (!valid || !request.warning) { ResolveReservation(i); continue; }
                request.remaining -= delta;
                if (request.remaining > 0f || request.batch != null && GameplayAge < request.batch.nextMemberSpawnAge)
                { i++; continue; }
                if (CanSpawnEnemy(request.enemy, request.cap, request) && IsPlacementClear(request.enemy, request.position, request))
                {
                    SpawnAt(request.enemy, request.position);
                    if (request.batch != null) request.batch.nextMemberSpawnAge = GameplayAge + Mathf.Max(.05f, request.batch.rule.intervalWithinBatch);
                    ResolveReservation(i); continue;
                }
                request.blocked += delta;
                if (request.blocked >= request.timeout) { ResolveReservation(i); continue; }
                i++;
            }
        }

        private void ResolveReservation(int index)
        {
            var request = _reservedSpawns[index]; _reservedSpawns.RemoveAt(index);
            if (request.warning) request.warning.Complete();
            if (request.batch != null && --request.batch.pending == 0) FinishBatch(request.batch);
        }

        private void CancelReservations(BatchState batch)
        {
            for (int i = _reservedSpawns.Count - 1; i >= 0; i--)
            {
                var request = _reservedSpawns[i]; if (request.batch != batch) continue;
                if (request.warning) request.warning.Complete();
                _reservedSpawns.RemoveAt(i);
            }
        }
    }
}
