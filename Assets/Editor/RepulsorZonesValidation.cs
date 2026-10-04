#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Demonstrations;
using Massive.Enemies;
using Massive.EditorTools;
using Massive.Player;
using Massive.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RepulsorZonesValidation
{
    const string Key = "MASSIVE.RepulsorZonesValidation.";
    const string Output = "Library/RepulsorZonesValidation";
    static readonly Stack<IEnumerator> routines = new();
    static readonly List<string> results = new(), errors = new();
    static readonly List<Object> owned = new();
    static int frame;
    static double deadline;
    static PlayerTuningProfile tuning;
    const string AttackPath = "Assets/Scripts/Player/Actions/PlayerAttackProfile.asset";
    static RepulsorZonesValidation()
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
        SessionState.SetString(Key + "Profile", File.ReadAllText(AttackPath));
        File.WriteAllText(Output + "/combat-rules.txt", PlayerRepulsorCombatValidation.RunChecks());
        SessionState.SetBool(Key + "Pending", true);
        EditorSceneManager.OpenScene("Assets/Scenes/S-T_PLAYER-ACTIONS.unity");
        // Scene edits are confined to this unsaved, isolated test scene.
        foreach (var lab in Object.FindObjectsByType<EnemyLab>(FindObjectsInactive.Include, FindObjectsSortMode.None)) lab.gameObject.SetActive(false);
        var arena = Object.FindFirstObjectByType<PlayerActionTestArena>();
        arena.enabled = true; arena.SetMode(PlayerActionTestArena.TestMode.Manual);
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
                    AssetDatabase.ImportAsset(AttackPath, ImportAssetOptions.ForceUpdate);
                    var saved = AssetDatabase.LoadAssetAtPath<PlayerAttackProfile>(AttackPath);
                    Check(Mathf.Approximately(saved.GetStage(2).RepulsorInnerRadiusPlayerMultiplier, 1.8f), "Saved inner radius survives Play Mode exit and asset reimport");
                }
            }
            catch (Exception e) { passed = false; results.Add(e.ToString()); }
            finally
            {
                File.WriteAllText(AttackPath, SessionState.GetString(Key + "Profile", ""));
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
        Debug.Log("REPULSOR ZONES VALIDATION " + (passed ? "PASSED" : "FAILED"));
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
    static void SetAuto(object target, string property, object value) => Set(target, "<" + property + ">k__BackingField", value);
    static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    static void Near(float actual, float expected, string label) => Check(Mathf.Abs(actual - expected) < .0002f, label + $" ({actual:0.####} / {expected:0.####})");
    static IEnumerator Seconds(float duration) { float end = Time.time + duration; while (Time.time < end) yield return null; }
    static PlayerControllerScript Actor(Transform scope, string name, Vector3 position, int team)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), scope);
        go.name = name; go.transform.position = position;
        var actor = go.GetComponent<PlayerControllerScript>(); actor.ConfigureDemonstration(scope, team - 1, team);
        var scale = actor.GetComponent<PlayerScaleAdjuster>(); scale.UseGlobalModifiers = false; scale.Size = 1f;
        var rb = actor.GetComponent<Rigidbody>(); rb.useGravity = false; rb.constraints = RigidbodyConstraints.FreezeAll;
        Set(actor, "massLossPerHit", .08f); Set(actor, "_invulnUntil", -999f); Set(actor, "_timeOfLastShrink", -999f);
        return actor;
    }
    static EnemyBase Enemy(Transform scope, PlayerControllerScript source, EnemyDefinition definition, Vector3 position)
    {
        var go = new GameObject("Repulsor NPC fixture"); go.SetActive(false); go.transform.SetParent(scope); go.transform.position = position;
        var enemy = go.AddComponent<EnemyBase>(); enemy.ConfigureDemonstration(scope, source); enemy.Init(definition, null);
        var rb = go.AddComponent<Rigidbody>(); rb.useGravity = false; rb.isKinematic = true;
        var body = go.AddComponent<SphereCollider>(); body.radius = .2f; body.isTrigger = true;
        go.AddComponent<EnemyHurtbox>(); go.SetActive(true); return enemy;
    }
    static IEnumerator Pulse(PlayerAttackController attack, PlayerRepulsorAOE pulse)
    {
        attack.CancelAttack(); Call(attack, "StartStage", 2);
        yield return Until(() => pulse.IsPulseActive, "Repulsor activation");
    }
    static void ResetVictim(PlayerControllerScript victim)
    { victim.massScore = .8f; Set(victim, "_timeOfLastShrink", -999f); Set(victim, "_invulnUntil", -999f); }
    static IEnumerator Checks()
    {
        Check(tuning.repulsor.applyMassLoss && Mathf.Approximately(tuning.repulsor.massLossScale01, 1f), "Shared Repulsor enables one normal outer player hit");
        var stage = tuning.attackProfile.GetStage(2);
        Near(stage.RepulsorInnerRadiusPlayerMultiplier, 1.5f, "Inner radius default is 150% of body radius");
        Near(stage.RepulsorInnerDamageMultiplier, 2f, "Inner damage default is doubled");
        foreach (float size in new[] { .5f, 1f, 2f })
            Near(stage.GetRepulsorInnerRadius(.5f * size, 3f * size), .75f * size, "Inner zone scales with body at size " + size);
        Near(stage.GetRepulsorInnerRadius(.5f, .3f), .3f, "Inner radius cannot exceed reduced outer reach");
        var lab = Object.FindFirstObjectByType<PlayerActionTestArena>(); lab.SetMode(PlayerActionTestArena.TestMode.Manual);
        var root = new GameObject("Repulsor damage zones fixture"); owned.Add(root); root.SetActive(false);
        Vector3 origin = new Vector3(1000, 0, 1000);
        var source = Actor(root.transform, "Source", origin, 1);
        var innerPlayer = Actor(root.transform, "Inner target", origin + Vector3.right, 2);
        var outerPlayer = Actor(root.transform, "Outer target", origin + Vector3.left * 2.5f, 2);
        var attack = source.attackController;
        var profile = Object.Instantiate(tuning.attackProfile); owned.Add(profile);
        attack.UseSharedSettings = false; Set(attack, "attackProfile", profile);
        Set(attack, "attackCooldown", 0f); Set(attack, "lockOnEnabled", false);
        var pulse = source.GetComponentInChildren<PlayerRepulsorAOE>(true);
        pulse.UseSharedSettings = false; Set(pulse, "applyKnockback", false);
        Set(pulse, "applyMassLoss", true); Set(pulse, "massLossScale01", 1f);
        var definition = ScriptableObject.CreateInstance<EnemyDefinition>(); owned.Add(definition);
        definition.healthMassEq = 100f; definition.defeatRewardKey = "";
        var innerEnemy = Enemy(root.transform, source, definition, origin + Vector3.forward * .9f);
        var outerEnemy = Enemy(root.transform, source, definition, origin + Vector3.back * 2.5f);
        var beyondEnemy = Enemy(root.transform, source, definition, origin + Vector3.forward * 3.3f);
        root.SetActive(true); yield return Seconds(.1f);
        ResetVictim(innerPlayer); ResetVictim(outerPlayer);
        float releaseRadius = -1f, releaseTime = 0f;
        pulse.PulseStarted += value => { releaseRadius = value.RadiusWorld; releaseTime = Time.time; };
        yield return Pulse(attack, pulse);
        Near(releaseRadius, pulse.StartRadiusWorld, "Damage begins exactly at the captured player outline");
        Check(pulse.RadiusWorld < pulse.EndRadiusWorld * .3f, "First live frame has not jumped to full radius");
        Near(pulse.ActiveInnerRadiusWorld, Mathf.Min(pulse.RadiusWorld, pulse.InnerRadiusWorld), "Red live guide is clipped to the reached damage area");
        Near(outerEnemy.HealthRemaining, 100f, "Distant NPC takes no damage on release");
        Near(outerPlayer.massScore, .8f, "Distant player takes no damage on release");
        Near(pulse.InnerRadiusWorld, PlayerScaleAdjuster.BodyRadiusOf(source) * 1.5f, "Live inner radius uses physical body, not contracted outline");
        Near(pulse.EndRadiusWorld, 3f, "Outer radius retains configured maximum reach");
        Check(pulse.GetComponent<PlayerRepulsorRangeRings>() && pulse.ShowDamageRings, "Runtime damage circles automatically bind to existing prefab");
        Check(!source.GetComponentInChildren<PlayerMelee>(true).GetComponent<Collider>().enabled, "Sword hitbox stays disabled during Repulsor so it cannot consume the damage cooldown");
        Capture(pulse, "expansion-start.png");
        float previousRadius = pulse.RadiusWorld;
        yield return Until(() => pulse.ActiveProgress01 >= .4f, "Mid-expansion frame");
        Check(pulse.RadiusWorld > previousRadius && pulse.RadiusWorld < pulse.EndRadiusWorld, "Damage visibly grows through an intermediate radius");
        Near(outerEnemy.HealthRemaining, 100f, "Distant NPC remains untouched until the expanding front reaches it");
        Near(outerPlayer.massScore, .8f, "Distant player remains untouched until the expanding front reaches it");
        Capture(pulse, "expansion-middle.png");
        yield return Until(() => Mathf.Approximately(pulse.RadiusWorld, pulse.EndRadiusWorld), "Full-size frame before expiration");
        Check(pulse.IsPulseActive && pulse.ActiveProgress01 < .99f, "Full radius is reached before the damage window disappears");
        Check(Time.time - releaseTime >= .17f && Time.time - releaseTime <= .23f, "Default expansion reaches maximum in approximately 0.2 seconds");
        Near(pulse.ActiveInnerRadiusWorld, pulse.InnerRadiusWorld, "Inner damage boundary stops at its configured maximum");
        Capture(pulse, "damage-circles.png");
        yield return Until(() => !pulse.IsPulseActive, "First pulse completion");
        Near(pulse.RadiusWorld, 0f, "Live outer damage guide disappears after the active window");
        Near(pulse.ActiveInnerRadiusWorld, 0f, "Live inner damage guide disappears after the active window");
        Near(innerPlayer.massScore, .64f, "Actual inner player contact deals exactly two normal hits");
        Near(outerPlayer.massScore, .72f, "Actual outer player contact deals one full hit without distance falloff");
        Near(innerEnemy.HealthRemaining, 98f, "Actual inner NPC contact deals 2x configured damage");
        Near(outerEnemy.HealthRemaining, 99f, "Actual outer NPC contact deals 1x configured damage");
        Near(beyondEnemy.HealthRemaining, 100f, "Target beyond outer boundary is untouched");
        Check(!pulse.GetComponent<SphereCollider>().enabled, "Pulse completion disables damage and circles");

        // Boundary and compound-body cases deliberately reverse callback ordering.
        yield return Pulse(attack, pulse);
        var edge = Enemy(root.transform, source, definition, pulse.OriginWorld + Vector3.forward * (pulse.InnerRadiusWorld + .2f));
        var justOutside = Enemy(root.transform, source, definition, pulse.OriginWorld + Vector3.back * (pulse.InnerRadiusWorld + .205f));
        var compound = Enemy(root.transform, source, definition, pulse.OriginWorld + Vector3.right * .8f);
        var far = new GameObject("Far body receives callback first"); far.transform.SetParent(compound.transform, false); far.transform.localPosition = Vector3.right;
        var farCollider = far.AddComponent<SphereCollider>(); farCollider.radius = .1f; farCollider.isTrigger = true; far.AddComponent<EnemyHurtbox>();
        Physics.SyncTransforms();
        Call(pulse, "TryHit", edge.GetComponent<Collider>()); Call(pulse, "TryHit", justOutside.GetComponent<Collider>());
        Call(pulse, "TryHit", farCollider); Call(pulse, "TryHit", compound.GetComponent<Collider>()); Call(pulse, "TryHit", farCollider);
        Near(edge.HealthRemaining, 98f, "Body touching inner boundary receives double damage");
        Near(justOutside.HealthRemaining, 99f, "Body just outside inner boundary receives normal damage");
        Near(compound.HealthRemaining, 98f, "Multiple hurtboxes select nearest body and receive only one hit regardless of callback order");
        Vector3 release = pulse.OriginWorld; float capturedInner = pulse.InnerRadiusWorld;
        source.transform.position += Vector3.forward * 5f; Physics.SyncTransforms(); yield return null;
        Near(Vector3.Distance(release, pulse.OriginWorld), 0f, "Damage circles and zones remain at release origin as player moves");
        Near(pulse.InnerRadiusWorld, capturedInner, "Inner radius stays fixed for the pulse");
        float untouched = beyondEnemy.HealthRemaining;
        attack.CancelAttack(); Call(pulse, "TryHit", beyondEnemy.GetComponent<Collider>());
        Near(beyondEnemy.HealthRemaining, untouched, "Cancellation cannot apply late or final-expansion damage");
        Check(!pulse.IsPulseActive && !pulse.GetComponent<SphereCollider>().enabled, "Cancellation clears collider and range rings");
        source.transform.position = origin; source.GetComponent<Rigidbody>().position = origin; Physics.SyncTransforms();
        Object.Destroy(edge.gameObject); Object.Destroy(justOutside.gameObject); Object.Destroy(compound.gameObject); yield return null;

        ResetVictim(innerPlayer); ResetVictim(outerPlayer);
        innerPlayer.SetScriptedInput(new PlayerInputFrame { shieldDown = true, shieldHeld = true }); yield return null;
        innerPlayer.SetScriptedInput(new PlayerInputFrame { shieldHeld = true });
        yield return Seconds(innerPlayer.GetComponent<PlayerShieldAbility>().DurationSeconds + .05f);
        Check(innerPlayer.GetComponent<PlayerShieldAbility>().IsHolding && !innerPlayer.GetComponent<PlayerShieldAbility>().IsParryWindow, "Shield fixture is holding after its opening parry window");
        yield return Pulse(attack, pulse); yield return Until(() => !pulse.IsPulseActive, "Shielded pulse completion");
        Near(innerPlayer.massScore, .8f, "Held shield blocks the amplified inner hit completely");
        Near(outerPlayer.massScore, .72f, "Unshielded outer player still receives normal damage on a later pulse");
        innerPlayer.GetComponent<PlayerShieldAbility>().ForceStopShield(); innerPlayer.ClearScriptedInput();
        ResetVictim(innerPlayer); ResetVictim(outerPlayer); Set(innerPlayer, "_invulnUntil", Time.time + 10f);
        yield return Pulse(attack, pulse); yield return Until(() => !pulse.IsPulseActive, "Invulnerable pulse completion");
        Near(innerPlayer.massScore, .8f, "Respawn invulnerability still rejects amplified damage");
        ResetVictim(innerPlayer); innerPlayer.teamID = source.teamID;
        yield return Pulse(attack, pulse); yield return Until(() => !pulse.IsPulseActive, "Friendly pulse completion");
        Near(innerPlayer.massScore, .8f, "Same-team player remains immune"); innerPlayer.teamID = 2;

        // Amplified damage remains one accepted transaction, with existing cooldown/death attribution.
        ResetVictim(innerPlayer); int hits = 0; PlayerControllerScript killer = null;
        innerPlayer.HitAccepted += _ => hits++;
        innerPlayer.DeathResolved += context => killer = context.killer;
        innerPlayer.massScore = .12f;
        var lethal = innerPlayer.TryApplyHit(source.gameObject, 1f, 2f);
        Check(lethal.accepted && lethal.causedDeath && killer == source && hits == 1, "Doubled lethal hit is one transaction with correct killer attribution");
        Check(!innerPlayer.TryApplyHit(source.gameObject, 1f, 2f).accepted, "Further damage to eliminated player is rejected");
        ResetVictim(outerPlayer);
        Check(outerPlayer.TryApplyHit(source.gameObject, 1f, 2f).accepted && !outerPlayer.TryApplyHit(source.gameObject, 1f, 2f).accepted, "Amplified hit retains repeat-hit cooldown");
        Object.Destroy(root); yield return null;

        lab.SetMode(PlayerActionTestArena.TestMode.AllDemos);
        PlayerDemoGallery gallery = null;
        yield return Until(() => (gallery = Object.FindFirstObjectByType<PlayerDemoGallery>()) && gallery.IsPlaying, "Gallery start");
        yield return Until(() => gallery.Demos.All(d => d.SuccessfulLoops >= 2 || d.LastFailure != null), "Seven demo loops");
        foreach (var demo in gallery.Demos) Check(demo.SuccessfulLoops >= 2 && demo.LastFailure == null, "Two successful loops: " + demo.name);
        lab.SetMode(PlayerActionTestArena.TestMode.Manual); yield return null;
        using (var data = new SerializedObject(tuning.attackProfile))
        {
            data.FindProperty("stages").GetArrayElementAtIndex(2).FindPropertyRelative("repulsorInnerRadiusPlayerMultiplier").floatValue = 1.8f;
            data.ApplyModifiedProperties(); PlayerTuningEditing.Save(tuning.attackProfile);
        }
        var snapshot = PlayerTuningSnapshot.Capture();
        Check(Mathf.Approximately(snapshot.attacks.GetStage(2).RepulsorInnerRadiusPlayerMultiplier, 1.8f), "Persistent tuning snapshot includes the inner radius");
        snapshot.ReleaseTemporary();
    }
    static void Capture(PlayerRepulsorAOE pulse, string name)
    {
        var go = new GameObject("Repulsor zone capture camera"); var camera = go.AddComponent<Camera>(); camera.enabled = false;
        camera.cameraType = CameraType.Preview; camera.orthographic = true; camera.orthographicSize = 3.5f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .03f, .045f); camera.allowHDR = false;
        camera.transform.SetPositionAndRotation(pulse.OriginWorld + Vector3.up * 20f, Quaternion.LookRotation(Vector3.down, Vector3.forward));
        var target = new RenderTexture(896, 896, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
        var pixels = new Texture2D(896, 896, TextureFormat.RGBA32, false); var previous = RenderTexture.active;
        try
        {
            pulse.GetComponent<PlayerRepulsorRangeRings>().DrawShapes(camera); camera.Render();
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 896, 896), 0, 0); pixels.Apply();
            File.WriteAllBytes(Output + "/" + name, pixels.EncodeToPNG());
            int red = 0, yellow = 0;
            foreach (var color in pixels.GetPixels32())
            { if (color.r > 170 && color.g < 60 && color.b < 60) red++; if (color.r > 170 && color.g > 170 && color.b < 60) yellow++; }
            bool separateOuter = pulse.RadiusWorld > pulse.InnerRadiusWorld + pulse.DamageRingDotRadiusWorld * 2f;
            Check(red > 100 && (separateOuter ? yellow > 100 : yellow < 10), $"Rendered live tuning dots at radius {pulse.RadiusWorld:0.###}: {name} ({red} / {yellow} pixels)");
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(go); }
    }
}
#endif
