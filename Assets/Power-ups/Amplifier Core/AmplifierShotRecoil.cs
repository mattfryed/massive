using Massive.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Multiplier
{
    /// <summary>Brief displacement after a Core hit, including NPCs with kinematic motors.</summary>
    [DefaultExecutionOrder(500)]
    public sealed class AmplifierShotRecoil : MonoBehaviour
    {
        private EnemyBase enemy;
        private Rigidbody body;
        private Vector3 velocity;
        private float remaining, radius;
        private readonly RaycastHit[] hits = new RaycastHit[64];

        public static void Apply(EnemyBase target, Vector3 impulse)
        {
            if (!target || impulse.sqrMagnitude < .00001f) return;
            var recoil = target.GetComponent<AmplifierShotRecoil>();
            if (!recoil) recoil = target.gameObject.AddComponent<AmplifierShotRecoil>();
            recoil.enemy = target; recoil.body = target.GetComponent<Rigidbody>();
            recoil.radius = .1f;
            foreach (var hurtbox in target.GetComponentsInChildren<EnemyHurtbox>())
            {
                var collider = hurtbox.GetComponent<Collider>();
                if (collider) recoil.radius = Mathf.Max(recoil.radius, Mathf.Min(collider.bounds.extents.x, collider.bounds.extents.z));
            }
            recoil.velocity = impulse; recoil.remaining = .3f; recoil.enabled = true;
            if (recoil.body && !recoil.body.isKinematic) recoil.body.linearVelocity = impulse;
        }

        private void FixedUpdate()
        {
            if (!enemy || enemy.IsDead || remaining <= 0f) { enabled = false; return; }
            if (enemy.IsPaused) return;
            float dt = Time.fixedDeltaTime;
            Vector3 origin = body ? body.position : transform.position;
            float distance = velocity.magnitude * dt;
            Vector3 direction = velocity.normalized;
            int count = gameObject.scene.GetPhysicsScene().SphereCast(origin, radius, direction, hits, distance + .01f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (!collider || collider.transform.IsChildOf(transform) || collider.GetComponentInParent<AmplifierCoreGameplay>() ||
                    Mathf.Abs(hits[i].normal.y) > .5f || Physics.GetIgnoreLayerCollision(gameObject.layer, collider.gameObject.layer)) continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, hits[i].distance - .01f));
            }
            if (count == hits.Length) distance = 0f;
            Vector3 step = direction * distance;
            if (!body) transform.position += step;
            else if (body.isKinematic) body.MovePosition(origin + step);
            else body.linearVelocity = step / dt;
            velocity *= Mathf.Exp(-7f * dt);
            remaining -= dt;
        }
        private void OnDisable() { remaining = 0f; }
    }
}
