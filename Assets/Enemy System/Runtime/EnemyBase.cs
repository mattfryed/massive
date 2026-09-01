using System;
using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>
    /// Shared base component for all enemy + inert world entities spawned by EnemyDirector.
    ///
    /// Step A (foundation) responsibilities:
    /// - Hold the EnemyDefinition instance that describes this enemy type.
    /// - Track health & death.
    /// - Support pausing (ex: during anomaly minigames).
    /// - Provide a simple damage API for sword hits, reflected projectiles, etc.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyBase : MonoBehaviour
    {
        public EnemyDefinition Definition { get; private set; }
        public EnemyDirector Director { get; private set; }

        public float HealthRemaining { get; private set; }
        public float SpawnTime { get; private set; }

        public bool IsDead { get; private set; }
        public bool IsPaused { get; private set; }

        /// <summary>
        /// Optional: some enemy objects can become team-affiliated (ex: Polarity Shift).
        /// -1 = neutral / none.
        /// </summary>
        public int OwnerTeamId { get; set; } = -1;

        public event Action<EnemyBase, EnemyDamageSource> Died;

        private Rigidbody _rb;
        private bool _savedKinematic;
        private Vector3 _savedVel;
        private Vector3 _savedAngVel;

        private float _killTime = float.PositiveInfinity;

        public void Init(EnemyDefinition def, EnemyDirector director)
        {
            Definition = def;
            Director = director;

            SpawnTime = Time.time;
            IsDead = false;

            HealthRemaining = (def != null) ? def.healthMassEq : 1f;

            _rb = GetComponent<Rigidbody>();

            // Lifetime
            if (def != null && def.lifetimeSeconds > 0f)
                _killTime = Time.time + def.lifetimeSeconds;
            else
                _killTime = float.PositiveInfinity;
        }

        private void Update()
        {
            if (IsDead) return;
            if (IsPaused) return;

            if (Time.time >= _killTime)
            {
                Kill(EnemyDamageSource.LifetimeExpired);
            }
        }

        public void Pause(bool paused)
        {
            if (IsDead) return;
            if (IsPaused == paused) return;

            IsPaused = paused;

            if (_rb != null)
            {
                if (paused)
                {
                    _savedKinematic = _rb.isKinematic;
                    _savedVel = _rb.linearVelocity;
                    _savedAngVel = _rb.angularVelocity;

                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                    _rb.isKinematic = true;
                    _rb.Sleep();
                }
                else
                {
                    _rb.isKinematic = _savedKinematic;
                    if (!_rb.isKinematic)
                    {
                        _rb.WakeUp();
                        _rb.linearVelocity = _savedVel;
                        _rb.angularVelocity = _savedAngVel;
                    }
                }
            }
        }

        public void TakeDamage(float amountMassEq, EnemyDamageSource source = EnemyDamageSource.Unknown)
        {
            if (IsDead) return;
            if (amountMassEq <= 0f) return;

            HealthRemaining -= amountMassEq;

            // Optional SFX
            if (Definition != null)
                TryPlaySfx(Definition.sfxHit);

            if (HealthRemaining <= 0f)
                Kill(source);
        }

        /// <summary>
        /// Convenience hook for behaviors to fire the enemy's configured attack SFX.
        /// Safe to call even if no audio is set up.
        /// </summary>
        public void PlayAttackSfx()
        {
            if (Definition == null) return;
            TryPlaySfx(Definition.sfxAttack);
        }

        public void Kill(EnemyDamageSource source = EnemyDamageSource.Unknown)
        {
            if (IsDead) return;
            IsDead = true;

            if (Definition != null)
                TryPlaySfx(Definition.sfxDeath);

            Died?.Invoke(this, source);

            // Foundation: no drop logic yet.
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // Ensure director bookkeeping stays correct even if destroyed externally.
            if (Director != null)
                Director.NotifyEnemyDestroyed(this);
        }

        protected void TryPlaySfx(AudioEventId evt)
        {
            // Your project already uses AudioSystem.I?.Play(AudioEventId, pos) for player SFX.
            // This keeps EnemyDefinition decoupled from any legacy string-based SfxPlayerScript.
            AudioSystem.I?.Play(evt, transform.position);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (Definition == null || !Definition.drawGizmos) return;

            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.05f, Definition.spawnRadiusWorld));
        }
#endif
    }
}
