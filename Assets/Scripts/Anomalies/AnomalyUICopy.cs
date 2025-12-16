using UnityEngine;

public enum AnomalyTopState { Warning, Active, Post }
public enum AnomalyLowerState { Pre, During, Post }

[System.Serializable]
public class TopBannerPhase
{
    public bool enabled = true;
    public bool useCountdown = true;

    public string topLabelText = "ANOMALY DETECTED";
    [TextArea] public string typeText = ""; // if empty, fall back to def.shortDescription/displayName
}

[System.Serializable]
public class LowerThirdPhase
{
    public bool enabled = false;
    public string headerText = "OBJECTIVE";
    [TextArea] public string bodyText = "";
}

[CreateAssetMenu(menuName = "MASSIVE/Anomaly UI Copy")]
public class AnomalyUICopy : ScriptableObject
{
    [Header("Top Banner")]
    public TopBannerPhase topWarning = new TopBannerPhase { topLabelText = "ANOMALY DETECTED" };
    public TopBannerPhase topActive  = new TopBannerPhase { topLabelText = "ANOMALY ACTIVATED" };
    public TopBannerPhase topPost    = new TopBannerPhase { enabled = false, useCountdown = false, topLabelText = "ANOMALY COMPLETE" };

    [Header("Lower Third")]
    public LowerThirdPhase lowerPre    = new LowerThirdPhase();
    public LowerThirdPhase lowerDuring = new LowerThirdPhase();
    public LowerThirdPhase lowerPost   = new LowerThirdPhase();
}

