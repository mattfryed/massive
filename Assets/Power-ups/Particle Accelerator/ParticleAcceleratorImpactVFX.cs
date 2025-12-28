using UnityEngine;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MetaballSDFInstance))]
    public class ParticleAcceleratorImpactVFX : MonoBehaviour
    {
        [Header("Tuning")]
        public float duration = 0.20f;

        public int shardsMin = 6;
        public int shardsMax = 14;

        public float spreadMin = 0.18f;
        public float spreadMax = 0.55f;

        public float shardRadiusMin = 0.04f;
        public float shardRadiusMax = 0.11f;

        public float coreRadiusMin = 0.10f;
        public float coreRadiusMax = 0.26f;

        private MetaballSDFInstance _sdf;
        private float _t;
        private float _charge01;

        private Vector2[] _dirs;
        private float[] _distMul;
        private float[] _sizeMul;

        private float _volumeSizeWorld;

        public void Init(float charge01, int seed)
        {
            _charge01 = Mathf.Clamp01(charge01);

            int n = Mathf.RoundToInt(Mathf.Lerp(shardsMin, shardsMax, _charge01));
            n = Mathf.Clamp(n, 3, 24);

            _dirs = new Vector2[n];
            _distMul = new float[n];
            _sizeMul = new float[n];

            var rng = new System.Random(seed);
            for (int i = 0; i < n; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                _dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                _distMul[i] = Mathf.Lerp(0.65f, 1.25f, (float)rng.NextDouble());
                _sizeMul[i] = Mathf.Lerp(0.70f, 1.35f, (float)rng.NextDouble());
            }

            float maxSpread = Mathf.Lerp(spreadMin, spreadMax, _charge01) * 1.25f;
            float maxShardR = Mathf.Lerp(shardRadiusMin, shardRadiusMax, _charge01) * 1.35f;
            float maxCoreR  = Mathf.Lerp(coreRadiusMin, coreRadiusMax, _charge01);

            float extent = maxCoreR + maxSpread + maxShardR + 0.25f;
            _volumeSizeWorld = Mathf.Max(0.35f, extent * 2f);
            transform.localScale = Vector3.one * _volumeSizeWorld;
        }

        private void Awake()
        {
            _sdf = GetComponent<MetaballSDFInstance>();
        }

        private static float Ease(float x) => x * x * (3f - 2f * x);

        private void LateUpdate()
        {
            _t += Time.deltaTime;
            float u = Mathf.Clamp01(_t / Mathf.Max(0.001f, duration));

            // Nice “pop”: 0→1→0
            float pulse = Mathf.Sin(Mathf.PI * u);
            float ease = Ease(u);

            float invS = 1f / Mathf.Max(0.0001f, _volumeSizeWorld);

            float coreR = Mathf.Lerp(coreRadiusMin, coreRadiusMax, _charge01) * pulse;
            float spread = Mathf.Lerp(spreadMin, spreadMax, _charge01) * ease;
            float shardBase = Mathf.Lerp(shardRadiusMin, shardRadiusMax, _charge01) * pulse;

            _sdf.Clear();

            if (coreR > 0.0005f)
                _sdf.AddBall(Vector3.zero, coreR * invS);

            for (int i = 0; i < _dirs.Length; i++)
            {
                Vector3 off = new Vector3(_dirs[i].x, 0f, _dirs[i].y) * (spread * _distMul[i]);
                float rr = shardBase * _sizeMul[i];

                if (rr > 0.0005f)
                    _sdf.AddBall(off * invS, rr * invS);
            }

            _sdf.Apply();

            if (_t >= duration)
                Destroy(gameObject);
        }
    }
}
