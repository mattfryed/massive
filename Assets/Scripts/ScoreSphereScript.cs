using System;
using System.Collections;
using Massive.Scoring;
using UnityEngine;

/// <summary>
/// Presentation-only score void/sphere driver. MatchScoreService owns score.
/// The visual grows across the current engineering tier, bursts on promotion,
/// then collapses to the beginning of the next tier.
/// </summary>
[DisallowMultipleComponent]
public class ScoreSphereScript : MonoBehaviour
{
    [Header("Identity")]
    public int teamID = 1;

    [Header("Score Source")]
    [SerializeField] private MatchScoreService scoreService;

    [Header("Visual")]
    [SerializeField] private GameObject sphereGraphic;
    [SerializeField, Min(0f)] private float minSize = 0.15f;
    [SerializeField, Min(0.01f)] private float maxSize = 11f;
    [SerializeField] private AnimationCurve progressToScale = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Min(0f)] private float followSharpness = 16f;

    [Header("Tier Promotion")]
    [SerializeField] private Animator promotionAnimator;
    [SerializeField] private string promotionTrigger = "Promote";
    [SerializeField, Min(0f)] private float promotionSeconds = 0.28f;
    [SerializeField, Min(1f)] private float promotionOvershoot = 1.08f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Optional Legacy Scoreboard Object")]
    [Tooltip("Only used to auto-locate a ScoreboardManagerScript. The scoreboard subscribes to MatchScoreService directly.")]
    [SerializeField] private GameObject scoreboard;

    private Coroutine _bindRoutine;
    private Coroutine _promotionRoutine;
    private Vector3 _targetScale;
    private float _tierProgress01;
    private long _rawScore;
    private EnergyUnit _unit;
    private bool _bound;
    private bool _warnedLegacyApi;
    private bool _scorePresentationPreviewActive;
    private long _scoreBeforePresentationPreview;
    private long _previewScore;

    [Obsolete("Score01 now means current-tier visual progress only. Use MatchScoreService for score data.")]
    public float Score01 => _tierProgress01;
    public float TierProgress01 => _tierProgress01;
    public float MaxSize => maxSize;
    public long RawMilliElectronVolts => _rawScore;
    public EnergyUnit CurrentUnit => _unit;
    public bool IsScorePresentationPreviewActive => _scorePresentationPreviewActive;

    private Transform GraphicTransform => sphereGraphic != null ? sphereGraphic.transform : transform;

    private void OnEnable()
    {
        _targetScale = Vector3.one * Mathf.Max(0f, minSize);
        GraphicTransform.localScale = _targetScale;
        if (_bindRoutine == null)
            _bindRoutine = StartCoroutine(BindWhenAvailable());
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

        if (_promotionRoutine != null)
        {
            StopCoroutine(_promotionRoutine);
            _promotionRoutine = null;
        }
    }

    private void Update()
    {
        if (_promotionRoutine != null)
            return;

        Transform graphic = GraphicTransform;
        float t = followSharpness <= 0f
            ? 1f
            : 1f - Mathf.Exp(-followSharpness * (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime));

        graphic.localScale = Vector3.Lerp(graphic.localScale, _targetScale, t);
    }

    /// <summary>
    /// Temporarily detaches this presentation from live score events without
    /// changing the authoritative MatchScoreService total.
    /// </summary>
    public void BeginScorePresentationPreview()
    {
        if (!Application.isPlaying || _scorePresentationPreviewActive)
            return;

        _scoreBeforePresentationPreview = _rawScore;
        _previewScore = _rawScore;
        _scorePresentationPreviewActive = true;

        if (_promotionRoutine != null)
        {
            StopCoroutine(_promotionRoutine);
            _promotionRoutine = null;
        }
    }

    /// <summary>
    /// Applies an isolated preview score. Upward tier crossings use the same
    /// promotion burst as gameplay; downward scrubs settle immediately.
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

        if ((!animateChanges || nextUnit < previousUnit) && _promotionRoutine != null)
        {
            StopCoroutine(_promotionRoutine);
            _promotionRoutine = null;
        }

        ApplyScore(nextScore, instant: !animateChanges || nextUnit < previousUnit);

        if (animateChanges && promoted)
        {
            if (_promotionRoutine != null)
                StopCoroutine(_promotionRoutine);

            _promotionRoutine = StartCoroutine(PlayPromotionRoutine());
        }
    }

    public void EndScorePresentationPreview()
    {
        if (!_scorePresentationPreviewActive)
            return;

        _scorePresentationPreviewActive = false;

        if (_promotionRoutine != null)
        {
            StopCoroutine(_promotionRoutine);
            _promotionRoutine = null;
        }

        long liveScore = scoreService != null
            ? scoreService.GetTeamScore(teamID)
            : _scoreBeforePresentationPreview;
        ApplyScore(liveScore, instant: true);
    }

    private IEnumerator BindWhenAvailable()
    {
        while (isActiveAndEnabled && scoreService == null)
        {
            scoreService = MatchScoreService.Instance;
            if (scoreService == null)
                scoreService = FindFirstObjectByType<MatchScoreService>();

            if (scoreService == null)
                yield return null;
        }

        _bindRoutine = null;
        if (!isActiveAndEnabled || scoreService == null)
            yield break;

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

        ScoreboardManagerScript manager = scoreboard != null
            ? scoreboard.GetComponent<ScoreboardManagerScript>()
            : null;
        if (manager != null)
            manager.SetTeamID(teamID);

        ApplyScore(scoreService.GetTeamScore(teamID), instant: true);
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

        ApplyScore(0L, instant: true);
    }

    private void OnTeamScoreChanged(TeamScoreSnapshot snapshot)
    {
        if (_scorePresentationPreviewActive || snapshot.teamID != teamID)
            return;

        ApplyScore(snapshot.currentMilliElectronVolts, instant: false);
    }

    private void OnTierPromoted(EnergyTierPromotion promotion)
    {
        if (_scorePresentationPreviewActive || promotion.teamID != teamID)
            return;

        if (_promotionRoutine != null)
            StopCoroutine(_promotionRoutine);

        _promotionRoutine = StartCoroutine(PlayPromotionRoutine());
    }

    private void ApplyScore(long rawMilliElectronVolts, bool instant)
    {
        _rawScore = Math.Max(0L, rawMilliElectronVolts);
        _unit = EnergyScoreFormatter.GetUnit(_rawScore);
        _tierProgress01 = EnergyScoreFormatter.GetTierProgress01(_rawScore);

        float curved = progressToScale != null
            ? Mathf.Clamp01(progressToScale.Evaluate(_tierProgress01))
            : _tierProgress01;

        float size = Mathf.Lerp(Mathf.Max(0f, minSize), Mathf.Max(minSize, maxSize), curved);
        _targetScale = Vector3.one * size;

        if (instant)
            GraphicTransform.localScale = _targetScale;
    }

    private IEnumerator PlayPromotionRoutine()
    {
        if (promotionAnimator != null && !string.IsNullOrWhiteSpace(promotionTrigger))
            promotionAnimator.SetTrigger(promotionTrigger);

        Transform graphic = GraphicTransform;
        float duration = Mathf.Max(0f, promotionSeconds);
        if (duration <= 0f)
        {
            graphic.localScale = _targetScale;
            _promotionRoutine = null;
            yield break;
        }

        Vector3 start = graphic.localScale;
        Vector3 peak = Vector3.one * Mathf.Max(maxSize, maxSize * promotionOvershoot);
        float burstSeconds = duration * 0.38f;
        float collapseSeconds = Mathf.Max(0.0001f, duration - burstSeconds);

        float elapsed = 0f;
        while (elapsed < burstSeconds)
        {
            elapsed += PromotionDeltaTime();
            float t = burstSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / burstSeconds);
            graphic.localScale = Vector3.LerpUnclamped(start, peak, Smooth01(t));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < collapseSeconds)
        {
            elapsed += PromotionDeltaTime();
            float t = Mathf.Clamp01(elapsed / collapseSeconds);
            graphic.localScale = Vector3.LerpUnclamped(peak, _targetScale, Smooth01(t));
            yield return null;
        }

        graphic.localScale = _targetScale;
        _promotionRoutine = null;
    }

    private float PromotionDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // ---------------------------------------------------------------------
    // Legacy normalized-score compatibility. These methods intentionally do
    // not mutate the new score system; they warn so missed integrations are
    // visible during migration rather than silently corrupting the economy.
    // ---------------------------------------------------------------------

    [Obsolete("ScoreSphereScript is presentation-only. Award score through MatchScoreService.")]
    public void SetScore01(float _)
    {
        WarnLegacyApi();
    }

    [Obsolete("ScoreSphereScript is presentation-only. Award score through MatchScoreService.")]
    public void AddScore01(float _)
    {
        WarnLegacyApi();
    }

    [Obsolete("ScoreSphereScript is presentation-only. Team score is not removed on death.")]
    public void RemoveScore01(float _)
    {
        WarnLegacyApi();
    }

    [Obsolete("Death no longer applies a team-score penalty.")]
    public void LoseScore(int _)
    {
        WarnLegacyApi();
    }

    [Obsolete("Death no longer applies a team-score penalty.")]
    public void LoseScore01(float _)
    {
        WarnLegacyApi();
    }

    [Obsolete("Death no longer applies a team-score penalty.")]
    public float ComputeRespawnLossIfApplied01(int _)
    {
        WarnLegacyApi();
        return 0f;
    }

    private void WarnLegacyApi()
    {
        if (_warnedLegacyApi) return;
        _warnedLegacyApi = true;
        Debug.LogWarning(
            $"[{name}] A legacy normalized score API was called. Replace the caller with MatchScoreService/ScoreRewardEmitter.",
            this);
    }
}
