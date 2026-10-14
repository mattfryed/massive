using Shapes;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reveals a live incoming pane beneath a frozen capture of the outgoing demo area.</summary>
[DisallowMultipleComponent]
public sealed class HowToPlayDemoWipe : MonoBehaviour
{
    [SerializeField] private Camera screenCamera;
    [SerializeField] private Rectangle demoArea;
    [SerializeField] private Material wipeMaterial;
    [Min(.05f)] [SerializeField] private float duration = .45f;
    [Tooltip("Ripple depth as a fraction of the demo area's width.")]
    [Range(0f, .05f)] [SerializeField] private float rippleAmount = .012f;
    [Range(.0001f, .02f)] [SerializeField] private float edgeSoftness = .002f;
    [Tooltip("Inset in world units to leave the box outline untouched.")]
    [Min(0f)] [SerializeField] private float frameInset = .03f;

    private Canvas canvas;
    private RawImage image;
    private Material material;
    private RenderTexture snapshot;
    private float revealStart;
    private bool revealing;
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int DirectionId = Shader.PropertyToID("_Direction");
    private static readonly int RegionId = Shader.PropertyToID("_CaptureRect");
    private static readonly int RippleId = Shader.PropertyToID("_Ripple");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");

    public bool CanCapture => isActiveAndEnabled && screenCamera && demoArea && wipeMaterial && wipeMaterial.shader.isSupported;
    public bool IsRevealing => revealing;

    // Called at end of frame, before the carousel changes the active pane.
    public bool CaptureOutgoing()
    {
        if (!CanCapture || Screen.width < 1 || Screen.height < 1) return false;
        EnsureResources();
        canvas.enabled = false;
        ScreenCapture.CaptureScreenshotIntoRenderTexture(snapshot);
        LayoutOverlay();
        material.SetFloat(ProgressId, 0f);
        material.SetFloat(RippleId, rippleAmount);
        material.SetFloat(SoftnessId, edgeSoftness);
        image.texture = snapshot;
        canvas.enabled = true;
        return true;
    }

    public void Reveal(int direction)
    {
        if (!isActiveAndEnabled || !canvas || !canvas.enabled) return;
        material.SetFloat(DirectionId, direction > 0 ? 1f : -1f);
        revealStart = Time.unscaledTime;
        revealing = true;
    }

    private void LateUpdate()
    {
        if (!canvas || !canvas.enabled) return;
        // Resizing mid-wipe must never stretch the captured actors.
        if (snapshot.width != Screen.width || snapshot.height != Screen.height) { Finish(); return; }
        if (!revealing) return;
        float t = Mathf.Clamp01((Time.unscaledTime - revealStart) / Mathf.Max(.05f, duration));
        float eased = t * t * t * (t * (t * 6f - 15f) + 10f);
        material.SetFloat(ProgressId, eased);
        if (t >= 1f) Finish();
    }

    public void Finish()
    {
        revealing = false;
        if (canvas) canvas.enabled = false;
    }

    private void LayoutOverlay()
    {
        float halfWidth = Mathf.Max(0f, demoArea.Width * .5f - frameInset);
        float halfHeight = Mathf.Max(0f, demoArea.Height * .5f - frameInset);
        Vector3 a = screenCamera.WorldToScreenPoint(demoArea.transform.TransformPoint(new Vector3(-halfWidth, -halfHeight, 0f)));
        Vector3 b = screenCamera.WorldToScreenPoint(demoArea.transform.TransformPoint(new Vector3(halfWidth, halfHeight, 0f)));
        Vector2 min = new Vector2(Mathf.Min(a.x, b.x) / Screen.width, Mathf.Min(a.y, b.y) / Screen.height);
        Vector2 max = new Vector2(Mathf.Max(a.x, b.x) / Screen.width, Mathf.Max(a.y, b.y) / Screen.height);
        var rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        image.rectTransform.anchorMin = min;
        image.rectTransform.anchorMax = max;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        image.uvRect = rect;
        material.SetVector(RegionId, new Vector4(rect.x, rect.y, rect.width, rect.height));
    }

    private void EnsureResources()
    {
        if (!material) material = new Material(wipeMaterial) { name = "Demo wipe (runtime)" };
        if (!canvas)
        {
            var root = new GameObject("Demo wipe overlay", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var overlay = new GameObject("Frozen outgoing content", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            overlay.transform.SetParent(root.transform, false);
            image = overlay.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.material = material;
        }
        if (snapshot && snapshot.width == Screen.width && snapshot.height == Screen.height) return;
        ReleaseSnapshot();
        snapshot = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32)
        {
            name = "How To outgoing demo capture", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        snapshot.Create();
    }

    private void ReleaseSnapshot()
    {
        if (!snapshot) return;
        snapshot.Release();
        Destroy(snapshot);
        snapshot = null;
    }

    private void OnDisable()
    {
        Finish();
        ReleaseSnapshot();
        if (canvas) Destroy(canvas.gameObject);
        if (material) Destroy(material);
        canvas = null;
        image = null;
        material = null;
    }
}
