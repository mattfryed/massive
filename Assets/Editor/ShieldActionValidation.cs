#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Play-mode regression checks using the canonical PlayerActor and actual scripted input.</summary>
[InitializeOnLoad]
public static class ShieldActionValidation
{
    const string Key = "MASSIVE.ShieldValidation.";
    static IEnumerator run;
    static float resumeAt;
    static int resumeFrame, passed;
    static Camera camera;
    static readonly System.Collections.Generic.List<string> results = new();
    static ShieldActionValidation()
    {
        EditorApplication.playModeStateChanged += StateChanged;
        EditorApplication.update += BeginBatchWhenReady;
    }
    static void BeginBatchWhenReady()
    {
        if (!SessionState.GetBool(Key + "BatchStart", false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        SessionState.SetBool(Key + "BatchStart", false);
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/S-T_PLAYER-ACTIONS.unity");
            Start();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new Exception("RunBatch is only for an isolated batch editor.");
        SessionState.SetBool(Key + "BatchStart", true);
    }

    [MenuItem("MASSIVE/Demonstrations/Validate Shield Actions")]
    public static void Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new Exception("Wait for Edit Mode and compilation before validating.");
        var previous = SceneManager.GetActiveScene();
        string path = "Assets/ShieldValidation-" + Guid.NewGuid().ToString("N") + ".unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var go = new GameObject("Shield validation camera"); go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = 3.2f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.07f, .09f, .12f);
            go.transform.position = new Vector3(0, 12, 0); go.transform.rotation = Quaternion.Euler(90, 0, 0);
            go.AddComponent<AudioListener>();
            if (!EditorSceneManager.SaveScene(scene, path)) throw new Exception("Cannot save validation fixture.");
        }
        finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(previous); }
        SessionState.SetString(Key + "Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + "Scene", path); SessionState.SetBool(Key + "Pending", true);
        SessionState.SetBool(Key + "Passed", false);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.isPlaying = true;
    }

    static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Pending", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetBool(Key + "Background", Application.runInBackground);
            Application.runInBackground = true; camera = Camera.main;
            passed = 0; results.Clear(); resumeAt = 0; resumeFrame = 0;
            run = Checks(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; run = null;
            Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Previous", ""));
            string path = SessionState.GetString(Key + "Scene", "");
            if (path.StartsWith("Assets/ShieldValidation-", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(path);
            SessionState.SetBool(Key + "Pending", false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Key + "Passed", false) ? 0 : 1);
        }
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || run == null || Time.time < resumeAt || Time.frameCount < resumeFrame) return;
        try
        {
            if (run.MoveNext()) { resumeAt = Time.time + (run.Current is float f ? f : 0f); resumeFrame = Time.frameCount + 1; return; }
            Finish(true, passed + " shield checks passed.");
        }
        catch (Exception e) { Finish(false, "FAILED after " + passed + " checks: " + e); }
    }
    static void Finish(bool success, string report)
    {
        EditorApplication.update -= Tick; run = null;
        SessionState.SetBool(Key + "Passed", success);
        Directory.CreateDirectory("Library/ShieldValidation");
        File.WriteAllText("Library/ShieldValidation/report.txt", report + "\n" + string.Join("\n", results));
        Debug.Log("[Shield Validation] " + report); EditorApplication.isPlaying = false;
    }
    static void Check(bool value, string label)
    { if (!value) throw new Exception(label); passed++; results.Add("PASS " + label); }
    static void Set(object target, string field, object value)
    { target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value); }
    static void ReadyShield(PlayerShieldAbility shield)
    { shield.ForceStopShield(); Set(shield, "nextAllowedActivateTime", -999f); Set(shield, "lastUseTime", -999f); }
    static void ShieldInput(PlayerControllerScript player, bool press, bool hold)
    { player.SetScriptedInput(new PlayerInputFrame { shieldDown = press, shieldHeld = hold, hasAimDirWS = true, aimDirWS = Vector3.left }); }
    static void Place(PlayerControllerScript p, Vector3 pos)
    {
        p.transform.position = pos;
        var rb = p.GetComponent<Rigidbody>(); rb.position = pos; rb.linearVelocity = Vector3.zero;
        if (p.visualsController && p.visualsController.visuals) p.visualsController.visuals.position = pos;
        Physics.SyncTransforms();
    }
    static PlayerControllerScript Player(int id, int team, Vector3 pos)
    {
        var root = new GameObject("Shield test actor " + id); root.SetActive(false);
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), root.transform);
        var p = actor.GetComponent<PlayerControllerScript>(); p.playerID = id; p.teamID = team;
        p.SetControlMode(PlayerControlMode.Scripted); Set(p, "playMatchSpawnOnSceneLoad", false);
        var rb = p.GetComponent<Rigidbody>(); rb.useGravity = false;
        root.SetActive(true); Place(p, pos); return p;
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Library/ShieldValidation");
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1200, 700, 24); var texture = new Texture2D(1200, 700, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1200, 700), 0, 0); texture.Apply();
            File.WriteAllBytes("Library/ShieldValidation/" + name + ".png", texture.EncodeToPNG());
        }
        finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; RenderTexture.ReleaseTemporary(target); Object.Destroy(texture); }
    }

    static IEnumerator Checks()
    {
        var a = Player(0, 1, new Vector3(-2, 0, 0)); var d = Player(2, 2, new Vector3(2, 0, 0));
        var shield = d.GetComponent<PlayerShieldAbility>();
        var nuggets = d.GetComponentInChildren<PlayerNuggetsGPU>(true);
        yield return .2f;
        Check(Mathf.Approximately(shield.MaxHoldSeconds, 3f), "Canonical actor defaults to a 3-second hold");
        Capture("baseline");
        ShieldInput(d, true, false); yield return .04f;
        Check(shield.IsActive && shield.IsParryWindow && !shield.IsHolding, "Tap enters original parry window");
        yield return shield.DurationSeconds + .06f;
        Check(!shield.IsActive, "Tap expires at original duration");

        ReadyShield(shield); ShieldInput(d, true, true); yield return .04f;
        float holdStarted = Time.time - .04f;
        Check(shield.IsHolding && shield.IsParryWindow, "Held press retains opening parry");
        float mass = d.massScore;
        Check(!d.TryApplyHit(a.gameObject).accepted && Mathf.Approximately(mass, d.massScore), "Held shield rejects melee damage transaction");
        Check(!d.ApplyExternalMassDelta(-.1f, a.gameObject).accepted && Mathf.Approximately(mass, d.massScore), "Held shield rejects external hazard/projectile drain");
        d.ApplyExternalMassDelta(.01f, false);
        Check(d.massScore > mass, "Held shield still permits positive mass gain");
        yield return shield.DurationSeconds + .08f;
        Check(shield.IsHolding && !shield.IsParryWindow, "Hold becomes block-only after opening window");
        Check(!shield.TryParryProjectile(a, d.transform.position) && !a.IsStunned, "Late projectile contact does not stun attacker");
        Check(!shield.QueueMeleeParry(a, null) && !a.IsStunned, "Late melee contact cannot register a parry");
        mass = d.massScore; d.ApplyExternalMassDelta(-.12f, false);
        Check(Mathf.Approximately(mass, d.massScore), "Legacy external-damage overload also respects held block");
        var projectileObject = new GameObject("Held shield projectile check");
        projectileObject.transform.position = a.transform.position;
        var projectile = projectileObject.AddComponent<Massive.PowerUps.ParticleAcceleratorProjectile>();
        float attackerMass = a.massScore;
        projectile.Init(a, Vector3.right, 60f, 20f, .04f, .1f, true, ~0);
        yield return .18f;
        Check(Mathf.Approximately(mass, d.massScore) && Mathf.Approximately(attackerMass, a.massScore) && !a.IsStunned,
            "Actual late projectile is blocked with no stun or unearned mass transfer");
        if (projectileObject) Object.Destroy(projectileObject);
        Capture("held-middle");
        yield return Mathf.Max(0f, holdStarted + shield.MaxHoldSeconds - Time.time + .08f);
        Check(!shield.IsActive && !shield.BlocksAllDamage, "Protection expires after maximum hold");
        yield return shield.ActivationCooldownSeconds + .06f;
        Check(!shield.IsActive, "Continuing to hold does not automatically re-arm");
        Check(d.ApplyExternalMassDelta(-.02f, a.gameObject).accepted, "Damage resumes after expiry");

        ReadyShield(shield); ShieldInput(d, true, true); yield return shield.DurationSeconds + .06f;
        ShieldInput(d, false, false); yield return .04f;
        Check(!shield.IsActive && !shield.BlocksAllDamage, "Releasing after opening window ends protection immediately");
        ShieldInput(d, true, true); yield return .04f;
        Check(!shield.IsActive, "Hard lockout still prevents immediate reactivation");
        ReadyShield(shield); ShieldInput(d, true, true); yield return .06f;
        ShieldInput(d, false, false); yield return .04f;
        Check(shield.IsActive && !shield.IsHolding, "Early release finishes the original tap window");

        ReadyShield(shield); Set(shield, "lastUseTime", Time.time);
        ShieldInput(d, true, true); yield return .04f;
        Check(shield.CurrentStrength01 < .9f && shield.BlocksAllDamage, "Weak recharged parry still holds a full damage block");
        mass = d.massScore;
        Check(!d.ApplyExternalMassDelta(-.2f, a.gameObject).accepted && Mathf.Approximately(mass, d.massScore), "Weak held shield does not leak damage");
        shield.ForceStopShield(); ShieldInput(d, false, false); yield return .2f;

        ReadyShield(shield); Set(shield, "lastUseTime", Time.time);
        ShieldInput(d, true, false); yield return .04f;
        mass = d.massScore;
        var sword = new GameObject("Weak tap collision check"); sword.SetActive(false);
        sword.transform.SetParent(a.transform, false); sword.transform.position = d.shield.transform.position;
        sword.AddComponent<SphereCollider>().radius = .2f;
        var testMelee = sword.AddComponent<PlayerMelee>(); Set(testMelee, "gateHitboxToActivationWindow", false);
        sword.SetActive(true); Physics.SyncTransforms(); yield return .08f;
        Check(a.IsStunned && d.massScore < mass, "Weak tap retains scaled stun and original damage leak through actual melee collision");
        Object.Destroy(sword); shield.ForceStopShield(); ShieldInput(d, false, false);
        yield return a.stunTime + .2f;

        // Real attack movement and trigger colliders, as used in the arena demo.
        ReadyShield(shield);
        float gap = PlayerScaleAdjuster.BodyRadiusOf(a) + PlayerScaleAdjuster.BodyRadiusOf(d) +
            a.attackController.Profile.GetStage(0).TravelDistance * PlayerScaleAdjuster.ActionReachOf(a) * .45f;
        Place(a, Vector3.left * gap * .5f); Place(d, Vector3.right * gap * .5f);
        yield return .15f;
        float firstDistance = -1f;
        foreach (var melee in a.GetComponentsInChildren<PlayerMelee>(true))
            melee.ShieldContact += victim => { if (victim == d && firstDistance < 0) firstDistance = Vector3.Distance(a.transform.position, d.transform.position); };
        ShieldInput(d, true, false);
        a.SetScriptedInput(new PlayerInputFrame { attackDown = true, attackHeld = true, hasAimDirWS = true, aimDirWS = Vector3.right });
        yield return .02f;
        a.SetScriptedInput(new PlayerInputFrame { attackUp = true, hasAimDirWS = true, aimDirWS = Vector3.right });
        float deadline = Time.time + 2f;
        while (!a.IsStunned && Time.time < deadline) yield return 0f;
        Check(firstDistance > 0f && a.IsStunned, "Actual sword/shield collision preserves successful parry stun");
        float finalDistance = Vector3.Distance(a.transform.position, d.transform.position);
        Check(finalDistance < firstDistance - .01f, "Lunge closes the contact gap before knockback (" + firstDistance.ToString("F3") + " -> " + finalDistance.ToString("F3") + ")");
        yield return .14f;
        Check(nuggets.ParryFeedback01 > .1f, "Defender nuggets move toward padded rim during parry");
        Capture("parry-feedback");
        yield return a.stunTime + .2f;
        Check(!a.IsStunned && Mathf.Approximately(nuggets.ParryFeedback01, 0f), "Stun and defender feedback return to normal");
        Capture("recovered");

        // Full held immunity must end when control/lifecycle ends.
        ReadyShield(shield); ShieldInput(d, true, true); yield return .04f;
        d.ExternalStun(.15f); yield return .04f;
        Check(!shield.IsActive && !shield.BlocksAllDamage, "External stun cancels held protection");
        yield return .18f;
        ReadyShield(shield); ShieldInput(d, true, true); yield return .04f;
        shield.enabled = false;
        Check(!shield.IsActive && !shield.BlocksAllDamage, "Disabling the shield cleans up protection");
        var shader = Shader.Find("MASSIVE/NuggetInstanced");
        Check(shader && shader.isSupported && !ShaderUtil.GetShaderMessages(shader).Any(m => m.severity.ToString() == "Error"), "Nugget shader compiles and renders on the active graphics device");
    }
}
#endif
