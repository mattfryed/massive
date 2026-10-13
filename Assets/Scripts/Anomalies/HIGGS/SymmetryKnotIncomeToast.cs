using Massive.Scoring;
using TMPro;
using UnityEngine;

/// <summary>Capture status and accepted income, using the shared SDF text transition.</summary>
[DisallowMultipleComponent]
public sealed class SymmetryKnotIncomeToast : MonoBehaviour
{
    public enum Content { Hidden, Capturing, Earning }

    [Header("References")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMPTextTransition textTransition;
    [Header("Font sizes")]
    [SerializeField, Min(.1f)] private float incomeFontSize = 3.6f;
    [SerializeField, Min(.1f)] private float capturingFontSize = 3f;
    [SerializeField, Range(10, 100)] private int teamLabelPercent = 65;
    [Header("Position")]
    [SerializeField, Min(0)] private float clearance = .6f;
    [Header("Capturing pulse")]
    [SerializeField, Min(.1f)] private float pulsePeriodSeconds = 2f;
    [SerializeField, Range(0, .25f)] private float pulseScaleAmount = .04f;
    [SerializeField, Range(0, 1)] private float pulseMinimumAlpha = .55f;

    private Transform anchor;
    private Camera view;
    private float radius, age;
    private Vector3 authoredScale;
    private Content displayed;
    private int displayedTeam, displayedPercent;
    private long displayedRate;
    public long MilliElectronVoltsPerSecond { get; private set; }
    public int TeamID { get; private set; }
    public Content RequestedContent { get; private set; }
    public bool IsTransitioningOut { get; private set; }
    public bool IsVisible => gameObject.activeSelf && label != null && label.enabled;

    public void Initialize(Transform target, float captureRadius, Camera presentationCamera = null)
    {
        anchor = target;
        radius = captureRadius;
        authoredScale = transform.localScale;
        view = presentationCamera != null ? presentationCamera : Camera.main;
        if (label != null) label.raycastTarget = false;
        if (textTransition != null)
        {
            textTransition.onOutComplete.RemoveListener(FinishOut);
            textTransition.onOutComplete.AddListener(FinishOut);
        }
        HideImmediate();
    }

    public void ShowCapturing(int teamID)
    {
        if (teamID != 1 && teamID != 2) { Hide(); return; }
        Present(Content.Capturing, teamID, 0);
    }

    public void Show(ScoreAwardResult award, float tickSeconds)
    {
        if (!award.accepted || award.finalMilliElectronVolts <= 0 || award.quantity <= 0 ||
            (award.teamID != 1 && award.teamID != 2) || tickSeconds <= 0 ||
            float.IsNaN(tickSeconds) || float.IsInfinity(tickSeconds))
        { Hide(); return; }
        long rate = EnergyScoreMath.SaturatingScale(award.finalMilliElectronVolts,
            1d / (award.quantity * (double)tickSeconds));
        Present(Content.Earning, award.teamID, rate);
    }

    private void Present(Content content, int teamID, long rate)
    {
        if (label == null) return;
        RequestedContent = content;
        TeamID = teamID;
        MilliElectronVoltsPerSecond = rate;
        if (IsTransitioningOut) return; // Latest content replaces the pending value.
        if (!IsVisible) ApplyPendingAndGrow();
        else if (displayed != content || displayedTeam != teamID) BeginOut();
        else RefreshText(); // Income changes do not restart the intro every tick.
    }

    public void Hide()
    {
        RequestedContent = Content.Hidden;
        TeamID = 0;
        MilliElectronVoltsPerSecond = 0;
        if (IsVisible && !IsTransitioningOut) BeginOut();
    }

    public void HideImmediate()
    {
        RequestedContent = displayed = Content.Hidden;
        TeamID = 0;
        MilliElectronVoltsPerSecond = 0;
        IsTransitioningOut = false;
        if (label != null) label.enabled = false;
        gameObject.SetActive(false);
    }

    private void BeginOut()
    {
        IsTransitioningOut = true;
        if (textTransition != null) textTransition.PlayOut();
        else FinishOut();
    }

    private void FinishOut()
    {
        IsTransitioningOut = false;
        if (RequestedContent == Content.Hidden) HideImmediate();
        else ApplyPendingAndGrow();
    }

    private void ApplyPendingAndGrow()
    {
        age = 0;
        displayed = RequestedContent;
        displayedTeam = TeamID;
        displayedPercent = -1;
        label.enabled = true;
        label.alpha = 1;
        RefreshText();
        gameObject.SetActive(true);
        UpdatePose();
        if (textTransition != null) textTransition.PlayIn();
    }

    private void RefreshText()
    {
        label.fontSize = displayed == Content.Capturing ? capturingFontSize : incomeFontSize;
        if (displayedPercent == teamLabelPercent && displayedRate == MilliElectronVoltsPerSecond) return;
        string body = "CAPTURING";
        if (displayed == Content.Earning)
        {
            long rate = MilliElectronVoltsPerSecond;
            var display = EnergyScoreFormatter.GetDisplayValue(rate);
            string amount = EnergyScoreFormatter.FormatValue(rate, 1, display.fractionalThousandths == 0 ? 0 : 3);
            if (display.fractionalThousandths != 0) amount = amount.TrimEnd('0').TrimEnd('.');
            body = "+" + amount + " " + display.unitLabel + "/s";
        }
        label.text = "<size=" + teamLabelPercent + "%>" + (displayedTeam == 1 ? "LIGHT" : "DARK") +
            " CONTROL</size>\n" + body;
        displayedPercent = teamLabelPercent;
        displayedRate = MilliElectronVoltsPerSecond;
    }

    private void LateUpdate()
    {
        age += Time.deltaTime;
        if (!IsTransitioningOut) RefreshText();
        UpdatePose();
    }

    private void UpdatePose()
    {
        if (anchor == null) { HideImmediate(); return; }
        if (view == null) view = Camera.main;
        Vector3 up = view != null ? view.transform.up : Vector3.forward;
        transform.position = anchor.position + Vector3.up * .2f + up * (radius + clearance);
        if (view != null) transform.rotation = view.transform.rotation;
        float wave = .5f + .5f * Mathf.Cos(age * (Mathf.PI * 2) / Mathf.Max(.1f, pulsePeriodSeconds));
        bool pulsing = displayed == Content.Capturing && !IsTransitioningOut;
        transform.localScale = authoredScale * (pulsing ? 1 + pulseScaleAmount * (wave * 2 - 1) : 1);
        // The shared transition owns alpha/material while it animates; idle pulse owns alpha afterward.
        if (textTransition == null || !textTransition.IsPlaying)
            label.alpha = pulsing ? Mathf.Lerp(pulseMinimumAlpha, 1, wave) : 1;
    }

    private void OnDestroy()
    {
        if (textTransition != null) textTransition.onOutComplete.RemoveListener(FinishOut);
    }
}
