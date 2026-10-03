#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Demonstrations;
using Massive.EditorTools;
using Massive.Player;
using Massive.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ThrustSteeringValidation
{
    const string Key = "MASSIVE.ThrustSteeringValidation.";
    const string Output = "Library/ThrustSteeringValidation";
    static readonly Stack<IEnumerator> routines = new();
    static readonly List<string> results = new(), errors = new();
    static readonly List<Object> owned = new();
    static int frame;
    static double deadline;
    static PlayerTuningProfile tuning;
    static ThrustSteeringValidation()
    {
        EditorApplication.update += BeginWhenReady;
        EditorApplication.playModeStateChanged += StateChanged;
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Run in an isolated batch project copy.");
        SessionState.SetBool(Key + "Start", true);
    }
    static void BeginWhenReady()
    {
        if (!SessionState.GetBool(Key + "Start", false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        SessionState.SetBool(Key + "Start", false);
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/report.txt", "RUNNING\n");
        SessionState.SetString(Key + "Profile", File.ReadAllText(PlayerTuningEditing.AssetPath));
        SessionState.SetBool(Key + "Pending", true);
        EditorSceneManager.OpenScene("Assets/Scenes/S-T_PLAYER-ACTIONS.unity");
        // Scene edits are confined to this unsaved, isolated test scene.
        foreach (var lab in Object.FindObjectsByType<EnemyLab>(FindObjectsInactive.Include, FindObjectsSortMode.None)) lab.gameObject.SetActive(false);
        EditorApplication.isPlaying = true;
    }
    static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Pending", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Time.captureDeltaTime = .01f;
            tuning = SharedSettingsRuntime.Load<PlayerTuningProfile>();
            deadline = EditorApplication.timeSinceStartup + 240;
            results.Clear(); errors.Clear(); owned.Clear(); routines.Clear(); routines.Push(Checks()); frame = 0;
            Application.logMessageReceived += Log;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool passed = SessionState.GetBool(Key + "Passed", false);
            try
            {
                if (passed)
                {
                    AssetDatabase.ImportAsset(PlayerTuningEditing.AssetPath, ImportAssetOptions.ForceUpdate);
                    var saved = AssetDatabase.LoadAssetAtPath<PlayerTuningProfile>(PlayerTuningEditing.AssetPath);
                    Check(Mathf.Approximately(saved.combat.thrustMaxTurnDegrees, 22f), "Saved steering edit survives Play Mode exit and asset reimport");
                }
            }
            catch (Exception e) { passed = false; results.Add(e.ToString()); }
            finally
            {
                File.WriteAllText(PlayerTuningEditing.AssetPath, SessionState.GetString(Key + "Profile", ""));
                SessionState.SetBool(Key + "Pending", false);
                AssetDatabase.Refresh();
            }
            WriteReport(passed ? "PASSED" : "FAILED");
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routines.Count == 0 || Time.frameCount < frame) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out.");
            while (routines.Count > 0)
            {
                var routine = routines.Peek();
                if (!routine.MoveNext()) { routines.Pop(); continue; }
                if (routine.Current is IEnumerator nested) { routines.Push(nested); continue; }
                frame = Time.frameCount + 1; return;
            }
            Finish(true);
        }
        catch (Exception e) { results.Add(e.ToString()); Finish(false); }
    }
    static void Finish(bool passed)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log; routines.Clear();
        passed &= errors.Count == 0;
        foreach (var item in owned) if (item) Object.Destroy(item);
        owned.Clear();
        WriteReport(passed ? "PASSED" : "FAILED");
        Debug.Log("THRUST STEERING VALIDATION " + (passed ? "PASSED" : "FAILED"));
        SessionState.SetBool(Key + "Passed", passed);
        Time.captureDeltaTime = 0; EditorApplication.isPlaying = false;
    }
    static void WriteReport(string status) => File.WriteAllText(Output + "/report.txt", status + "\n" + string.Join("\n", results) + "\n" + string.Join("\n", errors));
    static void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); results.Add("PASS " + label); WriteReport("RUNNING"); }
    static IEnumerator Until(Func<bool> predicate, string label)
    {
        float end = Time.time + 40;
        while (!predicate() && Time.time < end) yield return null;
        if (!predicate()) throw new Exception("Timed out: " + label);
    }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    static Vector3 Aim(float degrees) => Quaternion.AngleAxis(degrees, Vector3.up) * Vector3.right;
    static float Angle(Vector3 start, Vector3 direction) => Vector3.SignedAngle(start, direction, Vector3.up);
    static PlayerControllerScript Actor(Transform parent, string name, Vector3 position, int team)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab");
        var go = Object.Instantiate(prefab, parent); go.name = name; go.transform.position = position;
        var actor = go.GetComponent<PlayerControllerScript>(); actor.ConfigureDemonstration(parent, team - 1, team);
        var rb = actor.GetComponent<Rigidbody>(); rb.useGravity = false; rb.constraints = RigidbodyConstraints.FreezeAll;
        return actor;
    }
    static void Input(PlayerControllerScript actor, Vector3 direction, bool press = false) =>
        actor.SetScriptedInput(new PlayerInputFrame { moveInput = new Vector2(direction.x, direction.z), attackDown = press });
    static IEnumerator Checks()
    {
        Check(Mathf.Approximately(tuning.combat.thrustMaxTurnDegrees, 15), "Project default is 15 degrees per side");
        var lab = Object.FindFirstObjectByType<PlayerActionTestArena>(); lab.SetMode(PlayerActionTestArena.TestMode.Manual);
        var root = new GameObject("Thrust steering fixture"); owned.Add(root); root.SetActive(false);
        Vector3 origin = new Vector3(1000, 0, 1000);
        var actor = Actor(root.transform, "Attacker", origin, 1);
        var rear = Actor(root.transform, "Rear target", origin + Vector3.left * 1.2f, 2);
        var attack = actor.attackController;
        var visual = actor.GetComponentInChildren<PlayerVisualController>(true);
        var sword = actor.GetComponentInChildren<PlayerMelee>(true).GetComponent<Collider>();
        var profile = Object.Instantiate(tuning.attackProfile); owned.Add(profile);
        Set(profile.GetStage(0), "duration", 3f); Set(profile.GetStage(0), "travelDistance", 0f);
        Set(profile.GetStage(0), "activationFrameStart", 0f); Set(profile.GetStage(0), "activationFrameEnd", 180f);
        Set(profile.GetStage(0), "customComboWindow", true); Set(profile.GetStage(0), "comboWindowStartSeconds", 0f);
        Set(profile.GetStage(0), "comboWindowEndSeconds", 3f); Set(profile.GetStage(0), "earlyComboHandoff", true);
        Set(profile.GetStage(0), "comboHandoffSeconds", .9f);
        attack.UseSharedSettings = false; Set(attack, "attackProfile", profile);
        Set(attack, "attackCooldown", 0f); Set(attack, "lockOnEnabled", false);
        root.SetActive(true); yield return null; yield return null;
        Input(actor, Vector3.right, true);
        yield return Until(() => attack.IsAttacking && sword.enabled, "Thrust hit window");
        Vector3 start = attack.CurrentAttackDirectionWS;
        Check(Vector3.Angle(start, Vector3.right) < .1f, "Thrust captures its initial heading");
        float rearMass = rear.massScore;
        foreach (float requested in new[] { 10f, -10f, 15f, -15f, 70f, -70f, 180f })
        {
            Input(actor, Aim(requested)); yield return null; yield return null; yield return null;
            float expected = Mathf.Clamp(requested, -15, 15);
            float actual = Angle(start, sword.transform.right);
            float error = Mathf.Abs(requested) == 180f ? Mathf.Abs(Mathf.Abs(actual) - 15f) : Mathf.Abs(actual - expected);
            Check(attack.IsAttacking && sword.enabled && error < .15f,
                $"Live input {requested} degrees produces hitbox heading {actual:F2} degrees");
            Check(Vector3.Angle(attack.CurrentAttackVisualDirectionWS, sword.transform.right) < .1f,
                $"Attack VFX and damage facing agree for {requested}-degree input");
        }
        Check(Mathf.Approximately(rear.massScore, rearMass), "A full reversal cannot damage the target behind the attacker");
        // Place the same target inside the actual collider to prove it is damageable.
        rear.transform.position = sword.bounds.center;
        Physics.SyncTransforms(); yield return null; yield return null; yield return null; yield return null;
        Check(rear.massScore < rearMass, "The same active Thrust still damages a target inside its allowed hitbox");
        rear.gameObject.SetActive(false);
        for (int i = 0; i < 6; i++)
        {
            Input(actor, Quaternion.AngleAxis(10, Vector3.up) * sword.transform.right);
            yield return null; yield return null;
        }
        Check(Mathf.Abs(Angle(start, sword.transform.right)) <= 15.1f && Vector3.Angle(start, attack.CurrentAttackDirectionWS) < .1f,
            "Repeated small turns cannot move the cone center");
        actor.SetScriptedInput(new PlayerInputFrame { hasAimDirWS = true, aimDirWS = Vector3.left });
        yield return null; yield return null;
        Check(Mathf.Abs(Angle(start, sword.transform.right)) <= 15.1f, "Explicit scripted aim obeys the same limit");
        visual.gameplayFacingInstant = false;
        visual.gameplayFacing.rotation = Quaternion.AngleAxis(170, Vector3.up);
        visual.SetAimDirection(Vector3.left);
        Check(Mathf.Abs(Angle(start, sword.transform.right)) <= 15.1f, "Smoothed combat facing cannot escape the cone");
        visual.gameplayFacingInstant = true;
        attack.CancelAttack(true); Input(actor, Vector3.left); yield return null; yield return null;
        Check(!attack.IsAttacking && Vector3.Angle(sword.transform.right, Vector3.left) < .1f, "Cancellation restores unrestricted facing");
        Input(actor, Aim(170), true); yield return Until(() => attack.IsAttacking, "New Thrust");
        start = attack.CurrentAttackDirectionWS;
        Input(actor, Aim(-170)); yield return null; yield return null;
        Check(Mathf.Abs(Angle(start, sword.transform.right) - 15) < .15f, "A new Thrust captures a new center and clamps correctly across the 180-degree seam");
        Set(attack, "thrustMaxTurnDegrees", 0f); yield return null; yield return null;
        Check(Vector3.Angle(start, sword.transform.right) < .1f, "Zero-degree local setting locks Thrust facing");
        Set(attack, "thrustMaxTurnDegrees", 180f); Input(actor, -start); yield return null; yield return null;
        Check(Vector3.Angle(-start, sword.transform.right) < .1f, "180-degree local setting restores unrestricted turning");
        attack.UseSharedSettings = true; tuning.combat.thrustMaxTurnDegrees = 7f;
        yield return null; yield return null;
        Check(Mathf.Abs(Angle(start, sword.transform.right)) <= 7.1f, "Live shared tuning overrides local steering settings");
        tuning.combat.thrustMaxTurnDegrees = 15f; attack.UseSharedSettings = false; Set(attack, "thrustMaxTurnDegrees", 15f);
        Input(actor, Vector3.forward, true);
        yield return Until(() => attack.CurrentStageIndex == 1, "Sweep handoff");
        Input(actor, Vector3.left); yield return null; yield return null;
        Check(attack.CurrentStage.StageType == AttackStageType.ComboSwipe && Vector3.Angle(sword.transform.right, Vector3.left) < .1f,
            "Sweep accepts a new unrestricted facing after Thrust");
        attack.CancelAttack(true); Input(actor, Vector3.right, true);
        yield return Until(() => attack.IsAttacking, "Final Thrust");
        // Natural completion: no combo request this time.
        Input(actor, Vector3.left);
        yield return Until(() => !attack.IsAttacking, "Natural Thrust completion");
        yield return null; yield return null;
        Check(Vector3.Angle(sword.transform.right, Vector3.left) < .1f, "Natural completion restores the latest requested direction");
        Object.Destroy(root); yield return null;
        lab.SetMode(PlayerActionTestArena.TestMode.AllDemos);
        PlayerDemoGallery gallery = null;
        yield return Until(() => (gallery = Object.FindFirstObjectByType<PlayerDemoGallery>()) && gallery.IsPlaying, "Gallery start");
        yield return Until(() => gallery.Demos.All(d => d.SuccessfulLoops >= 2 || d.LastFailure != null), "Seven demo loops");
        Check(gallery.Demos.All(d => d.SuccessfulLoops >= 2 && d.LastFailure == null), "All seven real attack/parry/projectile demos pass at least two loops");
        lab.SetMode(PlayerActionTestArena.TestMode.Manual); yield return null;
        using (var data = new SerializedObject(tuning))
        {
            data.FindProperty("combat").FindPropertyRelative("thrustMaxTurnDegrees").floatValue = 22f;
            data.ApplyModifiedProperties(); PlayerTuningEditing.Save(tuning);
        }
        var snapshot = PlayerTuningSnapshot.Capture();
        Check(Mathf.Approximately(snapshot.tuning.combat.thrustMaxTurnDegrees, 22f), "Player Tuning snapshots include the steering limit");
        snapshot.ReleaseTemporary();
    }
}
#endif
