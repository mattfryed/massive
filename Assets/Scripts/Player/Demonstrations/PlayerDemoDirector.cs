using System;
using System.Collections;
using Massive.Player;
using Massive.PowerUps;
using UnityEngine;

namespace Massive.Demonstrations
{
    /// <summary>Owns a complete, repeatable demonstration. Every action goes through PlayerInputFrame.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDemoDirector : MonoBehaviour
    {
        [SerializeField] private PlayerDemoScenario scenario;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool loop = true;
        [Header("Optional presentation (never gates simulation)")]
        [SerializeField] private Camera presentationCamera;
        [SerializeField] private VectorGridGPU demoGrid;
        [SerializeField] private GameObject attackButton;
        [SerializeField] private GameObject shieldButton;
        [SerializeField] private Transform joystick;

        public PlayerDemoScenario Scenario => scenario;
        public VectorGridGPU Grid => demoGrid;
        public Camera PresentationCamera => presentationCamera;
        public int ConfirmedKills { get; private set; }
        public int ConfirmedRespawns { get; private set; }
        public PlayerControllerScript Primary => primary;
        public PlayerControllerScript Partner => partner;
        public string Phase { get; private set; } = "Stopped";
        public string LastFailure { get; private set; }
        public int SuccessfulLoops { get; private set; }
        public int FailedLoops { get; private set; }
        public int ConfirmedBlocks { get; private set; }
        public int ConfirmedShots { get; private set; }
        public int ConfirmedClaims { get; private set; }
        public int LastComboStage { get; private set; } = -1;
        public int ConfirmedHits { get; private set; }
        public int ConfirmedParries { get; private set; }
        public int ConfirmedLateBlocks { get; private set; }
        public int ConfirmedDecoherenceParries { get; private set; }
        public int ConfirmedProjectileContacts { get; private set; }
        public float LastContactShieldAge { get; private set; }
        public string LastOutcome { get; private set; } = "Ready";
        public bool IsRunning => routine != null;
        public event Action<PlayerDemoDirector> LoopCompleted;
        private GameObject session;
        private PlayerControllerScript primary, partner;
        private bool failed, blocked, shot, receivedHit, projectileContact;
        private float shieldRaisedAt;
        private Coroutine routine;
        private Vector3 joystickHome;
        private float attackCueUntil, shieldCueUntil;
        private Vector3 primaryHome, partnerHome;
        private float pickupGap;
        private bool targetDied, targetRespawned;
        private PowerUpPickup activePickup;

        private void Awake() { if (joystick) joystickHome = joystick.localPosition; }
        private void OnEnable() { if (playOnEnable) Play(); }
        private void OnDisable() { Stop(); }
        public void Play()
        {
            Stop();
            failed = false; LastFailure = null;
            if (!scenario || !scenario.playerPrefab) { Fail("Missing scenario or canonical Player prefab."); return; }
            var template = scenario.playerPrefab.GetComponent<PlayerControllerScript>();
            if (!template || !template.attackController || !template.powerUps || !template.GetComponent<PlayerShieldAbility>())
            { Fail("Player prefab is missing required gameplay components/references."); return; }
            if (presentationCamera) presentationCamera.enabled = false;
            routine = StartCoroutine(Run());
        }
        public void Stop()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            Cleanup(); Phase = "Stopped";
            attackCueUntil = shieldCueUntil = 0f;
            if (attackButton) attackButton.SetActive(false);
            if (shieldButton) shieldButton.SetActive(false);
            if (joystick) joystick.localPosition = joystickHome;
        }
        private void LateUpdate()
        {
            if (attackButton) attackButton.SetActive(Time.time < attackCueUntil);
            if (shieldButton) shieldButton.SetActive(Time.time < shieldCueUntil);
            if (joystick && primary) joystick.localPosition = joystickHome + new Vector3(primary.moveHorizontal, 0f, primary.moveVertical) * .25f;
        }

        private IEnumerator Run()
        {
            Phase = "Preparing";
            session = new GameObject("Demo session");
            session.transform.SetParent(transform, false);
            session.SetActive(false);
            primaryHome = transform.position;
            primary = CreateActor(0, 1, Vector3.zero);
            var stage = primary.attackController.Profile.GetStage(0);
            float travel = stage.TravelDistance * PlayerScaleAdjuster.ActionReachOf(primary);
            pickupGap = PlayerScaleAdjuster.BodyRadiusOf(primary) + travel * .5f + .3f;
            if (scenario.kind == PlayerDemoKind.Block || scenario.kind == PlayerDemoKind.CombatPair || scenario.powerUp is ParticleAcceleratorPowerUpDefinition)
            {
                bool meleePair = scenario.kind == PlayerDemoKind.Block ||
                    (scenario.kind == PlayerDemoKind.CombatPair && !(scenario.powerUp is ParticleAcceleratorPowerUpDefinition));
                float gap = meleePair ? PlayerScaleAdjuster.BodyRadiusOf(primary) * 2f + travel * .45f :
                    (scenario.kind == PlayerDemoKind.CombatPair ? scenario.acceleratorTargetDistance : pickupGap + scenario.acceleratorTargetDistance);
                partnerHome = transform.position + Vector3.right * gap;
                partner = CreateActor(2, 2, Vector3.right * gap);
                partner.DeathStarted += p => { targetDied = true; ConfirmedKills++; };
                partner.RespawnCompleted += p => { targetRespawned = true; ConfirmedRespawns++; };
                partner.HitAccepted += hit => { receivedHit = true; ConfirmedHits++; };
                if (scenario.kind == PlayerDemoKind.CombatPair)
                {
                    primaryHome = transform.position - Vector3.right * gap * .5f;
                    partnerHome = transform.position + Vector3.right * gap * .5f;
                    primary.transform.localPosition = Vector3.left * gap * .5f;
                    partner.transform.localPosition = Vector3.right * gap * .5f;
                }
            }
            if (scenario.kind == PlayerDemoKind.SoloCombo)
            {
                // Center the solo attack's travel within its gallery lane.
                float comboTravel = 0f;
                foreach (var comboStage in primary.attackController.Profile.Stages) comboTravel += comboStage.TravelDistance;
                primaryHome = transform.position - Vector3.right * (comboTravel * PlayerScaleAdjuster.ActionReachOf(primary) * .5f);
                primary.transform.position = primaryHome;
            }
            session.SetActive(true);
            yield return null;
            yield return null;
            SetFrame(primary, Vector2.zero, Vector3.right);
            if (partner) SetFrame(partner, Vector2.zero, Vector3.left);
            if (scenario.kind == PlayerDemoKind.CombatPair)
            {
                if (scenario.powerUp) primary.powerUps.Equip(scenario.powerUp, float.PositiveInfinity);
                if (scenario.defense == CombatDemoDefense.DecoherenceParry)
                {
                    if (!scenario.defenderPowerUp) { Fail("Missing Decoherence definition."); yield break; }
                    partner.powerUps.Equip(scenario.defenderPowerUp, float.PositiveInfinity);
                }
            }
            FrameStage();
            if (presentationCamera) presentationCamera.enabled = true;
            do
            {
                failed = blocked = shot = receivedHit = projectileContact = false;
                targetDied = targetRespawned = false;
                LastComboStage = -1;
                if (scenario.kind == PlayerDemoKind.CombatPair) yield return CombatPairDemo();
                else if (scenario.kind == PlayerDemoKind.Block) yield return BlockDemo();
                else if (scenario.kind == PlayerDemoKind.PowerUp) yield return PowerUpDemo();
                else yield return ComboDemo();
                Neutral();
                if (failed) break; // Keep the scene intact for diagnosis; never hide a failure with a reset.
                yield return new WaitForSeconds(scenario.readablePause);
                Phase = "Returning";
                if (scenario.kind == PlayerDemoKind.CombatPair)
                {
                    yield return Ready(primary); yield return Ready(partner);
                    if (failed) break;
                    if (primary.transform.position.x > partner.transform.position.x)
                    {
                        // Decoherence sends the attacker through: walk around the defender on the way home.
                        float bypass = PlayerScaleAdjuster.BodyRadiusOf(primary) + PlayerScaleAdjuster.BodyRadiusOf(partner) + .4f;
                        yield return MoveActorTo(primary, primary.transform.position + Vector3.forward * bypass, Vector3.right);
                        yield return MoveActorTo(primary, primaryHome + Vector3.forward * bypass, Vector3.right);
                    }
                }
                yield return MoveActorTo(primary, primaryHome, Vector3.right);
                if (partner && !partner.temporarilyEliminated)
                    yield return MoveActorTo(partner, partnerHome, Vector3.left);
                if (failed) break;
                SuccessfulLoops++; Phase = "Complete"; LoopCompleted?.Invoke(this);
                yield return new WaitForSeconds(.35f);
            } while (loop);
            Neutral(); routine = null;
            if (!failed) Phase = "Stopped";
        }

        private PlayerControllerScript CreateActor(int slot, int team, Vector3 localPosition)
        {
            var go = Instantiate(scenario.playerPrefab, session.transform);
            go.name = "Demo P" + (slot + 1);
            // Initial placement happens while inactive. Subsequent loops only submit movement input.
            go.transform.localPosition = localPosition;
            var actor = go.GetComponent<PlayerControllerScript>();
            actor.ConfigureDemonstration(session.transform, slot, team);
            var scale = go.GetComponent<PlayerScaleAdjuster>();
            if (scale) scale.ApplyScale();
            foreach (var interactor in go.GetComponentsInChildren<GridInteractor>(true)) interactor.grid = demoGrid;
            foreach (var pulse in go.GetComponentsInChildren<PlayerRepulsorGridPulse>(true)) pulse.BindGrid(demoGrid);
            actor.InputApplied += OnInput;
            actor.attackController.OnStageStarted.AddListener(stage =>
            {
                LastComboStage = actor.attackController.CurrentStageIndex;
                if (scenario.kind == PlayerDemoKind.SoloCombo)
                    Phase = stage.StageType == AttackStageType.PrimaryLunge ? "Thrust" :
                        stage.StageType == AttackStageType.ComboSwipe ? "Sweep" : "Repulsor";
            });
            foreach (var melee in go.GetComponentsInChildren<PlayerMelee>(true)) melee.ShieldContact += OnBlock;
            actor.powerUps.ProjectileFired += OnShot;
            return actor;
        }

        private void FrameStage()
        {
            if (presentationCamera && presentationCamera.TryGetComponent<PlayerDemoView>(out var view)) view.RefreshRect();
            if (scenario.kind == PlayerDemoKind.MovementAndCombo)
            {
                var profile = primary.attackController.Profile;
                float travel = 0f;
                foreach (var stage in profile.Stages) travel += stage.TravelDistance * PlayerScaleAdjuster.ActionReachOf(primary);
                float radius = profile.GetStage(profile.Stages.Count - 1).GetRepulsorRadius(PlayerScaleAdjuster.SizeOf(primary), PlayerScaleAdjuster.BodyRadiusOf(primary));
                float left = -scenario.movementDistance - 1f, right = travel + radius + 1f;
                FrameView(Vector3.right * ((left + right) * .5f), right - left, radius * 2f + 1f);
            }
            else if (scenario.kind == PlayerDemoKind.Block)
                FrameView((partnerHome - primaryHome) * .5f, Vector3.Distance(primaryHome, partnerHome) + 5f, 3.4f);
            else if (scenario.powerUp is ParticleAcceleratorPowerUpDefinition accelerator)
            {
                // Include the full charge telegraph, muzzle, recoil and impact; the camera stays still all loop.
                float distance = Mathf.Lerp(accelerator.minDistanceOnTap, accelerator.maxDistance, scenario.acceleratorChargeFraction) * PlayerScaleAdjuster.ProjectileReachOf(primary);
                float right = Mathf.Max(pickupGap + distance + 1.5f, partnerHome.x - primaryHome.x + 2f);
                FrameView(Vector3.right * ((right - 2f) * .5f), right + 2f, 4.4f);
            }
            else if (scenario.powerUp is DecoherencePowerUpDefinition)
                FrameView(Vector3.right * pickupGap * .5f, pickupGap + 3.6f, 4.6f);
            else
                FrameView(Vector3.right * pickupGap * .25f, Mathf.Max(pickupGap + 2.4f, scenario.movementDistance * 2f + 2.4f), 3.4f);
        }
        private void OnInput(PlayerInputFrame input)
        {
            if (input.attackDown || input.attackHeld) attackCueUntil = Time.time + .12f;
            if (input.shieldDown) shieldCueUntil = Time.time + .12f;
        }
        private void OnBlock(PlayerControllerScript defender)
        {
            if (defender != partner) return;
            if (!blocked) ConfirmedBlocks++;
            LastContactShieldAge = Time.time - shieldRaisedAt;
            blocked = true;
        }
        private void OnShot(GameObject projectile)
        {
            shot = true; ConfirmedShots++;
            if (projectile.TryGetComponent<ParticleAcceleratorProjectile>(out var beam))
                beam.PlayerContact += (victim, shielded) =>
                {
                    if (victim != partner) return;
                    projectileContact = true; ConfirmedProjectileContacts++;
                    if (shielded) OnBlock(victim);
                };
        }
        private void Cleanup()
        {
            Neutral();
            // Deactivation stops physics and callbacks immediately; Destroy finishes at frame end.
            if (primary && primary.powerUps) primary.powerUps.Clear();
            if (partner && partner.powerUps) partner.powerUps.Clear();
            if (session) { session.SetActive(false); Destroy(session); }
            session = null; primary = partner = null; activePickup = null;
        }
        private void Neutral()
        {
            if (primary) primary.ClearScriptedInput();
            if (partner) partner.ClearScriptedInput();
        }
        private void Fail(string message)
        {
            if (failed) return;
            failed = true; FailedLoops++; LastFailure = message; Phase = "Failed";
            Debug.LogWarning("[Player demonstration] " + name + ": " + message, this);
        }
        private IEnumerator Until(Func<bool> condition, string failure, float timeout = -1f)
        {
            float end = Time.time + (timeout > 0f ? timeout : scenario.actionTimeout);
            while (!condition() && Time.time < end) yield return null;
            if (!condition()) Fail(failure);
        }
        private void SetFrame(PlayerControllerScript actor, Vector2 move, Vector3 aim, bool down = false, bool held = false, bool up = false, bool shield = false, bool holdShield = false)
        {
            actor.SetScriptedInput(new PlayerInputFrame { moveInput = move, hasAimDirWS = true, aimDirWS = aim,
                attackDown = down, attackHeld = held, attackUp = up, shieldDown = shield, shieldHeld = shield || holdShield });
        }
        private IEnumerator Attack(PlayerControllerScript actor, Vector3 direction, float hold = 0f)
        {
            SetFrame(actor, Vector2.zero, direction, down: true, held: true);
            yield return null;
            float end = Time.time + hold;
            while (Time.time < end) { SetFrame(actor, Vector2.zero, direction, held: true); yield return null; }
            SetFrame(actor, Vector2.zero, direction, up: true);
            yield return null;
            SetFrame(actor, Vector2.zero, direction);
        }
        private IEnumerator Shield(PlayerControllerScript actor, Vector3 direction)
        {
            SetFrame(actor, Vector2.zero, direction, shield: true);
            yield return null;
            SetFrame(actor, Vector2.zero, direction);
            yield return Until(() => actor.GetComponent<PlayerShieldAbility>().IsActive, "Shield input was not accepted.");
        }
        private IEnumerator Ready(PlayerControllerScript actor)
        {
            yield return Until(() => !actor.IsStunned && !actor.IsExternallyStunned && !actor.temporarilyEliminated &&
                !actor.attackController.IsAttacking && actor.attackController.CooldownRemaining <= 0f,
                "Player did not become ready for an attack.");
        }
        private IEnumerator MoveTo(Vector3 target)
        {
            yield return MoveActorTo(primary, target, primary.AimDirectionWS);
        }
        private IEnumerator MoveActorTo(PlayerControllerScript actor, Vector3 target, Vector3 finalAim)
        {
            var body = actor.GetComponent<Rigidbody>();
            float end = Time.time + scenario.actionTimeout;
            while (Time.time < end)
            {
                Vector3 delta = target - body.position; delta.y = 0f;
                Vector3 velocity = body.linearVelocity; velocity.y = 0f;
                if (delta.magnitude <= .1f && velocity.magnitude <= .15f) break;
                if (actor.temporarilyEliminated || actor.IsStunned || actor.IsExternallyStunned)
                    SetFrame(actor, Vector2.zero, finalAim);
                else
                {
                    // Approach using ordinary propulsion/braking. No transform, velocity or mass reset.
                    Vector2 stick = delta.magnitude < .1f ? Vector2.zero : new Vector2(delta.x, delta.z).normalized * Mathf.Clamp01(delta.magnitude);
                    SetFrame(actor, stick, delta.sqrMagnitude > .01f ? delta : finalAim);
                }
                yield return null;
            }
            SetFrame(actor, Vector2.zero, finalAim);
            if (Vector3.Distance(body.position, target) > .2f) Fail("Player could not steer back to its marker.");
        }
        private IEnumerator ComboDemo()
        {
            var profile = primary.attackController.Profile;
            if (!profile || profile.Stages.Count == 0) { Fail("Player has no attack profile."); yield break; }
            if (scenario.kind == PlayerDemoKind.MovementAndCombo)
            {
                yield return MoveTo(transform.position + Vector3.left * scenario.movementDistance);
                if (failed) yield break;
                yield return MoveTo(transform.position);
                if (failed) yield break;
            }
            Phase = "Thrust / Sweep / Repulsor";
            yield return Ready(primary); if (failed) yield break;
            yield return Attack(primary, Vector3.right);
            yield return Until(() => primary.attackController.IsAttacking, "Thrust input was not accepted.");
            for (int index = 0; index < profile.Stages.Count - 1 && !failed; index++)
            {
                int current = index;
                yield return Until(() => primary.attackController.CurrentStageIndex == current &&
                    primary.attackController.TimeInStage >= primary.attackController.ComboWindowSeconds(profile.GetStage(current)).x,
                    "Combo engagement window was not reached.");
                if (failed) yield break;
                yield return Attack(primary, Vector3.right);
                int next = index + 1;
                yield return Until(() => primary.attackController.CurrentStageIndex == next, "Next combo stage did not start.");
            }
            if (!failed) yield return Until(() => !primary.attackController.IsAttacking, "Combo did not complete.");
        }
        private IEnumerator BlockDemo()
        {
            Phase = "Preparing block";
            var stage = primary.attackController.Profile.GetStage(0);
            yield return Ready(partner); if (failed) yield break;
            yield return Ready(primary); if (failed) yield break;
            yield return Until(() => partner.GetComponent<PlayerShieldAbility>().CooldownRemaining <= 0f && partner.GetComponent<PlayerShieldAbility>().ActivationCooldownRemaining <= 0f, "Shield did not recharge.");
            if (failed) yield break;
            float hitStart = stage.ActivationStartNormalized * stage.Duration;
            bool shieldFirst = hitStart < partner.GetComponent<PlayerShieldAbility>().DurationSeconds * .5f;
            if (shieldFirst) yield return Shield(partner, Vector3.left);
            if (failed) yield break;
            Phase = "Blocking";
            yield return Attack(primary, Vector3.right);
            if (!shieldFirst)
            {
                yield return Until(() => primary.attackController.IsAttacking && primary.attackController.TimeInStage >= hitStart - partner.GetComponent<PlayerShieldAbility>().DurationSeconds * .35f, "Attack windup did not start.");
                if (!failed) yield return Shield(partner, Vector3.left);
            }
            if (!failed) yield return Until(() => blocked, "Attack did not make shield contact.");
        }
        private IEnumerator CombatPairDemo()
        {
            var shield = partner.GetComponent<PlayerShieldAbility>();
            var accelerator = scenario.powerUp as ParticleAcceleratorPowerUpDefinition;
            bool heldBlock = scenario.defense == CombatDemoDefense.HeldBlock;
            bool undefended = scenario.defense == CombatDemoDefense.None;
            bool decoherence = scenario.defense == CombatDemoDefense.DecoherenceParry;
            Phase = "Recharging";
            yield return Ready(primary); if (failed) yield break;
            yield return Ready(partner); if (failed) yield break;
            yield return Until(() => !shield.IsActive && shield.CooldownRemaining <= 0f && shield.ActivationCooldownRemaining <= 0f &&
                primary.powerUps.CooldownRemaining <= 0f, "Actions did not recharge.");
            if (failed) yield break;
            float massBefore = partner.massScore;

            if (heldBlock)
            {
                Phase = "Holding / parry window ending";
                shieldRaisedAt = Time.time;
                SetFrame(partner, Vector2.zero, Vector3.left, shield: true, holdShield: true);
                yield return null;
                SetFrame(partner, Vector2.zero, Vector3.left, holdShield: true);
                yield return new WaitForSeconds(shield.DurationSeconds + scenario.heldBlockLead);
            }
            if (accelerator)
            {
                Phase = "Charging shot";
                yield return Attack(primary, Vector3.right, accelerator.chargeToMaxSeconds * scenario.acceleratorChargeFraction);
                if (!heldBlock && !undefended)
                {
                    shieldRaisedAt = Time.time;
                    yield return Shield(partner, Vector3.left);
                }
                Phase = heldBlock ? "Projectile / held block" : "Projectile / parry";
                yield return Until(() => projectileContact, "Projectile did not contact its intended defender.");
            }
            else
            {
                if (!heldBlock && !undefended)
                {
                    shieldRaisedAt = Time.time;
                    yield return Shield(partner, Vector3.left);
                }
                Phase = undefended ? "Attack / exposed player" : heldBlock ? "Attack / held block" : "Attack / parry";
                yield return Attack(primary, Vector3.right);
                yield return Until(() => undefended ? receivedHit : blocked, "Melee did not reach its intended defender.");
            }
            if (failed) yield break;
            if (undefended)
            {
                if (!receivedHit || partner.massScore >= massBefore) { Fail("Exposed defender did not take damage."); yield break; }
                LastOutcome = "Damage landed";
            }
            else if (heldBlock)
            {
                if (shield.IsParryWindow || !shield.IsHolding || primary.IsStunned || primary.IsExternallyStunned || partner.massScore < massBefore)
                { Fail("Expected block-only contact after the parry window."); yield break; }
                ConfirmedLateBlocks++; LastOutcome = "Blocked / no stun";
                yield return Until(() => !shield.IsActive, "Held shield did not expire.");
            }
            else
            {
                yield return Until(() => decoherence && !accelerator ? primary.IsExternallyStunned : primary.IsStunned,
                    "Contact did not resolve the expected parry.");
                if (failed) yield break;
                if (partner.massScore < massBefore) { Fail("Fully charged parry leaked damage."); yield break; }
                ConfirmedParries++;
                if (decoherence) ConfirmedDecoherenceParries++;
                LastOutcome = decoherence ? "Decoherence parry" : "Parried / attacker stunned";
            }
            Phase = LastOutcome;
        }

        private IEnumerator ClaimPowerUp()
        {
            var def = scenario.powerUp;
            if (primary.powerUps.HasActive && primary.powerUps.ActiveDefinition == def) yield break;
            Phase = "Claiming " + def.displayName;
            if (!activePickup)
            {
                activePickup = PowerUpPickup.Spawn(def, primaryHome + Vector3.right * pickupGap, def.pickupPrefab.transform.rotation, session.transform);
                activePickup.Claimed += p => { if (p == primary) ConfirmedClaims++; };
            }
            yield return new WaitForSeconds(.6f);
            yield return Ready(primary); if (failed) yield break;
            yield return Attack(primary, Vector3.right);
            yield return Until(() => primary.powerUps.HasActive && primary.powerUps.ActiveDefinition == def, "Melee did not claim the pickup.");
            if (!failed) yield return Ready(primary);
        }
        private IEnumerator PowerUpDemo()
        {
            var def = scenario.powerUp;
            if (!def || !def.pickupPrefab) { Fail("Missing power-up definition/pickup prefab."); yield break; }
            yield return ClaimPowerUp(); if (failed) yield break;
            Phase = "Using " + def.displayName;
            if (def is ParticleAcceleratorPowerUpDefinition accelerator)
            {
                // A real opponent at normal spawn mass. Repeat real shots until gameplay resolves a kill.
                int safetyLimit = Mathf.CeilToInt(partner.massScoreMax / Mathf.Max(.01f, Mathf.Lerp(accelerator.massRemovedMin, accelerator.massRemovedMax, scenario.acceleratorChargeFraction))) + 2;
                for (int attempt = 0; attempt < safetyLimit && !targetDied && !failed; attempt++)
                {
                    yield return MoveActorTo(primary, primaryHome + Vector3.right * pickupGap, Vector3.right);
                    if (failed) yield break;
                    yield return Until(() => primary.powerUps.CooldownRemaining <= 0f, "Accelerator did not recharge.");
                    if (failed) yield break;
                    // Long sequences can outlive a pickup. Return and claim a fresh one naturally.
                    if (!primary.powerUps.HasActive || primary.powerUps.RemainingSeconds < accelerator.chargeToMaxSeconds * scenario.acceleratorChargeFraction + accelerator.preFireTelegraphSeconds + .5f)
                    {
                        if (primary.powerUps.HasActive)
                            yield return Until(() => !primary.powerUps.HasActive, "Power-up did not expire.", primary.powerUps.RemainingSeconds + 2f);
                        yield return MoveActorTo(primary, primaryHome, Vector3.right);
                        yield return ClaimPowerUp(); if (failed) yield break;
                    }
                    int beforeShots = ConfirmedShots;
                    float beforeMass = partner.massScore;
                    yield return Attack(primary, (partner.transform.position - primary.transform.position).normalized, accelerator.chargeToMaxSeconds * scenario.acceleratorChargeFraction);
                    yield return Until(() => ConfirmedShots > beforeShots, "Accelerator did not fire after release.");
                    if (failed) yield break;
                    yield return Until(() => targetDied || partner.massScore < beforeMass, "Accelerator did not hit its opponent.");
                }
                if (!targetDied && !failed) Fail("Accelerator could not eliminate its opponent.");
                if (!failed)
                {
                    Phase = "Opponent respawning";
                    yield return MoveActorTo(primary, primaryHome, Vector3.right);
                    yield return Until(() => targetRespawned, "Opponent did not finish its normal respawn.");
                }
            }
            else if (def is TimeDilationPowerUpDefinition)
            {
                yield return MoveTo(primaryHome + Vector3.left * scenario.movementDistance);
                if (!failed) yield return MoveTo(primaryHome + Vector3.right * scenario.movementDistance);
            }
            else if (def is DecoherencePowerUpDefinition)
            {
                var shield = primary.GetComponent<PlayerShieldAbility>();
                yield return Until(() => shield.CooldownRemaining <= 0f && shield.ActivationCooldownRemaining <= 0f, "Shield did not recharge.");
                if (!failed) yield return Shield(primary, Vector3.right);
                if (!failed) yield return Until(() => !shield.IsActive, "Shield did not finish.");
            }
        }
        private void FrameView(Vector3 localCenter, float width, float height)
        {
            if (!presentationCamera) return;
            float aspect = presentationCamera.aspect;
            presentationCamera.transform.position = transform.position + localCenter + Vector3.up * 30f;
            presentationCamera.orthographicSize = Mathf.Max(height * .5f, width * .5f / Mathf.Max(.1f, aspect)) + scenario.viewPadding;
            if (demoGrid)
            {
                demoGrid.transform.position = transform.position + localCenter + Vector3.down * .08f;
                demoGrid.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                float fullHeight = presentationCamera.orthographicSize * 2f;
                demoGrid.size = new Vector2(fullHeight * aspect, fullHeight);
                demoGrid.gridScale = fullHeight / 12f;
            }
        }
    }
}
