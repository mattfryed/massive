using System;

namespace Massive.Scoring
{
    // A value snapshot: copying a match result cannot change a completed average.
    [Serializable]
    public struct TimeWeightedMultiplier
    {
        public double durationSeconds;
        public double multiplierSeconds;

        public double Average => durationSeconds > 0d ? multiplierSeconds / durationSeconds : 1d;

        public void Accumulate(double multiplier, double seconds)
        {
            if (seconds <= 0d) return;
            durationSeconds += seconds;
            multiplierSeconds += Math.Max(1d, multiplier) * seconds;
        }
    }
}
