#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Massive.Demonstrations;

public static class HowToLayoutAuthoring
{
    public const string ScenePath = "Assets/Scenes/S-0_HOW-TO-PLAY.unity";
    public const string Output = "Library/HowToLayoutValidation";
    public static void Apply()
    {
        if (!Application.isBatchMode) throw new Exception("Author in the isolated How To project.");
        Directory.CreateDirectory(Output);
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var roots = scene.GetRootGameObjects();
        var attack = roots.Single(g => g.name == "Attack sequence");
        var defense = roots.Single(g => g.name.Trim() == "Shield sequence");
        var power = roots.Single(g => g.name == "PU panel");
        var stages = roots.Single(g => g.name == "Player demonstrations");
        var directors = stages.GetComponentsInChildren<PlayerDemoDirector>();
        var attackDemo = directors.Single(d => d.Scenario.kind == PlayerDemoKind.MovementAndCombo);
        var defenseDemo = directors.Single(d => d.Scenario.kind == PlayerDemoKind.Block);
        foreach (var d in directors) { d.Scenario.actorScale = 2f; EditorUtility.SetDirty(d.Scenario); }
        var borderTemplate = attack.transform.Find("Border").GetComponent<Shapes.Rectangle>();
        var titleTemplate = attack.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Attacking Title");
        var descriptionTemplate = attack.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Attacking Description");
        // Four independent, evenly separated panels in the combat row.
        Panel(attack, -12.1f, -7f, 3.72f, "Attacking", "Tap Attack to Thrust.\nTap again to chain\nSweep and Repulsor.", 2.4f);
        Panel(defense, 2.35f, 7.45f, 3.72f, "Defending", "Hold Shield to block.\nTime a tap to parry\nand stun the attacker.", 2.4f);
        View(attackDemo, -6.65f, 2f, 3.72f, 4.1f);
        View(defenseDemo, 7.8f, 15.6f, 3.72f, 4.1f);
        Panel(power, -12.1f, -6f, -3.82f, "Power-ups", "Attack an icon to claim it.\nUse your normal buttons\nto activate its effect.", 2.6f);
        var icons = power.transform.Find("PU icons"); if (icons) { icons.gameObject.SetActive(false); Persist(icons.gameObject); }
        const float left = -5.65f, right = 15.6f, gap = .35f;
        float unit = (right-left-gap*2)/4;
        foreach (var d in directors.Where(d => d.Scenario.kind == PlayerDemoKind.PowerUp))
        {
            if (d.Scenario.powerUp is Massive.PowerUps.TimeDilationPowerUpDefinition) View(d, left, left+unit, -3.82f, 5.3f);
            else if (d.Scenario.powerUp is Massive.PowerUps.ParticleAcceleratorPowerUpDefinition)
            {
                View(d, left+unit+gap, left+unit*3+gap, -3.82f, 5.3f);
                d.Scenario.acceleratorTargetDistance = 10.5f; EditorUtility.SetDirty(d.Scenario);
            }
            else View(d, left+unit*3+gap*2, right, -3.82f, 5.3f);
        }
        var multiplier = roots.FirstOrDefault(g => g.name == "Multiplier instructions");
        if (!multiplier)
        {
            multiplier = new GameObject("Multiplier instructions");
            UnityEngine.Object.Instantiate(borderTemplate.gameObject, multiplier.transform).name = "Border";
            UnityEngine.Object.Instantiate(titleTemplate.gameObject, multiplier.transform).name = "Multiplier Title";
            UnityEngine.Object.Instantiate(descriptionTemplate.gameObject, multiplier.transform).name = "Multiplier Description";
        }
        Panel(multiplier, -12.1f, -2.8f, -.04f, "Multipliers",
            "Scoring grows your personal multiplier to 16x.\nDying resets it.\nThrust into the Core, hold Attack to charge,\naim, then release into your team's goal.\nAmplifier goals multiply your whole team's score.", 2.4f);
        const string scenarioPath = "Assets/Scripts/Player/Demonstrations/Scenarios/Amplifier goal.asset";
        var data = AssetDatabase.LoadAssetAtPath<PlayerDemoScenario>(scenarioPath);
        bool createData = !data;
        if (createData) data = ScriptableObject.CreateInstance<PlayerDemoScenario>();
        data.actorScale = 2f;
        data.teamGoalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Levels/TeamGoals_Planar.prefab").transform.Find("TEAM 1 goal").gameObject;
        data.playerPrefab = attackDemo.Scenario.playerPrefab; data.kind = PlayerDemoKind.AmplifierGoal;
        data.amplifierCorePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Power-ups/Amplifier Core/Amplifier Core.prefab").GetComponent<Massive.Multiplier.AmplifierCoreGameplay>();
        data.scoreEconomy = AssetDatabase.LoadAssetAtPath<Massive.Scoring.ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
        data.labelFont = titleTemplate.font; data.amplifierRespawnDelay = 5f; data.readablePause = 1.5f; data.actionTimeout = 15f;
        if (createData) AssetDatabase.CreateAsset(data, scenarioPath); else EditorUtility.SetDirty(data);
        var existing = directors.FirstOrDefault(d => d.Scenario.kind == PlayerDemoKind.AmplifierGoal);
        var stage = existing ? existing.gameObject : UnityEngine.Object.Instantiate(defenseDemo.gameObject, stages.transform);
        stage.name = "Amplifier goal"; stage.transform.position = new Vector3(1500,0,1000); Persist(stage.transform);
        var demo = stage.GetComponent<PlayerDemoDirector>();
        Set(demo,"scenario", data); Set(demo,"attackButton", null); Set(demo,"shieldButton", null); Set(demo,"joystick", null);
        // Scene-internal references on the cloned stage are remapped by Unity.
        var cam = stage.GetComponentInChildren<Camera>(); cam.depth = 6; Persist(cam);
        var grid = stage.GetComponentInChildren<VectorGridGPU>(); Set(demo,"demoGrid",grid); Set(demo,"presentationCamera",cam);
        View(demo, -2.45f, 15.6f, -.04f, 4.1f);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }
    static void View(PlayerDemoDirector demo, float left, float right, float z, float height)
    {
        var view = demo.GetComponentInChildren<PlayerDemoView>();
        view.lowerLeft = new Vector3(left,0,z-1.7f); view.upperRight = new Vector3(right,0,z+1.7f);
        view.worldHeight = height; view.RefreshRect(); Persist(view); Persist(view.GetComponent<Camera>());
    }
    static void Panel(GameObject root, float left, float right, float z, string heading, string copy, float fontSize)
    {
        float x = (left+right)*.5f, width = right-left;
        var border = root.transform.Find("Border").GetComponent<Shapes.Rectangle>();
        border.transform.position = new Vector3(x,27.19f,z); border.Width=width; border.Height=3.4f;
        var texts = root.GetComponentsInChildren<TMP_Text>(true);
        var title = texts.Single(t => t.name.EndsWith("Title") || t.name == "PU");
        var body = texts.Single(t => t.name.EndsWith("Description"));
        // Match the original left-aligned type hierarchy, with explicit top-left anchors.
        Text(title, heading, new Vector3(left+.35f,27.6f,z+1.42f), new Vector2(width-.7f,.62f), 4.32f, TextAlignmentOptions.TopLeft);
        bool hasControls = root.transform.Find("Controls");
        Text(body, copy, new Vector3(left+.35f,27.6f,z+.48f), new Vector2(width-.7f,hasControls ? 1.45f : 1.9f), hasControls ? 2.55f : fontSize, TextAlignmentOptions.TopLeft);
        if (hasControls)
        {
            var controls=root.transform.Find("Controls");
            controls.localScale=Vector3.one*.55f;
            controls.position=new Vector3(x+.42f,26.2f,z-1.31f); Persist(controls);
        }
        Persist(border); Persist(border.transform);
    }
    static void Text(TMP_Text text, string content, Vector3 position, Vector2 size, float font, TextAlignmentOptions alignment)
    {
        text.text=content; text.fontSize=font; text.enableAutoSizing=false; text.alignment=alignment;
        text.rectTransform.pivot=new Vector2(0,1);
        text.rectTransform.anchorMin=text.rectTransform.anchorMax=new Vector2(.5f,.5f);
        text.margin=Vector4.zero; text.textWrappingMode=TextWrappingModes.Normal;
        text.overflowMode=TextOverflowModes.Overflow;
        text.rectTransform.sizeDelta=size;
        // These world-space labels have ordinary Transform parents. Unity persists
        // RectTransform XY through anchoredPosition, not Transform.position.
        text.rectTransform.anchoredPosition3D=text.transform.parent ? text.transform.parent.InverseTransformPoint(position) : position;
        Persist(text); Persist(text.rectTransform);
    }
    // Dirty flags alone do not serialize direct Transform edits on prefab instances.
    static void Persist(UnityEngine.Object value)
    {
        if (!value) return;
        EditorUtility.SetDirty(value);
        if (PrefabUtility.IsPartOfPrefabInstance(value))
            PrefabUtility.RecordPrefabInstancePropertyModifications(value);
    }
    static void Set(UnityEngine.Object obj, string field, UnityEngine.Object value)
    { var so=new SerializedObject(obj); so.FindProperty(field).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); Persist(obj); }
    public static void InspectBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Use the isolated authoring project.");
        Directory.CreateDirectory(Output);
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();
        foreach (var root in scene.GetRootGameObjects())
        {
            sb.AppendLine("ROOT " + root.name + " active=" + root.activeSelf);
            if (!(root.name.Contains("sequence") || root.name == "PU panel" || root.name == "Player demonstrations" || root.name == "Main Camera")) continue;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                sb.AppendLine(MassiveDemoMigration.PathOf(t) + " active=" + t.gameObject.activeSelf + " pos=" + t.position.ToString("F3") + " rot=" + t.eulerAngles + " scale=" + t.lossyScale);
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c is TMP_Text text) sb.AppendLine("TEXT " + text.text + " size=" + text.fontSize + " rect=" + text.rectTransform.rect + " align=" + text.alignment + " font=" + AssetDatabase.GetAssetPath(text.font));
                    else if (c is Shapes.Rectangle rect) sb.AppendLine("RECT " + EditorJsonUtility.ToJson(rect));
                    else if (c is Camera cam) sb.AppendLine("CAM " + EditorJsonUtility.ToJson(cam));
                }
            }
        }
        File.WriteAllText(Output + "/before-layout.txt", sb.ToString());
    }
}
#endif