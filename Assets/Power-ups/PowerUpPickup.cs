using UnityEngine;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    public class PowerUpPickup : MonoBehaviour
    {
        public PowerUpDefinition definition;

        [Header("Collision")]
        public string playerTag = "Player";
        public float pickupRadius = 0.75f;

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
            if (!other.CompareTag(playerTag)) return;

            var p = other.GetComponent<PlayerPowerUpController>();
            if (!p) p = other.GetComponentInParent<PlayerPowerUpController>();
            if (!p || definition == null) return;

            p.Equip(definition);
            Destroy(gameObject);
        }
    }
}