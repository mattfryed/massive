using System;
using System.Reflection;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class ScoreVoidMetaballsVisual : MonoBehaviour
{
    [Header("Score Source (0..1)")]
    public MonoBehaviour scoreSource;                 // ScoreSphereScript or anything with Score01
    [Range(0f, 1f)] public float score01Debug = 0f;
    public bool driveFromScoreSource = true;

    [Header("Metaballs Driver")]
    public MetaballSDFInstance sdf;
    [Min(0)] public int minBalls = 4;
    [Min(1)] public int maxBalls = 24;
    [Min(0.01f)] public float scoreCurvePow = 1f;

    public enum HalfPlane { PositiveX, NegativeX }

    [Header("Region (object-space inside unit cube [-0.5..0.5])")]
    public Vector2 centerOS_XZ = Vector2.zero;
    [Range(0.01f, 0.5f)] public float regionRadiusOS = 0.48f;
    public HalfPlane halfPlane = HalfPlane.NegativeX;
    [Range(0f, 0.25f)] public float boundaryPaddingOS = 0.02f;

    [Header("Ball Radii (object space)")]
    [Range(0.005f, 0.49f)] public float radiusCenterOS = 0.14f;
    [Range(0.005f, 0.49f)] public float radiusEdgeOS = 0.06f;
    [Range(0.1f, 4f)] public float radialIndexPow = 1.6f;

    [Header("Edge Detail Bias (more smaller balls near rim)")]
    [Tooltip("Fraction of balls (excluding core) generated in an outer 'rim band' for silhouette texture.")]
    [Range(0f, 1f)] public float edgeBallFraction = 0.60f;

    [Tooltip("Rim band start as a fraction of region radius (0=center, 1=rim). Higher = thinner outer shell.")]
    [Range(0f, 1f)] public float edgeBandStart01 = 0.78f;

    [Tooltip("Bias within the rim band toward the outer boundary. Higher = more balls hug the rim.")]
    [Min(0.01f)] public float edgeRadialBiasPow = 2.25f;

    [Tooltip("Edge balls get smaller radii to create a more scalloped boundary.")]
    [Range(0.1f, 2f)] public float edgeRadiusMul = 0.55f;

    [Tooltip("Extra shrink applied to balls that are very close to the outer rim (1 = none).")]
    [Range(0.1f, 2f)] public float edgeOuterRadiusMul = 0.75f;

    [Tooltip("Random radius variation for edge balls (0 = none).")]
    [Range(0f, 1f)] public float edgeRadiusJitter01 = 0.35f;

    [Header("Core Ball (index 0) — scales with score")]
    public bool enableCoreBallScaling = true;

    [Tooltip("Core ball radius when score = 0")]
    [Range(0.005f, 0.49f)] public float coreRadiusMinOS = 0.10f;

    [Tooltip("Core ball radius when score = 1")]
    [Range(0.005f, 0.49f)] public float coreRadiusMaxOS = 0.22f;

    [Tooltip("Curve shaping for core radius vs score (1 = linear, >1 = slower start)")]
    [Min(0.01f)] public float coreRadiusScorePow = 1f;

    [Header("Bubble Motion")]
    [Range(0f, 0.1f)] public float jitterAmplitudeOS = 0.02f;
    [Range(0f, 0.2f)] public float yWobbleOS = 0.02f;
    [Range(0f, 0.5f)] public float radiusPulseAmp = 0.12f;
    [Range(0.01f, 10f)] public float radiusPulseSpeed = 1.7f;
    [Range(0f, 30f)] public float appearSpeed = 6f; // 0 = instant

    [Header("Depth (object space)")]
    public float ballCenterY_OS = 0f;

    [Header("Team Look (optional) — no separate shader needed")]
    public bool applyMaterialColors = true;

    [Tooltip("If true, tries to read teamID from scoreSource via reflection (ScoreSphereScript.teamID).")]
    public bool readTeamFromScoreSource = true;

    [Tooltip("Fallback team id if scoreSource has no teamID (1 = Team1, anything else = Team2).")]
    public int teamIDOverride = 1;

    public Color team1Fill = Color.white;
    public Color team1Outline = Color.black;

    public Color team2Fill = Color.black;
    public Color team2Outline = Color.white;

    [Header("Seed")]
    public int seed = 12345;

    [Header("Debug")]
    [SerializeField] private int lastBallCount;
    [SerializeField] private float lastScore01;

    private struct Candidate
    {
        public Vector3 center;
        public float dist;        // radial distance from region center in OS
        public bool isEdge;
        public float edgeJit01;   // stable per-ball radius jitter
    }

    Vector3[] _baseCenters;
    float[] _baseRadii;
    float[] _spawnTimes;

    int _cachedForMaxBalls = -1;
    int _cacheKey = 0;
    int _lastDesiredCount = -1;

    int _lastTeamID = int.MinValue;
    Color _lastFill, _lastOutline;

    void OnEnable()
    {
        if (!sdf) sdf = GetComponent<MetaballSDFInstance>();
        EnsureBaseCache();
        RebuildAndApply(force: true);
    }

    void OnValidate()
    {
        if (!sdf) sdf = GetComponent<MetaballSDFInstance>();
        EnsureBaseCache();
        RebuildAndApply(force: true);
    }

    void Update()
    {
        RebuildAndApply(force: false);
    }

    float Now()
    {
        return Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
    }

    float ReadScore01()
    {
        if (!driveFromScoreSource || !scoreSource)
            return score01Debug;

        var t = scoreSource.GetType();

        var pScore01 = t.GetProperty("Score01", BindingFlags.Public | BindingFlags.Instance);
        if (pScore01 != null && pScore01.PropertyType == typeof(float))
            return Mathf.Clamp01((float)pScore01.GetValue(scoreSource));

        var mGet = t.GetMethod("GetScore01", BindingFlags.Public | BindingFlags.Instance);
        if (mGet != null && mGet.ReturnType == typeof(float))
            return Mathf.Clamp01((float)mGet.Invoke(scoreSource, null));

        return score01Debug;
    }

    int ReadTeamID()
    {
        if (!readTeamFromScoreSource || !scoreSource)
            return teamIDOverride;

        var t = scoreSource.GetType();

        var p = t.GetProperty("teamID", BindingFlags.Public | BindingFlags.Instance);
        if (p != null && p.PropertyType == typeof(int))
            return (int)p.GetValue(scoreSource);

        var f = t.GetField("teamID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && f.FieldType == typeof(int))
            return (int)f.GetValue(scoreSource);

        return teamIDOverride;
    }

    void ApplyTeamColorsIfNeeded(bool force)
    {
        if (!applyMaterialColors || !sdf) return;

        int teamID = ReadTeamID();
        bool isTeam2 = teamID != 1;

        Color fill = isTeam2 ? team2Fill : team1Fill;
        Color outline = isTeam2 ? team2Outline : team1Outline;

        if (!force && teamID == _lastTeamID && fill == _lastFill && outline == _lastOutline)
            return;

        _lastTeamID = teamID;
        _lastFill = fill;
        _lastOutline = outline;

        // Your shader has _LitColor, _UnlitColor, _OutlineColor.
        // For a crisp vector look, we typically set lit+unlit the same.
        sdf.SetMaterialColors(fill, fill, outline);
    }

    int ComputeDesiredCount(float score01)
    {
        int a = Mathf.Clamp(minBalls, 0, MetaballSDFInstance.MaxBalls);
        int b = Mathf.Clamp(maxBalls, 1, MetaballSDFInstance.MaxBalls);
        if (b < a) (a, b) = (b, a);

        float t = Mathf.Pow(Mathf.Clamp01(score01), Mathf.Max(0.0001f, scoreCurvePow));
        int desired = Mathf.RoundToInt(Mathf.Lerp(a, b, t));
        return Mathf.Clamp(desired, a, b);
    }

    int ComputeCacheKey(int b)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + b;
            h = h * 31 + seed;
            h = h * 31 + (int)halfPlane;
            h = h * 31 + centerOS_XZ.GetHashCode();
            h = h * 31 + regionRadiusOS.GetHashCode();
            h = h * 31 + boundaryPaddingOS.GetHashCode();
            h = h * 31 + ballCenterY_OS.GetHashCode();
            h = h * 31 + radiusCenterOS.GetHashCode();
            h = h * 31 + radiusEdgeOS.GetHashCode();
            h = h * 31 + radialIndexPow.GetHashCode();

            h = h * 31 + edgeBallFraction.GetHashCode();
            h = h * 31 + edgeBandStart01.GetHashCode();
            h = h * 31 + edgeRadialBiasPow.GetHashCode();
            h = h * 31 + edgeRadiusMul.GetHashCode();
            h = h * 31 + edgeOuterRadiusMul.GetHashCode();
            h = h * 31 + edgeRadiusJitter01.GetHashCode();

            return h;
        }
    }

    void EnsureBaseCache()
    {
        int b = Mathf.Clamp(maxBalls, 1, MetaballSDFInstance.MaxBalls);
        int key = ComputeCacheKey(b);

        if (_cachedForMaxBalls == b &&
            _cacheKey == key &&
            _baseCenters != null &&
            _baseCenters.Length == b)
            return;

        _cachedForMaxBalls = b;
        _cacheKey = key;

        _baseCenters = new Vector3[b];
        _baseRadii = new float[b];
        _spawnTimes = new float[b];

        // Reset desired count so appear animation re-stamps if needed
        _lastDesiredCount = -1;

        float rr = Mathf.Clamp(regionRadiusOS - boundaryPaddingOS, 0.01f, 0.5f);
        float denom = Mathf.Max(0.0001f, rr);

        // Core ball (index 0) is always at the center.
        _baseCenters[0] = new Vector3(centerOS_XZ.x, ballCenterY_OS, centerOS_XZ.y);
        _baseRadii[0] = Mathf.Max(0.0005f, radiusCenterOS);
        _spawnTimes[0] = 0f;

        if (b == 1) return;

        var rng = new System.Random(seed);

        float edgeFrac = Mathf.Clamp01(edgeBallFraction);
        float bandStart = Mathf.Clamp01(edgeBandStart01);
        float edgePow = Mathf.Max(0.01f, edgeRadialBiasPow);
        float centerPow = Mathf.Max(0.01f, radialIndexPow);

        int candCount = b - 1;
        var candidates = new Candidate[candCount];

        for (int i = 0; i < candCount; i++)
        {
            // Angle across half-circle
            double v = rng.NextDouble(); // 0..1
            float phi = (float)((v * 2.0 - 1.0) * (Math.PI * 0.5)); // [-pi/2..pi/2]
            float ang = (halfPlane == HalfPlane.PositiveX) ? phi : (phi + Mathf.PI);

            // Decide edge vs bulk
            bool isEdge = rng.NextDouble() < edgeFrac;

            // Radius distribution
            double u = rng.NextDouble(); // 0..1
            float r01;

            if (isEdge)
            {
                // sample within [bandStart..1], biased toward 1.0
                float uEdge = 1f - Mathf.Pow((float)u, edgePow);
                r01 = Mathf.Lerp(bandStart, 1f, uEdge);
            }
            else
            {
                // center-biased bulk fill
                r01 = Mathf.Pow((float)u, centerPow);
            }

            float r = rr * r01;

            float x = Mathf.Cos(ang) * r;
            float z = Mathf.Sin(ang) * r;

            candidates[i] = new Candidate
            {
                center = new Vector3(centerOS_XZ.x + x, ballCenterY_OS, centerOS_XZ.y + z),
                dist = r,
                isEdge = isEdge,
                edgeJit01 = (float)rng.NextDouble()
            };
        }

        // Growth order: center -> edge
        Array.Sort(candidates, (a, b2) => a.dist.CompareTo(b2.dist));

        float edgeJ = Mathf.Clamp01(edgeRadiusJitter01);
        float edgeMul = Mathf.Max(0.01f, edgeRadiusMul);
        float edgeOuterMul = Mathf.Max(0.01f, edgeOuterRadiusMul);

        for (int i = 0; i < candCount; i++)
        {
            int idx = i + 1;
            var cand = candidates[i];

            _baseCenters[idx] = cand.center;

            float r01 = Mathf.Clamp01(cand.dist / denom);

            // Base radius from center -> edge
            float tRad = Mathf.Pow(r01, centerPow);
            float rad = Mathf.Lerp(radiusCenterOS, radiusEdgeOS, tRad);

            if (cand.isEdge)
            {
                // Make edge balls smaller + varied
                rad *= edgeMul;

                float jitter = Mathf.Lerp(1f - edgeJ, 1f + edgeJ, cand.edgeJit01);
                rad *= jitter;

                // shrink even more as we approach the outer rim
                float edgeT = (bandStart >= 0.999f) ? 1f : Mathf.InverseLerp(bandStart, 1f, r01);
                rad *= Mathf.Lerp(1f, edgeOuterMul, edgeT);
            }

            _baseRadii[idx] = Mathf.Max(0.0005f, rad);
            _spawnTimes[idx] = 0f;
        }
    }

    void RebuildAndApply(bool force)
    {
        if (!sdf) return;

        float score01 = ReadScore01();
        ApplyTeamColorsIfNeeded(force);

        int desired = ComputeDesiredCount(score01);

        // This is what makes your "dials" work again:
        // layout is rebuilt when any relevant dial changes (cache key).
        EnsureBaseCache();

        float now = Now();

        if (force || desired != _lastDesiredCount)
        {
            if (desired > _lastDesiredCount)
            {
                int start = Mathf.Max(0, _lastDesiredCount);
                for (int i = start; i < desired && i < _spawnTimes.Length; i++)
                    _spawnTimes[i] = now;
            }
            _lastDesiredCount = desired;
        }

        sdf.Clear();

        int n = Mathf.Min(desired, _baseCenters.Length);

        float coreT = Mathf.Pow(Mathf.Clamp01(score01), Mathf.Max(0.0001f, coreRadiusScorePow));
        float coreBaseRadius = Mathf.Lerp(coreRadiusMinOS, coreRadiusMaxOS, coreT);

        for (int i = 0; i < n; i++)
        {
            float phase = i * 0.73f + seed * 0.001f;

            // jitter
            Vector3 jitter = Vector3.zero;
            if (jitterAmplitudeOS > 0.0001f)
            {
                float t = now * 1.1f;
                jitter = new Vector3(
                    Mathf.Sin(t * 1.13f + phase),
                    Mathf.Sin(t * 1.41f + phase * 1.7f) * yWobbleOS,
                    Mathf.Sin(t * 0.97f + phase * 2.1f)
                ) * jitterAmplitudeOS;
            }

            float pulse = 1f;
            if (radiusPulseAmp > 0.0001f)
                pulse = 1f + radiusPulseAmp * Mathf.Sin(now * radiusPulseSpeed + phase);

            float appear = 1f;
            if (appearSpeed > 0.0001f)
            {
                float dt = now - _spawnTimes[i];
                appear = Mathf.Clamp01(dt * appearSpeed);
            }

            Vector3 c = _baseCenters[i] + jitter;

            float baseR = _baseRadii[i];

            // Core ball scaling (index 0)
            if (i == 0 && enableCoreBallScaling)
                baseR = coreBaseRadius;

            float r = Mathf.Max(0.0005f, baseR * pulse * appear);

            sdf.AddBall(c, r);
        }

        sdf.Apply();

        lastBallCount = n;
        lastScore01 = score01;
    }
}
