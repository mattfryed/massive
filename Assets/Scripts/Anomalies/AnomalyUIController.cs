using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AnomalyUIController : MonoBehaviour
{
    [Header("Copy")]
    [Tooltip("Fallback copy if an anomaly doesn't provide an override.")]
    public AnomalyUICopy defaultCopy;

    [Header("Top Banner Root")]
    public GameObject warningPanel;

    [Tooltip("Left indicator (! / countdown).")]
    public TMP_Text leftIndicatorText;

    [Tooltip("Right indicator (! / countdown).")]
    public TMP_Text rightIndicatorText;

    [Tooltip("Top line text (DETECTED / ACTIVATED / COMPLETE).")]
    public TMP_Text centerLabelText;

    [Tooltip("Second line text (anomaly type).")]
    public TMP_Text anomalyTypeText;

    [Header("Lower Third Root")]
    public GameObject lowerThirdRoot;

    [Tooltip("Optional lower third header text. If null, header is omitted.")]
    public TMP_Text lowerHeaderText;

    [Tooltip("Lower third body/instructions text.")]
    public TMP_Text instructionText;

    [Header("Minigame Content Root")]
    public RectTransform minigameContentRoot;

    [Header("Optional Control Icons")]
    public Image controlIconA;
    public Image controlIconB;

    [Header("Optional Result Popup")]
    public GameObject resultRoot;
    public TMP_Text resultLabel;

    [Header("Score UI (optional)")]
    public TMP_Text lightTeamCountText;
    public TMP_Text darkTeamCountText;

    [Header("Indicator Behavior")]
    [Tooltip("Seconds spent flashing '!' before switching to numeric countdown.")]
    public float indicatorFlashSeconds = 0.6f;

    [Tooltip("Blink rate during flash (blinks/sec).")]
    public float indicatorFlashHz = 6f;

    Coroutine _countdownRoutine;

    void Awake()
    {
        SetPanel(warningPanel, false);
        SetPanel(lowerThirdRoot, false);
        SetPanel(resultRoot, false);
        ClearIndicators();
    }

    // ------------------------------------------------------------
    // High-level state API (recommended usage going forward)
    // ------------------------------------------------------------

    public void SetTopState(AnomalyDefinition def, AnomalyTopState state, float countdownSeconds)
    {
        StopCountdown();

        var copy = ResolveCopy(def);
        var phase = GetTopPhase(copy, state);

        if (copy == null || phase == null || !phase.enabled)
        {
            SetPanel(warningPanel, false);
            ClearIndicators();
            return;
        }

        SetPanel(warningPanel, true);

        if (centerLabelText != null)
            centerLabelText.text = phase.topLabelText;

        if (anomalyTypeText != null)
            anomalyTypeText.text = ResolveTypeText(def, phase.typeText);

        if (phase.useCountdown && countdownSeconds > 0f)
        {
            _countdownRoutine = StartCoroutine(IndicatorCountdown(countdownSeconds));
        }
        else
        {
            ClearIndicators();
        }
    }

    public void SetLowerState(AnomalyDefinition def, AnomalyLowerState state)
    {
        var copy = ResolveCopy(def);
        var phase = GetLowerPhase(copy, state);

        if (copy == null || phase == null || !phase.enabled)
        {
            SetPanel(lowerThirdRoot, false);
            return;
        }

        SetPanel(lowerThirdRoot, true);

        if (lowerHeaderText != null)
            lowerHeaderText.text = phase.headerText;

        if (instructionText != null)
        {
            // If phase body is empty, fall back to def.instructionText.
            string body = phase.bodyText;
            if (string.IsNullOrEmpty(body) && def != null && !string.IsNullOrEmpty(def.instructionText))
                body = def.instructionText;

            instructionText.text = body;
        }

        // Control icons remain per-anomaly (keeps your existing workflow)
        if (controlIconA != null)
        {
            controlIconA.sprite = def != null ? def.controlIconA : null;
            controlIconA.enabled = (controlIconA.sprite != null);
        }

        if (controlIconB != null)
        {
            controlIconB.sprite = def != null ? def.controlIconB : null;
            controlIconB.enabled = (controlIconB.sprite != null);
        }
    }

    public void HideTop()
    {
        StopCountdown();
        ClearIndicators();
        SetPanel(warningPanel, false);
    }

    public void HideLower()
    {
        SetPanel(lowerThirdRoot, false);
    }

    // ------------------------------------------------------------
    // Backwards-compatible API used by your current AnomalyManager
    // ------------------------------------------------------------

    // Warning countdown = time until intro animation starts.
    public void ShowWarning(AnomalyDefinition def, float preStartDuration)
    {
        SetTopState(def, AnomalyTopState.Warning, preStartDuration);

        // Optional: if you want a separate "pre" lower third state
        // SetLowerState(def, AnomalyLowerState.Pre);
    }

    // Active countdown = gameplay time (post-intro).
    public void ShowActiveBanner(AnomalyDefinition def, float activeDuration)
    {
        SetTopState(def, AnomalyTopState.Active, activeDuration);
        // Optional: during state for lower third
        // SetLowerState(def, AnomalyLowerState.During);
    }

    public void HideActiveBanner()
    {
        HideTop();
    }

    public void ShowLowerThird(AnomalyDefinition def)
    {
        SetLowerState(def, AnomalyLowerState.During);
    }

    public void HideLowerThird()
    {
        HideLower();
    }

    public void ShowResult(AnomalyDefinition def, AnomalyResult result)
    {
        StopCountdown();

        // Post states (toggleable per anomaly)
        SetTopState(def, AnomalyTopState.Post, 0f);
        SetLowerState(def, AnomalyLowerState.Post);

        if (resultRoot != null)
            resultRoot.SetActive(true);

        if (resultLabel != null)
        {
            string status = result.success ? "RESOLVED" : "FAILED";
            resultLabel.text = $"{(def != null ? def.displayName.ToUpperInvariant() : "ANOMALY")}\n{status}";
        }
    }

    public void SetParticleCounts(int light, int dark)
    {
        if (lightTeamCountText != null) lightTeamCountText.text = light.ToString("00");
        if (darkTeamCountText  != null) darkTeamCountText.text  = dark.ToString("00");
    }

    // ------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------

    AnomalyUICopy ResolveCopy(AnomalyDefinition def)
    {
        if (def != null && def.uiCopyOverride != null)
            return def.uiCopyOverride;

        return defaultCopy;
    }

    string ResolveTypeText(AnomalyDefinition def, string phaseOverride)
    {
        if (!string.IsNullOrEmpty(phaseOverride))
            return phaseOverride;

        if (def == null) return "";

        if (!string.IsNullOrEmpty(def.shortDescription))
            return def.shortDescription;

        return def.displayName;
    }

    TopBannerPhase GetTopPhase(AnomalyUICopy copy, AnomalyTopState state)
    {
        if (copy == null) return null;

        // Support BOTH naming schemes via reflection:
        // new: topWarning/topActive/topPost
        // old: warning/active/post
        string fieldName = state switch
        {
            AnomalyTopState.Warning => "topWarning",
            AnomalyTopState.Active  => "topActive",
            AnomalyTopState.Post    => "topPost",
            _ => "topWarning"
        };

        var phase = ReadField<TopBannerPhase>(copy, fieldName);
        if (phase != null) return phase;

        // fallback to old names
        fieldName = state switch
        {
            AnomalyTopState.Warning => "warning",
            AnomalyTopState.Active  => "active",
            AnomalyTopState.Post    => "post",
            _ => "warning"
        };

        return ReadField<TopBannerPhase>(copy, fieldName);
    }

    LowerThirdPhase GetLowerPhase(AnomalyUICopy copy, AnomalyLowerState state)
    {
        if (copy == null) return null;

        // new: lowerPre/lowerDuring/lowerPost
        // old: pre/during/post
        string fieldName = state switch
        {
            AnomalyLowerState.Pre    => "lowerPre",
            AnomalyLowerState.During => "lowerDuring",
            AnomalyLowerState.Post   => "lowerPost",
            _ => "lowerDuring"
        };

        var phase = ReadField<LowerThirdPhase>(copy, fieldName);
        if (phase != null) return phase;

        // fallback to old names
        fieldName = state switch
        {
            AnomalyLowerState.Pre    => "pre",
            AnomalyLowerState.During => "during",
            AnomalyLowerState.Post   => "post",
            _ => "during"
        };

        return ReadField<LowerThirdPhase>(copy, fieldName);
    }

    static T ReadField<T>(object obj, string fieldName) where T : class
    {
        if (obj == null) return null;
        var t = obj.GetType();
        var fi = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return fi != null ? fi.GetValue(obj) as T : null;
    }

    void SetPanel(GameObject panel, bool visible)
    {
        if (panel != null) panel.SetActive(visible);
    }

    void StopCountdown()
    {
        if (_countdownRoutine != null)
        {
            StopCoroutine(_countdownRoutine);
            _countdownRoutine = null;
        }
    }

    void ClearIndicators()
    {
        if (leftIndicatorText != null)  leftIndicatorText.text  = "";
        if (rightIndicatorText != null) rightIndicatorText.text = "";
    }

    IEnumerator IndicatorCountdown(float totalDuration)
    {
        float flashTime = Mathf.Min(indicatorFlashSeconds, totalDuration * 0.3f);
        float t = 0f;

        while (t < flashTime)
        {
            t += Time.deltaTime;
            bool on = Mathf.FloorToInt(t * indicatorFlashHz) % 2 == 0;
            string s = on ? "!" : "";
            if (leftIndicatorText != null)  leftIndicatorText.text  = s;
            if (rightIndicatorText != null) rightIndicatorText.text = s;
            yield return null;
        }

        float remaining = Mathf.Max(0f, totalDuration - flashTime);
        while (remaining > 0f)
        {
            int seconds = Mathf.CeilToInt(remaining);
            string s = seconds.ToString();
            if (leftIndicatorText != null)  leftIndicatorText.text  = s;
            if (rightIndicatorText != null) rightIndicatorText.text = s;
            remaining -= Time.deltaTime;
            yield return null;
        }

        if (leftIndicatorText != null)  leftIndicatorText.text  = "!";
        if (rightIndicatorText != null) rightIndicatorText.text = "!";
        _countdownRoutine = null;
    }
}
