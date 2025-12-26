using UnityEngine;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    public class PowerUpPickup : MonoBehaviour
    {
        public PowerUpDefinition definition;

        private float _deathTime;

        private void OnEnable()
        {
            _deathTime = Time.time + (definition ? definition.worldLifetimeSeconds : 10f);
        }

        private void Update()
        {
            if (Time.time >= _deathTime)
                Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
Debug.Log($"[PowerUpPickup] Triggered by {other.name} root={other.transform.root.name}", this);

var p = other.GetComponentInParent<PlayerPowerUpController>();
Debug.Log($"[PowerUpPickup] Found PlayerPowerUpController? {(p != null)}", this);
            if (!p) p = other.GetComponentInParent<PlayerPowerUpController>();
            if (!p) return;

            if (definition == null)
            {
                Debug.LogWarning($"[{name}] PowerUpPickup hit player but definition is NULL.");
                return;
            }

            p.Equip(definition);
            Destroy(gameObject);
        }
    }
}