using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(600)]
[DisallowMultipleComponent]
public class HiggsExcitationKnotManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HiggsFieldGPU higgsField;
    [SerializeField] private SymmetryKnotController knotPrefab;
    [SerializeField] private Transform knotsParent; // optional

    [Header("Readback")]
    [SerializeField, Range(0.02f, 0.5f)] private float readbackInterval = 0.10f;
    [SerializeField] private float exciteTThreshold = 0.01f;

    [Header("Spawn Mapping")]
    [SerializeField] private float arenaY = 0f;
    [SerializeField] private bool swapUV = false;
    [SerializeField] private bool invertU = false;
    [SerializeField] private bool invertV = false;

    [Header("Concurrency Ramp")]
    [SerializeField] private int maxKnotsEarly = 1;
    [SerializeField] private int maxKnotsLate = 8;
    [SerializeField] private float rampSeconds = 600f;

    [Header("Spacing / Cooldown")]
    [SerializeField] private float minKnotSpacing = 5f;
    [SerializeField] private float respawnCooldownSeconds = 2.0f;
    [SerializeField] private bool suppressRespawnUntilExcitationEnds = true;

    [Header("Knot Timing (stable time, not including intro/outro)")]
    [SerializeField] private float unclaimedSeconds = 4.0f;
    [SerializeField] private float claimedSeconds = 10.0f;

    [Tooltip("Seconds AFTER knot outro before excitation dies (tail).")]
    [SerializeField] private float excitationTailSeconds = 0.75f;

    [Header("Debug")]
    [SerializeField] private bool logSpawnEvents = false;

    [StructLayout(LayoutKind.Sequential)]

private struct BubbleGPU
{
    public Vector2 posUV;
    public Vector2 velUV;
    public float amp;
    public float radius;
    public float phase;
    public float exciteT;

    // NEW (matches compute)
    public float exciteAge;
    public float pad0;

    public Vector2 pad;
}

    private struct Excitation
    {
        public int id;
        public Vector3 worldPos;
        public float remaining;
    }

    private class KnotRecord
    {
        public int id;
        public SymmetryKnotController knot;
        public Vector3 spawnPos;
        public float excitationEndTime; // last known
    }

    private readonly Dictionary<int, KnotRecord> _knotsById = new();
    private readonly Dictionary<int, float> _cooldownUntil = new();
    private readonly List<Excitation> _scratch = new();

    private float _sessionStartTime;
    private float _nextReadbackTime;
    private bool _pending;

    private void Start()
    {
        _sessionStartTime = Time.time;
        if (knotsParent == null) knotsParent = transform;
    }

    private void LateUpdate()
    {
        PruneDeadKnots();

        if (higgsField == null || knotPrefab == null)
            return;

        if (_pending) return;
        if (Time.time < _nextReadbackTime) return;

        var buffer = higgsField.BubbleBuffer;
        if (buffer == null) return;

        _pending = true;
        _nextReadbackTime = Time.time + readbackInterval;

        AsyncGPUReadback.Request(buffer, OnReadback);
    }

    private void OnReadback(AsyncGPUReadbackRequest req)
    {
        _pending = false;
        if (!this || !enabled) return;
        if (req.hasError) return;

        float now = Time.time;

        var data = req.GetData<BubbleGPU>();
        _scratch.Clear();

        for (int i = 0; i < data.Length; i++)
        {
            float tRem = data[i].exciteT;
            if (tRem <= exciteTThreshold) continue;

            Vector2 uv = data[i].posUV;
            if (swapUV) uv = new Vector2(uv.y, uv.x);
            if (invertU) uv.x = 1f - uv.x;
            if (invertV) uv.y = 1f - uv.y;

            _scratch.Add(new Excitation
            {
                id = i,
                worldPos = UVToWorld(uv),
                remaining = tRem
            });
        }

        // Update existing knot records with latest excitation end time
        for (int i = 0; i < _scratch.Count; i++)
        {
            var ex = _scratch[i];
            float endTime = now + ex.remaining;

            if (_knotsById.TryGetValue(ex.id, out var rec) && rec != null && rec.knot != null)
            {
                rec.excitationEndTime = endTime;
                rec.knot.UpdateExcitationEndTime(endTime); // controller will max() it internally
            }
        }

        int allowed = ComputeAllowedKnots(now);
        TrySpawn(now, allowed);
    }

    private void TrySpawn(float now, int allowed)
    {
        int active = 0;
        foreach (var kv in _knotsById)
            if (kv.Value != null && kv.Value.knot != null)
                active++;

        int capacity = Mathf.Max(0, allowed - active);
        if (capacity <= 0) return;

        // Prefer longer remaining
        _scratch.Sort((a, b) => b.remaining.CompareTo(a.remaining));

        for (int i = 0; i < _scratch.Count && capacity > 0; i++)
        {
            var ex = _scratch[i];

            if (_knotsById.ContainsKey(ex.id))
                continue;

            if (_cooldownUntil.TryGetValue(ex.id, out float cd) && now < cd)
                continue;

            if (!IsFarEnough(ex.worldPos))
                continue;

            Spawn(now, ex);
            capacity--;
        }
    }

    private void Spawn(float now, Excitation ex)
    {
        float endTime = now + ex.remaining;

        var knot = Instantiate(knotPrefab, ex.worldPos, Quaternion.identity, knotsParent);
        knot.Initialize(ex.worldPos);

        // Bind with stable lifetimes (controller handles intro/outro offsets + excitation extension)
        knot.BindToExcitation(
            higgsField,
            ex.id,
            endTime,
            unclaimedSeconds,
            claimedSeconds,
            excitationTailSeconds
        );

        _knotsById[ex.id] = new KnotRecord
        {
            id = ex.id,
            knot = knot,
            spawnPos = ex.worldPos,
            excitationEndTime = endTime
        };

        if (logSpawnEvents)
            Debug.Log($"[HiggsExcitationKnotManager] Spawned knot for excitation {ex.id}", knot);
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

            // Basic cooldown
            float cd = now + respawnCooldownSeconds;

            // Optional: suppress respawn until the original excitation ends
            if (suppressRespawnUntilExcitationEnds && _knotsById.TryGetValue(id, out var rec) && rec != null)
                cd = Mathf.Max(cd, rec.excitationEndTime);

            _cooldownUntil[id] = cd;
            _knotsById.Remove(id);
        }
    }

    private int ComputeAllowedKnots(float now)
    {
        float t = now - _sessionStartTime;
        float r01 = (rampSeconds <= 0.001f) ? 1f : Mathf.Clamp01(t / rampSeconds);
        return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(maxKnotsEarly, maxKnotsLate, r01)), 0, Mathf.Max(maxKnotsEarly, maxKnotsLate));
    }

    private bool IsFarEnough(Vector3 pos)
    {
        float min2 = minKnotSpacing * minKnotSpacing;

        foreach (var kv in _knotsById)
        {
            var rec = kv.Value;
            if (rec == null || rec.knot == null) continue;
            if ((rec.spawnPos - pos).sqrMagnitude < min2) return false;
        }

        return true;
    }

    private Vector3 UVToWorld(Vector2 uv)
    {
        Vector2 size = higgsField != null ? higgsField.WorldSizeXZ : new Vector2(40f, 24f);
        Vector3 center = higgsField != null ? higgsField.transform.position : Vector3.zero;

        float x = center.x + (uv.x - 0.5f) * size.x;
        float z = center.z + (uv.y - 0.5f) * size.y;

        return new Vector3(x, arenaY, z);
    }
}
