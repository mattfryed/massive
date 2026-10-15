using System.Collections.Generic;
using UnityEngine;

namespace Massive.PowerUps
{
    /// <summary>World-space brush deposits and the exposed arcs of their union. Damage uses these same discs.</summary>
    public sealed class DecoherencePaint
    {
        public struct Deposit
        {
            public Vector3 center;
            public float radius, initialRadius, expires, recedeSeconds;
            public int stroke;
        }
        public struct BoundaryArc
        {
            public int deposit;
            public float from, to;
        }
        private struct Interval { public float from, to; }
        private const int MaxDeposits = 512;
        private const float Tau = Mathf.PI * 2f;
        private readonly List<Deposit> deposits = new();
        private readonly List<BoundaryArc> boundary = new();
        private readonly List<Interval> covered = new();
        private bool boundaryDirty;
        private float lastStampTime = -100f;
        public float Clock { get; private set; }
        public float RecedeSeconds { get; set; } = .24f;
        public IReadOnlyList<Deposit> Deposits => deposits;
        public IReadOnlyList<BoundaryArc> Boundary { get { RebuildBoundary(); return boundary; } }

        public void Advance(float seconds)
        {
            Clock += Mathf.Max(0, seconds);
            for (int i = deposits.Count - 1; i >= 0; i--)
            {
                var deposit = deposits[i];
                float remaining = deposit.expires - Clock;
                if (remaining <= 0f) { deposits.RemoveAt(i); boundaryDirty = true; continue; }
                if (remaining >= deposit.recedeSeconds) continue;
                // The final contraction is part of the footprint, so damage and grid
                // echoes recede with the border instead of outliving the visible field.
                deposit.radius = deposit.initialRadius * Mathf.SmoothStep(0f, 1f, remaining / deposit.recedeSeconds);
                deposits[i] = deposit; boundaryDirty = true;
            }
        }
        public void Clear() { deposits.Clear(); boundary.Clear(); lastStampTime = -100f; boundaryDirty = false; }

        public void Paint(Vector3 center, float radius, float lifetime, int stroke, bool connect = true)
        {
            if (radius < .005f) return;
            radius = Mathf.Max(.005f, radius); lifetime = Mathf.Max(.1f, lifetime);
            if (deposits.Count > 0)
            {
                var previous = deposits[deposits.Count - 1];
                float distance = Vector3.Distance(previous.center, center);
                float spacing = Mathf.Clamp(Mathf.Min(previous.radius, radius) * .35f, .025f, .18f);
                // A growing activation must update even while standing still.
                float radiusStep = Mathf.Max(.005f, spacing * .1f);
                if (previous.stroke == stroke && Clock - lastStampTime < .08f && distance < spacing &&
                    Mathf.Abs(previous.radius - radius) < radiusStep) return;
                // Never bridge a teleport, portal or a very long unsupported displacement.
                if (connect && previous.stroke == stroke && distance < 8f && Clock - lastStampTime < .25f)
                {
                    int steps = Mathf.Clamp(Mathf.CeilToInt(distance / spacing), 1, 24);
                    for (int i = 1; i < steps; i++)
                    {
                        float t = (float)i / steps;
                        Add(Vector3.Lerp(previous.center, center, t), Mathf.Lerp(previous.radius, radius, t), lifetime, stroke);
                    }
                }
            }
            Add(center, radius, lifetime, stroke); lastStampTime = Clock;
        }
        private void Add(Vector3 center, float radius, float lifetime, int stroke)
        {
            if (deposits.Count == MaxDeposits) deposits.RemoveAt(0);
            deposits.Add(new Deposit { center = center, radius = radius, initialRadius = radius,
                expires = Clock + lifetime, recedeSeconds = Mathf.Clamp(RecedeSeconds, .01f, lifetime), stroke = stroke });
            boundaryDirty = true;
        }
        public bool Contains(Vector3 point, float padding = 0f)
        {
            foreach (var d in deposits)
            {
                Vector3 delta = point - d.center; delta.y = 0;
                if (delta.sqrMagnitude <= (d.radius + padding) * (d.radius + padding)) return true;
            }
            return false;
        }

        private void RebuildBoundary()
        {
            if (!boundaryDirty) return;
            boundaryDirty = false; boundary.Clear();
            for (int i = 0; i < deposits.Count; i++)
            {
                var a = deposits[i]; bool hidden = false; covered.Clear();
                for (int j = 0; j < deposits.Count; j++)
                {
                    if (i == j) continue;
                    var b = deposits[j]; var delta = b.center - a.center;
                    float distance = new Vector2(delta.x, delta.z).magnitude;
                    if (distance + a.radius <= b.radius + .00001f)
                    {
                        // Equal circles retain only the newest boundary.
                        if (distance > .00001f || b.radius > a.radius + .00001f || j > i) { hidden = true; break; }
                        continue;
                    }
                    if (distance >= a.radius + b.radius || distance + b.radius <= a.radius || distance < .00001f) continue;
                    float half = Mathf.Acos(Mathf.Clamp((distance * distance + a.radius * a.radius - b.radius * b.radius) /
                        (2f * distance * a.radius), -1f, 1f));
                    float start = Mathf.Repeat(Mathf.Atan2(delta.z, delta.x) - half, Tau), end = start + half * 2f;
                    covered.Add(new Interval { from = start, to = Mathf.Min(end, Tau) });
                    if (end > Tau) covered.Add(new Interval { from = 0, to = end - Tau });
                }
                if (hidden) continue;
                covered.Sort((left, right) => left.from.CompareTo(right.from));
                float cursor = 0;
                foreach (var interval in covered)
                {
                    if (interval.from > cursor + .00001f) boundary.Add(new BoundaryArc { deposit = i, from = cursor, to = interval.from });
                    cursor = Mathf.Max(cursor, interval.to);
                }
                if (cursor < Tau - .00001f) boundary.Add(new BoundaryArc { deposit = i, from = cursor, to = Tau });
            }
        }
    }
}
