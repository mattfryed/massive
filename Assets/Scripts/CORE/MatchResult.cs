using System;
using Massive.Scoring;

[Serializable]
public struct MatchResult
{
    public TeamSide winner;

    public long lightMilliElectronVolts;
    public long darkMilliElectronVolts;

    public GameMode mode;

    public int stageNumber;
    public string stageTitle;
    public string gameplaySceneName;

    public float regulationDurationSeconds;
    public float bonusDurationSeconds;

    public string scoringRulesetId;
    public int scoringRulesetVersion;

    public PlayerScoreContribution[] playerContributions;
    public ScoreTelemetryRecord[] scoreTelemetry;

    public long LightScore => Math.Max(0L, lightMilliElectronVolts);
    public long DarkScore => Math.Max(0L, darkMilliElectronVolts);
    public EnergyUnit LightUnit => EnergyScoreFormatter.GetUnit(LightScore);
    public EnergyUnit DarkUnit => EnergyScoreFormatter.GetUnit(DarkScore);
}
