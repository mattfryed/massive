#if UNITY_EDITOR
using System;
using System.Linq;
using Massive.Demonstrations;
using Shapes;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>One-time authoring for the separate pane experiment. Never edits the source scene.</summary>
public static class HowToPaneAuthoring
{
    public const string ScenePath = "Assets/Scenes/S-0_HOW-TO-PLAY-PANES.unity";
    public const string PrefabFolder = "Assets/Prefabs/UI/How To Play Panes";

    public static void CreatePanes()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != ScenePath)
            throw new InvalidOperationException("Open the duplicated How To Play Panes scene in Edit Mode first.");
        if (Object.FindFirstObjectByType<HowToPlayCarousel>())
            throw new InvalidOperationException("Panes already exist. Edit their prefabs directly.");

        var roots = scene.GetRootGameObjects();
        var stage = roots.Single(g=>g.name=="Stage Title");
        var attack = roots.Single(g => g.name == "Attack sequence");
        var defense = roots.Single(g => g.name.Trim() == "Shield sequence");
        var demos = roots.Single(g => g.name == "Player demonstrations");
        var directors = demos.GetComponentsInChildren<PlayerDemoDirector>(true);
        var titleTemplate = attack.transform.Find("Attacking Title").GetComponent<TMP_Text>();
        var bodyTemplate = attack.transform.Find("Attacking Description").GetComponent<TMP_Text>();
        var camera = roots.SelectMany(g => g.GetComponentsInChildren<Camera>()).Single(c => c.CompareTag("MainCamera"));
        EnsureFolder(PrefabFolder);

        var carouselRoot = new GameObject("How To Play Carousel");
        var panes = new GameObject("Panes - add and reorder prefab instances here");
        panes.transform.SetParent(carouselRoot.transform, false);
        var carousel = carouselRoot.AddComponent<HowToPlayCarousel>();
        var ordered = new[] {
            directors.Single(d => d.Scenario.kind == PlayerDemoKind.MovementAndCombo),
            directors.Single(d => d.Scenario.kind == PlayerDemoKind.Block),
            directors.Single(d => d.Scenario.kind == PlayerDemoKind.AmplifierGoal),
            directors.Single(d => d.Scenario.powerUp is Massive.PowerUps.TimeDilationPowerUpDefinition),
            directors.Single(d => d.Scenario.powerUp is Massive.PowerUps.ParticleAcceleratorPowerUpDefinition),
            directors.Single(d => d.Scenario.powerUp is Massive.PowerUps.DecoherencePowerUpDefinition)
        };
        string[] names = { "01 Attacking", "02 Defending", "03 Multipliers and goals", "04 Time Dilation", "05 Particle Accelerator", "06 Decoherence" };
        string[] titles = { "ATTACKING", "DEFENDING", "MULTIPLIERS\n& GOALS", "TIME DILATION", "PARTICLE\nACCELERATOR", "DECOHERENCE" };
        string[] labels = { "MOVE / AIM / ATTACK", "BLOCK / PARRY", "SCORE / CHARGE / RELEASE", "POWER-UP / MOVEMENT", "POWER-UP / ATTACK", "POWER-UP / DEFENSE" };
        string[] copy = {
            "Move and aim with the joystick.\n\nTap Attack to Thrust.\nTap again to chain a Sweep,\nthen a Repulsor.",
            "Hold Shield to block.\n\nTime a tap to parry and\nstun the attacker.\n\nAfter a parry, tap Attack\nto counter with a Thrust.",
            "Scoring builds your multiplier\nup to 16x. Dying resets it.\n\nThrust into the Amplifier Core.\nHold Attack to charge, aim,\nthen release into your goal.\n\nGoals permanently multiply\nyour team's score.",
            "Attack a power-up icon\nto claim it.\n\nTime Dilation boosts your\nmovement speed.\n\nKeep moving to make space\nand line up your next attack.",
            "Attack a power-up icon\nto claim it.\n\nHold Attack to charge.\nAim, then release to fire\na powerful energy beam.",
            "Attack a power-up icon\nto claim it.\n\nUse Shield to phase an\nattacker through you\nand stun them."
        };
        for (int i = 0; i < ordered.Length; i++)
        {
            var root = new GameObject(names[i]);
            root.transform.SetParent(panes.transform, false);
            var pane = root.AddComponent<HowToPlayPane>();
            if (i == 2) SetFloat(pane, "durationOverride", 16f);
            Text(titleTemplate, root.transform, "Mechanic", labels[i], new Vector3(-11.8f, 27.6f, 5f), new Vector2(11, .6f), 2.35f, new Color(1,1,1,.55f));
            bool tallTitle = titles[i].Contains("\n");
            Text(titleTemplate, root.transform, "Title", titles[i], new Vector3(-11.8f,27.6f,4.05f), new Vector2(11,2.2f), 6f, Color.white);
            Text(bodyTemplate, root.transform, "Instructions", copy[i], new Vector3(-11.8f,27.6f,tallTitle ? 1.6f : 2.3f), new Vector2(10.5f,6.1f), i == 2 ? 3.05f : 3.35f, new Color(1,1,1,.85f));

            var demo = Object.Instantiate(ordered[i], root.transform);
            demo.name = "Live demonstration";
            demo.transform.position = ordered[i].transform.position;
            // Remove scene-owned cues; pane-local controls are wired below.
            Set(demo, "attackButton", null); Set(demo, "shieldButton", null); Set(demo, "joystick", null);
            var view = demo.GetComponentInChildren<PlayerDemoView>(true);
            view.screenCamera = null;
            view.lowerLeft = new Vector3(.15f,0,-3.65f);
            view.upperRight = new Vector3(12.5f,0,4.75f);
            view.worldHeight = 0f; // Frame the full encounter, including long beams and goals.
            view.caption = ""; view.captionPrefab = null;
            demo.PresentationCamera.depth = 2;

            if (i < 2)
            {
                var controls = Object.Instantiate((i == 0 ? attack : defense).transform.Find("Controls").gameObject, root.transform);
                controls.name = "Controls"; controls.SetActive(true);
                controls.transform.position = new Vector3(-7f,26.2f,-3.85f);
                controls.transform.localScale = Vector3.one * 1.1f;
                var attackCue = controls.transform.Find("Attack button/Attack button center");
                var shieldCue = controls.transform.Find("Shield button/Shield button center");
                Set(demo,"attackButton",attackCue ? attackCue.gameObject : null);
                Set(demo,"shieldButton",shieldCue ? shieldCue.gameObject : null);
                Set(demo,"joystick",controls.transform.Find("Joystick/Stick"));
            }
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabFolder + "/" + names[i] + ".prefab", InteractionMode.AutomatedAction);
            // All bindings remain scene overrides; no prefab has a scene reference.
            pane.Bind(camera);
            foreach (var v in root.GetComponentsInChildren<PlayerDemoView>(true)) Persist(v);
            root.SetActive(i == 0); Persist(root);
        }

        foreach (var root in new[] { attack, defense, demos, roots.Single(g=>g.name=="PU panel"), roots.Single(g=>g.name=="Multiplier instructions") })
            Object.DestroyImmediate(root);

        var header = stage.transform.Find("How to play").GetComponent<TMP_Text>();
        RemoveTransitions(header.gameObject);
        Layout(header,"HOW TO PLAY",new Vector3(-12f,27.6f,7.25f),new Vector2(17,1),4.8f,Color.white);
        var oldRule = stage.transform.Find("Rectangle"); if (oldRule) oldRule.gameObject.SetActive(false);
        var prompt = stage.transform.Find("PRESS ANY BUTTON").GetComponent<TMP_Text>();
        prompt.text = "PRESS ANY BUTTON TO BEGIN"; Persist(prompt);
        var page = Text(header,carouselRoot.transform,"Page number","01 / 06",new Vector3(9.1f,27.6f,7.25f),new Vector2(4,1),3.5f,new Color(1,1,1,.6f));
        Line(carouselRoot.transform,"Header rule",new Vector3(-12,27.4f,6.1f),new Vector3(12.5f,27.4f,6.1f),.016f,new Color(1,1,1,.25f));
        var navigation = new GameObject("Joystick navigation"); navigation.transform.SetParent(carouselRoot.transform,false);
        foreach (int direction in new[] {-1,1})
        {
            float x=14.5f*direction;
            Line(navigation.transform,direction<0?"Left chevron upper":"Right chevron upper",new Vector3(x-direction*.45f,27.6f,.8f),new Vector3(x,27.6f,.1f),.065f,Color.white);
            Line(navigation.transform,direction<0?"Left chevron lower":"Right chevron lower",new Vector3(x,27.6f,.1f),new Vector3(x-direction*.45f,27.6f,-.6f),.065f,Color.white);
        }
        var hint = Text(header,navigation.transform,"Navigation hint","JOYSTICK LEFT / RIGHT TO EXPLORE",new Vector3(-9,27.6f,-5.7f),new Vector2(18,.6f),2.4f,new Color(1,1,1,.55f));
        hint.alignment = TextAlignmentOptions.Top;
        var track = Bar(carouselRoot.transform,"Timer track",.028f,new Color(1,1,1,.25f));
        var fill = Bar(carouselRoot.transform,"White timer fill",.08f,Color.white);
        fill.Width=.001f; fill.transform.position=new Vector3(0,27.5f,-8.25f);
        Set(carousel,"paneRoot",panes.transform); Set(carousel,"screenCamera",camera);
        Set(carousel,"progressFill",fill); Set(carousel,"pageNumber",page); Set(carousel,"navigationRoot",navigation);

        // The original scene normally inherits this from Attract. This experiment also plays directly.
        if (!Object.FindFirstObjectByType<Rewired.InputManager>())
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rewired Input Manager.prefab"), scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    static TMP_Text Text(TMP_Text source, Transform parent, string name, string content, Vector3 position, Vector2 size, float fontSize, Color color)
    {
        var text=Object.Instantiate(source,parent); text.name=name;
        RemoveTransitions(text.gameObject); Layout(text,content,position,size,fontSize,color);
        return text;
    }
    static void Layout(TMP_Text text,string content,Vector3 position,Vector2 size,float fontSize,Color color)
    {
        text.text=content; text.fontSize=fontSize; text.enableAutoSizing=false; text.color=color;
        text.alignment=TextAlignmentOptions.TopLeft; text.textWrappingMode=TextWrappingModes.Normal;
        text.overflowMode=TextOverflowModes.Overflow; text.margin=Vector4.zero;
        text.rectTransform.pivot=new Vector2(0,1); text.rectTransform.anchorMin=text.rectTransform.anchorMax=new Vector2(.5f,.5f);
        text.transform.rotation=Quaternion.Euler(90,0,0); text.transform.localScale=Vector3.one;
        text.rectTransform.sizeDelta=size;
        text.rectTransform.anchoredPosition3D=text.transform.parent.InverseTransformPoint(position);
        Persist(text); Persist(text.rectTransform);
    }
    static void RemoveTransitions(GameObject go) { foreach(var t in go.GetComponents<TMPTextTransition>()) Object.DestroyImmediate(t); }
    static void Line(Transform parent,string name,Vector3 start,Vector3 end,float thickness,Color color)
    { var go=new GameObject(name); go.transform.SetParent(parent,false); var l=go.AddComponent<Line>(); l.Start=start; l.End=end; l.Thickness=thickness; l.Color=color; }
    static Rectangle Bar(Transform parent,string name,float height,Color color)
    {
        var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.position=new Vector3(0,27.4f,-8.25f); go.transform.rotation=Quaternion.Euler(90,0,0);
        var r=go.AddComponent<Rectangle>(); r.Pivot=RectPivot.Center; r.Type=Rectangle.RectangleType.HardSolid; r.Width=28; r.Height=height; r.Color=color; return r;
    }
    static void EnsureFolder(string path)
    { if(AssetDatabase.IsValidFolder(path))return; int slash=path.LastIndexOf('/'); EnsureFolder(path.Substring(0,slash)); AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1)); }
    static void Persist(Object value) { EditorUtility.SetDirty(value); if(PrefabUtility.IsPartOfPrefabInstance(value))PrefabUtility.RecordPrefabInstancePropertyModifications(value); }
    static void Set(Object obj,string field,Object value) {var so=new SerializedObject(obj);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();Persist(obj);}
    static void SetFloat(Object obj,string field,float value) {var so=new SerializedObject(obj);so.FindProperty(field).floatValue=value;so.ApplyModifiedPropertiesWithoutUndo();Persist(obj);}
}
#endif
