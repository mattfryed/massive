using UnityEngine;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MetaballSDFInstance))]
    public class ParticleAcceleratorBeamVisual : MonoBehaviour
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

        private MetaballSDFInstance _sdf;

        private Vector3 _tailWS;
        private Vector3 _headWS;
        private float _radiusWorld;
        private float _charge01;

        private float _volumeSizeWorld = 1f;

        private void Awake()
        {
            _sdf = GetComponent<MetaballSDFInstance>();
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
            if (len <= 0.02f)
            {
                _sdf.Clear();
                _sdf.Apply();
                return;
            }

            Vector3 dir = seg / len;

            // Flicker (small)
            float n = Mathf.PerlinNoise(13.37f, Time.time * widthJitterFreq) * 2f - 1f;
            float jitterMul = 1f + n * widthJitterAmp * Mathf.Lerp(0.35f, 1f, _charge01);

            float bodyR = _radiusWorld * Mathf.Max(0.75f, jitterMul);
            float headR = bodyR * headRadiusMul;
            float tailR = bodyR * tailRadiusMul;

            // Volume size (world)
            if (useFixedVolumeSize)
            {
                _volumeSizeWorld = Mathf.Max(0.35f, fixedVolumeSizeWorld);
            }
            else
            {
                float extent = (len * 0.5f) + Mathf.Max(headR, tailR) + paddingWorld;
                _volumeSizeWorld = Mathf.Max(0.35f, extent * 2f);
            }

            // Apply scale ONLY if it actually changed a lot (or once at start)
            if (Mathf.Abs(transform.localScale.x - _volumeSizeWorld) > 0.001f)
                transform.localScale = Vector3.one * _volumeSizeWorld;


            // Place + align the volume
            Vector3 mid = (_tailWS + _headWS) * 0.5f;
            transform.position = mid;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            float invS = 1f / Mathf.Max(0.0001f, _volumeSizeWorld);

            _sdf.Clear();

            float half = len * 0.5f;

            // Head + tail
            _sdf.AddBall(new Vector3(0f, 0f, +half) * invS, headR * invS);
            _sdf.AddBall(new Vector3(0f, 0f, -half) * invS, tailR * invS);

            // Effective spacing (prevents dense chains getting fatter in the middle)
            float spacing = Mathf.Max(segmentSpacingWorld, bodyR * minSpacingRadiusFactor);

            // Compute interior count (keep under 32 balls total)
            const int maxInterior = 30;

            int interior;
            if (useFixedInteriorCount)
            {
                interior = Mathf.Clamp(fixedInteriorCount, 0, maxInterior);
            }
            else
            {
                // “density”: interior grows with length
                interior = Mathf.CeilToInt(len * Mathf.Max(0.1f, segmentsPerWorldUnit)) - 1;
                interior = Mathf.Clamp(interior, 0, maxInterior);
            }
            if (interior > 0)
            {
                float step = len / (interior + 1f);

                float ramp = Mathf.Max(0.05f, muzzleRampWorld);

                for (int i = 0; i < interior; i++)
                {
                    float z = -half + step * (i + 1f);

                    // Distance from tail (muzzle) in local beam space
                    float distFromTail = z + half;

                    // Ramp from tailR to bodyR quickly, then stay stable
                    float ramp01 = Mathf.Clamp01(distFromTail / ramp);
                    float rr = Mathf.Lerp(tailR, bodyR, Ease(ramp01));

                    _sdf.AddBall(new Vector3(0f, 0f, z) * invS, rr * invS);
                }
            }

            _sdf.Apply();
        }
    }
}
