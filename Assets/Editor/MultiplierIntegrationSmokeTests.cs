#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Massive.Multiplier;
using Massive.Player;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Isolated Play Mode checks for personal charge and team Amplifier integration.</summary>
[InitializeOnLoad]
public static class MultiplierIntegrationSmokeTests
{
    private const string Pending = "MASSIVE.MultiplierTests.Pending";
    private const string TempScene = "MASSIVE.MultiplierTests.Scene";
    private const string PreviousScene = "MASSIVE.MultiplierTests.Previous";
    private const string Report = "MASSIVE.MultiplierTests.Report";

    public static string LastReport => SessionState.GetString(Report, "Not run");

    static MultiplierIntegrationSmokeTests()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("MASSIVE/Scoring/Run Multiplier Integration Smoke Tests")]
    public static void Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Stop Play Mode and wait for compilation before running checks.");
        if (SessionState.GetBool(Pending, false))
            throw new InvalidOperationException("A multiplier integration check is already pending.");

        Scene original = SceneManager.GetActiveScene();
        string path = "Assets/MultiplierSmoke-" + Guid.NewGuid().ToString("N") + ".unity";
        Scene fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(fixture);
            new GameObject("Test camera").AddComponent<Camera>();
            new GameObject("Test light").AddComponent<Light>().type = LightType.Directional;
            if (!EditorSceneManager.SaveScene(fixture, path))
                throw new InvalidOperationException("Cannot create multiplier test scene.");
        }
        finally
        {
            EditorSceneManager.CloseScene(fixture, true);
            SceneManager.SetActiveScene(original);
        }

        SessionState.SetString(
            PreviousScene,
            AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(TempScene, path);
        SessionState.SetString(Report, "Running");
        SessionState.SetBool(Pending, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            try
            {
                string result = RunChecks();
                SessionState.SetString(Report, result);
                Debug.Log("[Multiplier Tests] " + result);
            }
            catch (Exception ex)
            {
                SessionState.SetString(Report, "FAILED: " + ex);
                Debug.LogException(ex);
            }
            finally
            {
                EditorApplication.isPlaying = false;
            }
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                SessionState.GetString(PreviousScene, string.Empty));
            string path = SessionState.GetString(TempScene, string.Empty);
            if (path.StartsWith("Assets/MultiplierSmoke-", StringComparison.Ordinal) &&
                path.EndsWith(".unity", StringComparison.Ordinal))
            {
                AssetDatabase.DeleteAsset(path);
            }

            SessionState.SetBool(Pending, false);
        }
    }

    private static string RunChecks()
    {
        if (SceneManager.GetActiveScene().path != SessionState.GetString(TempScene, string.Empty))
            throw new InvalidOperationException("Refusing to test outside the isolated fixture.");

        var owned = new List<Object>();
        int passed = 0;
        Action<bool, string> check = (condition, description) =>
        {
            if (!condition)
                throw new Exception(description);
            passed++;
        };

        try
        {
            ScoreEconomyProfile source = AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>(
                "Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
            ScoreEconomyProfile profile = Object.Instantiate(source);
            owned.Add(profile);

            GameObject serviceObject = new GameObject("Test score service");
            owned.Add(serviceObject);
            MatchScoreService service = serviceObject.AddComponent<MatchScoreService>();
            service.Configure(profile);

            PlayerControllerScript p1 = CreatePlayer(0, 1, service, owned);
            PlayerControllerScript p3 = CreatePlayer(2, 2, service, owned);
            service.ResetForMatch(openScoring: true);

            check(service.GetTeamAmplifierMultiplier(1) == 1 &&
                  service.GetTeamAmplifierMultiplier(2) == 1,
                "Both team Amplifiers start at x1");

            for (int i = 0; i < 4; i++)
            {
                check(service.TryAwardToPlayer(
                        ScoreRewardKeys.EnemyDefeat,
                        p1,
                        $"charge-{i}",
                        Vector3.zero,
                        out _),
                    "Charge award accepted");
            }

            PlayerScoreChain p1Chain = p1.GetComponent<PlayerScoreChain>();
            check(p1Chain.CurrentMultiplier == 2 && Mathf.Approximately(p1Chain.Progress01, 0f),
                "Personal bar promotes at its threshold and resets");
            check(service.GetTeamScore(1) == 4000L,
                "Promotion affects the next award, not the threshold award");

            check(service.AdvanceTeamAmplifier(1) && service.GetTeamAmplifierMultiplier(1) == 2,
                "Team capture advances x1 to x2");
            check(service.TryAwardToPlayer(
                    ScoreRewardKeys.EnemyDefeat,
                    p1,
                    "amplified-award",
                    Vector3.zero,
                    out ScoreAwardResult amplified),
                "Amplified award accepted");
            check(amplified.multiplier == 2 && amplified.teamAmplifierMultiplier == 2 &&
                  amplified.combinedMultiplier == 4 && amplified.finalMilliElectronVolts == 4000L,
                "Personal and team factors compose correctly");
            check(service.GetTeamScore(1) == 8000L && service.GetTeamScore(2) == 0L,
                "Amplifier affects only its team");

            check(service.AdvanceTeamAmplifier(1) && service.AdvanceTeamAmplifier(1) &&
                  service.GetTeamAmplifierMultiplier(1) == 8 &&
                  !service.AdvanceTeamAmplifier(1),
                "Team Amplifier caps at x8");

            long scoreBeforeReset = service.GetTeamScore(1);
            service.ResetMultipliers();
            check(service.GetTeamScore(1) == scoreBeforeReset &&
                  service.GetTeamAmplifierMultiplier(1) == 1 &&
                  p1Chain.CurrentMultiplier == 1,
                "Multiplier reset preserves earned score");

            GameObject corePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Power-ups/Amplifier Core/Amplifier Core.prefab");
            GameObject coreObject = Object.Instantiate(corePrefab);
            owned.Add(coreObject);
            AmplifierCoreGameplay core = coreObject.GetComponent<AmplifierCoreGameplay>();
            AmplifierCoreVisual coreVisual = coreObject.GetComponent<AmplifierCoreVisual>();
            check(core != null && coreVisual != null && core.IsSpawning,
                "Physical core begins with its staged spawn presentation");

            coreVisual.SetLifecycleReveal(0f, 0f);
            Renderer shellRenderer = coreObject.transform
                .Find("Visual Root/Neutral Shell").GetComponent<Renderer>();
            var shellProperties = new MaterialPropertyBlock();
            shellRenderer.GetPropertyBlock(shellProperties);
            check(Mathf.Approximately(shellProperties.GetFloat("_CellScale"), 1.5f) &&
                  Mathf.Approximately(shellProperties.GetFloat("_BandWidth"), 0f),
                "Spawn starts at ribbon scale 1.5 with zero shell coverage");
            core.CompleteSpawnImmediately();

            PlayerAttackProfile attackProfile =
                ScriptableObject.CreateInstance<PlayerAttackProfile>();
            owned.Add(attackProfile);
            SerializedObject profileSerialized = new SerializedObject(attackProfile);
            profileSerialized.FindProperty("stages").arraySize = 1;
            profileSerialized.ApplyModifiedPropertiesWithoutUndo();

            PlayerAttackController attack = p1.gameObject.AddComponent<PlayerAttackController>();
            SerializedObject attackSerialized = new SerializedObject(attack);
            attackSerialized.FindProperty("attackProfile").objectReferenceValue = attackProfile;
            attackSerialized.FindProperty("lockOnEnabled").boolValue = false;
            attackSerialized.ApplyModifiedPropertiesWithoutUndo();
            attack.ExternalMoveInput = Vector2.right;
            p1.GetComponent<Rigidbody>().linearVelocity = new Vector3(10f, 0f, 2f);
            attack.BeginAttack();
            check(attack.IsAttacking && core.TryApplyAttackImpact(attack),
                "Attack thrust is accepted as an Amplifier Core impact");
            check(!attack.IsAttacking && p1.IsExternallyStunned &&
                  p1.GetComponent<Rigidbody>().linearVelocity.magnitude < 0.4f,
                "Core impact stops the attack and nearly all player momentum");
            SimulationMode previousSimulationMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            try
            {
                Physics.Simulate(Time.fixedDeltaTime);
            }
            finally
            {
                Physics.simulationMode = previousSimulationMode;
            }
            check(core.Body.linearVelocity.x > 0f &&
                  Mathf.Abs(core.Body.linearVelocity.z) < 0.01f,
                "Core receives its added impulse along the locked thrust angle");

            GameObject goalObject = new GameObject("Test team 1 goal");
            owned.Add(goalObject);
            SphereCollider goalCollider = goalObject.AddComponent<SphereCollider>();
            goalCollider.isTrigger = true;
            AmplifierGoalCapture goal = goalObject.AddComponent<AmplifierGoalCapture>();
            check(core != null && core.TryCapture(goal) && service.GetTeamAmplifierMultiplier(1) == 2,
                "Physical core capture advances the goal's team");

            GameObject auroraObject = new GameObject("Test aurora blast");
            owned.Add(auroraObject);
            AmplifierAuroraBlast aurora = auroraObject.AddComponent<AmplifierAuroraBlast>();
            SerializedObject auroraSerialized = new SerializedObject(aurora);
            auroraSerialized.FindProperty("particleMaterial").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Power-ups/Amplifier Core/Amplifier Aurora Blast.mat");
            auroraSerialized.FindProperty("volumeMaterial").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Power-ups/Amplifier Core/Amplifier Aurora Volume.mat");
            auroraSerialized.ApplyModifiedPropertiesWithoutUndo();
            aurora.PreviewVolumeProgress(0.4f);
            check(aurora.VolumeRenderer != null &&
                  aurora.VolumeRenderer.sharedMaterial != null &&
                  aurora.VolumeRenderer.sharedMaterial.shader.name ==
                  "MASSIVE/Amplifier Core/Aurora Volume",
                "Aurora blast builds its ray-marched three-dimensional cloud");
            check(aurora.IsVolumeActive && Mathf.Approximately(aurora.VolumeProgress, 0.4f) &&
                  aurora.VolumeRenderer.transform.lossyScale.x > 1f,
                "Aurora cloud preview expands through a deterministic lifecycle");
            aurora.Play();
            check(aurora.GasSystem.particleCount == 14 &&
                  aurora.RibbonSystem.particleCount == 8,
                "Aurora keeps billboard particles to a sparse breakup layer");

            GameObject playingField = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/PLAYING FIELD.prefab");
            check(playingField.transform.Find("UI/Text Objects/TEAM_02/Player 3 multiplier") != null &&
                  playingField.transform.Find("UI/Text Objects/TEAM_02/Player 4 multiplier") != null,
                "Team 2 has mirrored personal multiplier widgets");

            PlayerScoreChainPresenter[] presenters =
                playingField.GetComponentsInChildren<PlayerScoreChainPresenter>(true);
            bool barsWired = presenters.Length == 4;
            for (int i = 0; i < presenters.Length && barsWired; i++)
            {
                SerializedObject presenter = new SerializedObject(presenters[i]);
                Object bar = presenter.FindProperty("progressRectangle").objectReferenceValue;
                Object track = presenter.FindProperty("progressTrackRectangle").objectReferenceValue;
                barsWired = bar != null && bar.name == "multiplier bar" &&
                            track != null && track.name == "fill";
            }
            check(barsWired,
                "All personal progress views drive multiplier bar against the full-width track");
            check(playingField.GetComponentsInChildren<PlayerScoreChainPresenter>(true).Length == 4 &&
                  playingField.GetComponentsInChildren<TeamAmplifierPresenter>(true).Length == 2 &&
                  playingField.GetComponentsInChildren<AmplifierGoalCapture>(true).Length == 2 &&
                  playingField.GetComponentsInChildren<AmplifierAuroraBlast>(true).Length == 2,
                "Four player views, two team views, two goal captures, and two aurora blasts are wired");

            // Ensure the second registered player remains independent.
            check(p3.GetComponent<PlayerScoreChain>().CurrentMultiplier == 1,
                "Opposing player's personal multiplier remains independent");

            return $"{passed} checks passed (isolated Play Mode).";
        }
        finally
        {
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                if (owned[i] != null)
                    Object.DestroyImmediate(owned[i]);
            }
        }
    }

    private static PlayerControllerScript CreatePlayer(
        int playerID,
        int teamID,
        MatchScoreService service,
        List<Object> owned)
    {
        GameObject playerObject = new GameObject($"Test player {playerID}");
        playerObject.SetActive(false);
        owned.Add(playerObject);
        playerObject.AddComponent<Rigidbody>().useGravity = false;
        PlayerControllerScript player = playerObject.AddComponent<PlayerControllerScript>();
        player.playerID = playerID;
        player.teamID = teamID;
        player.SetControlMode(PlayerControlMode.Disabled);
        player.enabled = false;
        playerObject.SetActive(true);
        service.RegisterPlayer(player);
        return player;
    }
}
#endif
