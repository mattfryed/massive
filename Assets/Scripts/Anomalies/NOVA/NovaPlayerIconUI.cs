using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NovaPlayerIconUI : MonoBehaviour
{
    [Header("Refs")]
    public Image fillImage;
    public Image outlineImage;
    public TMP_Text label;

    [Header("Team 0 (Light)")]
    public Color team0FillColor    = Color.white;
    public Color team0OutlineColor = Color.black;
    public Color team0TextColor    = Color.black;

    [Header("Team 1 (Dark)")]
    public Color team1FillColor    = Color.black;
    public Color team1OutlineColor = Color.black;
    public Color team1TextColor    = Color.white;

    public void SetStyle(int teamIndex, string labelText)
    {
        if (label != null)
            label.text = labelText;

        if (teamIndex == 0)
        {
            if (fillImage    != null) fillImage.color    = team0FillColor;
            if (outlineImage != null) outlineImage.color = team0OutlineColor;
            if (label        != null) label.color        = team0TextColor;
        }
        else
        {
            if (fillImage    != null) fillImage.color    = team1FillColor;
            if (outlineImage != null) outlineImage.color = team1OutlineColor;
            if (label        != null) label.color        = team1TextColor;
        }
    }
}
