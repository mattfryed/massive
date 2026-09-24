using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs an icon in a stationary capture stage so world-space trails record only
/// the effect's own motion. The carousel carries its live image instead.
/// Built-in render pipeline; intended for uniformly scaled menu icons.
/// </summary>
[DisallowMultipleComponent]
public sealed class LiveParticleIcon : MonoBehaviour
{
    [Header("Original effect")]
    [SerializeField] private GameObject sourcePrefab;
    [SerializeField] private Shader displayShader;
    [SerializeField] private Camera viewCamera;

    [Header("Image quality")]
    [Tooltip("Maximum capture resolution. Uses the viewing camera's aspect and pixel size up to this limit.")]
    [SerializeField] private Vector2Int textureSize = new Vector2Int(3840, 2160);
    [Tooltip("Conservative visibility area at the original prefab scale. Include the full trail reach.")]
    [SerializeField] private Vector2 captureSize = new Vector2(6f, 6f);
    [Tooltip("0 follows the active Quality setting; otherwise use 1, 2, 4, or 8.")]
    [SerializeField] private int antiAliasing = 2;

    private static readonly HashSet<int> OccupiedStages = new HashSet<int>();
    private GameObject stage;
    private GameObject sourceInstance;
    private Camera captureCamera;
    private Transform display;
    private MeshRenderer displayRenderer;
    private Mesh displayMesh;
    private Material displayMaterial;
    private RenderTexture texture;
    private int stageSlot = -1;

    public RenderTexture Texture => texture;

    private void OnEnable()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (stage != null) return;
        if (viewCamera == null) viewCamera = Camera.main;
        if (sourcePrefab == null || displayShader == null || viewCamera == null)
        {
            Debug.LogError("Live particle icon needs a source prefab, display shader, and viewing camera.", this);
            enabled = false;
            return;
        }

        stageSlot = 0;
        while (OccupiedStages.Contains(stageSlot)) stageSlot++;
        OccupiedStages.Add(stageSlot);

        stage = new GameObject(name + " Capture Stage");
        SceneManager.MoveGameObjectToScene(stage, gameObject.scene);
        // Outside the menu camera's far plane. Separate stages also lie outside
        // one another's capture frusta; no shared layer settings change.
        stage.transform.position = new Vector3(10000f + stageSlot * 128f, 0f, 0f);
        sourceInstance = Instantiate(sourcePrefab, stage.transform);
        sourceInstance.name = sourcePrefab.name + " (Live Source)";
        sourceInstance.transform.localPosition = Vector3.zero;
        sourceInstance.transform.localRotation = Quaternion.identity;
        foreach (var collider in sourceInstance.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (var force in sourceInstance.GetComponentsInChildren<GridForceSource>(true))
            force.enabled = false;
        foreach (var particles in sourceInstance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        }

        var cameraObject = new GameObject("Icon Capture Camera");
        cameraObject.transform.SetParent(stage.transform, false);
        captureCamera = cameraObject.AddComponent<Camera>();
        captureCamera.CopyFrom(viewCamera);
        captureCamera.enabled = false;
        captureCamera.depth = viewCamera.depth - 1f;
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = Color.clear;
        captureCamera.useOcclusionCulling = false;
        captureCamera.rect = new Rect(0f, 0f, 1f, 1f);

        float resolutionScale = Mathf.Min(1f,
            (float)textureSize.x / Mathf.Max(1, viewCamera.pixelWidth),
            (float)textureSize.y / Mathf.Max(1, viewCamera.pixelHeight));
        var descriptor = new RenderTextureDescriptor(
            Mathf.Clamp(Mathf.RoundToInt(viewCamera.pixelWidth * resolutionScale), 128, SystemInfo.maxTextureSize),
            Mathf.Clamp(Mathf.RoundToInt(viewCamera.pixelHeight * resolutionScale), 128, SystemInfo.maxTextureSize),
            RenderTextureFormat.ARGBHalf, 24);
        int samples = antiAliasing == 0 ? QualitySettings.antiAliasing : antiAliasing;
        descriptor.msaaSamples = samples >= 8 ? 8 : samples >= 4 ? 4 : samples >= 2 ? 2 : 1;
        descriptor.msaaSamples = SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
        descriptor.useMipMap = false;
        descriptor.autoGenerateMips = false;
        texture = new RenderTexture(descriptor)
        {
            name = name + " Live Image",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.Create();
        captureCamera.targetTexture = texture;

        displayMesh = new Mesh { name = "Live Particle Icon Quad" };
        displayMesh.vertices = new[] {
            new Vector3(-.5f, -.5f, 0f), new Vector3(.5f, -.5f, 0f),
            new Vector3(.5f, .5f, 0f), new Vector3(-.5f, .5f, 0f) };
        displayMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        displayMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        displayMesh.RecalculateBounds();
        displayMaterial = new Material(displayShader) { name = name + " Live Display" };
        displayMaterial.mainTexture = texture;
        var displayObject = new GameObject("Live Image", typeof(MeshFilter), typeof(MeshRenderer));
        display = displayObject.transform;
        display.SetParent(transform, false);
        displayObject.layer = gameObject.layer;
        displayObject.GetComponent<MeshFilter>().sharedMesh = displayMesh;
        displayRenderer = displayObject.GetComponent<MeshRenderer>();
        displayRenderer.sharedMaterial = displayMaterial;
        displayRenderer.shadowCastingMode = ShadowCastingMode.Off;
        displayRenderer.receiveShadows = false;
        displayRenderer.lightProbeUsage = LightProbeUsage.Off;
        displayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        displayRenderer.enabled = false;
    }

    private void LateUpdate()
    {
        if (stage == null || viewCamera == null) return;

        float scale = Mathf.Abs(transform.lossyScale.x / sourcePrefab.transform.localScale.x);
        Vector3 center = viewCamera.WorldToViewportPoint(transform.position);
        if (center.z <= viewCamera.nearClipPlane || scale < .0001f)
        {
            displayRenderer.enabled = false;
            captureCamera.enabled = false;
            return;
        }

        Vector2 size = captureSize * scale;
        Vector3 halfRight = viewCamera.transform.right * (size.x * .5f);
        Vector3 halfUp = viewCamera.transform.up * (size.y * .5f);
        Vector3 lower = viewCamera.WorldToViewportPoint(transform.position - halfRight - halfUp);
        Vector3 upper = viewCamera.WorldToViewportPoint(transform.position + halfRight + halfUp);
        if (upper.x < 0f || lower.x > 1f || upper.y < 0f || lower.y > 1f)
        {
            displayRenderer.enabled = false;
            captureCamera.enabled = false;
            return;
        }

        // Match screen pixels, including their sampling grid. A tightly cropped
        // texture was visibly softer on the thin trails due to a second resample.
        Vector3 bottomLeft = viewCamera.ViewportToWorldPoint(new Vector3(0f, 0f, center.z));
        Vector3 topRight = viewCamera.ViewportToWorldPoint(new Vector3(1f, 1f, center.z));
        Vector3 screenCenter = viewCamera.ViewportToWorldPoint(new Vector3(.5f, .5f, center.z));
        Vector3 diagonal = topRight - bottomLeft;
        display.SetPositionAndRotation(screenCenter, viewCamera.transform.rotation);
        display.localScale = new Vector3(
            Vector3.Dot(diagonal, viewCamera.transform.right) / Mathf.Abs(transform.lossyScale.x),
            Vector3.Dot(diagonal, viewCamera.transform.up) / Mathf.Abs(transform.lossyScale.y), 1f);
        displayRenderer.enabled = true;

        // Move the CAMERA around the stationary simulation. Old trail vertices
        // and current particles receive the same carousel transform at display.
        Quaternion inverseRotation = Quaternion.Inverse(transform.rotation);
        captureCamera.transform.SetPositionAndRotation(
            stage.transform.position + inverseRotation * (viewCamera.transform.position - transform.position) / scale,
            inverseRotation * viewCamera.transform.rotation);
        captureCamera.nearClipPlane = viewCamera.nearClipPlane / scale;
        captureCamera.farClipPlane = Mathf.Min(viewCamera.farClipPlane / scale, 100f);

        captureCamera.ResetProjectionMatrix();
        captureCamera.aspect = viewCamera.aspect;
        captureCamera.fieldOfView = viewCamera.fieldOfView;
        captureCamera.orthographic = viewCamera.orthographic;
        captureCamera.orthographicSize = viewCamera.orthographicSize / scale;
        // Let Unity schedule the capture before the viewing camera, instead of
        // forcing a nested Camera.Render and synchronizing its rendering work.
        captureCamera.depth = viewCamera.depth - 1f;
        captureCamera.enabled = viewCamera.isActiveAndEnabled;
    }

    private void OnDisable()
    {
        Release();
    }

    private void OnDestroy() => Release();

    private void Release()
    {
        if (stage != null)
        {
            stage.SetActive(false);
            Destroy(stage);
        }
        if (display != null) Destroy(display.gameObject);
        if (displayMaterial != null) Destroy(displayMaterial);
        if (displayMesh != null) Destroy(displayMesh);
        if (texture != null)
        {
            if (captureCamera != null) captureCamera.targetTexture = null;
            texture.Release();
            Destroy(texture);
        }
        if (stageSlot >= 0) OccupiedStages.Remove(stageSlot);
        stageSlot = -1;
        stage = null;
        sourceInstance = null;
        captureCamera = null;
        display = null;
        displayRenderer = null;
        displayMaterial = null;
        displayMesh = null;
        texture = null;
    }
}
