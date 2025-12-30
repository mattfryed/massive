using UnityEngine;
using Shapes;

/// <summary>
/// Draws a closed circle as volumetric 3D line segments (tube-like outline).
/// Designed to be controlled by SymmetryKnotVisual via reflection:
/// - Radius
/// - Thickness
/// - Color
/// </summary>
[ExecuteAlways]
public class VolumetricRingSlice : ImmediateModeShapeDrawer
{
    [Header("Ring")]
    [SerializeField, Min(0.0001f)] private float radius = 0.5f;
    [SerializeField, Min(0.0001f)] private float thickness = 0.06f;
    [SerializeField] private Color color = Color.white;

    [Tooltip("Number of segments around the circle. Higher = smoother, more cost.")]
    [Range(6, 256)]
    public int segments = 48;

    [Tooltip("Rotate the ring around its local Z axis (in degrees).")]
    public float startAngleDegrees = 0f;

    [Header("Shapes Line Style")]
    public LineGeometry lineGeometry = LineGeometry.Volumetric3D;
    public ThicknessSpace thicknessSpace = ThicknessSpace.Meters;

    // --- Properties with names that SymmetryKnotVisual's adapter already searches for ---
    public float Radius
    {
        get => radius;
        set => radius = Mathf.Max(0f, value);
    }

    public float Thickness
    {
        get => thickness;
        set => thickness = Mathf.Max(0f, value);
    }

    public Color Color
    {
        get => color;
        set => color = value;
    }

    public override void DrawShapes(Camera cam)
    {
        if (segments < 3) return;
        if (radius <= 0f || thickness <= 0f) return;

        using (Draw.Command(cam))
        {
            Draw.Matrix = transform.localToWorldMatrix;

            Draw.LineGeometry = lineGeometry;
            Draw.ThicknessSpace = thicknessSpace;
            Draw.Thickness = thickness;
            Draw.Color = color;

            float a0 = startAngleDegrees * Mathf.Deg2Rad;
            float step = Mathf.PI * 2f / segments;

            // Circle in local XY plane (normal is local +Z).
            Vector3 prev = new Vector3(Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float a = a0 + i * step;
                Vector3 next = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                Draw.Line(prev, next);
                prev = next;
            }
        }
    }
}
