using UnityEngine;

namespace Massive.Orbital
{
    public enum OrbitalFormation
    {
        Original = 0,
        [InspectorName("(3, 0, 0) — concentric shells")] Shells300 = 1,
        [InspectorName("(3, 2, 2) — horizontal lobes")] Lobes322 = 2,
        [InspectorName("(4, 1, 1) — nested horizontal lobes")] Lobes411 = 3,
        [InspectorName("(4, 2, 0) — polar lobes and belts")] Lobes420 = 4,
        [InspectorName("(4, 2, 1) — nested diagonal lobes")] Lobes421 = 5
    }

    /// <summary>Transition starts are interval seconds apart, including the transition itself.</summary>
    public sealed class OrbitalFormationCycle
    {
        public const int Count = 6;
        private double elapsed;
        private OrbitalFormation initial;
        public OrbitalFormation From { get; private set; }
        public OrbitalFormation To { get; private set; }
        public float Blend { get; private set; }
        public int TransitionsStarted { get; private set; }

        public void Reset(OrbitalFormation formation)
        {
            initial = (OrbitalFormation)Mathf.Clamp((int)formation, 0, Count - 1);
            From = To = initial; elapsed = 0; Blend = 0f; TransitionsStarted = 0;
        }

        public void Advance(float seconds, float interval, float transitionSeconds)
        {
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            interval = Mathf.Max(.1f, interval);
            elapsed += seconds;
            int step = (int)System.Math.Floor(elapsed / interval);
            TransitionsStarted = step;
            if (step == 0) return;
            From = (OrbitalFormation)(((int)initial + step - 1) % Count);
            To = (OrbitalFormation)(((int)initial + step) % Count);
            float duration = Mathf.Clamp(transitionSeconds, .01f, interval);
            Blend = Mathf.SmoothStep(0f, 1f, (float)(elapsed - step * (double)interval) / duration);
            if (Blend >= 1f) { From = To; Blend = 0f; }
        }
    }
}
