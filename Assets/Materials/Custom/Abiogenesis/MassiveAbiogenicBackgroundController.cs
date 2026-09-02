using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class MassiveAbiogenicBackgroundController : MonoBehaviour
{
    static readonly int FieldOriginWSId = Shader.PropertyToID("_FieldOriginWS");
    static readonly int FieldAxisXId = Shader.PropertyToID("_FieldAxisX_WS");
    static readonly int FieldAxisYId = Shader.PropertyToID("_FieldAxisY_WS");
    static readonly int FieldAxisZId = Shader.PropertyToID("_FieldAxisZ_WS");
    static readonly int ArenaHalfExtentsId = Shader.PropertyToID("_ArenaHalfExtents");

    static readonly int BackgroundColorId = Shader.PropertyToID("_BackgroundColor");
    static readonly int FieldColorId = Shader.PropertyToID("_FieldColor");

    static readonly int VirtualCameraDistanceId = Shader.PropertyToID("_VirtualCameraDistance");
    static readonly int ViewScaleId = Shader.PropertyToID("_ViewScale");
    static readonly int VanishingPointId = Shader.PropertyToID("_VanishingPoint");
    static readonly int VirtualCameraOffsetId = Shader.PropertyToID("_VirtualCameraOffset");
    static readonly int CameraDriftAmplitudeId = Shader.PropertyToID("_CameraDriftAmplitude");
    static readonly int CameraDriftSpeedId = Shader.PropertyToID("_CameraDriftSpeed");

    static readonly int BaseLumaId = Shader.PropertyToID("_BaseLuma");
    static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
    static readonly int StripeFrequencyId = Shader.PropertyToID("_StripeFrequency");
    static readonly int StripeContrastId = Shader.PropertyToID("_StripeContrast");
    static readonly int FogDensityId = Shader.PropertyToID("_FogDensity");
    static readonly int SurfaceSoftnessId = Shader.PropertyToID("_SurfaceSoftness");
    static readonly int NormalShadingId = Shader.PropertyToID("_NormalShading");
    static readonly int NearBoostId = Shader.PropertyToID("_NearBoost");

    static readonly int MaxStepsId = Shader.PropertyToID("_MaxSteps");
    static readonly int HitEpsilonId = Shader.PropertyToID("_HitEpsilon");
    static readonly int MaxDistanceId = Shader.PropertyToID("_MaxDistance");
    static readonly int RayStartOffsetId = Shader.PropertyToID("_RayStartOffset");
    static readonly int StepScaleId = Shader.PropertyToID("_StepScale");

    static readonly int CellScaleId = Shader.PropertyToID("_CellScale");
    static readonly int LayerDepthId = Shader.PropertyToID("_LayerDepth");
    static readonly int SphereRadiusId = Shader.PropertyToID("_SphereRadius");
    static readonly int SphereRadiusAmplitudeId = Shader.PropertyToID("_SphereRadiusAmplitude");
    static readonly int SphereRadiusFrequencyId = Shader.PropertyToID("_SphereRadiusFrequency");

    static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
    static readonly int SwirlSpeedId = Shader.PropertyToID("_SwirlSpeed");
    static readonly int BendAmplitudeId = Shader.PropertyToID("_BendAmplitude");
    static readonly int WarpStrengthId = Shader.PropertyToID("_WarpStrength");
    static readonly int PhaseOffsetId = Shader.PropertyToID("_PhaseOffset");
    static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");

    static Mesh s_unitQuad;

    [Header("Scene References")]
    [SerializeField] private ArenaBoundsFromVectorGrid arenaBounds;
    [SerializeField] private MeshRenderer targetRenderer;
    [SerializeField] private MeshFilter targetFilter;
    [SerializeField] private Material backgroundMaterial;
    [SerializeField] private MassiveAbiogenicFieldProfile profile;

    [Header("Placement")]
    [SerializeField] private bool driveTransform = true;
    [SerializeField, Min(0f)] private float planeOffsetWorld = 0.35f;
    [SerializeField, Min(1f)] private float overscan = 1.35f;
    [SerializeField] private bool useInsetBounds = true;

    MaterialPropertyBlock _mpb;

    void Reset()
    {
        arenaBounds = GetComponentInParent<ArenaBoundsFromVectorGrid>();
        if (!arenaBounds)
            arenaBounds = FindFirstObjectByType<ArenaBoundsFromVectorGrid>();

        targetRenderer = GetComponent<MeshRenderer>();
        targetFilter = GetComponent<MeshFilter>();
    }

    void OnEnable()
    {
        EnsureReferences();
        EnsureQuadMesh();
        ApplyAll();
    }

    void LateUpdate() => ApplyAll();

    void OnValidate()
    {
        overscan = Mathf.Max(1f, overscan);
        planeOffsetWorld = Mathf.Max(0f, planeOffsetWorld);

        EnsureReferences();
        EnsureQuadMesh();

        if (!Application.isPlaying)
            ApplyAll();
    }

    void EnsureReferences()
    {
        if (!targetRenderer) targetRenderer = GetComponent<MeshRenderer>();
        if (!targetFilter) targetFilter = GetComponent<MeshFilter>();

        if (!arenaBounds)
        {
            arenaBounds = GetComponentInParent<ArenaBoundsFromVectorGrid>();
            if (!arenaBounds)
                arenaBounds = FindFirstObjectByType<ArenaBoundsFromVectorGrid>();
        }

        if (targetRenderer && backgroundMaterial &&
            targetRenderer.sharedMaterial != backgroundMaterial)
        {
            targetRenderer.sharedMaterial = backgroundMaterial;
        }

        _mpb ??= new MaterialPropertyBlock();
    }

    void EnsureQuadMesh()
    {
        if (!targetFilter)
            return;

        s_unitQuad ??= BuildUnitQuad();

        if (targetFilter.sharedMesh != s_unitQuad)
            targetFilter.sharedMesh = s_unitQuad;
    }

    void ApplyAll()
    {
        if (!isActiveAndEnabled)
            return;

        EnsureReferences();

        if (!arenaBounds || !targetRenderer)
            return;

        var snapshot = arenaBounds.Current;
        Vector2 half = useInsetBounds
            ? snapshot.halfSizeLocalInset
            : snapshot.halfSizeLocal;

        Vector3 planeCenterWS = snapshot.centerWS -
            snapshot.normalWS * planeOffsetWorld;

        if (driveTransform)
        {
            transform.position = planeCenterWS;
            transform.rotation = Quaternion.LookRotation(
                snapshot.normalWS,
                snapshot.axisY_WS);

            transform.localScale = new Vector3(
                Mathf.Max(0.01f, half.x * 2f * overscan),
                Mathf.Max(0.01f, half.y * 2f * overscan),
                1f);
        }

        _mpb.Clear();

        // Retained for compatibility and potential later world-space blending.
        _mpb.SetVector(FieldOriginWSId, planeCenterWS);
        _mpb.SetVector(FieldAxisXId, snapshot.axisX_WS.normalized);
        _mpb.SetVector(FieldAxisYId, snapshot.axisY_WS.normalized);
        _mpb.SetVector(FieldAxisZId, (-snapshot.normalWS).normalized);
        _mpb.SetVector(ArenaHalfExtentsId,
            new Vector4(half.x, half.y, 0f, 0f));

        if (profile)
            ApplyProfile(profile);

        targetRenderer.SetPropertyBlock(_mpb);
    }

    void ApplyProfile(MassiveAbiogenicFieldProfile p)
    {
        _mpb.SetColor(BackgroundColorId, p.backgroundColor);
        _mpb.SetColor(FieldColorId, p.fieldColor);

        _mpb.SetFloat(VirtualCameraDistanceId, p.virtualCameraDistance);
        _mpb.SetFloat(ViewScaleId, p.viewScale);
        _mpb.SetVector(VanishingPointId,
            new Vector4(p.vanishingPoint.x, p.vanishingPoint.y, 0f, 0f));
        _mpb.SetVector(VirtualCameraOffsetId,
            new Vector4(p.virtualCameraOffset.x, p.virtualCameraOffset.y, 0f, 0f));
        _mpb.SetFloat(CameraDriftAmplitudeId, p.cameraDriftAmplitude);
        _mpb.SetFloat(CameraDriftSpeedId, p.cameraDriftSpeed);

        _mpb.SetFloat(BaseLumaId, p.baseLuma);
        _mpb.SetFloat(GlowStrengthId, p.glowStrength);
        _mpb.SetFloat(StripeFrequencyId, p.stripeFrequency);
        _mpb.SetFloat(StripeContrastId, p.stripeContrast);
        _mpb.SetFloat(FogDensityId, p.fogDensity);
        _mpb.SetFloat(SurfaceSoftnessId, p.surfaceSoftness);
        _mpb.SetFloat(NormalShadingId, p.normalShading);
        _mpb.SetFloat(NearBoostId, p.nearBoost);

        _mpb.SetFloat(MaxStepsId, p.maxSteps);
        _mpb.SetFloat(HitEpsilonId, p.hitEpsilon);
        _mpb.SetFloat(MaxDistanceId, p.maxDistance);
        _mpb.SetFloat(RayStartOffsetId, p.rayStartOffset);
        _mpb.SetFloat(StepScaleId, p.stepScale);

        _mpb.SetFloat(CellScaleId, p.cellScale);
        _mpb.SetFloat(LayerDepthId, p.layerDepth);
        _mpb.SetFloat(SphereRadiusId, p.sphereRadius);
        _mpb.SetFloat(SphereRadiusAmplitudeId, p.sphereRadiusAmplitude);
        _mpb.SetFloat(SphereRadiusFrequencyId, p.sphereRadiusFrequency);

        _mpb.SetFloat(ScrollSpeedId, p.scrollSpeed);
        _mpb.SetFloat(SwirlSpeedId, p.swirlSpeed);
        _mpb.SetFloat(BendAmplitudeId, p.bendAmplitude);
        _mpb.SetFloat(WarpStrengthId, p.warpStrength);
        _mpb.SetFloat(PhaseOffsetId, p.phaseOffset);
        _mpb.SetFloat(EdgeFadeId, p.edgeFade);
    }

    static Mesh BuildUnitQuad()
    {
        var mesh = new Mesh { name = "MASSIVE Abiogenic Quad" };

        mesh.SetVertices(new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
        });

        mesh.SetUVs(0, new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        });

        mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0, true);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }
}
