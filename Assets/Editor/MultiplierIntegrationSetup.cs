#if UNITY_EDITOR
using System;
using Massive.Multiplier;
using Massive.Scoring;
using Shapes;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class MultiplierIntegrationSetup
{
    private const string PlayingFieldPath = "Assets/Prefabs/PLAYING FIELD.prefab";
    private const string CorePrefabPath = "Assets/Power-ups/Amplifier Core/Amplifier Core.prefab";
    private const string EconomyProfilePath = "Assets/Scripts/Scoring/ScoreEconomyProfile.asset";
    private const string CaptureWavePath = "Assets/VectorGridNu/Traveling Wave.asset";
    private const string AuroraMaterialPath =
        "Assets/Power-ups/Amplifier Core/Amplifier Aurora Blast.mat";
    private const string AuroraVolumeMaterialPath =
        "Assets/Power-ups/Amplifier Core/Amplifier Aurora Volume.mat";

    [MenuItem("MASSIVE/Scoring/Integrate Amplifier + Multiplier HUD")]
    public static void Integrate()
    {
        ConfigureEconomyProfile();
        ConfigureCorePrefab();
        ConfigurePlayingFieldPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "[Multiplier Integration] Configured charge-based player multipliers, " +
            "team Amplifier HUDs, goal attraction/capture, and both team layouts.");
    }

    private static void ConfigureEconomyProfile()
    {
        ScoreEconomyProfile profile = AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>(
            EconomyProfilePath);
        if (profile == null)
            throw new InvalidOperationException($"Missing score profile at {EconomyProfilePath}.");

        SerializedObject serialized = new SerializedObject(profile);
        SerializedProperty chain = serialized.FindProperty("chainSettings");
        SetFloatArray(
            chain.FindPropertyRelative("chargeRequiredPerTier"),
            new[] { 4f, 6f, 8f, 10f });
        chain.FindPropertyRelative("hitPenalty").enumValueIndex =
            (int)ScoreChainHitPenalty.None;

        SerializedProperty amplifier = serialized.FindProperty("teamAmplifierSettings");
        SetIntArray(
            amplifier.FindPropertyRelative("multiplierSteps"),
            new[] { 1, 2, 4, 8 });

        SerializedProperty rewards = serialized.FindProperty("rewards");
        for (int i = 0; i < rewards.arraySize; i++)
        {
            SerializedProperty reward = rewards.GetArrayElementAtIndex(i);
            string key = reward.FindPropertyRelative("key").stringValue;
            reward.FindPropertyRelative("chainCharge").floatValue =
                RecommendedChargeFor(key);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static float RecommendedChargeFor(string rewardKey)
    {
        switch (rewardKey)
        {
            case ScoreRewardKeys.PlayerDefeat:
                return 2f;
            case ScoreRewardKeys.EnergyPickupSmall:
                return 0.5f;
            case ScoreRewardKeys.ObjectiveTick:
                return 0.25f;
            default:
                return 1f;
        }
    }

    private static void ConfigureCorePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CorePrefabPath);
        try
        {
            Transform visualRoot = Require(root.transform, "Visual Root");
            Transform energyCore = Require(visualRoot, "Energy Core");
            Transform neutralShell = Require(visualRoot, "Neutral Shell");
            AmplifierCoreVisual visual = root.GetComponent<AmplifierCoreVisual>() ??
                                         root.AddComponent<AmplifierCoreVisual>();
            SerializedObject visualSerialized = new SerializedObject(visual);
            visualSerialized.FindProperty("energyCore").objectReferenceValue = energyCore;
            visualSerialized.FindProperty("neutralShell").objectReferenceValue = neutralShell;
            visualSerialized.FindProperty("energyRenderer").objectReferenceValue =
                energyCore.GetComponent<Renderer>();
            visualSerialized.FindProperty("shellRenderer").objectReferenceValue =
                neutralShell.GetComponent<Renderer>();
            visualSerialized.FindProperty("spawnOrganicRibbonScale").floatValue = 1.5f;
            visualSerialized.ApplyModifiedPropertiesWithoutUndo();

            AmplifierCoreGameplay gameplay =
                root.GetComponent<AmplifierCoreGameplay>() ??
                root.AddComponent<AmplifierCoreGameplay>();
            SerializedObject serialized = new SerializedObject(gameplay);
            serialized.FindProperty("presentationOnly").boolValue = false;
            serialized.FindProperty("body").objectReferenceValue = root.GetComponent<Rigidbody>();
            serialized.FindProperty("collisionShape").objectReferenceValue = root.GetComponent<Collider>();
            serialized.FindProperty("visual").objectReferenceValue = visual;
            serialized.FindProperty("visualRoot").objectReferenceValue = visualRoot;
            serialized.FindProperty("playSpawnAnimationOnEnable").boolValue = true;
            serialized.FindProperty("spawnScaleSeconds").floatValue = 0.38f;
            serialized.FindProperty("spawnShellMorphSeconds").floatValue = 0.62f;
            serialized.FindProperty("spawnCoreDelaySeconds").floatValue = 0.13f;
            serialized.FindProperty("spawnCoreRevealSeconds").floatValue = 0.34f;
            serialized.FindProperty("despawnScaleSeconds").floatValue = 0.4f;
            serialized.FindProperty("despawnShellMorphSeconds").floatValue = 0.56f;
            serialized.FindProperty("despawnCoreHideSeconds").floatValue = 0.2f;
            serialized.FindProperty("attackImpulse").floatValue = 20f;
            serialized.FindProperty("playerPlanarVelocityRetention").floatValue = 0.03f;
            serialized.FindProperty("playerImpactStopSeconds").floatValue = 0.12f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, CorePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigurePlayingFieldPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayingFieldPath);
        try
        {
            Transform textObjects = Require(root.transform, "UI/Text Objects");
            Transform team1 = Require(textObjects, "TEAM_01");
            Transform team2 = Require(textObjects, "TEAM_02");

            Transform p1 = Require(team1, "Player 1 multiplier");
            Transform p2 = Require(team1, "Player 2 multiplier");
            Transform p3 = EnsureMirroredWidget(team2, p1, "Player 3 multiplier", -3.35f);
            Transform p4 = EnsureMirroredWidget(team2, p2, "Player 4 multiplier", 0f);

            ConfigurePlayerWidget(p1, 0, "P1x");
            ConfigurePlayerWidget(p2, 1, "P2x");
            ConfigurePlayerWidget(p3, 2, "P3x");
            ConfigurePlayerWidget(p4, 3, "P4x");

            ConfigureTeamAmplifier(team1, 1);
            ConfigureTeamAmplifier(team2, 2);
            ConfigureGoal(root.transform, 1);
            ConfigureGoal(root.transform, 2);

            PrefabUtility.SaveAsPrefabAsset(root, PlayingFieldPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform EnsureMirroredWidget(
        Transform team,
        Transform source,
        string name,
        float localX)
    {
        Transform existing = team.Find(name);
        if (existing != null)
            return existing;

        GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, team);
        clone.name = name;
        Transform result = clone.transform;
        Vector3 localPosition = source.localPosition;
        localPosition.x = localX;
        result.localPosition = localPosition;
        result.localRotation = source.localRotation;
        result.localScale = source.localScale;
        return result;
    }

    private static void ConfigurePlayerWidget(Transform widget, int playerID, string label)
    {
        Transform playerText = Require(widget, "Player text");
        TMP_Text playerLabel = playerText.GetComponent<TMP_Text>();
        if (playerLabel != null)
            playerLabel.text = label;

        Transform tierRoot = Require(widget, "Player multiplier levels/x1 (1)");
        TMP_Text multiplierText = Require(tierRoot, "multiplier level").GetComponent<TMP_Text>();
        Rectangle progressTrack = Require(tierRoot, "fill").GetComponent<Rectangle>();
        Rectangle progressBar = Require(tierRoot, "multiplier bar").GetComponent<Rectangle>();
        if (multiplierText == null || progressTrack == null || progressBar == null)
            throw new InvalidOperationException($"Incomplete multiplier widget at {GetPath(widget)}.");

        PlayerScoreChainPresenter presenter =
            widget.GetComponent<PlayerScoreChainPresenter>() ??
            widget.gameObject.AddComponent<PlayerScoreChainPresenter>();
        SerializedObject serialized = new SerializedObject(presenter);
        serialized.FindProperty("playerID").intValue = playerID;
        serialized.FindProperty("chain").objectReferenceValue = null;
        serialized.FindProperty("multiplierText").objectReferenceValue = multiplierText;
        serialized.FindProperty("progressImage").objectReferenceValue = null;
        serialized.FindProperty("progressRectangle").objectReferenceValue = progressBar;
        serialized.FindProperty("progressTrackRectangle").objectReferenceValue = progressTrack;
        serialized.FindProperty("progressSmoothSeconds").floatValue = 0.16f;
        serialized.FindProperty("activeRoot").objectReferenceValue = null;
        serialized.FindProperty("hideAtBaseMultiplier").boolValue = false;
        serialized.FindProperty("multiplierPrefix").stringValue = string.Empty;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureTeamAmplifier(Transform team, int teamID)
    {
        Transform amplifierRoot = Require(team, "Amplifier level");
        TMP_Text multiplierText = amplifierRoot.GetComponentInChildren<TMP_Text>(true);
        if (multiplierText == null)
            throw new InvalidOperationException($"No multiplier text under {GetPath(amplifierRoot)}.");

        TeamAmplifierPresenter presenter =
            amplifierRoot.GetComponent<TeamAmplifierPresenter>() ??
            amplifierRoot.gameObject.AddComponent<TeamAmplifierPresenter>();
        SerializedObject serialized = new SerializedObject(presenter);
        serialized.FindProperty("teamID").intValue = teamID;
        serialized.FindProperty("multiplierText").objectReferenceValue = multiplierText;
        serialized.FindProperty("multiplierPrefix").stringValue = "x";
        serialized.ApplyModifiedPropertiesWithoutUndo();

        AmplifierCoreGameplay hudCore =
            amplifierRoot.GetComponentInChildren<AmplifierCoreGameplay>(true);
        if (hudCore != null)
        {
            SerializedObject coreSerialized = new SerializedObject(hudCore);
            coreSerialized.FindProperty("presentationOnly").boolValue = true;
            coreSerialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ConfigureGoal(Transform playingField, int teamID)
    {
        string teamName = $"TEAM {teamID} goal";
        string hitboxName = $"Team {teamID} ScoreAcceptorHitbox";
        string acceptorName = $"Team {teamID} Score Acceptor";

        Transform teamGoal = Require(playingField, teamName);
        Transform hitbox = Require(teamGoal, hitboxName);
        Transform acceptor = Require(teamGoal, acceptorName);
        ParticleSystem particles = acceptor.GetComponentInChildren<ParticleSystem>(true);

        Collider trigger = hitbox.GetComponent<Collider>();
        if (trigger == null)
            throw new InvalidOperationException($"No goal collider at {GetPath(hitbox)}.");
        trigger.isTrigger = true;

        GridInteractor wave = hitbox.GetComponent<GridInteractor>() ??
                              hitbox.gameObject.AddComponent<GridInteractor>();
        wave.profile = AssetDatabase.LoadAssetAtPath<GridInteractionProfile>(CaptureWavePath);

        AmplifierGoalCapture capture =
            hitbox.GetComponent<AmplifierGoalCapture>() ??
            hitbox.gameObject.AddComponent<AmplifierGoalCapture>();
        AmplifierAuroraBlast aurora =
            hitbox.GetComponent<AmplifierAuroraBlast>() ??
            hitbox.gameObject.AddComponent<AmplifierAuroraBlast>();
        SerializedObject auroraSerialized = new SerializedObject(aurora);
        auroraSerialized.FindProperty("origin").objectReferenceValue = acceptor;
        auroraSerialized.FindProperty("particleMaterial").objectReferenceValue =
            GetOrCreateAuroraMaterial();
        auroraSerialized.FindProperty("volumeMaterial").objectReferenceValue =
            GetOrCreateAuroraVolumeMaterial();
        auroraSerialized.FindProperty("volumeLifetime").floatValue = 1.55f;
        auroraSerialized.FindProperty("volumeStartDiameter").floatValue = 0.34f;
        auroraSerialized.FindProperty("volumeMaximumDiameter").floatValue = 5.2f;
        auroraSerialized.FindProperty("volumeInwardDrift").floatValue = 0.72f;
        auroraSerialized.FindProperty("gasParticleCount").intValue = 14;
        auroraSerialized.FindProperty("gasLifetime").floatValue = 1.18f;
        auroraSerialized.FindProperty("gasSpeed").floatValue = 1.65f;
        auroraSerialized.FindProperty("gasSize").floatValue = 0.46f;
        auroraSerialized.FindProperty("ribbonParticleCount").intValue = 8;
        auroraSerialized.FindProperty("ribbonLifetime").floatValue = 0.92f;
        auroraSerialized.FindProperty("ribbonSpeed").floatValue = 2.65f;
        auroraSerialized.FindProperty("ribbonSize").floatValue = 0.12f;
        auroraSerialized.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serialized = new SerializedObject(capture);
        serialized.FindProperty("teamID").intValue = teamID;
        serialized.FindProperty("capturePoint").objectReferenceValue = acceptor;
        serialized.FindProperty("captureParticles").objectReferenceValue = particles;
        serialized.FindProperty("auroraBlast").objectReferenceValue = aurora;
        serialized.FindProperty("captureWave").objectReferenceValue = wave;
        serialized.FindProperty("captureWaveTag").stringValue = "Wave";
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Material GetOrCreateAuroraMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(AuroraMaterialPath);
        if (material != null)
            return material;

        Shader shader = Shader.Find("MASSIVE/Amplifier Core/Aurora Particle");
        if (shader == null)
        {
            throw new InvalidOperationException(
                "Amplifier Aurora particle shader has not finished importing.");
        }

        material = new Material(shader)
        {
            name = "Amplifier Aurora Blast"
        };
        material.SetFloat("_Softness", 0.62f);
        material.SetFloat("_Distortion", 0.48f);
        material.SetFloat("_Brightness", 1.35f);
        AssetDatabase.CreateAsset(material, AuroraMaterialPath);
        return material;
    }

    private static Material GetOrCreateAuroraVolumeMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(AuroraVolumeMaterialPath);
        if (material != null)
            return material;

        Shader shader = Shader.Find("MASSIVE/Amplifier Core/Aurora Volume");
        if (shader == null)
        {
            throw new InvalidOperationException(
                "Amplifier Aurora volume shader has not finished importing.");
        }

        material = new Material(shader)
        {
            name = "Amplifier Aurora Volume"
        };
        material.SetColor("_ColorA", new Color(1f, 0.08f, 0.72f, 1f));
        material.SetColor("_ColorB", new Color(0.02f, 0.78f, 1f, 1f));
        material.SetColor("_ColorC", new Color(0.55f, 1f, 0.12f, 1f));
        material.SetFloat("_Density", 1.45f);
        material.SetFloat("_NoiseScale", 3.8f);
        material.SetFloat("_NoiseThreshold", 0.42f);
        material.SetFloat("_WarpStrength", 0.72f);
        material.SetFloat("_FlowSpeed", 0.68f);
        material.SetFloat("_Brightness", 1.65f);
        AssetDatabase.CreateAsset(material, AuroraVolumeMaterialPath);
        return material;
    }

    private static Transform Require(Transform parent, string path)
    {
        Transform result = parent.Find(path);
        if (result == null)
            throw new InvalidOperationException($"Missing '{path}' under {GetPath(parent)}.");
        return result;
    }

    private static string GetPath(Transform target)
    {
        if (target == null)
            return "<null>";

        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = $"{target.name}/{path}";
        }
        return path;
    }

    private static void SetIntArray(SerializedProperty property, int[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).intValue = values[i];
    }

    private static void SetFloatArray(SerializedProperty property, float[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).floatValue = values[i];
    }
}
#endif
