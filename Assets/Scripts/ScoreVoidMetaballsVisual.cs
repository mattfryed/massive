using System;
using System.Collections;
using System.Reflection;
using Massive.Scoring;
using UnityEngine;

/// <summary>
/// World-space metaball presentation for a team's energy score.
///
/// MatchScoreService is the preferred runtime source. The metaball body grows
/// across the current engineering tier, then charges to full and collapses when
/// the team promotes to the next tier. The older reflection-based Score01 source
/// remains available as a migration/debug fallback.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class ScoreVoidMetaballsVisual : MonoBehaviour
{
    [Header("Energy Score Source (Preferred)")]
    [SerializeField] private MatchScoreService scoreService;
    [SerializeField] private bool driveFromMatchScoreService = true;
    [SerializeField] private bool autoFindScoreService = true;

    [Tooltip("1 = Light, 2 = Dark. This also remains compatible with the PostGame team override path.")]
    public int teamIDOverride = 1;

    [Tooltip("How quickly normal within-tier score changes are followed. 0 = instant.")]
    [Min(0f)]
    [SerializeField] private float scoreFollowSharpness = 18f;

    [SerializeField] private bool useUnscaledTime = true;

    [Header("Legacy Score Source (0..1 fallback)")]
    [Tooltip("Optional legacy source, such as ScoreSphereScript or anything exposing Score01/GetScore01.")]
    public MonoBehaviour scoreSource;

    [Range(0f, 1f)] public float score01Debug = 0f;
    public bool driveFromScoreSource = true;

    [Header("Tier Promotion")]
    [SerializeField] private Animator promotionAnimator;
    [SerializeField] private string promotionTrigger = "Promote";
    [SerializeField] private ParticleSystem[] promotionParticles;

    [Tooltip("Total charge-to-full and collapse duration.")]
    [Min(0f)]
    [SerializeField] private float promotionSeconds = 0.42f;

    [Tooltip("Fraction of the promotion spent charging the previous tier to full. The rest is the collapse.")]
    [Range(0.05f, 0.95f)]
    [SerializeField] private float promotionChargeFraction = 0.38f;

    [Tooltip("Temporary radius multiplier at the promotion peak.")]
    [Min(1f)]
    [SerializeField] private float promotionRadiusBurst = 1.10f;

    [Tooltip("Temporary motion/jitter multiplier at the promotion peak.")]
    [Min(1f)]
    [SerializeField] private float promotionMotionBurst = 1.55f;

    [SerializeField] private AnimationCurve promotionEase =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

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

    [Tooltip("Core ball radius when tier progress = 0")]
    [Range(0.005f, 0.49f)] public float coreRadiusMinOS = 0.10f;

    [Tooltip("Core ball radius when tier progress = 1")]
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

    [Tooltip("Legacy fallback only: tries to read teamID from scoreSource via reflection.")]
    public bool readTeamFromScoreSource = true;

    public Color team1Fill = Color.white;
    public Color team1Outline = Color.black;

    public Color team2Fill = Color.black;
    public Color team2Outline = Color.white;

    [Header("Seed")]
    public int seed = 12345;

    [Header("Debug")]
    [SerializeField] private int lastBallCount;
    [SerializeField] private float lastScore01;
    [SerializeField] private long lastRawMilliElectronVolts;
    [SerializeField] private EnergyUnit lastEnergyUnit;

    private struct Candidate
    {
        public Vector3 center;
        public float dist;
        public bool isEdge;
        public float edgeJit01;
    }

    private Vector3[] _baseCenters;
    private float[] _baseRadii;
    private float[] _spawnTimes;

    private int _cachedForMaxBalls = -1;
    private int _cacheKey;
    private int _lastDesiredCount = -1;

    private int _lastTeamID = int.MinValue;
    private Color _lastFill;
    private Color _lastOutline;

    private Coroutine _bindRoutine;
    private Coroutine _promotionRoutine;
    private bool _bound;

    private float _targetScore01;
    private float _displayScore01;
    private float _promotionRadiusMultiplier = 1f;
    private float _promotionMotionMultiplier = 1f;
    private long _rawMilliElectronVolts;
    private EnergyUnit _currentUnit;

    public float TierProgress01 => _displayScore01;
    public long RawMilliElectronVolts => _rawMilliElectronVolts;
    public EnergyUnit CurrentUnit => _currentUnit;
    public int TeamID => ResolveTeamID();

    private void OnEnable()
    {
        if (!sdf) sdf = GetComponent<MetaballSDFInstance>();
        EnsureBaseCache();

        RefreshFromFallback(instant: true);

        if (Application.isPlaying && driveFromMatchScoreService)
            BeginBinding();

        RebuildAndApply(force: true);
    }

    private void OnDisable()
    {
        Unbind();

        if (_bindRoutine != null)
        {
            StopCoroutine(_bindRoutine);
            _bindRoutine = null;
        }

        StopPromotion(resetVisualMultipliers: true);
    }

    private void OnValidate()
    {
        teamIDOverride = NormalizeTeamID(teamIDOverride);
        promotionChargeFraction = Mathf.Clamp(promotionChargeFraction, 0.05f, 0.95f);

        if (!sdf) sdf = GetComponent<MetaballSDFInstance>();
        EnsureBaseCache();

        if (!Application.isPlaying)
            RefreshFromFallback(instant: true);
        else if (_bound && scoreService != null)
            RefreshFromService(instant: true);

        RebuildAndApply(force: true);
    }

    private void Update()
    {
        if (Application.isPlaying && driveFromMatchScoreService && _bound)
        {
            if (_promotionRoutine == null)
                FollowTargetScore();
        }
        else
        {
            float fallback = ReadFallbackScore01();
            _targetScore01 = fallback;

            if (!Application.isPlaying || scoreFollowSharpness <= 0f)
                _displayScore01 = fallback;
            else if (_promotionRoutine == null)
                FollowTargetScore();
        }

        RebuildAndApply(force: false);
    }

    /// <summary>
    /// Public compatibility hook used by PostGame and inspector-driven refreshes.
    /// </summary>
    public void Refresh()
    {
        if (Application.isPlaying && driveFromMatchScoreService)
        {
            if (!_bound)
            {
                RefreshFromFallback(instant: true);
                BeginBinding();
            }
            else
            {
                RefreshFromService(instant: true);
            }
        }
        else
        {
            RefreshFromFallback(instant: true);
        }

        ApplyTeamColorsIfNeeded(force: true);
        RebuildAndApply(force: true);
    }

    public void Rebuild()
    {
        EnsureBaseCache();
        RebuildAndApply(force: true);
    }

    public void Apply()
    {
        RebuildAndApply(force: true);
    }

    public void SetTeamID(int teamID)
    {
        teamIDOverride = NormalizeTeamID(teamID);
        ApplyTeamColorsIfNeeded(force: true);

        if (_bound && scoreService != null)
            RefreshFromService(instant: true);

        RebuildAndApply(force: true);
    }

    private void BeginBinding()
    {
        if (!Application.isPlaying || !driveFromMatchScoreService || _bound)
            return;

        if (scoreService == null && autoFindScoreService)
        {
            scoreService = MatchScoreService.Instance;
            if (scoreService == null)
                scoreService = FindFirstObjectByType<MatchScoreService>();
        }

        if (scoreService != null)
        {
            Bind();
            return;
        }

        if (_bindRoutine == null)
            _bindRoutine = StartCoroutine(BindWhenAvailable());
    }

    private IEnumerator BindWhenAvailable()
    {
        while (isActiveAndEnabled && scoreService == null)
        {
            scoreService = MatchScoreService.Instance;
            if (scoreService == null && autoFindScoreService)
                scoreService = FindFirstObjectByType<MatchScoreService>();

            if (scoreService == null)
                yield return null;
        }

        _bindRoutine = null;

        if (isActiveAndEnabled && scoreService != null)
            Bind();
    }

    private void Bind()
    {
        if (_bound || scoreService == null)
            return;

        scoreService.TeamScoreChanged += OnTeamScoreChanged;
        scoreService.TierPromoted += OnTierPromoted;
        scoreService.ScoresReset += OnScoresReset;
        _bound = true;

        RefreshFromService(instant: true);
    }

    private void Unbind()
    {
        if (!_bound || scoreService == null)
            return;

        scoreService.TeamScoreChanged -= OnTeamScoreChanged;
        scoreService.TierPromoted -= OnTierPromoted;
        scoreService.ScoresReset -= OnScoresReset;
        _bound = false;
    }

    private void OnScoresReset()
    {
        StopPromotion(resetVisualMultipliers: true);
        _rawMilliElectronVolts = 0L;
        _currentUnit = EnergyUnit.MilliElectronVolt;
        _targetScore01 = 0f;
        _displayScore01 = 0f;
        RebuildAndApply(force: true);
    }

    private void OnTeamScoreChanged(TeamScoreSnapshot snapshot)
    {
        if (snapshot.teamID != TeamID)
            return;

        _rawMilliElectronVolts = Math.Max(0L, snapshot.currentMilliElectronVolts);
        _currentUnit = snapshot.currentUnit;
        _targetScore01 = Mathf.Clamp01(snapshot.tierProgress01);
    }

    private void OnTierPromoted(EnergyTierPromotion promotion)
    {
        if (promotion.teamID != TeamID)
            return;

        _rawMilliElectronVolts = Math.Max(0L, promotion.totalMilliElectronVolts);
        _currentUnit = promotion.currentUnit;
        _targetScore01 = EnergyScoreFormatter.GetTierProgress01(_rawMilliElectronVolts);

        StopPromotion(resetVisualMultipliers: true);
        _promotionRoutine = StartCoroutine(PlayPromotionRoutine());
    }

    private void RefreshFromService(bool instant)
    {
        if (scoreService == null)
        {
            RefreshFromFallback(instant);
            return;
        }

        _rawMilliElectronVolts = Math.Max(0L, scoreService.GetTeamScore(TeamID));
        _currentUnit = EnergyScoreFormatter.GetUnit(_rawMilliElectronVolts);
        _targetScore01 = EnergyScoreFormatter.GetTierProgress01(_rawMilliElectronVolts);

        if (instant)
            _displayScore01 = _targetScore01;
    }

    private void RefreshFromFallback(bool instant)
    {
        float value = ReadFallbackScore01();
        _targetScore01 = value;

        if (instant)
            _displayScore01 = value;
    }

    private void FollowTargetScore()
    {
        if (scoreFollowSharpness <= 0f)
        {
            _displayScore01 = _targetScore01;
            return;
        }

        float dt = DeltaTime();
        float t = 1f - Mathf.Exp(-scoreFollowSharpness * Mathf.Max(0f, dt));
        _displayScore01 = Mathf.Lerp(_displayScore01, _targetScore01, t);

        if (Mathf.Abs(_displayScore01 - _targetScore01) < 0.0001f)
            _displayScore01 = _targetScore01;
    }

    private IEnumerator PlayPromotionRoutine()
    {
        if (promotionAnimator != null && !string.IsNullOrWhiteSpace(promotionTrigger))
            promotionAnimator.SetTrigger(promotionTrigger);

        if (promotionParticles != null)
        {
            for (int i = 0; i < promotionParticles.Length; i++)
            {
                ParticleSystem ps = promotionParticles[i];
                if (ps != null)
                    ps.Play(true);
            }
        }

        float duration = Mathf.Max(0f, promotionSeconds);
        if (duration <= 0f)
        {
            _displayScore01 = _targetScore01;
            _promotionRadiusMultiplier = 1f;
            _promotionMotionMultiplier = 1f;
            _promotionRoutine = null;
            yield break;
        }

        float chargeSeconds = Mathf.Max(0.0001f, duration * promotionChargeFraction);
        float collapseSeconds = Mathf.Max(0.0001f, duration - chargeSeconds);
        float startProgress = Mathf.Clamp01(_displayScore01);

        float elapsed = 0f;
        while (elapsed < chargeSeconds)
        {
            elapsed += DeltaTime();
            float t = Mathf.Clamp01(elapsed / chargeSeconds);
            float eased = EvaluatePromotionEase(t);

            _displayScore01 = Mathf.Lerp(startProgress, 1f, eased);
            _promotionRadiusMultiplier = Mathf.Lerp(1f, promotionRadiusBurst, eased);
            _promotionMotionMultiplier = Mathf.Lerp(1f, promotionMotionBurst, eased);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < collapseSeconds)
        {
            elapsed += DeltaTime();
            float t = Mathf.Clamp01(elapsed / collapseSeconds);
            float eased = EvaluatePromotionEase(t);

            _displayScore01 = Mathf.Lerp(1f, 0f, eased);
            _promotionRadiusMultiplier = Mathf.Lerp(promotionRadiusBurst, 1f, eased);
            _promotionMotionMultiplier = Mathf.Lerp(promotionMotionBurst, 1f, eased);
            yield return null;
        }

        _displayScore01 = 0f;
        _promotionRadiusMultiplier = 1f;
        _promotionMotionMultiplier = 1f;
        _promotionRoutine = null;

        // Normal score following resumes on the next Update and settles toward
        // the exact progress already earned inside the destination tier.
    }

    private void StopPromotion(bool resetVisualMultipliers)
    {
        if (_promotionRoutine != null)
        {
            StopCoroutine(_promotionRoutine);
            _promotionRoutine = null;
        }

        if (resetVisualMultipliers)
        {
            _promotionRadiusMultiplier = 1f;
            _promotionMotionMultiplier = 1f;
        }
    }

    private float EvaluatePromotionEase(float t)
    {
        return promotionEase != null
            ? Mathf.Clamp01(promotionEase.Evaluate(Mathf.Clamp01(t)))
            : Smooth01(t);
    }

    private float DeltaTime()
    {
        if (!Application.isPlaying)
            return 0f;

        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private float Now()
    {
        if (!Application.isPlaying)
            return Time.realtimeSinceStartup;

        return useUnscaledTime ? Time.unscaledTime : Time.time;
    }

    private float ReadFallbackScore01()
    {
        if (!driveFromScoreSource || !scoreSource)
            return Mathf.Clamp01(score01Debug);

        Type type = scoreSource.GetType();

        PropertyInfo scoreProperty = type.GetProperty(
            "Score01",
            BindingFlags.Public | BindingFlags.Instance);

        if (scoreProperty != null && scoreProperty.PropertyType == typeof(float))
            return Mathf.Clamp01((float)scoreProperty.GetValue(scoreSource));

        MethodInfo getter = type.GetMethod(
            "GetScore01",
            BindingFlags.Public | BindingFlags.Instance);

        if (getter != null && getter.ReturnType == typeof(float))
            return Mathf.Clamp01((float)getter.Invoke(scoreSource, null));

        return Mathf.Clamp01(score01Debug);
    }

    private int ResolveTeamID()
    {
        if (readTeamFromScoreSource && scoreSource)
        {
            Type type = scoreSource.GetType();

            PropertyInfo property = type.GetProperty(
                "teamID",
                BindingFlags.Public | BindingFlags.Instance);

            if (property != null && property.PropertyType == typeof(int))
                return NormalizeTeamID((int)property.GetValue(scoreSource));

            FieldInfo field = type.GetField(
                "teamID",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null && field.FieldType == typeof(int))
                return NormalizeTeamID((int)field.GetValue(scoreSource));
        }

        return NormalizeTeamID(teamIDOverride);
    }

    private int ReadTeamID()
    {
        return ResolveTeamID();
    }

    private void ApplyTeamColorsIfNeeded(bool force)
    {
        if (!applyMaterialColors || !sdf) return;

        int teamID = ReadTeamID();
        bool isTeam2 = teamID == 2;

        Color fill = isTeam2 ? team2Fill : team1Fill;
        Color outline = isTeam2 ? team2Outline : team1Outline;

        if (!force && teamID == _lastTeamID && fill == _lastFill && outline == _lastOutline)
            return;

        _lastTeamID = teamID;
        _lastFill = fill;
        _lastOutline = outline;

        // The SDF shader exposes _LitColor, _UnlitColor, and _OutlineColor.
        // Matching lit/unlit preserves the crisp vector presentation.
        sdf.SetMaterialColors(fill, fill, outline);
    }

    private int ComputeDesiredCount(float score01)
    {
        int a = Mathf.Clamp(minBalls, 0, MetaballSDFInstance.MaxBalls);
        int b = Mathf.Clamp(maxBalls, 1, MetaballSDFInstance.MaxBalls);
        if (b < a) (a, b) = (b, a);

        float t = Mathf.Pow(Mathf.Clamp01(score01), Mathf.Max(0.0001f, scoreCurvePow));
        int desired = Mathf.RoundToInt(Mathf.Lerp(a, b, t));
        return Mathf.Clamp(desired, a, b);
    }

    private int ComputeCacheKey(int b)
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

    private void EnsureBaseCache()
    {
        int b = Mathf.Clamp(maxBalls, 1, MetaballSDFInstance.MaxBalls);
        int key = ComputeCacheKey(b);

        if (_cachedForMaxBalls == b &&
            _cacheKey == key &&
            _baseCenters != null &&
            _baseCenters.Length == b)
        {
            return;
        }

        _cachedForMaxBalls = b;
        _cacheKey = key;

        _baseCenters = new Vector3[b];
        _baseRadii = new float[b];
        _spawnTimes = new float[b];
        _lastDesiredCount = -1;

        float rr = Mathf.Clamp(regionRadiusOS - boundaryPaddingOS, 0.01f, 0.5f);
        float denom = Mathf.Max(0.0001f, rr);

        _baseCenters[0] = new Vector3(centerOS_XZ.x, ballCenterY_OS, centerOS_XZ.y);
        _baseRadii[0] = Mathf.Max(0.0005f, radiusCenterOS);
        _spawnTimes[0] = 0f;

        if (b == 1) return;

        var rng = new System.Random(seed);

        float edgeFrac = Mathf.Clamp01(edgeBallFraction);
        float bandStart = Mathf.Clamp01(edgeBandStart01);
        float edgePow = Mathf.Max(0.01f, edgeRadialBiasPow);
        float centerPow = Mathf.Max(0.01f, radialIndexPow);

        int candidateCount = b - 1;
        var candidates = new Candidate[candidateCount];

        for (int i = 0; i < candidateCount; i++)
        {
            double v = rng.NextDouble();
            float phi = (float)((v * 2.0 - 1.0) * (Math.PI * 0.5));
            float angle = halfPlane == HalfPlane.PositiveX ? phi : phi + Mathf.PI;

            bool isEdge = rng.NextDouble() < edgeFrac;
            double u = rng.NextDouble();
            float r01;

            if (isEdge)
            {
                float uEdge = 1f - Mathf.Pow((float)u, edgePow);
                r01 = Mathf.Lerp(bandStart, 1f, uEdge);
            }
            else
            {
                r01 = Mathf.Pow((float)u, centerPow);
            }

            float r = rr * r01;
            float x = Mathf.Cos(angle) * r;
            float z = Mathf.Sin(angle) * r;

            candidates[i] = new Candidate
            {
                center = new Vector3(centerOS_XZ.x + x, ballCenterY_OS, centerOS_XZ.y + z),
                dist = r,
                isEdge = isEdge,
                edgeJit01 = (float)rng.NextDouble()
            };
        }

        Array.Sort(candidates, (a, b2) => a.dist.CompareTo(b2.dist));

        float edgeJitter = Mathf.Clamp01(edgeRadiusJitter01);
        float edgeMultiplier = Mathf.Max(0.01f, edgeRadiusMul);
        float outerMultiplier = Mathf.Max(0.01f, edgeOuterRadiusMul);

        for (int i = 0; i < candidateCount; i++)
        {
            int index = i + 1;
            Candidate candidate = candidates[i];

            _baseCenters[index] = candidate.center;

            float r01 = Mathf.Clamp01(candidate.dist / denom);
            float tRad = Mathf.Pow(r01, centerPow);
            float radius = Mathf.Lerp(radiusCenterOS, radiusEdgeOS, tRad);

            if (candidate.isEdge)
            {
                radius *= edgeMultiplier;

                float jitter = Mathf.Lerp(
                    1f - edgeJitter,
                    1f + edgeJitter,
                    candidate.edgeJit01);
                radius *= jitter;

                float edgeT = bandStart >= 0.999f
                    ? 1f
                    : Mathf.InverseLerp(bandStart, 1f, r01);
                radius *= Mathf.Lerp(1f, outerMultiplier, edgeT);
            }

            _baseRadii[index] = Mathf.Max(0.0005f, radius);
            _spawnTimes[index] = 0f;
        }
    }

    private void RebuildAndApply(bool force)
    {
        if (!sdf) return;

        float score01 = Mathf.Clamp01(_displayScore01);
        ApplyTeamColorsIfNeeded(force);
        EnsureBaseCache();

        int desired = ComputeDesiredCount(score01);
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

        int count = Mathf.Min(desired, _baseCenters.Length);
        float coreT = Mathf.Pow(score01, Mathf.Max(0.0001f, coreRadiusScorePow));
        float coreBaseRadius = Mathf.Lerp(coreRadiusMinOS, coreRadiusMaxOS, coreT);

        for (int i = 0; i < count; i++)
        {
            float phase = i * 0.73f + seed * 0.001f;

            Vector3 jitter = Vector3.zero;
            if (jitterAmplitudeOS > 0.0001f)
            {
                float t = now * 1.1f;
                float motionMultiplier = _promotionMotionMultiplier;
                jitter = new Vector3(
                    Mathf.Sin(t * 1.13f + phase),
                    Mathf.Sin(t * 1.41f + phase * 1.7f) * yWobbleOS,
                    Mathf.Sin(t * 0.97f + phase * 2.1f)
                ) * (jitterAmplitudeOS * motionMultiplier);
            }

            float pulse = 1f;
            if (radiusPulseAmp > 0.0001f)
            {
                pulse = 1f +
                    radiusPulseAmp *
                    _promotionMotionMultiplier *
                    Mathf.Sin(now * radiusPulseSpeed + phase);
            }

            float appear = 1f;
            if (appearSpeed > 0.0001f)
            {
                float dt = now - _spawnTimes[i];
                appear = Mathf.Clamp01(dt * appearSpeed);
            }

            Vector3 center = _baseCenters[i] + jitter;
            float baseRadius = _baseRadii[i];

            if (i == 0 && enableCoreBallScaling)
                baseRadius = coreBaseRadius;

            float radius = Mathf.Max(
                0.0005f,
                baseRadius * pulse * appear * _promotionRadiusMultiplier);

            sdf.AddBall(center, radius);
        }

        sdf.Apply();

        lastBallCount = count;
        lastScore01 = score01;
        lastRawMilliElectronVolts = _rawMilliElectronVolts;
        lastEnergyUnit = _currentUnit;
    }

    private static int NormalizeTeamID(int teamID)
    {
        return teamID == 2 ? 2 : 1;
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
