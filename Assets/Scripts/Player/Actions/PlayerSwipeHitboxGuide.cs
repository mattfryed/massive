#if UNITY_EDITOR
using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Player
{
    /// <summary>Editor-only top-down outline of the current Swipe collider, sampled at camera render time.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSwipeHitboxGuide : ImmediateModeShapeDrawer
    {
        PlayerMelee melee;
        Massive.Singularity.SingularityPlayerAdapter surface;
        public CapsuleCollider Hitbox => melee ? melee.TuningHitbox as CapsuleCollider : null;
        public bool IsVisible
        {
            get
            {
                var attack = melee ? melee.TuningAttackController : null;
                var stage = attack ? attack.CurrentStage : null;
                return Application.isPlaying && melee && melee.isActiveAndEnabled &&
                    attack && attack.isActiveAndEnabled && attack.IsAttacking &&
                    stage != null && stage.StageType == AttackStageType.ComboSwipe && stage.ShowSwipeHitboxGuide &&
                    Hitbox && Hitbox.enabled && Hitbox.gameObject.activeInHierarchy;
            }
        }
        public override void OnEnable()
        {
            melee = GetComponent<PlayerMelee>();
            surface = GetComponentInParent<Massive.Singularity.SingularityPlayerAdapter>();
            useCullingMasks = true;
            base.OnEnable();
        }
        public override void DrawShapes(Camera cam)
        {
            if (!IsVisible || !TryGetFootprint(Hitbox, out Vector3 a, out Vector3 b, out float radius)) return;
            float dot = Mathf.Max(.005f, PlayerScaleAdjuster.BodyRadiusOf(melee.Owner) * .055f);
            float perimeter = 2f * Vector3.Distance(a, b) + 2f * Mathf.PI * radius;
            int count = Mathf.Clamp(Mathf.CeilToInt(perimeter / (dot * 6f)), 24, 192);
            using (Draw.Command(cam))
            {
                Draw.ResetAllDrawStates();
                Draw.ZTest = CompareFunction.LessEqual;
                for (int i = 0; i < count; i++)
                {
                    Vector3 point = FootprintPoint(a, b, radius, perimeter * i / count);
                    if (surface && surface.IsRenderingOnSurface)
                    {
                        float brightness = surface.EvaluateChartBrightness(point);
                        Draw.Sphere(surface.MapChartPoint(point, .025f), dot, new Color(brightness, brightness, 0f, 1f));
                    }
                    else Draw.Disc(point + Vector3.up * .025f, Vector3.up, dot, Color.yellow);
                }
            }
        }
        public static bool TryGetFootprint(CapsuleCollider capsule, out Vector3 a, out Vector3 b, out float radius)
        {
            a = b = Vector3.zero; radius = 0f;
            if (!capsule) return false;
            var scale = capsule.transform.lossyScale;
            int axis = capsule.direction;
            float along = Mathf.Abs(scale[axis]);
            float across = Mathf.Max(Mathf.Abs(scale[(axis + 1) % 3]), Mathf.Abs(scale[(axis + 2) % 3]));
            radius = capsule.radius * across;
            float halfSegment = Mathf.Max(0f, capsule.height * along * .5f - radius);
            Vector3 localAxis = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
            Vector3 segment = capsule.transform.TransformVector(localAxis).normalized * halfSegment;
            // Orthographic projection of a 3D capsule onto the XZ gameplay plane.
            segment.y = 0f;
            Vector3 center = capsule.transform.TransformPoint(capsule.center);
            a = center - segment; b = center + segment;
            return radius > 0f;
        }
        public static Vector3 FootprintPoint(Vector3 a, Vector3 b, float radius, float distance)
        {
            float length = Vector3.Distance(a, b);
            Vector3 axis = length > .00001f ? (b - a) / length : Vector3.right;
            Vector3 side = new Vector3(-axis.z, 0f, axis.x);
            float arcLength = Mathf.PI * radius;
            if (distance < length) return a + side * radius + axis * distance;
            distance -= length;
            if (distance < arcLength)
            {
                float angle = distance / radius;
                return b + (side * Mathf.Cos(angle) + axis * Mathf.Sin(angle)) * radius;
            }
            distance -= arcLength;
            if (distance < length) return b - side * radius - axis * distance;
            float finalAngle = (distance - length) / radius;
            return a - (side * Mathf.Cos(finalAngle) + axis * Mathf.Sin(finalAngle)) * radius;
        }
    }
}
#endif
