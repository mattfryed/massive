using System;
using System.Collections;
using System.Reflection;
using Massive.Scoring;
using Shapes;
using UnityEngine;

/// <summary>
/// World-space metaball presentation for a team's energy score.
///
/// MatchScoreService is the preferred runtime source. The metaball body grows
/// across the current engineering tier, then charges to full and implodes when
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

    [Tooltip("Total charge, implosion, and compact rebirth duration.")]
    [Min(0f)]
    [SerializeField] private float promotionSeconds = 0.42f;

    [Tooltip("Fraction of the promotion spent charging the previous tier to full.")]
    [Range(0.05f, 0.95f)]
    [SerializeField] private float promotionChargeFraction = 0.38f;

    [Tooltip("Temporary radius multiplier at the promotion peak.")]
    [Min(1f)]
    [SerializeField] private float promotionRadiusBurst = 1.10f;

    [Tooltip("Temporary motion/jitter multiplier at the promotion peak.")]
    [Min(1f)]
    [SerializeField] private float promotionMotionBurst = 1.55f;

    [Tooltip("Fraction of the post-charge time spent imploding. The remainder is the next-tier rebirth.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float promotionImplosionFraction = 0.62f;

    [Tooltip("How far the full mass expands immediately before it implodes.")]
    [Range(1f, 1.5f)]
    [SerializeField] private float promotionExpansionScale = 1.08f;

    [Tooltip("How tightly ball centers converge during the implosion.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float promotionImplosionPositionScale = 0.035f;

    [Tooltip("Radius multiplier at the center of the implosion.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float promotionImplosionRadiusScale = 0.12f;

    [Tooltip("Number of revolutions made while the mass spirals into its core.")]
    [Range(0f, 2f)]
    [SerializeField] private float promotionSwirlTurns = 0.55f;

    [Tooltip("Small elastic expansion as the compact next-tier mass reforms.")]
    [Range(1f, 1.5f)]
    [SerializeField] private float promotionRebirthOvershoot = 1.06f;

    [Tooltip("Unscaled-time pause at maximum compression before the next tier reforms.")]
    [Range(0f, 0.25f)]
    [SerializeField] private float promotionSingularityHoldSeconds = 0.07f;

    [Header("Promotion Compression Ring")]
    [SerializeField] private bool enableCompressionRing = true;

    [Tooltip("Thickness relative to the authored container outline.")]
    [Range(1f, 8f)]
    [SerializeField] private float compressionRingThicknessMultiplier = 3f;

    [Range(0f, 1f)]
    [SerializeField] private float compressionRingMaxAlpha = 0.90f;

    [Tooltip("Final compression-ring radius relative to the container radius.")]
    [Range(0.005f, 0.25f)]
    [SerializeField] private float compressionRingMinRadius01 = 0.04f;

    [Header("Promotion Particle Siphon")]
    [Tooltip("How much earlier outer satellite metaballs begin converging than the main body.")]
    [Range(0f, 0.8f)]
    [SerializeField] private float promotionSiphonLead = 0.32f;

    [Tooltip("Deterministic timing variation across the inward-moving satellites.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float promotionSiphonStagger = 0.12f;

    [SerializeField] private AnimationCurve promotionEase =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Metaballs Driver")]
    public MetaballSDFInstance sdf;
    [Min(0)] public int minBalls = 4;
    [Min(1)] public int maxBalls = 24;
    [Min(0.01f)] public float scoreCurvePow = 1f;

    [Header("Dense Core and Size Variation")]
    [Tooltip("Fraction of non-primary balls explicitly clustered close to the center.")]
    [Range(0f, 1f)] public float coreClusterFraction = 0.34f;

    [Tooltip("Maximum radial reach of the compact core cluster, relative to the full region.")]
    [Range(0.05f, 1f)] public float coreClusterRadius01 = 0.42f;

    [Tooltip("Size multiplier for the smaller balls packed around the primary core.")]
    [Range(0.1f, 2f)] public float coreSatelliteRadiusMultiplier = 0.72f;

    [Tooltip("Deterministic radius variation across all non-primary balls.")]
    [Range(0f, 0.9f)] public float radiusVariation01 = 0.42f;

    [Tooltip("Extra minimum balls retained for every tier already climbed.")]
    [Range(0, 4)] public int ballsPerTier = 1;

    [Header("Score-Driven Half-Sphere Fill")]
    [Tooltip("Spatial coverage at the bottom of a tier. The mass expands to full coverage as score approaches the threshold.")]
    [Range(0.1f, 1f)] public float tierFloorCoverage01 = 0.38f;

    [Tooltip("Shapes spatial expansion over tier progress. Values above one reserve more of the expansion for high scores.")]
    [Min(0.01f)] public float scoreCoveragePow = 1.35f;

    [Tooltip("Maximum radius boost for outer satellite balls at full tier progress, used to close gaps near the shell.")]
    [Range(1f, 2f)] public float outerRadiusAtFullScoreMultiplier = 1.28f;

    [Header("Authored Container Boundary")]
    [Tooltip("The Shapes Disc/Arc that visually defines this score mass boundary.")]
    [SerializeField] private Disc containerBoundary;

    [Tooltip("Derive the metaball center and radius from the referenced container.")]
    [SerializeField] private bool driveRegionFromContainer = true;

    [Tooltip("Fit the runtime renderer volume to the container before deriving object-space geometry.")]
    [SerializeField] private bool fitRendererToContainerAtRuntime = true;

    [Tooltip("Hard-clip the final smooth-union SDF to the authored half-disc.")]
    [SerializeField] private bool clipToContainer = true;

    [Tooltip("Additional world-space gap inside the inner edge of the container outline.")]
    [Min(0f)]
    [SerializeField] private float containerInsetWorld;

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

    [Header("Near-Threshold Energy")]
    [Tooltip("Tier progress at which the mass starts visibly charging toward promotion.")]
    [Range(0f, 0.99f)] public float nearThresholdStart01 = 0.76f;

    [Tooltip("Motion multiplier reached at the tier threshold.")]
    [Range(1f, 5f)] public float nearThresholdMotionMultiplier = 2.10f;

    [Tooltip("Pulse-amplitude multiplier reached at the tier threshold.")]
    [Range(1f, 5f)] public float nearThresholdPulseMultiplier = 1.70f;

    [Tooltip("Pulse-speed multiplier reached at the tier threshold.")]
    [Range(1f, 5f)] public float nearThresholdPulseSpeedMultiplier = 1.85f;

    [Tooltip("Small tangential displacement that makes near-threshold balls churn around the core.")]
    [Range(0f, 0.1f)] public float nearThresholdOrbitOS = 0.018f;

    [Tooltip("Additional baseline energy gained per completed score tier.")]
    [Range(0f, 0.5f)] public float tierEnergyPerUnit = 0.09f;

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
        public bool isCoreCluster;
        public float edgeJit01;
        public float sizeJit01;
    }

    private Vector3[] _baseCenters;
    private float[] _baseRadii;
    private float[] _baseShell01;
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
    private Disc _compressionRing;
    private int _containerStateKey = int.MinValue;
    private float _containerRadiusOS = 0.5f;
    private float _containerInnerRadiusWorld;

    private float _targetScore01;
    private float _displayScore01;
    private float _promotionRadiusMultiplier = 1f;
    private float _promotionMotionMultiplier = 1f;
    private float _promotionPositionScale = 1f;
    private float _promotionSpinRadians;
    private float _promotionSiphon01;
    private bool _promotionSiphonActive;
    private long _rawMilliElectronVolts;
    private EnergyUnit _currentUnit;
    private bool _scorePresentationPreviewActive;
    private long _scoreBeforePresentationPreview;
    private long _previewScore;

    public float TierProgress01 => _displayScore01;
    public long RawMilliElectronVolts => _rawMilliElectronVolts;
    public EnergyUnit CurrentUnit => _currentUnit;
    public int TeamID => ResolveTeamID();
    public bool IsScorePresentationPreviewActive => _scorePresentationPreviewActive;
    public int BallCount => lastBallCount;
    public bool IsPromoting => _promotionRoutine != null;
    public float SpatialCoverageScale => EvaluateSpatialCoverage(_displayScore01);
    public Disc ContainerBoundary => containerBoundary;
    public float ContainerRadiusOS => _containerRadiusOS;

    private void OnEnable()
    {
        if (!sdf) sdf = GetComponent<MetaballSDFInstance>();
        if (Application.isPlaying && fitRendererToContainerAtRuntime)
            FitRendererToContainer();
        SyncContainerBoundary(force: true);
        EnsureBaseCache();

        if (Application.isPlaying)
            EnsureCompressionRing();

        RefreshFromFallback(instant: true);

        if (Application.isPlaying && driveFromMatchScoreService)
            BeginBinding();

        RebuildAndApply(force: true);
    }

    private void OnDisable()
    {
        _scorePresentationPreviewActive = false;
        Unbind();

        if (_bindRoutine != null)
        {
            StopCoroutine(_bindRoutine);
            _bindRoutine = null;
        }

        StopPromotion(resetVisualMultipliers: true);
        DestroyCompressionRing();
    }

    private void OnValidate()
    {
        teamIDOverride = NormalizeTeamID(teamIDOverride);
        promotionChargeFraction = Mathf.Clamp(promotionChargeFraction, 0.05f, 0.95f);
        promotionImplosionFraction = Mathf.Clamp(promotionImplosionFraction, 0.1f, 0.9f);

        if (!sdf) sdf = GetComponent<MetaballSDFInstance>();
        SyncContainerBoundary(force: true);
        EnsureBaseCache();

        if (!Application.isPlaying)
            RefreshFromFallback(instant: true);
        else if (_bound && scoreService != null && !_scorePresentationPreviewActive)
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

    /// <summary>
    /// Temporarily detaches this visual from live score events without changing
    /// the authoritative MatchScoreService total.
    /// </summary>
    public void BeginScorePresentationPreview()
    {
        if (!Application.isPlaying || _scorePresentationPreviewActive)
            return;

        _scoreBeforePresentationPreview = _rawMilliElectronVolts;
        _previewScore = _rawMilliElectronVolts;
        _scorePresentationPreviewActive = true;
        StopPromotion(resetVisualMultipliers: true);
    }

    /// <summary>
    /// Applies an isolated preview score. Upward tier crossings use the same
    /// charge, implosion, and compact-rebirth animation as gameplay.
    /// </summary>
    public void PreviewScorePresentation(long rawMilliElectronVolts, bool animateChanges)
    {
        if (!Application.isPlaying)
            return;

        BeginScorePresentationPreview();

        long nextScore = Math.Max(0L, rawMilliElectronVolts);
        EnergyUnit previousUnit = EnergyScoreFormatter.GetUnit(_previewScore);
        EnergyUnit nextUnit = EnergyScoreFormatter.GetUnit(nextScore);
        bool promoted = nextUnit > previousUnit;

        _previewScore = nextScore;
        _rawMilliElectronVolts = nextScore;
        _currentUnit = nextUnit;
        _targetScore01 = EnergyScoreFormatter.GetTierProgress01(nextScore);

        if (!animateChanges || nextUnit < previousUnit)
        {
            StopPromotion(resetVisualMultipliers: true);
            _displayScore01 = _targetScore01;
        }
        else if (promoted)
        {
            StopPromotion(resetVisualMultipliers: true);
            _promotionRoutine = StartCoroutine(PlayPromotionRoutine());
        }

        RebuildAndApply(force: true);
    }

    public void EndScorePresentationPreview()
    {
        if (!_scorePresentationPreviewActive)
            return;

        _scorePresentationPreviewActive = false;
        StopPromotion(resetVisualMultipliers: true);

        if (scoreService != null)
            RefreshFromService(instant: true);
        else
        {
            _rawMilliElectronVolts = _scoreBeforePresentationPreview;
            _currentUnit = EnergyScoreFormatter.GetUnit(_rawMilliElectronVolts);
            _targetScore01 = EnergyScoreFormatter.GetTierProgress01(_rawMilliElectronVolts);
            _displayScore01 = _targetScore01;
        }

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
        if (_scorePresentationPreviewActive)
            return;

        StopPromotion(resetVisualMultipliers: true);
        _rawMilliElectronVolts = 0L;
        _currentUnit = EnergyUnit.MilliElectronVolt;
        _targetScore01 = 0f;
        _displayScore01 = 0f;
        RebuildAndApply(force: true);
    }

    private void OnTeamScoreChanged(TeamScoreSnapshot snapshot)
    {
        if (_scorePresentationPreviewActive || snapshot.teamID != TeamID)
            return;

        _rawMilliElectronVolts = Math.Max(0L, snapshot.currentMilliElectronVolts);
        _currentUnit = snapshot.currentUnit;
        _targetScore01 = Mathf.Clamp01(snapshot.tierProgress01);
    }

    private void OnTierPromoted(EnergyTierPromotion promotion)
    {
        if (_scorePresentationPreviewActive || promotion.teamID != TeamID)
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
        HideCompressionRing();
        _promotionSiphonActive = false;
        _promotionSiphon01 = 0f;

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
            _promotionPositionScale = 1f;
            _promotionSpinRadians = 0f;
            _promotionSiphonActive = false;
            _promotionSiphon01 = 0f;
            _promotionRoutine = null;
            yield break;
        }

        float chargeSeconds = Mathf.Max(0.0001f, duration * promotionChargeFraction);
        float postChargeSeconds = Mathf.Max(0.0002f, duration - chargeSeconds);
        float implosionSeconds = Mathf.Max(0.0001f, postChargeSeconds * promotionImplosionFraction);
        float rebirthSeconds = Mathf.Max(0.0001f, postChargeSeconds - implosionSeconds);
        float startProgress = Mathf.Clamp01(_displayScore01);
        float destinationProgress = Mathf.Clamp01(_targetScore01);
        float tierIntensity = GetTierEnergyMultiplier();
        float peakMotion = promotionMotionBurst * tierIntensity;
        float implosionMotion = peakMotion * 1.35f;
        float finalSpin = promotionSwirlTurns * Mathf.PI * 2f;

        float elapsed = 0f;
        while (elapsed < chargeSeconds)
        {
            elapsed += DeltaTime();
            float t = Mathf.Clamp01(elapsed / chargeSeconds);
            float eased = EvaluatePromotionEase(t);

            _displayScore01 = Mathf.Lerp(startProgress, 1f, eased);
            _promotionRadiusMultiplier = Mathf.Lerp(1f, promotionRadiusBurst, eased);
            _promotionMotionMultiplier = Mathf.Lerp(1f, peakMotion, eased);
            _promotionPositionScale = Mathf.Lerp(1f, promotionExpansionScale, eased);
            _promotionSpinRadians = 0f;
            _promotionSiphonActive = false;
            _promotionSiphon01 = 0f;
            yield return null;
        }

        _promotionSiphonActive = true;
        elapsed = 0f;
        while (elapsed < implosionSeconds)
        {
            elapsed += DeltaTime();
            float t = Mathf.Clamp01(elapsed / implosionSeconds);
            float eased = EvaluatePromotionEase(t);

            // Keep the previous tier's complete population visible until the
            // convergence itself removes it; reducing score progress here would
            // make balls disappear before the implosion could be read.
            _displayScore01 = 1f;
            _promotionRadiusMultiplier = Mathf.Lerp(
                promotionRadiusBurst,
                promotionImplosionRadiusScale,
                eased);
            _promotionMotionMultiplier = Mathf.Lerp(peakMotion, implosionMotion, eased);
            _promotionPositionScale = Mathf.Lerp(
                promotionExpansionScale,
                promotionImplosionPositionScale,
                eased);
            _promotionSpinRadians = finalSpin * eased;
            _promotionSiphon01 = eased;

            float ringFadeIn = Smooth01(Mathf.InverseLerp(0f, 0.14f, t));
            float ringSettle = Mathf.Lerp(
                1f,
                0.65f,
                Smooth01(Mathf.InverseLerp(0.72f, 1f, t)));
            SetCompressionRingVisual(1f - eased, ringFadeIn * ringSettle);
            yield return null;
        }

        float holdSeconds = Mathf.Max(0f, promotionSingularityHoldSeconds);
        elapsed = 0f;
        while (elapsed < holdSeconds)
        {
            elapsed += DeltaTime();
            float t = holdSeconds > 0.0001f
                ? Mathf.Clamp01(elapsed / holdSeconds)
                : 1f;

            _displayScore01 = 1f;
            _promotionRadiusMultiplier = promotionImplosionRadiusScale;
            _promotionMotionMultiplier = implosionMotion;
            _promotionPositionScale = promotionImplosionPositionScale;
            _promotionSpinRadians = finalSpin;
            _promotionSiphonActive = true;
            _promotionSiphon01 = 1f;
            SetCompressionRingVisual(0f, Mathf.Lerp(0.65f, 0f, Smooth01(t)));
            yield return null;
        }

        HideCompressionRing();
        _promotionSiphonActive = false;
        _promotionSiphon01 = 0f;
        elapsed = 0f;
        while (elapsed < rebirthSeconds)
        {
            elapsed += DeltaTime();
            float t = Mathf.Clamp01(elapsed / rebirthSeconds);
            float eased = EvaluatePromotionEase(t);
            float elastic = Mathf.Sin(t * Mathf.PI) * (promotionRebirthOvershoot - 1f);

            _displayScore01 = destinationProgress;
            _promotionRadiusMultiplier =
                Mathf.Lerp(promotionImplosionRadiusScale, 1f, eased) * (1f + elastic);
            _promotionMotionMultiplier = Mathf.Lerp(implosionMotion, 1f, eased);
            _promotionPositionScale =
                Mathf.Lerp(promotionImplosionPositionScale, 1f, eased) * (1f + elastic);
            _promotionSpinRadians = finalSpin * (1f - eased);
            yield return null;
        }

        _displayScore01 = destinationProgress;
        _promotionRadiusMultiplier = 1f;
        _promotionMotionMultiplier = 1f;
        _promotionPositionScale = 1f;
        _promotionSpinRadians = 0f;
        _promotionSiphonActive = false;
        _promotionSiphon01 = 0f;
        HideCompressionRing();
        _promotionRoutine = null;
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
            _promotionPositionScale = 1f;
            _promotionSpinRadians = 0f;
            _promotionSiphonActive = false;
            _promotionSiphon01 = 0f;
            HideCompressionRing();
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

    private void FitRendererToContainer()
    {
        if (containerBoundary == null)
            return;

        float innerRadiusWorld = GetBoundaryInnerRadiusWorld();
        if (innerRadiusWorld <= 0.0001f)
            return;

        float targetDiameterWorld = innerRadiusWorld * 2f;
        Vector3 localScale = transform.localScale;
        float currentWorldX = transform.TransformVector(Vector3.right).magnitude;
        float currentWorldZ = transform.TransformVector(Vector3.forward).magnitude;

        if (currentWorldX > 0.0001f)
            localScale.x *= targetDiameterWorld / currentWorldX;
        if (currentWorldZ > 0.0001f)
            localScale.z *= targetDiameterWorld / currentWorldZ;

        transform.localScale = localScale;

        Vector3 outward = halfPlane == HalfPlane.NegativeX
            ? -transform.right
            : transform.right;
        outward.y = 0f;
        if (outward.sqrMagnitude > 0.0001f)
            outward.Normalize();

        Vector3 desiredPosition = containerBoundary.transform.position + outward * innerRadiusWorld;
        desiredPosition.y = transform.position.y;
        transform.position = desiredPosition;
        _containerStateKey = int.MinValue;
    }

    private float GetBoundaryInnerRadiusWorld()
    {
        if (containerBoundary == null || containerBoundary.Radius <= 0.0001f)
            return 0f;

        Transform boundaryTransform = containerBoundary.transform;
        float radiusX = boundaryTransform.TransformVector(
            Vector3.right * containerBoundary.Radius).magnitude;
        float radiusY = boundaryTransform.TransformVector(
            Vector3.up * containerBoundary.Radius).magnitude;
        float centerlineRadiusWorld = (radiusX + radiusY) * 0.5f;
        float shapeScale = centerlineRadiusWorld / containerBoundary.Radius;
        float thicknessWorld = containerBoundary.Thickness * shapeScale;

        return Mathf.Max(
            0.0001f,
            centerlineRadiusWorld - thicknessWorld * 0.5f - Mathf.Max(0f, containerInsetWorld));
    }

    private int ComputeContainerStateKey()
    {
        if (containerBoundary == null)
            return 0;

        unchecked
        {
            Matrix4x4 relative = transform.worldToLocalMatrix *
                containerBoundary.transform.localToWorldMatrix;
            int h = 17;
            h = h * 31 + relative.GetHashCode();
            h = h * 31 + containerBoundary.Radius.GetHashCode();
            h = h * 31 + containerBoundary.Thickness.GetHashCode();
            h = h * 31 + containerInsetWorld.GetHashCode();
            h = h * 31 + (int)halfPlane;
            h = h * 31 + (driveRegionFromContainer ? 1 : 0);
            h = h * 31 + (clipToContainer ? 1 : 0);
            return h;
        }
    }

    private void SyncContainerBoundary(bool force)
    {
        if (!sdf)
            return;

        if (containerBoundary == null)
        {
            _containerRadiusOS = Mathf.Clamp(regionRadiusOS, 0.01f, 0.5f);
            sdf.SetContainerClip(centerOS_XZ, _containerRadiusOS, 1f, false);
            return;
        }

        int stateKey = ComputeContainerStateKey();
        if (!force && stateKey == _containerStateKey)
            return;

        _containerStateKey = stateKey;
        Transform boundaryTransform = containerBoundary.transform;
        Vector3 centerOS = transform.InverseTransformPoint(boundaryTransform.position);

        float radiusXOS = transform.InverseTransformVector(
            boundaryTransform.TransformVector(Vector3.right * containerBoundary.Radius)).magnitude;
        float radiusYOS = transform.InverseTransformVector(
            boundaryTransform.TransformVector(Vector3.up * containerBoundary.Radius)).magnitude;
        float centerlineRadiusOS = (radiusXOS + radiusYOS) * 0.5f;
        float shapeScaleOS = containerBoundary.Radius > 0.0001f
            ? centerlineRadiusOS / containerBoundary.Radius
            : 0f;
        float thicknessOS = containerBoundary.Thickness * shapeScaleOS;
        float worldPerObjectUnit = (
            transform.TransformVector(Vector3.right).magnitude +
            transform.TransformVector(Vector3.forward).magnitude) * 0.5f;
        float insetOS = worldPerObjectUnit > 0.0001f
            ? Mathf.Max(0f, containerInsetWorld) / worldPerObjectUnit
            : 0f;
        float renderableRadiusOS = Mathf.Max(0.01f, 0.5f - Mathf.Abs(centerOS.z));

        _containerRadiusOS = Mathf.Clamp(
            centerlineRadiusOS - thicknessOS * 0.5f - insetOS,
            0.01f,
            renderableRadiusOS);
        _containerInnerRadiusWorld = GetBoundaryInnerRadiusWorld();

        Vector2 derivedCenter = new Vector2(centerOS.x, centerOS.z);
        if (driveRegionFromContainer)
        {
            bool regionChanged = centerOS_XZ != derivedCenter ||
                !Mathf.Approximately(regionRadiusOS, _containerRadiusOS);
            centerOS_XZ = derivedCenter;
            regionRadiusOS = _containerRadiusOS;
            if (regionChanged)
                _cachedForMaxBalls = -1;
        }

        float halfPlaneSign = halfPlane == HalfPlane.NegativeX ? 1f : -1f;
        sdf.SetContainerClip(
            derivedCenter,
            _containerRadiusOS,
            halfPlaneSign,
            clipToContainer);
    }

    private void EnsureCompressionRing()
    {
        if (!Application.isPlaying || !enableCompressionRing ||
            containerBoundary == null || _compressionRing != null)
        {
            return;
        }

        _compressionRing = Instantiate(
            containerBoundary,
            containerBoundary.transform.parent);
        _compressionRing.name = containerBoundary.name + " (PROMOTION COMPRESSION)";
        _compressionRing.gameObject.hideFlags = HideFlags.DontSave;
        _compressionRing.transform.SetPositionAndRotation(
            containerBoundary.transform.position,
            containerBoundary.transform.rotation);
        _compressionRing.transform.localScale = containerBoundary.transform.localScale;
        _compressionRing.SortingOrder = containerBoundary.SortingOrder + 1;
        _compressionRing.gameObject.SetActive(false);
    }

    private void SetCompressionRingVisual(float normalizedRadius, float alpha)
    {
        EnsureCompressionRing();
        if (_compressionRing == null)
            return;

        float sourceRadius = Mathf.Max(0.0001f, containerBoundary.Radius);
        float radiusWorld = Mathf.Max(0.0001f, GetBoundaryInnerRadiusWorld());
        float centerlineWorld = containerBoundary.transform.TransformVector(
            Vector3.right * sourceRadius).magnitude;
        float localPerWorld = centerlineWorld > 0.0001f
            ? sourceRadius / centerlineWorld
            : 1f;
        float ringThickness = Mathf.Max(
            0.001f,
            containerBoundary.Thickness * compressionRingThicknessMultiplier);
        float maximumRadius = Mathf.Max(
            ringThickness * 0.5f,
            radiusWorld * localPerWorld - ringThickness * 0.5f);

        _compressionRing.Radius = Mathf.Lerp(
            maximumRadius * compressionRingMinRadius01,
            maximumRadius,
            Mathf.Clamp01(normalizedRadius));
        _compressionRing.Thickness = ringThickness;

        Color color = TeamID == 2 ? team2Outline : team1Outline;
        color.a *= Mathf.Clamp01(alpha) * compressionRingMaxAlpha;
        _compressionRing.Color = color;

        if (!_compressionRing.gameObject.activeSelf)
            _compressionRing.gameObject.SetActive(true);
    }

    private void HideCompressionRing()
    {
        if (_compressionRing != null)
            _compressionRing.gameObject.SetActive(false);
    }

    private void DestroyCompressionRing()
    {
        if (_compressionRing == null)
            return;

        if (Application.isPlaying)
            Destroy(_compressionRing.gameObject);
        else
            DestroyImmediate(_compressionRing.gameObject);

        _compressionRing = null;
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
        int tierBonus = Mathf.Max(0, (int)_currentUnit) * Mathf.Max(0, ballsPerTier);
        int a = Mathf.Clamp(minBalls + tierBonus, 0, MetaballSDFInstance.MaxBalls);
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
            h = h * 31 + coreClusterFraction.GetHashCode();
            h = h * 31 + coreClusterRadius01.GetHashCode();
            h = h * 31 + coreSatelliteRadiusMultiplier.GetHashCode();
            h = h * 31 + radiusVariation01.GetHashCode();
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
            _baseCenters.Length == b &&
            _baseShell01 != null &&
            _baseShell01.Length == b)
        {
            return;
        }

        _cachedForMaxBalls = b;
        _cacheKey = key;

        _baseCenters = new Vector3[b];
        _baseRadii = new float[b];
        _baseShell01 = new float[b];
        _spawnTimes = new float[b];
        _lastDesiredCount = -1;

        float rr = Mathf.Clamp(regionRadiusOS - boundaryPaddingOS, 0.01f, 0.5f);
        float denom = Mathf.Max(0.0001f, rr);

        _baseCenters[0] = new Vector3(centerOS_XZ.x, ballCenterY_OS, centerOS_XZ.y);
        _baseRadii[0] = Mathf.Max(0.0005f, radiusCenterOS);
        _baseShell01[0] = 0f;
        _spawnTimes[0] = 0f;

        if (b == 1) return;

        var rng = new System.Random(seed);

        float edgeFrac = Mathf.Clamp01(edgeBallFraction);
        float bandStart = Mathf.Clamp01(edgeBandStart01);
        float edgePow = Mathf.Max(0.01f, edgeRadialBiasPow);
        float centerPow = Mathf.Max(0.01f, radialIndexPow);
        float clusterFraction = Mathf.Clamp01(coreClusterFraction);
        float clusterRadius = Mathf.Clamp(coreClusterRadius01, 0.05f, 1f);

        int candidateCount = b - 1;
        var candidates = new Candidate[candidateCount];

        for (int i = 0; i < candidateCount; i++)
        {
            double v = rng.NextDouble();
            float phi = (float)((v * 2.0 - 1.0) * (Math.PI * 0.5));
            float angle = halfPlane == HalfPlane.PositiveX ? phi : phi + Mathf.PI;

            bool isCoreCluster = rng.NextDouble() < clusterFraction;
            bool isEdge = !isCoreCluster && rng.NextDouble() < edgeFrac;
            double u = rng.NextDouble();
            float r01;

            if (isCoreCluster)
            {
                // A compact, center-biased population remains visible even at
                // the bottom of a tier, keeping the energetic core dense.
                r01 = Mathf.Pow((float)u, 1.8f) * clusterRadius;
            }
            else if (isEdge)
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
                isCoreCluster = isCoreCluster,
                edgeJit01 = (float)rng.NextDouble(),
                sizeJit01 = (float)rng.NextDouble()
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
            _baseShell01[index] = r01;
            float tRad = Mathf.Pow(r01, centerPow);
            float radius = Mathf.Lerp(radiusCenterOS, radiusEdgeOS, tRad);
            float variation = Mathf.Clamp01(radiusVariation01);
            radius *= Mathf.Lerp(1f - variation, 1f + variation, candidate.sizeJit01);

            if (candidate.isCoreCluster)
                radius *= Mathf.Max(0.01f, coreSatelliteRadiusMultiplier);

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

        SyncContainerBoundary(force);
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
        float thresholdEnergy01 = _promotionRoutine == null
            ? Smooth01(Mathf.InverseLerp(nearThresholdStart01, 1f, score01))
            : 0f;
        float tierEnergy = GetTierEnergyMultiplier();
        float ambientTierEnergy = _promotionRoutine == null ? tierEnergy : 1f;
        float motionEnergy = ambientTierEnergy * Mathf.Lerp(
            1f,
            Mathf.Max(1f, nearThresholdMotionMultiplier),
            thresholdEnergy01);
        float pulseEnergy = ambientTierEnergy * Mathf.Lerp(
            1f,
            Mathf.Max(1f, nearThresholdPulseMultiplier),
            thresholdEnergy01);
        float pulseSpeed = radiusPulseSpeed * Mathf.Lerp(
            1f,
            Mathf.Max(1f, nearThresholdPulseSpeedMultiplier),
            thresholdEnergy01);
        float scoreFill01 = Mathf.Pow(score01, Mathf.Max(0.01f, scoreCoveragePow));
        float spatialCoverage = EvaluateSpatialCoverage(score01);
        Vector3 coreCenter = new Vector3(centerOS_XZ.x, ballCenterY_OS, centerOS_XZ.y);

        for (int i = 0; i < count; i++)
        {
            float phase = i * 0.73f + seed * 0.001f;

            Vector3 jitter = Vector3.zero;
            if (jitterAmplitudeOS > 0.0001f)
            {
                float t = now * 1.1f;
                float motionMultiplier = _promotionMotionMultiplier * motionEnergy;
                jitter = new Vector3(
                    Mathf.Sin(t * 1.13f + phase),
                    Mathf.Sin(t * 1.41f + phase * 1.7f) * yWobbleOS,
                    Mathf.Sin(t * 0.97f + phase * 2.1f)
                ) * (jitterAmplitudeOS * motionMultiplier);

                // Preserve a lively singularity without letting random motion
                // overwhelm the visible inward convergence.
                float convergenceJitter = Mathf.Lerp(
                    0.25f,
                    1f,
                    Mathf.Clamp01(_promotionPositionScale));
                jitter *= convergenceJitter;
            }

            float pulse = 1f;
            if (radiusPulseAmp > 0.0001f)
            {
                pulse = 1f +
                    radiusPulseAmp *
                    _promotionMotionMultiplier * pulseEnergy *
                    Mathf.Sin(now * pulseSpeed + phase);
            }

            float appear = 1f;
            if (appearSpeed > 0.0001f)
            {
                float dt = now - _spawnTimes[i];
                appear = Mathf.Clamp01(dt * appearSpeed);
            }

            Vector3 baseOffset = (_baseCenters[i] - coreCenter) * spatialCoverage;
            float siphonProgress = EvaluateSiphonProgress(i);
            Vector3 shapedOffset;

            if (_promotionSiphonActive && i > 0)
            {
                float timingJitter = Hash01(i + seed);
                float localScale = Mathf.Lerp(
                    promotionExpansionScale,
                    promotionImplosionPositionScale,
                    siphonProgress);
                float localSpin = promotionSwirlTurns * Mathf.PI * 2f *
                    siphonProgress * Mathf.Lerp(0.85f, 1.15f, timingJitter);
                shapedOffset = RotateOffsetXZ(baseOffset, localSpin) * localScale;
            }
            else
            {
                shapedOffset = RotateOffsetXZ(baseOffset, _promotionSpinRadians) *
                    _promotionPositionScale;
            }

            if (thresholdEnergy01 > 0.0001f && baseOffset.sqrMagnitude > 0.000001f)
            {
                Vector3 tangent = new Vector3(-baseOffset.z, 0f, baseOffset.x).normalized;
                shapedOffset += tangent *
                    (Mathf.Sin(now * pulseSpeed * 1.37f + phase) *
                     nearThresholdOrbitOS * thresholdEnergy01 * tierEnergy);
            }

            Vector3 center = coreCenter + shapedOffset + jitter;
            float baseRadius = _baseRadii[i];

            if (i == 0 && enableCoreBallScaling)
                baseRadius = coreBaseRadius;
            else
            {
                float shellWeightedFill = scoreFill01 *
                    Mathf.Lerp(0.35f, 1f, _baseShell01[i]);
                baseRadius *= Mathf.Lerp(
                    1f,
                    Mathf.Max(1f, outerRadiusAtFullScoreMultiplier),
                    shellWeightedFill);
            }

            float promotionRadius = _promotionRadiusMultiplier;
            if (_promotionSiphonActive && i > 0)
            {
                promotionRadius = Mathf.Lerp(
                    promotionRadiusBurst,
                    promotionImplosionRadiusScale,
                    siphonProgress);
            }

            float radius = Mathf.Max(
                0.0005f,
                baseRadius * pulse * appear * promotionRadius);

            sdf.AddBall(center, radius);
        }

        sdf.Apply();

        lastBallCount = count;
        lastScore01 = score01;
        lastRawMilliElectronVolts = _rawMilliElectronVolts;
        lastEnergyUnit = _currentUnit;
    }

    private float GetTierEnergyMultiplier()
    {
        int tierIndex = Mathf.Clamp((int)_currentUnit, 0, (int)EnergyUnit.TeraElectronVolt);
        return 1f + tierIndex * Mathf.Max(0f, tierEnergyPerUnit);
    }

    private float EvaluateSpatialCoverage(float score01)
    {
        float fill = Mathf.Pow(
            Mathf.Clamp01(score01),
            Mathf.Max(0.01f, scoreCoveragePow));
        return Mathf.Lerp(Mathf.Clamp01(tierFloorCoverage01), 1f, fill);
    }

    private float EvaluateSiphonProgress(int index)
    {
        if (!_promotionSiphonActive || index <= 0)
            return 0f;

        float shell01 = _baseShell01 != null && index < _baseShell01.Length
            ? Mathf.Clamp01(_baseShell01[index])
            : 0f;
        float timingJitter = Hash01(index + seed);
        float start = (1f - shell01) * promotionSiphonLead +
            timingJitter * promotionSiphonStagger;
        float end = 1f - shell01 * promotionSiphonLead +
            timingJitter * promotionSiphonStagger * 0.25f;
        end = Mathf.Max(start + 0.05f, end);
        return Smooth01(Mathf.InverseLerp(start, end, _promotionSiphon01));
    }

    private static float Hash01(int value)
    {
        float hash = Mathf.Sin(value * 12.9898f) * 43758.5453f;
        return hash - Mathf.Floor(hash);
    }

    private static Vector3 RotateOffsetXZ(Vector3 offset, float radians)
    {
        if (Mathf.Abs(radians) < 0.0001f)
            return offset;

        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector3(
            offset.x * cos - offset.z * sin,
            offset.y,
            offset.x * sin + offset.z * cos);
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
