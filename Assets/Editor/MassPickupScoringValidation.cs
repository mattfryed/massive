#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Massive.Enemies;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real pickup prefabs and body-trigger contacts in an isolated Play Mode scene.</summary>
[InitializeOnLoad]
public static class MassPickupScoringValidation
{
    const string Key = "MASSIVE.MassPickupScoringValidation";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new();
    public static string Result => SessionState.GetString(Key + ".Result", "Not run");
    static MassPickupScoringValidation() => EditorApplication.playModeStateChanged += State;

    [MenuItem("MASSIVE/Scoring/Validate Mass Pickup Rewards")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run in Edit Mode after compilation.");
        Scene original = SceneManager.GetActiveScene();
        string path = "Assets/MassPickupValidation-" + Guid.NewGuid().ToString("N") + ".unity";
        Scene fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var audio = new GameObject("Fixture audio listener");
            SceneManager.MoveGameObjectToScene(audio, fixture); audio.AddComponent<AudioListener>();
            if (!EditorSceneManager.SaveScene(fixture, path)) throw new Exception("Cannot save test fixture.");
        }
        finally { EditorSceneManager.CloseScene(fixture, true); SceneManager.SetActiveScene(original); }
        SessionState.SetString(Key + ".Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + ".Scene", path);
        SessionState.SetString(Key + ".Result", "RUNNING"); SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.isPlaying = true;
    }

    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear();
            try
            {
                if (SceneManager.GetActiveScene().path != SessionState.GetString(Key + ".Scene", ""))
                    throw new Exception("Refusing to run outside the disposable scene.");
                RunChecks();
                SessionState.SetString(Key + ".Result", "PASSED (" + checks.Count + " checks)\n" + string.Join("\n", checks));
                Debug.Log(Result);
            }
            catch (Exception e)
            { SessionState.SetString(Key + ".Result", "FAILED: " + e.GetBaseException() + "\n" + string.Join("\n", checks)); Debug.LogError(Result); }
            finally { EditorApplication.isPlaying = false; }
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + ".Previous", ""));
            string path = SessionState.GetString(Key + ".Scene", "");
            if (path.StartsWith("Assets/MassPickupValidation-", StringComparison.Ordinal) && path.EndsWith(".unity", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(path);
            SessionState.SetBool(Key, false);
        }
    }

    static void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); checks.Add("PASS " + label); }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
    static void Collect(MatterNuggetScript pickup, Collider body) =>
        typeof(MatterNuggetScript).GetMethod("TryCollect", Hidden).Invoke(pickup, new object[] { body });
    static void Arm(MatterNuggetScript pickup, Vector3 position)
    {
        pickup.Eject(position, Vector3.zero, 30);
        typeof(MatterNuggetScript).GetField("age", Hidden).SetValue(pickup, 1f);
        Physics.SyncTransforms();
    }

    static void RunChecks()
    {
        var profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset"));
        var defaults = ScoreEconomyProfile.CreateRuntimeDefaults();
        defaults.name = "Runtime default economy";
        Scene physicsScene = SceneManager.CreateScene("Mass pickup contact fixtures", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var scope = new GameObject("Pickup fixtures"); scope.SetActive(false); SceneManager.MoveGameObjectToScene(scope, physicsScene);
        var scoreObject = new GameObject("Fixture score service");
        var service = scoreObject.AddComponent<MatchScoreService>(); service.Configure(profile);
        try
        {
            foreach (var p in new[] { profile, defaults, AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Player/Demonstrations/Action Test Economy.asset") })
            {
                Check(p.TryGetReward(ScoreRewardKeys.MassNugget, out var nuggetRule) && nuggetRule.BaseMilliElectronVolts == 100 &&
                    p.TryGetReward(ScoreRewardKeys.MassNugglet, out var nuggletRule) && nuggletRule.BaseMilliElectronVolts == 10,
                    p.name + " contains 100/10 meV pickup rules");
            }
            profile.TryGetReward(ScoreRewardKeys.MassNugget, out var rule);
            Check(rule.multiplierEligible && !rule.ignoreAllMultipliers && rule.chainEffect == ScoreChainAwardMode.None && rule.chainCharge == 0,
                "Pickup defaults apply earned multipliers without adding charge");

            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), scope.transform);
            var player = actor.GetComponent<PlayerControllerScript>(); player.ConfigureDemonstration(scope.transform, 0, 1);
            Set(player, "isPseudoPlayer", false); player.SetControlMode(PlayerControlMode.Disabled);
            scope.SetActive(true); service.RegisterPlayer(player); service.ResetForMatch(true);
            var body = player.GetComponent<SphereCollider>();
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = collider == body;
            player.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            var chain = player.GetComponent<PlayerScoreChain>();

            string[] paths = { "Assets/Prefabs/Matter nugget.prefab", "Assets/Scripts/Anomalies/ORBITAL/Orbital Mass Nugget.prefab", "Assets/Scripts/Anomalies/ORBITAL/Mass Nugglet.prefab" };
            float fullMass = 0f;
            foreach (string path in paths)
            {
                service.ResetForMatch(true); player.massScore = .25f;
                var pickup = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), scope.transform).GetComponent<MatterNuggetScript>();
                long expected = path.Contains("Nugglet") ? 10 : 100;
                Arm(pickup, body.bounds.center); physicsScene.GetPhysicsScene().Simulate(.02f);
                Check(pickup.IsDespawning && pickup.LastScoreAward.accepted && service.GetTeamScore(1) == expected && service.GetTeamScore(2) == 0,
                    pickup.name + " body contact awards exactly the intended team score");
                Check(pickup.LastMassRestored > 0 && Mathf.Approximately(player.massScore - .25f, pickup.LastMassRestored), "Actual mass restoration preserved");
                if (expected == 100) fullMass = pickup.LastMassRestored;
                else Check(Mathf.Abs(pickup.LastMassRestored * 10 - fullMass) < .0001f, "Nugglet keeps one tenth mass restoration independently of score");
                var contribution = service.GetContributionSnapshot().Single();
                Check(contribution.playerID == player.playerID && contribution.acceptedAwardCount == 1 && contribution.finalMilliElectronVolts == expected,
                    "One award is credited to the collector's contribution");
                Collect(pickup, body); physicsScene.GetPhysicsScene().Simulate(.02f);
                Check(service.GetTeamScore(1) == expected && chain.CurrentMultiplier == 1, "Repeated contacts do not duplicate score or add charge");
                Check(!service.TryAwardToPlayer(pickup.scoreRewardKey, player, pickup.LastScoreAward.sourceToken, pickup.transform.position, out var duplicate) &&
                    duplicate.rejection == ScoreAwardRejection.Duplicate, "Central service rejects the same pickup life twice");
                Object.DestroyImmediate(pickup.gameObject);
            }

            var pooled = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(paths[1]), scope.transform).GetComponent<MatterNuggetScript>();
            Vector3 away = Vector3.forward * 20;
            service.ResetForMatch(true); player.massScore = player.massScoreMax;
            chain.ApplyAward(ScoreChainAwardMode.AdvanceAndRefresh, 4);
            service.AdvanceTeamAmplifier(1); service.AdvanceTeamAmplifier(1);
            Arm(pooled, away); Collect(pooled, body);
            Check(pooled.LastScoreAward.finalMilliElectronVolts == 800 && pooled.LastMassRestored == 0 && player.massScore == player.massScoreMax,
                "Full-mass collection earns 100 x personal 2 x team 4 = 800 meV without excess mass");
            Check(chain.CurrentMultiplier == 2, "Collection leaves existing multiplier charge unchanged");
            var fullToast = Object.FindObjectsByType<EnemyScoreToast>(FindObjectsSortMode.None).First(t => t.Amount == 800);
            Check(fullToast.label.text == "+800 meV SCORE" && fullToast.MassRestored == 0, "Full-mass feedback shows only awarded score");

            string oldToken = pooled.LastScoreAward.sourceToken;
            Arm(pooled, away); player.massScore = player.massScoreMax - .005f; Collect(pooled, body);
            Check(pooled.LastScoreAward.sourceToken != oldToken && service.GetTeamScore(1) == 1600 && Mathf.Abs(pooled.LastMassRestored - .005f) < .00001f,
                "Active pooled reuse earns once again; near-full mass does not reduce score");
            var combinedToast = Object.FindObjectsByType<EnemyScoreToast>(FindObjectsSortMode.None).First(t => t.Amount == 800 && t.MassRestored > 0);
            Check(combinedToast.label.text.Contains("+0.5% MASS") && combinedToast.label.text.Contains("+800 meV SCORE"),
                "Combined receipt separates fixed score from actual restored mass");
            SaveFeedbackPreview(fullToast, combinedToast, scope);

            pooled.gameObject.SetActive(false); Arm(pooled, away); player.teamID = 2; Collect(pooled, body);
            Check(service.GetTeamScore(2) == 200 && service.GetTeamScore(1) == 1600, "Re-enabled pool life credits the collecting team and its own Amplifier");
            player.teamID = 1; service.ResetForMatch(true);
            Set(player, "isPseudoPlayer", true); Arm(pooled, away); Collect(pooled, body);
            Check(!pooled.IsDespawning && service.GetTeamScore(1) == 0, "Demonstration players cannot collect or score");
            Set(player, "isPseudoPlayer", false);
            var fakeWeapon = new GameObject("Weapon"); fakeWeapon.transform.SetParent(actor.transform, false);
            var trigger = fakeWeapon.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.tag = "Player";
            Collect(pooled, trigger); Check(!pooled.IsDespawning, "Player-owned attack triggers cannot collect nuggets");

            player.massScore = .25f; service.CloseScoring(); Collect(pooled, body);
            Check(pooled.IsDespawning && !pooled.LastScoreAward.accepted && pooled.LastMassRestored > 0 && service.GetTeamScore(1) == 0,
                "Closed scoring gives no points while preserving eligible mass restoration");
            service.OpenScoring(); Check(service.GetTeamScore(1) == 0, "Rejected awards are not queued for later");
            rule.enabled = false; Arm(pooled, away); Collect(pooled, body);
            Check(!pooled.LastScoreAward.accepted && pooled.LastMassRestored > 0, "Disabling pickup score preserves mass collection");
            rule.enabled = true; rule.chainEffect = ScoreChainAwardMode.AdvanceAndRefresh; rule.chainCharge = .5f;
            Arm(pooled, away); Collect(pooled, body);
            Check(service.GetTeamScore(1) == 100 && chain.CurrentMultiplier == 1.125, "Optional charge applies after the award using the existing economy controls");
            rule.chainEffect = ScoreChainAwardMode.None;
            rule.ignoreAllMultipliers = true; service.AdvanceTeamAmplifier(1);
            Arm(pooled, away); Collect(pooled, body);
            Check(pooled.LastScoreAward.finalMilliElectronVolts == 100, "Ignore All Multipliers bypasses both personal and team factors");
            long beforeExpiry = service.GetTeamScore(1);
            Arm(pooled, away);
            typeof(MatterNuggetScript).GetMethod("SetVisualSize", Hidden).Invoke(pooled, new object[] { 1f });
            pooled.EjectSmoothly(away + Vector3.right, Vector3.zero, 30);
            Check(pooled.IsDespawning, "A visible pooled pickup retires before replacement");
            typeof(MatterNuggetScript).GetField("exitAge", Hidden).SetValue(pooled, pooled.despawnSeconds);
            typeof(MatterNuggetScript).GetMethod("Update", Hidden).Invoke(pooled, null);
            Check(!pooled.IsDespawning && pooled.transform.position == away + Vector3.right && service.GetTeamScore(1) == beforeExpiry,
                "Pool retirement and replacement grant no score");
            typeof(MatterNuggetScript).GetField("age", Hidden).SetValue(pooled, 31f);
            typeof(MatterNuggetScript).GetMethod("Update", Hidden).Invoke(pooled, null);
            Check(!pooled.gameObject.activeSelf && service.GetTeamScore(1) == beforeExpiry, "Uncollected pooled despawn awards nothing");
            Check(service.GetTelemetrySnapshot().Any(t => t.rewardKey == ScoreRewardKeys.MassNugget && t.lightAwardCount == 2),
                "Pickup score is represented in existing economy telemetry");
        }
        finally
        {
            Object.DestroyImmediate(scope); Object.DestroyImmediate(scoreObject);
            Object.DestroyImmediate(profile); Object.DestroyImmediate(defaults);
            SceneManager.UnloadSceneAsync(physicsScene);
        }
    }

    static void SaveFeedbackPreview(EnemyScoreToast full, EnemyScoreToast combined, GameObject scope)
    {
        string path = SessionState.GetString(Key + ".Preview", "");
        if (string.IsNullOrEmpty(path)) return;
        var go = new GameObject("Receipt preview camera"); var camera = go.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 1.4f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.transform.position = new Vector3(0, 0, -10);
        var visible = Object.FindObjectsByType<EnemyScoreToast>(FindObjectsSortMode.None);
        foreach (var t in visible) t.gameObject.SetActive(t == full || t == combined);
        full.transform.SetPositionAndRotation(new Vector3(-1.65f, 0, 0), Quaternion.identity);
        combined.transform.SetPositionAndRotation(new Vector3(1.65f, 0, 0), Quaternion.identity);
        full.label.ForceMeshUpdate(); combined.label.ForceMeshUpdate();
        var rt = new RenderTexture(1280, 480, 24); var previous = RenderTexture.active;
        var image = new Texture2D(1280, 480, TextureFormat.RGB24, false);
        var fixtureRenderers = scope.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
        try
        {
            foreach (var renderer in fixtureRenderers) renderer.enabled = false;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 1280, 480), 0, 0); image.Apply();
            System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            foreach (var renderer in fixtureRenderers) if (renderer) renderer.enabled = true;
            camera.targetTexture = null; RenderTexture.active = previous;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(image); Object.DestroyImmediate(go);
        }
    }
}
#endif
