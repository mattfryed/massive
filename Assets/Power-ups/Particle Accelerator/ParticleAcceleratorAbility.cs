using UnityEngine;
using Massive.Player;

namespace Massive.PowerUps
{
    internal class ParticleAcceleratorAbility : IPowerUpAbility
    {
        readonly ParticleAcceleratorPowerUpDefinition definition;
        readonly PlayerControllerScript owner;
        readonly PlayerPowerUpController host;
        PlayerVisualController visuals;
        ParticleAcceleratorBeam beam;
        ParticleAcceleratorVFXModule meter;
        bool held;
        bool attackArmed;
        int equippedFrame;
        float aimVelocity;
        float energy = 1f, minimumBurstRemaining;
        Vector3 aim = Vector3.right;
        public bool IsMovementActionActive => beam && beam.IsFiring;
        public float Energy01 => energy;

        public ParticleAcceleratorAbility(ParticleAcceleratorPowerUpDefinition def, PlayerControllerScript player, PlayerPowerUpController controller)
        { definition = def; owner = player; host = controller; }

        public void OnEquip()
        {
            visuals = owner.visualsController ? owner.visualsController : owner.GetComponentInChildren<PlayerVisualController>(true);
            aim = owner.transform.forward;
            energy = 1f;
            equippedFrame = Time.frameCount;
            attackArmed = !host.AttackInputHeld;
            owner.DeathStarted += OnOwnerDeath;
            var hub = owner.GetComponent<PlayerVFXHub>();
            if (!hub) hub = owner.gameObject.AddComponent<PlayerVFXHub>();
            meter = hub.GetOrCreate<ParticleAcceleratorVFXModule>(definition.vfxModulePrefab);
            meter.gameObject.SetActive(true);
            meter.Bind(owner);
            meter.SetEnergyMeter(energy);
            meter.SetVisible(true);
            if (definition.sustainedBeamPrefab)
            {
                beam = Object.Instantiate(definition.sustainedBeamPrefab);
                beam.name = "Particle Accelerator — sustained plasma";
                beam.Initialize(owner, definition);
                beam.EnergyDriver = this;
            }
        }

        Vector3 Origin => (visuals && visuals.visuals ? visuals.visuals.position : owner.transform.position)
            + (aim * Mathf.Max(0, definition.muzzleOffset) + Vector3.up * definition.muzzleHeight) * PlayerScaleAdjuster.SizeOf(owner);

        public void Tick(float dt)
        {
            if (dt <= 0 || owner.IsMatchInputLocked) return;
            if (beam) beam.QueueAdvance(dt, Origin, aim);
        }

        // The beam calls this after all actors have applied input. Energy, collision and
        // the minimum burst share the same clock, including a partially funded last frame.
        internal void AdvanceBeamFrame(float dt, Vector3 origin, Vector3 direction)
        {
            if (CannotFire)
            { held = false; minimumBurstRemaining = 0; beam.Stop(); }
            if (beam.IsFiring && !held && minimumBurstRemaining <= 0) beam.Stop();

            float idleTime = dt;
            if (beam.IsFiring)
            {
                float capacitySeconds = Mathf.Max(.01f, definition.fullMeterFireSeconds);
                float firingTime = Mathf.Min(dt, energy * capacitySeconds);
                if (!held) firingTime = Mathf.Min(firingTime, minimumBurstRemaining);
                beam.Advance(firingTime, origin, direction);
                energy = Mathf.Max(0, energy - firingTime / capacitySeconds);
                minimumBurstRemaining = Mathf.Max(0, minimumBurstRemaining - firingTime);
                idleTime = Mathf.Max(0, dt - firingTime);
                if (energy <= 0 || (!held && minimumBurstRemaining <= 0)) beam.Stop();
            }
            if (idleTime > 0)
            {
                energy = Mathf.Min(1, energy + idleTime / Mathf.Max(.01f, definition.emptyToFullRefillSeconds));
                beam.Advance(idleTime, origin, direction);
            }
            meter.SetEnergyMeter(energy);
            UpdateMovement();
        }

        bool CannotFire => owner.temporarilyEliminated || owner.IsStunned || owner.IsExternallyStunned || !owner.isActiveAndEnabled;

        public void PreTickInput(in PowerUpInputState input)
        {
            Vector3 direction = input.aimDirWS; direction.y = 0;
            if (direction.sqrMagnitude > .0001f)
            {
                direction.Normalize();
                if (IsMovementActionActive)
                {
                    float turnRate = visuals ? Mathf.Max(0, visuals.maxYawSpeed) : 900f;
                    turnRate *= Mathf.Clamp01(definition.turningWhileFiring);
                    float yaw = Mathf.Atan2(aim.z, aim.x) * Mathf.Rad2Deg;
                    float targetYaw = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
                    aimVelocity = Mathf.Clamp(aimVelocity, -turnRate, turnRate);
                    if (turnRate <= 0) aimVelocity = 0;
                    else if (definition.aimDirectionEaseSeconds > 0)
                        yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref aimVelocity,
                            definition.aimDirectionEaseSeconds, turnRate, Time.deltaTime);
                    else
                    {
                        aimVelocity = 0;
                        yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, turnRate * Time.deltaTime);
                    }
                    yaw *= Mathf.Deg2Rad;
                    aim = new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw));
                }
                else { aim = direction; aimVelocity = 0; }
            }
            // Physics pickup callbacks can precede routing this frame's Sword
            // edge. Never let that edge (or its held continuation) fire the beam.
            if (Time.frameCount == equippedFrame && (input.attackDown || input.attackHeld)) attackArmed = false;
            else if (input.attackUp || (!input.attackHeld && !input.attackDown)) attackArmed = true;
            if (beam) beam.UpdateQueuedPose(Origin, aim);
            held = input.attackHeld && !input.attackUp;
            UpdateMovement();
        }

        void UpdateMovement()
        {
            host.SetMovementMultiplierWhileCharging(IsMovementActionActive ? Mathf.Clamp01(definition.movementWhileFiring) : 1);
            if (visuals)
            {
                visuals.SetExternalChargeJitter01(0);
                visuals.SetExternalTurnDamp01(0);
                // Use the same limited heading for combat facing and the beam's swept path.
                // Raw input remains available for normal facing as soon as firing stops.
                if (IsMovementActionActive) visuals.SetAimDirection(aim);
            }
        }

        public bool ConsumesAttackWhileOnCooldown(in PowerUpInputState input) => input.attackDown || input.attackHeld || input.attackUp;

        public bool HandleInput(in PowerUpInputState input, out float cooldownToApply)
        {
            cooldownToApply = 0;
            // A new press is required after exhaustion or a parry. Holding an empty
            // meter never turns its first refill increment into repeated tiny shots.
            if (attackArmed && Time.frameCount > equippedFrame && input.attackDown && energy > 0 && beam && !beam.IsFiring && !CannotFire && !owner.IsMatchInputLocked)
            {
                minimumBurstRemaining = Mathf.Max(0, definition.minimumBurstSeconds);
                beam.Begin(Origin, aim);
                host.NotifyProjectileFired(beam.gameObject);
                UpdateMovement();
            }
            return ConsumesAttackWhileOnCooldown(input);
        }

        void ResetMovement()
        {
            aimVelocity = 0;
            host.SetMovementMultiplierWhileCharging(1);
            if (visuals) { visuals.SetExternalChargeJitter01(0); visuals.SetExternalTurnDamp01(0); }
        }

        public bool TryHandleShieldImpact(PlayerControllerScript attacker, Collider shieldCollider, Vector3 attackDirWS) => false;

        void OnOwnerDeath(PlayerControllerScript player)
        {
            held = false; attackArmed = false; minimumBurstRemaining = 0;
            if (beam) beam.Clear();
            ResetMovement();
        }

        public void OnUnequip()
        {
            owner.DeathStarted -= OnOwnerDeath;
            held = false; minimumBurstRemaining = 0; ResetMovement();
            if (meter) meter.SetVisible(false);
            if (beam) { beam.Clear(); Object.Destroy(beam.gameObject); beam = null; }
        }
    }
}
