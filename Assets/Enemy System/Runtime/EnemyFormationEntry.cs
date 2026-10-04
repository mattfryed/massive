using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>A brief coordinated entrance, then hands control back to the enemy's existing AI.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class EnemyFormationEntry : MonoBehaviour
    {
        public float seconds = .8f, speed = 1.5f;
        public Vector3 direction;
        private EnemyBase enemy;
        private DroneController drone;
        private Rigidbody body;
        private float age;
        private bool oldHold, oldAttacks;
        private void Start()
        {
            enemy = GetComponent<EnemyBase>(); body = GetComponent<Rigidbody>(); drone = GetComponent<DroneController>();
            oldHold = enemy.HoldPosition; oldAttacks = enemy.AttacksEnabled;
            enemy.HoldPosition = true; enemy.AttacksEnabled = false;
        }
        private void FixedUpdate()
        {
            if (!enemy || enemy.IsDead) { enabled = false; return; }
            if (enemy.IsPaused || body.isKinematic || drone && !drone.IsReady) return;
            age += Time.fixedDeltaTime;
            if (age >= seconds) { enabled = false; return; }
            var delta = direction.normalized * (speed * Time.fixedDeltaTime);
            float radius = Mathf.Max(.1f, enemy.Definition.spawnRadiusWorld * .5f);
            foreach (var hit in Physics.SphereCastAll(body.position, radius, direction.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform) && !hit.collider.GetComponentInParent<MatterNuggetScript>())
                { enabled = false; return; }
            var next = body.position + delta;
            if (enemy.Director && !enemy.Director.arenaBounds.ContainsWorldPoint(next, radius)) { enabled = false; return; }
            body.MovePosition(next); body.MoveRotation(Quaternion.LookRotation(direction, Vector3.up));
        }
        private void OnDisable()
        {
            if (!enemy) return;
            enemy.HoldPosition = oldHold; enemy.AttacksEnabled = oldAttacks;
        }
    }
}
