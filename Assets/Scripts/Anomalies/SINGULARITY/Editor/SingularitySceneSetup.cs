using System;
using Massive.Singularity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SingularitySceneSetup
{
    public const string ScenePath = "Assets/Scenes/SINGULARITY-PROTOTYPE.unity";

    [MenuItem("MASSIVE/SINGULARITY/Create Prototype Scene")]
    public static void CreateMenu() { Debug.Log(Create()); }

    /// <summary>Create only a missing isolated scene, without saving or closing the user's scene.</summary>
    public static string Create()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before creating this scene.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            return "SINGULARITY already exists. Left it unchanged: " + ScenePath;
        Shader gridShader = Shader.Find("MASSIVE/Singularity/Grid");
        Shader playerShader = Shader.Find("MASSIVE/Singularity/Player Surface");
        if (!gridShader || !playerShader) throw new InvalidOperationException("Import and compile the SINGULARITY shaders first.");
        Scene original = SceneManager.GetActiveScene();
        Scene created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(created);
            var root = new GameObject("SINGULARITY - Surface");
            var surface = root.AddComponent<SingularitySurface>();
            surface.ConfigureProjectedBounds(28f, 12f, .875f, 3f, 2.1f);

            var playerObject = new GameObject("Player - Surface Probe");
            playerObject.transform.SetParent(root.transform, false);
            var player = playerObject.AddComponent<SingularityPlayerMotor>();
            player.Surface = surface;
            var playerProperties = new SerializedObject(player);
            playerProperties.FindProperty("playerShader").objectReferenceValue = playerShader;
            playerProperties.ApplyModifiedPropertiesWithoutUndo();

            var gridObject = new GameObject("Loop Grid");
            gridObject.transform.SetParent(root.transform, false);
            var grid = gridObject.AddComponent<SingularityGridRenderer>();
            grid.surface = surface; grid.player = player; grid.gridShader = gridShader;
            grid.lineWidthPixels = 1.8f;
            grid.frontColor = new Color(1f, 1f, 1f, .9f);
            grid.rearColor = new Color(.55f, .68f, .78f, .6f);
            grid.Rebuild(); player.RefreshVisual();

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 60f, 0f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.orthographic = true; camera.orthographicSize = 9f;
            camera.nearClipPlane = .1f; camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.allowHDR = true; camera.allowMSAA = true;
            cameraObject.AddComponent<AudioListener>();
            var framing = cameraObject.AddComponent<SingularityCameraFraming>();
            framing.surface = surface; framing.autoFrame = false; framing.minimumOrthographicSize = 9f;

            var hud = new GameObject("Navigation Legend").AddComponent<SingularityPrototypeHUD>();
            hud.surface = surface; hud.player = player;

            // Reuse the authored device maps. No game manager / match / scoring bootstrap.
            var inputPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rewired Input Manager.prefab");
            if (inputPrefab) PrefabUtility.InstantiatePrefab(inputPrefab, created);

            if (!EditorSceneManager.SaveScene(created, ScenePath))
                throw new InvalidOperationException("Could not save the new SINGULARITY prototype.");
            return "Created " + ScenePath + ". Original scene unchanged; not added to Build Settings or level selection.";
        }
        finally
        {
            EditorSceneManager.CloseScene(created, true);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
        }
    }

    [MenuItem("MASSIVE/SINGULARITY/Open Prototype Scene")]
    public static void Open()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before opening this scene.");
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) { Create(); }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var surface = UnityEngine.Object.FindFirstObjectByType<SingularitySurface>();
        if (surface) Selection.activeGameObject = surface.gameObject;
    }
}
