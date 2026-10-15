using System.Collections.Generic;
using Shapes;
using UnityEngine;

/// <summary>Mirrored, softly tapered arrow grids around the authored chevron, pulsing outward on navigation.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class HowToPlayNavigationVectorField : ImmediateModeShapeDrawer
{
    [Header("Scene references")]
    [SerializeField] private HowToPlayCarousel carousel;
    [Tooltip("The original rounded strokes define the full-size inner region. Keep their Line components disabled.")]
    [SerializeField] private Line leftUpperBounds;
    [SerializeField] private Line leftLowerBounds;
    [SerializeField] private Shader vectorShader;
    [Tooltip("Mirror the left field around this X coordinate in this object's local space.")]
    [SerializeField] private float mirrorAxisX;

    [Header("Vector grid")]
    [Min(.04f), SerializeField] private float gridSpacing = .18f;
    [Min(.01f), SerializeField] private float idleLength = .075f;
    [Min(.01f), SerializeField] private float selectedLength = .155f;
    [Min(.001f), SerializeField] private float lineThickness = .012f;
    [Min(.001f), SerializeField] private float arrowheadLength = .033f;
    [Range(10f, 60f), SerializeField] private float arrowheadAngle = 35f;
    [Min(0f), SerializeField] private float boundaryInset = .015f;
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, .75f);
    [SerializeField] private Color selectedColor = Color.white;

    [Header("Outer taper")]
    [Tooltip("Scale of the full-size inner region relative to the authored chevron. 1 keeps the original boundary. Does not resize the outer field; capped at its boundary.")]
    [Range(.1f, 2f), SerializeField] private float innerFieldScale = 1f;
    [Tooltip("Expand the original silhouette around its center. 0.5 makes it 50% wider and taller.")]
    [Range(0f, 1f), SerializeField] private float boundaryExpansion = .5f;
    [Tooltip("0 gives a linear drop-off. 1 smoothly eases into and out of the taper. Intermediate values blend the two.")]
    [Range(0f, 1f), SerializeField] private float taperDropOffSmoothness = 1f;
    [Tooltip("Diameter of the points at the outer edge; arrow shafts and heads taper down to these.")]
    [Min(.001f), SerializeField] private float edgePointDiameter = .01f;

    [Header("Idle noise")]
    [Min(0f), SerializeField] private float idleNoiseSpeed = .18f;
    [Min(.01f), SerializeField] private float idleNoiseScale = 1.4f;
    [Range(0f, .5f), SerializeField] private float idleLengthVariation = .12f;
    [Range(0f, 20f), SerializeField] private float idleAngleDegrees = 6f;
    [Range(0f, .3f), SerializeField] private float idleBrightnessVariation = .06f;

    [Header("Selection wave")]
    [Tooltip("Delay from the innermost column to the outermost column.")]
    [Min(0f), SerializeField] private float waveTravelSeconds = .42f;
    [Min(.01f), SerializeField] private float growSeconds = .22f;
    [Min(0f), SerializeField] private float holdSeconds = .08f;
    [Min(.01f), SerializeField] private float settleSeconds = .36f;

    private struct Cell
    {
        public Vector2 tail;
        public float maximumLength, wavePosition, sizeFactor;
    }

    private const int PulseCapacity = 8;
    private readonly float[] leftPulses = new float[PulseCapacity], rightPulses = new float[PulseCapacity];
    private int nextLeftPulse, nextRightPulse;
    private Cell[] cells = System.Array.Empty<Cell>();
    private Vector2 upperStart, upperEnd, lowerStart, lowerEnd;
    private Vector2 fieldCenter;
    private float outerScale = 1f, innerScale = 1f;
    private float upperRadius, lowerRadius, depth;
    private bool gridDirty = true;
    private HowToPlayCarousel subscribedCarousel;
    private Mesh vectorMesh;
    private Material vectorMaterial;
    private Vector3[] vertices;
    private Vector2[] coordinates;
    private Vector4[] dimensions;
    private Color[] colors;
    private int meshFrame = -1;
    public int VectorsPerSide => cells.Length;

    public override void OnEnable()
    {
        ClearPulses();
        gridDirty = true;
        if (Application.isPlaying && carousel)
        {
            subscribedCarousel = carousel;
            subscribedCarousel.Navigated += Pulse;
        }
        useCullingMasks = true;
        base.OnEnable();
    }

    public override void OnDisable()
    {
        if (subscribedCarousel) subscribedCarousel.Navigated -= Pulse;
        subscribedCarousel = null;
        ClearPulses();
        base.OnDisable();
        ReleaseResource(vectorMesh);
        ReleaseResource(vectorMaterial);
        vectorMesh = null;
        vectorMaterial = null;
        meshFrame = -1;
    }

    private static void ReleaseResource(Object resource)
    {
        if (!resource) return;
        if (Application.isPlaying) Destroy(resource);
        else DestroyImmediate(resource);
    }

    private void ClearPulses()
    {
        for (int i = 0; i < PulseCapacity; i++) leftPulses[i] = rightPulses[i] = float.NegativeInfinity;
        nextLeftPulse = nextRightPulse = 0;
    }

    public void Pulse(int direction)
    {
        if (!Application.isPlaying || direction == 0) return;
        if (direction < 0) { leftPulses[nextLeftPulse] = Time.unscaledTime; nextLeftPulse = (nextLeftPulse + 1) % PulseCapacity; }
        else { rightPulses[nextRightPulse] = Time.unscaledTime; nextRightPulse = (nextRightPulse + 1) % PulseCapacity; }
    }

    private Vector2 PlanePoint(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        return new Vector2(local.x, local.z);
    }

    private void EnsureGrid()
    {
        if (!leftUpperBounds || !leftLowerBounds) { cells = System.Array.Empty<Cell>(); return; }
        Vector2 a = PlanePoint(leftUpperBounds.transform.TransformPoint(leftUpperBounds.Start));
        Vector2 b = PlanePoint(leftUpperBounds.transform.TransformPoint(leftUpperBounds.End));
        Vector2 c = PlanePoint(leftLowerBounds.transform.TransformPoint(leftLowerBounds.Start));
        Vector2 d = PlanePoint(leftLowerBounds.transform.TransformPoint(leftLowerBounds.End));
        float scale = Mathf.Max(.0001f, Mathf.Abs(transform.lossyScale.x));
        float r1 = leftUpperBounds.Thickness * .5f / scale, r2 = leftLowerBounds.Thickness * .5f / scale;
        depth = transform.InverseTransformPoint(leftUpperBounds.transform.TransformPoint(leftUpperBounds.Start)).y;
        if (!gridDirty && a == upperStart && b == upperEnd && c == lowerStart && d == lowerEnd && r1 == upperRadius && r2 == lowerRadius) return;
        gridDirty = false;
        upperStart = a; upperEnd = b; lowerStart = c; lowerEnd = d; upperRadius = r1; lowerRadius = r2;

        Vector2 min = Vector2.Min(Vector2.Min(a, b) - Vector2.one * r1, Vector2.Min(c, d) - Vector2.one * r2);
        Vector2 max = Vector2.Max(Vector2.Max(a, b) + Vector2.one * r1, Vector2.Max(c, d) + Vector2.one * r2);
        Vector2 size = max - min;
        fieldCenter = (min + max) * .5f;
        outerScale = 1f + Mathf.Max(0f, boundaryExpansion);
        innerScale = Mathf.Clamp(innerFieldScale, .01f, outerScale);
        Vector2 outerMin = fieldCenter - size * (.5f * outerScale);
        Vector2 outerMax = fieldCenter + size * (.5f * outerScale);
        // Bound the work even if an author makes the silhouette enormous or the grid very dense.
        float spacing = Mathf.Max(.04f, Mathf.Max(gridSpacing, Mathf.Max(size.x, size.y) * outerScale / 63f));
        int columns = Mathf.Max(1, Mathf.FloorToInt(size.x / spacing));
        int rows = Mathf.Max(1, Mathf.FloorToInt(size.y / spacing));
        // Retain the original grid's anchors while adding rows around it.
        Vector2 first = fieldCenter - new Vector2(columns - 1, rows - 1) * (spacing * .5f);
        int firstColumn = Mathf.CeilToInt((outerMin.x - first.x) / spacing);
        int lastColumn = Mathf.FloorToInt((outerMax.x - first.x) / spacing);
        int firstRow = Mathf.CeilToInt((outerMin.y - first.y) / spacing);
        int lastRow = Mathf.FloorToInt((outerMax.y - first.y) / spacing);
        float peak = Mathf.Max(idleLength * (1f + idleLengthVariation), selectedLength);
        float headWidth = arrowheadLength * Mathf.Tan(arrowheadAngle * Mathf.Deg2Rad);
        float pointInset = boundaryInset + edgePointDiameter * .5f;
        var result = new List<Cell>((lastColumn - firstColumn + 1) * (lastRow - firstRow + 1));
        for (int row = firstRow; row <= lastRow; row++)
        for (int col = firstColumn; col <= lastColumn; col++)
        {
            Vector2 tail = first + new Vector2(col, row) * spacing;
            if (!Contains(tail, pointInset)) continue;
            float outsideInner = Mathf.Max(0f, InnerDistance(tail));
            float room = Mathf.Max(0f, -OuterDistance(tail) - pointInset);
            // Finish the morph within half a grid step so the final row is visibly made of points.
            float taperPosition = outsideInner <= 0f ? 0f :
                outsideInner / (outsideInner + Mathf.Max(0f, room - spacing * .5f));
            float taper = 1f - Mathf.Lerp(taperPosition, Ease(taperPosition), Mathf.Clamp01(taperDropOffSmoothness));
            float inset = pointInset + (headWidth + lineThickness * .5f +
                peak * Mathf.Sin(idleAngleDegrees * Mathf.Deg2Rad)) * taper;
            float available = peak * taper;
            if (!Contains(tail, inset)) available = 0f;
            else if (!Contains(tail + Vector2.left * available, inset))
            {
                float low = 0f, high = available;
                for (int i = 0; i < 12; i++)
                {
                    float middle = (low + high) * .5f;
                    if (Contains(tail + Vector2.left * middle, inset)) low = middle;
                    else high = middle;
                }
                available = low;
            }
            result.Add(new Cell { tail = tail, maximumLength = available, sizeFactor = taper,
                wavePosition = Mathf.InverseLerp(outerMax.x, outerMin.x, tail.x) });
        }
        cells = result.ToArray();
        meshFrame = -1;
    }

    private bool Contains(Vector2 point, float inset)
    {
        return OuterDistance(point) <= -inset;
    }

    private float SignedDistance(Vector2 point) => Mathf.Min(
        Mathf.Sqrt(DistanceToSegmentSquared(point, upperStart, upperEnd)) - upperRadius,
        Mathf.Sqrt(DistanceToSegmentSquared(point, lowerStart, lowerEnd)) - lowerRadius);

    private float OuterDistance(Vector2 point) =>
        SignedDistance(fieldCenter + (point - fieldCenter) / outerScale) * outerScale;

    private float InnerDistance(Vector2 point) =>
        SignedDistance(fieldCenter + (point - fieldCenter) / innerScale) * innerScale;

    private static float DistanceToSegmentSquared(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        float t = Mathf.Clamp01(Vector2.Dot(point - start, delta) / Mathf.Max(.000001f, delta.sqrMagnitude));
        return (point - start - delta * t).sqrMagnitude;
    }

    private static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private float Wave(float now, float wavePosition, float[] pulses)
    {
        float remaining = 1f;
        float grow = Mathf.Max(.01f, growSeconds), settle = Mathf.Max(.01f, settleSeconds);
        for (int i = 0; i < pulses.Length; i++)
        {
            float age = now - pulses[i] - wavePosition * Mathf.Max(0f, waveTravelSeconds);
            if (age < 0f || age >= grow + holdSeconds + settle) continue;
            float amount = age < grow ? Ease(age / grow) :
                1f - Ease((age - grow - holdSeconds) / settle);
            // Overlapping selections blend continuously, without snapping an existing wave back to idle.
            remaining *= 1f - amount;
        }
        return 1f - remaining;
    }

    public override void DrawShapes(Camera camera)
    {
        EnsureGrid();
        if (cells.Length == 0 || !vectorShader) return;
        EnsureMesh();
        if (!Application.isPlaying || meshFrame != Time.frameCount) UpdateMesh();
        using (Draw.Command(camera))
        {
            Draw.Matrix = transform.localToWorldMatrix;
            // A single mesh avoids the project's D3D11 Shapes instanced-line issue.
            Draw.Mesh(vectorMesh, vectorMaterial);
        }
    }

    private void EnsureMesh()
    {
        if (!vectorMaterial || vectorMaterial.shader != vectorShader)
        {
            ReleaseResource(vectorMaterial);
            vectorMaterial = new Material(vectorShader) { name = "Navigation vectors (generated)", hideFlags = HideFlags.HideAndDontSave };
        }
        if (vectorMesh && vectorMesh.vertexCount == cells.Length * 8) return;
        ReleaseResource(vectorMesh);
        vectorMesh = new Mesh { name = "Navigation vector grid (generated)", hideFlags = HideFlags.HideAndDontSave };
        vectorMesh.MarkDynamic();
        int count = cells.Length * 8;
        vertices = new Vector3[count];
        coordinates = new Vector2[count];
        dimensions = new Vector4[count];
        colors = new Color[count];
        int[] indices = new int[cells.Length * 12];
        for (int quad = 0; quad < cells.Length * 2; quad++)
        {
            int v = quad * 4, t = quad * 6;
            indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
            indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
        }
        vectorMesh.vertices = vertices;
        vectorMesh.triangles = indices;
        meshFrame = -1;
    }

    private void UpdateMesh()
    {
        float now = Application.isPlaying ? Time.unscaledTime : 0f;
        for (int i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            WriteVector(i * 8, cell, -1, Wave(now, cell.wavePosition, leftPulses), now);
            WriteVector(i * 8 + 4, cell, 1, Wave(now, cell.wavePosition, rightPulses), now);
        }
        vectorMesh.vertices = vertices;
        vectorMesh.uv = coordinates;
        vectorMesh.SetUVs(1, dimensions);
        vectorMesh.colors = colors;
        vectorMesh.RecalculateBounds();
        meshFrame = Time.frameCount;
    }

    private void WriteVector(int index, Cell cell, int direction, float amount, float now)
    {
        float noiseX = (cell.tail.x - fieldCenter.x) * idleNoiseScale + 31.7f + (direction > 0 ? 47.3f : 0f);
        float noiseY = (cell.tail.y - fieldCenter.y) * idleNoiseScale + 19.2f;
        float time = now * idleNoiseSpeed;
        float flow = Mathf.PerlinNoise(noiseX + time, noiseY - time * .43f) * 2f - 1f;
        float turn = Mathf.PerlinNoise(noiseX - time * .61f + 13.8f, noiseY + time + 8.4f) * 2f - 1f;
        float length = Mathf.Min(cell.maximumLength, cell.sizeFactor *
            Mathf.Lerp(idleLength * (1f + flow * idleLengthVariation), Mathf.Max(idleLength, selectedLength), amount));
        float x = direction < 0 ? cell.tail.x : mirrorAxisX * 2f - cell.tail.x;
        float headLength = Mathf.Min(arrowheadLength * cell.sizeFactor, length * .4f);
        float headWidth = headLength * Mathf.Tan(arrowheadAngle * Mathf.Deg2Rad);
        float radius = Mathf.Lerp(edgePointDiameter, lineThickness, cell.sizeFactor) * .5f;
        float padding = radius + .025f;
        Color color = Color.Lerp(idleColor, selectedColor, amount);
        color.a *= 1f + flow * idleBrightnessVariation * (1f - amount);
        var shape = new Vector4(length, headLength, headWidth, radius);
        float angle = turn * idleAngleDegrees * Mathf.Deg2Rad * (1f - amount);
        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
        for (int corner = 0; corner < 4; corner++)
        {
            float along = corner == 0 || corner == 3 ? -padding : length + padding;
            float across = (corner < 2 ? -1f : 1f) * (headWidth + padding);
            int v = index + corner;
            vertices[v] = new Vector3(x + direction * (along * cos - across * sin),
                depth, cell.tail.y + along * sin + across * cos);
            coordinates[v] = new Vector2(along, across);
            dimensions[v] = shape;
            colors[v] = color;
        }
    }

    private void OnValidate() { gridDirty = true; }
}
