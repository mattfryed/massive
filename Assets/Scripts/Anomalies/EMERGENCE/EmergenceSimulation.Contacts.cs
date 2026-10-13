using UnityEngine;

namespace Massive.Emergence
{
    public sealed partial class EmergenceSimulation
    {
        const float ContactSkin = .0002f;
        void AddToGrid(int i)
        {
            int cell = Cell(Particles[i].position);
            next[i] = heads[cell]; heads[cell] = i;
        }
        void CellRange(Vector2 point, float reach, out int x0, out int x1, out int y0, out int y1)
        {
            int a = Cell(point - Vector2.one * reach), b = Cell(point + Vector2.one * reach);
            x0 = a % columns; x1 = b % columns; y0 = a / columns; y1 = b / columns;
        }
        bool IsClear(int ignore, Vector2 point, float radius)
        {
            CellRange(point, radius + maximumRadius + contactGap + ContactSkin, out int x0, out int x1, out int y0, out int y1);
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                for (int j = heads[y * columns + x]; j >= 0; j = next[j])
                {
                    if (j == ignore || !Particles[j].Alive) continue;
                    float distance = radius + Particles[j].radius + contactGap;
                    if ((point - Particles[j].position).sqrMagnitude < distance * distance) return false;
                }
            return true;
        }
        bool TryFindFree(float radius, float coverage, int attempts, out Vector2 point)
        {
            Vector2 extents = HalfSize * coverage - Vector2.one * (radius + ContactSkin);
            point = Vector2.zero;
            if (extents.x <= 0 || extents.y <= 0) return false;
            for (int k = 0; k < attempts; k++)
            {
                point = new Vector2(Range(-extents.x, extents.x), Range(-extents.y, extents.y));
                if (IsClear(-1, point, radius + ContactSkin)) return true;
            }
            return false;
        }
        void MoveWithinBounds(ref Particle p, float dt)
        {
            Vector2 bound = HalfSize - Vector2.one * (p.radius + ContactSkin);
            p.position += p.velocity * dt;
            if (p.position.x < -bound.x || p.position.x > bound.x)
            {
                p.position.x = Mathf.Clamp(p.position.x, -bound.x, bound.x);
                p.velocity.x *= -profile.wallBounce;
            }
            if (p.position.y < -bound.y || p.position.y > bound.y)
            {
                p.position.y = Mathf.Clamp(p.position.y, -bound.y, bound.y);
                p.velocity.y *= -profile.wallBounce;
            }
        }
        // Diagnostics only: movement leaves the grid at the start-of-tick positions.
        public float MaximumPenetration(bool includeGap = true)
        {
            BuildGrid(); float maximum = 0;
            for (int i = 0; i < Particles.Length; i++)
            {
                var p = Particles[i]; if (!p.Alive) continue;
                CellRange(p.position, p.radius + maximumRadius + contactGap, out int x0, out int x1, out int y0, out int y1);
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                    for (int j = heads[y * columns + x]; j >= 0; j = next[j])
                        if (j > i && Particles[j].Alive) maximum = Mathf.Max(maximum, p.radius + Particles[j].radius + (includeGap ? contactGap : 0) - Vector2.Distance(p.position, Particles[j].position));
            }
            return maximum;
        }
    }
}
