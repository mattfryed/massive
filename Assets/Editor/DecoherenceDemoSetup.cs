#if UNITY_EDITOR
using System;
using System.Linq;
using Massive.Demonstrations;
using Massive.Enemies;
using Massive.PowerUps;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DecoherenceDemoSetup
{
    [MenuItem("MASSIVE/Demonstrations/Set Up Decoherence Demo")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        var scene = SceneManager.GetActiveScene();
        var lab = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerActionTestArena>(true)).SingleOrDefault();
        if (!lab) throw new InvalidOperationException("Open the Player Action Lab scene first.");
        var serialized = new SerializedObject(lab);
        if (serialized.FindProperty("decoherenceContent").objectReferenceValue)
        {
            serialized.FindProperty("mode").enumValueIndex = (int)PlayerActionTestArena.TestMode.Decoherence;
            serialized.ApplyModifiedProperties(); return;
        }
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab");
        var powerUp = AssetDatabase.LoadAssetAtPath<DecoherencePowerUpDefinition>("Assets/Power-ups/Decoherence/PU_Decoherence.asset");
        var drone = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Enemy System/Enemy Types/Melee/Drone/ED_Drone.asset");
        var carrier = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Enemy System/Enemy Types/Melee/Carrier/ED_Carrier.asset");
        var grid = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<VectorGridGPU>(true)).First(g => g.gameObject.activeInHierarchy);
        if (!player || !powerUp || !drone || !carrier) throw new InvalidOperationException("Missing canonical demo assets.");
        Undo.SetCurrentGroupName("Set up Decoherence demo"); int group = Undo.GetCurrentGroup();
        var content = new GameObject("Decoherence Focus Demo"); content.transform.SetParent(lab.transform, false); content.SetActive(false);
        Undo.RegisterCreatedObjectUndo(content, "Create Decoherence demo");
        var plate = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scripts/Player/Demonstrations/Action Gallery/Label backing.mat");
        Label(content.transform, "Heading", "DECOHERENCE", new Vector3(0, .3f, 5.8f), 27f, .7f, 5f, plate);
        Label(content.transform, "Instruction", "HOLD SHIELD + MOVE   /   Paint the arena. Release to refill while the trail stays behind.",
            new Vector3(0, .3f, 5.1f), 27f, .5f, 2.7f, plate);
        string[] titles = { "01  SWEEP THROUGH DRONES", "02  ERODE A CARRIER", "03  PRESSURE / COUNTERPLAY" };
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject(titles[i]); go.transform.SetParent(content.transform, false);
            go.transform.localPosition = Vector3.right * ((i - 1) * 9f);
            var lane = go.AddComponent<DecoherenceDemoLane>();
            lane.example = (DecoherenceDemoLane.Example)i; lane.playerPrefab = player; lane.powerUp = powerUp;
            lane.enemyDefinition = i == 0 ? drone : i == 1 ? carrier : null; lane.grid = grid;
            Label(go.transform, "Title", titles[i], new Vector3(0, .3f, 3.6f), 8.5f, .55f, 3f, plate);
            lane.explanation = Label(go.transform, "Explanation", "", new Vector3(0, .3f, 2.95f), 8.5f, .75f, 2.2f, plate);
            lane.status = Label(go.transform, "Status", "Ready", new Vector3(0, .3f, -2.8f), 8.5f, .8f, 2.5f, plate);
            lane.health = Label(go.transform, "Live health", "", new Vector3(0, .3f, -3.8f), 8.5f, 1.1f, 2.5f, plate);
        }
        Label(content.transform, "Footer", "Shared charge rings  |  Smaller brush as charge drains  |  Reactivate from 15%  |  Sample enemies do not attack",
            new Vector3(0, .3f, -5.15f), 27f, .5f, 2.3f, plate);
        serialized.FindProperty("decoherenceContent").objectReferenceValue = content;
        serialized.FindProperty("mode").enumValueIndex = (int)PlayerActionTestArena.TestMode.Decoherence;
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = lab.gameObject;
        Debug.Log("Decoherence demo installed. Press Play, or choose Decoherence in the lab panel.");
    }

    private static TMP_Text Label(Transform parent, string name, string value, Vector3 position, float width, float height, float size, Material plate)
    {
        if (plate)
        {
            var backing = GameObject.CreatePrimitive(PrimitiveType.Quad); backing.name = name + " backing";
            UnityEngine.Object.DestroyImmediate(backing.GetComponent<Collider>());
            backing.transform.SetParent(parent, false); backing.transform.localPosition = position - Vector3.up * .02f;
            backing.transform.localRotation = Quaternion.Euler(90f, 0, 0); backing.transform.localScale = new Vector3(width, height, 1f);
            backing.GetComponent<MeshRenderer>().sharedMaterial = plate;
        }
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        var text = go.AddComponent<TextMeshPro>(); text.font = TMP_Settings.defaultFontAsset;
        text.text = value; text.alignment = TextAlignmentOptions.Center; text.color = Color.white;
        text.rectTransform.sizeDelta = new Vector2(width, height); text.enableAutoSizing = true;
        text.fontSize = text.fontSizeMax = size; text.fontSizeMin = 1.6f; text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
}
#endif
