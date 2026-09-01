using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(600)]
[DisallowMultipleComponent]
public class HiggsExcitationKnotManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HiggsFieldGPU higgsField;
    [SerializeField] private SymmetryKnotController knotPrefab;
    [SerializeField] private Transform knotsParent; // optional; defaults to this transform

    [Header("Gameplay -> Higgs Excitation Rate Override")]
    [SerializeField] private bool driveHiggsSpawnRateFromManager = true;
    [SerializeField] private float initialNoSpawnSeconds = 6f;
    [SerializeField] private float spawnsPerSecondEarly = 0.18f;
    [SerializeField] private float spawnsPerSecondLate = 0.55f;
    [SerializeField] private float spawnRateRampSeconds = 90f;
    [SerializeField] private AnimationCurve spawnRateCurve01 = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Readback")]
    [SerializeField, Range(0.02f, 0.5f)] private float readbackInterval = 0.10f;

    [Tooltip("If exciteT is <= this, treat bubble as not excited (for readback end-time updates).")]
    [SerializeField] private float exciteTThreshold = 0.01f;

    [Header("Spawn Mapping")]
    [SerializeField] private float arenaY = 0f;
    [SerializeField] private bool swapUV = false;
    [SerializeField] private bool invertU = false;
    [SerializeField] private bool invertV = false;

    [Header("Spawn Bounds (no-spawn border)")]
    [Tooltip("Extra padding beyond knot capture radius to keep the capture ring fully in-bounds.")]
    [SerializeField] private float borderPaddingWorld = 0.25f;

    [Tooltip("If > 0, overrides (captureRadius + padding) and uses this absolute border inset.")]
    [SerializeField] private float borderOverrideWorld = -1f;

    // If you don't expose CaptureRadius on the knot, set this manually to match your prefab.
    [SerializeField] private float knotCaptureRadiusForBounds = 1.15f;

    [Header("Follow Bubble (optional)")]
    [SerializeField] private bool followExcitationPosition = false;
    [SerializeField, Range(1f, 30f)] private float followSmoothing = 10f;

    [Header("Match Start Gating")]
    [SerializeField, Min(0f)] private float initialKnotSpawnDelaySeconds = 6.0f;

    [Header("Spawn Restrictions")]
    [SerializeField, Min(0f)] private float minKnotSpacing = 5f;

    [SerializeField, Min(0f)] private float minDistanceFromGoalMouths = 7.0f;

    [SerializeField] private List<SymmetryKnotGoalMouth> goalMouths = new();
    [SerializeField] private bool autoFindGoalMouthsIfEmpty = true;

    [Header("Cooldown")]
    [SerializeField, Min(0f)] private float respawnCooldownSeconds = 2.0f;
    [SerializeField] private bool suppressRespawnUntilExcitationEnds = true;

    [Header("Pacing (Designer Friendly)")]
    [Tooltip("Seconds until pacing reaches the 'late game' end of the curves.")]
    [SerializeField, Min(0.01f)] private float rampSeconds = 600f;

    [SerializeField] private int maxKnotsEarly = 1;
    [SerializeField] private int maxKnotsLate = 8;

    [Tooltip("Shapes how quickly concurrency ramps up over match progress (0..1). Y expected 0..1.")]
    [SerializeField] private AnimationCurve concurrencyCurve01 =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Hard cap on how many knots we can spawn on a single readback tick (extra anti-burst safety).")]
    [SerializeField, Range(1, 8)] private int maxSpawnsPerReadbackTick = 2;

    [Header("Knot Timing (passed into SymmetryKnotController.BindToExcitation)")]
    [SerializeField] private float unclaimedSeconds = 4.0f;
    [SerializeField] private float claimedSeconds = 10.0f;
    [SerializeField] private float excitationTailSeconds = 0.75f;

    [Header("Debug")]
    [SerializeField] private bool logSpawnEvents = false;
    [SerializeField] private bool prewarmOnStart = true;

    // IMPORTANT: must match Bubble struct layout in HiggsFieldSim.compute
    [StructLayout(LayoutKind.Sequential)]
    private struct BubbleGPU
    {
        public Vector2 posUV;
        public Vector2 velUV;
        public float amp;
        public float radius;
        public float phase;
        public float exciteT;

        public float exciteAge;
        public float pad0;

        public Vector2 pad;
    }

    private class KnotRecord
    {
        public int id;
        public SymmetryKnotController knot;
        public Vector3 spawnPos;
        public float excitationEndTime; // world time
    }

    private readonly Dictionary<int, KnotRecord> _knotsById = new();
    private readonly Dictionary<int, float> _cooldownUntil = new();

    private float _sessionStartTime;
    private float _nextReadbackTime;
    private bool _readbackPending;

    private float _nextSpawnAllowedTime; // spawn-rate limiter

    private IEnumerator Start()
    {
        if (knotsParent == null)
            knotsParent = transform;

        if (autoFindGoalMouthsIfEmpty && (goalMouths == null || goalMouths.Count == 0))
            AutoPopulateGoalMouths();

        _sessionStartTime = Time.time;
        _nextReadbackTime = Time.time; // allow immediate first readback
        _nextSpawnAllowedTime = _sessionStartTime + initialKnotSpawnDelaySeconds;

        if (prewarmOnStart)
            yield return PrewarmRoutine();
    }

    private IEnumerator PrewarmRoutine()
    {
        // Warm one readback (discard result) to avoid first-time AsyncGPUReadback hitch.
        if (higgsField != null && higgsField.BubbleBuffer != null)
            AsyncGPUReadback.Request(higgsField.BubbleBuffer, _ => { });

        // Warm one knot instantiate to build Shapes reflection cache / slice pools.
        if (knotPrefab != null)
        {
            var warm = Instantiate(knotPrefab, new Vector3(0, -9999f, 0), Quaternion.identity, knotsParent);
            warm.Initialize(warm.transform.position);

            // Let one frame happen so any render/shader/material setup occurs.
            yield return null;

            Destroy(warm.gameObject);
        }
    }

    /// <summary>
    /// STEP 3 BLOCK GOES HERE:
    /// Put the Higgs spawn-rate override driving code INSIDE Update(),
    /// so it runs continuously while the match runs.
    /// </summary>
    private void Update()
    {
        if (!driveHiggsSpawnRateFromManager || higgsField == null)
            return;

        float t = Time.time - _sessionStartTime;

        // Hard delay at match start so you don't get an instant excitation/knot.
        if (t < initialNoSpawnSeconds)
        {
            higgsField.SetGameplayExcitationsPerSecond(0f);
            return;
        }

        float t01 = Mathf.Clamp01((t - initialNoSpawnSeconds) / Mathf.Max(0.001f, spawnRateRampSeconds));
        float k = (spawnRateCurve01 != null) ? spawnRateCurve01.Evaluate(t01) : t01;

        float perSecond = Mathf.Lerp(spawnsPerSecondEarly, spawnsPerSecondLate, k);

        // Decouple gameplay pacing from bubble visuals.
        higgsField.SetGameplayExcitationsPerSecond(perSecond);
    }

    private void LateUpdate()
    {
        PruneDeadKnots();

        if (higgsField == null || knotPrefab == null)
            return;

        // Periodic readback
        if (!_readbackPending && Time.time >= _nextReadbackTime)
        {
            var buffer = higgsField.BubbleBuffer;
            if (buffer == null)
                return;

            _readbackPending = true;
            _nextReadbackTime = Time.time + readbackInterval;

            AsyncGPUReadback.Request(buffer, OnReadbackComplete);
        }
    }

    private void OnReadbackComplete(AsyncGPUReadbackRequest req)
    {
        _readbackPending = false;
        if (!this || !enabled) return;
        if (req.hasError) return;

        float now = Time.time;

        var data = req.GetData<BubbleGPU>();

        // 1) Update existing knots (end time + optional follow)
        foreach (var kv in _knotsById)
        {
            var rec = kv.Value;
            if (rec == null || rec.knot == null) continue;

            int id = rec.id;
            if (id < 0 || id >= data.Length) continue;

            // Update excitation end time from GPU (includes any hold-extensions)
            float exciteT = data[id].exciteT;
            if (exciteT > exciteTThreshold)
            {
                float endTime = now + exciteT;
                rec.excitationEndTime = Mathf.Max(rec.excitationEndTime, endTime);
                rec.knot.UpdateExcitationEndTime(endTime);
            }

            if (followExcitationPosition)
            {
                Vector3 wpos = UVToWorld(NormalizeUV(data[id].posUV));
                rec.spawnPos = Vector3.Lerp(rec.spawnPos, wpos, 1f - Mathf.Exp(-followSmoothing * readbackInterval));
                rec.knot.transform.position = rec.spawnPos;
            }
        }

        // 2) Spawn if under cap + pacing allows
        int allowed = ComputeAllowedKnotCount(now);
        TrySpawnKnots(now, allowed, data);
    }

    private void TrySpawnKnots(float now, int allowed, NativeArray<BubbleGPU> bubbles)
    {
        // Don’t spawn during the “start delay”
        if (now < _sessionStartTime + initialKnotSpawnDelaySeconds)
            return;

        int activeCount = CountActiveKnots();
        int capacity = Mathf.Max(0, allowed - activeCount);
        if (capacity <= 0)
            return;

        // Rate-limit spawning (designer curve controlled)
        float progress01 = ComputeProgress01(now);

        // NOTE: this is *knot* spawn pacing. (Independent from Higgs excitation pacing override)
        float spawnRate01 = Mathf.Clamp01((spawnRateCurve01 != null) ? spawnRateCurve01.Evaluate(progress01) : progress01);
        float spawnsPerSec = Mathf.Lerp(spawnsPerSecondEarly, spawnsPerSecondLate, spawnRate01);
        float spawnInterval = (spawnsPerSec <= 0.001f) ? float.PositiveInfinity : (1f / spawnsPerSec);

        int spawnedThisTick = 0;

        while (capacity > 0 &&
               spawnedThisTick < maxSpawnsPerReadbackTick &&
               now >= _nextSpawnAllowedTime)
        {
            if (TryPickSpawnBubble(now, bubbles, out int id, out Vector3 pos))
            {
                SpawnKnot(now, id, pos, bubbles);
                capacity--;
                spawnedThisTick++;

                _nextSpawnAllowedTime = now + spawnInterval;
            }
            else
            {
                // No valid candidate found this tick; try again next readback.
                _nextSpawnAllowedTime = now + Mathf.Min(0.25f, spawnInterval);
                break;
            }
        }
    }

    private bool TryPickSpawnBubble(float now, NativeArray<BubbleGPU> bubbles, out int id, out Vector3 worldPos)
    {
        id = -1;
        worldPos = Vector3.zero;

        if (bubbles.Length == 0)
            return false;

        const int attempts = 48;

        for (int a = 0; a < attempts; a++)
        {
            int candidate = UnityEngine.Random.Range(0, bubbles.Length);

            if (_knotsById.ContainsKey(candidate))
                continue;

            if (_cooldownUntil.TryGetValue(candidate, out float cd) && now < cd)
                continue;

            Vector3 pos = UVToWorld(NormalizeUV(bubbles[candidate].posUV));

            // Border no-spawn (keeps capture radius fully in-bounds)
            if (!IsInsideSpawnBounds(pos))
                continue;

            if (!IsFarEnoughFromActiveKnots(pos))
                continue;

            if (!IsFarEnoughFromGoals(pos))
                continue;

            id = candidate;
            worldPos = pos;
            return true;
        }

        return false;
    }

    private void SpawnKnot(float now, int id, Vector3 worldPos, NativeArray<BubbleGPU> bubbles)
    {
        float exciteT = bubbles[id].exciteT;
        float endTime = (exciteT > exciteTThreshold) ? (now + exciteT) : (now + 0.25f);

        var knot = Instantiate(knotPrefab, worldPos, Quaternion.identity, knotsParent);
        knot.Initialize(worldPos);

        knot.BindToExcitation(
            higgsField: higgsField,
            excitationId: id,
            currentExcitationEndTime: endTime,
            unclaimedSeconds: unclaimedSeconds,
            claimedSeconds: claimedSeconds,
            excitationTailSeconds: excitationTailSeconds
        );

        _knotsById[id] = new KnotRecord
        {
            id = id,
            knot = knot,
            spawnPos = worldPos,
            excitationEndTime = endTime
        };

        if (logSpawnEvents)
            Debug.Log($"[HiggsExcitationKnotManager] Spawned knot for bubble {id}", knot);
    }

    private void PruneDeadKnots()
    {
        if (_knotsById.Count == 0) return;

        float now = Time.time;
        List<int> dead = null;

        foreach (var kv in _knotsById)
        {
            if (kv.Value == null || kv.Value.knot == null)
            {
                dead ??= new List<int>(8);
                dead.Add(kv.Key);
            }
        }

        if (dead == null) return;

        for (int i = 0; i < dead.Count; i++)
        {
            int id = dead[i];

            float cd = now + respawnCooldownSeconds;

            if (_knotsById.TryGetValue(id, out var rec) && rec != null)
            {
                if (suppressRespawnUntilExcitationEnds && now < rec.excitationEndTime)
                    cd = Mathf.Max(cd, rec.excitationEndTime);
            }

            _cooldownUntil[id] = cd;
            _knotsById.Remove(id);
        }
    }

    private int CountActiveKnots()
    {
        int n = 0;
        foreach (var kv in _knotsById)
            if (kv.Value != null && kv.Value.knot != null)
                n++;
        return n;
    }

    private int ComputeAllowedKnotCount(float now)
    {
        float progress01 = ComputeProgress01(now);
        float c01 = Mathf.Clamp01(concurrencyCurve01.Evaluate(progress01));
        int allowed = Mathf.RoundToInt(Mathf.Lerp(maxKnotsEarly, maxKnotsLate, c01));
        return Mathf.Clamp(allowed, 0, Mathf.Max(maxKnotsEarly, maxKnotsLate));
    }

    private float ComputeProgress01(float now)
    {
        float t = now - (_sessionStartTime + initialKnotSpawnDelaySeconds);
        if (t <= 0f) return 0f;
        return Mathf.Clamp01(t / rampSeconds);
    }

    private bool IsFarEnoughFromActiveKnots(Vector3 pos)
    {
        float min2 = minKnotSpacing * minKnotSpacing;
        foreach (var kv in _knotsById)
        {
            var rec = kv.Value;
            if (rec == null || rec.knot == null) continue;

            Vector3 d = rec.spawnPos - pos;
            d.y = 0f;
            if (d.sqrMagnitude < min2)
                return false;
        }
        return true;
    }

    private bool IsFarEnoughFromGoals(Vector3 pos)
    {
        if (minDistanceFromGoalMouths <= 0.001f)
            return true;

        if (autoFindGoalMouthsIfEmpty && (goalMouths == null || goalMouths.Count == 0))
            AutoPopulateGoalMouths();

        if (goalMouths == null || goalMouths.Count == 0)
            return true;

        float min2 = minDistanceFromGoalMouths * minDistanceFromGoalMouths;

        for (int i = 0; i < goalMouths.Count; i++)
        {
            if (goalMouths[i] == null) continue;

            Vector3 g = goalMouths[i].transform.position;
            Vector3 d = pos - g;
            d.y = 0f;

            if (d.sqrMagnitude < min2)
                return false;
        }

        return true;
    }

    private void AutoPopulateGoalMouths()
    {
        goalMouths = new List<SymmetryKnotGoalMouth>(
            FindObjectsOfType<SymmetryKnotGoalMouth>(includeInactive: true)
        );
    }

    private Vector2 NormalizeUV(Vector2 uv)
    {
        if (swapUV) uv = new Vector2(uv.y, uv.x);
        if (invertU) uv.x = 1f - uv.x;
        if (invertV) uv.y = 1f - uv.y;
        return uv;
    }

    private Vector3 UVToWorld(Vector2 uv)
    {
        Vector2 size = higgsField != null ? higgsField.WorldSizeXZ : new Vector2(40f, 24f);
        if (size.sqrMagnitude < 0.0001f)
            size = new Vector2(40f, 24f);

        Vector3 center = higgsField != null ? higgsField.transform.position : Vector3.zero;

        float x = center.x + (uv.x - 0.5f) * size.x;
        float z = center.z + (uv.y - 0.5f) * size.y;

        return new Vector3(x, arenaY, z);
    }

    private float GetBorderInsetWorld()
    {
        float inset = knotCaptureRadiusForBounds + Mathf.Max(0f, borderPaddingWorld);
        if (borderOverrideWorld > 0f)
            inset = borderOverrideWorld;
        return Mathf.Max(0f, inset);
    }

    private bool IsInsideSpawnBounds(Vector3 pos)
    {
        if (higgsField == null) return true;

        Vector2 size = higgsField.WorldSizeXZ;
        Vector3 center = higgsField.transform.position;

        float halfX = size.x * 0.5f;
        float halfZ = size.y * 0.5f;

        float inset = GetBorderInsetWorld();

        float dx = Mathf.Abs(pos.x - center.x);
        float dz = Mathf.Abs(pos.z - center.z);

        // If inset is too large for the arena, don't hard-fail; just allow spawns.
        if (halfX - inset <= 0f || halfZ - inset <= 0f)
            return true;

        return (dx <= (halfX - inset)) && (dz <= (halfZ - inset));
    }

    private void OnDisable()
    {
        if (higgsField != null)
            higgsField.ClearGameplayExcitationRateOverride();
    }
}
