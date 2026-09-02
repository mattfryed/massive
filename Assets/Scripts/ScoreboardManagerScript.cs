using System;
using System.Collections;
using Massive.Scoring;
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

    private Coroutine _bindRoutine;
    private bool _bound;
    private bool _hasRefreshed;

    public long ScoreMilliElectronVolts { get; private set; }
    public int TeamID => teamID;

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
    }


    private void OnValidate()
    {
        teamID = teamID == 2 ? 2 : 1;

        if (tierVisualController == null)
            tierVisualController = GetComponent<EnergyTierVisualController>();

        if (Application.isPlaying)
            ConfigureTierVisualController();
    }

    private void OnEnable()
    {
        if (_bindRoutine == null)
            _bindRoutine = StartCoroutine(BindWhenAvailable());
    }

    private void OnDisable()
    {
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
        ScoreMilliElectronVolts = Math.Max(0L, rawMilliElectronVolts);
        EnergyDisplayValue display = EnergyScoreFormatter.GetDisplayValue(ScoreMilliElectronVolts);
        score = display.whole + display.fractionalThousandths / 1_000f;

        string value = EnergyScoreFormatter.FormatValue(
            ScoreMilliElectronVolts,
            integerDigits,
            fractionDigits,
            spaceCharacters);

        if (scoreValueText != null)
        {
            scoreValueText.text = activeUnitText == null && includeUnitInValueWhenNoUnitText
                ? $"{value} {display.unitLabel}"
                : value;
        }

        if (activeUnitText != null)
            activeUnitText.text = display.unitLabel;

        if (legacyTextMesh != null)
        {
            legacyTextMesh.text = includeUnitInValueWhenNoUnitText
                ? $"{value} {display.unitLabel}"
                : value;
        }

        RefreshTierLabels(display.unit);

        if (tierVisualController != null)
            tierVisualController.ApplyTier(display.unit, immediateVisuals);

        _hasRefreshed = true;
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
        _hasRefreshed = false;
        Refresh(0L, immediateVisuals: true);
    }

    private void OnTeamScoreChanged(TeamScoreSnapshot snapshot)
    {
        if (snapshot.teamID == teamID)
            Refresh(snapshot.currentMilliElectronVolts);
    }

    private void OnTierPromoted(EnergyTierPromotion promotion)
    {
        if (promotion.teamID != teamID)
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
