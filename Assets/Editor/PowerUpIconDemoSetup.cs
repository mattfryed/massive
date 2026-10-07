#if UNITY_EDITOR
using System;
using System.Linq;
using Massive.Demonstrations;
using Massive.PowerUps;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class PowerUpIconDemoSetup
{
    public const string Output = "Library/PowerUpIconDemoValidation";
    [MenuItem("MASSIVE/Demonstrations/Set Up Power-up Icon Row %#&F2")]
    public static void Install()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != PlayerActionTestArenaSetup.ScenePath)
            throw new InvalidOperationException("Open PLAYER ACTIONS in Edit Mode.");
        var gallery = All<PlayerDemoGallery>().Single();
        var content = new SerializedObject(gallery).FindProperty("content").objectReferenceValue as GameObject;
        if (!content) throw new InvalidOperationException("Missing simultaneous demo content.");
        if (content.GetComponentInChildren<PowerUpIconDemo>(true)) { Validate(); return; }
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab");
        var toasts = All<PowerUpPickupToastSystem>().Single();
        var font = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Power-ups/PU_PickupToast.prefab").GetComponentInChildren<TMP_Text>(true);
        string[] paths = { "Time Dilation/PU_TimeDilation", "Particle Accelerator/PU_ParticleAccelerator", "Decoherence/PU_Decoherence", "Mass Node/PU_MassNode" };
        var definitions = paths.Select(p => AssetDatabase.LoadAssetAtPath<PowerUpDefinition>("Assets/Power-ups/" + p + ".asset")).ToArray();
        if (!player || definitions.Any(d => !d || !d.pickupPrefab)) throw new InvalidOperationException("Missing canonical power-up assets.");
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add power-up icon row");
        var row = new GameObject("Power-up Icons - Natural and Hit");
        Undo.RegisterCreatedObjectUndo(row, "Add power-up icon row"); row.transform.SetParent(content.transform, false);
        for (int i = 0; i < definitions.Length; i++)
        {
            var go = new GameObject(definitions[i].displayName); go.transform.SetParent(row.transform, false);
            go.transform.localPosition = new Vector3(-10.5f + i * 7f, 0, -4.45f);
            var demo = go.AddComponent<PowerUpIconDemo>(); demo.definition = definitions[i]; demo.playerPrefab = player;
            demo.grid = gallery.Demos[0].Grid; demo.toasts = toasts; demo.initialHitDelay = i * .65f;
            demo.naturalMarker = Marker(go.transform, "Natural spawn", -2.1f);
            demo.hitMarker = Marker(go.transform, "Player hit", 1.5f);
            Label(go.transform, "Power-up name", definitions[i].displayName.ToUpperInvariant(), new Vector3(0, .25f, 1.35f), 6.6f, 2.4f, font);
            demo.naturalLabel = Label(go.transform, "Natural status", "NATURAL", new Vector3(-1.75f, .25f, -1.02f), 3.4f, 1.5f, font);
            demo.hitLabel = Label(go.transform, "Hit status", "HIT", new Vector3(1.75f, .25f, -1.02f), 3.4f, 1.5f, font);
        }
        // Make a third row inside the same arena without rescaling any gameplay actor or effect.
        for (int i = 4; i < gallery.Demos.Count; i++)
        {
            var t = gallery.Demos[i].transform; Undo.RecordObject(t, "Make room for pickup icons");
            var p = t.localPosition; p.z = -.65f; t.localPosition = p;
        }
        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = row; Validate();
    }
    static Transform Marker(Transform parent, string name, float x)
    { var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(x, 0, 0); return go.transform; }
    static TMP_Text Label(Transform parent, string name, string value, Vector3 position, float width, float size, TMP_Text source)
    {
        var text = new GameObject(name).AddComponent<TextMeshPro>(); text.transform.SetParent(parent, false);
        text.transform.localPosition = position; text.transform.localRotation = Quaternion.Euler(90, 0, 0);
        text.font = source.font; text.fontSharedMaterial = source.fontSharedMaterial;
        text.fontStyle = source.fontStyle; text.fontWeight = source.fontWeight; text.text = value;
        text.alignment = TextAlignmentOptions.Center; text.color = Color.white;
        text.rectTransform.sizeDelta = new Vector2(width, .4f); text.fontSize = text.fontSizeMax = size;
        text.fontSizeMin = 1.2f; text.enableAutoSizing = true; text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
    public static T[] All<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    public static void Validate()
    {
        var demos = All<PowerUpIconDemo>();
        if (demos.Length != 4 || demos.Select(d => d.definition).Distinct().Count() != 4 ||
            demos.Any(d => !d.definition || !d.definition.pickupPrefab || !d.playerPrefab || !d.grid || !d.toasts || !d.naturalMarker || !d.hitMarker || !d.hitLabel || !d.naturalLabel))
            throw new InvalidOperationException("Expected four completely wired power-up icon pairs.");
        Debug.Log("POWER-UP ICON ROW: four canonical definitions, eight markers, player/grid/toast bindings verified.");
    }
}
#endif
