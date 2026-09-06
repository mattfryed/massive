using System;
using UnityEngine;

namespace Massive.Scoring
{
    public enum ScoreAwardRejection
    {
        None = 0,
        ScoringClosed = 1,
        InvalidRewardKey = 2,
        RewardDisabled = 3,
        InvalidTeam = 4,
        InvalidQuantity = 5,
        PseudoPlayer = 6,
        Duplicate = 7,
        AttributionMismatch = 8,
        MissingSourceToken = 9,
        InvalidRewardValue = 10
    }

    [Serializable]
    public struct TeamScoreSnapshot
    {
        public int teamID;
        public long previousMilliElectronVolts;
        public long currentMilliElectronVolts;
        public EnergyUnit previousUnit;
        public EnergyUnit currentUnit;
        public float tierProgress01;
    }

    [Serializable]
    public struct EnergyTierPromotion
    {
        public int teamID;
        public EnergyUnit previousUnit;
        public EnergyUnit currentUnit;
        public long totalMilliElectronVolts;
    }

    [Serializable]
    public struct TeamAmplifierSnapshot
    {
        public int teamID;
        public int previousTierIndex;
        public int currentTierIndex;
        public int previousMultiplier;
        public int currentMultiplier;
        public bool isAtMaximum;
    }

    [Serializable]
    public struct ScoreAwardResult
    {
        public bool accepted;
        public ScoreAwardRejection rejection;
        public string rewardKey;
        public string sourceToken;
        public int teamID;
        public int playerID;
        public Vector3 worldPosition;
        public long quantity;
        public long baseMilliElectronVolts;

        // Personal multiplier. Retained under the original field name for
        // compatibility with existing scoring presentation and telemetry.
        public int multiplier;
        public int teamAmplifierMultiplier;
        public int combinedMultiplier;
        public long finalMilliElectronVolts;
        public long previousTeamTotalMilliElectronVolts;
        public long newTeamTotalMilliElectronVolts;
        public EnergyUnit previousUnit;
        public EnergyUnit newUnit;
    }

    [Serializable]
    public struct PlayerScoreContribution
    {
        public int playerID;
        public int teamID;
        public int acceptedAwardCount;
        public int highestMultiplier;
        public long baseMilliElectronVolts;
        public long finalMilliElectronVolts;
    }

    [Serializable]
    public struct ScoreTelemetryRecord
    {
        public string rewardKey;
        public int lightAwardCount;
        public int darkAwardCount;
        public long lightBaseMilliElectronVolts;
        public long darkBaseMilliElectronVolts;
        public long lightFinalMilliElectronVolts;
        public long darkFinalMilliElectronVolts;
    }
}
