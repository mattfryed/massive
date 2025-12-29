using UnityEngine;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    public class PowerUpPickup : MonoBehaviour
    {
        public PowerUpDefinition definition;

        private float _deathTime;
        private bool _despawning;
        private PowerUpIconManifestAnimator _anim;

        private void Awake()
        {
            _anim = GetComponent<PowerUpIconManifestAnimator>();
        }

        private void OnEnable()
        {
            _despawning = false;
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
                    _anim.BeginDespawn();
                    return;
                }

                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_despawning) return;

            var p = other.GetComponentInParent<PlayerPowerUpController>();
            if (!p) return;

            if (definition == null)
            {
                Debug.LogWarning($"[{name}] PowerUpPickup hit player but definition is NULL.");
                return;
            }

            p.Equip(definition);

            if (PowerUpPickupToastSystem.Instance != null)
                PowerUpPickupToastSystem.Instance.Show(definition, transform.position);

            if (_anim != null)
            {
                _despawning = true;
                _deathTime = float.PositiveInfinity; // prevent Update() destroying mid-outro
                _anim.BeginDespawn();
                return;
            }

            Destroy(gameObject);
        }
    }
}
