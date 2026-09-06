using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>
    /// Foundation projectile class for ranged enemies.
    ///
    /// Handles:
    /// - Lifetime
    /// - Player hit damage (massScore drain)
    /// - Shield reflection (tag "Shield")
    /// - Optional wall bounce via collision normals
    /// - Optional damaging owner enemy when reflected
    ///
    /// Concrete projectiles can derive from this and implement visuals or specialized seeking.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class EnemyProjectileBase : MonoBehaviour
    {
        [Header("Motion")]
        public float speed = 10f;
        public float lifetimeSeconds = 6f;
        public bool useRigidbody = true;

        [Header("Damage")]
        [Tooltip("Mass removed from player on hit (massScore space 0..1).")]
        public float damageToPlayerMass01 = 0.08f;

        [Tooltip("If true, a shielded player can reflect this projectile.")]
        public bool reflectable = true;

        [Tooltip("Damage to the firing enemy when this projectile is reflected back into it.")]
        [Min(0f)] public float reflectedDamageToEnemyMassEq = 1f;

        [Tooltip("If true, projectile can bounce off world colliders.")]
        public bool canBounceOffWorld = false;

        [Min(0)] public int maxBounces = 3;

        [Header("Layer Filters")]
        [Tooltip("What counts as world for bouncing. If 0, bounces off everything except players/shields.")]
        public LayerMask worldMask;

        private Rigidbody _rb;
        private Vector3 _dir;
        private float _dieTime;
        private int _bounces;

        private bool _isReflected;
        private bool _impactResolved;
        public PlayerControllerScript ReflectedBy { get; private set; }

        public EnemyBase Owner { get; private set; }

        protected virtual void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            var col = GetComponent<Collider>();
            if (col != null && useRigidbody)
            {
                // Prefer non-trigger for bounce. You can set trigger in prefab if you only want trigger hits.
                // We'll support both trigger + collision callbacks.
            }
        }

        public virtual void Init(EnemyBase owner, Vector3 directionWS)
        {
            Owner = owner;
            _isReflected = false;
            _impactResolved = false;
            ReflectedBy = null;
            _bounces = 0;
            _dir = directionWS.sqrMagnitude > 0.0001f ? directionWS.normalized : Vector3.forward;

            _dieTime = (lifetimeSeconds > 0f) ? (Time.time + lifetimeSeconds) : float.PositiveInfinity;

            if (useRigidbody && _rb != null)
            {
                _rb.isKinematic = false;
                _rb.linearVelocity = _dir * speed;
                _rb.angularVelocity = Vector3.zero;
            }
        }

        protected virtual void Update()
        {
            if (Time.time >= _dieTime)
                Destroy(gameObject);

            if (!useRigidbody)
            {
                transform.position += _dir * (speed * Time.deltaTime);
            }
        }

        // --- Trigger hits (player/shield) ---
        protected virtual void OnTriggerEnter(Collider other)
        {
            if (_impactResolved) return;
            if (TryHandleEnemy(other)) return;
            if (TryHandleShield(other)) return;
            if (TryHandlePlayer(other)) return;
        }

        // --- Collision hits (world bounce) ---
        protected virtual void OnCollisionEnter(Collision collision)
        {
            if (collision == null) return;
            if (_impactResolved) return;
            if (TryHandleEnemy(collision.collider)) return;

            // Shield as non-trigger collider
            if (TryHandleShield(collision.collider)) return;

            // Player as non-trigger collider
            if (TryHandlePlayer(collision.collider)) return;

            if (!canBounceOffWorld) return;
            if (maxBounces > 0 && _bounces >= maxBounces)
            {
                Destroy(gameObject);
                return;
            }

            // Layer gate for world bounce
            if (worldMask.value != 0)
            {
                int layer = collision.collider.gameObject.layer;
                if ((worldMask.value & (1 << layer)) == 0)
                    return;
            }

            // Reflect velocity/direction about the contact normal
            Vector3 normal = collision.GetContact(0).normal;

            if (useRigidbody && _rb != null)
            {
                Vector3 v = _rb.linearVelocity;
                Vector3 rv = Vector3.Reflect(v, normal);
                _rb.linearVelocity = rv;
                _dir = rv.sqrMagnitude > 0.0001f ? rv.normalized : _dir;
            }
            else
            {
                _dir = Vector3.Reflect(_dir, normal);
            }

            _bounces++;
        }

        private bool TryHandleShield(Collider other)
        {
            if (!reflectable) return false;
            if (other == null) return false;
            if (!other.CompareTag("Shield")) return false;

            // Only reflect if the owning player's shield is actually active
            var shieldAbility = other.GetComponentInParent<Massive.Player.PlayerShieldAbility>();
            if (shieldAbility != null && !shieldAbility.IsActive) return false;

            var player = other.GetComponentInParent<PlayerControllerScript>();
            if (player == null || player.temporarilyEliminated) return false;
            if (shieldAbility == null && !player.shieldOn) return false;
            ReflectedBy = player;

            ReflectFrom(other.transform.position);
            return true;
        }

        private void ReflectFrom(Vector3 shieldPos)
        {
            _isReflected = true;

            Vector3 normal = (transform.position - shieldPos);
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.0001f)
                normal = -_dir;
            normal.Normalize();

            if (useRigidbody && _rb != null)
            {
                Vector3 v = _rb.linearVelocity;
                Vector3 rv = Vector3.Reflect(v, normal);
                _rb.linearVelocity = rv;
                _dir = rv.sqrMagnitude > 0.0001f ? rv.normalized : _dir;
            }
            else
            {
                _dir = Vector3.Reflect(_dir, normal);
            }

            // Optional: if reflected, it can now hurt its owner.
        }

        private bool TryHandlePlayer(Collider other)
        {
            if (other == null) return false;
            if (!other.CompareTag("Player")) return false;

            var player = other.GetComponentInParent<PlayerControllerScript>();
            if (player == null) return false;
            if (player.temporarilyEliminated) return true;
            if (player.IsInvulnerable) return true;

            // If the player is shielding and projectile is reflectable,
            // the trigger/collision will likely have hit the shield collider first.
            // But as a safety, allow shielded players to block it here.
            if (reflectable && player.shieldOn) return true;

            float dmg = Mathf.Max(0f, damageToPlayerMass01);
            if (dmg > 0f)
                player.ApplyExternalMassDelta(-dmg, allowDeath: true);

            _impactResolved = true;
            Destroy(gameObject);
            return true;
        }

        protected bool TryHandleEnemy(Collider other)
        {
            if (_impactResolved || !_isReflected || Owner == null || other == null) return false;
            var target = other.GetComponentInParent<EnemyBase>();
            if (target != Owner || target.IsDead || target.IsPaused) return false;
            if (reflectedDamageToEnemyMassEq <= 0f) return false;

            _impactResolved = true;
            target.TakeDamage(reflectedDamageToEnemyMassEq, EnemyDamageSource.ShieldReflect, ReflectedBy);
            Destroy(gameObject);
            return true;
        }

        protected virtual void OnDestroy()
        {
            // If this projectile is reflected and hits owner in derived classes,
            // you'd apply Owner.TakeDamage(...) there.
        }
    }
}
