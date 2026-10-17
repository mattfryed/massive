using System;
using System.Collections.Generic;
using Massive.Enemies;
using Massive.Player;
using Massive.Resonance;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Multiplier
{
    public sealed partial class AmplifierCoreGameplay
    {
        private struct ShotFlight
        {
            public float remaining, age, stillAge, drag;
            public bool playerLaunched;
        }
        private ShotFlight flight;
        private ResonanceInteractionDriver[] shotPatterns = Array.Empty<ResonanceInteractionDriver>();
        private ArenaBoundsFromVectorGrid shotArena;
        private Massive.Singularity.SingularityAmplifierAdapter shotSurface;
        private RaycastHit[] shotHits = new RaycastHit[128];
        private Collider[] shotOverlap = new Collider[128];
        private bool physicalShotActive;
        private readonly Dictionary<Collider, bool> shotIgnoredActors = new Dictionary<Collider, bool>();
        private readonly List<Collider> shotActorScratch = new List<Collider>(32);
        private sealed class HitDistance : IComparer<RaycastHit>
        {
            public static readonly HitDistance Instance = new HitDistance();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        private void RefreshShotWorld()
        {
            if (Time.unscaledTime < nextWorldRefresh) return;
            nextWorldRefresh = Time.unscaledTime + .25f;
            shotPatterns = FindObjectsByType<ResonanceInteractionDriver>(FindObjectsSortMode.None);
            if (!shotSurface) shotSurface = GetComponent<Massive.Singularity.SingularityAmplifierAdapter>();
            if (!shotArena)
                foreach (var candidate in FindObjectsByType<ArenaBoundsFromVectorGrid>(FindObjectsSortMode.None))
                    if (candidate.gameObject.scene == gameObject.scene) { shotArena = candidate; break; }
            if (IsLaunched) RefreshShotActorFilters();
        }

        private void BeginPhysicalShot(float range)
        {
            physicalShotActive = true;
            body.isKinematic = false;
            collisionShape.isTrigger = possessionWasTrigger;
            float speed = LimitPlanarSpeed(aimDirection * Mathf.Max(.1f, Possession.shotSpeed), Effective_maximumPlanarSpeed).magnitude;
            // Distance-driven shots include the stopping interval in their launch
            // drag. Legacy mode retains the independent body-drag floor.
            float drag = AmplifierCoreMotion.ShotDrag(speed, range, Time.fixedDeltaTime, Possession);
            flight = new ShotFlight { drag = drag, remaining = range, playerLaunched = true };
            body.linearDamping = AmplifierCoreMotion.CoastingDrag(drag, Effective_linearDrag, Possession);
            body.linearVelocity = aimDirection * speed;
            _incomingVelocity = body.linearVelocity;
            body.angularVelocity = Vector3.Cross(Vector3.up, body.linearVelocity) * (Mathf.Clamp01(Possession.rollingSpin) / CoreRadius);
            body.WakeUp();
            nextWorldRefresh = 0f;
            RefreshShotWorld();
        }

        private void EndPhysicalShot()
        {
            if (physicalShotActive && body) body.linearDamping = Mathf.Max(0f, Effective_linearDrag);
            physicalShotActive = false;
            foreach (var pair in shotIgnoredActors)
                if (collisionShape && pair.Key) Physics.IgnoreCollision(collisionShape, pair.Key, pair.Value);
            shotIgnoredActors.Clear();
        }

        internal void WakeForGoalField()
        {
            if (!UsesPossession || IsOccupied || IsCaptured || IsInExternalTransit || !isActiveAndEnabled || !body) return;
            EnsurePossessionPhysics();
            if (IsLaunched) return;
            ControlState = AmplifierControlState.Launched;
            physicalShotActive = true;
            flight = new ShotFlight();
            shotOwner = null; // A goal pulling a resting Core is not a player's damage-dealing shot.
            shotVictims.Clear(); shieldContacts.Clear();
            body.isKinematic = false;
            collisionShape.isTrigger = possessionWasTrigger;
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }

        private void RefreshShotActorFilters()
        {
            // Players remain solid, including the shooter once its exit animation
            // restores collisions. Only NPCs use the shot's swept damage/push response.
            foreach (var enemy in EnemyBase.ActiveEnemies)
                if (enemy && enemy.gameObject.scene == gameObject.scene) IgnoreShotActor(enemy);
        }

        private void IgnoreShotActor(Component actor)
        {
            actor.GetComponentsInChildren(true, shotActorScratch);
            foreach (var c in shotActorScratch)
            {
                if (!c || shotIgnoredActors.ContainsKey(c)) continue;
                shotIgnoredActors.Add(c, Physics.GetIgnoreCollision(collisionShape, c));
                Physics.IgnoreCollision(collisionShape, c, true);
            }
        }

        private void TickPossessionFlight()
        {
            if (!UsesPossession || IsCaptured || IsInExternalTransit) return;
            EnsurePossessionPhysics();
            if (!IsLaunched || !body || body.isKinematic) return;
            RefreshShotWorld();
            float dt = Time.fixedDeltaTime;
            flight.age += dt;
            // A lifetime safety brake also eases out. Never teleport or zero a
            // moving shot just because its estimated range/time has elapsed.
            body.linearDamping = AmplifierCoreMotion.Drag(flight.drag, Effective_linearDrag, flight.age, Possession);
            body.linearVelocity = LimitPlanarSpeed(body.linearVelocity, Effective_maximumPlanarSpeed);
            CheckShotActorContacts(dt);
            if (IsCaptured) return;
            _incomingVelocity = body.linearVelocity;
            float speed = new Vector2(_incomingVelocity.x, _incomingVelocity.z).magnitude;
            flight.remaining = speed / Mathf.Max(.01f, body.linearDamping);
            bool magnetic = false;
            foreach (var driver in shotPatterns)
                if (driver && driver.IsTurningCore(this)) { magnetic = true; break; }
            bool goalDriven = AmplifierGoalCapture.IsDrivingCore(this);
            if (AmplifierCoreMotion.Settle(speed, magnetic || goalDriven, dt, ref flight.stillAge, Possession))
            {
                bool playerShot = flight.playerLaunched;
                BecomeAvailable();
                if (playerShot) ShotSettled?.Invoke(this);
            }
        }

        /// <summary>General straight aiming direction. It deliberately makes no
        /// promise about collisions, fields, or a final stopping position.</summary>
        public int BuildTrajectory(List<Vector3> points)
        {
            points.Clear();
            if (!UsesPossession || !IsOccupied) return 0;
            // A stable guide lets the charge travel from its first dot to its
            // last; changing length during charge would move that goalpost.
            float range = Mathf.Max(.1f, Mathf.Max(Possession.minimumRange, Possession.maximumRange));
            float length = Mathf.Min(range, Mathf.Max(.5f, Possession.previewLength));
            // Samples keep the straight chart direction usable on SINGULARITY's folded surface.
            int segments = Mathf.Max(1, Mathf.CeilToInt(length / .2f));
            for (int i = 0; i <= segments; i++) points.Add(transform.position + aimDirection * (length * i / segments));
            return points.Count;
        }

        private void CheckShotActorContacts(float dt)
        {
            var physics = gameObject.scene.GetPhysicsScene();
            Vector3 velocity = body.linearVelocity;
            Vector3 direction = PlanarDirection(velocity, aimDirection);
            float distance = velocity.magnitude * dt;
            int count = physics.SphereCast(body.position, CoreRadius, direction, shotHits, distance, ~0, QueryTriggerInteraction.Collide);
            while (count == shotHits.Length && shotHits.Length < 4096)
            {
                Array.Resize(ref shotHits, shotHits.Length * 2);
                count = physics.SphereCast(body.position, CoreRadius, direction, shotHits, distance, ~0, QueryTriggerInteraction.Collide);
            }
            Array.Sort(shotHits, 0, count, HitDistance.Instance);
            for (int i = 0; i < count; i++)
            {
                var c = shotHits[i].collider;
                if (!c || c.transform.IsChildOf(transform) || Physics.GetIgnoreLayerCollision(gameObject.layer, c.gameObject.layer)) continue;
                var player = c.GetComponentInParent<PlayerControllerScript>();
                if (player)
                {
                    if (TryShieldShot(player, c)) break;
                    // A solid player stops this sweep just like a wall: do not
                    // damage an NPC behind a body that PhysX will collide with.
                    if (!player.IsInsideAmplifier && !c.isTrigger && !Physics.GetIgnoreCollision(collisionShape, c) &&
                        Mathf.Abs(shotHits[i].normal.y) < .5f) break;
                    continue;
                }
                var hurtbox = c.GetComponent<EnemyHurtbox>();
                if (hurtbox) { DamageShotEnemy(hurtbox, direction); continue; }
                if (c.GetComponentInParent<EnemyBase>()) continue;
                // A solid wall occludes damage. PhysX owns the actual rebound.
                if (!c.isTrigger && !Physics.GetIgnoreCollision(collisionShape, c) && Mathf.Abs(shotHits[i].normal.y) < .5f) break;
            }
            int overlaps = physics.OverlapSphere(body.position, CoreRadius, shotOverlap, ~0, QueryTriggerInteraction.Collide);
            while (overlaps == shotOverlap.Length && shotOverlap.Length < 4096)
            {
                Array.Resize(ref shotOverlap, shotOverlap.Length * 2);
                overlaps = physics.OverlapSphere(body.position, CoreRadius, shotOverlap, ~0, QueryTriggerInteraction.Collide);
            }
            for (int i = 0; i < overlaps; i++)
            {
                var c = shotOverlap[i];
                if (!c || Physics.GetIgnoreLayerCollision(gameObject.layer, c.gameObject.layer)) continue;
                var player = c.GetComponentInParent<PlayerControllerScript>();
                if (player) TryShieldShot(player, c);
                var hurtbox = c.GetComponent<EnemyHurtbox>();
                if (hurtbox) DamageShotEnemy(hurtbox, direction);
            }
            foreach (var player in PlayerControllerScript.ActivePlayers)
                if (player && shieldContacts.Contains(player) && (player.transform.position - body.position).sqrMagnitude > Mathf.Pow(CoreRadius + PlayerScaleAdjuster.BodyRadiusOf(player) + 2f, 2f)) shieldContacts.Remove(player);
        }

        private bool TryShieldShot(PlayerControllerScript player, Collider contact)
        {
            if (player.IsInsideAmplifier || player.temporarilyEliminated || shieldContacts.Contains(player)) return false;
            var shield = player.GetComponent<PlayerShieldAbility>();
            if (!shield || !shield.IsActive || !player.shield || !contact.transform.IsChildOf(player.shield.transform)) return false;
            Vector3 velocity = body.linearVelocity;
            if (shield.IsParryWindow)
            {
                shield.PlayCoreParryFeedback();
                Vector3 outward = PlanarDirection(body.position - player.transform.position, -velocity.normalized);
                Vector3 aim = PlanarDirection(player.AimDirectionWS, outward);
                if (Vector3.Dot(aim, outward) < .15f) aim = (aim - outward * Vector3.Dot(aim, outward) + outward * .35f).normalized;
                body.linearVelocity = aim * velocity.magnitude * Mathf.Clamp01(Possession.parryRangeRetention);
                body.angularVelocity = Vector3.Cross(Vector3.up, body.linearVelocity) * (Mathf.Clamp01(Possession.rollingSpin) / CoreRadius);
                shotOwner = player;
            }
            else if (shield.BlocksAllDamage)
            {
                body.linearVelocity = velocity * Mathf.Clamp01(Possession.blockSpeedRetention);
                float coastDrag = AmplifierCoreMotion.CoastingDrag(flight.drag, Effective_linearDrag, Possession);
                flight.drag = Mathf.Max(coastDrag, coastDrag * Mathf.Clamp01(Possession.blockSpeedRetention) / Mathf.Max(.01f, Possession.blockRangeRetention));
            }
            else return false;
            foreach (var driver in shotPatterns) if (driver) driver.ReleaseCoreTurn(this);
            shieldContacts.Add(player);
            flight.remaining = body.linearVelocity.magnitude / Mathf.Max(.01f, AmplifierCoreMotion.CoastingDrag(flight.drag, Effective_linearDrag, Possession));
            return true;
        }

        private void DamageShotEnemy(EnemyHurtbox hurtbox, Vector3 direction)
        {
            var enemy = hurtbox.Enemy;
            if (!hurtbox.isActiveAndEnabled || !enemy || enemy.IsDead || enemy.IsPaused || !shotOwner ||
                !enemy.SharesSimulationWith(shotOwner) || enemy.OwnerTeamId == shotOwner.teamID || !shotVictims.Add(enemy)) return;
            enemy.TakeDamage(hurtbox.SwordHitDamage * Mathf.Max(0f, Possession.npcDamageMultiplier), EnemyDamageSource.AmplifierShot, shotOwner);
            if (!enemy.IsDead) AmplifierShotRecoil.Apply(enemy, direction * Mathf.Max(0f, Possession.npcPushSpeed));
        }
    }
}
