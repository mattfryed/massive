#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Massive.Player;
using UnityEngine;

public sealed class PlayerRepulsorPlayValidationRunner : MonoBehaviour
{
    public static string Status { get; private set; } = "Idle";
    public static string Result { get; private set; } = "";
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<string> checks = new List<string>();
    readonly List<AttackStageType> stages = new List<AttackStageType>();
    readonly List<InputSnapshot> inputs = new List<InputSnapshot>();
    PlayerControllerScript player;
    PlayerAttackController attack;
    PlayerRepulsorAOE pulse;
    PlayerRepulsorFeedback feedback;
    PlayerVisualController body;
    PlayerScaleAdjuster scale;
    Rigidbody playerBody;
    GameObject victimObject;
    Vector3 oldPosition, oldVelocity, oldAngularVelocity;
    Quaternion oldRotation;
    float oldSize, oldTimeScale, beganAt;
    int oldCaptureRate, emitted;
    bool saved, cleaned;

    struct InputSnapshot
    {
        public PlayerControllerScript player;
        public PlayerControlMode mode;
        public PlayerInputFrame frame;
        public bool hadFrame;
    }

    public void Run() { Status = "Starting"; Result = ""; StartCoroutine(Guarded()); }

    IEnumerator Guarded()
    {
        IEnumerator routine = Validate();
        while (true)
        {
            object instruction = null;
            bool next;
            try
            {
                if (saved && Time.time - beganAt > 9.5f)
                    throw new InvalidOperationException("Validation exceeded its 9.5 simulated-second limit.");
                next = routine.MoveNext();
                if (next) instruction = routine.Current;
            }
            catch (Exception ex)
            {
                Status = "FAILED";
                Result = string.Join("\n", checks) + "\nFAIL: " + ex.GetBaseException().Message;
                Debug.LogError(Result);
                Cleanup(); Destroy(gameObject); yield break;
            }
            if (!next) break;
            yield return instruction;
        }
        Status = "PASSED";
        Result = string.Join("\n", checks);
        Debug.Log(Result);
        Cleanup(); Destroy(gameObject);
    }

    IEnumerator Validate()
    {
        foreach (var candidate in FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None))
            if (candidate.isActiveAndEnabled && candidate.transform.parent && candidate.transform.parent.name == "Players" && candidate.playerID == 0)
                player = candidate;
        Require(player, "Active P1 was not found under Players.");
        attack = player.GetComponent<PlayerAttackController>();
        pulse = player.GetComponentInChildren<PlayerRepulsorAOE>();
        feedback = player.GetComponent<PlayerRepulsorFeedback>();
        body = player.GetComponentInChildren<PlayerVisualController>();
        scale = player.GetComponent<PlayerScaleAdjuster>();
        playerBody = player.GetComponent<Rigidbody>();
        Require(attack && pulse && feedback && body && scale && playerBody, "P1 is missing a required Repulsor component.");
        Require(!attack.IsAttacking && !player.temporarilyEliminated && !player.IsStunned && !player.IsExternallyStunned && !player.IsMatchInputLocked,
            "P1 must be idle, alive, unstunned and unlocked before validation.");
        Require(Mathf.Abs(player.ExternalMovementMultiplier - 1f) < .001f, "Let existing external movement effects finish before validation.");
        Require(feedback.bodyPulseEnabled && feedback.recoveryEnabled, "Enable the body pulse and movement recovery for this validation.");
        Require(attack.Profile && attack.Profile.GetStage(2) != null, "P1 needs a three-stage attack profile.");
        oldPosition = player.transform.position; oldRotation = player.transform.rotation;
        oldVelocity = playerBody.linearVelocity; oldAngularVelocity = playerBody.angularVelocity;
        oldSize = scale.Size; oldTimeScale = Time.timeScale; oldCaptureRate = Time.captureFramerate;
        saved = true; beganAt = Time.time;
        foreach (var candidate in FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None))
        {
            if (!candidate.transform.parent || candidate.transform.parent.name != "Players") continue;
            inputs.Add(new InputSnapshot { player = candidate, mode = candidate.ControlMode,
                frame = Get<PlayerInputFrame>(candidate, "_scriptedInput"), hadFrame = Get<bool>(candidate, "_hasScriptedInput") });
            candidate.SetControlMode(PlayerControlMode.Disabled);
        }
        player.SetControlMode(PlayerControlMode.Scripted);
        player.ClearScriptedInput(); playerBody.linearVelocity = Vector3.zero;
        Time.timeScale = 1f; Time.captureFramerate = 60;
        attack.OnStageStarted.AddListener(OnStage);
        pulse.PulseStarted += OnPulse;

        Status = "Normal three-press combo";
        float settleUntil = Time.time + .3f;
        while (Time.time < settleUntil) yield return null;
        var press = PlayerInputFrame.Neutral; press.attackDown = true;
        player.SetScriptedInput(press);
        int nextCombo = 1;
        float deadline = Time.time + 4f, minimum = 1f, maximum = 1f, recoveryMinimum = 1f, movementMinimum = 1f;
        while (Time.time < deadline)
        {
            yield return null;
            minimum = Mathf.Min(minimum, body.RepulsorVisualScale);
            maximum = Mathf.Max(maximum, body.RepulsorVisualScale);
            recoveryMinimum = Mathf.Min(recoveryMinimum, feedback.RecoveryMultiplier);
            movementMinimum = Mathf.Min(movementMinimum, player.ExternalMovementMultiplier);
            var input = PlayerInputFrame.Neutral;
            if (nextCombo < 3 && attack.IsAttacking && attack.CurrentStageIndex == nextCombo - 1 && attack.StageNormalizedTime >= .86f)
            { input.attackDown = true; nextCombo++; }
            player.SetScriptedInput(input);
            if (stages.Count >= 3 && !attack.IsAttacking && feedback.RecoveryMultiplier >= .999f && recoveryMinimum < .999f) break;
        }
        Check(stages.Count == 3 && stages[0] == AttackStageType.PrimaryLunge && stages[1] == AttackStageType.ComboSwipe &&
            stages[2] == AttackStageType.FinisherRepulsor, "Normal input produces thrust, sweep, Repulsor in order (exactly three button presses).");
        Check(emitted == 1, "The normal combo emits exactly one Repulsor at activation.");
        Check(minimum < 1f - feedback.contraction * .5f && maximum > 1f + feedback.expansion * .5f,
            "Body contracts and rebounds: " + minimum.ToString("F3") + " .. " + maximum.ToString("F3") + ".");
        Check(recoveryMinimum < .99f && movementMinimum < .99f && Mathf.Abs(player.ExternalMovementMultiplier - 1f) < .001f &&
            Mathf.Abs(feedback.RecoveryMultiplier - 1f) < .001f &&
            Mathf.Abs(body.RepulsorVisualScale - 1f) < .002f,
            "Recovery slows and restores movement; body returns to normal (minimum " + recoveryMinimum.ToString("F3") + ").");
        Check(!pulse.IsPulseActive && !pulse.GetComponent<SphereCollider>().enabled, "Normal completion leaves no live Repulsor collider.");

        Status = "Isolated half-size contact and cancellation";
        // Only this section enters stage 3 directly. It tests contact/lifecycle separately from the real input chain above.
        scale.Size = .5f;
        player.transform.position = new Vector3(-3f, oldPosition.y, 0f);
        playerBody.position = player.transform.position; playerBody.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        victimObject = new GameObject("Repulsor inert contact fixture") { hideFlags = HideFlags.DontSave, tag = "Player", layer = player.gameObject.layer };
        victimObject.SetActive(false);
        var victimBody = victimObject.AddComponent<Rigidbody>();
        victimBody.useGravity = false; victimBody.linearDamping = 0f;
        victimBody.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        var victimCollider = victimObject.AddComponent<SphereCollider>();
        victimCollider.radius = .04f; victimCollider.isTrigger = true;
        var victim = victimObject.AddComponent<PlayerControllerScript>();
        victim.enabled = false; victim.SetControlMode(PlayerControlMode.Disabled);
        Set(victim, "isPseudoPlayer", true); victim.teamID = player.teamID == 1 ? 2 : 1;
        victimObject.transform.position = player.transform.position + Vector3.forward * (PlayerScaleAdjuster.BodyRadiusOf(player) + .12f);
        victimObject.SetActive(true);
        // Ignore all P1 contacts except the actual Repulsor trigger.
        foreach (var collider in player.GetComponentsInChildren<Collider>(true))
            if (collider != pulse.GetComponent<SphereCollider>()) Physics.IgnoreCollision(victimCollider, collider, true);
        Physics.SyncTransforms();
        Invoke(attack, "StartStage", 2);
        deadline = Time.time + 1f;
        while (!pulse.IsPulseActive && Time.time < deadline) yield return null;
        Require(pulse.IsPulseActive, "Isolated stage did not activate.");
        Vector3 releasePoint = pulse.OriginWorld;
        float expectedEnd = Mathf.Max(pulse.StartRadiusWorld, attack.Profile.GetStage(2).RepulsorMaxRadius * PlayerScaleAdjuster.SizeOf(player));
        Check(Mathf.Abs(pulse.EndRadiusWorld - expectedEnd) < .001f && PlayerScaleAdjuster.SizeOf(player) > 0f,
            "Half-size Repulsor starts at the body outline and uses scaled final reach " + pulse.EndRadiusWorld.ToString("F3") + ".");
        var victims = Get<HashSet<PlayerControllerScript>>(pulse, "_hitVictims");
        deadline = Time.time + .3f;
        while (pulse.IsPulseActive && !victims.Contains(victim) && Time.time < deadline) yield return new WaitForFixedUpdate();
        Require(victims.Contains(victim), "The expanding Repulsor did not make real physics contact with the inert victim.");
        yield return new WaitForFixedUpdate();
        Check(victimBody.linearVelocity.sqrMagnitude > .01f, "An actual trigger contact imparts knockback to the opposing fixture.");
        victimBody.linearVelocity = Vector3.zero;
        Invoke(pulse, "TryHit", victimCollider);
        yield return new WaitForFixedUpdate();
        Check(victimBody.linearVelocity.sqrMagnitude < .0001f, "A second contact with the same victim adds no second impulse.");
        playerBody.position += Vector3.right * .1f;
        player.transform.position = playerBody.position;
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();
        Check((pulse.OriginWorld - releasePoint).sqrMagnitude < .000001f, "The pulse retains its activation origin.");
        attack.CancelAttack();
        Check(!pulse.IsPulseActive && !pulse.GetComponent<SphereCollider>().enabled, "Cancellation removes the hitbox immediately.");
        victimBody.linearVelocity = Vector3.zero;
        Invoke(pulse, "TryHit", victimCollider);
        yield return new WaitForFixedUpdate();
        Check(victimBody.linearVelocity.sqrMagnitude < .0001f && feedback.RecoveryMultiplier == 1f && body.RepulsorVisualScale == 1f,
            "Cancelled contact has no impulse and body/recovery modifiers are reset.");
        checks.Add("PASS: Contact section was an isolated direct-stage fixture; the preceding combo used normal scripted player input. No scene assets changed.");
    }

    void OnStage(AttackStage stage) { stages.Add(stage.StageType); }
    void OnPulse(PlayerRepulsorAOE source) { emitted++; }
    void Check(bool condition, string message) { Require(condition, message); checks.Add("PASS: " + message); }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
    static void Invoke(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, PrivateInstance).Invoke(target, arguments);

    void Cleanup()
    {
        if (cleaned) return; cleaned = true;
        if (attack) { attack.OnStageStarted.RemoveListener(OnStage); if (saved) attack.CancelAttack(); }
        if (pulse) pulse.PulseStarted -= OnPulse;
        if (victimObject) { victimObject.SetActive(false); Destroy(victimObject); }
        if (!saved) return;
        Time.timeScale = oldTimeScale; Time.captureFramerate = oldCaptureRate;
        if (scale) scale.Size = oldSize;
        if (player)
        {
            player.transform.SetPositionAndRotation(oldPosition, oldRotation);
            if (playerBody) { playerBody.position = oldPosition; playerBody.rotation = oldRotation;
                playerBody.linearVelocity = oldVelocity; playerBody.angularVelocity = oldAngularVelocity; }
            var gridPulse = player.GetComponent<PlayerRepulsorGridPulse>();
            if (gridPulse) gridPulse.ClearPulses();
        }
        foreach (var snapshot in inputs)
        {
            if (!snapshot.player) continue;
            snapshot.player.ClearScriptedInput();
            if (snapshot.hadFrame) snapshot.player.SetScriptedInput(snapshot.frame);
            snapshot.player.SetControlMode(snapshot.mode);
        }
        Physics.SyncTransforms();
    }

    void OnDestroy() { Cleanup(); }
}
#endif
