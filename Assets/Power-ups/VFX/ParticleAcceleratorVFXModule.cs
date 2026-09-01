using UnityEngine;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    public class ParticleAcceleratorVFXModule : MonoBehaviour
    {
        [Header("Bind")]
        [SerializeField] private PlayerControllerScript owner;

        [Header("Metaball Material (MASSIVE/MetaballSDF)")]
        [SerializeField] private Material metaballMaterial; // e.g. MASSIVE_Metaball_White

        // IMPORTANT: all ring/orb sizes below are WORLD UNITS.
        [Header("Ring (WORLD units)")]
        [SerializeField, Range(8, 32)] private int ringDots = 16;
        [SerializeField] private float ringRadiusWorld = 1.05f;          // distance from player center
        [SerializeField] private float ringDotBaseRadiusWorld = 0.08f;
        [SerializeField] private float ringDotMaxRadiusWorld = 0.14f;
        [SerializeField] private float ringHeightWorld = 0.02f;
        [SerializeField] private float ringStartAngleDeg = 90f;
        [SerializeField] private bool ringClockwise = true;
        [SerializeField] private float ringVolumePaddingWorld = 0.25f;    // extra cube padding around ring
        [SerializeField] private float vfxPlaneYOffsetWorld = 0.20f; // try 0.15–0.35


        [Header("Energy Orb (WORLD units)")]
        [SerializeField] private float orbForwardOffsetWorld = 1.15f;
        [SerializeField] private float orbMinRadiusWorld = 0.07f;
        [SerializeField] private float orbMaxRadiusWorld = 0.22f;
        [SerializeField] private float orbVolumePaddingWorld = 0.25f;

        [Header("Orb Jitter")]
[SerializeField] private float orbScaleJitterAmp = 0.08f;     // 0.05–0.12
[SerializeField] private float orbScaleJitterFreq = 17f;      // 12–24
[SerializeField] private float orbScaleJitterChargeWeight = 1f; // scale jitter by charge01

[Header("Burst Shards (charge-scaled)")]
[SerializeField, Range(3, 24)] private int burstShardsMin = 5;
[SerializeField, Range(3, 24)] private int burstShardsMax = 12;

[SerializeField] private float burstSpreadMinWorld = 0.18f;
[SerializeField] private float burstSpreadMaxWorld = 0.45f;

[SerializeField] private float burstShardRadiusMinWorld = 0.04f;
[SerializeField] private float burstShardRadiusMaxWorld = 0.10f;

[SerializeField] private float burstShardSpinDeg = 25f;



        [Header("Telegraph Burst")]
        [SerializeField] private float burstDuration = 0.14f;
        [SerializeField] private float burstRadiusMult = 1.8f;

        [Header("Aim Ray Dots")]
        [SerializeField] private int rayMinDots = 6;
        [SerializeField] private int rayMaxDots = 24;

[SerializeField] private float rayStartRadiusWorld = 0.95f;
[SerializeField] private float rayDotRadiusWorld = 0.10f;
[SerializeField] private float rayDotSpacingWorld = 0.35f;
[SerializeField] private float rayDotYLiftWorld = 0.03f;

[SerializeField] private float rayEaseSpeed = 14f;
[SerializeField] private bool rayFadeAlphaWithScale = true;


        // Children
        private MetaballSDFInstance _ringSdf;
        private MetaballSDFInstance _orbSdf;
        private AimRayDotsWorld _rayDots;

        // Runtime state
        private bool _visible;
        private bool _charging;
        private float _charge01;
        private float _cooldown01; // 0..1 (0=ready)
        private int _spentDots;
        private Vector3 _aimDirWS = Vector3.right;
        private float _blockedDistance = 0f;

        private Vector2[] _shardDirs;
private float[] _shardDistMul;
private float[] _shardSizeMul;
private float[] _shardSpinMul;
private int _burstSerial = 0;


        private float _burstT = 0f;
        private float _burstCharge01 = 0f;

        // Cached volume sizes (WORLD) for conversion
        private float _ringVolumeSizeWorld = 1f;
        private float _orbVolumeSizeWorld = 1f;

        public int RingDots => ringDots;

        public void Bind(PlayerControllerScript p)
        {
            owner = p;
            // Keep module at player origin (since hub parents under player)
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        private void Awake()
        {
            EnsureChildren();
            RecomputeVolumeSizes();
            SetVisible(false);
        }

        private void OnValidate()
        {
            ringDots = Mathf.Clamp(ringDots, 8, 32);
            RecomputeVolumeSizes();
            ApplyVolumeScales();
        }

        private void RecomputeVolumeSizes()
        {
            // We scale the unit cube so that our ring fits inside it in OBJECT space.
            // If cube world size is S, then object-space coordinate = worldOffset / S.
            // We need ringRadiusWorld + dotRadiusWorld to be comfortably < S * 0.5.
            float ringExtentWorld = ringRadiusWorld + ringDotMaxRadiusWorld + ringVolumePaddingWorld;
            _ringVolumeSizeWorld = Mathf.Max(0.5f, ringExtentWorld * 2f); // cube size in world

            float orbExtentWorld = (orbMaxRadiusWorld * burstRadiusMult) + orbVolumePaddingWorld;
            _orbVolumeSizeWorld = Mathf.Max(0.35f, orbExtentWorld * 2f);
        }

        private static Mesh GetCubeMesh()
        {
            // Robust cube mesh fetch without relying on "Cube.fbx" path.
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(temp);
            return mesh;
        }

        private void EnsureChildren()
        {
            if (!_ringSdf)
            {
                var ringGO = new GameObject("PA_ChargeRing");
                ringGO.transform.SetParent(transform, false);
                ringGO.transform.localPosition = Vector3.zero;
                ringGO.transform.localRotation = Quaternion.identity;

                var mf = ringGO.AddComponent<MeshFilter>();
                mf.sharedMesh = GetCubeMesh();

                var mr = ringGO.AddComponent<MeshRenderer>();
                mr.sharedMaterial = metaballMaterial;

                _ringSdf = ringGO.AddComponent<MetaballSDFInstance>();
            }

            if (!_orbSdf)
            {
                var orbGO = new GameObject("PA_EnergyOrb");
                orbGO.transform.SetParent(transform, false);
                orbGO.transform.localPosition = Vector3.zero;
                orbGO.transform.localRotation = Quaternion.identity;

                var mf = orbGO.AddComponent<MeshFilter>();
                mf.sharedMesh = GetCubeMesh();

                var mr = orbGO.AddComponent<MeshRenderer>();
                mr.sharedMaterial = metaballMaterial;

                _orbSdf = orbGO.AddComponent<MetaballSDFInstance>();
            }

            if (!_rayDots)
            {
                var rayGO = new GameObject("PA_AimRayDots");
                rayGO.transform.SetParent(transform, false);
                rayGO.transform.localPosition = Vector3.zero;
                rayGO.transform.localRotation = Quaternion.identity;

                _rayDots = rayGO.AddComponent<AimRayDotsWorld>();
            }

            ApplyVolumeScales();
        }

        private void ApplyVolumeScales()
        {
            if (_ringSdf) _ringSdf.transform.localScale = Vector3.one * _ringVolumeSizeWorld;
            if (_orbSdf)  _orbSdf.transform.localScale  = Vector3.one * _orbVolumeSizeWorld;
        }

        public void SetVisible(bool on)
        {
            _visible = on;
            if (_ringSdf) _ringSdf.gameObject.SetActive(on);
            if (_orbSdf)  _orbSdf.gameObject.SetActive(on);
            if (_rayDots)
            {
                _rayDots.gameObject.SetActive(on);
                if (!on) _rayDots.Hide();
            }
        }

        public void SetState(
            bool charging,
            float charge01,
            float cooldown01,
            int spentDots,
            Vector3 aimDirWS,
            float blockedDistance)
        {
            _charging = charging;
            _charge01 = Mathf.Clamp01(charge01);
            _cooldown01 = Mathf.Clamp01(cooldown01);
            _spentDots = Mathf.Clamp(spentDots, 0, ringDots);

            _aimDirWS = aimDirWS;
            _aimDirWS.y = 0f;
            if (_aimDirWS.sqrMagnitude < 0.0001f) _aimDirWS = Vector3.right;
            _aimDirWS.Normalize();

            _blockedDistance = Mathf.Max(0f, blockedDistance);

            // In case you tweak ring radius at runtime
            RecomputeVolumeSizes();
            ApplyVolumeScales();
        }

public void TriggerTelegraphBurst(float charge01)
{
    _burstT = burstDuration;
    _burstCharge01 = Mathf.Clamp01(charge01);

    // Build a deterministic-ish random pattern per burst (stable during the burst)
    _burstSerial++;

int n = Mathf.RoundToInt(Mathf.Lerp(burstShardsMin, burstShardsMax, _burstCharge01));
n = Mathf.Clamp(n, 3, 24);

_shardDirs    = new Vector2[n];
_shardDistMul = new float[n];
_shardSizeMul = new float[n];
_shardSpinMul = new float[n];


    // Seed from player + burst serial so it's repeatable per play session but varies per burst
    int seed = (_burstSerial * 73856093) ^ (owner != null ? owner.playerID * 19349663 : 83492791);
    var rng = new System.Random(seed);

    for (int i = 0; i < n; i++)
    {
        float a = (float)rng.NextDouble() * Mathf.PI * 2f;
        _shardDirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));

        _shardDistMul[i] = Mathf.Lerp(0.65f, 1.25f, (float)rng.NextDouble());
        _shardSizeMul[i] = Mathf.Lerp(0.70f, 1.35f, (float)rng.NextDouble());
        _shardSpinMul[i] = (rng.NextDouble() < 0.5) ? -1f : 1f;
    }
}


        public int ComputeSpentDots(float charge01)
        {
            float p = Mathf.Clamp01(charge01) * ringDots;
            int full = Mathf.FloorToInt(p);
            float frac = p - full;

            int spent = full + (frac > 0.01f ? 1 : 0);
            return Mathf.Clamp(spent, 1, ringDots);
        }

        private static float Ease(float x) => x * x * (3f - 2f * x); // smoothstep

        private void LateUpdate()
        {
            if (!_visible || !owner) return;

            Vector3 center = GetOwnerCenterWS();
            center.y += vfxPlaneYOffsetWorld;

_rayDots.SetTuning(
    startRadiusWorld: rayStartRadiusWorld,
    dotRadiusWorld: rayDotRadiusWorld,
    spacingWorld: rayDotSpacingWorld,
    yLiftWorld: rayDotYLiftWorld,
    easeSpeed: rayEaseSpeed,
    fadeAlphaWithScale: rayFadeAlphaWithScale
);


            // Aim ray dots (clamped by blocked distance from ability)
            if (_charging)
            {
                int dots = Mathf.RoundToInt(Mathf.Lerp(rayMinDots, rayMaxDots, _charge01));
                _rayDots.SetRay(center, _aimDirWS, _blockedDistance, dots);
            }
            else
            {
                _rayDots.Hide();
            }

            BuildRing(center);
            BuildOrb(center);

            Debug.DrawLine(center, center + Vector3.right * 0.5f, Color.red);
Debug.DrawLine(center, center + Vector3.forward * 0.5f, Color.green);

        }

        private Vector3 GetOwnerCenterWS()
{
    if (!owner) return transform.position;

    var vc = owner.visualsController;
    if (vc != null && vc.visuals != null)
        return vc.visuals.position;

    // fallback: try find a "Visuals" child
    var t = owner.transform.Find("Visuals");
    if (t) return t.position;

    return owner.transform.position;
}

public Vector3 GetVfxOriginWS()
{
    if (!owner) return transform.position;

    var vc = owner.visualsController;
    Vector3 c = (vc != null && vc.visuals != null) ? vc.visuals.position : owner.transform.position;

    // match what the module uses
    c.y += vfxPlaneYOffsetWorld;
    return c;
}


        private void BuildRing(Vector3 centerWS)
        {
            if (!_ringSdf) return;

            _ringSdf.Clear();

            // Convert WORLD → OBJECT space using cube world size
            float invS = 1f / Mathf.Max(0.0001f, _ringVolumeSizeWorld);

            float step = (Mathf.PI * 2f) / ringDots;
            float start = ringStartAngleDeg * Mathf.Deg2Rad;

            if (_charging)
            {
                float p = _charge01 * ringDots;
                int full = Mathf.FloorToInt(p);
                float frac = p - full;

                for (int i = 0; i < ringDots; i++)
                {
                    float g01 = (i < full) ? 1f : (i == full ? frac : 0f);
                    g01 = Ease(Mathf.Clamp01(g01));

                    float rW = Mathf.Lerp(ringDotBaseRadiusWorld, ringDotMaxRadiusWorld, g01);
                    float rOS = rW * invS;

                    float a = start + step * (ringClockwise ? -i : i);

                    // WORLD offset
                    float xW = Mathf.Cos(a) * ringRadiusWorld;
                    float zW = Mathf.Sin(a) * ringRadiusWorld;
                    float yW = ringHeightWorld;

                    // OBJECT position inside cube
                    Vector3 pOS = new Vector3(xW, yW, zW) * invS;
                    _ringSdf.AddBall(pOS, rOS);
                }
            }
            else
            {
                // cooldown refill
                if (_cooldown01 > 0.0001f && _spentDots > 0)
                {
                    float refill01 = 1f - _cooldown01; // 0..1
                    float rp = refill01 * _spentDots;  // 0..spentDots

                    for (int i = 0; i < ringDots; i++)
                    {
                        float rW = ringDotBaseRadiusWorld;

                        if (i < _spentDots)
                        {
                            // Reverse fill order:
                            // when rp is small, the LAST spent dot refills first.
                            int revIndex = (_spentDots - 1) - i;      // maps i=0 -> last, i=spent-1 -> first
                            float u = Mathf.Clamp01(rp - revIndex);   // grows one-by-one in reverse
                            u = Ease(u);

                            // Hard-zero when empty (prevents tiny “near 0” artifacts)
                            rW = (u <= 0.0001f) ? 0f : Mathf.Lerp(0f, ringDotBaseRadiusWorld, u);
                        }

                        float rOS = rW * invS;

                        float a = start + step * (ringClockwise ? -i : i);
                        float xW = Mathf.Cos(a) * ringRadiusWorld;
                        float zW = Mathf.Sin(a) * ringRadiusWorld;
                        float yW = 0f;

                        Vector3 pOS = new Vector3(xW, yW, zW) * invS;
                        if (rW > 0f)
                            _ringSdf.AddBall(pOS, rOS);

                    }

                }
                else
                {
                    // ready ring
                    for (int i = 0; i < ringDots; i++)
                    {
                        float a = start + step * (ringClockwise ? -i : i);
                        float xW = Mathf.Cos(a) * ringRadiusWorld;
                        float zW = Mathf.Sin(a) * ringRadiusWorld;
                        float yW = 0f; // keep ring dots in the plane


                        Vector3 pOS = new Vector3(xW, yW, zW) * invS;
                        _ringSdf.AddBall(pOS, ringDotBaseRadiusWorld * invS);
                    }
                }
            }

            _ringSdf.Apply();

            // Place ring volume at player center (local space via parented module)
            _ringSdf.transform.position = centerWS;
            _ringSdf.transform.rotation = Quaternion.identity;
        }

private void BuildOrb(Vector3 centerWS)
{
    if (!_orbSdf) return;

    _orbSdf.Clear();

    bool show = _charging || _burstT > 0f;
    if (!show)
    {
        _orbSdf.Apply();
        return;
    }

    Vector3 orbCenterWS = centerWS + _aimDirWS * orbForwardOffsetWorld; // centerWS already has Y plane offset
    float invS = 1f / Mathf.Max(0.0001f, _orbVolumeSizeWorld);

    // Base orb radius from charge
    float rW = Mathf.Lerp(orbMinRadiusWorld, orbMaxRadiusWorld, _charge01);

    // --- Scaling jitter while charging (orb only) ---
    // Use a stable per-player offset so different players don't jitter in sync.
    if (_charging && _burstT <= 0f && orbScaleJitterAmp > 0.0001f)
    {
        float id = (owner != null) ? owner.playerID * 13.37f : 0f;
        float n = Mathf.PerlinNoise(id, Time.time * orbScaleJitterFreq); // 0..1
        float signed = (n * 2f - 1f); // -1..1

        float w = Mathf.Clamp01(_charge01) * orbScaleJitterChargeWeight;
        float jitterMul = 1f + signed * orbScaleJitterAmp * w;
        rW *= Mathf.Max(0.65f, jitterMul);
    }

    // --- Burst behavior ---
    if (_burstT > 0f)
    {
        _burstT -= Time.deltaTime;
        if (_burstT < 0f) _burstT = 0f;

        // 0..1 through the burst
        float t01 = 1f - Mathf.Clamp01(_burstT / Mathf.Max(0.001f, burstDuration));

        // Smooth “in/out” so it doesn’t end abruptly
        float ease = Ease(t01);                // smoothstep 0..1
        float pulse = Mathf.Sin(Mathf.PI * t01); // 0..1..0 (perfect for shard size fade)

        // Main orb pop (ramps up smoothly)
        float burstMul = Mathf.Lerp(1f, burstRadiusMult, ease);
        rW *= burstMul;

        // Shards: randomized fan-out + subtle swirl + eased fade-out
        int n = (_shardDirs != null) ? _shardDirs.Length : 0;
        if (n > 0)
        {
            float spreadW = Mathf.Lerp(burstSpreadMinWorld, burstSpreadMaxWorld, _burstCharge01) * ease;
            float shardBaseRW = Mathf.Lerp(burstShardRadiusMinWorld, burstShardRadiusMaxWorld, _burstCharge01) * pulse;


            for (int i = 0; i < n; i++)
            {
                // Swirl a little as it bursts (optional)
                float spin = burstShardSpinDeg * Mathf.Deg2Rad * _shardSpinMul[i] * t01;
                Vector2 d2 = new Vector2(
                    _shardDirs[i].x * Mathf.Cos(spin) - _shardDirs[i].y * Mathf.Sin(spin),
                    _shardDirs[i].x * Mathf.Sin(spin) + _shardDirs[i].y * Mathf.Cos(spin)
                );

                Vector3 offW = new Vector3(d2.x, 0f, d2.y) * (spreadW * _shardDistMul[i]);


                // Size fades in/out with pulse so the end isn't abrupt
                float shardRW = shardBaseRW * _shardSizeMul[i];


                if (shardRW > 0.0005f)
                    _orbSdf.AddBall(offW * invS, shardRW * invS);
            }
        }
    }

    // Main orb ball (object space)
    _orbSdf.AddBall(Vector3.zero, rW * invS);
    _orbSdf.Apply();

    _orbSdf.transform.position = orbCenterWS;
    _orbSdf.transform.rotation = Quaternion.identity;
}

    }
}
