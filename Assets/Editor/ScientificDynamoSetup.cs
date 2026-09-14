#if UNITY_EDITOR
using System;
using System.IO;
using Massive.Dynamo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ScientificDynamoSetup
{
    public const string DataFolder = "Assets/Scripts/Anomalies/Magnetosphere/ScientificData/January2026";
    public const string EpisodePath = DataFolder + "/January pressure pulse.asset";
    public const string PrefabPath = "Assets/Scripts/Anomalies/Magnetosphere/Scientific Dynamo.prefab";
    public const string DemoPath = "Assets/Scenes/Dynamo Scientific Demo.unity";
    public const string GameplayPath = "Assets/Scenes/S-8_DYNAMO-SCIENTIFIC.unity";
    private const string RootFolder = "Assets/Scripts/Anomalies/Magnetosphere/";

    [MenuItem("MASSIVE/Dynamo/Build Scientific Prototype")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before building the prototype.");
        var episode = AssetDatabase.LoadAssetAtPath<ScientificStormEpisode>(EpisodePath);
        if (!episode)
        {
            episode = ScriptableObject.CreateInstance<ScientificStormEpisode>();
            AssetDatabase.CreateAsset(episode, EpisodePath);
        }
        episode.manifest = JsonUtility.FromJson<StormManifest>(File.ReadAllText(DataFolder + "/manifest.json"));
        episode.frames = new TextAsset[episode.manifest.frames.Length];
        for (int i = 0; i < episode.frames.Length; i++)
            episode.frames[i] = AssetDatabase.LoadAssetAtPath<TextAsset>(DataFolder + "/" + episode.manifest.frames[i].file);
        episode.ValidateData();
        EditorUtility.SetDirty(episode);
        AssetDatabase.SaveAssetIfDirty(episode);
        var material = AssetDatabase.LoadAssetAtPath<Material>(RootFolder + "Scientific Field Lines.mat");
        if (!material)
        {
            var shader = Shader.Find("MASSIVE/Scientific Field Lines");
            if (!shader) throw new InvalidOperationException("Scientific line shader has not imported.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, RootFolder + "Scientific Field Lines.mat");
        }
        var tracer = AssetDatabase.LoadAssetAtPath<ComputeShader>(RootFolder + "ScientificFieldLines.compute");
        if (!tracer) throw new InvalidOperationException("Scientific tracer has not imported.");
        var original = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var root = CreateField(episode, material, tracer);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            var camera = new GameObject("Scientific view").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.008f,.006f,.02f); camera.nearClipPlane = .1f; camera.farClipPlane = 200;
            camera.fieldOfView = 50; camera.allowHDR = true;
            camera.transform.position = new Vector3(15,18,-34); camera.transform.LookAt(Vector3.zero);
            new GameObject("Main Light").AddComponent<Light>().type = LightType.Directional;
            var demo = new GameObject("Scientific playback controls").AddComponent<ScientificDynamoDemo>();
            Set(demo,"source",root.GetComponent<ScientificMagnetosphere>()); Set(demo,"view",camera); Set(demo,"orbitTarget",root.transform);
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoPath)) EditorSceneManager.SaveScene(scene,DemoPath);
        }
        finally { EditorSceneManager.CloseScene(scene,true); SceneManager.SetActiveScene(original); }
        // A new copy integrates with the real players without resaving the user's prototype scene.
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayPath))
        {
            if (!AssetDatabase.CopyAsset("Assets/Scenes/S-8_DYNAMO-PROTOTYPE.unity",GameplayPath))
                throw new InvalidOperationException("Cannot create scientific gameplay scene.");
            var gameplay = EditorSceneManager.OpenScene(GameplayPath,OpenSceneMode.Additive);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab,gameplay);
                var source = root.GetComponent<ScientificMagnetosphere>();
                foreach (var go in gameplay.GetRootGameObjects())
                {
                    foreach (var legacy in go.GetComponentsInChildren<DynamoStormController>(true)) legacy.enabled = false;
                    foreach (var legacy in go.GetComponentsInChildren<MagnetosphereFieldLinesGPU2D>(true)) legacy.enabled = false;
                    foreach (var legacy in go.GetComponentsInChildren<StormWindFlowVisualizer>(true)) legacy.enabled = false;
                    foreach (var push in go.GetComponentsInChildren<PlayerStormPush>(true))
                    {
                        push.SetScientificField(source);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(push);
                    }
                }
                EditorSceneManager.MarkSceneDirty(gameplay);
                EditorSceneManager.SaveScene(gameplay);
            }
            finally { EditorSceneManager.CloseScene(gameplay,true); SceneManager.SetActiveScene(original); }
        }
        Debug.Log("[Dynamo] Scientific episode, reusable prefab, demo and gameplay scene are ready.");
    }

    private static GameObject CreateField(ScientificStormEpisode episode, Material material, ComputeShader tracer)
    {
        var root = new GameObject("Scientific Dynamo");
        var source = root.AddComponent<ScientificMagnetosphere>(); Set(source,"episode",episode);
        var catalogue = new SerializedObject(source); var entries = catalogue.FindProperty("catalogue");
        entries.arraySize = 1; entries.GetArrayElementAtIndex(0).objectReferenceValue = episode; catalogue.ApplyModifiedPropertiesWithoutUndo();
        foreach (bool flow in new[] {false,true})
        {
            var lines = new GameObject(flow ? "Local plasma flow" : "Magnetic field lines"); lines.transform.SetParent(root.transform,false);
            var renderer = lines.AddComponent<ScientificFieldLineRenderer>();
            Set(renderer,"source",source); Set(renderer,"lineMaterial",material); Set(renderer,"tracer",tracer);
            var so = new SerializedObject(renderer);
            so.FindProperty("flowLines").boolValue = flow;
            if (flow)
            {
                so.FindProperty("seedCount").intValue = 96; so.FindProperty("steps").intValue = 20;
                so.FindProperty("stepRe").floatValue = .35f; so.FindProperty("brightness").floatValue = 1f;
                so.FindProperty("widthPixels").floatValue = 1.3f;
                so.FindProperty("nearColor").colorValue = new Color(.2f,.85f,1);
                so.FindProperty("farColor").colorValue = new Color(.1f,.65f,1f);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var earth = GameObject.CreatePrimitive(PrimitiveType.Sphere); earth.name = "Earth (1 Re)";
        earth.transform.SetParent(root.transform,false); earth.transform.localScale = Vector3.one * 1.4f;
        UnityEngine.Object.DestroyImmediate(earth.GetComponent<Collider>());
        string path = RootFolder + "Scientific Earth.mat";
        var planetMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!planetMaterial)
        {
            planetMaterial = new Material(Shader.Find("Unlit/Color")) { color = new Color(.08f,.13f,.22f) };
            AssetDatabase.CreateAsset(planetMaterial,path);
        }
        earth.GetComponent<Renderer>().sharedMaterial = planetMaterial;
        return root;
    }

    private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target); serialized.FindProperty(property).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
