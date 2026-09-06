using System;
using System.Collections;
using Massive.Scoring;
using Massive.TextAnimation;
using TMPro;
using UnityEngine;

/// <summary>
/// Event-driven live score HUD. Displays one fixed-width engineering value and
/// highlights the active unit in the meV -> TeV ladder.
/// </summary>
[DisallowMultipleComponent]
public class ScoreboardManagerScript : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private int teamID = 1;

    [Header("Score Source")]
    [SerializeField] private MatchScoreService scoreService;

    [Header("Text")]
    [SerializeField] private TMP_Text scoreValueText;
    [SerializeField] private TMP_Text activeUnitText;
    [Tooltip("Assign in exact order: meV, eV, keV, MeV, GeV, TeV.")]
    [SerializeField] private TMP_Text[] tierLabels;

    [Header("Legacy TextMesh Fallback")]
    [SerializeField] private TextMesh legacyTextMesh;

    [Header("Formatting")]
    [SerializeField, Min(1)] private int integerDigits = 3;
    [SerializeField, Range(0, 3)] private int fractionDigits = 3;
    [SerializeField] private bool spaceCharacters = true;
    [SerializeField] private bool includeUnitInValueWhenNoUnitText = true;

    [Header("Tier Label Colors")]
    [SerializeField] private Color activeTierColor = Color.white;
    [SerializeField] private Color completedTierColor = new Color(0.32f, 0.32f, 0.32f, 1f);
    [SerializeField] private Color futureTierColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    [Header("Promotion (Legacy / Optional)")]
    [SerializeField] private Animator promotionAnimator;
    [SerializeField] private string promotionTrigger = "Promote";

    [Header("Tier Visual Effects")]
    [Tooltip("Optional data-driven TMP/SDF, shake, particle, audio, and custom tier hooks.")]
    [SerializeField] private EnergyTierVisualController tierVisualController;

    [Header("Score Digit Morph")]
    [Tooltip("Preset used only when visible score digits change. Unchanged characters remain stable.")]
    [SerializeField] private TextAnimationPreset scoreDigitMorphPreset;
    [SerializeField] private TMPTextAnimator scoreValueAnimator;
    [Tooltip("At runtime, add the shared TMP animator to the score label when it is not already present.")]
    [SerializeField] private bool autoCreateScoreValueAnimator = true;
    [SerializeField, Min(0f)] private float scoreDigitMorphIntensity = 1f;

    private Coroutine _bindRoutine;
    private Coroutine _scoreDigitMorphShowcaseRoutine;
    private bool _bound;
    private bool _hasRefreshed;
    private bool _scorePresentationPreviewActive;
    private long _scoreBeforePresentationPreview;
    private long _previewScore;

    public long ScoreMilliElectronVolts { get; private set; }
    public int TeamID => teamID;
    public TMPTextAnimator ScoreValueAnimator => scoreValueAnimator;
    public TextAnimationPreset ScoreDigitMorphPreset => scoreDigitMorphPreset;
    public bool IsScorePresentationPreviewActive => _scorePresentationPreviewActive;

    [Obsolete("Legacy display-only field. Use ScoreMilliElectronVolts for authoritative score data.")]
    [HideInInspector] public float score;

    private void Reset()
    {
        legacyTextMesh = GetComponent<TextMesh>();
        tierVisualController = GetComponent<EnergyTierVisualController>();
    }

    private void Awake()
    {
        if (legacyTextMesh == null)
            legacyTextMesh = GetComponent<TextMesh>();

        if (tierVisualController == null)
            tierVisualController = GetComponent<EnergyTierVisualController>();

        ConfigureTierVisualController();
        ResolveScoreValueAnimator();
    }


    private void OnValidate()
    {
        teamID = teamID == 2 ? 2 : 1;

        if (tierVisualController == null)
            tierVisualController = GetComponent<EnergyTierVisualController>();

        // OnValidate can run while Unity is entering Play Mode. Creating helper
        // components from that callback produces "SendMessage cannot be called"
        // errors, so runtime wiring is intentionally owned by Awake/OnEnable.
    }

    private void OnEnable()
    {
        if (_bindRoutine == null)
            _bindRoutine = StartCoroutine(BindWhenAvailable());
    }

    private void OnDisable()
    {
        _scorePresentationPreviewActive = false;
        StopScoreDigitMorphShowcase(restoreAuthoritativeValue: false);
        Unbind();

        if (_bindRoutine != null)
        {
            StopCoroutine(_bindRoutine);
            _bindRoutine = null;
        }
    }

    public void SetTeamID(int value)
    {
        teamID = value == 2 ? 2 : 1;
        if (_bound && scoreService != null)
            Refresh(scoreService.GetTeamScore(teamID), immediateVisuals: true);
    }

    public void Refresh(long rawMilliElectronVolts)
    {
        Refresh(rawMilliElectronVolts, immediateVisuals: !_hasRefreshed);
    }

    private void Refresh(long rawMilliElectronVolts, bool immediateVisuals)
    {
        StopScoreDigitMorphShowcase(restoreAuthoritativeValue: false);
        ScoreMilliElectronVolts = Math.Max(0L, rawMilliElectronVolts);
        EnergyDisplayValue display = EnergyScoreFormatter.GetDisplayValue(ScoreMilliElectronVolts);
        score = display.whole + display.fractionalThousandths / 1_000f;

        string value = FormatScoreValue(ScoreMilliElectronVolts, display);

        if (activeUnitText != null)
            activeUnitText.text = display.unitLabel;

        if (legacyTextMesh != null)
        {
            legacyTextMesh.text = activeUnitText == null
                ? value
                : includeUnitInValueWhenNoUnitText
                    ? $"{value} {display.unitLabel}"
                    : value;
        }

        RefreshTierLabels(display.unit);

        if (tierVisualController != null)
            tierVisualController.ApplyTier(display.unit, immediateVisuals);

        SetScoreValueText(value, immediateVisuals);

        _hasRefreshed = true;
    }

    public void SetScoreDigitMorphPreset(TextAnimationPreset preset)
    {
        scoreDigitMorphPreset = preset;
    }

    /// <summary>
    /// Starts an isolated score-presentation preview. MatchScoreService remains
    /// authoritative and continues to retain the real team total.
    /// </summary>
    public void BeginScorePresentationPreview()
    {
        if (!Application.isPlaying || _scorePresentationPreviewActive)
            return;

        StopScoreDigitMorphShowcase(restoreAuthoritativeValue: false);
        _scoreBeforePresentationPreview = ScoreMilliElectronVolts;
        _previewScore = ScoreMilliElectronVolts;
        _scorePresentationPreviewActive = true;
    }

    /// <summary>
    /// Drives the score number and energy-tier presentation without mutating
    /// authoritative match score. Upward tier crossings reuse gameplay's tier
    /// promotion animation; downward scrubs settle immediately.
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
        Refresh(
            nextScore,
            immediateVisuals: !animateChanges || nextUnit < previousUnit);

        if (animateChanges && promoted && tierVisualController != null)
        {
            tierVisualController.PlayPromotion(new EnergyTierPromotion
            {
                teamID = teamID,
                previousUnit = previousUnit,
                currentUnit = nextUnit,
                totalMilliElectronVolts = nextScore
            });
        }
    }

    public void EndScorePresentationPreview()
    {
        if (!_scorePresentationPreviewActive)
            return;

        _scorePresentationPreviewActive = false;
        long liveScore = scoreService != null
            ? scoreService.GetTeamScore(teamID)
            : _scoreBeforePresentationPreview;
        Refresh(liveScore, immediateVisuals: true);
    }

    /// <summary>
    /// Plays representative carry and multi-digit changes without modifying the
    /// authoritative score. Intended for the Text Animation preview window.
    /// </summary>
    public void PlayScoreDigitMorphShowcase(float holdBetweenValues = 0.65f)
    {
        if (!isActiveAndEnabled || scoreValueText == null)
            return;

        StopScoreDigitMorphShowcase(restoreAuthoritativeValue: false);
        _scoreDigitMorphShowcaseRoutine = StartCoroutine(
            PlayScoreDigitMorphShowcaseRoutine(Mathf.Max(0f, holdBetweenValues)));
    }

    public void StopScoreDigitMorphShowcase(bool restoreAuthoritativeValue = true)
    {
        if (_scoreDigitMorphShowcaseRoutine != null)
        {
            StopCoroutine(_scoreDigitMorphShowcaseRoutine);
            _scoreDigitMorphShowcaseRoutine = null;
        }

        if (restoreAuthoritativeValue && scoreValueText != null)
        {
            EnergyDisplayValue display =
                EnergyScoreFormatter.GetDisplayValue(ScoreMilliElectronVolts);
            SetScoreValueText(
                FormatScoreValue(ScoreMilliElectronVolts, display),
                immediate: true);
        }
    }

    private IEnumerator PlayScoreDigitMorphShowcaseRoutine(float holdBetweenValues)
    {
        long[] samples = { 0L, 7L, 42L, 99L, 100L, 137L, 888L, 999L };
        SetScoreValueText(
            FormatScoreValue(samples[0], EnergyScoreFormatter.GetDisplayValue(samples[0])),
            immediate: true);
        yield return null;

        float animationSeconds = scoreDigitMorphPreset != null
            ? scoreDigitMorphPreset.EstimateTotalSeconds(6)
            : 0f;

        for (int i = 1; i < samples.Length; i++)
        {
            EnergyDisplayValue display =
                EnergyScoreFormatter.GetDisplayValue(samples[i]);
            SetScoreValueText(
                FormatScoreValue(samples[i], display),
                immediate: false);

            float elapsed = 0f;
            float wait = animationSeconds + holdBetweenValues;
            while (elapsed < wait)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        _scoreDigitMorphShowcaseRoutine = null;
    }

    private string FormatScoreValue(
        long rawMilliElectronVolts,
        EnergyDisplayValue display)
    {
        string value = EnergyScoreFormatter.FormatValue(
            rawMilliElectronVolts,
            integerDigits,
            fractionDigits,
            spaceCharacters);
        return activeUnitText == null && includeUnitInValueWhenNoUnitText
            ? $"{value} {display.unitLabel}"
            : value;
    }

    private void SetScoreValueText(string value, bool immediate)
    {
        if (scoreValueText == null)
            return;

        ResolveScoreValueAnimator();
        string next = value ?? string.Empty;
        if (string.Equals(scoreValueText.text, next, StringComparison.Ordinal))
            return;

        if (immediate ||
            scoreDigitMorphPreset == null ||
            scoreValueAnimator == null)
        {
            scoreValueAnimator?.StopAll(
                restoreBaseline: true,
                restoreVisibility: true);
            scoreValueText.text = next;
            scoreValueAnimator?.RefreshBaselineFromCurrent();
            return;
        }

        TextAnimationContext context = TextAnimationContext.Default
            .WithIntensity(scoreDigitMorphIntensity)
            .WithDirection(Vector2.up);
        scoreValueAnimator.SetTextAndPlay(next, scoreDigitMorphPreset, context);
    }

    private void ResolveScoreValueAnimator()
    {
        if (scoreValueText == null)
            return;

        if (scoreValueAnimator == null)
            scoreValueAnimator = scoreValueText.GetComponent<TMPTextAnimator>();

        if (scoreValueAnimator == null &&
            autoCreateScoreValueAnimator &&
            Application.isPlaying)
        {
            scoreValueAnimator =
                scoreValueText.gameObject.AddComponent<TMPTextAnimator>();
        }

        if (scoreValueAnimator != null)
        {
            scoreValueAnimator.Configure(scoreValueText, scoreValueText.transform);
            scoreValueAnimator.ConfigureFixedVisibleCharacterSlots(true);
        }
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
        _hasRefreshed = false;

        Refresh(scoreService.GetTeamScore(teamID), immediateVisuals: true);
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

        _hasRefreshed = false;
        Refresh(0L, immediateVisuals: true);
    }

    private void OnTeamScoreChanged(TeamScoreSnapshot snapshot)
    {
        if (!_scorePresentationPreviewActive && snapshot.teamID == teamID)
            Refresh(snapshot.currentMilliElectronVolts);
    }

    private void OnTierPromoted(EnergyTierPromotion promotion)
    {
        if (_scorePresentationPreviewActive || promotion.teamID != teamID)
            return;

        if (promotionAnimator != null && !string.IsNullOrWhiteSpace(promotionTrigger))
            promotionAnimator.SetTrigger(promotionTrigger);

        if (tierVisualController != null)
            tierVisualController.PlayPromotion(promotion);
    }

    private void ConfigureTierVisualController()
    {
        if (tierVisualController == null)
            return;

        tierVisualController.ConfigureTargets(
            scoreValueText,
            activeUnitText,
            tierLabels);
    }

    private void RefreshTierLabels(EnergyUnit activeUnit)
    {
        if (tierLabels == null) return;

        for (int i = 0; i < tierLabels.Length; i++)
        {
            TMP_Text label = tierLabels[i];
            if (label == null) continue;

            if (i < EnergyScoreMath.UnitCount)
                label.text = EnergyScoreMath.GetLabel((EnergyUnit)i);

            label.color = i < (int)activeUnit
                ? completedTierColor
                : i == (int)activeUnit
                    ? activeTierColor
                    : futureTierColor;
        }
    }

    [Obsolete("Use MatchScoreService. This temporary bridge maps 0..1 into the initial 0..999 meV tier only.")]
    public void UpdateScoreboard(float legacyPercentage)
    {
        long raw = Mathf.RoundToInt(Mathf.Clamp01(legacyPercentage) * 999f);
        Refresh(raw);
    }
}
