using UnityEngine;
using Massive.Player;

namespace Massive.PowerUps
{
    public class ParticleAcceleratorProjectile : MonoBehaviour
    {
        [Header("Optional Visuals")]
        [SerializeField] private ParticleAcceleratorBeamVisual beamVisual;  // child (BeamVolume)
        [SerializeField] private GameObject impactPrefab;                   // ParticleAcceleratorImpactVFX prefab

        [Header("Feed / Timing")]
        [Tooltip("After a hit, tail feeds into the hit point at speed * this multiplier.")]
        [SerializeField] private float tailCatchupSpeedMul = 1.25f;

        private PlayerControllerScript _shooter;
        private Vector3 _dir;
        private float _speed;
        private float _maxDistance;
        private float _radius;
        private float _massRemove;
        private bool _transfer;
        private LayerMask _blockMask;

        private float _charge01;

        // World origin at the moment the beam spawned
        private Vector3 _originWS;

        // Distances along dir
        private float _headDist;
        private float _tailDist;

        // Visual cap (pulse length while traveling)
        private float _beamVisualMaxLength;

        // Hit lock
        private bool _hitLocked;
        private float _hitDist;
        private bool _impactSpawned;

        private const string ShieldTag = "Shield";

        public void Init(
            PlayerControllerScript shooter,
            Vector3 dirWS,
            float speed,
            float maxDistance,
            float radius,
            float massRemove,
            bool transferToShooter,
            LayerMask blockMask,
            float charge01 = 0f,
            float beamVisualMaxLength = -1f,
            GameObject impactPrefabOverride = null)
        {
            _shooter = shooter;

            _dir = dirWS;
            _dir.y = 0f;
            if (_dir.sqrMagnitude < 0.0001f) _dir = Vector3.right;
            _dir.Normalize();

            _speed = Mathf.Max(0.01f, speed);
            _maxDistance = Mathf.Max(0.01f, maxDistance);

            _radius = Mathf.Max(0.001f, radius);
            _massRemove = massRemove;
            _transfer = transferToShooter;
            _blockMask = blockMask;

            _charge01 = Mathf.Clamp01(charge01);

            // Cap length cannot exceed travel distance.
            // If <= 0, treat as "no cap" (beam grows until hit/maxDistance).
            _beamVisualMaxLength = (beamVisualMaxLength > 0f)
                ? Mathf.Min(beamVisualMaxLength, _maxDistance)
                : _maxDistance;

            _originWS = transform.position;

            _headDist = 0f;
            _tailDist = 0f;

            _hitLocked = false;
            _impactSpawned = false;

            // Orient so BeamVisual local +Z is forward
            transform.rotation = Quaternion.LookRotation(_dir, Vector3.up);

            if (impactPrefabOverride) impactPrefab = impactPrefabOverride;

            if (!beamVisual) beamVisual = GetComponentInChildren<ParticleAcceleratorBeamVisual>(true);
            if (beamVisual) beamVisual.gameObject.SetActive(true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float headStep = _speed * dt;

            // 1) Advance head until hit or maxDistance
            if (!_hitLocked)
            {
                float nextHead = Mathf.Min(_headDist + headStep, _maxDistance);

                // Spherecast between current head and next head
                Vector3 headPosWS = _originWS + _dir * _headDist;
                float stepDist = nextHead - _headDist;

                if (stepDist > 0.0001f)
                {
                    if (TryFindNearestHit(headPosWS, stepDist, out var nearest, out var isBlocker, out var victim))
                    {
                        _hitLocked = true;
                        _hitDist = _headDist + Mathf.Max(0f, nearest.distance - 0.01f);
                        _hitDist = Mathf.Clamp(_hitDist, 0f, _maxDistance);

                            // Shield interaction (tagged "Shield") uses scaled stun + partial damage leak,
                            // instead of the standard full damage logic.
                            bool hitShield = nearest.collider != null && nearest.collider.CompareTag(ShieldTag);

                            if (hitShield)
                             {
                                float strength01 = 1f;
                                if (victim)
                                {
                                    var shieldAbility = victim.GetComponent<PlayerShieldAbility>();
                                    if (shieldAbility != null && shieldAbility.IsActive)
                                        strength01 = shieldAbility.CurrentStrength01;
                                }
                                strength01 = Mathf.Clamp01(strength01);
 
                                // 1) Attacker gets stunned proportional to shield strength
                                if (_shooter && victim && victim != _shooter)
                                    _shooter.Stun(_originWS + _dir * _hitDist, strength01);

                                // 2) Defender takes "leaked" damage proportional to weakness (1 - strength)
                                float leak01 = 1f - strength01;
                                if (leak01 > 0.0001f && victim && victim != _shooter)
                                {
                                    float leakedMass = _massRemove * leak01;
                                    victim.ApplyExternalMassDelta(-leakedMass, allowDeath: true);

                                    if (_transfer && _shooter)
                                        _shooter.ApplyExternalMassDelta(+leakedMass, allowDeath: false);
                                }
                            }
                            else
                            {
                                // Apply mass drain if we hit a player
                                if (!isBlocker && victim && victim != _shooter)
                                {
                                    victim.ApplyExternalMassDelta(-_massRemove, allowDeath: true);

                                    if (_transfer && _shooter)
                                        _shooter.ApplyExternalMassDelta(+_massRemove, allowDeath: false);
                                }
                             }

                        SpawnImpactOnce(_originWS + _dir * _hitDist);
                    }
                    else
                    {
                        _headDist = nextHead;
                    }
                }
                else
                {
                    _headDist = nextHead;
                }

                // Reached max distance without hit → lock and feed out
                if (!_hitLocked && _headDist >= _maxDistance - 0.0001f)
                {
                    _hitLocked = true;
                    _hitDist = _maxDistance;
                }
            }

            float headD = _hitLocked ? _hitDist : _headDist;

            // 2) Tail behavior:
            // - while traveling: tail is clamped to head - capLen (this is the "pulse cruising" behavior)
            // - after hit: tail catches up to the locked head
            if (!_hitLocked)
            {
                // Grow from muzzle to cap, then cruise with constant length.
                _tailDist = Mathf.Max(0f, headD - _beamVisualMaxLength);
            }
            else
            {
                float tailStep = _speed * tailCatchupSpeedMul * dt;
                _tailDist = Mathf.Min(_tailDist + tailStep, headD);
            }

            // 3) Drive visual segment
            Vector3 tailWS = _originWS + _dir * _tailDist;
            Vector3 headWS = _originWS + _dir * headD;

            // Keep transform at head for sanity
            transform.position = headWS;

            if (beamVisual)
                beamVisual.SetSegment(tailWS, headWS, _radius, _charge01);

            // 4) Done when tail reaches head
            if (_hitLocked && _tailDist >= headD - 0.0001f)
                Destroy(gameObject);
        }

        private bool TryFindNearestHit(
            Vector3 headPosWS,
            float stepDist,
            out RaycastHit nearest,
            out bool isBlocker,
            out PlayerControllerScript victim)
        {
            nearest = default;
            isBlocker = false;
            victim = null;

            // NOTE: Mostly ignore triggers so pickups / VFX triggers don't block the beam,
            // but allow the player's Shield (tagged "Shield") to be detected even if it's a trigger collider.
            var hits = Physics.SphereCastAll(headPosWS, _radius, _dir, stepDist, ~0, QueryTriggerInteraction.Collide);
 
            float best = float.PositiveInfinity;
            bool hasHit = false;

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (!h.collider) continue;

                // Ignore trigger colliders except for Shield
                if (h.collider.isTrigger && !h.collider.CompareTag(ShieldTag))
                    continue;

                // Ignore our own projectile hierarchy
                if (h.collider.GetComponentInParent<ParticleAcceleratorProjectile>() == this)
                    continue;

                // Ignore shooter
                if (_shooter && h.collider.GetComponentInParent<PlayerControllerScript>() == _shooter)
                    continue;

                if (h.distance < best)
                {
                    best = h.distance;
                    nearest = h;
                    hasHit = true;
                }
            }

            if (!hasHit) return false;

            isBlocker = (_blockMask.value != 0) &&
                        (((1 << nearest.collider.gameObject.layer) & _blockMask.value) != 0);

            victim = nearest.collider.GetComponentInParent<PlayerControllerScript>();
            return true;
        }

        private void SpawnImpactOnce(Vector3 posWS)
        {
            if (_impactSpawned) return;
            _impactSpawned = true;

            if (!impactPrefab) return;

            var go = Instantiate(impactPrefab, posWS, Quaternion.identity);

            // Optional: if your impact VFX supports charge scaling
            var fx = go.GetComponent<ParticleAcceleratorImpactVFX>();
            if (fx != null)
            {
                int seed = (int)(Time.time * 1000f) ^ (_shooter ? _shooter.playerID * 73856093 : 83492791);
                fx.Init(_charge01, seed);
            }
        }
    }
}
