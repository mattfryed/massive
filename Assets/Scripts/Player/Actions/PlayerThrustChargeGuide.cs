using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Player
{
    /// <summary>Charge telegraph: open chevrons revealed outward, using render-only geometry.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerThrustChargeGuide : ImmediateModeShapeDrawer
    {
        PlayerAttackController attack;
        PlayerControllerScript owner;
        Vector3 drawOrigin;
        Massive.Singularity.SingularityPlayerAdapter surface;
        public bool IsVisible => Application.isPlaying && isActiveAndEnabled && attack &&
            attack.isActiveAndEnabled && attack.IsCharging && attack.Effective_thrustChargeGuideEnabled;
        public int ArrowCount => attack ? attack.Effective_thrustChargeGuideArrowCount : 0;

        public override void OnEnable()
        {
            attack = GetComponent<PlayerAttackController>();
            owner = GetComponent<PlayerControllerScript>();
            surface = GetComponent<Massive.Singularity.SingularityPlayerAdapter>();
            useCullingMasks = true;
            base.OnEnable();
        }

        public float Stroke01(int index)
        {
            if (!IsVisible || index < 0 || index >= ArrowCount) return 0f;
            float slot = attack.Effective_thrustChargeMaxSeconds / ArrowCount;
            float duration = Mathf.Min(attack.Effective_thrustChargeGuideRevealSeconds, slot);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((attack.ChargeElapsed - slot * index) / duration));
        }

        public bool TryGetArrow(int index, out Vector3 left, out Vector3 tip, out Vector3 right, out float width)
        {
            left = tip = right = Vector3.zero; width = 0f;
            if (!IsVisible) return false;
            attack.GetChargeGuidePath(out var origin, out var direction, out float maxDistance);
            return ArrowGeometry(index, origin, direction, maxDistance, out left, out tip, out right, out width);
        }

        bool ArrowGeometry(int index, Vector3 origin, Vector3 direction, float maxDistance,
            out Vector3 left, out Vector3 tip, out Vector3 right, out float width)
        {
            left = tip = right = Vector3.zero; width = 0f;
            float envelope = Stroke01(index);
            if (envelope <= 0f) return false;
            float size = PlayerScaleAdjuster.SizeOf(this);
            float reach = maxDistance * attack.Effective_thrustChargeGuideDistanceFraction;
            float start = PlayerScaleAdjuster.BodyRadiusOf(owner) + .08f * size;
            if (reach <= start) return false;
            float spacing = (reach - start) / ArrowCount;
            float depth = Mathf.Min(attack.Effective_thrustChargeGuideArrowLength * size, spacing * .8f);
            float halfWidth = attack.Effective_thrustChargeGuideArrowWidth * size * .5f;
            origin.y += .03f * size;
            tip = origin + direction * (start + spacing * (index + 1));
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized * halfWidth;
            left = tip - direction * depth + side;
            right = tip - direction * depth - side;
            width = attack.Effective_thrustChargeGuideStrokeWidth * size * envelope;
            return width > .00001f;
        }

        public override void DrawShapes(Camera cam)
        {
            if (!IsVisible) return;
            attack.GetChargeGuidePath(out var origin, out var direction, out float distance);
            using (Draw.Command(cam))
            {
                Draw.ResetAllDrawStates();
                // Draw near the player origin to keep short strokes stable
                // at large world coordinates, including isolated demo galleries.
                drawOrigin = surface && surface.IsRenderingOnSurface ? surface.MapChartPoint(origin, .03f) : origin;
                Draw.Matrix = Matrix4x4.Translate(drawOrigin);
                Draw.ZTest = CompareFunction.LessEqual;
                Draw.ThicknessSpace = ThicknessSpace.Meters;
                Draw.LineGeometry = LineGeometry.Volumetric3D;
                Draw.LineEndCaps = LineEndCap.Round;
                for (int i = 0; i < ArrowCount; i++)
                {
                    if (!ArrowGeometry(i, origin, direction, distance, out var left, out var tip, out var right, out float width)) continue;
                    DrawLeg(left, tip, width);
                    DrawLeg(tip, right, width); // Deliberately no rear edge.
                }
            }
        }

        void DrawLeg(Vector3 a, Vector3 b, float width)
        {
            Color color = attack.Effective_thrustChargeGuideColor;
            if (!surface || !surface.IsRenderingOnSurface) { Draw.Line(a - drawOrigin, b - drawOrigin, width, color); return; }
            // Follow the curved SINGULARITY surface, like the existing shield rings.
            const int segments = 8;
            for (int i = 0; i < segments; i++)
            {
                Vector3 from = Vector3.Lerp(a, b, i / (float)segments);
                Vector3 to = Vector3.Lerp(a, b, (i + 1) / (float)segments);
                Color ca = color, cb = color;
                ca *= surface.EvaluateChartBrightness(from); cb *= surface.EvaluateChartBrightness(to);
                Draw.Line(surface.MapChartPoint(from, .03f) - drawOrigin, surface.MapChartPoint(to, .03f) - drawOrigin, width, ca, cb);
            }
        }
    }
}
