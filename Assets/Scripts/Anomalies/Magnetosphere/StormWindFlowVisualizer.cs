using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(ParticleSystem))]
public class StormWindFlowVisualizer : MonoBehaviour
{
    // ============================================================
    // References / Sync
    // ============================================================

    [Header("Source / Sync")]
    [SerializeField] private MonoBehaviour magnetosphereSource;
    [SerializeField] private bool syncFromMagnetosphere = true;
    [SerializeField] private bool useMagnetosphereEnvelopeStrength = true;

    [Tooltip("Magnetosphere script wind is DOWNSTREAM (tail direction). If true, particles flow INCOMING (opposite).")]
    [SerializeField] private bool flowUsesIncomingDirection = true;

    [Tooltip("Fallback downstream direction if sync fails (tail direction).")]
    [SerializeField] private Vector3 downstreamDirectionOverride = Vector3.right;

    [Tooltip("Fallback envelope strength if sync fails.")]
    [SerializeField] private float envelopeStrengthOverride = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool debugDrawPlayfieldRect = true;
    [SerializeField] private bool forceRendererEnabled = true;

    // ============================================================
    // Playfield mask / wrapping
    // ============================================================

    [Header("Playfield Mask (XZ)")]
    [SerializeField] private Vector3 playfieldCenter = Vector3.zero;
    [SerializeField] private Vector2 playfieldSizeXZ = new Vector2(30f, 16f);
    [SerializeField] private float planeY = 0f;
    [SerializeField] private float spawnMargin = 3f;
    [SerializeField] private bool wrapParticles = true;

    // ============================================================
    // Particle controls
    // ============================================================

    [Header("Upstream Spawn")]
    [SerializeField] private float upstreamSpawnBand = 1.5f;

    [Header("Particle Controls")]
    [SerializeField, Min(1)] private int maxParticles = 800;

    [Tooltip("Finite particle lifetime (seconds). Also applied to emitted replacements.")]
    [SerializeField, Range(0.5f, 120f)] private float particleLifetime = 25f;

    [SerializeField] private Vector2 sizeRange = new Vector2(0.02f, 0.08f);
    [SerializeField] private Vector2 speedRange = new Vector2(3.5f, 10.0f);
    [SerializeField] private Color particleColor = new Color(1f, 1f, 1f, 1f);

    [Header("Trails (Tail)")]
    [SerializeField] private bool enableTrails = true;
    [SerializeField] private Vector2 tailLifetimeRange = new Vector2(0.10f, 0.60f);

    // ============================================================
    // Jitter / motion noise
    // ============================================================

    [Header("Jitter / Noise")]
    [SerializeField] private float jitterStrength = 0.8f;
    [SerializeField] private float jitterAlongStrength = 0.15f;
    [SerializeField] private float jitterFrequency = 0.8f;

    [Tooltip("Scales lateral jitter relative to forward speed. Keep small to avoid criss-crossing.")]
    [SerializeField, Range(0f, 1f)] private float lateralJitterSpeedFraction = 0.12f;

    [Tooltip("How quickly particle velocity chases the desired flow direction (higher = snappier, lower = smoother).")]
    [SerializeField, Range(0.1f, 30f)] private float velocityResponse = 6.0f;

    // ============================================================
    // Boundary avoidance behavior (OUTSIDE ONLY)
    // ============================================================

    [Header("Magnetopause Avoidance (stay OUTSIDE)")]
    [SerializeField, Range(0.01f, 0.6f)] private float approachBand = 0.18f;
    [SerializeField, Range(0f, 3f)] private float tangentSteerStrength = 1.25f;
    [SerializeField, Range(0f, 3f)] private float outwardPush = 0.65f;
    [SerializeField, Range(0f, 0.25f)] private float boundaryClearance = 0.03f;
    [SerializeField, Range(0.5f, 6f)] private float approachPower = 2.0f;

    [Header("Option 3: Calm Near Boundary")]
    [Tooltip("Multiplies velocity near the boundary to reduce high-frequency buzzing. 1=no change.")]
    [SerializeField, Range(0.05f, 1f)] private float boundaryCalmMultiplier = 0.25f;

    // ============================================================
    // Update rate
    // ============================================================

    [Header("Update Rate")]
    [SerializeField] private float particleUpdateRateHz = 60f;

    [Tooltip("Caps how many fixed sim substeps can run in one frame (prevents spiral-of-death on hitches).")]
    [SerializeField, Range(1, 8)] private int maxSubstepsPerFrame = 2;

    [Header("Boundary Gating (performance)")]
    [Tooltip("Only run expensive envelope math when particle radius is above this fraction of magnetosphereRadius.")]
    [SerializeField, Range(0.1f, 0.95f)] private float boundaryGateRadiusFrac = 0.6f;

    // ============================================================
    // Bow shock
    // ============================================================

    [Header("Bow Shock")]
    [SerializeField] private bool enableBowShock = true;
    [SerializeField] private float bowShockOffset = 0.8f;
    [SerializeField] private float bowShockWidth = 0.08f;
    [SerializeField] private Color bowShockColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField, Range(16, 256)] private int bowShockSamples = 96;
    [SerializeField] private float bowShockUpdateRateHz = 6f;
    [SerializeField] private bool bowShockWindwardOnly = true;

    // --- Public API (gameplay queries) ---
public float GetEnvelopeQ(Vector3 worldPos) => EnvelopeQ(worldPos);
public bool IsOutsideMagnetopause(Vector3 worldPos) => GetEnvelopeQ(worldPos) >= 1f;

// Also helpful so DynamoStormController doesn't reflect this every time:
public bool FlowUsesIncomingDirection => flowUsesIncomingDirection;


    // ============================================================
    // Internal state
    // ============================================================

    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;

    private float accum;
    private float step;

    private float bowAccum;
    private float bowStep;

    private LineRenderer bowShockLR;

    // Magnetopause axis (DOWNSTREAM) and particle flow direction
    private Vector3 windAxisDownstream = Vector3.right; // envelope axis
    private Vector3 flowDir = Vector3.right;            // particles move along this

    private Vector3 dipolePos = Vector3.zero;
    private float envStrength = 1.0f;

    private EnvelopeParams env = new EnvelopeParams();
    private BindingCache bind;

    // Trails-safe respawn: kill + emit
    private ParticleSystem.EmitParams[] emitQueue;
    private int emitCount;

    // ============================================================
    // Envelope parameter set
    // ============================================================

    [Serializable]
    private class EnvelopeParams
    {
        public float magnetosphereRadius = 13f;
        public float solarWindStrength = 0f;

        public float daysideCompression = 0f;
        public float nightsideStretch = 0f;
        public float tailFieldStrength = 0f;

        public float magnetopauseBlendWidth = 0f;
        public float dayPerpCompressionRatio = 0.35f;
        public float tailPerpFlareRatio = 0.35f;

        public float pressureGain = 0.35f;
        public bool useSquaredPressure = true;

        public float daysideResponseExp = 1.6f;
        public float tailResponseExp = 2.0f;
        public float tailPerpResponseExp = 1.4f;

        public float minDaysideAlongScale = 0.10f;
        public float minDaysidePerpScale = 0.18f;
        public float stormDaysideCompressionBoost = 3.0f;

        public float stormIntensity = 4.5f;
        public float stormWarpScale = 0.25f;

        public float flankPinchStrength = 2.0f;
        public float minFlankPerpScale = 0.55f;
        public float flankPinchWidth = 0f;
        public float flankPinchResponseExp = 1.5f;
    }

    private class BindingCache
    {
        public bool valid;
        public object target;
        public Type type;

        // CPU vs GPU naming differences
        public FieldInfo f_effectiveWindDir;   // CPU field name
        public FieldInfo f__effectiveWindDir;  // GPU private field name

        public FieldInfo f_dipolePosition;

        // CPU version
        public FieldInfo f_globalWindStrengthCached;

        // GPU version (envelope reconstruction)
        public FieldInfo f_stormEnvelope; // "_stormEnvelope" or "stormEnvelope"
        public FieldInfo f_stormActive;   // "_stormActive" or "stormActive"

        public FieldInfo f_magnetosphereRadius;
        public FieldInfo f_solarWindStrength;

        public FieldInfo f_daysideCompression;
        public FieldInfo f_nightsideStretch;
        public FieldInfo f_tailFieldStrength;

        public FieldInfo f_magnetopauseBlendWidth;
        public FieldInfo f_dayPerpCompressionRatio;
        public FieldInfo f_tailPerpFlareRatio;

        public FieldInfo f_pressureGain;
        public FieldInfo f_useSquaredPressure;

        public FieldInfo f_daysideResponseExp;
        public FieldInfo f_tailResponseExp;
        public FieldInfo f_tailPerpResponseExp;

        public FieldInfo f_minDaysideAlongScale;
        public FieldInfo f_minDaysidePerpScale;
        public FieldInfo f_stormDaysideCompressionBoost;

        public FieldInfo f_stormIntensity;
        public FieldInfo f_stormWarpScale;

        public FieldInfo f_flankPinchStrength;
        public FieldInfo f_minFlankPerpScale;
        public FieldInfo f_flankPinchWidth;
        public FieldInfo f_flankPinchResponseExp;
    }

    // ============================================================
    // Unity lifecycle
    // ============================================================

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();

        ApplyTrailsSettings();

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = true;
        main.maxParticles = maxParticles;
        main.playOnAwake = true;

        // finite lifetimes
        main.startLifetime = particleLifetime;

        main.startColor = particleColor;
        main.startSize = Mathf.Max(0.001f, sizeRange.y);
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        var emission = ps.emission;
        emission.enabled = false;

        if (forceRendererEnabled)
        {
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.enabled = true;
                rend.renderMode = ParticleSystemRenderMode.Billboard;

                if (rend.sharedMaterial == null)
                    rend.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

                if (enableTrails && rend.trailMaterial == null)
                    rend.trailMaterial = rend.sharedMaterial;
            }
        }

        particles = new ParticleSystem.Particle[Mathf.Max(1, maxParticles)];
        emitQueue = new ParticleSystem.EmitParams[Mathf.Max(1, maxParticles)];

        step = (particleUpdateRateHz <= 0f) ? 0f : 1f / particleUpdateRateHz;
        bowStep = (bowShockUpdateRateHz <= 0f) ? 0f : 1f / bowShockUpdateRateHz;

        if (enableBowShock)
            bowShockLR = GetOrCreateBowShockLine();

        if (magnetosphereSource != null)
            bind = BuildBindings(magnetosphereSource);

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Play(true);
    }

    private void OnEnable()
    {
        ApplyTrailsSettings();
        InitializeParticles();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        ApplyTrailsSettings();
    }

    private void Update()
    {
        SyncFromMagnetosphereIfNeeded();

        float dt = Time.deltaTime;

        if (step <= 0f)
        {
            SimStep(dt);
        }
        else
        {
            accum += dt;
            int it = 0;
            int maxIt = Mathf.Max(1, maxSubstepsPerFrame);
            while (accum >= step && it++ < maxIt)
            {
                SimStep(step);
                accum -= step;
            }
            // If we hit the cap, drop remaining accumulated time to avoid running multiple full passes.
            if (it >= maxIt) accum = 0f;
        }

        if (enableBowShock && bowShockLR != null)
        {
            if (bowStep <= 0f)
            {
                UpdateBowShock();
            }
            else
            {
                bowAccum += dt;
                if (bowAccum >= bowStep)
                {
                    bowAccum = 0f;
                    UpdateBowShock();
                }
            }
        }
    }

    // ============================================================
    // Trails are script-authoritative
    // ============================================================

    private void ApplyTrailsSettings()
    {
        if (ps == null) return;

        var trails = ps.trails;

        if (!enableTrails)
        {
            trails.enabled = false;
            return;
        }

        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 1.0f;
        trails.dieWithParticles = true;

        float minT = Mathf.Max(0.01f, tailLifetimeRange.x);
        float maxT = Mathf.Max(minT, tailLifetimeRange.y);
        trails.lifetime = new ParticleSystem.MinMaxCurve(minT, maxT);
    }

    // ============================================================
    // Particle init + step
    // ============================================================

    private void InitializeParticles()
    {
        ps.Clear(true);

        int count = Mathf.Min(maxParticles, particles.Length);

        Vector3 f = flowDir;
        Vector3 p = GetPerp(f);

        float halfLen, halfWid;
        GetProjectedHalfExtents(f, p, out halfLen, out halfWid);

        for (int i = 0; i < count; i++)
        {
            var part = new ParticleSystem.Particle();

            part.startLifetime = particleLifetime;
            part.remainingLifetime = particleLifetime * (0.25f + 0.75f * UnityEngine.Random.value);

            part.startColor = particleColor;

Vector3 pos = SpawnOnUpstreamEdge(f, p, halfLen, halfWid);
SnapOutside(ref pos);


            part.position = pos;
            part.startSize = UnityEngine.Random.Range(sizeRange.x, sizeRange.y);

            float spd = UnityEngine.Random.Range(Mathf.Min(speedRange.x, speedRange.y), Mathf.Max(speedRange.x, speedRange.y));
            Vector3 vel = f * spd;
            vel.y = 0f;
            part.velocity = vel;

            part.randomSeed = (uint)UnityEngine.Random.Range(1, int.MaxValue);
            particles[i] = part;
        }

        ps.SetParticles(particles, count);
        ps.Play(true);
    }

    private void SimStep(float dt)
    {
        int count = ps.GetParticles(particles);
        emitCount = 0;

        Vector3 f = flowDir;
        Vector3 p = GetPerp(f);

        float halfLen, halfWid;
        GetProjectedHalfExtents(f, p, out halfLen, out halfWid);

        float minSpd = Mathf.Min(speedRange.x, speedRange.y);
        float maxSpd = Mathf.Max(speedRange.x, speedRange.y);

        for (int i = 0; i < count; i++)
        {
            var part = particles[i];

            Vector3 pos = part.position;
            pos.y = planeY;

            float baseSpeed = Mathf.Clamp(new Vector2(part.velocity.x, part.velocity.z).magnitude, minSpd, maxSpd);

            uint rs = part.randomSeed;
            float lateralJ = NoiseSigned(rs ^ 0xA2C2u, Time.time, jitterFrequency) * jitterStrength;
            float alongJ   = NoiseSigned(rs ^ 0xBEEFu, Time.time, jitterFrequency * 1.37f) * jitterAlongStrength;

            // Desired velocity: mostly along storm direction, with a small lateral component.
            Vector3 desiredV = f * baseSpeed * (1f + alongJ * 0.10f)
                             + p * (lateralJ * baseSpeed * lateralJitterSpeedFraction);
            desiredV.y = 0f;

            // Smoothly chase desired velocity to avoid frame-to-frame direction flipping (criss-cross trails).
            Vector3 vPrev = part.velocity;
            vPrev.y = 0f;
            float a = 1f - Mathf.Exp(-Mathf.Max(0.01f, velocityResponse) * dt);
            Vector3 v = Vector3.Lerp(vPrev, desiredV, a);
            v.y = 0f;

            // Envelope-aware gating (robust for oblong/compressed envelopes)
            // 1) Only steer/tangent-prep when close to the boundary.
            // 2) Always enforce "outside" after integration if we crossed inside.

            float q0 = EnvelopeQ(pos);
            float nearBand = 1f + Mathf.Max(0.0001f, approachBand) * 2.0f; // widen band a bit

            if (q0 < nearBand)
            {
                // Close enough that we should start wrapping behavior.
                AvoidOutside(ref pos, ref v, baseSpeed);
            }

            pos += v * dt;
            pos.y = planeY;

            // If we crossed inside at all, hard constrain back outside.
            float q1 = EnvelopeQ(pos);
            if (q1 < 1f)
            {
                ConstrainOutside(ref pos, ref v, baseSpeed);
            }
            else
            {
                // Keep speeds stable.
                NormalizeToSpeed(ref v, baseSpeed);
            }

if (wrapParticles)
{
    // Use flow-space bounds to avoid corner-biased respawns for diagonal storm directions.
    if (IsOutsideFlowStrip(pos, f, p, halfLen, halfWid))
    {
        part.remainingLifetime = 0f;
        particles[i] = part;

        QueueEmitUpstream(f, p, halfLen, halfWid);
        continue;
    }
}

            part.position = pos;
            part.velocity = v;
            particles[i] = part;
        }

        ps.SetParticles(particles, count);

        for (int k = 0; k < emitCount; k++)
            ps.Emit(emitQueue[k], 1);
    }

    private void QueueEmitUpstream(Vector3 f, Vector3 p, float halfLen, float halfWid)
    {
        if (emitCount >= emitQueue.Length) return;

Vector3 pos = SpawnOnUpstreamEdge(f, p, halfLen, halfWid);
SnapOutside(ref pos);


        float spd = UnityEngine.Random.Range(Mathf.Min(speedRange.x, speedRange.y), Mathf.Max(speedRange.x, speedRange.y));
        Vector3 vel = f * spd;
        vel.y = 0f;

        var ep = new ParticleSystem.EmitParams();
        ep.position = pos;
        ep.velocity = vel;
        ep.startSize = UnityEngine.Random.Range(sizeRange.x, sizeRange.y);
        ep.startColor = particleColor;

        ep.startLifetime = particleLifetime;
        ep.randomSeed = (uint)UnityEngine.Random.Range(1, int.MaxValue);

        emitQueue[emitCount++] = ep;
    }

    // ============================================================
    // Outside-only avoidance (UPDATED to wrap along boundary using flowDir)
    // ============================================================

    private void AvoidOutside(ref Vector3 pos, ref Vector3 vel, float baseSpeed)
    {
        float q = EnvelopeQ(pos);

        float start = 1f + approachBand;
        float t = Mathf.Clamp01((start - q) / Mathf.Max(1e-5f, approachBand));
        t = Mathf.Pow(t, approachPower);

        if (t > 0f)
        {
            // Option 3: calm the motion near boundary (reduces buzzing)
            float calm = Mathf.Lerp(1f, boundaryCalmMultiplier, t);
            vel *= calm;

            Vector3 n = EnvelopeNormal(pos);

            // If we're pointing into the boundary, remove inward component.
            float vn = Vector3.Dot(vel, n);
            if (vn < 0f)
                vel -= n * vn * (tangentSteerStrength * t);

            // Prefer sliding along the boundary in the storm/flow direction.
            Vector3 flowTangent = flowDir - n * Vector3.Dot(flowDir, n);
            flowTangent.y = 0f;

            if (flowTangent.sqrMagnitude > 1e-8f)
            {
                flowTangent.Normalize();
                Vector3 desired = flowTangent * baseSpeed;
                vel = Vector3.Lerp(vel, desired, Mathf.Clamp01(t * tangentSteerStrength));
            }

            // Push outward so we never cross into the magnetosphere
            vel += n * (outwardPush * t);

            if (q < 1f)
            {
                ProjectToBoundary(ref pos);
                pos += n * boundaryClearance;
                pos.y = planeY;
            }
        }

        NormalizeToSpeed(ref vel, baseSpeed);
    }

    private void ConstrainOutside(ref Vector3 pos, ref Vector3 vel, float baseSpeed)
    {
        float q = EnvelopeQ(pos);
        if (q >= 1f)
        {
            NormalizeToSpeed(ref vel, baseSpeed);
            return;
        }

        Vector3 n = EnvelopeNormal(pos);
        ProjectToBoundary(ref pos);
        pos += n * boundaryClearance;
        pos.y = planeY;

        // Remove inward velocity component
        float vn = Vector3.Dot(vel, n);
        if (vn < 0f) vel -= n * vn;

        // Force motion along the boundary in the storm/flow direction
        Vector3 flowTangent = flowDir - n * Vector3.Dot(flowDir, n);
        flowTangent.y = 0f;

        if (flowTangent.sqrMagnitude > 1e-8f)
        {
            flowTangent.Normalize();
            vel = flowTangent * baseSpeed;
        }
        else
        {
            Vector3 tangent = vel - n * Vector3.Dot(vel, n);
            if (tangent.sqrMagnitude > 1e-8f) vel = tangent;
        }

        NormalizeToSpeed(ref vel, baseSpeed);
    }

    private void SnapOutside(ref Vector3 pos)
    {
        float q = EnvelopeQ(pos);
        if (q >= 1f) return;

        Vector3 n = EnvelopeNormal(pos);
        ProjectToBoundary(ref pos);
        pos += n * boundaryClearance;
        pos.y = planeY;
    }

    private static void NormalizeToSpeed(ref Vector3 v, float speed)
    {
        v.y = 0f;
        float m = new Vector2(v.x, v.z).magnitude;
        if (m < 1e-6f) return;
        v = (v / m) * Mathf.Max(0.01f, speed);
        v.y = 0f;
    }

    // ============================================================
    // Envelope helpers
    // ============================================================

    private float EnvelopeQ(Vector3 pos)
    {
        Vector3 w = windAxisDownstream;
        Vector3 r = pos - dipolePos;

        float along = Vector3.Dot(r, w);
        Vector3 perp = r - w * along;
        float perpMag = perp.magnitude;

        float a, b, alongScale, perpScale;
        GetLocalEnvelope(envStrength, along, out a, out b, out alongScale, out perpScale);

        a = Mathf.Max(1e-4f, a);
        b = Mathf.Max(1e-4f, b);

        return Mathf.Sqrt((along * along) / (a * a) + (perpMag * perpMag) / (b * b));
    }

    private Vector3 EnvelopeNormal(Vector3 pos)
    {
        Vector3 w = windAxisDownstream;
        Vector3 r = pos - dipolePos;

        float along = Vector3.Dot(r, w);
        Vector3 perp = r - w * along;

        float a, b, alongScale, perpScale;
        GetLocalEnvelope(envStrength, along, out a, out b, out alongScale, out perpScale);

        a = Mathf.Max(1e-4f, a);
        b = Mathf.Max(1e-4f, b);

        Vector3 n = w * (along / (a * a));
        float pm = perp.magnitude;
        if (pm > 1e-6f) n += perp * (1f / (b * b));

        n.y = 0f;
        if (n.sqrMagnitude < 1e-10f) n = w;
        return n.normalized;
    }

    private void ProjectToBoundary(ref Vector3 pos)
    {
        float q = EnvelopeQ(pos);
        if (q <= 1e-6f) return;

        Vector3 w = windAxisDownstream;
        Vector3 r = pos - dipolePos;

        float along = Vector3.Dot(r, w);
        Vector3 perp = r - w * along;

        float inv = 1f / q;
        Vector3 p2 = dipolePos + w * (along * inv) + perp * inv;
        p2.y = planeY;
        pos = p2;
    }

    // ============================================================
    // Sync from magnetosphere (reflection)
    // ============================================================

    private static FieldInfo FindFieldAny(Type t, BindingFlags flags, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            var fi = t.GetField(names[i], flags);
            if (fi != null) return fi;
        }
        return null;
    }

    private void SyncFromMagnetosphereIfNeeded()
    {
        // Defaults / fallbacks
        windAxisDownstream = downstreamDirectionOverride;
        windAxisDownstream.y = 0f;
        if (windAxisDownstream.sqrMagnitude < 1e-6f) windAxisDownstream = Vector3.right;
        windAxisDownstream.Normalize();

        flowDir = flowUsesIncomingDirection ? -windAxisDownstream : windAxisDownstream;
        flowDir.y = 0f;
        flowDir.Normalize();

        dipolePos = playfieldCenter;
        envStrength = envelopeStrengthOverride;

        if (!syncFromMagnetosphere || magnetosphereSource == null) return;

        if (bind == null || !bind.valid || bind.target != magnetosphereSource)
            bind = BuildBindings(magnetosphereSource);

        if (bind == null || !bind.valid) return;

        // Downstream axis (tail direction). Support CPU and GPU field names.
        if (!TryReadField(bind.f_effectiveWindDir, ref windAxisDownstream))
            TryReadField(bind.f__effectiveWindDir, ref windAxisDownstream);

        windAxisDownstream.y = 0f;
        if (windAxisDownstream.sqrMagnitude < 1e-6f) windAxisDownstream = downstreamDirectionOverride.normalized;
        windAxisDownstream.Normalize();

        // Particles move with storm direction across field (incoming vs downstream toggle)
        flowDir = flowUsesIncomingDirection ? -windAxisDownstream : windAxisDownstream;
        flowDir.y = 0f;
        flowDir.Normalize();

        TryReadField(bind.f_dipolePosition, ref dipolePos);

        // Pull the rest of envelope params (works for both CPU + GPU scripts)
        TryReadField(bind.f_magnetosphereRadius, ref env.magnetosphereRadius);
        TryReadField(bind.f_solarWindStrength, ref env.solarWindStrength);

        TryReadField(bind.f_daysideCompression, ref env.daysideCompression);
        TryReadField(bind.f_nightsideStretch, ref env.nightsideStretch);
        TryReadField(bind.f_tailFieldStrength, ref env.tailFieldStrength);

        TryReadField(bind.f_magnetopauseBlendWidth, ref env.magnetopauseBlendWidth);
        TryReadField(bind.f_dayPerpCompressionRatio, ref env.dayPerpCompressionRatio);
        TryReadField(bind.f_tailPerpFlareRatio, ref env.tailPerpFlareRatio);

        TryReadField(bind.f_pressureGain, ref env.pressureGain);
        TryReadField(bind.f_useSquaredPressure, ref env.useSquaredPressure);

        TryReadField(bind.f_daysideResponseExp, ref env.daysideResponseExp);
        TryReadField(bind.f_tailResponseExp, ref env.tailResponseExp);
        TryReadField(bind.f_tailPerpResponseExp, ref env.tailPerpResponseExp);

        TryReadField(bind.f_minDaysideAlongScale, ref env.minDaysideAlongScale);
        TryReadField(bind.f_minDaysidePerpScale, ref env.minDaysidePerpScale);
        TryReadField(bind.f_stormDaysideCompressionBoost, ref env.stormDaysideCompressionBoost);

        TryReadField(bind.f_stormIntensity, ref env.stormIntensity);
        TryReadField(bind.f_stormWarpScale, ref env.stormWarpScale);

        TryReadField(bind.f_flankPinchStrength, ref env.flankPinchStrength);
        TryReadField(bind.f_minFlankPerpScale, ref env.minFlankPerpScale);
        TryReadField(bind.f_flankPinchWidth, ref env.flankPinchWidth);
        TryReadField(bind.f_flankPinchResponseExp, ref env.flankPinchResponseExp);

        // Envelope strength (CPU cached OR reconstructed for GPU)
        if (useMagnetosphereEnvelopeStrength)
        {
            float s = 0f;
            if (TryReadField(bind.f_globalWindStrengthCached, ref s))
            {
                envStrength = s;
            }
            else
            {
                float stormEnv = 0f;
                bool stormActiveLocal = false;

                TryReadField(bind.f_stormEnvelope, ref stormEnv);
                TryReadField(bind.f_stormActive, ref stormActiveLocal);

                float env01 = (stormActiveLocal || stormEnv > 0.0001f) ? Mathf.Clamp01(stormEnv) : 0f;
                envStrength = env.solarWindStrength + (env.stormIntensity * env.stormWarpScale * env01);
            }
        }

        planeY = dipolePos.y;
    }

    private static BindingCache BuildBindings(MonoBehaviour src)
    {
        var bc = new BindingCache();
        bc.target = src;
        bc.type = src.GetType();

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        bc.f_effectiveWindDir = FindFieldAny(bc.type, flags, "effectiveWindDir");
        bc.f__effectiveWindDir = FindFieldAny(bc.type, flags, "_effectiveWindDir");

        bc.f_dipolePosition = FindFieldAny(bc.type, flags, "dipolePosition", "_dipolePosition");

        bc.f_globalWindStrengthCached = FindFieldAny(bc.type, flags, "globalWindStrengthCached", "_globalWindStrengthCached");

        bc.f_stormEnvelope = FindFieldAny(bc.type, flags, "_stormEnvelope", "stormEnvelope");
        bc.f_stormActive = FindFieldAny(bc.type, flags, "_stormActive", "stormActive");

        bc.f_magnetosphereRadius = FindFieldAny(bc.type, flags, "magnetosphereRadius");
        bc.f_solarWindStrength = FindFieldAny(bc.type, flags, "solarWindStrength");

        bc.f_daysideCompression = FindFieldAny(bc.type, flags, "daysideCompression");
        bc.f_nightsideStretch = FindFieldAny(bc.type, flags, "nightsideStretch");
        bc.f_tailFieldStrength = FindFieldAny(bc.type, flags, "tailFieldStrength");

        bc.f_magnetopauseBlendWidth = FindFieldAny(bc.type, flags, "magnetopauseBlendWidth");
        bc.f_dayPerpCompressionRatio = FindFieldAny(bc.type, flags, "dayPerpCompressionRatio");
        bc.f_tailPerpFlareRatio = FindFieldAny(bc.type, flags, "tailPerpFlareRatio");

        bc.f_pressureGain = FindFieldAny(bc.type, flags, "pressureGain");
        bc.f_useSquaredPressure = FindFieldAny(bc.type, flags, "useSquaredPressure");

        bc.f_daysideResponseExp = FindFieldAny(bc.type, flags, "daysideResponseExp");
        bc.f_tailResponseExp = FindFieldAny(bc.type, flags, "tailResponseExp");
        bc.f_tailPerpResponseExp = FindFieldAny(bc.type, flags, "tailPerpResponseExp");

        bc.f_minDaysideAlongScale = FindFieldAny(bc.type, flags, "minDaysideAlongScale");
        bc.f_minDaysidePerpScale = FindFieldAny(bc.type, flags, "minDaysidePerpScale");
        bc.f_stormDaysideCompressionBoost = FindFieldAny(bc.type, flags, "stormDaysideCompressionBoost");

        bc.f_stormIntensity = FindFieldAny(bc.type, flags, "stormIntensity");
        bc.f_stormWarpScale = FindFieldAny(bc.type, flags, "stormWarpScale");

        bc.f_flankPinchStrength = FindFieldAny(bc.type, flags, "flankPinchStrength");
        bc.f_minFlankPerpScale = FindFieldAny(bc.type, flags, "minFlankPerpScale");
        bc.f_flankPinchWidth = FindFieldAny(bc.type, flags, "flankPinchWidth");
        bc.f_flankPinchResponseExp = FindFieldAny(bc.type, flags, "flankPinchResponseExp");

        // Valid if we can read a wind dir via CPU or GPU name and a radius.
        bc.valid = ((bc.f_effectiveWindDir != null) || (bc.f__effectiveWindDir != null)) && (bc.f_magnetosphereRadius != null);
        return bc;
    }

    private bool TryReadField(FieldInfo fi, ref float dst)
    {
        if (fi == null || bind == null || !bind.valid || bind.target == null) return false;
        try { dst = (float)fi.GetValue(bind.target); return true; }
        catch { return false; }
    }

    private bool TryReadField(FieldInfo fi, ref bool dst)
    {
        if (fi == null || bind == null || !bind.valid || bind.target == null) return false;
        try { dst = (bool)fi.GetValue(bind.target); return true; }
        catch { return false; }
    }

    private bool TryReadField(FieldInfo fi, ref Vector3 dst)
    {
        if (fi == null || bind == null || !bind.valid || bind.target == null) return false;
        try { dst = (Vector3)fi.GetValue(bind.target); return true; }
        catch { return false; }
    }

    // ============================================================
    // Bow shock
    // ============================================================

    private LineRenderer GetOrCreateBowShockLine()
    {
        var go = new GameObject("BowShock");
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 0;
        lr.widthMultiplier = bowShockWidth;
        lr.alignment = LineAlignment.View;

        var mat = new Material(Shader.Find("Sprites/Default"));
        lr.material = mat;

        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(bowShockColor, 0f), new GradientColorKey(bowShockColor, 1f) },
            new[] { new GradientAlphaKey(bowShockColor.a, 0f), new GradientAlphaKey(bowShockColor.a, 1f) }
        );
        lr.colorGradient = g;

        return lr;
    }

    private void UpdateBowShock()
    {
        if (!enableBowShock || bowShockLR == null) return;

        Vector3 windward = -windAxisDownstream;
        windward.y = 0f;
        windward.Normalize();

        Vector3 center = dipolePos;
        center.y = planeY;

        int N = Mathf.Max(16, bowShockSamples);
        List<Vector3> pts = new List<Vector3>(N);

        for (int i = 0; i < N; i++)
        {
            float a = (i / (float)(N - 1)) * Mathf.PI * 2f;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)).normalized;

            if (bowShockWindwardOnly && Vector3.Dot(d, windward) <= 0f)
                continue;

            float r = SolveEnvelopeRadiusAlongDirection(d, envStrength);
            Vector3 p = center + d * r;
            p.y = planeY;

            Vector3 n = EnvelopeNormal(p);
            Vector3 s = p + n * bowShockOffset;
            s.y = planeY;

            pts.Add(s);
        }

        if (pts.Count < 2)
        {
            bowShockLR.positionCount = 0;
            return;
        }

        bowShockLR.widthMultiplier = bowShockWidth;
        bowShockLR.positionCount = pts.Count;
        bowShockLR.SetPositions(pts.ToArray());
    }

    // ============================================================
    // Solve envelope radius (used by bow shock)
    // ============================================================

    private float SolveEnvelopeRadiusAlongDirection(Vector3 dirXZ, float windStrength)
    {
        Vector3 w = windAxisDownstream;
        if (w.sqrMagnitude < 1e-6f) w = Vector3.right;
        w.y = 0f;
        w.Normalize();

        dirXZ.y = 0f;
        if (dirXZ.sqrMagnitude < 1e-6f) dirXZ = Vector3.right;
        dirXZ.Normalize();

        float dot = Mathf.Clamp(Vector3.Dot(dirXZ, w), -1f, 1f);
        float perpFactor = Mathf.Sqrt(Mathf.Max(0f, 1f - dot * dot));

        float Q(float r)
        {
            float along = r * dot;
            float perpMag = r * perpFactor;

            float a, b, alongScale, perpScale;
            GetLocalEnvelope(windStrength, along, out a, out b, out alongScale, out perpScale);

            a = Mathf.Max(1e-4f, a);
            b = Mathf.Max(1e-4f, b);

            return Mathf.Sqrt((along * along) / (a * a) + (perpMag * perpMag) / (b * b));
        }

        float low = 0f;
        float high = env.magnetosphereRadius * 2f;
        float qh = Q(high);

        int expand = 0;
        while (qh < 1f && expand < 12)
        {
            high *= 1.5f;
            qh = Q(high);
            expand++;
        }

        for (int it = 0; it < 26; it++)
        {
            float mid = 0.5f * (low + high);
            float qm = Q(mid);
            if (qm < 1f) low = mid;
            else high = mid;
        }

        return high;
    }

    // ============================================================
    // Envelope function (conceptual)
    // ============================================================

    private void GetLocalEnvelope(float windStrength, float along, out float a, out float b, out float alongScale, out float perpScale)
    {
        float bw = (env.magnetopauseBlendWidth > 0.001f) ? env.magnetopauseBlendWidth : env.magnetosphereRadius * 0.35f;
        float tTail = Smoothstep01(-bw, bw, along);

        float w = Mathf.Max(0f, windStrength);
        float x = env.useSquaredPressure ? (w * w) : w;
        float pressure01 = 1f - Mathf.Exp(-x * Mathf.Max(0.0001f, env.pressureGain));
        pressure01 = Mathf.Clamp01(pressure01);
        pressure01 = Mathf.Max(1e-6f, pressure01);

        float pDay = Mathf.Pow(pressure01, Mathf.Max(0.01f, env.daysideResponseExp));
        float pTail = Mathf.Pow(pressure01, Mathf.Max(0.01f, env.tailResponseExp));
        float pPerp = Mathf.Pow(pressure01, Mathf.Max(0.01f, env.tailPerpResponseExp));

        float comp = env.daysideCompression * pDay;
        float stretch = env.nightsideStretch * pTail;
        float flare = env.tailFieldStrength * pPerp;

        float stormMaxDrive = Mathf.Max(0.0001f, env.stormIntensity * env.stormWarpScale);
        float stormContribution = Mathf.Max(0f, windStrength - env.solarWindStrength);
        float stormLevel01 = Mathf.Clamp01(stormContribution / stormMaxDrive);
        comp *= (1f + env.stormDaysideCompressionBoost * stormLevel01);

        float dayAlong = 1f / (1f + comp);
        float dayPerp = 1f / (1f + comp * env.dayPerpCompressionRatio);

        float tailAlong = 1f + stretch;
        float tailPerp = 1f + flare * env.tailPerpFlareRatio;

        dayAlong = Mathf.Clamp(dayAlong, env.minDaysideAlongScale, 1f);
        dayPerp = Mathf.Clamp(dayPerp, env.minDaysidePerpScale, 1.25f);
        tailAlong = Mathf.Clamp(tailAlong, 1f, 18f);
        tailPerp = Mathf.Clamp(tailPerp, 1f, 6f);

        alongScale = Mathf.Lerp(dayAlong, tailAlong, tTail);
        perpScale = Mathf.Lerp(dayPerp, tailPerp, tTail);

        float fw = (env.flankPinchWidth > 0.001f) ? env.flankPinchWidth : bw;
        fw = Mathf.Max(0.001f, fw);

        float flankW = Mathf.Exp(-(along * along) / (fw * fw));
        float pFlank = Mathf.Pow(pressure01, env.flankPinchResponseExp);
        float pinch01 = Mathf.Clamp01(env.flankPinchStrength * pFlank);

        float targetPerp = Mathf.Lerp(perpScale, Mathf.Max(0.001f, perpScale * env.minFlankPerpScale), pinch01);
        perpScale = Mathf.Lerp(perpScale, targetPerp, flankW);

        a = env.magnetosphereRadius * alongScale;
        b = env.magnetosphereRadius * perpScale;
    }

    // ============================================================
    // Playfield helpers
    // ============================================================

    private static Vector3 GetPerp(Vector3 v)
    {
        Vector3 p = new Vector3(-v.z, 0f, v.x);
        if (p.sqrMagnitude < 1e-6f) p = Vector3.forward;
        return p.normalized;
    }

    private void GetProjectedHalfExtents(Vector3 alongAxis, Vector3 perpAxis, out float halfLen, out float halfWid)
    {
        float hx = playfieldSizeXZ.x * 0.5f;
        float hz = playfieldSizeXZ.y * 0.5f;

        halfLen = Mathf.Abs(alongAxis.x) * hx + Mathf.Abs(alongAxis.z) * hz;
        halfWid = Mathf.Abs(perpAxis.x) * hx + Mathf.Abs(perpAxis.z) * hz;
    }

    private void ClampToRectXZ(ref Vector3 pos)
    {
        float hx = playfieldSizeXZ.x * 0.5f;
        float hz = playfieldSizeXZ.y * 0.5f;

        pos.x = Mathf.Clamp(pos.x, playfieldCenter.x - hx, playfieldCenter.x + hx);
        pos.z = Mathf.Clamp(pos.z, playfieldCenter.z - hz, playfieldCenter.z + hz);
    }

    private bool IsOutsideRectXZ(Vector3 pos)
    {
        float hx = playfieldSizeXZ.x * 0.5f;
        float hz = playfieldSizeXZ.y * 0.5f;

        return (pos.x < playfieldCenter.x - hx || pos.x > playfieldCenter.x + hx ||
                pos.z < playfieldCenter.z - hz || pos.z > playfieldCenter.z + hz);
    }
    // Flow-space (oriented) bounds check. This is the correct test for diagonal storm directions.
// It prevents premature respawns for particles that are outside the world AABB but still inside the flow strip.
private bool IsOutsideFlowStrip(Vector3 pos, Vector3 f, Vector3 p, float halfLen, float halfWid)
{
    Vector3 r = pos - playfieldCenter;
    r.y = 0f;

    float along = Vector3.Dot(r, f);
    float perp = Vector3.Dot(r, p);

    // Allow extra margin on the sides so we can spawn upstream without corner bias.
    float sideLimit = halfWid + spawnMargin;
    float downLimit = halfLen + spawnMargin * 1.25f;

    if (Mathf.Abs(perp) > sideLimit) return true;
    if (along > downLimit) return true;

    return false;
}

    private void GetPlayfieldRectXZ(out Vector2 rectMin, out Vector2 rectMax)
{
    float hx = playfieldSizeXZ.x * 0.5f;
    float hz = playfieldSizeXZ.y * 0.5f;

    rectMin = new Vector2(playfieldCenter.x - hx, playfieldCenter.z - hz);
    rectMax = new Vector2(playfieldCenter.x + hx, playfieldCenter.z + hz);
}

// Returns t interval [tMin,tMax] where (o + d*t) lies inside AABB [min,max] in 2D.
// Slab method, stable and fast.
private static bool TryLineAabbSegment2D(Vector2 o, Vector2 d, Vector2 min, Vector2 max, out float tMin, out float tMax)
{
    tMin = float.NegativeInfinity;
    tMax = float.PositiveInfinity;

    const float eps = 1e-6f;

    // X slab
    if (Mathf.Abs(d.x) < eps)
    {
        if (o.x < min.x || o.x > max.x) return false;
    }
    else
    {
        float inv = 1f / d.x;
        float t1 = (min.x - o.x) * inv;
        float t2 = (max.x - o.x) * inv;
        if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }

        tMin = Mathf.Max(tMin, t1);
        tMax = Mathf.Min(tMax, t2);
        if (tMax < tMin) return false;
    }

    // Z slab (stored in y)
    if (Mathf.Abs(d.y) < eps)
    {
        if (o.y < min.y || o.y > max.y) return false;
    }
    else
    {
        float inv = 1f / d.y;
        float t1 = (min.y - o.y) * inv;
        float t2 = (max.y - o.y) * inv;
        if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }

        tMin = Mathf.Max(tMin, t1);
        tMax = Mathf.Min(tMax, t2);
        if (tMax < tMin) return false;
    }

    return true;
}

/// <summary>
/// Spawns on the upstream edge segment (uniformly) for the given flow direction.
/// This removes the corner “hose” artifact.
/// </summary>
private Vector3 SpawnOnUpstreamEdge(Vector3 f, Vector3 p, float halfLen, float halfWid)
{
    // IMPORTANT: Use a fixed upstream plane so the clipped segment length stays constant.
    // If we randomize the plane position (s) per particle, the segment length varies near corners,
    // which biases density into corners (the "hose" artifact).

    float sBase = -halfLen - spawnMargin;

    // Origin on the upstream plane
    Vector3 o3 = playfieldCenter + f * sBase;
    o3.y = planeY;

    // Intersect the line (o + p*t) with the playfield rect in XZ
    GetPlayfieldRectXZ(out Vector2 rMin, out Vector2 rMax);

    Vector2 o2 = new Vector2(o3.x, o3.z);
    Vector2 d2 = new Vector2(p.x, p.z);

    if (!TryLineAabbSegment2D(o2, d2, rMin, rMax, out float tMin, out float tMax))
    {
        // Fallback: sample across full perp width, then clamp.
        float lateral = (UnityEngine.Random.value * 2f - 1f) * halfWid;
        Vector3 posFallback = o3 + p * lateral;
        posFallback.y = planeY;
        ClampToRectXZ(ref posFallback);

        // Apply upstream depth after clamping so the stream direction is correct.
        float depth = UnityEngine.Random.value * Mathf.Max(0.01f, upstreamSpawnBand);
        posFallback += (-f) * depth;
        posFallback.y = planeY;
        return posFallback;
    }

    // Uniform along the clipped edge segment
    float t = Mathf.Lerp(tMin, tMax, UnityEngine.Random.value);
    Vector3 pos = o3 + p * t;
    pos.y = planeY;

    // Add depth by moving further upstream (keeps a consistent sheet without corner bias)
    float depth2 = UnityEngine.Random.value * Mathf.Max(0.01f, upstreamSpawnBand);
    pos += (-f) * depth2;
    pos.y = planeY;

    return pos;
}


    // ============================================================
    // Bow shock / debug
    // ============================================================

    private void OnDrawGizmosSelected()
    {
        if (!debugDrawPlayfieldRect) return;

        Gizmos.color = Color.green;

        float hx = playfieldSizeXZ.x * 0.5f;
        float hz = playfieldSizeXZ.y * 0.5f;

        Vector3 c = new Vector3(playfieldCenter.x, planeY, playfieldCenter.z);

        Vector3 a = c + new Vector3(-hx, 0f, -hz);
        Vector3 b = c + new Vector3(hx, 0f, -hz);
        Vector3 d = c + new Vector3(-hx, 0f, hz);
        Vector3 e = c + new Vector3(hx, 0f, hz);

        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, e);
        Gizmos.DrawLine(e, d);
        Gizmos.DrawLine(d, a);
    }

    private static uint HashU32(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return x;
    }

    private static float Hash01(uint x)
    {
        return (HashU32(x) & 0x00FFFFFFu) / 16777216f;
    }

    // Cheap deterministic "noise" in [-1,1] without Perlin.
    private static float NoiseSigned(uint seed, float t, float freq)
    {
        float a = (Hash01(seed) * 2f - 1f) * 6.28318530718f;
        return Mathf.Sin(a + t * freq);
    }

    private static float Smoothstep01(float edge0, float edge1, float x)
    {
        if (edge1 <= edge0) return (x < edge0) ? 0f : 1f;
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
