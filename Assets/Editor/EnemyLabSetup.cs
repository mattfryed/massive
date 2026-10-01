#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Massive.Demonstrations;
using Massive.Enemies;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class EnemyLabSetup
{
    [MenuItem("MASSIVE/Demonstrations/Set Up Enemy Lab")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != CarrierPrototypeSetup.ScenePath) throw new InvalidOperationException("Open Player Actions.");
        Directory.CreateDirectory("Library/EnemyLabValidation");
        if (!File.Exists("Library/EnemyLabValidation/PlayerActions-before.unity")) File.Copy(scene.path,"Library/EnemyLabValidation/PlayerActions-before.unity");
        UpgradeDysonCore();
        var existing = Object.FindObjectsByType<EnemyLab>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        if (existing) { RefreshLabels(existing); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); Selection.activeGameObject = existing.gameObject; return; }
        var root = new GameObject("Enemy Lab"); Undo.RegisterCreatedObjectUndo(root, "Create Enemy Lab");
        var lab = root.AddComponent<EnemyLab>();
        var content = new GameObject("Enemy columns"); content.transform.SetParent(root.transform, false); content.SetActive(false); lab.content = content;
        var bounds = Object.FindObjectsByType<ArenaBoundsFromVectorGrid>(FindObjectsSortMode.None).First(x => x.gameObject.scene == scene);
        bounds.RefreshNow(); var grid = bounds.Grid;
        var half = bounds.Current.halfSizeLocal;
        var definitions = new[] { DronePrototypeSetup.DefinitionPath, RangedDroneSetup.DefinitionPath, CarrierPrototypeSetup.DefinitionPath,
            ParticleBeamTurretSetup.DefinitionPath, SeekerSetup.DefinitionPath, DysonRepulsorSetup.DefinitionPath };
        var names = new[] { "Drone", "Ranged Drone", "Carrier", "Particle Beam Turret", "Seeker", "Dyson Sphere" };
        lab.columns = new EnemyLabColumn[names.Length];
        float width = half.x * 2f / names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            var lane = new GameObject((i+1).ToString("00") + " — " + names[i]); lane.transform.SetParent(content.transform, false);
            Vector3 center = grid.transform.TransformPoint(new Vector3(-half.x + width * (i+.5f),0f,0f)); center.y = 0f; lane.transform.position = center;
            var column = lane.AddComponent<EnemyLabColumn>(); lab.columns[i] = column;
            column.lab = lab; column.definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(definitions[i]);
            column.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"); column.grid = grid;
            column.columnHalfWidth = width * .5f;
            column.enemyStart = Marker(lane.transform,"Enemy start", new Vector3(0,0,half.y - 1.6f));
            column.enemyStart.rotation = Quaternion.Euler(0,180,0);
            column.playerStart = Marker(lane.transform,"Player start",new Vector3(0,0,-half.y+1.7f));
            if (i == 0 || i == 1 || i == 5)
                column.externalTelegraph = AssetDatabase.LoadAssetAtPath<GameObject>(i == 5 ? DysonRepulsorSetup.Folder + "/Dyson Spawn Telegraph.prefab" : DronePrototypeSetup.Folder + "/Drone Spawn Telegraph.prefab").GetComponent<EnemySpawnTelegraph>();
            Title(lane.transform, "Enemy name", names[i].ToUpperInvariant(), half.y+.42f, width-.2f, 2.5f);
            column.status = Title(lane.transform,"Sequence status","Ready",-half.y+.5f,width-.2f,1.6f);
            // Thin white dividers also receive lane colliders in RefreshLabels.
            if (i > 0)
            {
                var line = new GameObject("Column divider").AddComponent<LineRenderer>(); line.transform.SetParent(lane.transform,false);
                line.useWorldSpace = false; line.positionCount = 2; line.SetPosition(0,new Vector3(-width*.5f,.02f,-half.y+.9f));
                line.SetPosition(1,new Vector3(-width*.5f,.02f,half.y-.6f)); line.widthMultiplier = .008f;
                line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(DronePrototypeSetup.Folder + "/Drone Spawn Glow.mat");
                line.startColor = line.endColor = Color.white; line.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
        RefreshLabels(lab);
        lab.encounters = Object.FindObjectsByType<EnemyDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(x => x.gameObject.scene == scene).Select(x => x.gameObject).Distinct().ToArray();
        // A separately authored top turret is outside its old test Director.
        lab.encounters = lab.encounters.Concat(Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(x => x.gameObject.scene == scene && !x.GetComponentInParent<EnemyDirector>()).Select(x => x.gameObject)).Distinct().ToArray();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        Debug.Log("[Enemy Lab] Six columns installed across " + grid.size + "; actor and enemy prefabs remain live references.");
    }
    private static void RefreshLabels(EnemyLab lab)
    {
        string folder = "Assets/Scripts/Player/Demonstrations";
        var toast = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Power-ups/PU_PickupToast.prefab").GetComponentInChildren<TMP_Text>(true);
        var bounds = Object.FindObjectsByType<ArenaBoundsFromVectorGrid>(FindObjectsSortMode.None).First(x => x.gameObject.scene == lab.gameObject.scene);
        bounds.RefreshNow();
        float topSign = Vector3.Dot(bounds.Current.axisY_WS, Vector3.forward) >= 0f ? 1f : -1f;
        float topZ = bounds.Grid.transform.TransformPoint(new Vector3(0f, topSign * bounds.Current.halfSizeLocal.y, 0f)).z;
        // Arena masks sit above gameplay geometry. Keep the header on the visible side of them.
        float labelY = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .Where(x => x.gameObject.scene == lab.gameObject.scene && x.name == "mask plane TOP")
            .Select(x => x.bounds.max.y + .1f).DefaultIfEmpty(2f).Max();
        var black = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Enemy Lab Labels.mat");
        var white = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Enemy Lab Dividers.mat");
        if (!black) { black = new Material(Shader.Find("Unlit/Color")); black.color = Color.black; AssetDatabase.CreateAsset(black,folder+"/Enemy Lab Labels.mat"); }
        if (!white) { white = new Material(Shader.Find("Unlit/Color")); white.color = Color.white; AssetDatabase.CreateAsset(white,folder+"/Enemy Lab Dividers.mat"); }
        foreach (var c in lab.columns)
        {
            foreach (var line in c.GetComponentsInChildren<LineRenderer>(true))
            {
                line.sharedMaterial = white;
                if (line.name == "Column divider" && !line.GetComponent<BoxCollider>())
                {
                    var wall = line.gameObject.AddComponent<BoxCollider>();
                    wall.center = new Vector3(-c.columnHalfWidth,0,0); wall.size = new Vector3(.025f,3f,12f);
                    wall.gameObject.layer = LayerMask.NameToLayer("Obstacle");
                }
            }
            if (c.definition.id == "PARTICLE_BEAM_TURRET")
            {
                Undo.RecordObject(c.enemyStart, "Mount lab turret on top wall");
                c.enemyStart.position = new Vector3(c.enemyStart.position.x, 0f, topZ);
                var rail = c.transform.Find("Turret mounting rail");
                if (rail) Undo.DestroyObjectImmediate(rail.gameObject);
            }
            var label = c.transform.Find("Enemy name"); if (!label) continue;
            var text = label.GetComponent<TMP_Text>();
            Undo.RecordObjects(new Object[] { label, text }, "Style Enemy Lab titles");
            label.position = new Vector3(c.transform.position.x, labelY, topZ + .42f);
            text.font = toast.font; text.fontSharedMaterial = toast.fontSharedMaterial;
            text.fontStyle = toast.fontStyle; text.fontWeight = toast.fontWeight;
            text.color = toast.color; text.characterSpacing = toast.characterSpacing; text.wordSpacing = toast.wordSpacing;
            text.fontSizeMax = text.fontSize = 2.4f;
            text.rectTransform.sizeDelta = new Vector2(c.columnHalfWidth * 2f - .2f, .36f);
            text.GetComponent<Renderer>().sortingOrder = 5000;
            var plate = c.transform.Find("Name backing");
            if (!plate)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = "Name backing";
                Object.DestroyImmediate(go.GetComponent<Collider>()); go.transform.SetParent(c.transform,false);
                Undo.RegisterCreatedObjectUndo(go, "Create Enemy Lab title backing"); plate = go.transform;
            }
            Undo.RecordObject(plate, "Move Enemy Lab title backing");
            plate.position = label.position - Vector3.up * .02f;
            plate.localRotation = Quaternion.Euler(90,0,0); plate.localScale = new Vector3(c.columnHalfWidth*2f-.2f,.36f,1f);
            plate.GetComponent<Renderer>().sharedMaterial = black;
        }
    }
    private static Transform Marker(Transform parent,string name,Vector3 position)
    { var t = new GameObject(name).transform; t.SetParent(parent,false); t.localPosition = position; return t; }
    private static TMP_Text Title(Transform parent,string name,string value,float z,float width,float size)
    {
        var text = new GameObject(name).AddComponent<TextMeshPro>(); text.transform.SetParent(parent,false);
        text.transform.localPosition = new Vector3(0,.3f,z); text.transform.localRotation = Quaternion.Euler(90,0,0);
        text.font = TMP_Settings.defaultFontAsset; text.text = value; text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = new Vector2(width,.6f);
        text.enableAutoSizing = true; text.fontSizeMin = 1f; text.fontSizeMax = size; text.fontSize = size;
        text.textWrappingMode = TextWrappingModes.NoWrap; return text;
    }
    public static void UpgradeDysonCore()
    {
        var prefab = PrefabUtility.LoadPrefabContents(DysonRepulsorSetup.PrefabPath);
        try
        {
            if (prefab.GetComponentInChildren<DysonRepulsorCore>(true)) return;
            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
            var c = prefab.GetComponent<DysonSphereRepulsorController>();
            var panels = new SerializedObject(c.panels); panels.FindProperty("core").objectReferenceValue = null; panels.ApplyModifiedPropertiesWithoutUndo();
            var go = new GameObject("Black and white energy core"); go.transform.SetParent(prefab.transform,false);
            var visual = go.AddComponent<DysonRepulsorCore>(); visual.controller = c;
            string blackPath = DysonRepulsorSetup.Folder + "/Dyson Core Black.mat";
            var black = AssetDatabase.LoadAssetAtPath<Material>(blackPath);
            var white = AssetDatabase.LoadAssetAtPath<Material>(DronePrototypeSetup.Folder + "/Drone Engine.mat");
            if (!black)
            {
                black = new Material(white); black.name = "Dyson Core Black";
                black.SetColor("_LitColor",Color.black); black.SetColor("_UnlitColor",Color.black); black.SetColor("_OutlineColor",Color.white);
                black.SetFloat("_OutlineThreshold",.12f); AssetDatabase.CreateAsset(black,blackPath);
            }
            for (int i = 0; i < 2; i++)
            {
                var volume = GameObject.CreatePrimitive(PrimitiveType.Cube); volume.name = i == 0 ? "White metaballs" : "Black metaballs";
                Object.DestroyImmediate(volume.GetComponent<Collider>()); volume.transform.SetParent(go.transform,false);
                var renderer = volume.GetComponent<Renderer>(); renderer.sharedMaterial = i == 0 ? white : black;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.enabled = false;
                if (i == 0) visual.whitePlasma = renderer; else visual.blackPlasma = renderer;
            }
            PrefabUtility.SaveAsPrefabAsset(prefab,DysonRepulsorSetup.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }
}
#endif
