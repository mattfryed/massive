using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage-owned controller for the MAGNETOSPHERE “Solar Storm” anomaly:
/// - Periodic storms from random sides
/// - Safe zone shrinks each storm (magnetosphere “weakens”)
/// - VectorField3D warps visually (solar wind / reconnection)
/// - VectorGridGPU gets storm-front pushes
/// - Players outside safe zone drain mass rapidly during impact
///
/// Recommended AnomalyDefinition setup:
/// - minStartDelay == maxStartDelay == telegraphDuration
/// - minDuration    == maxDuration    == impactDuration
/// - arenaMode: Normal (don’t pause/ghost players)
/// - participantSelection: AllPlayers
/// </summary>
public class MagnetosphereStormController : MonoBehaviour
{
    public enum StormPhase { Disabled, Idle, Telegraph, Impact, Recovery }

    [Header("Scene Refs")]
    [SerializeField] private VectorGridGPU grid;
    [SerializeField] private VectorField3D magnetosphere;
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private AnomalyManager anomalyManager;
    [SerializeField] private AnomalyDefinition stormAnomaly;

    [Header("Schedule")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private float initialDelay = 8f;
    [SerializeField] private Vector2 idleIntervalRange = new Vector2(10f, 18f);
    [SerializeField] private float telegraphDuration = 3f;
    [SerializeField] private float impactDuration = 8f;
    [SerializeField] private float recoveryDuration = 1.25f;
    [SerializeField] private bool includeDiagonalDirections = false;

    [Header("Progression")]
    [Min(1)] public int stormsUntilMinimumSafe = 7;           // how many storms to reach minSafeAreaFrac
    [Range(0.05f, 0.95f)] public float maxSafeAreaFrac = 0.88f; // storm #1 safe fraction (85–90% safe)
    [Range(0.05f, 0.95f)] public float minSafeAreaFrac = 0.12f; // late-game safe fraction (10–15% safe)
    public AnimationCurve safeProgressCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Range(0f, 1f)] public float minStormIntensity01 = 0.15f;
    [Range(0f, 1f)] public float maxStormIntensity01 = 1.00f;
    public AnimationCurve intensityProgressCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Storm Envelope (within one storm)")]
    [Tooltip("Telegraph visual envelope (0..1 over telegraph).")]
    public AnimationCurve telegraphEnvelope = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Impact envelope (0..1 over impact). Default flat at 1.")]
    public AnimationCurve impactEnvelope = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 1));

    [Tooltip("Recovery envelope (0..1 over recovery). 1 -> 0.")]
    public AnimationCurve recoveryEnvelope = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Safe Zone Shape (magnetopause distortion)")]
    [Tooltip("If true, safe zone center follows the magnetosphere object projected into grid-local space.")]
    public bool centerSafeZoneOnMagnetosphere = true;

    public Vector2 manualSafeCenterLocal = Vector2.zero;

    [Range(0f, 0.9f)] public float daySideSquash = 0.35f;     // windward compression at peak intensity
    [Range(0f, 2.0f)] public float tailSideStretch = 0.65f;   // leeward stretch at peak intensity
    [Range(0f, 0.8f)] public float lateralSquash = 0.10f;     // squeeze perpendicular axis slightly
    [Range(0.0f, 0.5f)] public float sideBlendWidthN = 0.12f; // blend width along storm axis (normalized)

    [Header("Hazard (mass drain outside safe zone)")]
    [Tooltip("massScore drain per second at CurrentIntensity01 = 1. (massScore is 0..1 in your PlayerControllerScript)")]
    public float drainPerSecondAtMax = 0.35f;

    [Tooltip("If true, increases drain on the windward side of the magnetosphere.")]
    public bool windwardBias = true;

    [Range(0f, 1.5f)] public float windwardBiasStrength = 0.5f;

    [Header("VectorGrid push (visual storm front)")]
    public bool pushVectorGrid = true;
    public int stormFrontForceCount = 5;
    public float gridTelegraphStrength = 0.8f;
    public float gridImpactStrength = 1.6f;
    [Range(0.1f, 2f)] public float gridForceRadiusFrac = 0.45f;
    [Range(0f, 0.9f)] public float gridForceInnerFrac = 0.2f;
    [Range(0f, 1f)] public float gridFrontJitterFrac = 0.08f; // wiggle along the edge

    [Header("Magnetosphere warp (VectorField3D)")]
    public bool warpMagnetosphere = true;
    [Range(0f, 3f)] public float windWeightAtMax = 2.0f;
    [Range(0f, 2f)] public float curlWeightBoostAtMax = 0.35f;
    [Range(0f, 1f)] public float dipoleWeightAtMin = 0.35f;
    public float spinBoostDegPerSecAtMax = 1.0f;

    // -------------------- Runtime state (readable by minigame UI) --------------------

    public StormPhase Phase { get; private set; } = StormPhase.Disabled;
    public int StormsCompleted => _stormsCompleted;
    public Vector2 StormDirLocal2D => _stormDirLocal;
    public Vector3 StormDirWorld => _stormDirWorld;
    public float StormProgress01 => _stormProgress01;          // 0 first storm ... 1 at min-safe
    public float Health01 => 1f - _stormProgress01;            // simple inverse for “magnetosphere health”
    public float SafeAreaFrac => _safeAreaFrac;
    public float SafeRadiusScale => _safeRadiusScale;
    public float BaseIntensity01 => _baseIntensity01;          // intensity for this storm (progression)
    public float CurrentIntensity01 => _currentIntensity01;    // includes phase envelope
    public float PhaseTime01 => _phaseDuration > 0f ? Mathf.Clamp01((Time.time - _phaseStartTime) / _phaseDuration) : 1f;
    public float PhaseTimeRemaining => Mathf.Max(0f, _phaseDuration - (Time.time - _phaseStartTime));
    public Vector2 SafeCenterLocal2D => _safeCenterLocal;

    public event Action<StormPhase> OnPhaseChanged;

    // -------------------- Internals --------------------

    Coroutine _loop;
    int _stormsCompleted;

    float _stormProgress01;
    float _safeAreaFrac;
    float _safeRadiusScale;
    float _baseIntensity01;
    float _currentIntensity01;

    Vector2 _stormDirLocal = Vector2.right;
    Vector3 _stormDirWorld = Vector3.right;
    Vector2 _safeCenterLocal;

    float _phaseStartTime;
    float _phaseDuration;

    // Magnetosphere baselines (so we can non-destructively override)
    float _vfBaseWindWeight;
    Vector3 _vfBaseWindDir;
    float _vfBaseCurlWeight;
    float _vfBaseDipoleWeight;
    bool  _vfBaseSpin;
    float _vfBaseSpinDegPerSec;

    // direction sets
    static readonly Vector2[] _cardinalDirs =
    {
        Vector2.right, Vector2.left, Vector2.up, Vector2.down
    };

    static readonly Vector2[] _diagDirs =
    {
        (Vector2.right + Vector2.up).normalized,
        (Vector2.right + Vector2.down).normalized,
        (Vector2.left  + Vector2.up).normalized,
        (Vector2.left  + Vector2.down).normalized,
    };

    void Reset()
    {
        AutoWireIfMissing();
    }

    void Awake()
    {
        AutoWireIfMissing();
        CacheMagnetosphereBaselines();
    }

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (autoStart) StartStorms();
    }

    void OnDisable()
    {
        StopStorms(restoreVisuals: true);
    }

    void AutoWireIfMissing()
    {
        if (!grid)          grid = FindFirstObjectByType<VectorGridGPU>();
        if (!magnetosphere) magnetosphere = FindFirstObjectByType<VectorField3D>();
        if (!playerManager) playerManager = FindFirstObjectByType<PlayerManager>();
        if (!anomalyManager) anomalyManager = FindFirstObjectByType<AnomalyManager>();
    }

    void CacheMagnetosphereBaselines()
    {
        if (!magnetosphere) return;

        _vfBaseWindWeight   = magnetosphere.windWeight;
        _vfBaseWindDir = magnetosphere.windDir;
        _vfBaseCurlWeight   = magnetosphere.curlWeight;
        _vfBaseDipoleWeight = magnetosphere.dipoleWeight;
        _vfBaseSpin         = magnetosphere.spin;
        _vfBaseSpinDegPerSec = magnetosphere.spinDegPerSec;
    }

    // -------------------- Public control --------------------

    public void StartStorms()
    {
        if (_loop != null) return;
        Phase = StormPhase.Idle;
        SetPhase(StormPhase.Idle, 0f);
        _loop = StartCoroutine(StormLoop());
    }

    public void StopStorms(bool restoreVisuals)
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }

        SetPhase(StormPhase.Disabled, 0f);

        if (restoreVisuals)
        {
            RestoreMagnetosphereBaselines();
        }
    }

    public void ResetProgression()
    {
        _stormsCompleted = 0;
        RecomputeProgressionForNextStorm();
    }

    public bool IsPointSafeWorld(Vector3 worldPos)
    {
        if (!grid) return true;
        Vector2 pLocal2 = WorldToGridLocal2D(worldPos);
        return IsPointSafeLocal2D(pLocal2);
    }

    // -------------------- Main loop --------------------

    IEnumerator StormLoop()
    {
        RecomputeProgressionForNextStorm();

        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            // Idle wait
            float idleWait = UnityEngine.Random.Range(idleIntervalRange.x, idleIntervalRange.y);
            SetPhase(StormPhase.Idle, idleWait);
            yield return new WaitForSeconds(idleWait);

            // Start storm (telegraph)
            BeginStormTelegraph();

            // Anomaly wrapper (warning + minigame spawn under UI)
            TriggerStormAnomalyWrapper();

            // Telegraph
            yield return new WaitForSeconds(_phaseDuration);

            // Impact
            BeginStormImpact();
            yield return new WaitForSeconds(_phaseDuration);

            // Recovery
            BeginStormRecovery();
            yield return new WaitForSeconds(_phaseDuration);

            // End storm
            EndStorm();

            // Progression step for next storm
            _stormsCompleted++;
            RecomputeProgressionForNextStorm();
        }
    }

    void BeginStormTelegraph()
    {
        PickNewStormDirection();
        SetStormWindDirOnMagnetosphere();
        SetPhase(StormPhase.Telegraph, Mathf.Max(0.05f, telegraphDuration));
    }

    void BeginStormImpact()
    {
        SetPhase(StormPhase.Impact, Mathf.Max(0.05f, impactDuration));
    }

    void BeginStormRecovery()
    {
        SetPhase(StormPhase.Recovery, Mathf.Max(0.05f, recoveryDuration));
    }

    void EndStorm()
    {
        SetPhase(StormPhase.Idle, 0f);
        RestoreMagnetosphereBaselines();
    }

    void SetPhase(StormPhase phase, float duration)
    {
        Phase = phase;
        _phaseStartTime = Time.time;
        _phaseDuration = duration;
        OnPhaseChanged?.Invoke(phase);
    }

    void TriggerStormAnomalyWrapper()
    {
        if (!anomalyManager || !stormAnomaly || !playerManager) return;

        // Force duration to match our impact window (minigame should Complete() when done)
        anomalyManager.TriggerAnomaly(stormAnomaly, playerManager.ActivePlayers, forcedDuration: impactDuration);
    }

    // -------------------- Per-frame work (visuals + hazard) --------------------

    void Update()
    {
        if (!Application.isPlaying) return;
        if (Phase == StormPhase.Disabled) return;

        UpdateSafeCenterLocal();

        // Current intensity includes phase envelope, but base intensity is per-storm progression.
        float env = GetPhaseEnvelope01();
        _currentIntensity01 = Mathf.Clamp01(_baseIntensity01 * env);

        if (warpMagnetosphere)
            ApplyMagnetosphereOverrides(_currentIntensity01);

        if (pushVectorGrid)
            ApplyGridStormFront(_currentIntensity01);

        if (Phase == StormPhase.Impact)
            ApplyStormHazard(_currentIntensity01, Time.deltaTime);
    }

    float GetPhaseEnvelope01()
    {
        float t01 = PhaseTime01;

        switch (Phase)
        {
            case StormPhase.Telegraph:
                return Mathf.Clamp01(telegraphEnvelope.Evaluate(t01)) * 0.35f; // telegraph is intentionally weaker
            case StormPhase.Impact:
                return Mathf.Clamp01(impactEnvelope.Evaluate(t01));
            case StormPhase.Recovery:
                return Mathf.Clamp01(recoveryEnvelope.Evaluate(t01));
            default:
                return 0f;
        }
    }

    // -------------------- Progression math --------------------

    void RecomputeProgressionForNextStorm()
    {
        // stormsUntilMinimumSafe == 1 means “immediately min-safe”
        float denom = Mathf.Max(1, stormsUntilMinimumSafe - 1);
        float raw = (_stormsCompleted <= 0) ? 0f : (_stormsCompleted / denom);
        _stormProgress01 = Mathf.Clamp01(raw);

        float safeT = Mathf.Clamp01(safeProgressCurve.Evaluate(_stormProgress01));
        _safeAreaFrac = Mathf.Lerp(maxSafeAreaFrac, minSafeAreaFrac, safeT);

        // area -> radius scale (area ∝ r^2), for an ellipse normalized to arena extents
        _safeRadiusScale = Mathf.Sqrt(Mathf.Clamp(_safeAreaFrac, 0.001f, 0.999f));

        float intT = Mathf.Clamp01(intensityProgressCurve.Evaluate(_stormProgress01));
        _baseIntensity01 = Mathf.Lerp(minStormIntensity01, maxStormIntensity01, intT);
    }

    // -------------------- Direction + coordinate helpers --------------------

    void PickNewStormDirection()
    {
        int whichSet = includeDiagonalDirections ? UnityEngine.Random.Range(0, 2) : 0;

        if (whichSet == 0)
            _stormDirLocal = _cardinalDirs[UnityEngine.Random.Range(0, _cardinalDirs.Length)];
        else
            _stormDirLocal = _diagDirs[UnityEngine.Random.Range(0, _diagDirs.Length)];

        // World dir used mostly for debug / optional future FX alignment.
        if (grid)
        {
            var w = grid.transform.TransformDirection(new Vector3(_stormDirLocal.x, _stormDirLocal.y, 0f));
            _stormDirWorld = (w.sqrMagnitude > 1e-6f) ? w.normalized : Vector3.right;
        }
        else
        {
            _stormDirWorld = new Vector3(_stormDirLocal.x, _stormDirLocal.y, 0f).normalized;
        }
    }

    Vector2 WorldToGridLocal2D(Vector3 worldPos)
    {
        if (!grid) return new Vector2(worldPos.x, worldPos.z);

        Vector3 lp = grid.transform.InverseTransformPoint(worldPos);
        return new Vector2(lp.x, lp.y);
    }

    void UpdateSafeCenterLocal()
    {
        if (!grid)
        {
            _safeCenterLocal = Vector2.zero;
            return;
        }

        if (centerSafeZoneOnMagnetosphere && magnetosphere)
        {
            Vector3 lp = grid.transform.InverseTransformPoint(magnetosphere.transform.position);
            _safeCenterLocal = new Vector2(lp.x, lp.y);
        }
        else
        {
            _safeCenterLocal = manualSafeCenterLocal;
        }
    }

    // -------------------- Safe zone test (magnetopause ellipse) --------------------

    bool IsPointSafeLocal2D(Vector2 pLocal)
    {
        if (!grid) return true;

        Vector2 half = grid.size * 0.5f;
        half.x = Mathf.Max(0.001f, half.x);
        half.y = Mathf.Max(0.001f, half.y);

        Vector2 rel = pLocal - _safeCenterLocal;

        // normalize to arena extents
        Vector2 relN = new Vector2(rel.x / half.x, rel.y / half.y);

        Vector2 d = (_stormDirLocal.sqrMagnitude > 1e-6f) ? _stormDirLocal.normalized : Vector2.right;
        Vector2 p = new Vector2(-d.y, d.x);

        float u = Vector2.Dot(relN, d);   // along storm axis
        float v = Vector2.Dot(relN, p);   // perpendicular

        // Shape intensity follows currentIntensity (storm “hits” -> more squash/stretch)
        float shapeI = Mathf.Clamp01(_currentIntensity01);

        // Smoothly blend between day-side (windward) and tail-side radii
        float bw = Mathf.Max(1e-4f, sideBlendWidthN);
        float s = Mathf.SmoothStep(-bw, bw, u); // u<0 => ~0 (windward), u>0 => ~1 (tail)

        float rAlong = _safeRadiusScale *
                       Mathf.Lerp(1f - daySideSquash * shapeI,
                                  1f + tailSideStretch * shapeI, s);

        float rPerp  = _safeRadiusScale *
                       Mathf.Max(0.05f, 1f - lateralSquash * shapeI);

        rAlong = Mathf.Max(0.01f, rAlong);
        rPerp  = Mathf.Max(0.01f, rPerp);

        float eq = (u * u) / (rAlong * rAlong) + (v * v) / (rPerp * rPerp);
        return eq <= 1f;
    }

    // -------------------- Hazard (mass drain) --------------------

    void ApplyStormHazard(float intensity01, float dt)
    {
        if (!playerManager) return;
        if (intensity01 <= 0f) return;

        var players = playerManager.ActivePlayers;
        if (players == null) return;

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (!p) continue;
            if (!p.isActive) continue;
            if (p.temporarilyEliminated) continue;

            bool safe = IsPointSafeWorld(p.transform.position);
            if (safe) continue;

            float drain = drainPerSecondAtMax * intensity01 * dt;

            if (windwardBias && grid)
            {
                // windward = side facing into incoming storm => u negative
                Vector2 pl = WorldToGridLocal2D(p.transform.position);

                Vector2 half = grid.size * 0.5f;
                Vector2 rel = pl - _safeCenterLocal;
                Vector2 relN = new Vector2(rel.x / Mathf.Max(0.001f, half.x),
                                           rel.y / Mathf.Max(0.001f, half.y));

                Vector2 d = (_stormDirLocal.sqrMagnitude > 1e-6f) ? _stormDirLocal.normalized : Vector2.right;
                float u = Vector2.Dot(relN, d);

                float windward01 = Mathf.Clamp01((-u) / Mathf.Max(0.05f, _safeRadiusScale));
                drain *= (1f + windwardBiasStrength * windward01);
            }


            p.ApplyExternalMassDelta(-drain, allowDeath: true);


        }
    }

    // -------------------- VectorGrid storm-front forces --------------------

    void ApplyGridStormFront(float intensity01)
    {
        if (!grid) return;

        // Only push during telegraph/impact/recovery (not idle)
        if (Phase != StormPhase.Telegraph && Phase != StormPhase.Impact && Phase != StormPhase.Recovery)
            return;

        float phaseStrength =
            (Phase == StormPhase.Telegraph) ? gridTelegraphStrength :
            (Phase == StormPhase.Impact)    ? gridImpactStrength :
                                             (gridImpactStrength * 0.5f);

        float strength = phaseStrength * intensity01;
        if (strength <= 0f) return;

        Vector2 half2 = grid.size * 0.5f;
        float halfX = Mathf.Max(0.001f, half2.x);
        float halfY = Mathf.Max(0.001f, half2.y);

        Vector2 dir2 = (_stormDirLocal.sqrMagnitude > 1e-6f) ? _stormDirLocal.normalized : Vector2.right;

        // Incoming edge is opposite of dir sign (dir points INTO arena).
        Vector3 edgeBase = Vector3.zero;

        bool xDominant = Mathf.Abs(dir2.x) >= Mathf.Abs(dir2.y);
        if (xDominant)
        {
            edgeBase.x = -Mathf.Sign(dir2.x) * halfX * 0.98f;
            edgeBase.y = 0f;
        }
        else
        {
            edgeBase.x = 0f;
            edgeBase.y = -Mathf.Sign(dir2.y) * halfY * 0.98f;
        }

        Vector3 dir3 = new Vector3(dir2.x, dir2.y, 0f);

        // Distribute several directional forces along the storm front.
        int n = Mathf.Max(1, stormFrontForceCount);
        float radius = Mathf.Max(halfX, halfY) * gridForceRadiusFrac;

        float jitter = (gridFrontJitterFrac * intensity01) * (xDominant ? halfY : halfX);
        float jitterPhase = Time.time * 1.7f;

        for (int i = 0; i < n; i++)
        {
            float t = (n == 1) ? 0f : Mathf.Lerp(-1f, 1f, i / (float)(n - 1));

            Vector3 pos = edgeBase;

            if (xDominant)
            {
                pos.y = t * halfY;
                pos.y += Mathf.Sin(jitterPhase + i * 0.9f) * jitter;
            }
            else
            {
                pos.x = t * halfX;
                pos.x += Mathf.Sin(jitterPhase + i * 0.9f) * jitter;
            }

            var f = VectorGridGPU.MakeDirectional(pos, radius, dir3, strength, gridForceInnerFrac);
            grid.AddForce(f);
        }
    }

    // -------------------- VectorField3D overrides --------------------

void SetStormWindDirOnMagnetosphere()
{
    if (!magnetosphere || !grid) return;

    // Convert grid-local storm direction to WORLD.
    Vector3 worldDir = grid.transform.TransformDirection(new Vector3(_stormDirLocal.x, _stormDirLocal.y, 0f));
    if (worldDir.sqrMagnitude < 1e-6f) worldDir = Vector3.right;

    // Force storms to be XZ-only (top-down readability).
    worldDir = Vector3.ProjectOnPlane(worldDir, Vector3.up);
    if (worldDir.sqrMagnitude < 1e-6f) worldDir = Vector3.right;

    magnetosphere.SetWindDirWorldPlanar(worldDir.normalized, Vector3.up);
}


    void ApplyMagnetosphereOverrides(float intensity01)
    {
        if (!magnetosphere) return;

        // Wind weight ramps hard with storm
        float targetWind = Mathf.Lerp(_vfBaseWindWeight, windWeightAtMax, intensity01);
        magnetosphere.windWeight = targetWind;

        // Curl adds agitation
        magnetosphere.curlWeight = Mathf.Clamp(_vfBaseCurlWeight + curlWeightBoostAtMax * intensity01, 0f, 2f);

        // Dipole weakens with “health” (progression), and can sag slightly during peak impact
        float healthDipole = Mathf.Lerp(dipoleWeightAtMin, _vfBaseDipoleWeight, Health01);
        magnetosphere.dipoleWeight = Mathf.Lerp(healthDipole, healthDipole * 0.85f, intensity01);

        // Optional spin boost
        if (_vfBaseSpin)
            magnetosphere.spinDegPerSec = _vfBaseSpinDegPerSec + spinBoostDegPerSecAtMax * intensity01;
    }

    void RestoreMagnetosphereBaselines()
    {
        if (!magnetosphere) return;

        magnetosphere.windWeight   = _vfBaseWindWeight;
        magnetosphere.windDir = _vfBaseWindDir;
        magnetosphere.curlWeight   = _vfBaseCurlWeight;
        magnetosphere.dipoleWeight = _vfBaseDipoleWeight;

        if (_vfBaseSpin)
            magnetosphere.spinDegPerSec = _vfBaseSpinDegPerSec;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!grid) return;

        // Draw a simple “safe ellipse” gizmo (approx; magnetopause distortion is not rendered here).
        UpdateSafeCenterLocal();

        Gizmos.color = Color.white;

        Vector2 half = grid.size * 0.5f;
        int steps = 64;

        Vector3 centerW = grid.transform.TransformPoint(new Vector3(_safeCenterLocal.x, _safeCenterLocal.y, 0f));

        Vector3 prev = Vector3.zero;
        for (int i = 0; i <= steps; i++)
        {
            float a = (i / (float)steps) * Mathf.PI * 2f;
            float rx = half.x * _safeRadiusScale;
            float ry = half.y * _safeRadiusScale;

            Vector3 pLocal = new Vector3(
                _safeCenterLocal.x + Mathf.Cos(a) * rx,
                _safeCenterLocal.y + Mathf.Sin(a) * ry,
                0f
            );

            Vector3 pW = grid.transform.TransformPoint(pLocal);

            if (i > 0) Gizmos.DrawLine(prev, pW);
            prev = pW;
        }

        // storm direction
        Gizmos.color = Color.cyan;
        Vector3 dirW = StormDirWorld.normalized;
        Gizmos.DrawLine(centerW, centerW + dirW * Mathf.Max(half.x, half.y) * 0.25f);
    }
#endif

    void OnValidate()
    {
        idleIntervalRange.x = Mathf.Max(0f, idleIntervalRange.x);
        idleIntervalRange.y = Mathf.Max(idleIntervalRange.x, idleIntervalRange.y);

        telegraphDuration = Mathf.Max(0f, telegraphDuration);
        impactDuration    = Mathf.Max(0f, impactDuration);
        recoveryDuration  = Mathf.Max(0f, recoveryDuration);

        maxSafeAreaFrac = Mathf.Clamp(maxSafeAreaFrac, 0.05f, 0.95f);
        minSafeAreaFrac = Mathf.Clamp(minSafeAreaFrac, 0.05f, 0.95f);
        if (minSafeAreaFrac > maxSafeAreaFrac) minSafeAreaFrac = maxSafeAreaFrac;
    }
}
