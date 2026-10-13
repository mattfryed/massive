using UnityEngine;

namespace Massive.Emergence
{
    public sealed partial class EmergenceSimulation
    {
        static float Ease(float t) => t * t * (3 - 2 * t);

        void AdvanceNutrientTransitions(float dt)
        {
            for (int i = bodyParticles; i < nutrientEnd; i++)
            {
                var p = Particles[i];
                if (p.feedingPhase == 1 || p.feedingPhase == 2)
                {
                    var target = Particles[p.feedingTarget];
                    Vector2 delta = p.feedingStart - target.position;
                    Vector2 direction = delta.sqrMagnitude > .00000001f ? delta.normalized : Vector2.right;
                    if (p.feedingPhase == 1)
                    {
                        p.transition = Mathf.Min(1, p.transition + dt / profile.feedingApproachSeconds);
                        p.position = Vector2.Lerp(p.feedingStart, target.position + direction * (target.radius + p.radius), Ease(p.transition));
                        if (p.transition >= 1) { p.feedingPhase = 2; p.transition = 0; }
                    }
                    else
                    {
                        p.transition = Mathf.Min(1, p.transition + dt / profile.particleShrinkSeconds);
                        p.visualScale = 1 - Ease(p.transition);
                        p.position = target.position + direction * (target.radius + p.radius * p.visualScale);
                        if (p.transition >= 1) { p.feedingPhase = 0; p.transition = 0; }
                    }
                    Vector2 bound = HalfSize - Vector2.one * (p.radius + ContactSkin);
                    p.position = new Vector2(Mathf.Clamp(p.position.x, -bound.x, bound.x), Mathf.Clamp(p.position.y, -bound.y, bound.y));
                }
                else if (p.feedingPhase == 3)
                {
                    p.transition = Mathf.Min(1, p.transition + dt / profile.particleGrowSeconds);
                    p.visualScale = Ease(p.transition);
                    if (p.transition >= 1) { p.feedingPhase = 0; p.transition = 0; }
                }
                Particles[i] = p;
            }
        }
    }
}
