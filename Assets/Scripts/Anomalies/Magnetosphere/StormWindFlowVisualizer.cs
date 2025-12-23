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

    [Tooltip("PATCH 2: Finite particle lifetime (seconds). Also applied to emitted replacements.")]
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

    // ============================================================
    // Boundary avoidance behavior (OUTSIDE ONLY)
    // ============================================================

    [Header("Magnetopause Avoidance (stay OUTSIDE)")]
    [SerializeField, Range(0.01f, 0.6f)] private float approachBand = 0.18f;
    [SerializeField, Range(0f, 3f)] private float tangentSteerStrength = 1.25f;
    [SerializeField, Range(0f, 3f)] private float outwardPush = 0.65f;
    [SerializeField, Range(0f, 0.25f)] private float boundaryClearance = 0.03f;
    [SerializeField, Range(0.5f, 6f)] private float approachPower = 2.0f;

    // ============================================================
    // Update rate
    // ============================================================

    [Header("Update Rate")]
    [SerializeField] private float particleUpdateRateHz = 60f;

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

        public FieldInfo f_effectiveWindDir; // downstream
        public FieldInfo f_dipolePosition;
        public FieldInfo f_globalWindStrengthCached;

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

        // Patch 1: script is authoritative for trails
        ApplyTrailsSettings();

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = true;
        main.maxParticles = maxParticles;
        main.playOnAwake = true;

        // PATCH 2: finite lifetimes (also used by EmitParams)
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
            while (accum >= step)
            {
                SimStep(step);
                accum -= step;
            }
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
    // Patch 1 helper: trails are script-authoritative
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

            // PATCH 2: finite lifetime (stagger remaining to avoid synchronized death)
            part.startLifetime = particleLifetime;
            part.remainingLifetime = particleLifetime * (0.25f + 0.75f * UnityEngine.Random.value);

            part.startColor = particleColor;

            float along = -halfLen - spawnMargin - UnityEngine.Random.value * Mathf.Max(0.01f, upstreamSpawnBand);
            float lateral = (UnityEngine.Random.value * 2f - 1f) * halfWid;

            Vector3 pos = playfieldCenter + f * along + p * lateral;
            pos.y = planeY;
            ClampToRectXZ(ref pos);

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

            float seed = (part.randomSeed & 0x00FFFFFF) / 16777216f;
            float n1 = Mathf.PerlinNoise(seed * 17.123f, Time.time * jitterFrequency);
            float n2 = Mathf.PerlinNoise(seed * 91.331f, Time.time * (jitterFrequency * 1.37f));

            float lateralJ = (n1 - 0.5f) * 2f * jitterStrength;
            float alongJ = (n2 - 0.5f) * 2f * jitterAlongStrength;

            Vector3 v = f * baseSpeed * (1f + alongJ * 0.15f) + p * (lateralJ * baseSpeed);
            v.y = 0f;

            AvoidOutside(ref pos, ref v, baseSpeed);

            pos += v * dt;
            pos.y = planeY;

            ConstrainOutside(ref pos, ref v, baseSpeed);

            if (wrapParticles)
            {
                bool outRect = IsOutsideRectXZ(pos);
                float alongFlow = Vector3.Dot(pos - playfieldCenter, f);

                if (outRect || alongFlow > halfLen + spawnMargin * 1.25f)
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

        float along = -halfLen - spawnMargin - UnityEngine.Random.value * Mathf.Max(0.01f, upstreamSpawnBand);
        float lateral = (UnityEngine.Random.value * 2f - 1f) * halfWid;

        Vector3 pos = playfieldCenter + f * along + p * lateral;
        pos.y = planeY;
        ClampToRectXZ(ref pos);

        SnapOutside(ref pos);

        float spd = UnityEngine.Random.Range(Mathf.Min(speedRange.x, speedRange.y), Mathf.Max(speedRange.x, speedRange.y));
        Vector3 vel = f * spd;
        vel.y = 0f;

        var ep = new ParticleSystem.EmitParams();
        ep.position = pos;
        ep.velocity = vel;
        ep.startSize = UnityEngine.Random.Range(sizeRange.x, sizeRange.y);
        ep.startColor = particleColor;

        // PATCH 2: finite lifetime for emitted replacements
        ep.startLifetime = particleLifetime;

        ep.randomSeed = (uint)UnityEngine.Random.Range(1, int.MaxValue);

        emitQueue[emitCount++] = ep;
    }

    // ============================================================
    // Outside-only avoidance (unchanged)
    // ============================================================

    private void AvoidOutside(ref Vector3 pos, ref Vector3 vel, float baseSpeed)
    {
        float q = EnvelopeQ(pos);

        float start = 1f + approachBand;
        float t = Mathf.Clamp01((start - q) / Mathf.Max(1e-5f, approachBand));
        t = Mathf.Pow(t, approachPower);

        if (t > 0f)
        {
            Vector3 n = EnvelopeNormal(pos);

            float vn = Vector3.Dot(vel, n);
            if (vn < 0f)
                vel -= n * vn * (tangentSteerStrength * t);

            Vector3 tangent = vel - n * Vector3.Dot(vel, n);
            if (tangent.sqrMagnitude > 1e-8f)
                vel = Vector3.Lerp(vel, tangent, Mathf.Clamp01(t * tangentSteerStrength));

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

        float vn = Vector3.Dot(vel, n);
        if (vn < 0f) vel -= n * vn;

        Vector3 tangent = vel - n * Vector3.Dot(vel, n);
        if (tangent.sqrMagnitude > 1e-8f) vel = tangent;

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

    // ============================================================
    // Sync from magnetosphere (reflection)
    // ============================================================

    private void SyncFromMagnetosphereIfNeeded()
    {
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

        TryReadField(bind.f_effectiveWindDir, ref windAxisDownstream);
        windAxisDownstream.y = 0f;
        if (windAxisDownstream.sqrMagnitude < 1e-6f) windAxisDownstream = downstreamDirectionOverride.normalized;
        windAxisDownstream.Normalize();

        flowDir = flowUsesIncomingDirection ? -windAxisDownstream : windAxisDownstream;
        flowDir.y = 0f;
        flowDir.Normalize();

        TryReadField(bind.f_dipolePosition, ref dipolePos);

        if (useMagnetosphereEnvelopeStrength)
        {
            float s = 0f;
            if (TryReadField(bind.f_globalWindStrengthCached, ref s))
                envStrength = s;
        }

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

        planeY = dipolePos.y;
    }

    private static BindingCache BuildBindings(MonoBehaviour src)
    {
        var bc = new BindingCache();
        bc.target = src;
        bc.type = src.GetType();

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        bc.f_effectiveWindDir = bc.type.GetField("effectiveWindDir", flags);
        bc.f_dipolePosition = bc.type.GetField("dipolePosition", flags);
        bc.f_globalWindStrengthCached = bc.type.GetField("globalWindStrengthCached", flags);

        bc.f_magnetosphereRadius = bc.type.GetField("magnetosphereRadius", flags);
        bc.f_solarWindStrength = bc.type.GetField("solarWindStrength", flags);

        bc.f_daysideCompression = bc.type.GetField("daysideCompression", flags);
        bc.f_nightsideStretch = bc.type.GetField("nightsideStretch", flags);
        bc.f_tailFieldStrength = bc.type.GetField("tailFieldStrength", flags);

        bc.f_magnetopauseBlendWidth = bc.type.GetField("magnetopauseBlendWidth", flags);
        bc.f_dayPerpCompressionRatio = bc.type.GetField("dayPerpCompressionRatio", flags);
        bc.f_tailPerpFlareRatio = bc.type.GetField("tailPerpFlareRatio", flags);

        bc.f_pressureGain = bc.type.GetField("pressureGain", flags);
        bc.f_useSquaredPressure = bc.type.GetField("useSquaredPressure", flags);

        bc.f_daysideResponseExp = bc.type.GetField("daysideResponseExp", flags);
        bc.f_tailResponseExp = bc.type.GetField("tailResponseExp", flags);
        bc.f_tailPerpResponseExp = bc.type.GetField("tailPerpResponseExp", flags);

        bc.f_minDaysideAlongScale = bc.type.GetField("minDaysideAlongScale", flags);
        bc.f_minDaysidePerpScale = bc.type.GetField("minDaysidePerpScale", flags);
        bc.f_stormDaysideCompressionBoost = bc.type.GetField("stormDaysideCompressionBoost", flags);

        bc.f_stormIntensity = bc.type.GetField("stormIntensity", flags);
        bc.f_stormWarpScale = bc.type.GetField("stormWarpScale", flags);

        bc.f_flankPinchStrength = bc.type.GetField("flankPinchStrength", flags);
        bc.f_minFlankPerpScale = bc.type.GetField("minFlankPerpScale", flags);
        bc.f_flankPinchWidth = bc.type.GetField("flankPinchWidth", flags);
        bc.f_flankPinchResponseExp = bc.type.GetField("flankPinchResponseExp", flags);

        bc.valid = (bc.f_effectiveWindDir != null) && (bc.f_magnetosphereRadius != null);
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
    // Debug playfield gizmo
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

    private static float Smoothstep01(float edge0, float edge1, float x)
    {
        if (edge1 <= edge0) return (x < edge0) ? 0f : 1f;
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
