#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Massive.Cosmos;
using Massive.Lattice;
using Massive.Levels;
using Massive.Multiplier;
using Shapes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CosmosLayoutSetup
{
    public const string ScenePath = "Assets/Scenes/S-9_COSMOS.unity";
    const string Content = "Assets/Scripts/Anomalies/COSMOS";
    public static T[] All<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();

    [MenuItem("MASSIVE/COSMOS/Apply oval layout %#&F10")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("Open COSMOS in Edit Mode.");
        Directory.CreateDirectory("Library/CosmosLayout");
        if (!File.Exists("Library/CosmosLayout/COSMOS-before.unity"))
            File.Copy(ScenePath, "Library/CosmosLayout/COSMOS-before.unity");
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("COSMOS oval layout");
        int group = Undo.GetCurrentGroup();
        foreach (var root in scene.GetRootGameObjects()) Undo.RegisterFullObjectHierarchyUndo(root, "COSMOS oval layout");
        var grid = All<VectorGridGPU>().Single();
        var existingLayout = grid.GetComponent<CosmosArenaLayout>();
        if (existingLayout && existingLayout.layoutRevision < 3) RestoreOriginalSpawnMarkers();
        var goalRoot = All<Transform>().Single(t => t.name == "Goals");
        var protectedPoses = All<Transform>().Where(t => !t.IsChildOf(goalRoot) && !t.IsChildOf(grid.transform))
            .ToDictionary(t => t, t => (t.localPosition, t.localRotation, t.localScale));
        var protectedCameras = All<Camera>().ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
        var bounds = grid.GetComponent<ArenaBoundsFromVectorGrid>();
        if (!bounds.ovalOutline) bounds.ovalEndHeightRatio = .7f;
        bounds.ovalOutline = true;
        grid.boundary.borderOverlay = false;
        var lattice = grid.GetComponent<LatticeDisruptionField>();
        if (lattice) lattice.enabled = false;
        // The project also has an Editor-assembly class with this name. Select the
        // actual attached component instead of resolving that shadowing type.
        foreach (var rectangularWalls in grid.GetComponents<MonoBehaviour>().Where(c => c && c.GetType().Name == "ArenaBoundaryCollidersFromVectorGrid"))
            rectangularWalls.enabled = false;
        foreach (Transform t in grid.transform)
            if (t.name.StartsWith("ArenaWall_", StringComparison.Ordinal)) t.gameObject.SetActive(false);
        var layout = grid.GetComponent<CosmosArenaLayout>();
        if (!layout) layout = Undo.AddComponent<CosmosArenaLayout>(grid.gameObject);
        if (layout.layoutRevision < 2)
        {
            grid.size = new Vector2(24, 14);
            bounds.ovalEndHeightRatio = Mathf.Sqrt(1 - Mathf.Pow(12f / 15.6f, 2));
            layout.layoutRevision = 2;
        }
        layout.layoutRevision = 3;
        layout.boundaryShader = AssetDatabase.LoadAssetAtPath<Shader>(Content + "/CosmosBoundary.shader");
        string maskPath = Content + "/CosmosArenaMask.mat";
        var maskMaterial = AssetDatabase.LoadAssetAtPath<Material>(maskPath);
        if (!maskMaterial)
        {
            maskMaterial = new Material(AssetDatabase.LoadAssetAtPath<Shader>(Content + "/CosmosArenaMask.shader"));
            AssetDatabase.CreateAsset(maskMaterial, maskPath);
        }
        layout.fieldMasks = All<Transform>().Single(t => t.name == "Mask Planes").GetComponentsInChildren<Renderer>(true);
        foreach (var mask in layout.fieldMasks) { mask.sharedMaterial = maskMaterial; Record(mask); }
        bounds.RefreshNow(); grid.RefreshLayout(); layout.Rebuild();

        FitGoals(layout, All<ScoreSphereScript>());

        var context = All<LevelSceneContext>().Single();
        string definitionPath = Content + "/LevelDefinition-COSMOS.asset";
        var definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(definitionPath);
        if (!definition)
        {
            definition = ScriptableObject.CreateInstance<LevelDefinition>();
            definition.audioProfile = context.level.audioProfile;
            AssetDatabase.CreateAsset(definition, definitionPath);
        }
        definition.levelTitle = "COSMOS"; definition.levelNumber = 9; definition.scaleExponent = 26;
        definition.anomalyTypeName = "COSMIC WEB";
        var definitionObject = new SerializedObject(definition);
        definitionObject.FindProperty("gameplayScene.sceneAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        definitionObject.FindProperty("gameplayScene.sceneName").stringValue = "S-9_COSMOS";
        definitionObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(definition);
        context.level = definition;
        string stagePath = Content + "/StageProfile-COSMOS.asset";
        var stage = AssetDatabase.LoadAssetAtPath<StageProfile>(stagePath);
        if (!stage)
        {
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(context.stage), stagePath);
            stage = AssetDatabase.LoadAssetAtPath<StageProfile>(stagePath);
        }
        var stageObject = new SerializedObject(stage);
        stageObject.FindProperty("stageId").stringValue = "STAGE_009";
        stageObject.FindProperty("displayName").stringValue = "COSMOS";
        stageObject.ApplyModifiedProperties();
        context.stage = stage;
        if (context.anomalies) context.anomalies.stageProfile = stage;
        if (context.stageTitleText) { context.stageTitleText.text = "COSMOS"; Record(context.stageTitleText); }
        if (context.stageNumberText) { context.stageNumberText.text = "STAGE_009"; Record(context.stageNumberText); }
        foreach (var t in All<Transform>()) if (t.name == "LATTICE") t.name = "COSMOS";
        // Only grid and goal poses may change. Preserve the user's restored camera,
        // HUD, players, spawn anchors and every other authored transform.
        foreach (var entry in protectedPoses)
            if (entry.Value != (entry.Key.localPosition, entry.Key.localRotation, entry.Key.localScale))
                throw new InvalidOperationException("Unexpected non-arena pose change: " + entry.Key.name);
        foreach (var entry in protectedCameras)
            if (entry.Value != EditorJsonUtility.ToJson(entry.Key))
                throw new InvalidOperationException("Unexpected camera change: " + entry.Key.name);
        File.WriteAllText("Library/CosmosLayout/layout-preservation.txt", "PASS All " + protectedPoses.Count +
            " non-grid/goal transforms and " + protectedCameras.Count + " cameras unchanged.\n");
        Record(context); if (context.anomalies) Record(context.anomalies);
        EditorUtility.SetDirty(context); EditorUtility.SetDirty(grid); EditorUtility.SetDirty(bounds);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        SceneView.RepaintAll();
        Debug.Log($"COSMOS ellipse saved: {grid.size.x:0.##} x {grid.size.y:0.##} field, {2 * bounds.OvalHalfWidthLocal:0.##} overall width; camera and non-arena poses preserved.");
    }
    // Shared by initial scene setup and the dimension sliders; one scaling path
    // keeps score growth, capture volumes and local effects concordant.
    internal static void FitGoals(CosmosArenaLayout layout, ScoreSphereScript[] scores)
    {
        var bounds = layout.GetComponent<ArenaBoundsFromVectorGrid>();
        var grid = bounds.Grid;
        foreach (var score in scores)
        {
            Transform root = score.transform.parent;
            var ring = root.GetComponentInChildren<Disc>(true);
            if (!ring) throw new InvalidOperationException("Goal ring missing: " + root.name);
            float currentRadius = ring.transform.TransformVector(Vector3.right * ring.Radius).magnitude;
            float desiredRadius = bounds.OvalGoalHalfHeightLocal * Mathf.Abs(grid.transform.lossyScale.y);
            float factor = desiredRadius / currentRadius;
            Vector3 seam = ring.transform.position;
            int side = score.teamID == 1 ? -1 : 1;
            Vector3 newSeam = grid.transform.TransformPoint(new Vector3(side * grid.size.x * .5f, 0, 0));
            foreach (Transform child in root)
            {
                Vector3 p = child.position;
                p.x = newSeam.x + (p.x - seam.x) * factor;
                p.z = newSeam.z + (p.z - seam.z) * factor;
                child.position = p;
                child.localScale *= factor;
                Record(child);
            }
            // This legacy presentation driver must never resize the capture collider.
            var scoreObject = new SerializedObject(score);
            scoreObject.FindProperty("sphereGraphic").objectReferenceValue = root.Find("Score Sphere").gameObject;
            scoreObject.FindProperty("minSize").floatValue *= factor;
            scoreObject.FindProperty("maxSize").floatValue *= factor;
            scoreObject.ApplyModifiedProperties();
            var capture = score.GetComponent<AmplifierGoalCapture>();
            Scale(capture, factor, "attractionRadius", "captureRadius");
            Scale(score.GetComponent<AmplifierAuroraBlast>(), factor, "volumeStartDiameter", "volumeMaximumDiameter", "volumeInwardDrift", "gasSpeed", "gasSize", "ribbonSpeed", "ribbonSize");
            var visual = root.GetComponentInChildren<ScoreVoidMetaballsVisual>();
            var goalShape = ring.GetComponent<CosmosGoalShape>();
            if (!goalShape) goalShape = Undo.AddComponent<CosmosGoalShape>(ring.gameObject);
            goalShape.arena = bounds; goalShape.side = side; goalShape.boundaryShader = layout.boundaryShader;
            Record(goalShape);
            var visualObject = new SerializedObject(visual);
            visualObject.FindProperty("containerBoundary").objectReferenceValue = ring;
            visualObject.FindProperty("ellipticalContainer").objectReferenceValue = goalShape;
            visualObject.FindProperty("scoreSource").objectReferenceValue = score;
            visualObject.FindProperty("teamIDOverride").intValue = score.teamID;
            visualObject.ApplyModifiedProperties();
            goalShape.FitRenderer(visual.transform); Record(visual.transform);
            // The layout and goal adapter draw the elliptical outer/promotion arcs.
            // Keep the Disc as an invisible size/thickness template for legacy consumers.
            Color ringColor = ring.Color; ringColor.a = 0; ring.Color = ringColor; Record(ring);
        }
    }
    static void Scale(UnityEngine.Object value, float factor, params string[] names)
    {
        if (!value) return;
        var so = new SerializedObject(value);
        foreach (string name in names) so.FindProperty(name).floatValue *= factor;
        so.ApplyModifiedProperties();
    }
    static void RestoreOriginalSpawnMarkers()
    {
        // Undo only this tool's first-pass clamping of unused markers. Exact original
        // poses were verified against Library/CosmosLayout/COSMOS-before.unity.
        // Actual player MatchSpawnAnchor references point to the separate Respawn objects.
        var root = All<Transform>().Single(t => t.name == "SpawnAnchors");
        var previous = new[] { new Vector3(-13.2f, 0, -1.6549023f), new Vector3(13.2f, 0, 1.4780841f), new Vector3(13.2f, 0, -1.6677847f) };
        var original = new[] { new Vector3(-16.83f, 0, -2.11f), new Vector3(16.7f, 0, 1.87f), new Vector3(16.7f, 0, -2.11f) };
        for (int i = 0; i < previous.Length; i++)
        {
            var marker = root.Find("P" + (i + 2));
            if (marker && Vector3.Distance(marker.localPosition, previous[i]) < .001f)
            { marker.localPosition = original[i]; Record(marker); }
        }
    }
    static void Record(UnityEngine.Object value)
    {
        EditorUtility.SetDirty(value);
        if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
    }
}
#endif
