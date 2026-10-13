using UnityEngine;

namespace Massive.Emergence
{
    public sealed partial class EmergenceSimulation
    {
        // Per-particle memory survives root changes, splits and merges without assigning permanent organisms.
        readonly Vector2[] swimVelocity, groupSwim;
        readonly bool[] matureGroups;
        readonly int[] identities;

        static uint SwimHash(uint x)
        {
            unchecked { x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; return x ^ (x >> 16); }
        }
        static float SwimRandom(uint key) => (SwimHash(key) & 0xffffffu) / 16777216f;
        static float TurnTo(Vector2 heading, Vector2 target) => Mathf.Atan2(heading.x * target.y - heading.y * target.x, Vector2.Dot(heading, target));

        void SteerOrganisms(float dt)
        {
            float blend = 1 - Mathf.Exp(-dt / profile.swimResponseSeconds);
            float sense = profile.interactionRadius * profile.swimSenseRadiusMultiple;
            for (int r = 0; r < bodyParticles; r++)
            {
                if (sizes[r] == 0) continue;
                Vector2 previous = groupSwim[r] / sizes[r];
                if (!profile.enableSwimming || !viable[r] || !matureGroups[r] || profile.swimSpeedFraction <= 0)
                { groupSwim[r] = previous * (1 - blend); continue; }
                uint key = SwimHash((uint)identities[r] ^ (uint)profile.seed);
                float phase = SwimRandom(key) * Mathf.PI * 2;
                Vector2 heading = previous.sqrMagnitude > .0000000001f ? previous.normalized : new Vector2(Mathf.Cos(phase), Mathf.Sin(phase));
                float time = Age / profile.swimWanderSeconds;
                uint segment = (uint)Mathf.Floor(time);
                float t = time - segment; t = t * t * (3 - 2 * t);
                float noise = Mathf.Lerp(SwimRandom(key ^ SwimHash(segment)), SwimRandom(key ^ SwimHash(segment + 1)), t) * 2 - 1;
                float turn = profile.swimTurnSpeed * Mathf.Deg2Rad;
                float rate = noise * profile.swimWanderStrength * turn;
                Vector2 center = centers[r] / sizes[r], food = Vector2.zero;
                if (profile.swimFoodBias > 0)
                {
                    CellRange(center, sense, out int x0, out int x1, out int y0, out int y1);
                    for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                        for (int j = heads[y * columns + x]; j >= 0; j = next[j])
                        {
                            var q = Particles[j];
                            if (q.species != EmergenceProfile.NutrientSpecies || !q.Alive || q.feedingPhase != 0) continue;
                            Vector2 delta = q.position - center; float d = delta.magnitude;
                            if (d <= .00001f || d >= sense) continue;
                            float weight = 1 - d / sense; food += delta * (weight * weight / d);
                        }
                    if (food.sqrMagnitude > .00000001f) rate += Mathf.Clamp(TurnTo(heading, food), -1, 1) * turn * profile.swimFoodBias;
                }
                float margin = profile.interactionRadius * profile.swimWallRangeMultiple + Mathf.Sqrt(sizes[r]) * maximumRadius;
                Vector2 danger = new Vector2(Mathf.Clamp01(1 - (HalfSize.x - Mathf.Abs(center.x)) / margin),
                    Mathf.Clamp01(1 - (HalfSize.y - Mathf.Abs(center.y)) / margin));
                Vector2 wall = new Vector2(-Mathf.Sign(center.x) * danger.x * danger.x, -Mathf.Sign(center.y) * danger.y * danger.y);
                if (wall.sqrMagnitude > .00000001f) rate += Mathf.Clamp(TurnTo(heading, wall), -1, 1) * turn * Mathf.Max(danger.x, danger.y) * 2;
                float angle = Mathf.Clamp(rate, -turn, turn) * dt;
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                Vector2 direction = new Vector2(heading.x * c - heading.y * s, heading.x * s + heading.y * c);
                float speed = profile.speedLimit * profile.swimSpeedFraction * (1 + profile.swimPulseAmount * Mathf.Sin(Age * Mathf.PI * 2 / profile.swimPulseSeconds + phase));
                groupSwim[r] = direction * Mathf.Lerp(previous.magnitude, speed, blend);
            }
        }
    }
}
