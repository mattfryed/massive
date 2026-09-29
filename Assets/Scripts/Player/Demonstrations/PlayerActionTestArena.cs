using System;
using System.Collections;
using Massive.Player;
using Massive.Scoring;
using UnityEngine;

namespace Massive.Demonstrations
{
    /// <summary>Input-only experiments on the ordinary, scoring-enabled arena roster.</summary>
    [DefaultExecutionOrder(-600), DisallowMultipleComponent]
    public sealed class PlayerActionTestArena : MonoBehaviour
    {
        public enum TestMode { Manual, Combo, AttackAndShield, AllDemos }
        [SerializeField] private PlayerRosterController roster;
        [SerializeField] private GameManagerScript match;
        [SerializeField] private TestMode mode;
        [SerializeField] private PlayerDemoGallery gallery;
        private bool galleryRequested;
        [Header("Choreography (does not override gameplay tuning)")]
        [SerializeField] private Vector3 center = Vector3.zero;
        [SerializeField, Range(1f, 12f)] private float spacing = 3f;
        [Tooltip("Seconds after attack press; negative values raise the shield first.")]
        [SerializeField, Range(-3f, 2f)] private float shieldDelay;
        [Tooltip("Keep the shield button held for its full lifetime in repeatable trials.")]
        [SerializeField] private bool holdShield;
        [SerializeField, Min(.1f)] private float repeatPause = 1.5f;
        [SerializeField, Min(2f)] private float timeout = 20f;
        [SerializeField] private bool repeat = true;
        [SerializeField] private bool showControls = true;
        private PlayerControllerScript attacker, defender;
        private PlayerShieldAbility shield;
        private PlayerMelee[] melee;
        private Coroutine routine;
        private bool failed;
        private TestMode appliedMode;
        public string Status { get; private set; } = "Manual control";
        public int CompletedTrials { get; private set; }
        public int ShieldContacts { get; private set; }
        public TestMode Mode => mode;
        public bool IsRunning => routine != null || (gallery && gallery.IsPlaying);
        public bool HasFailed => failed;

        private void Awake()
        {
            GameFlowContext.EnsureExists();
            GameFlowContext.Instance.SetMode(GameMode.OneVOne);
            GameFlowContext.Instance.ClearSelection();
            GameFlowContext.Instance.ClearLastMatchResult();
        }
        private void Start()
        {
            if (!roster || !match || !roster.P1 || !roster.P3)
            { Status = "Missing match / P1 / P3 bindings"; enabled = false; return; }
            attacker = roster.P1.GetComponent<PlayerControllerScript>();
            defender = roster.P3.GetComponent<PlayerControllerScript>();
            shield = defender.GetComponent<PlayerShieldAbility>();
            if (!attacker.attackController || !defender.attackController || !shield)
            { Status = "Missing attack or shield component"; enabled = false; return; }
            melee = attacker.GetComponentsInChildren<PlayerMelee>(true);
            foreach (var contact in melee) contact.ShieldContact += OnShieldContact;
            SetMode(mode);
        }
        private void Update()
        {
            if (!attacker || !defender) return;
            if (Input.GetKeyDown(KeyCode.F1)) showControls = !showControls;
            if (Input.GetKeyDown(KeyCode.F6)) SetMode(mode == TestMode.Manual ? TestMode.AttackAndShield : TestMode.Manual);
            if (mode != appliedMode) SetMode(mode);
            if (mode == TestMode.AllDemos && galleryRequested && gallery && !gallery.IsPlaying && match.Phase == MatchRuntimePhase.Regulation)
            {
                gallery.Play(match, attacker, defender);
                Status = "Seven independent demos â€” match timer paused";
            }
            if (match.IsStartupBlocked) Status = match.StartupFailureReason;
            if (routine != null && match.Phase != MatchRuntimePhase.Regulation) StopTrials();
        }
        public void SetMode(TestMode next)
        {
            StopTrials();
            mode = appliedMode = next;
            galleryRequested = next == TestMode.AllDemos;
            SetInputMode(next == TestMode.Manual ? PlayerControlMode.Rewired : PlayerControlMode.Scripted);
            Status = next == TestMode.Manual ? "Manual: normal Rewired P1 / P3 controls" : "Ready â€” Run to begin";
        }
        private void SetInputMode(PlayerControlMode next)
        {
            if (attacker) { attacker.ClearScriptedInput(); attacker.SetControlMode(next); }
            if (defender) { defender.ClearScriptedInput(); defender.SetControlMode(next); }
        }
        public void StopTrials()
        {
            galleryRequested = false;
            if (gallery) gallery.Stop();
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (attacker) attacker.ClearScriptedInput();
            if (defender) defender.ClearScriptedInput();
            Status = "Stopped (health, cooldowns and score preserved)";
        }
        public void RunTrials()
        {
            if (!attacker || !defender || mode == TestMode.Manual || match.Phase != MatchRuntimePhase.Regulation) return;
            if (mode == TestMode.AllDemos)
            {
                StopTrials(); galleryRequested = true; return;
            }
            StopTrials(); failed = false;
            routine = StartCoroutine(Trials());
        }
        public void UseDemoSpacingAndTiming()
        {
            if (!attacker || !shield) return;
            var stage = attacker.attackController.Profile.GetStage(0);
            spacing = PlayerScaleAdjuster.BodyRadiusOf(attacker) + PlayerScaleAdjuster.BodyRadiusOf(defender) +
                stage.TravelDistance * PlayerScaleAdjuster.ActionReachOf(attacker) * .45f;
            float hitStart = stage.ActivationStartNormalized * stage.Duration;
            shieldDelay = hitStart < shield.DurationSeconds * .5f ? -.05f : hitStart - shield.DurationSeconds * .35f;
        }
        private void OnShieldContact(PlayerControllerScript target)
        { if (target == defender) ShieldContacts++; }
        private void OnDisable()
        {
            StopTrials();
            SetInputMode(PlayerControlMode.Rewired);
        }
        private void OnDestroy()
        { if (melee != null) foreach (var contact in melee) if (contact) contact.ShieldContact -= OnShieldContact; }
        private void Fail(string reason)
        {
            failed = true; Status = reason;
            Debug.LogWarning("[Player action arena] " + reason, this);
        }
        private IEnumerator WaitFor(Func<bool> condition, string reason)
        {
            float end = Time.time + timeout;
            while (!condition() && Time.time < end) yield return null;
            if (!condition()) Fail(reason);
        }
        private static bool Ready(PlayerControllerScript actor)
        {
            return !actor.temporarilyEliminated && !actor.IsStunned && !actor.IsExternallyStunned &&
                !actor.IsMatchInputLocked && !actor.attackController.IsAttacking && actor.attackController.CooldownRemaining <= 0f;
        }
        private void Frame(PlayerControllerScript actor, Vector3 aim, Vector2 move = default,
            bool attack = false, bool release = false, bool block = false, bool hold = false)
        {
            actor.SetScriptedInput(new PlayerInputFrame { moveInput = move, hasAimDirWS = true, aimDirWS = aim,
                attackDown = attack, attackHeld = attack, attackUp = release, shieldDown = block, shieldHeld = block || hold });
        }
        private IEnumerator Attack()
        {
            Frame(attacker, Vector3.right, attack: true); yield return null;
            Frame(attacker, Vector3.right, release: true); yield return null;
            Frame(attacker, Vector3.right);
        }
        private IEnumerator PositionActors(float gap)
        {
            Status = "Moving to test markers";
            var a = attacker.GetComponent<Rigidbody>(); var b = defender.GetComponent<Rigidbody>();
            Vector3 left = center - Vector3.right * gap * .5f, right = center + Vector3.right * gap * .5f;
            float end = Time.time + timeout;
            while (Time.time < end)
            {
                bool first = Steer(attacker, a, left, Vector3.right);
                bool second = Steer(defender, b, right, Vector3.left);
                if (first && second) yield break;
                yield return null;
            }
            Fail("Could not reach markers through normal movement. Check spacing, obstacles or cooldowns.");
        }
        private bool Steer(PlayerControllerScript actor, Rigidbody body, Vector3 target, Vector3 aim)
        {
            Vector3 delta = target - body.position; delta.y = 0;
            Vector3 velocity = body.linearVelocity; velocity.y = 0;
            bool close = delta.magnitude < .15f;
            Vector2 stick = close || !Ready(actor) ? Vector2.zero : new Vector2(delta.x, delta.z).normalized * Mathf.Clamp01(delta.magnitude);
            Frame(actor, aim, stick);
            return close && velocity.magnitude < .2f;
        }
        private IEnumerator Trials()
        {
            // First yield lets RunTrials retain a valid handle even if setup immediately fails.
            yield return null;
            do
            {
                yield return WaitFor(() => Ready(attacker) && Ready(defender), "Actors did not become ready (death/respawn or action lockout).");
                if (failed) break;
                yield return PositionActors(spacing);
                if (failed) break;
                yield return WaitFor(() => Ready(attacker) && Ready(defender) && !shield.IsActive &&
                    shield.CooldownRemaining <= 0f && shield.ActivationCooldownRemaining <= 0f, "Actions did not recharge.");
                if (failed) break;
                if (mode == TestMode.Combo) yield return Combo();
                else yield return Block();
                attacker.ClearScriptedInput(); defender.ClearScriptedInput();
                if (failed) break;
                CompletedTrials++;
                yield return new WaitForSeconds(repeatPause);
            } while (repeat);
            attacker.ClearScriptedInput(); defender.ClearScriptedInput();
            routine = null;
        }
        private IEnumerator Combo()
        {
            Status = "Thrust / Sweep / Repulsor";
            var attack = attacker.attackController;
            yield return Attack();
            yield return WaitFor(() => attack.IsAttacking, "Attack input was not accepted.");
            for (int index = 0; index < attack.Profile.Stages.Count - 1 && !failed; index++)
            {
                int current = index;
                yield return WaitFor(() => attack.IsAttacking && attack.CurrentStageIndex == current &&
                    attack.TimeInStage >= attack.ComboWindowSeconds(attack.Profile.GetStage(current)).x, "Combo window was not reached.");
                if (failed) yield break;
                yield return Attack();
                yield return WaitFor(() => attack.CurrentStageIndex == current + 1, "Combo handoff was not accepted.");
            }
            if (!failed) yield return WaitFor(() => !attack.IsAttacking, "Combo did not finish.");
            if (!failed) Status = "Combo completed";
        }
        private IEnumerator Block()
        {
            Status = "Attack / shield trial";
            int before = ShieldContacts;
            float delay = shieldDelay;
            bool hold = holdShield;
            if (delay < 0)
            {
                Frame(defender, Vector3.left, block: true); yield return null;
                Frame(defender, Vector3.left, hold: hold);
                yield return new WaitForSeconds(Mathf.Max(0f, -delay - Time.deltaTime));
            }
            float started = Time.time;
            Frame(attacker, Vector3.right, attack: true);
            if (delay == 0) Frame(defender, Vector3.left, block: true);
            yield return null;
            Frame(attacker, Vector3.right, release: true);
            if (delay == 0) Frame(defender, Vector3.left, hold: hold);
            yield return null;
            Frame(attacker, Vector3.right);
            if (delay > 0)
            {
                while (Time.time < started + delay) yield return null;
                Frame(defender, Vector3.left, block: true); yield return null;
                Frame(defender, Vector3.left, hold: hold);
            }
            yield return WaitFor(() => !attacker.attackController.IsAttacking && !shield.IsActive, "Attack/shield did not finish.");
            if (!failed) Status = ShieldContacts > before ? "Confirmed shield contact" : "No shield contact at this spacing / timing";
        }
        private void OnGUI()
        {
            if (!showControls) return;
            float panelWidth = mode == TestMode.AllDemos ? Mathf.Min(900, Screen.width * .46f) : Mathf.Min(900, Screen.width - 24);
            float panelHeight = mode == TestMode.AllDemos ? 100f : 190f;
            GUILayout.BeginArea(new Rect((Screen.width - panelWidth) * .5f, Screen.height - panelHeight - 12, panelWidth, panelHeight), GUI.skin.box);
            GUILayout.Label("PLAYER ACTION LAB  |  F6 manual / shield test  |  F1 hide panel");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("All demos")) SetMode(TestMode.AllDemos);
            if (GUILayout.Button("Manual")) SetMode(TestMode.Manual);
            if (GUILayout.Button("Combo")) SetMode(TestMode.Combo);
            if (GUILayout.Button("Attack + shield")) SetMode(TestMode.AttackAndShield);
            GUI.enabled = mode != TestMode.Manual && match && match.Phase == MatchRuntimePhase.Regulation;
            if (GUILayout.Button("Run")) RunTrials();
            if (GUILayout.Button("Stop")) StopTrials();
            GUI.enabled = true;
            if (mode != TestMode.AllDemos)
            {
                repeat = GUILayout.Toggle(repeat, "Repeat");
                if (GUILayout.Button("Demo spacing / timing")) UseDemoSpacingAndTiming();
            }
            GUILayout.EndHorizontal();
            if (mode == TestMode.AllDemos)
            {
                GUILayout.Label(Status);
                GUILayout.Label("Seven looping demos | Tap parry / late held block | Shared Player Tuning");
                GUILayout.EndArea(); return;
            }
            GUILayout.BeginHorizontal(); GUILayout.Label("Spacing " + spacing.ToString("F2"), GUILayout.Width(110));
            spacing = GUILayout.HorizontalSlider(spacing, 1f, 12f); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label("Shield delay " + shieldDelay.ToString("F2") + "s", GUILayout.Width(150));
            shieldDelay = GUILayout.HorizontalSlider(shieldDelay, -3f, 2f); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            holdShield = GUILayout.Toggle(holdShield, "Hold shield (up to 3s)");
            if (shield) GUILayout.Label(shield.IsActive ?
                (shield.IsParryWindow ? "Opening parry" : "Block only") +
                (shield.IsHolding ? " | " + shield.HoldRemaining.ToString("F1") + "s left" : " | Tap") : "Shield ready / recharging");
            GUILayout.EndHorizontal();
            GUILayout.Label(Status + "  |  Trials " + CompletedTrials + "  |  Contacts " + ShieldContacts);
            GUILayout.Label("Live values / visuals: MASSIVE > Player Tuning. Tests preserve health and score.");
            GUILayout.EndArea();
        }
    }
}


