using UnityEngine;
using Massive.Player;

namespace Massive.Enemies
{
    /// <summary>
    /// Dyson Sphere (Melee) behavior:
    /// - Chases the nearest active player.
    /// - When in range, panels "spear" forward, then the sphere lunges.
    /// - If the lunge hits a Shield collider, the sphere is parried and becomes stunned.
    /// - If the lunge hits a Player collider, it removes player mass.
    ///
    /// Visuals are handled by <see cref="DysonSpherePanels"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyBase))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DysonSphereController : MonoBehaviour
    {
        private enum State
        {
            Chase,
            Windup,
            Lunge,
            Cooldown,
            Stunned
        }

        [Header("Refs")]
        [SerializeField] private EnemyBase enemy;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private DysonSpherePanels panels;
        [SerializeField] private EnemyObstacleAvoidance avoidance;

        [Header("Spawn")]
        [Tooltip("If true, the Dyson Sphere will not move or attack until its spawn-in animation is finished.")]
        [SerializeField] private bool lockMovementDuringSpawn = true;

        [Header("Targeting")]
        [SerializeField, Min(0.05f)] private float reacquireIntervalSeconds = 0.35f;
        [SerializeField, Min(0f)] private float ignoreTargetsFartherThan = 0f; // 0 = no limit

        [Header("Chase")]
        [Tooltip("If 0, uses EnemyDefinition.moveSpeed")]
        [SerializeField, Min(0f)] private float chaseSpeedOverride = 0f;
        [Tooltip("How quickly we steer velocity toward the desired chase velocity")]
        [SerializeField, Min(0f)] private float chaseAcceleration = 40f;
        [Tooltip("If 0, uses EnemyDefinition.turnSpeed")]
        [SerializeField, Min(0f)] private float turnSpeedOverride = 0f;

        [Header("Attack")]
        [SerializeField, Min(0.1f)] private float attackTriggerDistance = 3.0f;
        [SerializeField, Min(0.05f)] private float windupSeconds = 0.35f;

        [SerializeField, Min(0.1f)] private float lungeSpeed = 16f;
        [SerializeField, Min(0.05f)] private float lungeSeconds = 0.22f;
        [SerializeField, Min(0.05f)] private float postLungeCooldownSeconds = 0.65f;

        [Tooltip("Multiplier on EnemyDefinition.damageToPlayerMass01 for the lunge hit.")]
        [SerializeField, Min(0f)] private float lungeDamageMultiplier = 1.25f;

        [Header("Parry / Stun")]
        [SerializeField, Min(0.05f)] private float parryStunSeconds = 1.25f;
        [SerializeField, Min(0f)] private float parryRecoilSpeed = 8f;
        [SerializeField, Min(0f)] private float stunnedLinearDamping = 10f;

        
        [Header("Stun Visual")]
        [Tooltip("Exponent for how quickly the stun jitter decays over time. Higher = strong at start, quickly fades.")]
        [SerializeField, Min(0.1f)] private float stunVisualDecayPower = 1.6f;
        [Tooltip("Multiplier on the visual stun intensity sent to DysonSpherePanels.")]
        [SerializeField, Min(0f)] private float stunVisualIntensity = 1f;

[Header("Collision Tags")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private string shieldTag = "Shield";

        [Header("Pathing")]
        [Tooltip("If true, the Dyson Sphere will only begin windup when it has line-of-sight to the target (prevents lunging into walls/obstacles).")]
        [SerializeField] private bool requireLineOfSightToAttack = true;
        [Tooltip("Small padding subtracted from the LoS ray length so we don't accidentally ray-hit the target's own collider.")]
        [SerializeField, Min(0f)] private float lineOfSightTargetPadding = 0.15f;

        // --- runtime ---
        private State _state = State.Chase;
        private float _stateEndTime = -Mathf.Infinity;

        private float _nextReacquireTime = -Mathf.Infinity;
        private PlayerControllerScript _target;
        private PlayerControllerScript[] _players;

        private Vector3 _attackDirWS;
        private bool _lungeResolved;

        private float _spear01;

        // stun visual timing
        private float _stunStartTime = -Mathf.Infinity;
        private float _stunDuration = 0f;

        private void Reset()
        {
            enemy = GetComponent<EnemyBase>();
            rb = GetComponent<Rigidbody>();
            panels = GetComponentInChildren<DysonSpherePanels>(true);
            avoidance = GetComponent<EnemyObstacleAvoidance>();
        }

        private void Awake()
        {
            if (!enemy) enemy = GetComponent<EnemyBase>();
            if (!rb) rb = GetComponent<Rigidbody>();
            if (!panels) panels = GetComponentInChildren<DysonSpherePanels>(true);
            if (!avoidance) avoidance = GetComponent<EnemyObstacleAvoidance>();

            // Safety defaults for top-down arena motion.
            if (rb)
            {
                rb.useGravity = false;
                rb.constraints |= RigidbodyConstraints.FreezePositionY;
                rb.constraints |= RigidbodyConstraints.FreezeRotationX;
                rb.constraints |= RigidbodyConstraints.FreezeRotationZ;
            }
        }

        private void OnEnable()
        {
            _state = State.Chase;
            _stateEndTime = -Mathf.Infinity;
            _lungeResolved = false;
            _spear01 = 0f;
            _stunStartTime = -Mathf.Infinity;
            _stunDuration = 0f;
            if (panels != null) panels.SetStun01(0f);
            _nextReacquireTime = -Mathf.Infinity;
        }

        private void FixedUpdate()
        {
            if (enemy == null || rb == null) return;
            if (enemy.IsDead) return;
            if (enemy.IsPaused) { rb.linearVelocity = Vector3.zero; return; }


            // Spawn lock: keep the Dyson Sphere inert until the panels finish spawning in.
            if (lockMovementDuringSpawn && panels != null && panels.IsSpawning)
            {
                _state = State.Chase;
                _stateEndTime = -Mathf.Infinity;
                _lungeResolved = false;
                _spear01 = 0f;
                rb.linearVelocity = Vector3.zero;
                panels.SetSpear01(0f);
                panels.SetStun01(0f);
                return;
            }
            if (rb.isKinematic) return;

            // Targeting refresh
            if (Time.time >= _nextReacquireTime)
            {
                _nextReacquireTime = Time.time + Mathf.Max(0.05f, reacquireIntervalSeconds);
                RefreshPlayersCache();
                _target = PickBestTarget(_target);
            }

            // State machine
            switch (_state)
            {
                case State.Chase:
                    TickChase();
                    break;

                case State.Windup:
                    TickWindup();
                    break;

                case State.Lunge:
                    TickLunge();
                    break;

                case State.Cooldown:
                    TickCooldown();
                    break;

                case State.Stunned:
                    TickStunned();
                    break;
            }

            // Drive visuals
            if (panels != null)
            {
                panels.SetSpear01(_spear01);
                panels.SetStun01(GetStunVisual01());
            }
        }

        // -------------------------
        // State ticks
        // -------------------------

        private float GetMoveSpeed()
        {
            if (chaseSpeedOverride > 0f) return chaseSpeedOverride;
            return enemy.Definition != null ? Mathf.Max(0f, enemy.Definition.moveSpeed) : 4f;
        }

        private float GetTurnSpeed()
        {
            if (turnSpeedOverride > 0f) return turnSpeedOverride;
            return enemy.Definition != null ? Mathf.Max(0f, enemy.Definition.turnSpeed) : 360f;
        }

        private float GetLungeDamage01()
        {
            float baseDmg = enemy.Definition != null ? enemy.Definition.damageToPlayerMass01 : 0.08f;
            return Mathf.Max(0f, baseDmg * Mathf.Max(0f, lungeDamageMultiplier));
        }

        private void TickChase()
        {
            _spear01 = Mathf.MoveTowards(_spear01, 0f, Time.fixedDeltaTime * 6f);

            if (_target == null)
            {
                // No target: drift to a stop.
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 6f);
                return;
            }

            Vector3 to = _target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            if (ignoreTargetsFartherThan > 0f && dist > ignoreTargetsFartherThan)
            {
                _target = null;
                return;
            }

            Vector3 seekDir = (to.sqrMagnitude > 0.0001f) ? to.normalized : Vector3.zero;

            // Only begin an attack if we have line-of-sight. This prevents "dumb" lunges into
            // central obstacles (like NOVA's star) when the target is technically close but blocked.
            if (dist <= Mathf.Max(0.1f, attackTriggerDistance))
            {
                bool canAttack = true;
                if (requireLineOfSightToAttack && avoidance != null && seekDir.sqrMagnitude > 0.0001f)
                {
                    float losDist = Mathf.Max(0.01f, dist - Mathf.Max(0f, lineOfSightTargetPadding));
                    canAttack = !avoidance.HasObstacleInDirection(seekDir, losDist, out _);
                }

                if (canAttack)
                {
                    BeginWindup(to);
                    return;
                }
            }

            // Apply obstacle avoidance to the chase direction (keeps enemies from getting "stuck"
            // pushing directly into geometry).
            Vector3 moveDir = seekDir;
            if (avoidance != null && seekDir.sqrMagnitude > 0.0001f)
                moveDir = avoidance.AdjustDirection(seekDir, out _);

            // Rotate toward travel dir (use the *actual move dir* when avoiding).
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRot = Quaternion.LookRotation(moveDir.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRot, GetTurnSpeed() * Time.fixedDeltaTime);
            }

            // Chase velocity
            Vector3 desiredVel = (moveDir.sqrMagnitude > 0.0001f) ? moveDir.normalized * GetMoveSpeed() : Vector3.zero;
            desiredVel.y = 0f;

            float a = Mathf.Max(0f, chaseAcceleration);
            if (a <= 0.001f)
            {
                rb.linearVelocity = desiredVel;
            }
            else
            {
                rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, desiredVel, a * Time.fixedDeltaTime);
            }
        }

        private void TickWindup()
        {
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 16f);

            // Aim at target during windup (keeps it fair but readable)
            Vector3 dir = _attackDirWS;
            if (_target != null)
            {
                Vector3 to = _target.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f)
                    dir = to.normalized;
            }

            if (dir.sqrMagnitude > 0.0001f)
            {
                _attackDirWS = dir;
                Quaternion desired = Quaternion.LookRotation(_attackDirWS, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, GetTurnSpeed() * Time.fixedDeltaTime);
            }

            float t01 = 1f;
            if (windupSeconds > 0.0001f)
                t01 = Mathf.Clamp01(1f - ((_stateEndTime - Time.time) / windupSeconds));

            // Spear ramps in as the telegraph
            _spear01 = Mathf.SmoothStep(0f, 1f, t01);

            if (Time.time >= _stateEndTime)
                BeginLunge();
        }

        private void TickLunge()
        {
            _spear01 = 1f;

            // Maintain forward drive during the lunge
            Vector3 v = _attackDirWS * lungeSpeed;
            v.y = 0f;
            rb.linearVelocity = v;

            if (Time.time >= _stateEndTime)
                BeginCooldown();
        }

        private void TickCooldown()
        {
            _spear01 = Mathf.MoveTowards(_spear01, 0f, Time.fixedDeltaTime * 10f);
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 8f);

            if (Time.time >= _stateEndTime)
                _state = State.Chase;
        }

        private void TickStunned()
        {
            _spear01 = Mathf.MoveTowards(_spear01, 0f, Time.fixedDeltaTime * 18f);

            // Dampen residual velocity so parry recoil feels good but doesn't slide forever.
            float d = Mathf.Max(0f, stunnedLinearDamping);
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, d * Time.fixedDeltaTime);

            if (Time.time >= _stateEndTime)
                _state = State.Chase;
        }

        // -------------------------
        // Transitions
        // -------------------------

        private void BeginWindup(Vector3 toTarget)
        {
            _state = State.Windup;
            _stateEndTime = Time.time + Mathf.Max(0.05f, windupSeconds);
            _lungeResolved = false;

            toTarget.y = 0f;
            _attackDirWS = (toTarget.sqrMagnitude > 0.0001f) ? toTarget.normalized : transform.forward;
        }

        private void BeginLunge()
        {
            _state = State.Lunge;
            _stateEndTime = Time.time + Mathf.Max(0.05f, lungeSeconds);
            _lungeResolved = false;

            // Lock aim at lunge start
            _attackDirWS.y = 0f;
            if (_attackDirWS.sqrMagnitude < 0.0001f)
                _attackDirWS = transform.forward;
            _attackDirWS.Normalize();

            enemy.PlayAttackSfx();
        }

        private void BeginCooldown()
        {
            _state = State.Cooldown;
            _stateEndTime = Time.time + Mathf.Max(0.05f, postLungeCooldownSeconds);
        }

        private void BeginStunned(float durationSeconds)
        {
            _state = State.Stunned;
            _stunStartTime = Time.time;
            _stunDuration = Mathf.Max(0.05f, durationSeconds);
            _stateEndTime = _stunStartTime + _stunDuration;
        }

        
        private float GetStunVisual01()
        {
            if (_state != State.Stunned) return 0f;
            if (_stunDuration <= 0.0001f) return 0f;

            // remaining fraction (1 at start, 0 at end)
            float elapsed = Time.time - _stunStartTime;
            float t01 = Mathf.Clamp01(elapsed / _stunDuration);
            float remaining01 = 1f - t01;

            float pow = Mathf.Max(0.1f, stunVisualDecayPower);
            float mul = Mathf.Max(0f, stunVisualIntensity);
            return Mathf.Clamp01(Mathf.Pow(remaining01, pow) * mul);
        }

// -------------------------
        // Collisions / parry / damage
        // -------------------------

        private void OnCollisionEnter(Collision collision)
        {
            HandleImpact(collision.collider);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleImpact(other);
        }

        private void HandleImpact(Collider other)
        {
            if (enemy == null || enemy.IsDead || enemy.IsPaused) return;
            if (_state != State.Lunge) return;
            if (_lungeResolved) return;
            if (other == null) return;

            // Parry
            if (other.CompareTag(shieldTag))
            {
                float strength = 1f;
                var shield = other.GetComponentInParent<PlayerShieldAbility>();
                if (shield != null && shield.IsActive)
                    strength = Mathf.Clamp01(shield.CurrentStrength01);

                _lungeResolved = true;

                // Recoil
                Vector3 recoil = -_attackDirWS * Mathf.Max(0f, parryRecoilSpeed);
                recoil.y = 0f;
                rb.linearVelocity = recoil;

                // Stronger shields stun longer (feels like a "clean" parry)
                float stun = Mathf.Lerp(parryStunSeconds * 0.55f, parryStunSeconds, strength);
                BeginStunned(stun);
                return;
            }

            // Player hit
            if (other.CompareTag(playerTag))
            {
                var victim = other.GetComponentInParent<PlayerControllerScript>();
                if (victim == null) return;

                // Shield blocks should be handled by the shield collider itself.
                // If we hit the body while they were shielding, it means the spear got around the shield.
                float dmg01 = GetLungeDamage01();
                if (dmg01 > 0.0001f)
                    victim.ApplyExternalMassDelta(-dmg01, allowDeath: true);

                _lungeResolved = true;
                BeginCooldown();
            }
        }

        // -------------------------
        // Target selection
        // -------------------------

        private void RefreshPlayersCache()
        {
            // Unity 6: fast enough at this scale, and player roster can change between 1v1/2v2.
            _players = FindObjectsByType<PlayerControllerScript>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        private PlayerControllerScript PickBestTarget(PlayerControllerScript current)
        {
            if (_players == null || _players.Length == 0) return null;

            // Keep current if still valid.
            if (IsValidTarget(current))
                return current;

            float best = float.PositiveInfinity;
            PlayerControllerScript bestP = null;

            Vector3 p0 = transform.position;
            p0.y = 0f;

            for (int i = 0; i < _players.Length; i++)
            {
                var p = _players[i];
                if (!IsValidTarget(p)) continue;

                Vector3 d = p.transform.position - p0;
                d.y = 0f;
                float dist = d.sqrMagnitude;

                if (ignoreTargetsFartherThan > 0f)
                {
                    float max = ignoreTargetsFartherThan;
                    if (dist > max * max) continue;
                }

                if (dist < best)
                {
                    best = dist;
                    bestP = p;
                }
            }

            return bestP;
        }

        private static bool IsValidTarget(PlayerControllerScript p)
        {
            if (p == null) return false;
            if (!p.gameObject.activeInHierarchy) return false;
            if (p.temporarilyEliminated) return false;
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, attackTriggerDistance));
        }
#endif
    }
}
