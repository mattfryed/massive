using UnityEngine;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MetaballSDFInstance))]
    public partial class ParticleAcceleratorBeamVisual : MonoBehaviour
    {

        [Header("Volume")]
        [SerializeField] private bool useFixedVolumeSize = true;
        [SerializeField] private float fixedVolumeSizeWorld = 26f; // must be >= max visual length + padding

        [Header("Shape")]
        [SerializeField] private float paddingWorld = 0.25f;

        [Tooltip("Head radius multiplier (near impact). Keep close to 1 for uniform width.")]
        [SerializeField] private float headRadiusMul = 1.10f;

        [Tooltip("Tail radius multiplier (near muzzle). < 1 makes it a bit thinner at the start.")]
        [SerializeField] private float tailRadiusMul = 0.75f;

        [Tooltip("How far from the muzzle (tail) we ramp from tailRadiusMul to full body radius.")]
        [SerializeField] private float muzzleRampWorld = 0.45f;

        [Tooltip("Desired metaball spacing in world units (will be clamped up based on radius).")]
        [SerializeField] private float segmentSpacingWorld = 0.22f;

        [Tooltip("Minimum spacing expressed as radius * factor (prevents over-dense chains that inflate).")]
        [SerializeField] private float minSpacingRadiusFactor = 1.65f;

        [Header("Density")]
[SerializeField] private bool useFixedInteriorCount = false;
[SerializeField, Range(0, 30)] private int fixedInteriorCount = 18;

[SerializeField] private float segmentsPerWorldUnit = 4.0f; // if not fixed


        [Header("Flicker")]
        [SerializeField] private float widthJitterAmp = 0.06f;
        [SerializeField] private float widthJitterFreq = 22f;

        [Header("Beam thickness ranges (visual multipliers)")]
        [Tooltip("Multiplies the whole plasma bundle. 1–1 preserves the current size; equal limits give a fixed thickness.")]
        public ThicknessRange overallThickness = new ThicknessRange(1f, 1f);
        [Tooltip("Additional multiplier at the core/muzzle. Blends into the body over Muzzle Ramp World.")]
        public ThicknessRange startThickness = new ThicknessRange(1f, 1f);
        [Tooltip("Additional multiplier along the beam body, varying smoothly within these limits.")]
        public ThicknessRange bodyThickness = new ThicknessRange(1f, 1f);
        [Tooltip("Additional multiplier at the contact tip. Blends from the body over Muzzle Ramp World.")]
        public ThicknessRange endThickness = new ThicknessRange(1f, 1f);
        [Tooltip("Speed of thickness-range variation. Independent of strand motion and the thickness ripple.")]
        [Min(0f)] public float thicknessVariationSpeed = 4f;

        [Header("Opaque plasma (turret)")]
        [Tooltip("Separate volumetric black/white plasma strands. Disabled on the original power-up.")]
        public bool plasmaLayers;
        [Range(0f, 1f)] public float whiteFraction = .75f;
        [Tooltip("Average bend frequency; each strand uses different, reversing rates.")]
        [InspectorName("Strand Bend Frequency"), Min(.1f)] public float swirlTurnsPerUnit = .8f;
        [Tooltip("Motion of the strand pattern toward the tip. Individual strands travel at different rates.")]
        [InspectorName("Strand Flow Speed"), Min(0f)] public float swirlSpeed = 7f;
        [Range(0f, 1f)] public float thicknessWaveAmplitude = .12f;
        [Min(.2f)] public float waveLength = 1.6f;
        [Tooltip("Sideways motion as a fraction of beam radius; endpoints stay anchored.")]
        [Range(0f, 2f)] public float curveAmplitude = .4f;
        [Header("Plasma stranding")]
        [Range(3, 8)] public int strandCount = 6;
        [Tooltip("How far strands wander around the beam, with independent changes of direction.")]
        [Range(0f, 2f)] public float strandWander = 1.15f;
        [Tooltip("Zero keeps strand widths even; one lets individual strands taper completely in and out.")]
        [Range(0f, 1f)] public float strandTaper = 1f;
        [Tooltip("Spacing of the individual 3D strands around the beam center; larger values open more gaps.")]
        [Range(0f, 2f)] public float strandSeparation = 1f;

        private MetaballSDFInstance _sdf;

        private Vector3 _tailWS;
        private Vector3 _headWS;
        private float _radiusWorld;
        private float _charge01;

        private float _volumeSizeWorld = 1f;
        private float _spatialScale = 1f;
        private float _animationTime = -1f;
        private MaterialPropertyBlock _properties;
        private Mesh _volumeMesh;
        private MeshFilter _filter;
        private Mesh _sourceMesh;
        private readonly Vector3[] _volumeVertices = new Vector3[8];
        public float SegmentLength => Vector3.Distance(_headWS, _tailWS);
        public float VolumeSizeWorld => _volumeSizeWorld;
        // Enemy beams supply their active gameplay clock so match pause also freezes flicker.
        public void SetAnimationTime(float seconds) { _animationTime = Mathf.Max(0f, seconds); }

        public void SetSpatialScale(float scale)
        {
            _spatialScale = Mathf.Max(0.01f, scale);
        }

        private void Awake()
        {
            _sdf = GetComponent<MetaballSDFInstance>();
            _properties = new MaterialPropertyBlock();
            _filter = GetComponent<MeshFilter>();
            if (_filter)
            {
                _sourceMesh = _filter.sharedMesh;
                _volumeMesh = new Mesh { name = "Fitted particle beam volume", hideFlags = HideFlags.DontSave };
                _volumeMesh.MarkDynamic(); _volumeMesh.vertices = _volumeVertices;
                _volumeMesh.triangles = new[] { 0,2,1,1,2,3, 4,5,6,5,7,6, 0,1,4,1,5,4, 2,6,3,3,6,7, 0,4,2,2,4,6, 1,3,5,3,7,5 };
                _filter.sharedMesh = _volumeMesh;
            }
        }
        private void OnDestroy()
        {
            if (_filter && _filter.sharedMesh == _volumeMesh) _filter.sharedMesh = _sourceMesh;
            if (_volumeMesh) Destroy(_volumeMesh);
        }

        public void SetSegment(Vector3 tailWS, Vector3 headWS, float radiusWorld, float charge01)
        {
            _tailWS = tailWS;
            _headWS = headWS;
            _radiusWorld = Mathf.Max(0.001f, radiusWorld);
            _charge01 = Mathf.Clamp01(charge01);
        }

        private static float Ease(float x) => x * x * (3f - 2f * x); // smoothstep

        private void LateUpdate()
        {
            if (_sdf == null) return;

            Vector3 seg = _headWS - _tailWS;
            seg.y = 0f;

            float len = seg.magnitude;
            float clock = _animationTime >= 0f ? _animationTime : Time.time;
            float thickness = plasmaLayers ? overallThickness.Sample(clock * Mathf.Max(0f, thicknessVariationSpeed)) : 1f;
            if (len <= 0.02f * _spatialScale || thickness <= .00001f)
            {
                _sdf.Clear();
                _sdf.Apply();
                if (_sdf.TargetRenderer)
                {
                    _sdf.TargetRenderer.GetPropertyBlock(_properties);
                    _properties.SetVector("_PlasmaTubeSampling", Vector4.zero);
                    _sdf.TargetRenderer.SetPropertyBlock(_properties);
                }
                return;
            }

            Vector3 dir = seg / len;

            // Flicker (small)
            float n = Mathf.PerlinNoise(13.37f, clock * widthJitterFreq) * 2f - 1f;
            float jitterMul = 1f + n * widthJitterAmp * Mathf.Lerp(0.35f, 1f, _charge01);

            float bodyR = _radiusWorld * Mathf.Max(0.75f, jitterMul) * thickness;
            float headR = bodyR * headRadiusMul;
            float tailR = bodyR * tailRadiusMul;

            // Volume size (world)
            if (useFixedVolumeSize)
            {
                _volumeSizeWorld = Mathf.Max(0.35f, fixedVolumeSizeWorld) * _spatialScale;
                _volumeSizeWorld = Mathf.Max(_volumeSizeWorld, len + 2f * (Mathf.Max(headR, tailR) + paddingWorld * _spatialScale));
            }
            else
            {
                float extent = (len * 0.5f) + Mathf.Max(headR, tailR) + paddingWorld * _spatialScale;
                _volumeSizeWorld = Mathf.Max(0.35f * _spatialScale, extent * 2f);
            }

            // Geometry is specified in world units, independently of prefab parent scale.
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(
                _volumeSizeWorld / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                _volumeSizeWorld / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                _volumeSizeWorld / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));


            // Place + align the volume
            Vector3 mid = (_tailWS + _headWS) * 0.5f;
            transform.position = mid;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            float invS = 1f / Mathf.Max(0.0001f, _volumeSizeWorld);
            float waveNumber = 2f * Mathf.PI / Mathf.Max(.2f, waveLength * _spatialScale);

            _sdf.Clear();

            float half = len * 0.5f;

            if (!plasmaLayers)
            {
                // Head + tail
                _sdf.AddBall(new Vector3(0f, 0f, +half) * invS, headR * invS);
                _sdf.AddBall(new Vector3(0f, 0f, -half) * invS, tailR * invS);

                // Effective spacing (prevents dense chains getting fatter in the middle)
                float spacing = Mathf.Max(segmentSpacingWorld * _spatialScale, bodyR * minSpacingRadiusFactor);

                // The shared SDF supports 48 balls. The analytic connecting segment also
                // keeps long, thin beams continuous when that finite budget is exhausted.
                const int maxInterior = MetaballSDFInstance.MaxBalls - 2;

                int interior;
                if (useFixedInteriorCount)
                {
                    interior = Mathf.Clamp(fixedInteriorCount, 0, maxInterior);
                }
                else
                {
                    // “density”: interior grows with length
                    interior = Mathf.CeilToInt(len * Mathf.Max(0.1f, segmentsPerWorldUnit) / _spatialScale) - 1;
                    interior = Mathf.Clamp(interior, 0, maxInterior);
                }
                interior = Mathf.Min(interior, Mathf.Max(0, Mathf.CeilToInt(len / Mathf.Max(.001f, spacing)) - 1));
                if (interior > 0)
                {
                    float step = len / (interior + 1f);

                    float ramp = Mathf.Max(0.05f, muzzleRampWorld) * _spatialScale;

                    for (int i = 0; i < interior; i++)
                    {
                        float z = -half + step * (i + 1f);

                        // Distance from tail (muzzle) in local beam space
                        float distFromTail = z + half;

                        // Ramp from tailR to bodyR quickly, then stay stable
                        float ramp01 = Mathf.Clamp01(distFromTail / ramp);
                        float rr = Mathf.Lerp(tailR, bodyR, Ease(ramp01));

                        Vector3 center = new Vector3(0f, 0f, z);
                        _sdf.AddBall(center * invS, rr * invS);
                    }
                }
            }

            _sdf.Apply();
            var renderer = _sdf.TargetRenderer;
            if (renderer)
            {
                renderer.GetPropertyBlock(_properties);
                _properties.SetFloat("_BeamContinuous", 1f);
                _properties.SetVector("_BeamShape", new Vector4(half * invS, bodyR * invS, tailR * invS, headR * invS));
                _properties.SetFloat("_BeamRamp", Mathf.Max(.05f, muzzleRampWorld) * _spatialScale * invS);
                _properties.SetFloat("_PlasmaMode", plasmaLayers ? 1f : 0f);
                float radial = Mathf.Max(tailR, Mathf.Max(headR, bodyR)) + paddingWorld * _spatialScale;
                Vector3 extent = new Vector3(radial, radial, half + radial) * invS;
                if (plasmaLayers) extent = BuildPlasmaStrands(len, bodyR, tailR, headR, invS, clock, waveNumber);
                _properties.SetVector("_VolumeHalfExtents", extent);
                // Smoothness belongs to the beam's physical width, not the size of its bounding cube.
                _properties.SetFloat("_SmoothK", bodyR * .25f * invS);
                renderer.SetPropertyBlock(_properties);
                if (_volumeMesh)
                {
                    for (int i = 0; i < 8; i++) _volumeVertices[i] = new Vector3((i & 1) == 0 ? -extent.x : extent.x,
                        (i & 2) == 0 ? -extent.y : extent.y, (i & 4) == 0 ? -extent.z : extent.z);
                    _volumeMesh.vertices = _volumeVertices; _volumeMesh.RecalculateBounds();
                }
            }
        }
    }
}
