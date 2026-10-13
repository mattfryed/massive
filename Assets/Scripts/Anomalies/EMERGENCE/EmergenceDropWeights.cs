using System;
using UnityEngine;

namespace Massive.Emergence
{
    public enum EmergenceDropType { MatterNugglet, AmplifierNugglet, MatterNugget, AmplifierNugget }

    [Serializable]
    public sealed class EmergenceDropWeights
    {
        [Min(0)] public float matterNugglet = 40;
        [Min(0)] public float amplifierNugglet = 40;
        [Min(0)] public float matterNugget = 10;
        [Min(0)] public float amplifierNugget = 10;

        public double Total => (double)matterNugglet + amplifierNugglet + matterNugget + amplifierNugget;
        public bool IsValid => Valid(matterNugglet) && Valid(amplifierNugglet) && Valid(matterNugget) && Valid(amplifierNugget) && Total > 0;
        static bool Valid(float value) => value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);

        // A separate random stream chooses the reward after conversion; it never changes the ecology.
        public EmergenceDropType Select(double sample)
        {
            if (!IsValid || double.IsNaN(sample) || sample < 0 || sample >= 1)
                throw new ArgumentOutOfRangeException(nameof(sample), "Use valid nonzero weights and a sample in [0, 1).");
            double position = sample * Total;
            if (position < matterNugglet) return EmergenceDropType.MatterNugglet;
            position -= matterNugglet;
            if (position < amplifierNugglet) return EmergenceDropType.AmplifierNugglet;
            position -= amplifierNugglet;
            if (position < matterNugget) return EmergenceDropType.MatterNugget;
            // Skip zero-weight trailing entries even at a floating-point boundary.
            if (amplifierNugget > 0) return EmergenceDropType.AmplifierNugget;
            if (matterNugget > 0) return EmergenceDropType.MatterNugget;
            return amplifierNugglet > 0 ? EmergenceDropType.AmplifierNugglet : EmergenceDropType.MatterNugglet;
        }
    }
}
