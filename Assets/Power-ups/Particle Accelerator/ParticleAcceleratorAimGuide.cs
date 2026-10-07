using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.PowerUps
{
    /// <summary>Idle targeting guide, using the turret's white volumetric dash style.</summary>
    [DisallowMultipleComponent]
    public sealed class ParticleAcceleratorAimGuide : ImmediateModeShapeDrawer
    {
        public bool IsVisible { get; private set; }
        public Vector3 Origin { get; private set; }
        public Vector3 Direction { get; private set; }
        public float Length { get; private set; }
        float size = 1;

        public void SetRay(Vector3 origin, Vector3 direction, float length, float spatialScale)
        {
            Origin = origin; Direction = direction; Length = Mathf.Max(0, length);
            size = Mathf.Max(.001f, spatialScale); IsVisible = Length > .02f;
        }

        public void Hide() { IsVisible = false; }

        public override void DrawShapes(Camera camera)
        {
            if (!IsVisible || !isActiveAndEnabled) return;
            using (Draw.Command(camera))
            {
                Draw.Matrix = Matrix4x4.identity; Draw.ZTest = CompareFunction.LessEqual;
                Draw.LineGeometry = LineGeometry.Volumetric3D; Draw.ThicknessSpace = ThicknessSpace.Meters;
                Draw.Color = Color.white; Draw.Thickness = .009f * size;
                // Match a fully charged turret: .14-unit dashes spaced every .25 units.
                float step = Mathf.Max(.25f * size, Length / 512f);
                for (float distance = 0; distance < Length; distance += step)
                    Draw.Line(Origin + Direction * distance,
                        Origin + Direction * Mathf.Min(Length, distance + .14f * size));
            }
        }
    }
}
