using System;
using UnityEngine;

[Serializable]
public struct MatchResult
{
    public TeamSide winner;

    public float lightRaw;
    public float darkRaw;
    public float winScale;

    public GameMode mode;

    public int stageNumber;
    public string stageTitle;
    public string gameplaySceneName;

    public float Light01 => winScale > 0f ? Mathf.Clamp01(lightRaw / winScale) : 0f;
    public float Dark01  => winScale > 0f ? Mathf.Clamp01(darkRaw / winScale)  : 0f;
}
