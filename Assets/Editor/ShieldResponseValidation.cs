#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Massive.Player;
using Massive.PowerUps;
using Massive.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Runs real PlayerActor input, physics contacts and lifecycle in an isolated batch project.
[InitializeOnLoad]
public static class ShieldResponseValidation
{
    const string Key = "MASSIVE.ShieldResponseValidation";
    const string Output = "Library/ShieldResponseValidation";
    static readonly List<string> checks = new(), errors = new();
    static IEnumerator routine;
    static double deadline;
    static float resumeTime;
    static int resumeFrame;
    static PlayerControllerScript attacker, defender;
    static PlayerShieldAbility shield;
    static PlayerAttackController attack;
    static PlayerTuningProfile tuning;
    static string originalTuning;
    static ShieldResponseValidation()
    {
        EditorApplication.update += Begin;
        EditorApplication.playModeStateChanged += State;
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated Unity batch project.");
        SessionState.SetBool(Key + ".begin", true);
    }
    static void Begin()
    {
        if (!SessionState.GetBool(Key + ".begin", false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        SessionState.SetBool(Key + ".begin", false); SessionState.SetBool(Key, true);
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground = true; Time.captureDeltaTime = .02f;
            checks.Clear(); errors.Clear(); resumeFrame = 0; resumeTime = 0;
            deadline = EditorApplication.timeSinceStartup + 150;
            Application.logMessageReceived += Log;
            routine = Checks(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(SessionState.GetBool(Key + ".passed", false) ? 0 : 1);
        }
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out.");
            if (Time.frameCount < resumeFrame || Time.time < resumeTime) return;
            if (routine.MoveNext())
            { resumeFrame = Time.frameCount + 1; resumeTime = Time.time + (routine.Current is float f ? f : 0f); return; }
            Finish(true);
        }
        catch (Exception ex) { checks.Add(ex.ToString()); Finish(false); }
    }
    static void Finish(bool passed)
    {
        EditorApplication.update -= Tick; routine = null; Time.timeScale = 1; Time.captureDeltaTime = 0;
        if (tuning && originalTuning != null) JsonUtility.FromJsonOverwrite(originalTuning, tuning);
        Application.logMessageReceived -= Log;
        passed &= errors.Count == 0;
        File.WriteAllText(Output + "/report.txt", (passed ? "PASSED" : "FAILED") + "\n" + string.Join("\n", checks) + "\n" + string.Join("\n", errors));
        SessionState.SetBool(Key + ".passed", passed); EditorApplication.isPlaying = false;
    }
    static void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); checks.Add("PASS " + label); }
    static void Set(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (f == null) throw new Exception("Missing fixture field " + field);
        f.SetValue(target, value);
    }
    static PlayerControllerScript CreatePlayer(int id, Vector3 position)
    {
        var holder = new GameObject("Shield response fixture " + id); holder.SetActive(false);
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), holder.transform);
        var p = go.GetComponent<PlayerControllerScript>(); p.playerID = id; p.teamID = id == 0 ? 1 : 2;
        p.SetControlMode(PlayerControlMode.Scripted);
        Set(p, "playMatchSpawnOnSceneLoad", false); Set(p, "showPlayerIdToastOnMatchStart", false);
        Set(p, "stunAppliesKnockback", false); p.stunTime = .12f;
        var body = go.GetComponent<Rigidbody>(); body.useGravity = false;
        holder.SetActive(true); Place(p, position);
        return p;
    }
    static void Place(PlayerControllerScript p, Vector3 position)
    {
        p.transform.position = position;
        var body = p.GetComponent<Rigidbody>(); body.position = position; body.linearVelocity = Vector3.zero;
        if (p.visualsController && p.visualsController.visuals) p.visualsController.visuals.position = position;
        Physics.SyncTransforms();
    }
    static void Input(PlayerControllerScript p, bool shieldDown = false, bool shieldHeld = false,
        bool down = false, bool held = false, bool up = false, Vector2 move = default)
    {
        p.SetScriptedInput(new PlayerInputFrame { shieldDown = shieldDown, shieldHeld = shieldHeld,
            attackDown = down, attackHeld = held, attackUp = up, move = move,
            hasAimDirWS = true, aimDirWS = p == defender ? Vector3.left : Vector3.right });
    }
    static void Ready()
    {
        shield.ForceStopShield(); attack.CancelAttack(); defender.GetComponent<PlayerPowerUpController>().Clear();
        Input(defender); Input(attacker);
        Set(shield, "lastUseTime", -999f); Set(shield, "nextAllowedActivateTime", -999f);
        Set(attack, "lastAttackEndTime", -999f);
        Place(attacker, new Vector3(-6,0,0)); Place(defender, new Vector3(6,0,0));
    }
    static Collider ShieldCollider() => Array.Find(defender.shield.GetComponentsInChildren<Collider>(true), c => c.CompareTag("Shield"));
    static void Parry() => Check(shield.TryParryProjectile(attacker, defender.transform.position), "Opening projectile contact confirms a parry");

    static IEnumerator Checks()
    {
        tuning = SharedSettingsRuntime.Load<PlayerTuningProfile>(); originalTuning = JsonUtility.ToJson(tuning);
        attacker = CreatePlayer(0, new Vector3(-6,0,0)); defender = CreatePlayer(2, new Vector3(6,0,0));
        shield = defender.GetComponent<PlayerShieldAbility>(); attack = defender.attackController;
        yield return .2f;
        Check(Mathf.Approximately(defender.Effective_shieldMoveMultiplier,.55f), "Shared shield movement is 55%");
        Check(shield.TapActivationCooldownSeconds == .5f && shield.HoldActivationCooldownSeconds == 2f && shield.CooldownSeconds == 4f,
            "Tap/hold recovery is 0.5s/2s; existing 4s strength recharge is preserved");
        Check(shield.DurationSeconds == .65f && shield.MaxHoldSeconds == 3f, "Opening window and maximum hold are unchanged");

        Ready(); Input(defender, shieldDown:true); yield return .04f;
        Input(defender, down:true, held:true); yield return .04f;
        Check(shield.IsActive && !attack.IsAttacking && !attack.IsCharging, "An unsuccessful tap cannot cancel into Attack");
        Input(defender); yield return shield.DurationSeconds + .04f;
        Check(!shield.IsActive && !shield.UsedSustainedHold && shield.ActivationCooldownRemaining > .3f && shield.ActivationCooldownRemaining <= .5f,
            "Early-released tap finishes its opening window and gets short recovery");
        Check(!shield.TryActivate(), "Tap recovery blocks immediate reuse");
        yield return shield.ActivationCooldownRemaining + .04f;
        Check(shield.TryActivate() && shield.CurrentStrength01 < 1, "Tap can reactivate after 0.5s while retaining partial soft-recharge strength");

        Ready(); Input(defender, shieldDown:true, shieldHeld:true); yield return shield.DurationSeconds + .08f;
        Check(shield.IsHolding && !shield.IsParryWindow, "Sustained hold becomes block-only");
        Check(!shield.TryParryProjectile(attacker, defender.transform.position) && !shield.CanParryCounter, "Late blocked hit cannot earn a counter");
        Input(defender, shieldHeld:true, down:true, held:true); yield return .04f;
        Check(shield.IsActive && !attack.IsAttacking, "Ordinary held block cannot cancel into Attack");
        Input(defender); yield return .04f;
        Check(!shield.IsActive && shield.UsedSustainedHold && shield.ActivationCooldownRemaining > 1.9f,
            "Release after opening window gets two-second recovery");
        yield return .6f;
        Check(!shield.TryActivate(), "Long-hold recovery does not use the short-tap timer");
        yield return shield.ActivationCooldownRemaining + .04f;
        Check(shield.TryActivate(), "Hold can reactivate after its full recovery");

        Ready(); Input(defender, shieldDown:true, shieldHeld:true); yield return .04f;
        float mass = defender.massScore;
        Check(!defender.ApplyExternalMassDelta(-.2f, attacker.gameObject).accepted && defender.massScore == mass,
            "Held protection still blocks external damage");
        Check(!defender.TryApplyHit(attacker.gameObject).accepted, "Held protection still blocks melee damage");
        yield return shield.MaxHoldSeconds + .04f;
        Check(!shield.IsActive && shield.UsedSustainedHold && shield.ActivationCooldownRemaining > 1.8f,
            "Maximum hold expires with long recovery");
        yield return 2.1f;
        Check(!shield.IsActive, "Continuing to hold does not automatically reactivate Shield");

        Ready(); Input(defender, shieldDown:true, shieldHeld:true); yield return .04f;
        Parry();
        Check(attacker.IsStunned && shield.CanParryCounter, "Confirmed parry keeps the original stun and enables the counter");
        Check(!attacker.GetComponent<PlayerShieldAbility>().CanParryCounter, "Counter belongs only to the defending player");
        Set(attack,"lastAttackEndTime",Time.time);
        Check(attack.CooldownRemaining > 0, "Counter fixture begins during ordinary attack cooldown");
        int stages = 0; UnityEngine.Events.UnityAction<AttackStage> stageListener = _ => stages++;
        attack.OnStageStarted.AddListener(stageListener);
        Input(defender, shieldHeld:true, down:true, held:true); yield return 0f;
        Check(attack.IsAttacking && attack.CurrentStageIndex == 0 && !attack.IsCharging,
            "Fresh Attack starts Thrust on the next input update without cooldown or charge delay");
        Check(!shield.IsActive && !shield.BlocksAllDamage && !defender.shieldOn && !defender.shield.activeSelf,
            "Counter drops all shield protection and collision before attacking");
        Check(attack.CurrentDamageMultiplier == 1 && !shield.CanParryCounter, "Counter is uncharged and consumes the earned opportunity");
        Check(shield.ActivationCooldownRemaining > .4f && !shield.UsedSustainedHold, "Early counter uses tap recovery");
        yield return .45f;
        Check(stages == 1 && !attack.IsAttacking && !attack.IsCharging, "Holding the counter button neither queues Swipe nor starts another Thrust");
        attack.OnStageStarted.RemoveListener(stageListener);

        Ready(); Input(defender, shieldDown:true, shieldHeld:true, down:true, held:true); yield return .04f;
        Parry(); Input(defender, shieldHeld:true, held:true); yield return .06f;
        Check(shield.IsActive && !attack.IsAttacking, "Attack already held before the parry does not auto-counter");
        Input(defender, shieldHeld:true); yield return .02f;
        Input(defender, shieldHeld:true, down:true, held:true); yield return 0f;
        Check(attack.IsAttacking && !shield.IsActive, "Releasing and pressing Attack creates a fresh counter edge");
        yield return .35f;

        Ready(); Input(defender, shieldDown:true, shieldHeld:true); yield return .04f;
        Parry(); yield return shield.DurationSeconds + .06f;
        Input(defender, down:true, held:true); yield return 0f;
        Check(attack.IsAttacking && !shield.IsActive && shield.UsedSustainedHold && shield.ActivationCooldownRemaining > 1.9f,
            "Counter and Shield release on the same frame preserve the earned counter and long-hold recovery");
        yield return .35f;

        Ready(); Input(defender, shieldDown:true, shieldHeld:true); yield return .04f;
        Parry(); tuning.shield.parryCounterThrust = false;
        Input(defender, shieldHeld:true, down:true, held:true); yield return .04f;
        Check(shield.IsActive && !attack.IsAttacking, "Live global counter toggle is respected");
        tuning.shield.parryCounterThrust = true;
        shield.UseSharedSettings = false; Set(shield,"tapActivationCooldownSeconds",.8f); Set(shield,"activationCooldownSeconds",1.4f);
        Check(shield.TapActivationCooldownSeconds == .8f && shield.HoldActivationCooldownSeconds == 1.4f, "Local opt-out retains authored recovery values");
        shield.UseSharedSettings = true; tuning.sharedEnabled = false;
        Check(shield.TapActivationCooldownSeconds == .8f, "Master shared-settings switch restores local recovery");
        tuning.sharedEnabled = true;
        Check(shield.TapActivationCooldownSeconds == .5f && shield.HoldActivationCooldownSeconds == 2f, "Shared re-enable restores global recovery");
        Set(shield,"tapActivationCooldownSeconds",.5f); Set(shield,"activationCooldownSeconds",2f);

        Ready(); Input(defender, down:true, held:true); yield return .04f;
        Check(attack.IsCharging && !attack.IsAttacking, "Ordinary Attack still begins a charge");
        Input(defender, up:true); yield return 0f;
        Check(attack.IsAttacking, "Ordinary charge still fires on release");
        yield return .35f;
        Ready(); Input(defender, move:Vector2.right); yield return 1f;
        float ordinarySpeed = defender.GetComponent<Rigidbody>().linearVelocity.magnitude;
        Ready(); Input(defender, shieldDown:true, shieldHeld:true, move:Vector2.right); yield return 1f;
        float speed = defender.GetComponent<Rigidbody>().linearVelocity.magnitude;
        float ratio = speed / Mathf.Max(.001f, ordinarySpeed);
        Check(ordinarySpeed > 1f && ratio > .50f && ratio < .60f,
            "Actual held locomotion is 55% of the same actor's unshielded motion ("+ratio.ToString("P1")+")");
        Input(defender, shieldHeld:true);
        float remaining = shield.HoldRemaining; Time.timeScale = 0;
        for (int i=0;i<5;i++) yield return 0f;
        Check(Mathf.Approximately(remaining,shield.HoldRemaining), "Gameplay pause freezes the hold timer");
        Time.timeScale = 1;

        Ready(); Input(defender, shieldDown:true, shieldHeld:true); yield return .04f;
        var projectileGo = new GameObject("Counter reflection fixture");
        projectileGo.transform.position = defender.shield.transform.position + Vector3.left * .35f;
        projectileGo.AddComponent<SphereCollider>().radius = .04f;
        var projectile = projectileGo.AddComponent<Massive.Enemies.EnemyProjectileBase>();
        projectile.useRigidbody = false; projectile.speed = 0; projectile.Init(null,Vector3.right);
        // Physics callbacks use this same contact resolver; the real overlap is also active.
        typeof(Massive.Enemies.EnemyProjectileBase).GetMethod("OnTriggerEnter",BindingFlags.NonPublic|BindingFlags.Instance)
            .Invoke(projectile,new object[]{ShieldCollider()});
        Check(projectile.ReflectedBy == defender && shield.CanParryCounter, "Existing enemy reflection earns a counter only in the opening parry window");
        Object.Destroy(projectileGo);

        Ready(); Input(defender, shieldDown:true, shieldHeld:true); yield return .04f;
        shield.PlayCoreParryFeedback();
        Check(shield.CanParryCounter, "Confirmed Core parry can also counter");
        var host = defender.GetComponent<PlayerPowerUpController>();
        Ready(); host.Equip(AssetDatabase.LoadAssetAtPath<DecoherencePowerUpDefinition>("Assets/Power-ups/Decoherence/PU_Decoherence.asset"));
        Input(defender, shieldDown:true, shieldHeld:true); yield return .04f;
        Check(host.TryHandleShieldImpact(attacker,ShieldCollider(),Vector3.right) &&
            shield.CanParryCounter && attacker.IsExternallyStunned, "Decoherence interception retains its effect and grants a counter");
        yield return 1.2f;

        Ready(); yield return .04f;
        host.Equip(AssetDatabase.LoadAssetAtPath<ParticleAcceleratorPowerUpDefinition>("Assets/Power-ups/Particle Accelerator/PU_ParticleAccelerator.asset"));
        yield return .08f;
        Input(defender, shieldDown:true, shieldHeld:true); yield return .04f;
        Parry(); Input(defender, shieldHeld:true, down:true, held:true); yield return .04f;
        Check(!shield.IsActive && !attack.IsAttacking && host.HasMovementAction,
            "Equipped Particle Accelerator keeps attack replacement priority after releasing a parry");
        host.Clear();

        Ready(); yield return .2f;
        float gap = PlayerScaleAdjuster.BodyRadiusOf(attacker)+PlayerScaleAdjuster.BodyRadiusOf(defender)+
            attacker.attackController.Profile.GetStage(0).TravelDistance*PlayerScaleAdjuster.ActionReachOf(attacker)*.45f;
        Place(attacker, Vector3.left*gap*.5f); Place(defender,Vector3.right*gap*.5f);
        Input(defender,shieldDown:true,shieldHeld:true);
        Input(attacker,down:true,held:true); yield return .02f;
        Input(attacker,up:true);
        float end = Time.time + 1f;
        while (!shield.CanParryCounter && Time.time<end) yield return 0f;
        Check(shield.CanParryCounter && attacker.IsStunned, "Actual sword/shield collision resolves delayed parry and earns counter");
        Input(defender,down:true,held:true); yield return 0f;
        Check(attack.IsAttacking && !shield.IsActive, "Real melee parry transitions directly into Thrust");
        yield return .4f;

        Ready(); Input(defender,shieldDown:true,shieldHeld:true); yield return .04f;
        Parry(); defender.ExternalStun(.15f); yield return .04f;
        Check(!shield.IsActive && !shield.CanParryCounter, "External stun clears the earned counter");
        yield return .2f;
        Ready(); Input(defender,shieldDown:true,shieldHeld:true); yield return .04f;
        Parry(); shield.enabled = false;
        Check(!shield.IsActive && !shield.CanParryCounter, "Disabling Shield clears protection and counter");
        shield.enabled = true;
        Ready(); Input(defender,shieldDown:true); yield return .04f;
        Parry(); yield return shield.DurationSeconds + .04f;
        Check(!shield.CanParryCounter, "Natural tap expiry clears an unused counter");
        yield return shield.ActivationCooldownRemaining + .04f;
        Input(defender,shieldDown:true); yield return .04f;
        Check(shield.IsActive && !shield.CanParryCounter, "New Shield never inherits a previous parry");
        Parry(); defender.ApplyExternalMassDelta(-2f,attacker.gameObject);
        yield return .04f;
        Check(!shield.CanParryCounter && !shield.IsActive, "Death clears shield and counter state");
        Check(JsonUtility.ToJson(tuning) == originalTuning, "Validation restores all shared tuning values");
        Check(errors.Count == 0, "No Unity runtime errors during the Shield scenarios");
    }
}
#endif
