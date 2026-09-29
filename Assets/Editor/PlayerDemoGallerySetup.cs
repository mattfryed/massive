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

public static class PlayerDemoGallerySetup
{
    const string Folder = "Assets/Scripts/Player/Demonstrations/Action Gallery";
    static readonly string[] Titles = {
        "01  ATTACK / TAP PARRY", "02  ATTACK / HELD SHIELD", "03  THRUST / SWEEP / REPULSOR", "04  ATTACK / DECOHERENCE",
        "05  ACCELERATOR / TAP PARRY", "06  ACCELERATOR / HELD SHIELD", "07  ACCELERATOR / DECOHERENCE" };
    static readonly CombatDemoDefense[] Defenses = { CombatDemoDefense.TapParry, CombatDemoDefense.HeldBlock,
        CombatDemoDefense.None, CombatDemoDefense.DecoherenceParry, CombatDemoDefense.TapParry,
        CombatDemoDefense.HeldBlock, CombatDemoDefense.DecoherenceParry };

    [MenuItem("MASSIVE/Demonstrations/Set Up Simultaneous Action Demos")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Use Edit Mode.");
        var scene = SceneManager.GetSceneByPath(PlayerActionTestArenaSetup.ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(PlayerActionTestArenaSetup.ScenePath);
        var lab = All<PlayerActionTestArena>(scene).Single();
        if (lab.GetComponent<PlayerDemoGallery>()) { Validate(); return; }
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Scripts/Player/Demonstrations", "Action Gallery");
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab");
        var accelerator = AssetDatabase.LoadAssetAtPath<ParticleAcceleratorPowerUpDefinition>("Assets/Power-ups/Particle Accelerator/PU_ParticleAccelerator.asset");
        var decoherence = AssetDatabase.LoadAssetAtPath<DecoherencePowerUpDefinition>("Assets/Power-ups/Decoherence/PU_Decoherence.asset");
        if (!playerPrefab || !accelerator || !decoherence) throw new Exception("Missing canonical actor or power-up definition.");
        var grid = All<VectorGridGPU>(scene).First(g => g.gameObject.activeInHierarchy);
        var gallery = lab.gameObject.AddComponent<PlayerDemoGallery>();
        var content = new GameObject("Simultaneous Player Demos"); content.transform.SetParent(lab.transform, false); content.SetActive(false);
        var plate = new Material(Shader.Find("Unlit/Color")) { color = Color.black, name = "Gallery label backing" };
        AssetDatabase.CreateAsset(plate, Folder + "/Label backing.mat");
        var demos = new PlayerDemoDirector[7]; var labels = new TMP_Text[7];
        for (int i = 0; i < 7; i++)
        {
            float x = i < 4 ? -10.2f + 6.8f * i : -9f + 9f * (i - 4);
            float z = i < 4 ? 2.6f : -2.7f;
            float width = i < 4 ? 6.25f : 8.35f;
            var scenario = ScriptableObject.CreateInstance<PlayerDemoScenario>();
            scenario.name = Titles[i]; scenario.playerPrefab = playerPrefab;
            scenario.kind = i == 2 ? PlayerDemoKind.SoloCombo : PlayerDemoKind.CombatPair; scenario.defense = Defenses[i];
            scenario.powerUp = i >= 4 ? accelerator : null;
            scenario.defenderPowerUp = Defenses[i] == CombatDemoDefense.DecoherenceParry ? decoherence : null;
            scenario.acceleratorChargeFraction = .3f; scenario.acceleratorTargetDistance = 3f;
            scenario.readablePause = 1f; scenario.actionTimeout = 15f;
            AssetDatabase.CreateAsset(scenario, Folder + "/Demo " + (i + 1) + ".asset");
            var lane = new GameObject(Titles[i]); lane.transform.SetParent(content.transform, false);
            lane.transform.localPosition = new Vector3(x, 0, z);
            demos[i] = lane.AddComponent<PlayerDemoDirector>();
            Set(demos[i], "scenario", scenario); Set(demos[i], "demoGrid", grid);
            Label(lane.transform, "Title", Titles[i], new Vector3(0, .25f, 1.8f), width, 2.85f, plate);
            labels[i] = Label(lane.transform, "Status", "Ready", new Vector3(0, .25f, 1.34f), width, 2.15f, plate);
        }
        var serialized = new SerializedObject(gallery);
        serialized.FindProperty("content").objectReferenceValue = content;
        SetArray(serialized.FindProperty("demos"), demos);
        SetArray(serialized.FindProperty("statusLabels"), labels);
        var encounters = All<Transform>(scene).Where(t => t.name == "Encounters").Select(t => t.gameObject).ToArray();
        SetArray(serialized.FindProperty("encounters"), encounters);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Set(lab, "gallery", gallery);
        var ls = new SerializedObject(lab); ls.FindProperty("mode").enumValueIndex = (int)PlayerActionTestArena.TestMode.AllDemos;
        ls.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Validate();
    }

    static TMP_Text Label(Transform parent, string name, string value, Vector3 position, float width, float fontSize, Material plate)
    {
        var backing = GameObject.CreatePrimitive(PrimitiveType.Quad); backing.name = name + " backing";
        Object.DestroyImmediate(backing.GetComponent<Collider>());
        backing.transform.SetParent(parent, false); backing.transform.localPosition = position - Vector3.up * .02f;
        backing.transform.localRotation = Quaternion.Euler(90f, 0, 0); backing.transform.localScale = new Vector3(width, .44f, 1f);
        backing.GetComponent<MeshRenderer>().sharedMaterial = plate;
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        var text = go.AddComponent<TextMeshPro>(); text.font = TMP_Settings.defaultFontAsset;
        text.text = value; text.alignment = TextAlignmentOptions.Center; text.color = Color.white;
        text.rectTransform.sizeDelta = new Vector2(width, .44f);
        text.enableAutoSizing = true; text.fontSize = fontSize; text.fontSizeMax = fontSize; text.fontSizeMin = 1.4f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
    static void Set(Object target, string field, Object value)
    { var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static void SetArray<T>(SerializedProperty property, T[] values) where T : Object
    { property.arraySize = values.Length; for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }
    static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

    public static void Validate()
    {
        var scene = SceneManager.GetSceneByPath(PlayerActionTestArenaSetup.ScenePath);
        var gallery = All<PlayerDemoGallery>(scene).Single();
        if (gallery.Demos.Count != 7 || All<PlayerDemoDirector>(scene).Length != 7) throw new Exception("Expected seven demonstration lanes.");
        for (int i = 0; i < 7; i++)
        {
            var demo = gallery.Demos[i]; var s = demo.Scenario;
            if (!s || !s.playerPrefab || s.kind != (i == 2 ? PlayerDemoKind.SoloCombo : PlayerDemoKind.CombatPair) || s.defense != Defenses[i] || !demo.Grid)
                throw new Exception("Invalid gallery lane " + (i + 1));
            if (i >= 4 && !(s.powerUp is ParticleAcceleratorPowerUpDefinition)) throw new Exception("Missing accelerator.");
            if (s.defense == CombatDemoDefense.DecoherenceParry && !s.defenderPowerUp) throw new Exception("Missing Decoherence.");
        }
        Debug.Log("PLAYER DEMO GALLERY: seven authored lane bindings passed.");
    }
}
#endif
