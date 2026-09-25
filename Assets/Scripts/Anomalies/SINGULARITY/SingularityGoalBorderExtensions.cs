using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Singularity
{
    /// <summary>Four straight presentation-only joins. The authored goal discs,
    /// scoring volumes and standard scene transforms remain unchanged.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SingularityGoalBorderExtensions : ImmediateModeShapeDrawer
    {
        public SingularityGridRenderer grid;
        public Disc leftGoalOutline;
        public Disc rightGoalOutline;
        [Tooltip("Follow the moving folded border during Amplifier capture/held effects.")]
        public bool followAmplifierEffects = true;

        public bool TryGetSegment(int team, bool top, out Vector3 start, out Vector3 end, out Color color)
        {
            start = end = Vector3.zero; color = Color.white;
            var goal = team == 1 ? leftGoalOutline : team == 2 ? rightGoalOutline : null;
            if (!isActiveAndEnabled || !grid || !grid.isActiveAndEnabled || !grid.surface || !goal || !goal.isActiveAndEnabled)
                return false;
            var surface = grid.surface;
            Vector3 a = GoalEndpoint(goal, goal.AngRadiansStart);
            Vector3 b = GoalEndpoint(goal, goal.AngRadiansEnd);
            float aZ = surface.transform.InverseTransformPoint(a).z;
            float bZ = surface.transform.InverseTransformPoint(b).z;
            start = (top ? aZ >= bZ : aZ <= bZ) ? a : b;
            float s = top ? surface.TopCrestDistance : surface.BottomCrestDistance;
            Vector2 logical = new Vector2(surface.Width * .5f * (team == 1 ? -1f : 1f), s);
            Color borderColor;
            end = followAmplifierEffects ? grid.EvaluateAmplifierPoint(logical, out borderColor)
                : surface.WorldPosition(logical.x, logical.y);
            // Goals are deliberately UI-depth overlays. Project the joined end
            // into that same plane, retaining its exact XZ screen alignment.
            Vector3 normal = goal.transform.forward.normalized;
            end -= normal * Vector3.Dot(end - goal.transform.position, normal);
            color = goal.Color;
            return (end - start).sqrMagnitude > .00000001f;
        }

        private static Vector3 GoalEndpoint(Disc goal, float angle)
        {
            return goal.transform.TransformPoint(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * goal.Radius);
        }

        public override void DrawShapes(Camera cam)
        {
            if (!grid || !grid.surface) return;
            for (int team = 1; team <= 2; team++)
            {
                Disc goal = team == 1 ? leftGoalOutline : rightGoalOutline;
                if (!goal) continue;
                float scale = goal.ThicknessSpace == ThicknessSpace.Meters
                    ? Mathf.Max(Mathf.Abs(goal.transform.lossyScale.x), Mathf.Abs(goal.transform.lossyScale.y)) : 1f;
                for (int side = 0; side < 2; side++)
                {
                    if (!TryGetSegment(team, side == 0, out Vector3 a, out Vector3 b, out Color tint)) continue;
                    // Four explicit strokes avoid the Shapes instanced-line path,
                    // which drops these world-space joins on the project's D3D11
                    // configuration. Do not change global Shapes instancing settings.
                    using (Draw.Command(cam))
                    {
                        Draw.ResetAllDrawStates(); Draw.Matrix = Matrix4x4.identity;
                        Draw.ZTest = CompareFunction.LessEqual;
                        Draw.LineGeometry = LineGeometry.Billboard;
                        Draw.LineEndCaps = LineEndCap.None;
                        Draw.ThicknessSpace = goal.ThicknessSpace;
                        Draw.Color = tint; Draw.Line(a, b, goal.Thickness * scale);
                    }
                }
            }
        }
    }
}
