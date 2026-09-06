#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Massive.Enemies;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Repeatable integration checks in an isolated Play Mode scene; no gameplay scene is saved.</summary>
[InitializeOnLoad]
public static class EnemyScoringSmokeTests
{
    private const string Pending = "MASSIVE.EnemyScoringTests.Pending";
    private const string TempScene = "MASSIVE.EnemyScoringTests.Scene";
    private const string PreviousScene = "MASSIVE.EnemyScoringTests.Previous";
    private const string Report = "MASSIVE.EnemyScoringTests.Report";
    public static string LastReport => SessionState.GetString(Report, "Not run");

    static EnemyScoringSmokeTests()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("MASSIVE/Scoring/Run Enemy Scoring Smoke Tests")]
    public static void Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Stop Play Mode and wait for compilation before running checks.");
        if (SessionState.GetBool(Pending, false))
            throw new InvalidOperationException("An enemy scoring check is already pending.");

        Scene original = SceneManager.GetActiveScene();
        string path = "Assets/EnemyScoringSmoke-" + Guid.NewGuid().ToString("N") + ".unity";
        Scene fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(fixture);
            var camera = new GameObject("Test camera");
            camera.AddComponent<Camera>();
            camera.AddComponent<AudioListener>();
            var light = new GameObject("Test light");
            light.AddComponent<Light>().type = LightType.Directional;
            if (!EditorSceneManager.SaveScene(fixture, path)) throw new InvalidOperationException("Cannot create test scene.");
        }
        finally
        {
            EditorSceneManager.CloseScene(fixture, true);
            SceneManager.SetActiveScene(original);
        }
        SessionState.SetString(PreviousScene, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(TempScene, path);
        SessionState.SetString(Report, "Running");
        SessionState.SetBool(Pending, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            try
            {
                string result = RunChecks();
                SessionState.SetString(Report, result);
                Debug.Log("[Enemy Scoring Tests] " + result);
            }
            catch (Exception ex)
            {
                SessionState.SetString(Report, "FAILED: " + ex);
                Debug.LogException(ex);
            }
            finally { EditorApplication.isPlaying = false; }
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousScene, ""));
            string path = SessionState.GetString(TempScene, "");
            if (path.StartsWith("Assets/EnemyScoringSmoke-", StringComparison.Ordinal) && path.EndsWith(".unity", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(path);
            SessionState.SetBool(Pending, false);
        }
    }

    private static string RunChecks()
    {
        if (SceneManager.GetActiveScene().path != SessionState.GetString(TempScene, ""))
            throw new InvalidOperationException("Refusing to test outside the isolated fixture.");
        var owned = new List<Object>();
        int passed = 0;
        Action<bool, string> check = (condition, description) =>
        {
            if (!condition) throw new Exception(description);
            passed++;
        };
        try
        {
            var profileAsset = AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
            var profile = Object.Instantiate(profileAsset);
            owned.Add(profile);
            profile.TryGetReward(ScoreRewardKeys.DysonDefeat, out ScoreRewardRule rule);
            check(rule != null && rule.BaseMilliElectronVolts == 1000, "Dyson central row is 1 eV");
            var serviceGo = new GameObject("Test score service"); owned.Add(serviceGo);
            var service = serviceGo.AddComponent<MatchScoreService>();
            service.Configure(profile);

            Func<int, int, PlayerControllerScript> player = (id, team) =>
            {
                var go = new GameObject("Test player " + id); go.SetActive(false); owned.Add(go);
                go.AddComponent<Rigidbody>().useGravity = false;
                var p = go.AddComponent<PlayerControllerScript>(); p.playerID = id; p.teamID = team;
                p.SetControlMode(PlayerControlMode.Disabled);
                p.enabled = false;
                go.SetActive(true);
                service.RegisterPlayer(p);
                return p;
            };
            var p1 = player(0, 1); var p2 = player(1, 2); var teammate = player(2, 1);
            var definition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                "Assets/Enemy System/Enemy Types/Melee/DysonSphere/ED_DysonSphere.asset")); owned.Add(definition);
            Func<EnemyBase> enemy = () =>
            {
                var go = new GameObject("Test enemy"); owned.Add(go); go.AddComponent<SphereCollider>();
                var e = go.AddComponent<EnemyBase>(); e.Init(definition, null); return e;
            };
            Action reset = () => service.ResetForMatch(true);
            Action<EnemyBase, PlayerControllerScript> defeat = (e, p) => e.TakeDamage(100f, EnemyDamageSource.Sword, p);
            reset();
            var first = enemy(); first.TakeDamage(1f, EnemyDamageSource.Sword, p1);
            check(service.GetTeamScore(1) == 0, "Nonlethal damage awards nothing");
            defeat(first, p2);
            check(service.GetTeamScore(1) == 0 && service.GetTeamScore(2) == 1000, "Final blow owns award");
            defeat(first, p1);
            check(service.GetTeamScore(1) == 0 && service.GetTeamScore(2) == 1000, "Duplicate lethal hit rejected");
            check(!service.TryAwardToPlayer(rule.key, p2, first.SourceLifeToken, Vector3.zero, out var duplicate) &&
                duplicate.rejection == ScoreAwardRejection.Duplicate, "Source life token deduplicated centrally");
            reset();
            for (int i = 0; i < 4; i++) defeat(enemy(), p1);
            check(service.GetTeamScore(1) == 4000 &&
                  p1.GetComponent<PlayerScoreChain>().CurrentMultiplier == 2,
                "Four configured charge points promote x1 to x2");
            defeat(enemy(), p1);
            check(service.GetTeamScore(1) == 6000, "Promoted personal multiplier applies to the next award");
            defeat(enemy(), teammate);
            check(service.GetTeamScore(1) == 7000 && teammate.GetComponent<PlayerScoreChain>().CurrentMultiplier == 1 &&
                  teammate.GetComponent<PlayerScoreChain>().Progress01 > 0f,
                "Teammate has independent charge and shared team total");
            check(service.GetContributionSnapshot().Length == 2, "Individual contributions recorded");
            reset(); var expired = enemy(); expired.Kill(EnemyDamageSource.LifetimeExpired);
            check(service.GetTeamScore(1) == 0, "Expiration awards nothing");
            enemy().Kill(EnemyDamageSource.Sword);
            check(service.GetTeamScore(1) == 0, "Administrative Kill awards nothing even with combat category");
            defeat(enemy(), null);
            check(service.GetTeamScore(1) == 0, "Anonymous damage awards nothing");
            enemy().TakeDamage(100f, EnemyDamageSource.Environmental, p1);
            check(service.GetTeamScore(1) == 0, "Environmental removal awards nothing");
            var friendly = enemy(); friendly.OwnerTeamId = 1; defeat(friendly, p1);
            check(service.GetTeamScore(1) == 0, "Friendly enemy awards nothing");
            var paused = enemy(); paused.Pause(true); defeat(paused, p1);
            check(!paused.IsDead && service.GetTeamScore(1) == 0, "Paused enemies reject damage");
            string life = paused.SourceLifeToken; paused.Init(definition, null);
            check(life != paused.SourceLifeToken && !paused.IsPaused, "Initialization renews life token and pause state");
            rule.enabled = false; defeat(enemy(), p1); check(service.GetTeamScore(1) == 0, "Disabled reward awards nothing"); rule.enabled = true;
            service.CloseScoring(); defeat(enemy(), p1); check(service.GetTeamScore(1) == 0, "Buzzer closes enemy scoring");
            reset(); rule.multiplierEligible = false;
            for (int i = 0; i < 4; i++) defeat(enemy(), p1);
            check(service.GetTeamScore(1) == 4000 && p1.GetComponent<PlayerScoreChain>().CurrentMultiplier == 2,
                "Chain effect is independent of multiplier eligibility"); rule.multiplierEligible = true;

            reset(); var shooter = enemy(); var projectileGo = new GameObject("Test projectile"); owned.Add(projectileGo);
            projectileGo.AddComponent<SphereCollider>();
            var projectile = projectileGo.AddComponent<EnemyProjectileBase>(); projectile.useRigidbody = false;
            projectile.reflectedDamageToEnemyMassEq = 100f; projectile.Init(shooter, Vector3.forward);
            var shield = new GameObject("Test shield"); shield.transform.SetParent(p2.transform); shield.tag = "Shield";
            var shieldCollider = shield.AddComponent<SphereCollider>(); p2.shieldOn = true;
            projectile.SendMessage("OnTriggerEnter", shieldCollider);
            check(projectile.ReflectedBy == p2, "Shield reflection retains player");
            projectile.SendMessage("OnTriggerEnter", shooter.GetComponent<Collider>());
            projectile.SendMessage("OnTriggerEnter", shooter.GetComponent<Collider>());
            check(service.GetTeamScore(2) == 1000 && service.GetTeamScore(1) == 0, "Reflected defeat awards once to reflector");

            reset(); var prefab = Object.Instantiate(definition.prefab); owned.Add(prefab);
            var dyson = prefab.GetComponent<EnemyBase>(); dyson.Init(definition, null);
            var hurtbox = prefab.GetComponentInChildren<EnemyHurtbox>();
            check(hurtbox != null && dyson.GetComponent<EnemyScoreReward>() != null, "Existing Dyson prefab gets scoring adapter");
            var sword = new GameObject("Test sword"); sword.SetActive(false); owned.Add(sword);
            sword.AddComponent<BoxCollider>();
            var melee = sword.AddComponent<PlayerMelee>(); var meleeSettings = new SerializedObject(melee);
            meleeSettings.FindProperty("owner").objectReferenceValue = p1; meleeSettings.ApplyModifiedPropertiesWithoutUndo();
            for (int i = 0; i < 20; i++) hurtbox.SendMessage("OnTriggerEnter", sword.GetComponent<Collider>());
            check(dyson.IsDead && service.GetTeamScore(1) == 1000, "Actual Dyson prefab: 20 sword hits award 1 eV");
            check(EnemyScoringValidation.Validate(profile, new[] { definition }).Count == 0, "Definition validation passes");
            definition.defeatRewardKey = "MISSING_TEST_KEY";
            check(EnemyScoringValidation.Validate(profile, new[] { definition }).Count == 1, "Missing reward detected");
            return passed + " checks passed (isolated Play Mode, including actual Dyson prefab).";
        }
        finally
        {
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        }
    }
}
#endif
