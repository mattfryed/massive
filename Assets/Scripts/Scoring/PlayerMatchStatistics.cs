using System;

namespace Massive.Scoring
{
    [Serializable]
    public struct PlayerMatchStatistics
    {
        public int playerID;
        public int teamID;
        public int playerKills;
        public int deaths;
        public int enemiesKilled;
        // One Unity world unit is one meter; retain precision until presentation.
        public double distanceTraveledMeters;
    }
}
