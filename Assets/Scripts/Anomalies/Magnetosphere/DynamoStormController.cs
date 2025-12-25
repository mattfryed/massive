using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class DynamoStormController : MonoBehaviour
{
    // ============================================================
    // References
    // ============================================================

    [Header("Scene References")]
    [SerializeField] private MonoBehaviour magnetosphere;            // MagnetosphereFieldLinesGPU2D
    [SerializeField] private StormWindFlowVisualizer stormFlow;

    [Header("Players")]
    [SerializeField] private List<Transform> playerTargets = new List<Transform>(4);
    [SerializeField] private bool autoFindPlayersByTag = true;
    [SerializeField] private string playerTag = "Player";

    // ============================================================
    // Storm scheduling
    // ============================================================

    [Header("Storm Scheduling")]
    [SerializeField] private Vector2 timeBetweenStormsRange = new Vector2(8f, 16f);
    [SerializeField] private Vector2 stormTotalDurationRange = new Vector2(6f, 12f);
    [SerializeField, Range(0f, 0.49f)] private float easeInFrac = 0.20f;
    [SerializeField, Range(0f, 0.49f)] private float easeOutFrac = 0.25f;

    [Header("Storm Direction")]
    [SerializeField] private bool randomizeDirectionEachStorm = true;
    [SerializeField] private Vector3 fallbackDownstreamDir = Vector3.right;

    // ============================================================
    // Intensity ramp (linear)
    // ============================================================

    [Header("Intensity Ramp (Linear)")]
    [SerializeField, Range(0f, 1f)] private float basePeak01 = 0.35f;
    [SerializeField, Range(0f, 1f)] private float perStormIncrement01 = 0.08f;
    [SerializeField, Range(0f, 1f)] private float maxPeak01 = 1.0f;

    // ============================================================
    // Magnetosphere drive
    // ============================================================

    [Header("Magnetosphere Drive")]
    [SerializeField] private float calmPressureGain = 0.0f;
    [SerializeField] private float maxPressureGain = 1.0f;

    [Header("Magnetosphere Response Timing")]
    [Tooltip("If true, magnetosphere pressure gain stays calm briefly so the particle storm arrives first.")]
    [SerializeField] private bool delayMagnetosphereResponse = true;

    [Tooltip("Seconds after storm start before magnetosphere begins deforming.")]
    [SerializeField, Min(0f)] private float magResponseDelaySeconds = 1.25f;

    [Tooltip("Seconds to ease magnetosphere response from 0 to 1 once delay has elapsed.")]
    [SerializeField, Min(0.01f)] private float magResponseRampSeconds = 1.0f;

    [Header("Optional extra knobs (can leave off)")]
    [SerializeField] private bool scaleDaysideCompression = false;
    [SerializeField] private float calmDaysideCompression = 0.0f;
    [SerializeField] private float maxDaysideCompression = 2.0f;

    [SerializeField] private bool scaleNightsideStretch = false;
    [SerializeField] private float calmNightsideStretch = 0.0f;
    [SerializeField] private float maxNightsideStretch = 0.66f;

    [Header("Magnetosphere Internal Storm Suppression (recommended)")]
    [Tooltip("If enabled, controller will attempt to disable magnetosphere's own storm scheduler / wind drift so external control wins.")]
    [SerializeField] private bool suppressInternalMagStorms = true;

    // ============================================================
    // Storm Flow scaling
    // ============================================================

    [Header("Storm Flow Scaling")]
    [SerializeField] private bool scaleStormFlow = true;

    [Tooltip("Reinitialize storm particles on storm start (good for a clean incoming front).")]
    [SerializeField] private bool reinitParticlesOnStormStart = true;

    [SerializeField] private Vector2 speedScaleRange = new Vector2(0.6f, 1.15f);
    [SerializeField] private Vector2 jitterScaleRange = new Vector2(0.35f, 1.0f);
    [SerializeField] private Vector2 alphaScaleRange = new Vector2(0.0f, 1.0f);

    // ============================================================
    // Damage
    // ============================================================

    [Header("Damage (Outside Magnetosphere)")]
    [SerializeField] private float damagePerSecondAtFullStorm = 1.0f;
    [SerializeField, Range(1f, 60f)] private float damageTickRateHz = 10f;

    [Header("Storm Front (Damage Delay)")]
    [SerializeField] private float damageFrontThickness = 7.0f;
    [SerializeField, Range(0.05f, 2f)] private float damageFrontFeather = 0.65f;
    [SerializeField] private float damageFrontTravelPad = 6.0f;

    [Header("Playfield For Damage Travel (XZ)")]
    [SerializeField] private Vector3 playfieldCenter = Vector3.zero;
    [SerializeField] private Vector2 playfieldSizeXZ = new Vector2(30f, 16f);

    // ============================================================
    // Debug UI
    // ============================================================

    [Header("Debug UI")]
    [SerializeField] private bool showDebugUI = true;
    [SerializeField] private KeyCode toggleDebugKey = KeyCode.F8;
    [SerializeField] private Vector2 debugOffset = new Vector2(10, 10);

    // ============================================================
    // Runtime state
    // ============================================================

    public int StormCount { get; private set; }
    public bool StormActive => _stormActive;
    public float StormStrength01 => _stormStrength01;

    private bool _stormActive;
    private float _stormTimer;
    private float _stormTotal;
    private float _easeIn, _hold, _easeOut;

    private float _nextStormStartTime;
    private float _stormPeak01;
    private float _stormEnvelope01;
    private float _stormStrength01;

    private Vector3 _downstreamDir = Vector3.right;
    private float _damageAccum;

    private float _magResponse01; // 0..1 factor applied to magnetosphere deformation
    private readonly Dictionary<int, float> _stormFactorByPlayer = new Dictionary<int, float>(8);

    // ============================================================
    // Reflection caches
    // ============================================================

    // StormFlow
    private MethodInfo _miStormFlowInitParticles;
    private MethodInfo _miStormFlowEnvelopeQ;
    private FieldInfo _fiStormFlowParticleColor;
    private FieldInfo _fiStormFlowSpeedRange;
    private FieldInfo _fiStormFlowJitterStrength;
    private FieldInfo _fiStormFlowJitterAlongStrength;

    // Baselines (so scaling does NOT compound)
    private bool _stormFlowBaselineCached;
    private Color _stormFlowBaseColor;
    private Vector2 _stormFlowBaseSpeedRange;
    private float _stormFlowBaseJitter;
    private float _stormFlowBaseJitterAlong;

    // Magnetosphere fields
    private FieldInfo _fiMagPressureGain;
    private FieldInfo _fiMagSolarWindDir;
    private FieldInfo _fiMagDaysideCompression;
    private FieldInfo _fiMagNightsideStretch;

    // Optional internal suppression fields (best effort)
    private FieldInfo _fiMagStormInterval;
    private FieldInfo _fiMagStormIntensity;
    private FieldInfo _fiMagStormWarpScale;
    private FieldInfo _fiMagRandomizeWindOnStormStart;
    private FieldInfo _fiMagDriftWindWhenCalm;

    // StormFlow reflection extras for “no leftovers”
    private MethodInfo _miStormFlowSyncNow;
    private FieldInfo _fiStormFlowDownstreamOverride;

    // ============================================================
    private void Awake()
    {
        CacheMagnetosphereBindings();
        CacheStormFlowBindings();
        CacheStormFlowBaselines();

        ScheduleNextStorm(Time.time);

        if (autoFindPlayersByTag)
            RefreshPlayersByTag();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleDebugKey))
            showDebugUI = !showDebugUI;

        if (autoFindPlayersByTag && (playerTargets == null || playerTargets.Count == 0))
            RefreshPlayersByTag();

        float now = Time.time;

        if (!_stormActive)
        {
            if (now >= _nextStormStartTime)
                StartStorm(now);
        }
        else
        {
            _stormTimer += Time.deltaTime;
            UpdateEnvelopeAndStrength();
            ApplyStormToSystems(_stormStrength01);

            if (_stormTimer >= _stormTotal)
                EndStorm(now);
        }

        if (!_stormActive)
        {
            _stormEnvelope01 = 0f;
            _stormStrength01 = 0f;
            ApplyStormToSystems(0f);
        }

        // Damage tick (cheap perf win)
        float tick = (damageTickRateHz <= 0f) ? 0f : 1f / damageTickRateHz;
        _damageAccum += Time.deltaTime;

        if (tick <= 0f)
        {
            ApplyDamageTick(Time.deltaTime);
        }
        else
        {
            while (_damageAccum >= tick)
            {
                ApplyDamageTick(tick);
                _damageAccum -= tick;
            }
        }
    }

    // ============================================================
    // Scheduling
    // ============================================================

    private void ScheduleNextStorm(float now)
    {
        float min = Mathf.Max(0.01f, timeBetweenStormsRange.x);
        float max = Mathf.Max(min, timeBetweenStormsRange.y);
        _nextStormStartTime = now + UnityEngine.Random.Range(min, max);
    }

    private void StartStorm(float now)
    {
        _stormActive = true;
        _stormTimer = 0f;

        StormCount++;

        _stormPeak01 = Mathf.Clamp01(basePeak01 + perStormIncrement01 * (StormCount - 1));
        _stormPeak01 = Mathf.Min(_stormPeak01, maxPeak01);

        float tMin = Mathf.Max(0.5f, stormTotalDurationRange.x);
        float tMax = Mathf.Max(tMin, stormTotalDurationRange.y);
        _stormTotal = UnityEngine.Random.Range(tMin, tMax);

        _easeIn = _stormTotal * Mathf.Clamp01(easeInFrac);
        _easeOut = _stormTotal * Mathf.Clamp01(easeOutFrac);
        _hold = Mathf.Max(0.01f, _stormTotal - _easeIn - _easeOut);

        // Direction for this storm
        if (randomizeDirectionEachStorm)
        {
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            _downstreamDir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)).normalized;
        }
        else
        {
            _downstreamDir = fallbackDownstreamDir;
            _downstreamDir.y = 0f;
            if (_downstreamDir.sqrMagnitude < 1e-6f) _downstreamDir = Vector3.right;
            _downstreamDir.Normalize();
        }

        // Apply a “spawn strength” so the storm is visible immediately (particles),
        // while magnetosphere response is still delayed by magResponseDelaySeconds.
        float spawnStrength01 = Mathf.Clamp01(Mathf.Max(0.20f, _stormPeak01 * 0.6f));

        // Set values (direction + scaled stormFlow params). Magnetosphere uses delayed response factor.
        ApplyStormToSystems(spawnStrength01);

        // Ensure StormFlow uses the NEW direction immediately
        ForceStormFlowDirectionOverride(_downstreamDir);

        // HARD clear any previous particles/trails BEFORE spawning new ones
        ClearStormFlowNow();

        // Force sync so stormFlow recomputes flowDir from the new direction right now
        ForceStormFlowSyncNow();

        // Now spawn particles using the new direction
        if (stormFlow && reinitParticlesOnStormStart)
            TryInvokeStormFlowInit();

        // Ensure PS is playing
        if (stormFlow)
        {
            var ps = stormFlow.GetComponent<ParticleSystem>();
            if (ps && !ps.isPlaying) ps.Play(true);
        }

        // Now proceed with the true storm envelope
        UpdateEnvelopeAndStrength();
        ApplyStormToSystems(_stormStrength01);
    }

    private void EndStorm(float now)
    {
        _stormActive = false;
        _stormTimer = 0f;
        _stormEnvelope01 = 0f;
        _stormStrength01 = 0f;

        // Hard clear particles & trails so nothing leaks into next storm
        ClearStormFlowNow();

        ScheduleNextStorm(now);
    }

    private void UpdateEnvelopeAndStrength()
    {
        float t = _stormTimer;

        float env;
        if (_easeIn > 1e-4f && t < _easeIn)
            env = Smooth01(t / _easeIn);
        else if (t < _easeIn + _hold)
            env = 1f;
        else if (_easeOut > 1e-4f && t < _easeIn + _hold + _easeOut)
            env = 1f - Smooth01((t - _easeIn - _hold) / _easeOut);
        else
            env = 0f;

        _stormEnvelope01 = Mathf.Clamp01(env);
        _stormStrength01 = _stormEnvelope01 * _stormPeak01;
    }

    // ============================================================
    // Magnetosphere response timing
    // ============================================================

    private float ComputeMagnetosphereResponse01()
    {
        if (!delayMagnetosphereResponse) return 1f;
        if (!_stormActive) return 0f;

        float t = _stormTimer - magResponseDelaySeconds;
        if (t <= 0f) return 0f;

        float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, magResponseRampSeconds));
        return Smooth01(u);
    }

    // ============================================================
    // Apply to systems
    // ============================================================

    private void ApplyStormToSystems(float strength01)
    {
        float s = Mathf.Clamp01(strength01);

        _magResponse01 = ComputeMagnetosphereResponse01();
        float magS = s * _magResponse01;

        // Magnetosphere drive (delayed)
        if (magnetosphere)
        {
            if (suppressInternalMagStorms)
                SuppressMagnetosphereInternalStorms();

            TrySetField(_fiMagSolarWindDir, _downstreamDir);

            float pg = Mathf.Lerp(calmPressureGain, maxPressureGain, magS);
            TrySetField(_fiMagPressureGain, pg);

            if (scaleDaysideCompression && _fiMagDaysideCompression != null)
                TrySetField(_fiMagDaysideCompression, Mathf.Lerp(calmDaysideCompression, maxDaysideCompression, magS));

            if (scaleNightsideStretch && _fiMagNightsideStretch != null)
                TrySetField(_fiMagNightsideStretch, Mathf.Lerp(calmNightsideStretch, maxNightsideStretch, magS));
        }

        // StormFlow drive (immediate)
        if (stormFlow && scaleStormFlow)
        {
            CacheStormFlowBaselines();

            float speedMul = Mathf.Lerp(speedScaleRange.x, speedScaleRange.y, s);
            float jitterMul = Mathf.Lerp(jitterScaleRange.x, jitterScaleRange.y, s);
            float alphaMul = Mathf.Lerp(alphaScaleRange.x, alphaScaleRange.y, s);

            // Color alpha (used at spawn)
            if (_fiStormFlowParticleColor != null)
            {
                Color c = _stormFlowBaseColor;
                c.a = Mathf.Clamp01(_stormFlowBaseColor.a * alphaMul);
                _fiStormFlowParticleColor.SetValue(stormFlow, c);
            }

            // Speed range (do NOT compound)
            if (_fiStormFlowSpeedRange != null)
            {
                Vector2 scaled = new Vector2(_stormFlowBaseSpeedRange.x * speedMul, _stormFlowBaseSpeedRange.y * speedMul);
                _fiStormFlowSpeedRange.SetValue(stormFlow, scaled);
            }

            var psr = stormFlow.GetComponent<ParticleSystemRenderer>();
if (psr && psr.sharedMaterial)
{
    psr.sharedMaterial.SetFloat("_Intensity", s); // s = storm strength 0..1
}

            // Jitter (do NOT compound)
            if (_fiStormFlowJitterStrength != null)
                _fiStormFlowJitterStrength.SetValue(stormFlow, _stormFlowBaseJitter * jitterMul);

            if (_fiStormFlowJitterAlongStrength != null)
                _fiStormFlowJitterAlongStrength.SetValue(stormFlow, _stormFlowBaseJitterAlong * jitterMul);
        }
    }

    private void SuppressMagnetosphereInternalStorms()
    {
        // best-effort: set interval=0, randomizeWindOnStormStart=false, driftWindWhenCalm=false
        if (_fiMagStormInterval != null) TrySetField(_fiMagStormInterval, 0f);
        if (_fiMagRandomizeWindOnStormStart != null) TrySetField(_fiMagRandomizeWindOnStormStart, false);
        if (_fiMagDriftWindWhenCalm != null) TrySetField(_fiMagDriftWindWhenCalm, false);

        // optional: also zero internal storm intensity/warp scale
        if (_fiMagStormIntensity != null) TrySetField(_fiMagStormIntensity, 0f);
        if (_fiMagStormWarpScale != null) TrySetField(_fiMagStormWarpScale, 0f);
    }

    // ============================================================
    // Damage
    // ============================================================
private void ApplyDamageTick(float dt)
{
    if (!_stormActive) return;
    if (playerTargets == null || playerTargets.Count == 0) return;

    Vector3 downstream = _downstreamDir;
    downstream.y = 0f;
    if (downstream.sqrMagnitude < 1e-6f) downstream = Vector3.right;
    downstream.Normalize();

    Vector3 flow = GetStormFlowIncomingFlag() ? -downstream : downstream;

    float halfLen = ProjectedHalfLen(flow, playfieldSizeXZ);
    float startS = -halfLen - damageFrontTravelPad;
    float endS = halfLen + damageFrontTravelPad;

    float t01 = Mathf.Clamp01(_stormTimer / Mathf.Max(0.0001f, _stormTotal));
    float frontCenterS = Mathf.Lerp(startS, endS, t01);

    float halfT = damageFrontThickness * 0.5f;
    float feather = Mathf.Lerp(0.05f, 2.0f, damageFrontFeather);

    for (int i = 0; i < playerTargets.Count; i++)
    {
        var tr = playerTargets[i];
        if (!tr) continue;

        Vector3 pos = tr.position;
        int id = tr.GetInstanceID();

        // If player is inside magnetosphere, storm factor is 0 and no damage.
        if (!IsOutsideMagnetosphere(pos))
        {
            _stormFactorByPlayer[id] = 0f;
            continue;
        }

        // Storm front delay mask
        float along = Vector3.Dot(pos - playfieldCenter, flow);
        float dist = along - frontCenterS;

        float core = 1f - Smoothstep01(halfT, halfT * feather, Mathf.Abs(dist));
        float dmgFactor = Mathf.Clamp01(core) * _stormStrength01;

        // Cache per-player factor for other systems (like storm push)
        _stormFactorByPlayer[id] = dmgFactor;

        if (dmgFactor <= 0f)
            continue;

        float dmg = damagePerSecondAtFullStorm * dmgFactor * dt;

        // Placeholder hook
        tr.gameObject.SendMessage("ApplyStormDamage", dmg, SendMessageOptions.DontRequireReceiver);
    }
}

    private bool IsOutsideMagnetosphere(Vector3 worldPos)
    {
        if (stormFlow != null && _miStormFlowEnvelopeQ != null)
        {
            try
            {
                object[] args = { worldPos };
                float q = (float)_miStormFlowEnvelopeQ.Invoke(stormFlow, args);
                return q >= 1f;
            }
            catch { }
        }
        return true;
    }

    public float GetStormFactor01(Transform player)
{
    if (!player) return 0f;
    return _stormFactorByPlayer.TryGetValue(player.GetInstanceID(), out var v) ? v : 0f;
}

public Vector3 GetStormFlowDirWS()
{
    // same logic you use for damage: downstream + stormFlow incoming flag
    Vector3 d = _downstreamDir; d.y = 0f;
    if (d.sqrMagnitude < 1e-6f) d = Vector3.right;
    d.Normalize();
    return GetStormFlowIncomingFlag() ? -d : d;
}

    // ============================================================
    // Debug UI
    // ============================================================

    private void OnGUI()
    {
        if (!showDebugUI) return;

        float now = Time.time;
        float nextIn = Mathf.Max(0f, _nextStormStartTime - now);

        string magName = magnetosphere ? magnetosphere.GetType().Name : "(none)";
        string flowName = stormFlow ? stormFlow.name : "(none)";

        float pg = ReadFloatField(_fiMagPressureGain, magnetosphere);

        int alive = 0;
        bool psPlaying = false;
        if (stormFlow)
        {
            var ps = stormFlow.GetComponent<ParticleSystem>();
            if (ps)
            {
                alive = ps.particleCount;
                psPlaying = ps.isPlaying;
            }
        }

        GUI.color = new Color(1, 1, 1, 0.9f);
        GUILayout.BeginArea(new Rect(debugOffset.x, debugOffset.y, 440, 230), GUI.skin.box);
        GUILayout.Label($"DYNAMO STORM DEBUG  (toggle {toggleDebugKey})");
        GUILayout.Space(4);

        GUILayout.Label($"StormActive: {_stormActive}");
        GUILayout.Label($"StormCount: {StormCount}");
        GUILayout.Label($"Peak01: {_stormPeak01:0.000}  Envelope01: {_stormEnvelope01:0.000}  Strength01: {_stormStrength01:0.000}  MagResp01: {_magResponse01:0.000}");
        GUILayout.Label(_stormActive
            ? $"StormTime: {_stormTimer:0.00}/{_stormTotal:0.00}"
            : $"Next storm in: {nextIn:0.00}s");

        GUILayout.Label($"DownstreamDir: {_downstreamDir.x:0.00}, {_downstreamDir.z:0.00}");
        GUILayout.Space(6);

        GUILayout.Label($"Magnetosphere: {magName}  pressureGain={pg:0.000}");
        GUILayout.Label($"StormFlow: {flowName}  psPlaying={psPlaying}  aliveParticles={alive}");

        GUILayout.EndArea();
    }

    // ============================================================
    // Reflection + utilities
    // ============================================================

    private void CacheMagnetosphereBindings()
    {
        if (!magnetosphere) return;
        var t = magnetosphere.GetType();
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        _fiMagPressureGain = t.GetField("pressureGain", flags);
        _fiMagSolarWindDir = t.GetField("solarWindDirection", flags);

        _fiMagDaysideCompression = t.GetField("daysideCompression", flags);
        _fiMagNightsideStretch = t.GetField("nightsideStretch", flags);

        _fiMagStormInterval = t.GetField("stormInterval", flags);
        _fiMagStormIntensity = t.GetField("stormIntensity", flags);
        _fiMagStormWarpScale = t.GetField("stormWarpScale", flags);
        _fiMagRandomizeWindOnStormStart = t.GetField("randomizeWindOnStormStart", flags);
        _fiMagDriftWindWhenCalm = t.GetField("driftWindWhenCalm", flags);
    }

    private void CacheStormFlowBindings()
    {
        if (!stormFlow) return;
        var t = stormFlow.GetType();
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        _fiStormFlowParticleColor = t.GetField("particleColor", flags);
        _fiStormFlowSpeedRange = t.GetField("speedRange", flags);
        _fiStormFlowJitterStrength = t.GetField("jitterStrength", flags);
        _fiStormFlowJitterAlongStrength = t.GetField("jitterAlongStrength", flags);

        _miStormFlowInitParticles = t.GetMethod("InitializeParticles", flags);
        _miStormFlowEnvelopeQ = t.GetMethod("EnvelopeQ", flags);

        _miStormFlowSyncNow = t.GetMethod("SyncFromMagnetosphereIfNeeded", flags);
        _fiStormFlowDownstreamOverride = t.GetField("downstreamDirectionOverride", flags);
    }

    private void CacheStormFlowBaselines()
    {
        if (_stormFlowBaselineCached) return;
        if (!stormFlow) return;

        try
        {
            _stormFlowBaseColor = _fiStormFlowParticleColor != null
                ? (Color)_fiStormFlowParticleColor.GetValue(stormFlow)
                : Color.white;

            _stormFlowBaseSpeedRange = _fiStormFlowSpeedRange != null
                ? (Vector2)_fiStormFlowSpeedRange.GetValue(stormFlow)
                : new Vector2(3.5f, 10f);

            _stormFlowBaseJitter = _fiStormFlowJitterStrength != null
                ? (float)_fiStormFlowJitterStrength.GetValue(stormFlow)
                : 0.8f;

            _stormFlowBaseJitterAlong = _fiStormFlowJitterAlongStrength != null
                ? (float)_fiStormFlowJitterAlongStrength.GetValue(stormFlow)
                : 0.15f;

            _stormFlowBaselineCached = true;
        }
        catch
        {
            _stormFlowBaselineCached = false;
        }
    }

    private void TryInvokeStormFlowInit()
    {
        if (!stormFlow || _miStormFlowInitParticles == null) return;
        try { _miStormFlowInitParticles.Invoke(stormFlow, null); }
        catch { }
    }

    private bool GetStormFlowIncomingFlag()
    {
        if (!stormFlow) return false;
        try
        {
            var fi = stormFlow.GetType().GetField("flowUsesIncomingDirection", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fi != null) return (bool)fi.GetValue(stormFlow);
        }
        catch { }
        return false;
    }

    private void TrySetField(FieldInfo fi, Vector3 v)
    {
        if (!magnetosphere || fi == null) return;
        try { fi.SetValue(magnetosphere, v); } catch { }
    }

    private void TrySetField(FieldInfo fi, float v)
    {
        if (!magnetosphere || fi == null) return;
        try { fi.SetValue(magnetosphere, v); } catch { }
    }

    private void TrySetField(FieldInfo fi, bool v)
    {
        if (!magnetosphere || fi == null) return;
        try { fi.SetValue(magnetosphere, v); } catch { }
    }

    private float ReadFloatField(FieldInfo fi, MonoBehaviour target)
    {
        if (!target || fi == null) return -1f;
        try { return (float)fi.GetValue(target); } catch { return -1f; }
    }

    private void RefreshPlayersByTag()
    {
        playerTargets ??= new List<Transform>(4);
        playerTargets.Clear();

        if (string.IsNullOrEmpty(playerTag)) return;

        var gos = GameObject.FindGameObjectsWithTag(playerTag);
        for (int i = 0; i < gos.Length; i++)
            playerTargets.Add(gos[i].transform);
    }

    private static float ProjectedHalfLen(Vector3 flowDir, Vector2 fieldSizeXZ)
    {
        flowDir.y = 0f;
        if (flowDir.sqrMagnitude < 1e-6f) flowDir = Vector3.right;
        flowDir.Normalize();

        float hx = fieldSizeXZ.x * 0.5f;
        float hz = fieldSizeXZ.y * 0.5f;

        return Mathf.Abs(flowDir.x) * hx + Mathf.Abs(flowDir.z) * hz;
    }

    private static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    private static float Smoothstep01(float edge0, float edge1, float x)
    {
        if (edge1 <= edge0) return (x < edge0) ? 0f : 1f;
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    // ============================================================
    // StormFlow “no leftovers” helpers
    // ============================================================

    private void ClearStormFlowNow()
    {
        if (!stormFlow) return;
        var ps = stormFlow.GetComponent<ParticleSystem>();
        if (!ps) return;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Clear(true);
    }

    private void ForceStormFlowDirectionOverride(Vector3 downstreamDir)
    {
        if (!stormFlow || _fiStormFlowDownstreamOverride == null) return;
        try { _fiStormFlowDownstreamOverride.SetValue(stormFlow, downstreamDir); }
        catch { }
    }

    private void ForceStormFlowSyncNow()
    {
        if (!stormFlow || _miStormFlowSyncNow == null) return;
        try { _miStormFlowSyncNow.Invoke(stormFlow, null); }
        catch { }
    }
}