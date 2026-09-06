using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class MetaballSDFInstance : MonoBehaviour
{
    // IMPORTANT:
    // This MUST match the shader array size in every shader driven by this component.
    // The score void uses the additional capacity for a dense, varied energetic core.
    public const int MaxBalls = 48;

    [Header("Target")]
    [SerializeField] private Renderer targetRenderer;

    [Header("Shader Property Names")]
    [SerializeField] private string ballCountProperty = "_BallCount";
    [SerializeField] private string ballsProperty = "_Balls";

    [Header("Optional Colors (MaterialPropertyBlock)")]
    [SerializeField] private bool applyMaterialColors = false;
    [SerializeField] private string litColorProperty = "_LitColor";
    [SerializeField] private string unlitColorProperty = "_UnlitColor";
    [SerializeField] private string outlineColorProperty = "_OutlineColor";
    [SerializeField] private Color litColor = Color.white;
    [SerializeField] private Color unlitColor = Color.white;
    [SerializeField] private Color outlineColor = Color.black;

    [Header("Optional Container Clip (MaterialPropertyBlock)")]
    [SerializeField] private string containerClipEnabledProperty = "_ContainerClipEnabled";
    [SerializeField] private string containerCenterRadiusProperty = "_ContainerCenterRadius";

    private readonly Vector4[] _balls = new Vector4[MaxBalls];
    private int _count;
    private MaterialPropertyBlock _mpb;
    private bool _containerClipEnabled;
    private Vector4 _containerCenterRadius;

    public int Count => _count;

    public Renderer TargetRenderer
    {
        get => targetRenderer;
        set => targetRenderer = value;
    }

    private void EnsureInit()
    {
        _mpb ??= new MaterialPropertyBlock();

        // In edit mode, Awake may not run before another ExecuteAlways script calls Apply().
        if (!targetRenderer)
            targetRenderer = GetComponentInChildren<Renderer>(true);
    }

    private void Awake() => EnsureInit();
    private void OnEnable() => EnsureInit();
    private void OnValidate() => EnsureInit();
    private void Reset() => EnsureInit();

    public void Clear()
    {
        _count = 0;
    }

    public void AddBall(Vector3 centerOS, float radius)
    {
        if (_count >= MaxBalls) return;

        _balls[_count] = new Vector4(centerOS.x, centerOS.y, centerOS.z, radius);
        _count++;
    }

    public void SetBall(int index, Vector3 centerOS, float radius)
    {
        if (index < 0 || index >= MaxBalls) return;

        _balls[index] = new Vector4(centerOS.x, centerOS.y, centerOS.z, radius);
        _count = Mathf.Max(_count, index + 1);
    }

    // New API expected by ScoreVoidMetaballsVisual
    public void SetMaterialColors(Color lit, Color unlit, Color outline)
    {
        applyMaterialColors = true;
        litColor = lit;
        unlitColor = unlit;
        outlineColor = outline;
    }

    // Backwards-friendly alias (in case you used SetColors elsewhere)
    public void SetColors(Color lit, Color unlit, Color outline) => SetMaterialColors(lit, unlit, outline);

    /// <summary>
    /// Clips the rendered SDF to a half-disc in object-space XZ. A positive
    /// half-plane sign retains x values at or below the center; negative retains
    /// values at or above it.
    /// </summary>
    public void SetContainerClip(Vector2 centerXZ, float radiusOS, float halfPlaneSign, bool enabled)
    {
        _containerClipEnabled = enabled && radiusOS > 0.0001f;
        _containerCenterRadius = new Vector4(
            centerXZ.x,
            centerXZ.y,
            Mathf.Max(0.0001f, radiusOS),
            halfPlaneSign >= 0f ? 1f : -1f);
    }

    public void Apply()
    {
        EnsureInit();
        if (!targetRenderer) return;

        // Preserve other MPB values if some other script also uses MPB on this renderer.
        targetRenderer.GetPropertyBlock(_mpb);

        _mpb.SetInt(ballCountProperty, _count);
        _mpb.SetVectorArray(ballsProperty, _balls);

        if (!string.IsNullOrEmpty(containerClipEnabledProperty))
            _mpb.SetFloat(containerClipEnabledProperty, _containerClipEnabled ? 1f : 0f);
        if (!string.IsNullOrEmpty(containerCenterRadiusProperty))
            _mpb.SetVector(containerCenterRadiusProperty, _containerCenterRadius);

        if (applyMaterialColors)
        {
            if (!string.IsNullOrEmpty(litColorProperty)) _mpb.SetColor(litColorProperty, litColor);
            if (!string.IsNullOrEmpty(unlitColorProperty)) _mpb.SetColor(unlitColorProperty, unlitColor);
            if (!string.IsNullOrEmpty(outlineColorProperty)) _mpb.SetColor(outlineColorProperty, outlineColor);
        }

        targetRenderer.SetPropertyBlock(_mpb);
    }
}
