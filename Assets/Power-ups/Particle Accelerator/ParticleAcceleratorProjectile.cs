using UnityEngine;

namespace Massive.PowerUps
{
    public class ParticleAcceleratorProjectile : MonoBehaviour
    {
        private PlayerControllerScript _shooter;
        private Vector3 _dir;
        private float _speed;
        private float _maxDistance;
        private float _radius;
        private float _massRemove;
        private bool _transfer;

        private Vector3 _start;

        public void Init(PlayerControllerScript shooter, Vector3 dirWS, float speed, float maxDistance, float radius, float massRemove, bool transferToShooter)
        {
            _shooter = shooter;
            _dir = dirWS.normalized;
            _speed = speed;
            _maxDistance = maxDistance;
            _radius = radius;
            _massRemove = massRemove;
            _transfer = transferToShooter;
            _start = transform.position;
        }

        private void Update()
        {
            transform.position += _dir * (_speed * Time.deltaTime);

            float traveled = Vector3.Distance(_start, transform.position);
            if (traveled >= _maxDistance)
                Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_shooter) { Destroy(gameObject); return; }

            var victim = other.GetComponent<PlayerControllerScript>();
            if (!victim || victim == _shooter) return;

            // remove mass from victim
            victim.ApplyExternalMassDelta(-_massRemove, allowDeath: true);

            // optionally give to shooter
            if (_transfer)
                _shooter.ApplyExternalMassDelta(+_massRemove, allowDeath: false);

            Destroy(gameObject);
        }
    }
}