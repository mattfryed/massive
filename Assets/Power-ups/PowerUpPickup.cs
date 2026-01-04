using UnityEngine;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    public class PowerUpPickup : MonoBehaviour
    {
        public PowerUpDefinition definition;

        [Header("Activation")]
        [Tooltip("If enabled, only colliders on these layers can activate the pickup (ex: PlayerAttackHitbox).")]
        [SerializeField] private bool requireAttackToActivate = true;

        [SerializeField] private LayerMask attackActivatorLayers;

        private float _deathTime;
        private bool _despawning;
        private bool _activated;
        private PowerUpIconManifestAnimator _anim;

        private void Awake()
        {
            _anim = GetComponent<PowerUpIconManifestAnimator>();
        }

        private void OnEnable()
        {
            _despawning = false;
            _activated = false;
            _deathTime = Time.time + (definition ? definition.worldLifetimeSeconds : 10f);
        }

        private void Update()
        {
            if (_despawning) return;

            if (Time.time >= _deathTime)
            {
                // timed out -> play outro if possible
                if (_anim != null)
                {
                    _despawning = true;
                    _anim.BeginDespawn(); // timeout uses existing outro
                    return;
                }

                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_despawning) return;

            // Component-based: only a real melee hitbox can activate.
            // (Hitbox is already gated by PlayerMelee to the attack window.)
            var melee = other.GetComponent<PlayerMelee>();
            if (!melee) melee = other.GetComponentInParent<PlayerMelee>();
            if (!melee) return;

            var p = melee.GetComponentInParent<PlayerPowerUpController>();
            if (!p) return;

            if (definition == null)
            {
                Debug.LogWarning($"[{name}] PowerUpPickup hit by melee but definition is NULL.");
                return;
            }

            p.Equip(definition);

            if (PowerUpPickupToastSystem.Instance != null)
                PowerUpPickupToastSystem.Instance.Show(definition, transform.position);

            if (_anim != null)
            {
                _despawning = true;
                _deathTime = float.PositiveInfinity;

                // Use your new “attack shatter” path here
                _anim.BeginAttackDespawn();
                return;
            }

            Destroy(gameObject);
        }

    }
}
