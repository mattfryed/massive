using System;
using UnityEngine;
using System.Collections.Generic;


#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class HiggsStackedTopoIcon : MonoBehaviour
{
    public enum StackAxis
    {
        LocalX, LocalY, LocalZ,
        LocalNegX, LocalNegY, LocalNegZ
    }

    [Header("Source")]
    [SerializeField] private HiggsFieldGPU higgsSource;
    [SerializeField] private Material sourceMaterialWithTextures;

    [Header("Render")]
    [SerializeField] private Material stackedMaterial;
    [SerializeField] private Mesh meshOverride;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool disableLocalRenderer = true;

    [Header("Stack Settings")]
    [Range(4, 64)] [SerializeField] private int layerCount = 24;
    [SerializeField] private StackAxis stackAxis = StackAxis.LocalZ;
    [SerializeField] private float stackHeight = 1.2f;
    [Range(0f, 1f)] [SerializeField] private float stackAlpha = 1.0f;
    [Range(0f, 1f)] [SerializeField] private float topFade = 0.25f;
    [Tooltip("Local offset along the stack axis to recenter the drawn stack relative to the mesh pivot.")]
[SerializeField] private float stackCenterOffset = 0f;

    [Header("Slice Height Range (raw height)")]
    [SerializeField] private float heightMin = -1f;
    [SerializeField] private float heightMax = 1f;

    [Header("Use Base Height")]
    [Tooltip("If HiggsFieldGPU provides a base height texture (no excitations), use it for slicing.")]
    [SerializeField] private bool useBaseHeightIfAvailable = true;

    [Header("Copy Visuals From Real Field Material")]
    [Tooltip("Copies line width/AA/opacity + hue settings from the real underlay material each frame.")]
    [SerializeField] private bool copyVisualsFromSourceMaterial = true;

    [Header("Spin")]
    [SerializeField] private bool autoRotate = true;
    [SerializeField] private Vector3 rotateAxis = new Vector3(1, 1, 1);
    [SerializeField] private float rotateDegreesPerSecond = 18f;

    [Header("Debug")]
    [SerializeField] private bool logMissingTexture = true;

    // Icon shader property IDs
    private static readonly int PID_HeightTex  = Shader.PropertyToID("_HiggsHeight");
    private static readonly int PID_ExciteTex  = Shader.PropertyToID("_HiggsExcite");
    private static readonly int PID_HeightMin  = Shader.PropertyToID("_HeightMin");
    private static readonly int PID_HeightMax  = Shader.PropertyToID("_HeightMax");
    private static readonly int PID_Slice01    = Shader.PropertyToID("_Slice01");
    private static readonly int PID_LayerAlpha = Shader.PropertyToID("_LayerAlpha");

    // Underlay shader (real field) IDs we can copy from
    private static readonly int SID_Opacity = Shader.PropertyToID("_Opacity");
    private static readonly int SID_StrokeWidthPx = Shader.PropertyToID("_StrokeWidthPx");
    private static readonly int SID_AAMult = Shader.PropertyToID("_AAMult");

    private static readonly int SID_AmpColorMin = Shader.PropertyToID("_AmpColorMin");
    private static readonly int SID_AmpColorMax = Shader.PropertyToID("_AmpColorMax");
    private static readonly int SID_AmpColorStrength = Shader.PropertyToID("_AmpColorStrength");
    private static readonly int SID_AmpColorPow = Shader.PropertyToID("_AmpColorPow");

    private static readonly int SID_PierceHeight = Shader.PropertyToID("_PierceHeight");
    private static readonly int SID_PierceFeather = Shader.PropertyToID("_PierceFeather");
    private static readonly int SID_PierceColorMin = Shader.PropertyToID("_PierceColorMin");
    private static readonly int SID_PierceColorMax = Shader.PropertyToID("_PierceColorMax");
    private static readonly int SID_PierceColorStrength = Shader.PropertyToID("_PierceColorStrength");
    private static readonly int SID_PierceColorPow = Shader.PropertyToID("_PierceColorPow");

    // Icon shader equivalents
    private static readonly int IID_Opacity = Shader.PropertyToID("_Opacity");
    private static readonly int IID_StrokeWidthPx = Shader.PropertyToID("_StrokeWidthPx");
    private static readonly int IID_AAMult = Shader.PropertyToID("_AAMult");

    private static readonly int IID_ColorMin = Shader.PropertyToID("_ColorMin");
    private static readonly int IID_ColorMax = Shader.PropertyToID("_ColorMax");
    private static readonly int IID_ColorStrength = Shader.PropertyToID("_ColorStrength");
    private static readonly int IID_ColorPow = Shader.PropertyToID("_ColorPow");

    private static readonly int IID_PierceHeight = Shader.PropertyToID("_PierceHeight");
    private static readonly int IID_PierceFeather = Shader.PropertyToID("_PierceFeather");
    private static readonly int IID_PierceColorMin = Shader.PropertyToID("_PierceColorMin");
    private static readonly int IID_PierceColorMax = Shader.PropertyToID("_PierceColorMax");
    private static readonly int IID_PierceStrength = Shader.PropertyToID("_PierceStrength");
    private static readonly int IID_PiercePow = Shader.PropertyToID("_PiercePow");

    private Mesh _mesh;
    private Material _runtimeMat;
    private bool _warned;

    private int[] _order = new int[64];
    private float[] _depth = new float[64];

    private void OnEnable()
    {
        _mesh = meshOverride != null
            ? meshOverride
            : (GetComponent<MeshFilter>() ? GetComponent<MeshFilter>().sharedMesh : null);

        if (disableLocalRenderer)
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
        }

        CreateRuntimeMaterial();
    }

    private void OnDisable()
    {
        DestroyRuntimeMaterial();
    }

    private void Update()
    {
        if (!autoRotate) return;
        float dt = Application.isPlaying ? Time.deltaTime : (1f / 60f);
        transform.Rotate(rotateAxis, rotateDegreesPerSecond * dt, Space.Self);
    }

    private void OnRenderObject()
    {
        if (_mesh == null || _runtimeMat == null) return;

        var cam = Camera.current;
        if (cam == null) return;
        if (targetCamera != null && cam != targetCamera) return;

        // Resolve textures
        Texture heightTex = null;
        Texture exciteTex = null;

        if (higgsSource != null)
        {
            // Prefer base height if requested + available
            if (useBaseHeightIfAvailable && higgsSource.HeightBaseTexture != null)
                heightTex = higgsSource.HeightBaseTexture;
            else
                heightTex = higgsSource.HeightTexture;

            exciteTex = higgsSource.ExciteTexture;
        }

        if (heightTex == null && sourceMaterialWithTextures != null)
        {
            heightTex = sourceMaterialWithTextures.GetTexture(PID_HeightTex);
            exciteTex = sourceMaterialWithTextures.GetTexture(PID_ExciteTex);
        }

        if (heightTex == null)
        {
            if (logMissingTexture && !_warned)
            {
                Debug.LogWarning($"{nameof(HiggsStackedTopoIcon)} on {name}: No height texture found.");
                _warned = true;
            }
            return;
        }

        _warned = false;

        // Optional: copy “real field” visuals into icon shader
        if (copyVisualsFromSourceMaterial && sourceMaterialWithTextures != null)
            CopyVisuals(sourceMaterialWithTextures, _runtimeMat);

        _runtimeMat.SetTexture(PID_HeightTex, heightTex);
        if (exciteTex != null) _runtimeMat.SetTexture(PID_ExciteTex, exciteTex);
        _runtimeMat.SetFloat(PID_HeightMin, heightMin);
        _runtimeMat.SetFloat(PID_HeightMax, heightMax);

        int n = Mathf.Clamp(layerCount, 1, 64);
        float denom = Mathf.Max(1, n - 1);
        float halfH = stackHeight * 0.5f;

        // Camera-sorted order for consistent transparent blending
        Vector3 axisLocal = GetLocalAxis(stackAxis);

        for (int i = 0; i < n; i++)
        {
            float slice01 = i / denom;
            float y = Mathf.Lerp(-halfH, +halfH, slice01);

            Vector3 layerWorldPos = transform.TransformPoint(axisLocal * y);
            _depth[i] = Vector3.Dot(cam.transform.forward, (layerWorldPos - cam.transform.position));
            _order[i] = i;
        }

        Array.Sort(_order, 0, n, Comparer<int>.Create((a, b) => _depth[b].CompareTo(_depth[a]))); // far -> near

        for (int oi = 0; oi < n; oi++)
        {
            int i = _order[oi];

            float slice01 = i / denom;
            float y = Mathf.Lerp(-halfH, +halfH, slice01);

            float a = stackAlpha;
            if (topFade > 0f)
            {
                float topT = Mathf.InverseLerp(1f - topFade, 1f, slice01);
                a *= Mathf.Lerp(1f, 0.35f, topT);
            }

            _runtimeMat.SetFloat(PID_Slice01, slice01);
            _runtimeMat.SetFloat(PID_LayerAlpha, a);

            if (_runtimeMat.SetPass(0))
            {
                // Recenters the stack around the mesh pivot (or wherever you want).
float yCentered = y - stackCenterOffset;
Matrix4x4 m = transform.localToWorldMatrix * Matrix4x4.Translate(axisLocal * yCentered);
                Graphics.DrawMeshNow(_mesh, m);
            }
        }
    }

    private static Vector3 GetLocalAxis(StackAxis axis)
    {
        switch (axis)
        {
            case StackAxis.LocalX:    return Vector3.right;
            case StackAxis.LocalY:    return Vector3.up;
            case StackAxis.LocalZ:    return Vector3.forward;
            case StackAxis.LocalNegX: return Vector3.left;
            case StackAxis.LocalNegY: return Vector3.down;
            case StackAxis.LocalNegZ: return Vector3.back;
            default:                  return Vector3.forward;
        }
    }

    private static void CopyVisuals(Material src, Material dst)
    {
        // Copy only if properties exist (safe across shader swaps)
        if (src.HasProperty(SID_Opacity) && dst.HasProperty(IID_Opacity))
            dst.SetFloat(IID_Opacity, src.GetFloat(SID_Opacity));

        if (src.HasProperty(SID_StrokeWidthPx) && dst.HasProperty(IID_StrokeWidthPx))
            dst.SetFloat(IID_StrokeWidthPx, src.GetFloat(SID_StrokeWidthPx));

        if (src.HasProperty(SID_AAMult) && dst.HasProperty(IID_AAMult))
            dst.SetFloat(IID_AAMult, src.GetFloat(SID_AAMult));

        if (src.HasProperty(SID_AmpColorMin) && dst.HasProperty(IID_ColorMin))
            dst.SetColor(IID_ColorMin, src.GetColor(SID_AmpColorMin));

        if (src.HasProperty(SID_AmpColorMax) && dst.HasProperty(IID_ColorMax))
            dst.SetColor(IID_ColorMax, src.GetColor(SID_AmpColorMax));

        if (src.HasProperty(SID_AmpColorStrength) && dst.HasProperty(IID_ColorStrength))
            dst.SetFloat(IID_ColorStrength, src.GetFloat(SID_AmpColorStrength));

        if (src.HasProperty(SID_AmpColorPow) && dst.HasProperty(IID_ColorPow))
            dst.SetFloat(IID_ColorPow, src.GetFloat(SID_AmpColorPow));

        if (src.HasProperty(SID_PierceHeight) && dst.HasProperty(IID_PierceHeight))
            dst.SetFloat(IID_PierceHeight, src.GetFloat(SID_PierceHeight));

        if (src.HasProperty(SID_PierceFeather) && dst.HasProperty(IID_PierceFeather))
            dst.SetFloat(IID_PierceFeather, src.GetFloat(SID_PierceFeather));

        if (src.HasProperty(SID_PierceColorMin) && dst.HasProperty(IID_PierceColorMin))
            dst.SetColor(IID_PierceColorMin, src.GetColor(SID_PierceColorMin));

        if (src.HasProperty(SID_PierceColorMax) && dst.HasProperty(IID_PierceColorMax))
            dst.SetColor(IID_PierceColorMax, src.GetColor(SID_PierceColorMax));

        if (src.HasProperty(SID_PierceColorStrength) && dst.HasProperty(IID_PierceStrength))
            dst.SetFloat(IID_PierceStrength, src.GetFloat(SID_PierceColorStrength));

        if (src.HasProperty(SID_PierceColorPow) && dst.HasProperty(IID_PiercePow))
            dst.SetFloat(IID_PiercePow, src.GetFloat(SID_PierceColorPow));
    }

    private void CreateRuntimeMaterial()
    {
        if (stackedMaterial == null) return;

        _runtimeMat = new Material(stackedMaterial)
        {
            name = $"{stackedMaterial.name} (Runtime)",
            hideFlags = HideFlags.DontSave
        };
    }

    private void DestroyRuntimeMaterial()
    {
        if (_runtimeMat == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying) DestroyImmediate(_runtimeMat);
        else Destroy(_runtimeMat);
#else
        Destroy(_runtimeMat);
#endif
        _runtimeMat = null;
    }
}
