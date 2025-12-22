using UnityEngine;
using System.Runtime.InteropServices;

public class MagnetosphereVisualizer : MonoBehaviour
{
    [Header("Compute Shader (optional particles)")]
    [SerializeField] private ComputeShader magnetosphereCompute;

    [Header("Render Toggles")]
    [SerializeField] private bool renderParticles = false;
    [SerializeField] private bool renderFieldLines = true;

    [Header("Particle Settings")]
    [SerializeField] private int particleCount = 50000;
    [SerializeField] private float particleSize = 0.02f;
    [SerializeField] private float particleDamping = 0.5f;
    [SerializeField] private float velocityLimit = 5.0f;
    [SerializeField] private Material particleMaterial;

    [Header("Magnetosphere Settings")]
    [SerializeField] private float dipoleMoment = 170.0f;
    [SerializeField] private float magnetosphereRadius = 13.0f;
    [SerializeField] private float planetRadius = 0.03f;
    [SerializeField] private Vector3 dipolePosition = Vector3.zero;

    [Header("Orientation")]
    [Tooltip("Magnetic dipole axis in world space. Forward gives you a side-on magnetosphere when viewing the XZ plane.")]
    [SerializeField] private Vector3 dipoleAxis = Vector3.forward;

    [Header("Wind / Global Shape (non-storm baseline)")]
    [Tooltip("DOWNSTREAM direction (tail direction). Will be clamped to XZ if enabled.")]
    [SerializeField] private Vector3 solarWindDirection = Vector3.right;

    [Tooltip("Baseline wind strength (shape warp).")]
    [SerializeField, Range(0f, 6f)] private float solarWindStrength = 0.0f;

    [Tooltip("Upstream/day side compression amount.")]
    [SerializeField, Range(0f, 2.5f)] private float daysideCompression = 0.0f;

    [Tooltip("Downstream/tail stretch amount.")]
    [SerializeField, Range(0f, 4.0f)] private float nightsideStretch = 0.0f;

    [Tooltip("Tail flare (perpendicular expansion) amount.")]
    [SerializeField, Range(0f, 4.0f)] private float tailFieldStrength = 0.0f;

    [Header("Live Turbulence (non-storm “warble/layers”)")]
    [SerializeField, Range(0f, 3f)] private float turbulenceStrength = 0.06f;
    [SerializeField, Range(0.05f, 2f)] private float turbulenceScale = 1.09f;
    [SerializeField, Range(0.05f, 5f)] private float turbulenceSpeed = 1.10f;

    [Header("Storm Settings (moving front/wave)")]
    [SerializeField] private float stormInterval = 2.0f;
    [SerializeField] private float stormDuration = 2.0f;

    [Tooltip("Storm intensity controls how much EXTRA wind is injected by the front.")]
    [SerializeField, Range(0f, 10f)] private float stormIntensity = 6.0f;

    [Tooltip("Scales how much stormIntensity affects the SPACE-WARP strength. Lower = less shredding.")]
    [SerializeField, Range(0f, 1f)] private float stormWarpScale = 0.35f;

    [Tooltip("Scales how much stormIntensity boosts turbulence inside/behind the front.")]
    [SerializeField, Range(0f, 1.5f)] private float stormTurbulenceScale = 0.65f;

    [Header("Storm Front Shape")]
    [Tooltip("Front thickness (world units) along the wind axis.")]
    [SerializeField] private float stormFrontThickness = 5.5f;

    [Tooltip("Extra wake distance behind the front that retains some turbulence (world units).")]
    [SerializeField] private float stormWakeLength = 12.0f;

    [Tooltip("How soft the front edges are. 0 = hard-ish slab, 1 = very soft.")]
    [SerializeField, Range(0f, 1f)] private float stormFrontFeather = 0.65f;

    [Header("Wind Direction Randomization")]
    [SerializeField] private bool windXZOnly = true;
    [SerializeField] private bool randomizeWindOnStormStart = true;

    [Tooltip("If true, baseline wind direction will slowly drift even when no storm.")]
    [SerializeField] private bool driftWindWhenCalm = false;

    [SerializeField, Range(0f, 1f)] private float calmWindDriftAmount = 0.15f;
    [SerializeField, Range(0.05f, 2f)] private float calmWindDriftSpeed = 0.20f;

    [Header("Field Lines (Runtime)")]
    [SerializeField] private Material fieldLineMaterial;
    [SerializeField] private int fieldLineCount = 100;
    [SerializeField] private int fieldLineSegments = 201;

    [Tooltip("Base step length for integrating field lines.")]
    [SerializeField] private float lineStep = 0.06f;

    [Tooltip("Smoothing speed (higher = snappier).")]
    [SerializeField] private float lineResponse = 10.0f;

    [Tooltip("How much seeds drift to create layer motion and “fusing/separating” looks.")]
    [SerializeField] private float seedDrift = 1.55f;

    [Tooltip("Extra small displacement applied after integration (subtle).")]
    [SerializeField] private float visualWarble = 0.08f;

    [Header("Spin / Layer Motion")]
    [SerializeField, Range(0f, 2f)] private float spinStrength = 1.0f;
    [SerializeField, Range(0f, 2f)] private float spinSpeed = 0.25f;

    [SerializeField] private Color innerLineColor = new Color(0.95f, 0.15f, 0.80f, 0.20f);
    [SerializeField] private Color outerLineColor = new Color(0.95f, 0.15f, 0.80f, 0.10f);
    [SerializeField] private float fieldLineWidth = 0.02f;

    // Particle struct (must match compute)
    [StructLayout(LayoutKind.Sequential)]
    private struct Particle
    {
        public Vector3 position;
        public Vector3 velocity;
        public float charge;
        public float energy;
        public float lifetime;
        public float maxLifetime;
    }

    // GPU
    private ComputeBuffer particleBuffer;
    private ComputeBuffer argsBuffer;
    private Mesh particleMesh;
    private Bounds bounds;

    private int initKernel, updateKernel, applyStormKernel;

    // Time / storms
    private float simulationTime;
    private float lastStormTime;
    private float currentStormTime;
    private Vector3 currentStormDirection;  // downstream dir (XZ)
    private bool stormActive;

    // Wind
    private Vector3 baseWindDir;
    private Vector3 effectiveWindDir;

    // Storm envelope + moving front
    private float stormEnvelope;            // 0..1 pulse shape
    private float stormFrontCenterS;        // scalar center along wind axis (world units)

    // Dipole
    private Vector3 axisY;
    private Quaternion dipoleRotation;

    // Field lines
    private GameObject fieldLinesRoot;
    private LineRenderer[] fieldLines;
    private Vector3[] linePointsPrev;
    private Vector3[] linePointsNow;

    private void OnValidate()
    {
        dipoleMoment = Mathf.Max(0.1f, dipoleMoment);
        magnetosphereRadius = Mathf.Max(0.5f, magnetosphereRadius);
        planetRadius = Mathf.Max(0.001f, Mathf.Abs(planetRadius));

        fieldLineCount = Mathf.Max(1, fieldLineCount);
        fieldLineSegments = Mathf.Max(8, fieldLineSegments);

        lineStep = Mathf.Max(0.005f, lineStep);
        lineResponse = Mathf.Max(0.01f, lineResponse);

        stormFrontThickness = Mathf.Max(0.2f, stormFrontThickness);
        stormWakeLength = Mathf.Max(0f, stormWakeLength);

        if (dipoleAxis == Vector3.zero) dipoleAxis = Vector3.forward;
        if (solarWindDirection == Vector3.zero) solarWindDirection = Vector3.right;
    }

    private void Start()
    {
        axisY = dipoleAxis.normalized;
        dipoleRotation = Quaternion.FromToRotation(Vector3.up, axisY);

        baseWindDir = NormalizeWind(solarWindDirection);
        currentStormDirection = baseWindDir;

        bounds = new Bounds(dipolePosition, Vector3.one * magnetosphereRadius * 10f);

        if (renderParticles)
        {
            InitializeCompute();
            CreateParticleMesh();
            InitializeParticles();
        }

        if (renderFieldLines)
        {
            BuildOrRebuildFieldLinesObjects();
        }

        lastStormTime = -Mathf.Max(0.01f, stormInterval);
        stormFrontCenterS = -999999f;
    }

    private void Update()
    {
        simulationTime += Time.deltaTime;

        // Keep base wind updated from inspector when not drifting (so tweaking works live)
        if (!driftWindWhenCalm && !stormActive)
            baseWindDir = NormalizeWind(solarWindDirection);

        // Optional calm wind drift
        if (driftWindWhenCalm && !stormActive)
        {
            float n = Mathf.PerlinNoise(10.1f, simulationTime * calmWindDriftSpeed);
            float a = (n - 0.5f) * 2f * calmWindDriftAmount * Mathf.PI;
            Vector3 drift = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            baseWindDir = Vector3.Slerp(baseWindDir, drift.normalized, 1f - Mathf.Exp(-1.5f * Time.deltaTime));
        }

        bool stormsEnabled = stormInterval > 0.01f && stormDuration > 0.01f && stormIntensity > 0.001f;
        if (stormsEnabled && !stormActive && (simulationTime - lastStormTime > stormInterval))
            StartNewStorm();

        // Storm envelope + front travel
        stormEnvelope = 0f;
        if (stormActive)
        {
            currentStormTime += Time.deltaTime;

            float t01 = Mathf.Clamp01(currentStormTime / Mathf.Max(0.0001f, stormDuration));

            // pulse: ramp in quickly, hold-ish, ramp out
            float up = Smooth01(Mathf.Clamp01(t01 / 0.22f));
            float down = 1f - Smooth01(Mathf.Clamp01((t01 - 0.65f) / 0.35f));
            stormEnvelope = Mathf.Clamp01(up * down);

            // Front moves from upstream -> downstream across duration
            // Along axis: negative = upstream, positive = tail (since w is downstream)
            float startS = -magnetosphereRadius * 1.25f;
            float endS   =  magnetosphereRadius * 2.25f;
            stormFrontCenterS = Mathf.Lerp(startS, endS, t01);

            if (currentStormTime >= stormDuration)
            {
                stormActive = false;
                stormFrontCenterS = -999999f;
            }
        }

        effectiveWindDir = stormActive ? currentStormDirection : baseWindDir;
        effectiveWindDir = NormalizeWind(effectiveWindDir);

        if (renderParticles && particleBuffer != null && particleMaterial != null)
        {
            UpdateCompute();
            RenderParticles();
        }

        if (renderFieldLines && fieldLines != null)
        {
            UpdateLiveFieldLines();
        }
    }

    // ----------------------------
    // WIND + STORM FRONT
    // ----------------------------

    private Vector3 NormalizeWind(Vector3 w)
    {
        if (windXZOnly) w.y = 0f;
        if (w.sqrMagnitude < 1e-6f) w = Vector3.right;
        return w.normalized;
    }

    private Vector3 RandomWindXZ()
    {
        float a = Random.Range(0f, Mathf.PI * 2f);
        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)).normalized;
    }

    private void StartNewStorm()
    {
        currentStormDirection = randomizeWindOnStormStart ? RandomWindXZ() : baseWindDir;

        stormActive = true;
        currentStormTime = 0f;
        lastStormTime = simulationTime;

        stormFrontCenterS = -magnetosphereRadius * 1.25f;
    }

    public void TriggerStorm(Vector3 directionXZ)
    {
        currentStormDirection = NormalizeWind(directionXZ);
        stormActive = true;
        currentStormTime = 0f;
        lastStormTime = simulationTime;

        stormFrontCenterS = -magnetosphereRadius * 1.25f;
    }

    /// <summary>
    /// Returns 0..1 mask of storm influence at a position:
    /// - strong in the front core (compression/warp driver)
    /// - moderate in the wake behind the front (turbulence driver)
    /// </summary>
    private void StormMaskAt(Vector3 pos, out float frontCore, out float wake)
    {
        frontCore = 0f;
        wake = 0f;

        if (!stormActive) return;

        Vector3 r = pos - dipolePosition;
        float along = Vector3.Dot(r, effectiveWindDir); // downstream axis scalar
        float dist = along - stormFrontCenterS;          // <0 ahead of front, >0 behind front

        float half = stormFrontThickness * 0.5f;
        float feather = Mathf.Lerp(0.05f, 2.0f, stormFrontFeather);

        // Core slab (symmetric)
        float ad = Mathf.Abs(dist);
        float core = 1f - Smoothstep01(half, half * feather, ad);

        // Wake behind front (downstream only)
        float wk = 0f;
        if (stormWakeLength > 0.001f && dist > 0f)
        {
            float u = Mathf.Clamp01(dist / stormWakeLength);
            wk = 1f - Smooth01(u); // strongest right behind front, fades out
        }

        // Apply temporal envelope
        frontCore = core * stormEnvelope;
        wake = wk * stormEnvelope;
    }

    // ----------------------------
    // FIELD LINES
    // ----------------------------

    private void BuildOrRebuildFieldLinesObjects()
    {
        if (fieldLineMaterial == null)
            fieldLineMaterial = new Material(Shader.Find("Sprites/Default"));

        if (fieldLinesRoot != null) Destroy(fieldLinesRoot);
        fieldLinesRoot = new GameObject("MagnetosphereFieldLines");
        fieldLinesRoot.transform.SetParent(transform, false);

        fieldLines = new LineRenderer[fieldLineCount];
        linePointsPrev = new Vector3[fieldLineCount * fieldLineSegments];
        linePointsNow = new Vector3[fieldLineCount * fieldLineSegments];

        for (int i = 0; i < fieldLineCount; i++)
        {
            var go = new GameObject($"FieldLine_{i:000}");
            go.transform.SetParent(fieldLinesRoot.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.material = fieldLineMaterial;
            lr.widthMultiplier = fieldLineWidth;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.positionCount = fieldLineSegments;

            float t = (fieldLineCount == 1) ? 0f : i / (float)(fieldLineCount - 1);
            Color c = Color.Lerp(innerLineColor, outerLineColor, t);

            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(c.a * 0.0f, 0.00f),
                    new GradientAlphaKey(c.a * 1.0f, 0.18f),
                    new GradientAlphaKey(c.a * 1.0f, 0.82f),
                    new GradientAlphaKey(c.a * 0.0f, 1.00f),
                }
            );
            lr.colorGradient = g;

            fieldLines[i] = lr;
        }

        ForceRecomputeFieldLinesImmediate();
    }

    private void ForceRecomputeFieldLinesImmediate()
    {
        RecomputeFieldLinesPositions(simulationTime, writePrev: true);
        ApplyFieldLinesToRenderers(lerpAlpha: 1f);
    }

    private void UpdateLiveFieldLines()
    {
        float a = 1f - Mathf.Exp(-lineResponse * Time.deltaTime);

        RecomputeFieldLinesPositions(simulationTime, writePrev: false);
        ApplyFieldLinesToRenderers(lerpAlpha: a);
    }

    private void RecomputeFieldLinesPositions(float t, bool writePrev)
    {
        int rings = Mathf.Max(3, Mathf.RoundToInt(Mathf.Sqrt(fieldLineCount)));
        int sectors = Mathf.CeilToInt(fieldLineCount / (float)rings);

        float seedRMin = Mathf.Max(planetRadius * 1.10f, magnetosphereRadius * 0.18f);
        float seedRMax = magnetosphereRadius * 0.75f;

        int half = fieldLineSegments / 2;
        float eps = 1e-4f;

        for (int i = 0; i < fieldLineCount; i++)
        {
            int ring = i % rings;
            int sector = i / rings;

            float ringT = (rings == 1) ? 0f : ring / (float)(rings - 1);
            float baseR = Mathf.Lerp(seedRMin, seedRMax, ringT);
            float baseAz = (sector / (float)Mathf.Max(1, sectors)) * Mathf.PI * 2f;

            float h = Hash01(i);
            float azNoise = (Mathf.PerlinNoise(h * 11.1f, t * 0.15f) - 0.5f) * 2f * seedDrift;
            float rNoise  = (Mathf.PerlinNoise(h * 33.7f, t * 0.12f + 4.1f) - 0.5f) * 2f * seedDrift;

            float spinDir = (h > 0.5f) ? 1f : -1f;
            float spin = spinDir * spinSpeed * (0.35f + 1.25f * ringT) * spinStrength;

            float az = baseAz + azNoise + t * spin;
            float r = baseR * (1f + rNoise * 0.12f);

            Vector3 seedLocal = new Vector3(r * Mathf.Cos(az), 0f, r * Mathf.Sin(az));
            Vector3 seedWorld = dipolePosition + dipoleRotation * seedLocal;

            int baseIndex = i * fieldLineSegments;

            // Backward
            Vector3 p = seedWorld;
            bool reachedPlanet = false;
            for (int s = half - 1; s >= 0; s--)
            {
                if (reachedPlanet)
                {
                    linePointsNow[baseIndex + s] = linePointsNow[baseIndex + s + 1];
                    continue;
                }

                Vector3 B = ComputeField(p, t, out float windStrengthLocal);
                float Bm = Mathf.Max(B.magnitude, eps);

                Vector3 dir = (B / Bm);
                dir = AdjustDirForBoundaries(p, dir, windStrengthLocal);

                float step = lineStep * (1f + 0.25f / Bm);
                Vector3 next = p - dir * step;

                next = ApplyPlanetPoleClamp(next, ref reachedPlanet);
                next = ApplyMagnetopauseClamp(next, windStrengthLocal);

                linePointsNow[baseIndex + s] = next;
                p = next;
            }

            // Middle
            linePointsNow[baseIndex + half] = seedWorld;

            // Forward
            p = seedWorld;
            reachedPlanet = false;
            for (int s = half + 1; s < fieldLineSegments; s++)
            {
                if (reachedPlanet)
                {
                    linePointsNow[baseIndex + s] = linePointsNow[baseIndex + s - 1];
                    continue;
                }

                Vector3 B = ComputeField(p, t, out float windStrengthLocal);
                float Bm = Mathf.Max(B.magnitude, eps);

                Vector3 dir = (B / Bm);
                dir = AdjustDirForBoundaries(p, dir, windStrengthLocal);

                float step = lineStep * (1f + 0.25f / Bm);
                Vector3 next = p + dir * step;

                next = ApplyPlanetPoleClamp(next, ref reachedPlanet);
                next = ApplyMagnetopauseClamp(next, windStrengthLocal);

                linePointsNow[baseIndex + s] = next;
                p = next;
            }

            // Subtle post-warp (alive look)
            for (int s = 0; s < fieldLineSegments; s++)
            {
                float u = (fieldLineSegments == 1) ? 0f : s / (float)(fieldLineSegments - 1);
                Vector3 pos = linePointsNow[baseIndex + s];

                float rMag = (pos - dipolePosition).magnitude;
                float boundaryW = Smooth01((rMag - magnetosphereRadius * 0.55f) / (magnetosphereRadius * 0.55f));
                float poleW = 1f - Mathf.Abs(Vector3.Dot((pos - dipolePosition).normalized, axisY));

                Vector3 wob = WarbleOffset(pos, t, i, u);
                linePointsNow[baseIndex + s] = pos + wob * visualWarble * boundaryW * poleW;
            }

            if (writePrev)
            {
                for (int s = 0; s < fieldLineSegments; s++)
                    linePointsPrev[baseIndex + s] = linePointsNow[baseIndex + s];
            }
        }
    }

    private void ApplyFieldLinesToRenderers(float lerpAlpha)
    {
        for (int i = 0; i < fieldLineCount; i++)
        {
            int baseIndex = i * fieldLineSegments;
            var lr = fieldLines[i];

            for (int s = 0; s < fieldLineSegments; s++)
            {
                int idx = baseIndex + s;
                Vector3 prev = linePointsPrev[idx];
                Vector3 now = linePointsNow[idx];
                Vector3 smoothed = Vector3.Lerp(prev, now, lerpAlpha);

                lr.SetPosition(s, smoothed);
                linePointsPrev[idx] = smoothed;
            }
        }
    }

    // ----------------------------
    // FIELD MODEL (CPU)
    // Storm affects SPACE WARP locally via moving front
    // ----------------------------

    private Vector3 ComputeField(Vector3 pos, float t, out float windStrengthLocal)
    {
        // Local storm mask at pos
        StormMaskAt(pos, out float frontCore, out float wake);

        // Storm contributes mainly via warp in the front core; turbulence persists into wake
        float stormWarp = stormIntensity * frontCore * stormWarpScale;
        windStrengthLocal = solarWindStrength + stormWarp;

        // Warp sampling position so topology stays dipole-like
        Vector3 warped = WarpPositionForWind(pos, effectiveWindDir, windStrengthLocal);

        // Dipole field at warped position
        Vector3 B = DipoleField(warped);

        // Turbulence: baseline + localized storm boost (front + wake)
        Vector3 r = pos - dipolePosition;
        float rMag = Mathf.Max(1e-4f, r.magnitude);

        float boundaryW = Smooth01((rMag - magnetosphereRadius * 0.55f) / (magnetosphereRadius * 0.55f));

        float stormTurb = stormIntensity * (frontCore * 1.0f + wake * 0.6f) * stormTurbulenceScale;
        float turbAmp = turbulenceStrength * (0.15f + 0.85f * boundaryW) * (1f + 0.25f * stormTurb);

        if (turbAmp > 0f)
        {
            Vector3 n = CurlNoise(pos / Mathf.Max(1e-4f, turbulenceScale), t * turbulenceSpeed);
            B += n * turbAmp;
        }

        return B;
    }

    private Vector3 DipoleField(Vector3 pos)
    {
        Vector3 r = pos - dipolePosition;
        float rMag = r.magnitude;
        if (rMag < 1e-4f) return Vector3.zero;

        Vector3 rHat = r / rMag;
        Vector3 m = axisY * dipoleMoment;

        float mDotR = Vector3.Dot(m, rHat);
        return (3f * mDotR * rHat - m) / (rMag * rMag * rMag);
    }

    private Vector3 WarpPositionForWind(Vector3 pos, Vector3 windDirDownstream, float windStrengthNow)
    {
        if (windStrengthNow <= 1e-4f) return pos;

        Vector3 r = pos - dipolePosition;
        float rMag = Mathf.Max(1e-4f, r.magnitude);
        Vector3 rHat = r / rMag;

        Vector3 w = windDirDownstream;  // tail direction
        Vector3 toSun = -w;

        float day = Mathf.Clamp01(Vector3.Dot(rHat, toSun));
        float night = Mathf.Clamp01(Vector3.Dot(rHat, w));

        float comp = daysideCompression * windStrengthNow;
        float stretch = nightsideStretch * windStrengthNow;
        float flare = tailFieldStrength * windStrengthNow;

        // clamps prevent “deleted hemisphere”
        float dayAlongFactor  = Mathf.Clamp(1f - 0.55f * comp, 0.55f, 1f);
        float tailAlongFactor = Mathf.Clamp(1f + 1.10f * stretch, 1f, 7f);

        float dayPerpFactor  = Mathf.Clamp(1f - 0.10f * comp, 0.75f, 1.2f);
        float tailPerpFactor = Mathf.Clamp(1f + 0.20f * flare, 1f, 2.2f);

        float along = Vector3.Dot(r, w);
        Vector3 perp = r - w * along;

        float alongFactor = 1f;
        float perpFactor = 1f;

        if (along < 0f)
        {
            alongFactor = Mathf.Lerp(1f, dayAlongFactor, day);
            perpFactor  = Mathf.Lerp(1f, dayPerpFactor, day);
        }
        else
        {
            alongFactor = Mathf.Lerp(1f, tailAlongFactor, night);
            perpFactor  = Mathf.Lerp(1f, tailPerpFactor, night);
        }

        return dipolePosition + w * (along * alongFactor) + perp * perpFactor;
    }

    private float DirectionalBoundary(Vector3 pos, float windStrengthNow)
    {
        Vector3 r = pos - dipolePosition;
        float rMag = Mathf.Max(1e-4f, r.magnitude);
        Vector3 rHat = r / rMag;

        Vector3 w = effectiveWindDir;
        Vector3 toSun = -w;

        float day = Mathf.Clamp01(Vector3.Dot(rHat, toSun));
        float night = Mathf.Clamp01(Vector3.Dot(rHat, w));

        float comp = daysideCompression * windStrengthNow;
        float stretch = nightsideStretch * windStrengthNow;

        float dayScale = Mathf.Clamp(1f - 0.55f * comp, 0.65f, 1f);
        float nightScale = Mathf.Clamp(1f + 1.10f * stretch, 1f, 9f);

        float boundaryScale = 1f + night * (nightScale - 1f) + day * (dayScale - 1f);
        return magnetosphereRadius * boundaryScale * 1.10f;
    }

    private Vector3 AdjustDirForBoundaries(Vector3 pos, Vector3 dir, float windStrengthNow)
    {
        Vector3 r = pos - dipolePosition;
        float rMag = Mathf.Max(1e-4f, r.magnitude);
        Vector3 rHat = r / rMag;

        float boundary = DirectionalBoundary(pos, windStrengthNow);

        // Near magnetopause: remove outward component so lines “graze” it instead of exiting
        if (rMag >= boundary * 0.985f)
        {
            float outComp = Vector3.Dot(dir, rHat);
            if (outComp > 0f)
            {
                dir = dir - rHat * outComp;
                if (dir.sqrMagnitude < 1e-6f) dir = Vector3.Cross(axisY, rHat);
                dir.Normalize();
            }
        }

        return dir;
    }

    private Vector3 ApplyMagnetopauseClamp(Vector3 next, float windStrengthNow)
    {
        Vector3 r = next - dipolePosition;
        float rMag = r.magnitude;
        if (rMag < 1e-4f) return next;

        float boundary = DirectionalBoundary(next, windStrengthNow);

        if (rMag > boundary)
        {
            Vector3 rHat = r / rMag;
            next = dipolePosition + rHat * boundary; // slide along boundary, don’t freeze
        }

        return next;
    }

    private Vector3 ApplyPlanetPoleClamp(Vector3 next, ref bool reachedPlanet)
    {
        Vector3 r = next - dipolePosition;
        float rMag = r.magnitude;

        float snapRadius = planetRadius * 1.05f;
        if (rMag <= snapRadius)
        {
            // Stylized pole snap so most arcs meet at poles
            float sign = Mathf.Sign(Vector3.Dot(r, axisY));
            if (Mathf.Abs(sign) < 1e-3f) sign = 1f;

            next = dipolePosition + axisY * sign * planetRadius;
            reachedPlanet = true;
        }

        return next;
    }

    private Vector3 CurlNoise(Vector3 p, float t)
    {
        Vector3 f1 = new Vector3(1.31f, 2.17f, 0.91f);
        Vector3 f2 = new Vector3(-1.73f, 0.63f, 2.41f);
        Vector3 f3 = new Vector3(2.27f, -1.11f, 1.53f);

        float p1 = Vector3.Dot(p, f1) + t * 1.1f;
        float p2 = Vector3.Dot(p, f2) + t * 0.9f;
        float p3 = Vector3.Dot(p, f3) + t * 1.3f;

        Vector3 da = Mathf.Cos(p1) * f1;
        Vector3 db = Mathf.Cos(p2) * f2;
        Vector3 dc = Mathf.Cos(p3) * f3;

        return new Vector3(dc.y - db.z, da.z - dc.x, db.x - da.y);
    }

    private Vector3 WarbleOffset(Vector3 pos, float t, int lineId, float u)
    {
        float h = Hash01(lineId);
        float phase = t * 0.9f + h * 12.7f;

        Vector3 r = pos - dipolePosition;
        Vector3 rN = r.sqrMagnitude < 1e-6f ? Vector3.forward : r.normalized;

        Vector3 tangent = Vector3.Cross(axisY, rN);
        if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.right;
        tangent.Normalize();

        Vector3 bitan = Vector3.Cross(rN, tangent).normalized;

        float w1 = Mathf.Sin(phase + u * 6.283f);
        float w2 = Mathf.Cos(phase * 1.17f + u * 9.111f);

        return (tangent * w1 + bitan * w2) * 0.5f;
    }

    private static float Hash01(int i)
    {
        uint x = (uint)i;
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return (x & 0x00FFFFFF) / 16777216f;
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

    // ----------------------------
    // OPTIONAL PARTICLES (kept working)
    // ----------------------------

    private void InitializeCompute()
    {
        if (magnetosphereCompute == null)
        {
            Debug.LogError("Magnetosphere compute shader not assigned.");
            enabled = false;
            return;
        }

        initKernel = magnetosphereCompute.FindKernel("InitParticles");
        updateKernel = magnetosphereCompute.FindKernel("UpdateParticles");
        applyStormKernel = magnetosphereCompute.FindKernel("ApplyStorm");

        int stride = Marshal.SizeOf(typeof(Particle));
        particleBuffer = new ComputeBuffer(particleCount, stride);

        magnetosphereCompute.SetBuffer(initKernel, "particles", particleBuffer);
        magnetosphereCompute.SetBuffer(updateKernel, "particles", particleBuffer);
        magnetosphereCompute.SetBuffer(applyStormKernel, "particles", particleBuffer);

        magnetosphereCompute.SetInt("particleCount", particleCount);
        magnetosphereCompute.SetFloat("particleDamping", particleDamping);
        magnetosphereCompute.SetFloat("velocityLimit", velocityLimit);

        magnetosphereCompute.SetFloat("dipoleMoment", dipoleMoment);
        magnetosphereCompute.SetFloat("magnetosphereRadius", magnetosphereRadius);
        magnetosphereCompute.SetFloat("planetRadius", planetRadius);
        magnetosphereCompute.SetVector("dipolePosition", dipolePosition);
        magnetosphereCompute.SetVector("dipoleAxis", axisY);

        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
    }

    private void CreateParticleMesh()
    {
        particleMesh = new Mesh { name = "ParticleQuad" };

        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3( 0.5f, -0.5f, 0),
            new Vector3(-0.5f,  0.5f, 0),
            new Vector3( 0.5f,  0.5f, 0),
        };

        int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };

        Vector2[] uv = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
        };

        particleMesh.vertices = vertices;
        particleMesh.triangles = triangles;
        particleMesh.uv = uv;
        particleMesh.RecalculateNormals();
        particleMesh.RecalculateBounds();
    }

    private void InitializeParticles()
    {
        magnetosphereCompute.SetFloat("time", Random.value * 1000f);
        int groups = Mathf.CeilToInt(particleCount / 256f);
        magnetosphereCompute.Dispatch(initKernel, groups, 1, 1);
    }

    private void UpdateCompute()
    {
        // NOTE: particles use the same effective wind direction and moving front params
        magnetosphereCompute.SetFloat("time", simulationTime);
        magnetosphereCompute.SetFloat("deltaTime", Time.deltaTime);

        magnetosphereCompute.SetVector("solarWindDirection", effectiveWindDir);
        magnetosphereCompute.SetFloat("solarWindStrength", solarWindStrength);

        magnetosphereCompute.SetFloat("daysideCompression", daysideCompression);
        magnetosphereCompute.SetFloat("nightsideStretch", nightsideStretch);
        magnetosphereCompute.SetFloat("tailFieldStrength", tailFieldStrength);

        magnetosphereCompute.SetFloat("turbulenceStrength", turbulenceStrength);
        magnetosphereCompute.SetFloat("turbulenceScale", turbulenceScale);
        magnetosphereCompute.SetFloat("turbulenceSpeed", turbulenceSpeed);

        magnetosphereCompute.SetFloat("stormIntensity", stormIntensity);
        magnetosphereCompute.SetFloat("stormActive", stormActive ? 1f : 0f);
        magnetosphereCompute.SetFloat("stormEnvelope", stormEnvelope);
        magnetosphereCompute.SetFloat("stormWarpScale", stormWarpScale);
        magnetosphereCompute.SetFloat("stormTurbulenceScale", stormTurbulenceScale);

        magnetosphereCompute.SetFloat("stormFrontCenterS", stormFrontCenterS);
        magnetosphereCompute.SetFloat("stormFrontThickness", stormFrontThickness);
        magnetosphereCompute.SetFloat("stormWakeLength", stormWakeLength);
        magnetosphereCompute.SetFloat("stormFrontFeather", stormFrontFeather);

        int groups = Mathf.CeilToInt(particleCount / 256f);

        if (stormActive)
            magnetosphereCompute.Dispatch(applyStormKernel, groups, 1, 1);

        magnetosphereCompute.Dispatch(updateKernel, groups, 1, 1);
    }

    private void RenderParticles()
    {
        if (particleMesh == null || argsBuffer == null) return;

        particleMaterial.SetBuffer("particles", particleBuffer);
        particleMaterial.SetFloat("_ParticleSize", particleSize);
        particleMaterial.SetVector("_DipolePosition", dipolePosition);

        uint[] args = new uint[5]
        {
            particleMesh.GetIndexCount(0),
            (uint)particleCount,
            particleMesh.GetIndexStart(0),
            particleMesh.GetBaseVertex(0),
            0
        };
        argsBuffer.SetData(args);

        Graphics.DrawMeshInstancedIndirect(
            particleMesh,
            0,
            particleMaterial,
            bounds,
            argsBuffer,
            castShadows: UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows: false,
            layer: gameObject.layer
        );
    }

    private void OnDestroy()
    {
        ReleaseBuffers();

        if (particleMesh != null)
        {
            if (Application.isPlaying) Destroy(particleMesh);
            else DestroyImmediate(particleMesh);
        }

        if (fieldLinesRoot != null)
        {
            if (Application.isPlaying) Destroy(fieldLinesRoot);
            else DestroyImmediate(fieldLinesRoot);
        }
    }

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    private void ReleaseBuffers()
    {
        if (particleBuffer != null) { particleBuffer.Release(); particleBuffer = null; }
        if (argsBuffer != null) { argsBuffer.Release(); argsBuffer = null; }
    }
}