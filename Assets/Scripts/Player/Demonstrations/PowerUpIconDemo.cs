using System;
using System.Collections;
using Massive.Player;
using Massive.PowerUps;
using TMPro;
using UnityEngine;

namespace Massive.Demonstrations
{
    /// <summary>Two live copies of one pickup: ordinary expiry and a confirmed player melee claim.</summary>
    [DisallowMultipleComponent]
    public sealed class PowerUpIconDemo : MonoBehaviour
    {
        public PowerUpDefinition definition;
        public GameObject playerPrefab;
        public VectorGridGPU grid;
        public PowerUpPickupToastSystem toasts;
        public Transform naturalMarker, hitMarker;
        public TMP_Text naturalLabel, hitLabel;
        [Min(0f)] public float initialHitDelay;
        [Min(.5f)] public float idleBeforeHit = 1.8f;
        [Min(.1f)] public float repeatPause = 1f;
        [Min(2f)] public float actionTimeout = 20f;

        public PlayerControllerScript Player { get; private set; }
        public PowerUpPickup NaturalPickup { get; private set; }
        public PowerUpPickup HitPickup { get; private set; }
        public int NaturalLoops { get; private set; }
        public int ConfirmedClaims { get; private set; }
        public int CompletedHitLoops { get; private set; }
        public int ToastsShown { get; private set; }
        public string Failure { get; private set; }
        private GameObject session;
        private Vector3 playerHome;
        private Rigidbody body;
        private string naturalPhase, hitPhase;
        private float nextLabelUpdate;

        private void OnEnable()
        {
            Failure = null; NaturalLoops = ConfirmedClaims = CompletedHitLoops = ToastsShown = 0;
            if (!definition || !definition.pickupPrefab || !playerPrefab || !naturalMarker || !hitMarker || !toasts)
            { Fail("Missing pickup, actor, markers or toast system."); return; }
            session = new GameObject("Power-up icon session");
            session.transform.SetParent(transform, false); session.SetActive(false);
            var go = Instantiate(playerPrefab, session.transform);
            Player = go.GetComponent<PlayerControllerScript>();
            Player.ConfigureDemonstration(session.transform, 0, 1);
            var scale = go.GetComponent<PlayerScaleAdjuster>();
            if (scale) scale.ApplyScale();
            var stage = Player.attackController.Profile.GetStage(0);
            float gap = PlayerScaleAdjuster.BodyRadiusOf(Player) + stage.TravelDistance * PlayerScaleAdjuster.ActionReachOf(Player) * .5f + .3f;
            playerHome = hitMarker.position - Vector3.right * gap;
            go.transform.position = playerHome;
            body = go.GetComponent<Rigidbody>();
            foreach (var interactor in go.GetComponentsInChildren<GridInteractor>(true)) interactor.grid = grid;
            foreach (var pulse in go.GetComponentsInChildren<PlayerRepulsorGridPulse>(true)) pulse.BindGrid(grid);
            session.SetActive(true);
            StartCoroutine(NaturalLoop()); StartCoroutine(HitLoop());
        }

        private PowerUpPickup Spawn(Transform marker, PlayerControllerScript claimant)
        {
            var pickup = PowerUpPickup.Spawn(definition, marker.position, definition.pickupPrefab.transform.rotation, session.transform);
            pickup.RestrictClaimsTo(claimant);
            // Keep the real body/hitbox interaction only for this pickup's assigned player.
            var colliders = pickup.GetComponentsInChildren<Collider>(true);
            foreach (var actor in FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None))
                if (actor != claimant) Ignore(colliders, actor.GetComponentsInChildren<Collider>(true));
            foreach (var other in PowerUpPickup.ActivePickups)
                if (other && other != pickup) Ignore(colliders, other.GetComponentsInChildren<Collider>(true));
            return pickup;
        }

        private static void Ignore(Collider[] first, Collider[] second)
        { foreach (var a in first) foreach (var b in second) if (a && b) Physics.IgnoreCollision(a, b); }

        private IEnumerator NaturalLoop()
        {
            // Wait until all sibling demo actors have been created before isolating pickup collisions.
            yield return null;
            while (Failure == null)
            {
                NaturalPickup = Spawn(naturalMarker, null); naturalPhase = "Spawn / idle";
                var animator = NaturalPickup.GetComponent<PowerUpIconManifestAnimator>();
                float end = Time.time + Mathf.Max(0, definition.WorldLifetimeSeconds) + actionTimeout;
                while (NaturalPickup && NaturalPickup.gameObject.activeSelf)
                {
                    if (animator && animator.IsDespawning) naturalPhase = "Despawn";
                    if (definition.WorldLifetimeSeconds > 0 && Time.time > end) { Fail("Natural pickup did not finish its lifetime."); yield break; }
                    yield return null;
                }
                if (NaturalPickup) Destroy(NaturalPickup.gameObject);
                NaturalPickup = null; NaturalLoops++; naturalPhase = "Respawning";
                yield return new WaitForSeconds(repeatPause);
            }
        }

        private IEnumerator HitLoop()
        {
            yield return null; yield return null;
            Frame(); hitPhase = "Preparing";
            yield return new WaitForSeconds(initialHitDelay);
            while (Failure == null)
            {
                yield return WaitFor(() => Ready() && !Player.powerUps.HasActive, "Player/effect did not become ready.",
                    Mathf.Max(actionTimeout, definition.EffectDurationSeconds + actionTimeout));
                if (Failure != null) yield break;
                HitPickup = Spawn(hitMarker, Player); HitPickup.Claimed += OnClaim;
                hitPhase = "Spawn / idle";
                yield return new WaitForSeconds(idleBeforeHit);
                int before = ConfirmedClaims;
                hitPhase = "Melee hit";
                Frame(attack: true); yield return null;
                Frame(release: true); yield return null; Frame();
                yield return WaitFor(() => ConfirmedClaims > before, "Melee did not claim the pickup.", actionTimeout);
                if (Failure != null) yield break;
                yield return WaitFor(() => !HitPickup || !HitPickup.gameObject.activeSelf, "Shatter did not finish.", actionTimeout);
                if (Failure != null) yield break;
                if (HitPickup) Destroy(HitPickup.gameObject);
                HitPickup = null;
                yield return new WaitForSeconds(repeatPause);
                yield return WaitFor(Ready, "Player did not finish its attack.", actionTimeout);
                if (Failure != null) yield break;
                hitPhase = "Returning";
                float end = Time.time + actionTimeout;
                while (Time.time < end)
                {
                    Vector3 delta = playerHome - body.position; delta.y = 0;
                    Vector3 velocity = body.linearVelocity; velocity.y = 0;
                    if (delta.magnitude <= .1f && velocity.magnitude <= .15f) break;
                    Frame(move: delta.magnitude < .1f ? Vector2.zero : new Vector2(delta.x, delta.z).normalized * Mathf.Clamp01(delta.magnitude));
                    yield return null;
                }
                Frame();
                if (Vector3.Distance(body.position, playerHome) > .2f) { Fail("Player could not return to the hit marker."); yield break; }
                hitPhase = "Effect active";
                yield return WaitFor(() => !Player.powerUps.HasActive, "Equipped effect did not expire.",
                    Mathf.Max(actionTimeout, definition.EffectDurationSeconds + 2f));
                if (Failure != null) yield break;
                CompletedHitLoops++;
                yield return new WaitForSeconds(repeatPause);
            }
        }

        private void OnClaim(PlayerControllerScript claimant)
        {
            if (claimant != Player) { Fail("Pickup was claimed by another demonstration."); return; }
            ConfirmedClaims++; hitPhase = "Activated / shatter";
            // Non-match actors deliberately skip gameplay HUD. Opt this fixture into the shared toast.
            if (toasts.Show(definition, HitPickup.transform.position, session.transform)) ToastsShown++;
        }
        private bool Ready() => Player && !Player.temporarilyEliminated && !Player.IsStunned && !Player.IsExternallyStunned &&
            !Player.attackController.IsAttacking && Player.attackController.CooldownRemaining <= 0;
        private void Frame(Vector2 move = default, bool attack = false, bool release = false) =>
            Player.SetScriptedInput(new PlayerInputFrame { moveInput = move, hasAimDirWS = true, aimDirWS = Vector3.right,
                attackDown = attack, attackHeld = attack, attackUp = release });
        private IEnumerator WaitFor(Func<bool> condition, string failure, float timeout)
        {
            float end = Time.time + timeout;
            while (!condition() && Time.time < end) yield return null;
            if (!condition()) Fail(failure);
        }
        private void Fail(string message)
        { Failure = message; if (Player) Player.ClearScriptedInput(); Debug.LogWarning("[Power-up icon demo] " + name + ": " + message, this); }
        private void Update()
        {
            if (Time.unscaledTime < nextLabelUpdate) return;
            nextLabelUpdate = Time.unscaledTime + .1f;
            if (naturalLabel) naturalLabel.text = "NATURAL  |  " + naturalPhase + "  |  " + NaturalLoops;
            if (hitLabel) hitLabel.text = Failure ?? ("HIT  |  " + hitPhase + "  |  " + ConfirmedClaims);
        }
        private void OnDisable()
        {
            StopAllCoroutines();
            if (Player) Player.ClearScriptedInput();
            if (session) { session.SetActive(false); Destroy(session); }
            Player = null; NaturalPickup = HitPickup = null; session = null;
        }
    }
}
