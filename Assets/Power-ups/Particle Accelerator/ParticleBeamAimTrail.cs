using UnityEngine;

namespace Massive.PowerUps
{
    /// <summary>Bounded, allocation-free aim history and integrated delayed tangents.</summary>
    public sealed class ParticleBeamAimTrail
    {
        const int HistoryCapacity = 256, Nodes = 65;
        readonly float[] times = new float[HistoryCapacity], angles = new float[HistoryCapacity];
        readonly float[] offsets = new float[Nodes];
        int head, count;
        float length, clock, heading, recordedAt;

        public void Reset(Vector3 direction)
        {
            head = 0; count = 1; clock = recordedAt = 0;
            times[0] = 0; angles[0] = heading = Heading(direction);
            System.Array.Clear(offsets, 0, offsets.Length);
        }

        static float Heading(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        public void Advance(float dt, Vector3 direction, float range, float speed, float maxDelay, float maxAngle)
        {
            clock += Mathf.Max(0f, dt);
            float next = Heading(direction);
            heading += Mathf.DeltaAngle(heading, next);
            // Limit recording to 120 Hz so even unusually high framerates retain >1s of history.
            if (clock - recordedAt >= 1f / 120f)
            { head = (head + 1) % HistoryCapacity; count = Mathf.Min(count + 1, HistoryCapacity); recordedAt = clock; }
            times[head] = clock; angles[head] = heading;
            length = Mathf.Max(.001f, range);
            float step = length / (Nodes - 1);
            offsets[0] = 0;
            for (int n = 1; n < Nodes; n++)
            {
                float delay = Mathf.Min(Mathf.Clamp(maxDelay, 0f, 1f), (n - .5f) * step / Mathf.Max(1f, speed));
                float delta = Mathf.Clamp(Sample(clock - delay) - heading, -Mathf.Clamp(maxAngle, 0f, 60f), Mathf.Clamp(maxAngle, 0f, 60f));
                offsets[n] = offsets[n - 1] + Mathf.Tan(delta * Mathf.Deg2Rad) * step;
            }
        }

        float Sample(float time)
        {
            int newer = head;
            for (int n = 1; n < count; n++)
            {
                int older = (head - n + HistoryCapacity) % HistoryCapacity;
                if (times[older] <= time)
                    return Mathf.Lerp(angles[older], angles[newer], Mathf.InverseLerp(times[older], times[newer], time));
                newer = older;
            }
            return angles[newer];
        }

        public float OffsetAt(float distance)
        {
            float index = Mathf.Clamp01(distance / Mathf.Max(.001f, length)) * (Nodes - 1);
            int first = Mathf.Min((int)index, Nodes - 2);
            return Mathf.Lerp(offsets[first], offsets[first + 1], index - first);
        }
    }
}
