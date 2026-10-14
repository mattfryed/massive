#if UNITY_EDITOR
using System;
using System.Linq;
using Massive.Demonstrations;
using Massive.Enemies;
using Shapes;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>One-time migration of the alternate scene to its authored boxed layout.</summary>
public static class HowToPaneBoxedLayout
{
    static readonly string[] Names = { "01 Attacking", "02 Defending", "03 Multipliers and goals", "04 Time Dilation", "05 Particle Accelerator", "06 Decoherence" };
    static readonly string[] Titles = { "COMBAT", "DEFENDING", "MULTIPLIERS & GOALS", "TIME DILATION", "PARTICLE ACCELERATOR", "DECOHERENCE" };
    static readonly string[] Copy = {
        "Move and aim with the joystick.\nTap Attack to Thrust. Tap again to chain a Sweep and Repulsor.\nHold Attack to charge a longer, stronger Thrust.",
        "Hold Shield to block incoming attacks.\nTime a tap to parry and stun the attacker.\nAfter a parry, tap Attack to counter with a Thrust.",
        "Scoring builds your multiplier up to 16x. Dying resets it.\nThrust into the Amplifier Core. Hold Attack, aim, then release.\nGoals permanently multiply your team's score.",
        "Attack a power-up icon to claim it.\nTime Dilation boosts your movement speed.\nKeep moving to make space and line up your next attack.",
        "Attack a power-up icon to claim it.\nHold Attack to charge. Aim, then release to fire\na powerful energy beam.",
        "Attack a power-up icon to claim it.\nUse Shield to phase an attacker through you\nand stun them."
    };
    static readonly string[] DemoTitles = { "", "BLOCK / PARRY", "CLAIM / CHARGE / SCORE", "CLAIM / MOVE", "CLAIM / CHARGE / FIRE", "CLAIM / PHASE" };

    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != HowToPaneAuthoring.ScenePath || scene.isDirty)
            throw new InvalidOperationException("Save and open the alternate How To scene in Edit Mode first.");
        var carousel = Object.FindFirstObjectByType<HowToPlayCarousel>();
        var instances = carousel.GetComponentsInChildren<HowToPlayPane>(true);
        if (instances.Any(p => p.transform.Find("Demo area")))
            throw new InvalidOperationException("Boxed layout is already applied. Edit its prefab contents directly.");
        var controlTemplate = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HowToPaneAuthoring.PrefabFolder + "/02 Defending.prefab").transform.Find("Controls").gameObject);
        controlTemplate.SetActive(false);
        try
        {
        for (int i = 0; i < Names.Length; i++)
        {
            string path = HowToPaneAuthoring.PrefabFolder + "/" + Names[i] + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                LayoutHeader(root, i);
                var title = root.transform.Find("Title").GetComponent<TMP_Text>();
                var demo = root.GetComponentInChildren<PlayerDemoDirector>(true);
                var oldControls = root.transform.Find("Controls");
                if (oldControls) Object.DestroyImmediate(oldControls.gameObject);
                var area = new GameObject("Demo area"); area.transform.SetParent(root.transform, false);
                demo.transform.SetParent(area.transform, true);
                if (i == 0)
                {
                    SetFloat(root.GetComponent<HowToPlayPane>(), "durationOverride", 16f);
                    var kinds = new[] { PlayerDemoKind.DroneThrust, PlayerDemoKind.DroneCombo, PlayerDemoKind.DroneChargedThrust };
                    var headings = new[] { "THRUST", "FULL COMBO", "CHARGED THRUST" };
                    var actions = new[] { "TAP ATTACK", "TAP / TAP / TAP", "HOLD / RELEASE ATTACK" };
                    for (int column = 0; column < 3; column++)
                    {
                        float center = -8f + column * 8f;
                        var director = column == 0 ? demo : Object.Instantiate(demo, area.transform);
                        director.name = headings[column] + " demonstration";
                        director.transform.position = new Vector3(1000f + column * 100f, 0f, 1000f);
                        var scenario = Object.Instantiate(demo.Scenario);
                        scenario.name = "How To " + headings[column]; scenario.kind = kinds[column]; scenario.actorScale = 1f;
                        scenario.droneDefinition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Enemy System/Enemy Types/Melee/Drone/ED_Drone.asset");
                        scenario.readablePause = 1.4f;
                        string scenarioPath = HowToPaneAuthoring.PrefabFolder + "/" + scenario.name + ".asset";
                        AssetDatabase.CreateAsset(scenario, scenarioPath);
                        Set(director, "scenario", scenario);
                        View(director, center - 3.7f, center + 3.7f, -3.35f, 2.35f, 0f);
                        Label(title, area.transform, headings[column], center, 3.85f, 7.4f, 3.1f);
                        Label(title, area.transform, actions[column], center, 2.95f, 7.4f, 2.15f);
                        Controls(controlTemplate, area.transform, director, center + .45f, -4.9f);
                    }
                    foreach (float x in new[] { -4f, 4f })
                    {
                        var go = new GameObject("Column divider"); go.transform.SetParent(area.transform, false);
                        var line = go.AddComponent<Line>(); line.Start = new Vector3(x,27.3f,4.15f); line.End = new Vector3(x,27.3f,-5.95f);
                        line.Thickness = .018f; line.Color = new Color(1,1,1,.3f);
                    }
                }
                else
                {
                    UseGameplayScaleScenario(demo, Names[i]);
                    Label(title, area.transform, DemoTitles[i], 0f, 3.85f, 21f, 3.1f);
                    View(demo, -10.9f, 10.9f, -3.6f, 2.85f, i == 1 ? 5.5f : i == 2 ? 6f : 7.2f);
                    Controls(controlTemplate, area.transform, demo, .45f, -5.15f);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        }
        finally { Object.DestroyImmediate(controlTemplate); }
        // Keep the user's scene-owned boxes, prompt and page position. Resolve old text overrides only.
        Undo.RegisterFullObjectHierarchyUndo(carousel.gameObject, "Apply boxed pane layout");
        for (int i = 0; i < instances.Length; i++) LayoutHeader(instances[i].gameObject, i);
        var top = scene.GetRootGameObjects().Single(g => g.name == "TOP BAR");
        foreach (var header in top.GetComponentsInChildren<TMP_Text>(true))
            if (header.name != "Page number") Animate(header, false);
        SetFloat(carousel, "barWidth", 24f);
        foreach (var pane in instances) pane.Bind(Camera.main);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    static void LayoutHeader(GameObject root, int index)
    {
        var title = root.transform.Find("Title").GetComponent<TMP_Text>();
        var description = root.GetComponentsInChildren<TMP_Text>(true).First(t => t.name.StartsWith("Instructions"));
        Layout(title, Titles[index], new Vector3(-11.6f,27.6f,5.8f), new Vector2(8.1f,1.65f), 6f);
        Layout(description, Copy[index], new Vector3(-2.51f,27.6f,7.1f), new Vector2(14.05f,2.15f), 2.6f);
        var mechanic = root.transform.Find("Mechanic"); if (mechanic) { mechanic.gameObject.SetActive(false); Persist(mechanic.gameObject); }
        Animate(title, false); Animate(description, true);
    }
    static void Layout(TMP_Text text, string copy, Vector3 position, Vector2 size, float font)
    {
        text.text = copy; text.fontSize = font; text.enableAutoSizing = false; text.margin = Vector4.zero;
        text.alignment = TextAlignmentOptions.TopLeft; text.color = Color.white; text.maxVisibleCharacters = int.MaxValue;
        text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
        text.rectTransform.sizeDelta = size; text.transform.position = position;
        Persist(text); Persist(text.rectTransform);
    }
    static void Animate(TMP_Text text, bool typewriter)
    {
        var transition = text.GetComponent<TMPTextTransition>();
        if (!transition) transition = text.gameObject.AddComponent<TMPTextTransition>();
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/Instructions/InstructionsPanel-NOVA.prefab")
            .GetComponentsInChildren<TMPTextTransition>(true).Single(t => t.name == (typewriter ? "Instructions body" : "Anomaly Type "));
        EditorUtility.CopySerialized(reference, transition);
        var so = new SerializedObject(transition);
        // Rebind the copied transition to this text; never retain the reference prefab's target.
        var targets = so.FindProperty("targets"); targets.arraySize = 1;
        targets.GetArrayElementAtIndex(0).objectReferenceValue = text;
        so.ApplyModifiedPropertiesWithoutUndo(); Persist(transition);
    }
    static void Label(TMP_Text template, Transform parent, string content, float x, float z, float width, float font)
    {
        var text = Object.Instantiate(template, parent); text.name = content;
        foreach (var transition in text.GetComponents<TMPTextTransition>()) Object.DestroyImmediate(transition);
        Layout(text, content, new Vector3(x-width*.5f,27.6f,z), new Vector2(width,.65f), font);
        text.alignment = TextAlignmentOptions.Top; text.characterSpacing = 0f;
    }
    static void View(PlayerDemoDirector demo, float left, float right, float bottom, float top, float height)
    {
        var view = demo.GetComponentInChildren<PlayerDemoView>(true);
        view.lowerLeft = new Vector3(left,0,bottom); view.upperRight = new Vector3(right,0,top);
        view.worldHeight = height; view.matchScreenWorldScale = true; view.screenCamera = null; view.caption = ""; view.captionPrefab = null;
    }
    static void UseGameplayScaleScenario(PlayerDemoDirector demo, string name)
    {
        var scenario = demo.Scenario;
        if (!AssetDatabase.GetAssetPath(scenario).StartsWith(HowToPaneAuthoring.PrefabFolder + "/", StringComparison.Ordinal))
        {
            string path = HowToPaneAuthoring.PrefabFolder + "/How To " + name + ".asset";
            var local = AssetDatabase.LoadAssetAtPath<PlayerDemoScenario>(path);
            if (!local) { local = Object.Instantiate(scenario); AssetDatabase.CreateAsset(local, path); }
            scenario = local;
        }
        scenario.actorScale = 1f; EditorUtility.SetDirty(scenario); Set(demo, "scenario", scenario);
    }

    public static void MatchExistingEffectsAndGameplayScale()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != HowToPaneAuthoring.ScenePath || scene.isDirty)
            throw new InvalidOperationException("Save and open the alternate How To scene in Edit Mode first.");
        foreach (var name in Names)
        {
            string path = HowToPaneAuthoring.PrefabFolder + "/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    if (text.name == "Title" || text.name.StartsWith("Instructions")) Animate(text, text.name != "Title");
                foreach (var demo in root.GetComponentsInChildren<PlayerDemoDirector>(true)) UseGameplayScaleScenario(demo, name);
                foreach (var view in root.GetComponentsInChildren<PlayerDemoView>(true)) view.matchScreenWorldScale = true;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var carousel = Object.FindFirstObjectByType<HowToPlayCarousel>();
        foreach (var pane in carousel.GetComponentsInChildren<HowToPlayPane>(true))
        {
            foreach (var text in pane.GetComponentsInChildren<TMP_Text>(true))
                if (text.name == "Title" || text.name.StartsWith("Instructions")) Animate(text, text.name != "Title");
            pane.Bind(Camera.main);
        }
        var top = scene.GetRootGameObjects().Single(g => g.name == "TOP BAR");
        foreach (var text in top.GetComponentsInChildren<TMP_Text>(true))
            if (text.name != "Page number") Animate(text, false);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }
    static void Controls(GameObject template, Transform parent, PlayerDemoDirector demo, float x, float z)
    {
        var go = Object.Instantiate(template, parent); go.name = demo.name + " controls"; go.SetActive(true);
        go.transform.position = new Vector3(x,26.2f,z); go.transform.localScale = Vector3.one * .85f;
        Set(demo,"attackButton",go.transform.Find("Attack button/Attack button center")?.gameObject);
        Set(demo,"shieldButton",go.transform.Find("Shield button/Shield button center")?.gameObject);
        Set(demo,"joystick",go.transform.Find("Joystick/Stick"));
    }
    static void Persist(Object obj) { EditorUtility.SetDirty(obj); if(PrefabUtility.IsPartOfPrefabInstance(obj))PrefabUtility.RecordPrefabInstancePropertyModifications(obj); }
    static void Set(Object obj,string field,Object value) { var so=new SerializedObject(obj); so.FindProperty(field).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); Persist(obj); }
    static void SetFloat(Object obj,string field,float value) { var so=new SerializedObject(obj); so.FindProperty(field).floatValue=value; so.ApplyModifiedPropertiesWithoutUndo(); Persist(obj); }
}
#endif
