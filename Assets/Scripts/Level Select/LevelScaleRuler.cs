using UnityEngine;
using UnityEngine.UI;

/// <summary>A fixed logarithmic ruler with height-only notch transitions.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class LevelScaleRuler : MaskableGraphic
{
    [Header("Levels")]
    [SerializeField] private LevelCatalog catalog;
    [SerializeField] private LevelDefinition selectedLevel;
    [SerializeField] private RectTransform scaleValue;

    [Header("Logarithmic Bounds")]
    [Tooltip("Rounded Planck-scale reference, not a proven minimum length.")]
    [Range(-40, 40)] [SerializeField] private int minimumExponent = -35;
    [Tooltip("Rounded observable-universe diameter, not the size of the whole universe.")]
    [Range(-40, 40)] [SerializeField] private int maximumExponent = 27;

    [Header("Ruler")]
    [Min(1f)] [SerializeField] private float endPadding = 100f;
    [Min(0.5f)] [SerializeField] private float lineThickness = 2f;
    [Min(0.5f)] [SerializeField] private float minorThickness = 1.5f;
    [Min(1f)] [SerializeField] private float minorHeight = 10f;
    [Min(1f)] [SerializeField] private float majorThickness = 4f;
    [Min(1f)] [SerializeField] private float majorHeight = 28f;
    [Min(1f)] [SerializeField] private float selectedHeight = 44f;
    [Min(1f)] [SerializeField] private float peakSpacing = 12f;
    [Tooltip("Gaussian width sampled from -3 to 3 across the peak. Lower values make a narrower peak.")]
    [Range(0.5f, 3f)] [SerializeField] private float peakStandardDeviation = 1.25f;
    [Min(0f)] [SerializeField] private float labelHeight = 70f;

    [Header("Notch Animation")]
    [Min(0f)] [SerializeField] private float inSeconds = 0.5f;
    [Min(0f)] [SerializeField] private float outSeconds = 0.25f;

    private float _reveal = 1f;
    private float _fromReveal;
    private float _targetReveal;
    private float _elapsed;
    private float _duration;
    private bool _animating;

    public LevelDefinition SelectedLevel => selectedLevel;

    protected override void OnEnable()
    {
        base.OnEnable();
        _reveal = Application.isPlaying ? 0f : 1f;
        PositionScaleValue();
        if (Application.isPlaying)
            AnimateTo(1f, inSeconds);
    }

    protected override void OnDisable()
    {
        _animating = false;
        base.OnDisable();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        PositionScaleValue();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        maximumExponent = Mathf.Max(minimumExponent + 1, maximumExponent);
        base.OnValidate();
        PositionScaleValue();
    }
#endif

    private void Update()
    {
        if (!_animating)
            return;

        _elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        _reveal = Mathf.Lerp(_fromReveal, _targetReveal, Mathf.SmoothStep(0f, 1f, t));
        _animating = t < 1f;
        SetVerticesDirty();
    }

    public void PlayOut() => AnimateTo(0f, outSeconds);

    public void SetSelection(LevelDefinition level)
    {
        selectedLevel = level;
        PositionScaleValue();
        AnimateTo(1f, inSeconds);
    }

    public float GetNormalizedPosition(int exponent)
    {
        return Mathf.InverseLerp(minimumExponent, Mathf.Max(minimumExponent + 1, maximumExponent), exponent);
    }

    private void AnimateTo(float target, float duration)
    {
        _fromReveal = _reveal;
        _targetReveal = target;
        _elapsed = 0f;
        _duration = duration;
        _animating = Application.isPlaying && isActiveAndEnabled && duration > 0f;
        if (!_animating)
            _reveal = target;
        SetVerticesDirty();
    }

    private void PositionScaleValue()
    {
        if (scaleValue == null || selectedLevel == null)
            return;

        Rect rect = rectTransform.rect;
        float padding = Padding(rect);
        float x = Position(selectedLevel.scaleExponent, rect.xMin + padding, rect.xMax - padding);
        // Scale Value is a centered, direct child of this ruler.
        scaleValue.anchoredPosition = new Vector2(x - rect.center.x, labelHeight);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f)
            return;

        float padding = Padding(rect);
        float left = rect.xMin + padding;
        float right = rect.xMax - padding;
        float y = rect.center.y;
        AddRect(vh, left, y - lineThickness * 0.5f, right - left, lineThickness);

        bool hasSelection = ContainsSelection();
        int maximum = Mathf.Max(minimumExponent + 1, maximumExponent);
        const int sideNotches = 4;
        // Fit the eight flanking notches between the adjacent decade ticks so
        // background ticks can remain unchanged for every selection.
        float spacing = Mathf.Min(peakSpacing * 3f / sideNotches,
            (right - left) / (maximum - minimumExponent) * 0.75f / sideNotches);
        float sigma = Mathf.Max(0.5f, peakStandardDeviation);

        // Decades stay equally spaced across fixed bounds, regardless of the catalog.
        for (int exponent = minimumExponent; exponent <= maximum; exponent++)
        {
            if (HasLevelAt(exponent))
                continue;

            float x = Position(exponent, left, right);
            bool endpoint = exponent == minimumExponent || exponent == maximum;
            AddNotch(vh, x, y, endpoint ? majorThickness : minorThickness,
                endpoint ? majorHeight : minorHeight);
        }

        if (catalog != null && catalog.Levels != null)
        {
            foreach (LevelDefinition level in catalog.Levels)
            {
                if (level == null)
                    continue;

                float x = Position(level.scaleExponent, left, right);
                if (hasSelection && level.scaleExponent == selectedLevel.scaleExponent)
                {
                    for (int offset = -sideNotches; offset <= sideNotches; offset++)
                    {
                        // Sample a normal distribution, normalized to a unit
                        // center height. The center eases from the ordinary level
                        // height; the surrounding notches grow from zero.
                        float z = (offset * 3f / sideNotches) / sigma;
                        float height = selectedHeight * Mathf.Exp(-0.5f * z * z);
                        if (offset == 0)
                            height = Mathf.Lerp(majorHeight, selectedHeight, _reveal);
                        else
                            height *= _reveal;
                        AddNotch(vh, x + offset * spacing, y, majorThickness, height);
                    }
                }
                else
                {
                    AddNotch(vh, x, y, majorThickness, majorHeight);
                }
            }
        }

        // Gaps and shorter segments imply indeterminacy without reducing opacity.
        AddContinuation(vh, left, y, -1f, padding);
        AddContinuation(vh, right, y, 1f, padding);
    }

    private float Padding(Rect rect) => Mathf.Min(endPadding, rect.width * 0.15f);

    private float Position(int exponent, float left, float right)
    {
        return Mathf.Lerp(left, right, GetNormalizedPosition(exponent));
    }

    private bool HasLevelAt(int exponent)
    {
        if (catalog == null || catalog.Levels == null)
            return false;
        foreach (LevelDefinition level in catalog.Levels)
            if (level != null && level.scaleExponent == exponent)
                return true;
        return false;
    }

    private bool ContainsSelection()
    {
        if (selectedLevel == null || catalog == null || catalog.Levels == null)
            return false;
        foreach (LevelDefinition level in catalog.Levels)
            if (level == selectedLevel)
                return true;
        return false;
    }

    private void AddContinuation(VertexHelper vh, float edge, float y, float direction, float padding)
    {
        for (int i = 0; i < 3; i++)
        {
            float x = edge + direction * padding * (0.16f + i * 0.17f);
            float width = padding * (0.12f - i * 0.025f);
            AddRect(vh, x - width * 0.5f, y - lineThickness * 0.5f, width, lineThickness);
        }
        for (int i = 0; i < 3; i++)
        {
            float x = edge + direction * padding * (0.72f + i * 0.09f);
            AddRect(vh, x - 1.5f, y - 1.5f, 3f, 3f);
        }
    }

    private void AddNotch(VertexHelper vh, float x, float y, float width, float height)
    {
        if (height > 0f)
            AddRect(vh, x - width * 0.5f, y - height * 0.5f, width, height);
    }

    private static void AddRect(VertexHelper vh, float x, float y, float width, float height)
    {
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(x, y), Color.white, Vector2.zero);
        vh.AddVert(new Vector3(x, y + height), Color.white, Vector2.zero);
        vh.AddVert(new Vector3(x + width, y + height), Color.white, Vector2.zero);
        vh.AddVert(new Vector3(x + width, y), Color.white, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}

