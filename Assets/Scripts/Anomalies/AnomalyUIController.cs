using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles all shared UI around anomalies:
/// - top warning banner
/// - top active banner
/// - lower-third instruction + control icons + content area
/// - result popup
///
/// The actual minigame visuals are instantiated under minigameContentRoot
/// by AnomalyManager.
/// </summary>
public class AnomalyUIController : MonoBehaviour
{
    [Header("Warning Banner")]
    public GameObject warningPanel;
    public TextMeshProUGUI warningLabel;
    public Image warningIcon;

    [Header("Active Banner")]
    public GameObject activeBanner;
    public TextMeshProUGUI activeNameLabel;

    [Header("Lower Third")]
    public GameObject lowerThirdRoot;
    public RectTransform minigameContentRoot;
    public TextMeshProUGUI instructionText;
    public Image controlIconA;
    public Image controlIconB;

    [Header("Result Popup")]
    public GameObject resultRoot;
    public TextMeshProUGUI resultLabel;

    private void Awake()
    {
        SetPanel(warningPanel, false);
        SetPanel(activeBanner, false);
        SetPanel(lowerThirdRoot, false);
        SetPanel(resultRoot, false);
    }

    public void ShowWarning(AnomalyDefinition def)
    {
        if (warningLabel != null)
        {
            warningLabel.text = $"ANOMALY DETECTED: {def.displayName}";
        }

        if (warningIcon != null)
        {
            warningIcon.sprite = def.warningIcon;
            warningIcon.enabled = (def.warningIcon != null);
        }

        SetPanel(warningPanel, true);
    }

    public void ShowActiveBanner(AnomalyDefinition def)
    {
        // Once active, we can hide the warning.
        SetPanel(warningPanel, false);

        if (activeNameLabel != null)
        {
            activeNameLabel.text = def.displayName.ToUpperInvariant();
        }

        SetPanel(activeBanner, true);
    }

    public void HideActiveBanner()
    {
        SetPanel(activeBanner, false);
    }

    public void ShowLowerThird(AnomalyDefinition def)
    {
        if (instructionText != null)
        {
            instructionText.text = def.instructionText;
        }

        if (controlIconA != null)
        {
            controlIconA.sprite = def.controlIconA;
            controlIconA.enabled = (def.controlIconA != null);
        }

        if (controlIconB != null)
        {
            controlIconB.sprite = def.controlIconB;
            controlIconB.enabled = (def.controlIconB != null);
        }

        SetPanel(lowerThirdRoot, true);
        // Result popup remains hidden until the anomaly completes.
        SetPanel(resultRoot, false);
    }

    public void HideLowerThird()
    {
        SetPanel(lowerThirdRoot, false);
    }

    public void ShowResult(AnomalyDefinition def, AnomalyResult result)
    {
        if (resultLabel != null)
        {
            string status = result.success ? "RESOLVED" : "FAILED";
            resultLabel.text = $"{def.displayName.ToUpperInvariant()}\n{status}";
        }

        SetPanel(resultRoot, true);
    }

    private void SetPanel(GameObject panel, bool visible)
    {
        if (panel != null)
            panel.SetActive(visible);
    }
}
