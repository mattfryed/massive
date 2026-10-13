using System;
using System.IO;
using System.Linq;
using Massive.Cosmos;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CosmicWebBaking : IPreprocessBuildWithReport, IProcessSceneWithReport
{
    public const string Folder = "Assets/Scripts/Anomalies/COSMOS/Baked";
    const string GeneratorPath = "Assets/Scripts/Anomalies/COSMOS/CosmicWebTopology.cs";
    const string ComponentPath = "Assets/Scripts/Anomalies/COSMOS/CosmicWebBackground.cs";
    public int callbackOrder => 100;

    public static string Fingerprint => Hash128.Compute(File.ReadAllText(GeneratorPath) + Application.unityVersion).ToString();

    public static CosmicWebBake GetOrCreate(CosmicWebBackground web, bool force = false)
    {
        int count = Mathf.Clamp(web.particleCount, 8000, 100000);
        string key = Hash128.Compute(count + "|" + web.seed + "|" +
            BitConverter.SingleToInt32Bits(web.organicStrength) + "|" + CosmicWebBake.FormatVersion).ToString();
        string path = Folder + "/CosmicWeb-" + key + ".asset";
        var bake = AssetDatabase.LoadAssetAtPath<CosmicWebBake>(path);
        string fingerprint = Fingerprint;
        if (!force && bake && bake.Matches(count, web.seed, web.organicStrength) && bake.MatchesGenerator(fingerprint)) return bake;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var data = CosmicWebTopology.Build(count, web.seed, out var statistics, web.organicStrength);
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Scripts/Anomalies/COSMOS", "Baked");
        bool created = !bake;
        if (created) bake = ScriptableObject.CreateInstance<CosmicWebBake>();
        bake.SetData(count, web.seed, web.organicStrength, fingerprint, data, statistics);
        if (created) AssetDatabase.CreateAsset(bake, path);
        EditorUtility.SetDirty(bake);
        AssetDatabase.SaveAssetIfDirty(bake);
        Debug.Log("COSMOS baked " + count + " particles in " + timer.ElapsedMilliseconds + " ms: " + path);
        return bake;
    }

    static CosmicWebBackground[] Webs(Scene scene)
        => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CosmicWebBackground>(true)).ToArray();

    public void OnPreprocessBuild(BuildReport report)
    {
        // Generate assets before the build dependency pass. Also covers components nested in prefabs.
        string guid = AssetDatabase.AssetPathToGUID(ComponentPath);
        var paths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        foreach (string path in AssetDatabase.GetDependencies(paths, true))
        {
            if (!(path.EndsWith(".unity") || path.EndsWith(".prefab")) || !File.ReadAllText(path).Contains(guid)) continue;
            if (path.EndsWith(".unity"))
            {
                var preview = EditorSceneManager.OpenPreviewScene(path);
                try { foreach (var web in Webs(preview)) GetOrCreate(web); }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            else
            {
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try { foreach (var web in prefab.GetComponentsInChildren<CosmicWebBackground>(true)) GetOrCreate(web); }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
        }
    }

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null) return; // Do not edit authoring scenes when entering Play Mode.
        foreach (var web in Webs(scene)) web.bakedTopology = GetOrCreate(web);
    }

    [MenuItem("MASSIVE/COSMOS/Cosmic web/Bake backgrounds in open scenes")]
    public static void BakeOpenScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Bake in Edit Mode.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (var web in Webs(SceneManager.GetSceneAt(i))) Assign(web);
    }

    public static void Assign(CosmicWebBackground web, bool force = false)
    {
        var bake = GetOrCreate(web, force);
        if (web.bakedTopology != bake)
        {
            Undo.RecordObject(web, "Assign COSMOS background bake");
            web.bakedTopology = bake;
            PrefabUtility.RecordPrefabInstancePropertyModifications(web);
            EditorUtility.SetDirty(web);
            if (web.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(web.gameObject.scene);
        }
        web.Rebuild();
    }
}

[CustomEditor(typeof(CosmicWebBackground)), CanEditMultipleObjects]
public sealed class CosmicWebBackgroundEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        bool ready = targets.Cast<CosmicWebBackground>().All(web => web.bakedTopology &&
            web.bakedTopology.Matches(web.particleCount, web.seed, web.organicStrength) &&
            web.bakedTopology.MatchesGenerator(CosmicWebBaking.Fingerprint));
        EditorGUILayout.HelpBox(ready
            ? "Topology bake matches these settings. Animation and appearance controls remain live."
            : "Topology bake is missing or outdated. Preview still uses current settings. Bake here for fast Play Mode startup; player builds refresh their bakes automatically.",
            ready ? MessageType.Info : MessageType.Warning);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Bake / Refresh Background Topology"))
                foreach (CosmicWebBackground web in targets) CosmicWebBaking.Assign(web, true);
    }
}
