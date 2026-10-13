using System.Collections.Generic;
using Massive.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Multiplier
{
    public sealed partial class AmplifierCoreGameplay
    {
        [SerializeField] private AmplifierPossessionSettings possession = new AmplifierPossessionSettings();
        public AmplifierPossessionSettings Possession => SharedTuning != null ? SharedTuning.possession : possession;
        public bool UsesPossession => !presentationOnly && Possession.mode == AmplifierInteractionMode.PossessionShot;
        public AmplifierControlState ControlState { get; private set; }
        public bool IsPossessionBusy => UsesPossession && ControlState != AmplifierControlState.Available;
        public bool IsOccupied => ControlState == AmplifierControlState.Aiming || ControlState == AmplifierControlState.Contested;
        public bool IsLaunched => ControlState == AmplifierControlState.Launched;
        public PlayerControllerScript Occupant => pilot != null ? pilot.player : null;
        public PlayerControllerScript Challenger => challenger != null ? challenger.player : null;
        public PlayerControllerScript ShotOwner => shotOwner;
        public float Charge01 => Mathf.Clamp01(chargeSeconds / Mathf.Max(.1f, Possession.fullChargeSeconds));
        public Vector3 AimDirection => aimDirection;
        public float LightControl01 => lightControl;
        public float ControlSecondsRemaining => Mathf.Max(0f, controlDeadline -
            (IsContestWinnerAiming ? Mathf.Max(Time.time, postContestInput.UnlockAt) : Time.time));
        public bool IsContestWinnerAiming => ControlState == AmplifierControlState.Aiming && contestWinnerAiming;
        public float PostContestGraceRemaining => IsContestWinnerAiming ? Mathf.Max(0f, postContestInput.UnlockAt - Time.time) : 0f;
        public bool PostContestInputProtected => IsContestWinnerAiming && !chargeStarted && !postContestInput.ReadyForPress;
        public float ShotRangeRemaining => flight.remaining;
        public Vector3 ShotVelocity => IsLaunched && body ? body.linearVelocity : Vector3.zero;

        private sealed class Occupancy
        {
            public PlayerControllerScript player;
            public Vector3 entryPosition;
            public PlayerInputFrame input;
            public bool lastWasAttack, hasPress, awaitNeutral;
            public float nextPress;
            public int inputFrame = -1;
        }
        private Occupancy pilot, challenger;
        private float chargeSeconds, controlDeadline, nextEntryTime, lightControl = .5f;
        private bool chargeStarted;
        private bool contestWinnerAiming;
        private AmplifierPostContestInputGate postContestInput;
        private Vector3 aimDirection = Vector3.right;
        private PlayerControllerScript shotOwner;
        private bool possessionPhysicsOwned, possessionWasKinematic, possessionWasTrigger;
        private readonly Collider[] placementHits = new Collider[128];
        private readonly HashSet<Massive.Enemies.EnemyBase> shotVictims = new HashSet<Massive.Enemies.EnemyBase>();
        private readonly HashSet<PlayerControllerScript> shieldContacts = new HashSet<PlayerControllerScript>();
        private float nextWorldRefresh;

        private void LateUpdate()
        {
            if (!Application.isPlaying || presentationOnly) return;
            if (!UsesPossession)
            {
                ResetPossession();
                return;
            }
            if (IsCaptured || IsInExternalTransit) return;
            EnsurePossessionPhysics();
            var score = Massive.Scoring.MatchScoreService.Instance;
            if (!DemonstrationRoot && score && !score.IsScoringOpen)
            {
                ResetPossession();
                return;
            }
            if (pilot != null && (!pilot.player || !pilot.player.CanRemainInsideAmplifier)) ReleaseOccupancy(ref pilot, 0f);
            if (challenger != null && (!challenger.player || !challenger.player.CanRemainInsideAmplifier)) ReleaseOccupancy(ref challenger, 0f);
            if (ControlState == AmplifierControlState.Contested && (pilot == null || challenger == null))
            {
                pilot = pilot ?? challenger; challenger = null;
                if (pilot != null) BeginWinnerAim(); else BecomeAvailable();
            }
            if (ControlState == AmplifierControlState.Aiming)
            {
                if (pilot == null) { BecomeAvailable(); return; }
                if (Time.time >= controlDeadline) { ReleaseOccupancy(ref pilot, 0f); BecomeAvailable(); return; }
                TickAim();
            }
            else if (ControlState == AmplifierControlState.Contested) TickContest();
            if (visual && IsOccupied) visual.SetExcitement(ControlState == AmplifierControlState.Contested ? 1f : Charge01);
            if (visual)
            {
                visual.SetChargeFeedback(Charge01, ControlState == AmplifierControlState.Aiming && chargeStarted, Possession);
                float light = 0f, dark = 0f;
                if (IsOccupied)
                {
                    AddPossessionMarker(pilot, ref light, ref dark);
                    AddPossessionMarker(challenger, ref light, ref dark);
                }
                visual.SetPossessionMarkers(light, dark, ControlState == AmplifierControlState.Contested,
                    Possession.playerMarkerScale, Possession.playerMarkerOutline,
                    Possession.markerFlowDeflection, Possession.markerClearance);
            }
            UpdatePossessionPresentation();
        }

        private void AddPossessionMarker(Occupancy seat, ref float light, ref float dark)
        {
            if (seat == null || !seat.player) return;
            float scale = Possession.playerMarkerScale * Mathf.SmoothStep(0f, 1f, seat.player.AmplifierEntryProgress);
            if (seat.player.teamID == 1) light = scale; else dark = scale;
        }

        private void EnsurePossessionPhysics()
        {
            if (!body || !collisionShape || possessionPhysicsOwned) return;
            possessionWasKinematic = body.isKinematic;
            possessionWasTrigger = collisionShape.isTrigger;
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = true;
            possessionPhysicsOwned = true;
        }

        private bool TryEnterFromThrust(PlayerAttackController attack)
        {
            if (!UsesPossession || IsCaptured || IsInExternalTransit || !isActiveAndEnabled ||
                IsLaunched || ControlState == AmplifierControlState.Contested || Time.time < nextEntryTime ||
                !attack || !attack.isActiveAndEnabled || !attack.IsAttacking || attack.CurrentStage == null ||
                attack.CurrentStage.StageType != AttackStageType.PrimaryLunge ||
                attack.StageNormalizedTime < attack.CurrentStage.ActivationStartNormalized ||
                attack.StageNormalizedTime > attack.CurrentStage.ActivationEndNormalized) return false;
            var player = attack.GetComponentInParent<PlayerControllerScript>();
            if (!player || (DemonstrationRoot && player.SimulationRoot != DemonstrationRoot) || !player.CanEnterAmplifier || player.gameObject.scene != gameObject.scene ||
                (pilot != null && pilot.player && pilot.player.teamID == player.teamID)) return false;
            // This method is also called by weapon contacts: only the body reaching the
            // Core admits a rider, never the far end of the Thrust's projected effect.
            Vector3 delta = player.transform.position - transform.position; delta.y = 0f;
            if (delta.magnitude > CoreRadius + PlayerScaleAdjuster.BodyRadiusOf(player) + .2f) return false;
            var entrant = new Occupancy { player = player, entryPosition = player.transform.position };
            if (!player.EnterAmplifier(this)) return false;
            EnsurePossessionPhysics();
            if (pilot == null)
            {
                contestWinnerAiming = false;
                postContestInput = default;
                pilot = entrant;
                aimDirection = PlanarDirection(player.AimDirectionWS, Vector3.right);
                ControlState = AmplifierControlState.Aiming;
                controlDeadline = Time.time + Mathf.Max(.25f, Possession.possessionSeconds) + EntrySeconds;
                chargeSeconds = 0f; chargeStarted = false;
            }
            else
            {
                contestWinnerAiming = false; postContestInput = default;
                challenger = entrant;
                ControlState = AmplifierControlState.Contested;
                controlDeadline = Time.time + Mathf.Max(.25f, Possession.contestSeconds) + EntrySeconds;
                lightControl = .5f; chargeSeconds = 0f; chargeStarted = false;
                pilot.awaitNeutral = challenger.awaitNeutral = true;
                pilot.hasPress = challenger.hasPress = false;
                pilot.nextPress = challenger.nextPress = 0f;
            }
            return true;
        }

        internal void ReceivePossessionInput(PlayerControllerScript player, PlayerInputFrame input)
        {
            var seat = pilot != null && pilot.player == player ? pilot : challenger != null && challenger.player == player ? challenger : null;
            if (seat == null) return;
            seat.input = input; seat.inputFrame = Time.frameCount;
        }

        private void TickAim()
        {
            if (pilot.inputFrame != Time.frameCount || pilot.player.IsAmplifierTransitioning)
            { postContestInput.BreakRelease(); return; }
            var input = pilot.input;
            aimDirection = PlanarDirection(pilot.player.AimDirectionWS, aimDirection);
            if (contestWinnerAiming && !chargeStarted)
            {
                if (!postContestInput.TryStartCharge(Time.time, input.attackDown, input.attackHeld, input.shieldDown, input.shieldHeld)) return;
                chargeStarted = true;
            }
            else if (input.attackDown || input.attackHeld) chargeStarted = true;
            if (chargeStarted && input.attackHeld) chargeSeconds += Time.deltaTime;
            if (chargeStarted && (Charge01 >= 1f || input.attackUp || !input.attackHeld)) Launch();
        }

        private float CreditPress(Occupancy seat)
        {
            if (seat == null || !seat.player || seat.inputFrame != Time.frameCount) return 0f;
            var input = seat.input;
            if (seat.awaitNeutral)
            {
                if (!input.attackHeld && !input.shieldHeld && !input.attackDown && !input.shieldDown) seat.awaitNeutral = false;
                return 0f;
            }
            bool attack = input.attackDown, shield = input.shieldDown;
            if (attack == shield || (input.attackHeld && input.shieldHeld) || Time.time < seat.nextPress ||
                (seat.hasPress && seat.lastWasAttack == attack)) return 0f;
            seat.hasPress = true; seat.lastWasAttack = attack;
            seat.nextPress = Time.time + 1f / Mathf.Clamp(Possession.pressesPerSecond, 1f, 12f);
            return (seat.player.teamID == 1 ? 1f : -1f) * Mathf.Clamp(Possession.progressPerPress, .01f, .5f);
        }

        private void TickContest()
        {
            if (pilot.player.IsAmplifierTransitioning || challenger.player.IsAmplifierTransitioning) return;
            // Credit both sides together, avoiding player Update-order advantage.
            lightControl = Mathf.Clamp01(lightControl + CreditPress(pilot) + CreditPress(challenger));
            if (lightControl > 0f && lightControl < 1f && Time.time < controlDeadline) return;
            if (Mathf.Abs(lightControl - .5f) < .0001f)
            {
                ReleaseOccupancy(ref pilot, 0f); ReleaseOccupancy(ref challenger, 0f); BecomeAvailable(); return;
            }
            int winnerTeam = lightControl > .5f ? 1 : 2;
            if (pilot.player.teamID == winnerTeam) ReleaseOccupancy(ref challenger, Possession.loserStunSeconds);
            else { ReleaseOccupancy(ref pilot, Possession.loserStunSeconds); pilot = challenger; challenger = null; }
            BeginWinnerAim();
        }

        private void BeginWinnerAim()
        {
            contestWinnerAiming = true;
            postContestInput.Begin(Time.time, Possession);
            ControlState = AmplifierControlState.Aiming;
            controlDeadline = postContestInput.UnlockAt + Mathf.Max(.25f, Possession.winnerAimSeconds);
            nextEntryTime = Time.time + Mathf.Max(0f, Possession.reentryDelay);
            chargeSeconds = 0f; chargeStarted = false;
            aimDirection = PlanarDirection(pilot.player.AimDirectionWS, aimDirection);
        }

        private void Launch()
        {
            float range = Mathf.Lerp(Mathf.Max(.1f, Possession.minimumRange), Mathf.Max(Possession.minimumRange, Possession.maximumRange), Charge01);
            shotOwner = pilot.player;
            ReleaseOccupancy(ref pilot, 0f, -aimDirection);
            RefreshShotWorld();
            shotVictims.Clear(); shieldContacts.Clear();
            ControlState = AmplifierControlState.Launched;
            BeginPhysicalShot(range);
        }

        public void ReleaseOccupant(PlayerControllerScript player)
        {
            if (pilot != null && pilot.player == player) ReleaseOccupancy(ref pilot, 0f, default, false);
            if (challenger != null && challenger.player == player) ReleaseOccupancy(ref challenger, 0f, default, false);
        }

        private float EntrySeconds => Possession.animatePlayerTransit ? Mathf.Max(.05f, Possession.entrySeconds) : 0f;

        private void ReleaseOccupancy(ref Occupancy seat, float stun, Vector3 preferred = default, bool animate = true)
        {
            var released = seat; seat = null;
            if (released == null || !released.player) return;
            if (preferred.sqrMagnitude < .001f) preferred = PlanarDirection(released.entryPosition - transform.position, -aimDirection);
            Vector3 point = FindEjectionPoint(released.player, preferred, released.entryPosition);
            if (animate && visual) visual.PlayEjectionRipple(point - transform.position, Possession);
            released.player.ExitAmplifier(this, point, PlanarDirection(point - transform.position, preferred) * Mathf.Max(0f, Possession.ejectionRecoil), Mathf.Max(0f, stun), animate);
        }

        private Vector3 FindEjectionPoint(PlayerControllerScript player, Vector3 direction, Vector3 fallback)
        {
            RefreshShotWorld();
            float radius = Mathf.Max(.05f, PlayerScaleAdjuster.BodyRadiusOf(player));
            for (int ring = 0; ring < 8; ring++)
                for (int i = 0; i < 24; i++)
                {
                    float angle = (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 15f;
                    Vector3 point = transform.position + Quaternion.AngleAxis(angle, Vector3.up) * direction.normalized * (CoreRadius + radius + .15f + ring * radius);
                    point.y = fallback.y;
                    if (EjectionClear(player, point, radius)) return point;
                }
            // The entry position is known to have admitted the player's full body.
            return fallback;
        }

        private bool EjectionClear(PlayerControllerScript player, Vector3 point, float radius)
        {
            if (shotArena && shotArena.IsValid && (shotArena.ClampWorldPointInside(point, radius) - point).sqrMagnitude > .0001f) return false;
            int count = gameObject.scene.GetPhysicsScene().OverlapSphere(point, radius, placementHits, ~0, QueryTriggerInteraction.Ignore);
            if (count == placementHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var c = placementHits[i];
                if (c.transform.IsChildOf(player.transform) || c.transform.IsChildOf(transform)) continue;
                if (c.GetComponentInParent<PlayerControllerScript>() is PlayerControllerScript p && p.IsInsideAmplifier) continue;
                if (Physics.GetIgnoreLayerCollision(player.gameObject.layer, c.gameObject.layer)) continue;
                return false;
            }
            return true;
        }

        private void BecomeAvailable()
        {
            contestWinnerAiming = false;
            postContestInput = default;
            EndPhysicalShot();
            ControlState = AmplifierControlState.Available;
            chargeSeconds = 0f; chargeStarted = false;
            nextEntryTime = Time.time + Mathf.Max(0f, Possession.reentryDelay);
            if (collisionShape && possessionPhysicsOwned) collisionShape.isTrigger = possessionWasTrigger;
            if (body && possessionPhysicsOwned) body.isKinematic = true;
            HidePossessionPresentation();
        }

        private void ResetPossession()
        {
            contestWinnerAiming = false;
            postContestInput = default;
            ReleaseOccupancy(ref pilot, 0f, default, false); ReleaseOccupancy(ref challenger, 0f, default, false);
            foreach (var player in PlayerControllerScript.ActivePlayers) if (player) player.CancelAmplifierTransit(this);
            EndPhysicalShot();
            if (visual) visual.SetPossessionMarker(0f, 0f, false, 0f);
            if (visual) visual.ResetInteractionFeedback();
            ControlState = AmplifierControlState.Available;
            if (possessionPhysicsOwned)
            {
                if (body) body.isKinematic = possessionWasKinematic;
                if (collisionShape) collisionShape.isTrigger = possessionWasTrigger;
                possessionPhysicsOwned = false;
            }
            HidePossessionPresentation();
        }

        private float CoreRadius => collisionShape ? Mathf.Max(.05f, Mathf.Max(collisionShape.bounds.extents.x, collisionShape.bounds.extents.z)) : .5f;
        private static Vector3 PlanarDirection(Vector3 value, Vector3 fallback)
        { value.y = 0f; return value.sqrMagnitude > .00001f ? value.normalized : fallback; }
    }
}
