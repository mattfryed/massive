#if UNITY_EDITOR
using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Player
{
    /// <summary>Editor-only tuning guides follow the live collider; never participate in hits or appear in player builds.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRepulsorRangeRings : ImmediateModeShapeDrawer
    {
        PlayerRepulsorAOE pulse;
        Massive.Singularity.SingularityPlayerAdapter surface;
        public override void OnEnable()
        {
            pulse = GetComponent<PlayerRepulsorAOE>();
            surface = GetComponentInParent<Massive.Singularity.SingularityPlayerAdapter>();
            useCullingMasks = true;
            base.OnEnable();
        }
        public override void DrawShapes(Camera cam)
        {
            if (!pulse || !pulse.isActiveAndEnabled || !pulse.IsPulseActive || !pulse.ShowDamageRings) return;
            using (Draw.Command(cam))
            {
                Draw.ResetAllDrawStates();
                Draw.ZTest = CompareFunction.LessEqual;
                DrawRing(pulse.RadiusWorld, Color.yellow);
                DrawRing(pulse.ActiveInnerRadiusWorld, Color.red);
            }
        }
        void DrawRing(float radius, Color color)
        {
            if (radius <= 0f) return;
            float dot = pulse.DamageRingDotRadiusWorld;
            int count = DotCount(radius, dot);
            for (int i = 0; i < count; i++)
            {
                float angle = i * (Mathf.PI * 2f / count);
                Vector3 point = pulse.OriginWorld + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (surface && surface.IsRenderingOnSurface)
                {
                    float brightness = surface.EvaluateChartBrightness(point);
                    Draw.Sphere(surface.MapChartPoint(point, .025f), dot, color * new Color(brightness, brightness, brightness, 1f));
                }
                else Draw.Disc(point + Vector3.up * .025f, Vector3.up, dot, color);
            }
        }
        static int DotCount(float radius, float dot) => Mathf.Clamp(Mathf.CeilToInt(radius / Mathf.Max(.001f, dot)), 24, 192);
        public static void DrawEditorGuides(Vector3 center, Vector3 normal, float inner, float outer, float dot)
        {
            Color previous = UnityEditor.Handles.color;
            DrawEditorRing(center, normal, outer, dot, Color.yellow);
            DrawEditorRing(center, normal, inner, dot, Color.red);
            UnityEditor.Handles.color = previous;
        }
        static void DrawEditorRing(Vector3 center, Vector3 normal, float radius, float dot, Color color)
        {
            if (radius <= 0f) return;
            UnityEditor.Handles.color = color;
            Quaternion plane = Quaternion.FromToRotation(Vector3.forward, normal);
            int count = DotCount(radius, dot);
            for (int i = 0; i < count; i++)
            {
                float angle = i * (Mathf.PI * 2f / count);
                Vector3 point = center + plane * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                UnityEditor.Handles.DrawSolidDisc(point, normal, dot);
            }
        }
    }
}
#endif
