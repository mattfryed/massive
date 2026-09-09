using System.Collections.Generic;
using Massive.Multiplier;
using Massive.Scoring;
using UnityEngine;

namespace Massive.Enemies
{
    public partial class EnemyDirector
    {
        [Header("Match and shared placement")]
        [Tooltip("Wait for the scene score service, and freeze when scoring closes or the bonus clock is paused.")]
        public bool waitForScoring;
        [Tooltip("Optional shared exclusion policy. Give this director its own region with neutral width 1 for full-arena spawning.")]
        public AmplifierSpawnRegion placementRegion;
        public AmplifierResonanceSpawner resonanceSpawner;
        public float GameplayAge { get; private set; }
        public int TotalSpawned { get; private set; }
        public int AliveCount => _alive.Count;

        private sealed class BatchState
        {
            public EnemyBatchSpawnRule rule;
            public float remaining, blocked;
            public int pending;
            public bool hasAnchor;
            public Vector3 anchor;
            public EnemySpawnTelegraph telegraph;
        }
        private readonly List<BatchState> _batches = new List<BatchState>();
        private readonly List<EnemySpawnTelegraph> _telegraphs = new List<EnemySpawnTelegraph>();
        public int ActiveTelegraphCount => _telegraphs.Count;
        private bool _matchPaused;
        private readonly System.Random _placementRandom = new System.Random();

        private void InitializeBatches()
        {
            CancelBatchTelegraphs(); _batches.Clear(); GameplayAge = 0f;
            if (spawnProfile == null || spawnProfile.batches == null) return;
            foreach (var rule in spawnProfile.batches)
                if (rule != null) _batches.Add(new BatchState { rule = rule, remaining = Mathf.Max(0f, rule.firstBatchDelay - rule.WarningSeconds) });
        }

        private bool CheckMatchPause()
        {
            bool paused = waitForScoring && (MatchScoreService.Instance == null ||
                !MatchScoreService.Instance.IsScoringOpen || !MatchScoreService.Instance.IsChainClockRunning);
            if (waitForScoring && (MatchScoreService.Instance == null || !MatchScoreService.Instance.IsScoringOpen))
                CancelBatchTelegraphs();
            if (paused != _matchPaused)
            {
                _matchPaused = paused;
                PauseAll(paused || (_cachedAnomalyRunning && spawnProfile.freezeExistingEnemiesDuringAnomalies));
            }
            if (paused) _nextSpawnTime += Time.deltaTime;
            return paused;
        }

        private void TickBatches(float delta)
        {
            GameplayAge += delta;
            _matchStartTime = Time.time - GameplayAge;
            // One owner advances warning, pulse, and fade using the same clock as the batch.
            for (int i = _telegraphs.Count - 1; i >= 0; i--)
            {
                var warning = _telegraphs[i];
                if (!warning || !warning.Advance(delta)) _telegraphs.RemoveAt(i);
            }
            foreach (var state in _batches)
            {
                var r = state.rule;
                if (!r.enabled || r.enemy == null || r.enemy.prefab == null)
                { if (state.pending > 0) FinishBatch(state); continue; }
                state.remaining -= delta;
                if (state.remaining > 0f) continue;
                if (state.pending == 0)
                {
                    // Pick the location before showing a warning. Never silently relocate an announced batch.
                    if (r.telegraphPrefab != null && (!CanSpawnEnemy(r.enemy, r.EffectiveCap) || !TryFindSpawnPosition(r.enemy, null, 0f, out state.anchor)))
                    { state.remaining = .25f; continue; }
                    state.pending = r.SizeAt(GameplayAge);
                    state.blocked = 0f;
                    state.hasAnchor = r.telegraphPrefab != null;
                    if (r.telegraphPrefab != null)
                    {
                        state.telegraph = Instantiate(r.telegraphPrefab, state.anchor, r.enemy.prefab.transform.rotation, transform);
                        state.telegraph.Begin(); _telegraphs.Add(state.telegraph);
                        state.remaining = r.WarningSeconds;
                        if (state.remaining > 0f) continue;
                    }
                }
                Vector3 position;
                if (TrySpawnEnemy(r.enemy, r.EffectiveCap, state.hasAnchor ? (Vector3?)state.anchor : null,
                    r.batchRadius, out position))
                {
                    if (!state.hasAnchor) { state.anchor = position; state.hasAnchor = true; }
                    state.pending--; state.blocked = 0f;
                    if (state.pending > 0) state.remaining = Mathf.Max(.05f, r.intervalWithinBatch);
                    else FinishBatch(state);
                }
                else
                {
                    state.blocked += .25f;
                    state.remaining = .25f;
                    if (state.blocked >= Mathf.Max(.1f, r.blockedBatchTimeout))
                        FinishBatch(state);
                }
            }
        }

        private void FinishBatch(BatchState state)
        {
            if (state.telegraph) state.telegraph.Complete();
            state.telegraph = null; state.pending = 0; state.blocked = 0f; state.hasAnchor = false;
            // Preserve completion-to-next-unit cadence where possible; the full warning always wins.
            state.remaining = Mathf.Max(0f, state.rule.DelayAt(GameplayAge) - state.rule.WarningSeconds);
        }

        private void CancelBatchTelegraphs()
        {
            foreach (var state in _batches)
                if (state.pending > 0 || state.hasAnchor) FinishBatch(state);
            foreach (var warning in _telegraphs) if (warning) warning.Cancel();
            _telegraphs.Clear();
        }

        public bool TrySpawnEnemy(EnemyDefinition def, int cap, Vector3? clusterCenter, float clusterRadius, out Vector3 position)
        {
            position = default;
            if (!CanSpawnEnemy(def, cap) || !TryFindSpawnPosition(def, clusterCenter, clusterRadius, out position)) return false;
            var go = Instantiate(def.prefab, position, def.prefab.transform.rotation, enemyRoot);
            if (go.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.position = position;
                if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            }
            var enemy = go.GetComponent<EnemyBase>();
            if (enemy == null) enemy = go.AddComponent<EnemyBase>();
            enemy.Init(def, this); RegisterEnemy(enemy); TotalSpawned++;
            if (_cachedAnomalyRunning && spawnProfile.freezeExistingEnemiesDuringAnomalies) enemy.Pause(true);
            return true;
        }

        private bool CanSpawnEnemy(EnemyDefinition def, int cap)
        {
            if (spawnProfile == null || !spawnProfile.enabled || def == null || def.prefab == null || arenaBounds == null) return false;
            if (_matchPaused || (_cachedAnomalyRunning && !spawnProfile.allowSpawningDuringAnomalies)) return false;
            if (spawnProfile.maxAliveTotal > 0 && _alive.Count >= spawnProfile.maxAliveTotal) return false;
            _aliveByDef.TryGetValue(def, out int count);
            int effectiveCap = cap <= 0 ? def.maxAliveOverride : def.maxAliveOverride <= 0 ? cap : Mathf.Min(cap, def.maxAliveOverride);
            if (effectiveCap > 0 && count >= effectiveCap || !CategoryCapAllows(def.category)) return false;
            return true;
        }

        private bool TryFindSpawnPosition(EnemyDefinition def, Vector3? clusterCenter, float clusterRadius, out Vector3 position)
        {
            position = default;
            float clearance = Mathf.Max(spawnCheckRadiusWorld, def.spawnRadiusWorld);
            if (placementRegion != null)
                placementRegion.RefreshPlacementCache(resonanceSpawner != null ? resonanceSpawner.ActivePattern : placementRegion.previewPattern);
            for (int attempt = 0; attempt < Mathf.Max(1, spawnAttemptsPerTick); attempt++)
            {
                Vector3 p;
                if (clusterCenter.HasValue)
                {
                    Vector2 offset = Random.insideUnitCircle * Mathf.Max(.1f, clusterRadius);
                    p = clusterCenter.Value + new Vector3(offset.x, 0f, offset.y);
                    if (!arenaBounds.ContainsWorldPoint(p, borderBufferWorld + clearance)) continue;
                }
                else if (placementRegion != null)
                {
                    Rect rect; string reason;
                    if (!placementRegion.TryGetNeutralRect(clearance, out rect, out reason)) return false;
                    p = placementRegion.GridPointToWorld(new Vector2(
                        Mathf.Lerp(rect.xMin, rect.xMax, (float)_placementRandom.NextDouble()),
                        Mathf.Lerp(rect.yMin, rect.yMax, (float)_placementRandom.NextDouble())));
                }
                else if (!TrySamplePointInArena(clearance, out p)) continue;
                if (placementRegion != null && !placementRegion.IsValidCached(p, clearance, out _)) continue;
                if (!IsSpawnPointClear(p, clearance) || !IsFarEnoughFromPlayers(p)) continue;
                // Trigger-only Drones also reserve space, independently of the optional physics mask.
                bool occupied = false;
                foreach (var live in _alive)
                {
                    if (live == null) continue;
                    float separation = clearance + (live.Definition != null ? live.Definition.spawnRadiusWorld : clearance);
                    if ((live.transform.position - p).sqrMagnitude < separation * separation) { occupied = true; break; }
                }
                if (occupied) continue;
                position = p; return true;
            }
            return false;
        }
    }
}
