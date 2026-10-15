#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Enemies;
using Massive.Player;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class AmplifiedDysonValidation
{
    const string Key = "MASSIVE.AmplifiedDysonValidation", Output = "Library/AmplifiedDysonValidation";
    static readonly List<string> checks = new(), errors = new();
    static IEnumerator routine;
    static int frame;
    static double deadline;
    public static string Result => SessionState.GetString(Key + ".Result", "Not run");
    static AmplifiedDysonValidation() => EditorApplication.playModeStateChanged += State;

    [MenuItem("MASSIVE/Enemies/Amplified Dyson/Validate Prototype")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Use Edit Mode after compilation.");
        AmplifiedDysonSetup.Install();
        var original = SceneManager.GetActiveScene();
        string path = "Assets/AmplifiedDysonValidation-" + Guid.NewGuid().ToString("N") + ".unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var camera = new GameObject("Validation camera").AddComponent<Camera>(); SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.gameObject.tag = "MainCamera"; camera.gameObject.AddComponent<AudioListener>(); camera.enabled = false;
            var light = new GameObject("Validation light").AddComponent<Light>(); light.type = LightType.Directional;
            SceneManager.MoveGameObjectToScene(light.gameObject, scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new Exception("Could not save isolated fixture.");
        }
        finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(original); }
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/report.txt", "RUNNING\n");
        SessionState.SetString(Key + ".Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + ".Scene", path); SessionState.SetBool(Key, true);
        SessionState.SetString(Key + ".Result", "RUNNING");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path); EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetBool(Key + ".Background", Application.runInBackground); Application.runInBackground = true;
            checks.Clear(); errors.Clear(); frame = -1; deadline = EditorApplication.timeSinceStartup + 100;
            if (SceneManager.GetActiveScene().path != SessionState.GetString(Key + ".Scene", "")) { Finish(new Exception("Wrong validation scene")); return; }
            routine = Checks(); Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + ".Previous", ""));
            string path = SessionState.GetString(Key + ".Scene", "");
            if (path.StartsWith("Assets/AmplifiedDysonValidation-", StringComparison.Ordinal) && path.EndsWith(".unity", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(path);
            SessionState.SetBool(Key, false);
        }
    }
    static void Log(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + stack); }
    static void Tick()
    {
        if (routine == null || !EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try { if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out"); if (!routine.MoveNext()) Finish(null); }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception error)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        string result = (error == null && errors.Count == 0 ? "PASSED" : "FAILED") + " (" + checks.Count + " checks)\n" + string.Join("\n", checks) + "\n" + error + "\n" + string.Join("\n", errors);
        SessionState.SetString(Key + ".Result", result); File.WriteAllText(Output + "/report.txt", result); Debug.Log(result);
        EditorApplication.isPlaying = false;
    }
    static void Check(bool pass, string label) { if (!pass) throw new Exception(label); checks.Add("PASS " + label); }
    static IEnumerator Delay(float seconds) { float until = Time.time + seconds; while (Time.time < until) yield return null; }
    static EnemyBase Spawn(Vector3 position)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AmplifiedDysonSetup.PrefabPath);
        var root = Object.Instantiate(prefab, position, Quaternion.identity); var enemy = root.GetComponent<EnemyBase>();
        enemy.HoldPosition = true; enemy.AttacksEnabled = false; return enemy;
    }
    static EnemyLootPickup[] Pickups() => Object.FindObjectsByType<EnemyLootPickup>(FindObjectsSortMode.None);
    static IEnumerator Checks()
    {
        var normal = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DysonRepulsorSetup.DefinitionPath);
        var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AmplifiedDysonSetup.DefinitionPath);
        Check(definition.healthMassEq == normal.healthMassEq * 2 && definition.healthMassEq == 40, "Variant has 40 health, twice the current Dyson");
        Check(PrefabUtility.GetPrefabAssetType(definition.prefab) == PrefabAssetType.Variant &&
            PrefabUtility.GetCorrespondingObjectFromSource(definition.prefab) == normal.prefab, "Variant inherits the canonical Repulsor Dyson");
        Check(!normal.prefab.GetComponent<AmplifiedDysonLoot>() && !normal.prefab.GetComponentInChildren<DysonSpherePanels>(true).shimmerEnabled,
            "Normal Dyson has no loot component or enabled flip wave");
        var enemy = Spawn(Vector3.zero); var controller = enemy.GetComponent<DysonSphereRepulsorController>();
        var loot = enemy.GetComponent<AmplifiedDysonLoot>();
        yield return null;
        Check(enemy.Definition == definition && enemy.HealthRemaining == 40 && controller.attackVolume && controller.hurtbox && controller.ripple,
            "Definition, attack, hurtbox and existing ripple are wired");
        while (controller.panels.IsSpawning) yield return null;
        var delay = Delay(.35f); while (delay.MoveNext()) yield return null;
        var core = enemy.GetComponentInChildren<AmplifiedDysonCore>();
        Check(core && core.visual && core.GetComponentsInChildren<ParticleSystem>().Length == 3 &&
            core.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount) > 0, "Shared white, black and prismatic core layers are live");
        Check(!enemy.GetComponentsInChildren<DysonRepulsorCore>().Any(), "Legacy monochrome core is inactive on the variant");
        var panels = controller.panels;
        controller.shellSpinDegreesPerSecond = Vector3.zero;
        panels.breatheLiftAmplitude = panels.breatheInsetAmplitude = 0;
        var panelTransforms = panels.GetComponentsInChildren<MeshFilter>().Where(m => m.name.StartsWith("Panel_")).Select(m => m.transform).ToArray();
        Check(panelTransforms.All(t => t.GetComponent<MeshFilter>().sharedMesh.triangles.Length == 6), "Flipping panels have solid reverse faces even with the legacy front-culling material");
        Check(ValidateFlipOutline(panels) < .00001f, "Fully reversed panels occupy their original triangle outlines");
        var rotations = panelTransforms.Select(t => t.localRotation).ToArray();
        // Emitted waves deliberately leave a settled interval between flips.
        // Wait for the next front instead of assuming motion in every .45-second window.
        float wavePeriod = Mathf.Max(1f / Mathf.Max(.01f, panels.shimmerFrequency),
            panels.shimmerWaveTravelSeconds + panels.shimmerPanelFlipSeconds);
        float flipDeadline = Time.time + Mathf.Min(15f, wavePeriod + panels.shimmerWaveTravelSeconds + panels.shimmerPanelFlipSeconds + .5f);
        while (Time.time < flipDeadline &&
            panelTransforms.Where((t, i) => Quaternion.Angle(t.localRotation, rotations[i]) > 10f).Count() <= 5)
            yield return null;
        Check(panelTransforms.Where((t, i) => Quaternion.Angle(t.localRotation, rotations[i]) > 10f).Count() > 5, "Panel wave flips actual geometry with breathing and shell spin disabled");
        Capture("amplified-dyson.png", Vector3.zero, 1.5f);
        panels.shimmerAmplitudeDegrees = 0;
        yield return new WaitForFixedUpdate(); yield return null;
        rotations = panelTransforms.Select(t => t.localRotation).ToArray();
        delay = Delay(.3f); while (delay.MoveNext()) yield return null;
        Check(panelTransforms.Where((t, i) => Quaternion.Angle(t.localRotation, rotations[i]) > .01f).Count() == 0, "Zero flip amplitude removes the effect without changing breathing settings");
        panels.shimmerAmplitudeDegrees = 180; panels.breatheLiftAmplitude = .03f; panels.breatheInsetAmplitude = .03f;
        enemy.Pause(true); yield return null;
        float clock = controller.AnimationClock; var coreTime = core.GetComponentsInChildren<ParticleSystem>()[0].time;
        rotations = panelTransforms.Select(t => t.localRotation).ToArray();
        delay = Delay(.2f); while (delay.MoveNext()) yield return null;
        Check(clock == controller.AnimationClock && panelTransforms.Where((t, i) => Quaternion.Angle(t.localRotation, rotations[i]) > .01f).Count() == 0,
            "Pause freezes the wave, breathing and attack clock");
        Check(Mathf.Abs(core.GetComponentsInChildren<ParticleSystem>()[0].time - coreTime) < .03f, "Pause freezes the Amplifier core particle motion");
        enemy.Pause(false);
        loot.hitDropChancePercent = 0; enemy.TakeDamage(1, EnemyDamageSource.Sword);
        Check(enemy.HealthRemaining == 39 && Pickups().Length == 0, "Zero-percent hit drop still damages normally without loot");
        delay = Delay(.4f); while (delay.MoveNext()) yield return null;
        loot.hitDropChancePercent = 100; loot.maxHitDrops = 2; enemy.TakeDamage(1, EnemyDamageSource.Projectile);
        Check(loot.HitDropsReleased == 1 && Pickups().Length == 1 && Pickups()[0].personalMultiplierGain == .1f, "Guaranteed surviving hit ejects one functional Amplifier Nugglet");
        var emitted = Pickups()[0]; var velocity = emitted.GetComponent<Rigidbody>().linearVelocity;
        Check(Mathf.Abs(velocity.y) < .001f && velocity.magnitude >= 2f && velocity.magnitude <= 4f, "Ejection uses a random playing-plane direction and configured speed range");
        for (int i = 0; i < 20; i++) enemy.TakeDamage(.01f, EnemyDamageSource.Projectile);
        Check(loot.HitDropsReleased == 1, "Continuous damage callbacks share the hit-roll cooldown");
        enemy.Pause(true); yield return new WaitForFixedUpdate(); yield return null;
        Vector3 dropPosition = emitted.transform.position; float scale = emitted.VisualScale, health = enemy.HealthRemaining;
        enemy.TakeDamage(1, EnemyDamageSource.Sword);
        delay = Delay(.2f); while (delay.MoveNext()) yield return null;
        Check(enemy.HealthRemaining == health && loot.HitDropsReleased == 1, "Paused damage is rejected before loot rolls");
        Check(Vector3.Distance(dropPosition, emitted.transform.position) < .001f && scale == emitted.VisualScale, "Released loot freezes movement and life animation with its source");
        enemy.Pause(false); delay = Delay(.4f); while (delay.MoveNext()) yield return null;
        enemy.TakeDamage(1, EnemyDamageSource.Repulsor);
        delay = Delay(.4f); while (delay.MoveNext()) yield return null;
        enemy.TakeDamage(1, EnemyDamageSource.Sword);
        Check(loot.HitDropsReleased == 2, "Per-life hit-drop limit bounds repeated damage farming");
        int before = Pickups().Length; loot.deathDropCount = new Vector2Int(6, 6);
        enemy.TakeDamage(999, EnemyDamageSource.Sword);
        Check(enemy.IsDead && loot.DeathDropsReleased == 6 && loot.HitDropsReleased == 2 && Pickups().Length == before + 6,
            "Lethal damage produces one death assortment without an extra surviving-hit drop");
        enemy.TakeDamage(999, EnemyDamageSource.Sword); enemy.Kill();
        Check(Pickups().Length == before + 6, "Repeated death/removal cannot duplicate loot");
        delay = Delay(.9f); while (delay.MoveNext()) yield return null;
        Check(!enemy && Pickups().Length == before + 6, "Released loot survives shell death cleanup");
        Capture("amplified-dyson-loot.png", Vector3.zero, 4.5f);
        foreach (var pickup in Pickups()) Object.Destroy(pickup.gameObject);
        var admin = Spawn(Vector3.right * 5); yield return null; admin.Kill(EnemyDamageSource.LifetimeExpired);
        Check(admin.GetComponent<AmplifiedDysonLoot>().DeathDropsReleased == 0, "Administrative/lifetime removal creates no death rewards");
        var expiring = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AmplifiedDysonSetup.Folder + "/Amplifier Nugglet.prefab")).GetComponent<EnemyLootPickup>();
        expiring.Eject(Vector3.forward * 10, Vector3.zero, .15f);
        delay = Delay(.3f); while (delay.MoveNext()) yield return null;
        Check(!expiring, "Expired loot destroys its finite-lived object rather than leaking inactive pooled pickups");
        ValidateCollection();
        Check(errors.Count == 0, "No runtime Console errors");
    }
    static void ValidateCollection()
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var scene = SceneManager.CreateScene("Amplified loot contact fixtures", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var scope = new GameObject("Pickup contact fixtures"); scope.SetActive(false); SceneManager.MoveGameObjectToScene(scope, scene);
        var profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset"));
        var scores = new GameObject("Fixture scores").AddComponent<MatchScoreService>(); scores.Configure(profile);
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), scope.transform);
        var player = actor.GetComponent<PlayerControllerScript>(); player.ConfigureDemonstration(scope.transform, 0, 1);
        typeof(PlayerControllerScript).GetField("isPseudoPlayer", hidden).SetValue(player, false);
        player.SetControlMode(PlayerControlMode.Disabled); scope.SetActive(true); scores.RegisterPlayer(player);
        var body = player.GetComponent<SphereCollider>();
        foreach (var c in actor.GetComponentsInChildren<Collider>(true)) c.enabled = c == body;
        player.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
        for (int i = 0; i < 4; i++)
        {
            scores.ResetForMatch(true); player.massScore = .25f;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AmplifiedDysonSetup.Folder + "/" + AmplifiedDysonSetup.PickupNames[i] + ".prefab");
            var pickup = Object.Instantiate(prefab, scope.transform).GetComponent<EnemyLootPickup>();
            pickup.Eject(body.bounds.center, Vector3.zero, 10);
            typeof(MatterNuggetScript).GetField("age", hidden).SetValue(pickup, 1f);
            Physics.SyncTransforms(); scene.GetPhysicsScene().Simulate(.02f);
            bool amplifier = i == 1 || i == 3;
            double gain = amplifier ? i == 1 ? .1 : 1 : 0;
            long energy = amplifier ? 0 : i == 0 ? 10 : 100;
            Check(pickup.IsDespawning && pickup.LastMassRestored > 0 && Math.Abs(pickup.LastMultiplierGained - gain) < .00001 &&
                scores.GetTeamScore(1) == energy, AmplifiedDysonSetup.PickupNames[i] + " grants the expected mass, score and personal multiplier through body contact");
            typeof(MatterNuggetScript).GetMethod("TryCollect", hidden).Invoke(pickup, new object[] { body });
            Check(scores.GetTeamScore(1) == energy && Math.Abs(player.GetComponent<PlayerScoreChain>().CurrentMultiplier - (1 + gain)) < .00001,
                AmplifiedDysonSetup.PickupNames[i] + " collects only once");
            Object.DestroyImmediate(pickup.gameObject);
        }
        scores.ResetForMatch(true);
        var enemy = Spawn(Vector3.right * 15); SceneManager.MoveGameObjectToScene(enemy.gameObject, scene);
        var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AmplifiedDysonSetup.DefinitionPath); enemy.Init(definition, null);
        profile.TryGetReward(definition.defeatRewardKey, out var killRule);
        enemy.TakeDamage(999, EnemyDamageSource.Sword, player);
        Check(scores.GetTeamScore(1) == killRule.BaseMilliElectronVolts && enemy.GetComponent<AmplifiedDysonLoot>().DeathDropsReleased >= 6,
            "Attributed defeat preserves the direct Dyson score while releasing separate collectible rewards");
        enemy.TakeDamage(999, EnemyDamageSource.Sword, player);
        Check(scores.GetTeamScore(1) == killRule.BaseMilliElectronVolts, "Repeated lethal contacts cannot duplicate direct kill credit");
        Object.DestroyImmediate(enemy.gameObject);
        foreach (var pickup in Pickups()) if (pickup.gameObject.scene == scene) Object.DestroyImmediate(pickup.gameObject);
        Object.DestroyImmediate(scope); Object.DestroyImmediate(scores.gameObject); Object.DestroyImmediate(profile);
    }
    // Exercise the actual flip calculation against the generated panel geometry.
    // Shared by the Play Mode fixture and focused preview-scene checks.
    public static float ValidateFlipOutline(DysonSpherePanels panels)
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(DysonSpherePanels);
        var rotate = type.GetMethod("RotateShimmerPanel", hidden);
        var generated = (IEnumerable)type.GetField("_panels", hidden).GetValue(panels);
        float amplitude = panels.shimmerAmplitudeDegrees;
        float worst = 0f;
        int count = 0;
        try
        {
            panels.shimmerAmplitudeDegrees = 180f;
            foreach (var panel in generated)
            {
                var fields = panel.GetType();
                var rotation = (Quaternion)fields.GetField("baseRotLocal").GetValue(panel);
                var front = (Quaternion)rotate.Invoke(panels, new object[] { panel, rotation, 0f });
                var back = (Quaternion)rotate.Invoke(panels, new object[] { panel, rotation, 1f });
                if (Vector3.Dot(front * Vector3.forward, back * Vector3.forward) > -.99999f)
                    throw new Exception("Panel did not expose its reverse face at full flip.");
                var vertices = new[] { (Vector3)fields.GetField("v0").GetValue(panel),
                    (Vector3)fields.GetField("v1").GetValue(panel), (Vector3)fields.GetField("v2").GetValue(panel) };
                foreach (var vertex in vertices)
                    worst = Mathf.Max(worst, vertices.Min(v => Vector3.Distance(back * vertex, front * v)));
                count++;
            }
            if (count == 0) throw new Exception("No generated panels were available for the outline check.");
            return worst;
        }
        finally
        {
            panels.shimmerAmplitudeDegrees = amplitude;
        }
    }

    static void Capture(string name, Vector3 center, float size)
    {
        var camera = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
        camera.orthographic = true; camera.orthographicSize = size; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.transform.position = center + new Vector3(0, 8, -2); camera.transform.LookAt(center);
        var target = new RenderTexture(1000, 800, 24); var previous = RenderTexture.active;
        var texture = new Texture2D(1000, 800, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1000, 800), 0, 0); texture.Apply(); File.WriteAllBytes(Output + "/" + name, texture.EncodeToPNG());
        }
        finally { camera.targetTexture = null; RenderTexture.active = previous; Object.DestroyImmediate(texture); target.Release(); Object.DestroyImmediate(target); }
    }
}
#endif
