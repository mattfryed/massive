using UnityEngine;

namespace Massive.Multiplier
{
    /// <summary>Discard contest input until a timed handoff, stable release, and fresh Attack press.</summary>
    public struct AmplifierPostContestInputGate
    {
        public float UnlockAt { get; private set; }
        public bool ReadyForPress { get; private set; }
        float releaseSeconds, releasedSince;
        bool wasReleased;

        public void Begin(float now, AmplifierPossessionSettings settings)
        {
            UnlockAt = now + settings.PostContestLockSeconds;
            releaseSeconds = Mathf.Max(0f, settings.postContestReleaseSeconds);
            BreakRelease();
        }

        public void BreakRelease() { wasReleased = ReadyForPress = false; }

        public bool TryStartCharge(float now, bool attackDown, bool attackHeld, bool shieldDown, bool shieldHeld)
        {
            bool released = !attackDown && !attackHeld && !shieldDown && !shieldHeld;
            if (released)
            {
                if (!wasReleased) releasedSince = now;
                wasReleased = true;
                ReadyForPress = now >= UnlockAt && now - releasedSince >= releaseSeconds;
                return false;
            }
            // Evaluate the completed neutral interval before consuming its next edge.
            // Held-only input and any Shield input invalidate it instead of starting charge.
            bool start = now >= UnlockAt && wasReleased && now - releasedSince >= releaseSeconds &&
                attackDown && !shieldDown && !shieldHeld;
            BreakRelease();
            return start;
        }
    }
}
