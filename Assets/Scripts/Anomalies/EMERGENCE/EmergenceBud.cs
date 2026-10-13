using System.Runtime.InteropServices;
using UnityEngine;

namespace Massive.Emergence
{
    // Matches GPU Bud (64 bytes). Stable catalyst IDs survive spatial sorting and group changes.
    [StructLayout(LayoutKind.Sequential)]
    public struct EmergenceBud
    {
        public const int Capacity = 32;
        public uint serial, phase, anchor, root; // 0 free, 1 windup, 2 retract, 3 released, 4 cancelled
        public Vector2 position, velocity, direction;
        public float elapsed, duration, cost, cancelFrom, retractElapsed, radius;
        public float Progress => Mathf.Clamp01(elapsed / Mathf.Max(.05f, duration));
        public bool Attached => phase == 1 || phase == 2;
        public float Scale(EmergenceProfile p)
        {
            float progress = phase == 2 || phase == 4 ? cancelFrom : Progress;
            float grow = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(p.budGrowthStart, p.budGrowthEnd, progress));
            return phase == 2 || phase == 4 ? grow * (1 - Mathf.SmoothStep(0, 1, retractElapsed / p.budRetractSeconds)) : grow;
        }
        public float Pulse(EmergenceProfile p)
        {
            float t = phase == 2 ? cancelFrom : Progress;
            float pulse = Mathf.Sin(t * Mathf.PI * 2) * (1 - t);
            return phase == 2 ? pulse * (1 - Mathf.SmoothStep(0, 1, retractElapsed / p.budRetractSeconds)) : pulse;
        }
        public Vector2 Tip(EmergenceProfile p, float rewardSize) => position + direction * (radius + p.budAttachmentDistance * rewardSize) * Scale(p);
    }
}
