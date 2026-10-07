using UnityEngine;
using Massive.Enemies;
using Massive.Player;
using Massive.Multiplier;

namespace Massive.PowerUps
{
    /// <summary>Player-owned turret plasma. The ability owns its clock and lifetime.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed partial class ParticleAcceleratorBeam : MonoBehaviour
    {
        public ParticleAcceleratorBeamVisual beamVisual;
        public ParticleBeamPlasmaContact corePlasma, contactPlasma;
        public float corePlasmaForwardOffset = .1f;
        public event System.Action<PlayerControllerScript, bool> PlayerContact;
        public bool IsFiring { get; private set; }
        public float BeamLength { get; private set; }
        public Vector3 BeamEnd { get; private set; }
        public Vector3 AimDirection => direction;
        public Collider Contact { get; private set; }
        public float AnimationTime => age;
        public ParticleBeamAimTrail AimTrail => trail;
        public ParticleAcceleratorAimGuide AimGuide { get; private set; }
        internal ParticleAcceleratorAbility EnergyDriver { private get; set; }
        readonly ParticleBeamAimTrail trail = new ParticleBeamAimTrail();
        PlayerControllerScript owner;
        ParticleAcceleratorPowerUpDefinition definition;
        ArenaBoundsFromVectorGrid arena;
        Vector3 origin, direction;
        float age, fadeAge, retractLength, referenceLength, radius, size = 1f;
        float reachProgress;
        bool fading;
        Collider previousContact;
        bool advanceQueued;
        float queuedDelta;
        Vector3 queuedOrigin, queuedAim;

        public void UpdateQueuedPose(Vector3 position, Vector3 aim)
        { queuedOrigin = position; queuedAim = aim; }

        public void QueueAdvance(float dt, Vector3 position, Vector3 aim)
        { advanceQueued = true; queuedDelta = dt; UpdateQueuedPose(position, aim); }

        // Resolve after every actor's Update has applied shield/release input, then let
        // the default-order plasma renderer upload the resulting path this same frame.
        void LateUpdate()
        {
            if (!owner || !owner.isActiveAndEnabled)
            { if (AimGuide) AimGuide.Hide(); return; }
            if (owner.temporarilyEliminated && AimGuide) AimGuide.Hide();
            if (!advanceQueued) return;
            advanceQueued = false;
            if (!owner || owner.IsMatchInputLocked || owner.IsSingularityTransitControlled) return;
            if (owner.IsStunned || owner.IsExternallyStunned || owner.temporarilyEliminated) Stop();
            if (EnergyDriver != null) EnergyDriver.AdvanceBeamFrame(queuedDelta, queuedOrigin, queuedAim);
            else Advance(queuedDelta, queuedOrigin, queuedAim);
            if (EnergyDriver != null && !IsFiring && !owner.IsSpawning)
                AimGuide.SetRay(queuedOrigin, queuedAim, TraceAimGuide(queuedOrigin, queuedAim), PlayerScaleAdjuster.SizeOf(owner));
            else AimGuide.Hide();
        }

        public void Initialize(PlayerControllerScript shooter, ParticleAcceleratorPowerUpDefinition settings)
        {
            owner = shooter; definition = settings;
            if (owner.SimulationRoot) transform.SetParent(owner.SimulationRoot, true);
            if (!owner.IsPseudoPlayer)
                foreach (var bounds in FindObjectsByType<ArenaBoundsFromVectorGrid>(FindObjectsSortMode.None))
                    if (bounds.gameObject.scene == owner.gameObject.scene && bounds.IsValid && bounds.ContainsWorldPoint(owner.transform.position))
                    { arena = bounds; break; }
            beamVisual.SetAimTrail(trail);
            AimGuide = GetComponent<ParticleAcceleratorAimGuide>();
            if (!AimGuide) AimGuide = gameObject.AddComponent<ParticleAcceleratorAimGuide>();
            Clear();
        }

        void Pose(Vector3 position, Vector3 aim)
        {
            origin = position; direction = aim; direction.y = 0;
            direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.right;
            float nextSize = PlayerScaleAdjuster.SizeOf(owner);
            float ratio = nextSize / Mathf.Max(.001f, size);
            ScalePlasma(corePlasma, ratio); ScalePlasma(contactPlasma, ratio);
            size = nextSize;
            beamVisual.SetSpatialScale(size);
        }

        static void ScalePlasma(ParticleBeamPlasmaContact plasma, float ratio)
        {
            plasma.dropRadius *= ratio; plasma.contactRadius *= ratio;
            plasma.splashSpeed *= ratio; plasma.dripAcceleration *= ratio;
        }

        public void Charge(float dt, Vector3 position, Vector3 aim, float progress)
        {
            Pose(position, aim);
            beamVisual.gameObject.SetActive(false);
            corePlasma.SetContact(origin + direction * corePlasmaForwardOffset * size, direction, .35f * Mathf.Clamp01(progress));
            contactPlasma.StopEmission(); AdvancePlasma(dt);
        }

        public void Begin(Vector3 position, Vector3 aim)
        {
            if (AimGuide) AimGuide.Hide();
            Pose(position, aim); trail.Reset(direction);
            age = fadeAge = reachProgress = 0; BeamLength = 0; previousContact = null;
            IsFiring = true; fading = false;
        }

        public void Stop()
        {
            if (IsFiring) { fading = true; fadeAge = 0; retractLength = BeamLength; }
            IsFiring = false; Contact = previousContact = null;
            corePlasma.StopEmission(); contactPlasma.StopEmission();
        }

        public void Advance(float dt, Vector3 position, Vector3 aim)
        {
            if (dt <= 0 || !definition || !owner) return;
            if (IsFiring || fading)
            {
                // Freeze the outgoing path during release; retract it without further damage.
                if (IsFiring) Pose(position, aim);
                age += dt;
                if (fading) fadeAge += dt;
                float rise = Mathf.SmoothStep(0, 1, age / Mathf.Max(.01f, definition.beamGrowSeconds));
                float fade = fading ? 1f - Mathf.SmoothStep(0, 1, fadeAge / Mathf.Max(.01f, definition.beamFadeSeconds)) : 1f;
                float opening = Mathf.Lerp(definition.openingRadiusMultiplier, 1f,
                    Mathf.SmoothStep(0, 1, age / Mathf.Max(.01f, definition.openingSettleSeconds)));
                radius = Mathf.Max(.001f, definition.beamRadius) * size * rise * fade * opening;
                float maxReach = Mathf.Max(.01f, definition.maxDistance) * PlayerScaleAdjuster.ProjectileReachOf(owner);
                referenceLength = maxReach * rise;
                if (IsFiring) trail.Advance(dt, direction, referenceLength, definition.turnPropagationSpeed, definition.maxTurnDelay, definition.maxBendAngle);
                float traceLimit = referenceLength;
                if (IsFiring)
                {
                    reachProgress = Mathf.Min(1f, reachProgress + dt / Mathf.Max(.01f, definition.beamGrowSeconds));
                    traceLimit = Mathf.Min(traceLimit, maxReach * Mathf.SmoothStep(0, 1, reachProgress));
                }
                float fullLength = Trace(traceLimit, out var contact);
                if (IsFiring && traceLimit > .02f && radius > .00001f && fullLength < traceLimit)
                {
                    // Invert the same smoothstep used for initial growth. Keep
                    // growth at the visible tip while blocked, then resume it
                    // with Beam Grow Seconds when the path opens up again.
                    float fraction = Mathf.Clamp01(fullLength / maxReach);
                    reachProgress = .5f - Mathf.Sin(Mathf.Asin(1f - 2f * fraction) / 3f);
                }
                Contact = IsFiring ? contact : null;
                BeamLength = IsFiring ? fullLength : Mathf.Min(fullLength, retractLength) * fade;
                Sample(BeamLength, out var endpoint, out _); BeamEnd = endpoint;
                beamVisual.gameObject.SetActive(BeamLength > .02f);
                beamVisual.SetClippedPath(origin, direction, referenceLength, BeamLength, radius, age);
                if (IsFiring)
                {
                    corePlasma.SetContact(origin + direction * corePlasmaForwardOffset * size, direction, rise);
                    if (fullLength + .001f < traceLimit)
                        contactPlasma.SetContact(tracePoint, traceNormal, rise);
                    else contactPlasma.StopEmission();
                    if (contact) ApplyContact(contact, dt * rise);
                    previousContact = contact;
                }
                if (fade <= 0) { fading = false; BeamLength = 0; beamVisual.gameObject.SetActive(false); }
            }
            AdvancePlasma(dt);
        }

        void AdvancePlasma(float dt) { corePlasma.Advance(dt); contactPlasma.Advance(dt); }

        void ApplyContact(Collider contact, float dt)
        {
            var core = contact.GetComponentInParent<AmplifierCoreGameplay>();
            if (core)
            {
                core.TryApplyBeamPush(traceDirection, definition.amplifierPushForce, dt);
                return;
            }
            var victim = contact.GetComponentInParent<PlayerControllerScript>();
            if (victim)
            {
                float amount = Mathf.Max(0, definition.playerDamagePerSecond) * dt;
                bool shielded = contact.CompareTag("Shield");
                if (shielded)
                {
                    var shield = victim.GetComponent<PlayerShieldAbility>();
                    if (shield && shield.IsActive)
                    {
                        // Resolve one parry when entering a shield, not once per damage tick.
                        if (previousContact != contact && shield.TryParryProjectile(owner, tracePoint)) Stop();
                        amount *= shield.BlocksAllDamage ? 0 : 1f - shield.CurrentStrength01;
                    }
                    else amount = 0;
                }
                if (previousContact != contact) PlayerContact?.Invoke(victim, shielded);
                if (amount > 0 && !victim.IsSpawning && !victim.IsInvulnerable)
                {
                    var hit = victim.ApplyExternalMassDelta(-amount, owner.gameObject, allowDeath: true);
                    if (hit.accepted && definition.transferMassToShooter)
                        owner.ApplyExternalMassDelta(hit.massLost01, allowDeath: false);
                }
            }
            else
            {
                var enemy = contact.GetComponentInParent<EnemyBase>();
                if (enemy) enemy.TakeDamage(Mathf.Max(0, definition.enemyDamagePerSecond) * dt, EnemyDamageSource.Projectile, owner);
            }
        }

        public void Clear()
        {
            if (AimGuide) AimGuide.Hide();
            advanceQueued = false; IsFiring = fading = false; BeamLength = 0; Contact = previousContact = null;
            if (beamVisual) beamVisual.gameObject.SetActive(false);
            if (corePlasma) corePlasma.Clear(); if (contactPlasma) contactPlasma.Clear();
        }
        void OnDisable() { Clear(); }
    }
}
