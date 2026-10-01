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
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnemyEncounterLabSetup
{
    public const string Folder = "Assets/Enemy System/Encounters/Enemy Lab";
    public const string TimelinePath = Folder + "/Enemy Lab Timeline.asset";
    static EnemyEncounterLabSetup()
    {
        EditorApplication.delayCall += () =>
        {
            const string request = "Library/EnemyEncounterValidation/install.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Install(); File.WriteAllText("Library/EnemyEncounterValidation/install.txt", "INSTALLED"); }
            catch (Exception e) { File.WriteAllText("Library/EnemyEncounterValidation/install.txt", e.ToString()); Debug.LogException(e); }
        };
    }
    [MenuItem("MASSIVE/Demonstrations/Set Up Encounter Timeline Lab")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != CarrierPrototypeSetup.ScenePath) throw new InvalidOperationException("Open Player Actions.");
        var lab = Object.FindObjectsByType<EnemyLab>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(x => x.gameObject.scene == scene);
        if (lab.timelinePreview) { Selection.activeGameObject = lab.gameObject; return; }
        Directory.CreateDirectory("Library/EnemyEncounterValidation");
        File.Copy(scene.path, "Library/EnemyEncounterValidation/PlayerActions-before.unity", true);
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var defs = new[] { DronePrototypeSetup.DefinitionPath, RangedDroneSetup.DefinitionPath, SeekerSetup.DefinitionPath,
            DysonRepulsorSetup.DefinitionPath, ParticleBeamTurretSetup.DefinitionPath, CarrierPrototypeSetup.DefinitionPath }
            .Select(AssetDatabase.LoadAssetAtPath<EnemyDefinition>).ToArray();
        var warningPaths = new[] { DronePrototypeSetup.Folder + "/Drone Spawn Telegraph.prefab", DronePrototypeSetup.Folder + "/Drone Spawn Telegraph.prefab",
            SeekerSetup.WarningPath, DysonRepulsorSetup.Folder + "/Dyson Spawn Telegraph.prefab", ParticleBeamTurretSetup.WarningPath, CarrierPrototypeSetup.Folder + "/Carrier Spawn Telegraph.prefab" };
        EnemyFormation.Slot Slot(int type, float x, float z, Vector2 facing, float delay = 0, string socket = null, float entry = 0) =>
            new() { enemy = defs[type], telegraph = AssetDatabase.LoadAssetAtPath<GameObject>(warningPaths[type]).GetComponent<EnemySpawnTelegraph>(),
                region = "Arena", position = new Vector2((x + 1f) * .5f, (z + 1f) * .5f), facing = facing,
                socket = socket, releaseDelay = delay, entrySeconds = entry, entrySpeed = 1.4f };
        var rows = Asset<EnemyFormation>("01 Drone Paired Rows", f =>
        { foreach (float x in new[] { -.65f, 0f, .65f }) { f.slots.Add(Slot(0, x, .75f, Vector2.down, entry: .9f)); f.slots.Add(Slot(0, x, -.75f, Vector2.up, entry: .9f)); } });
        var flanks = Asset<EnemyFormation>("02 Ranged Flanks", f =>
        { f.slots.Add(Slot(1, -.72f, .25f, Vector2.right)); f.slots.Add(Slot(1, .72f, .25f, Vector2.left)); });
        var seekers = Asset<EnemyFormation>("03 Seeker Stagger", f =>
        { f.slots.Add(Slot(2, -.5f, .67f, Vector2.down)); f.slots.Add(Slot(2, .5f, .67f, Vector2.down, .7f)); });
        var dyson = Asset<EnemyFormation>("04 Dyson Flanks", f =>
        { f.slots.Add(Slot(3, -.76f, -.4f, Vector2.right)); f.slots.Add(Slot(3, .76f, -.4f, Vector2.left)); });
        var turrets = Asset<EnemyFormation>("05 Turret Top Bottom", f =>
        { f.slots.Add(Slot(4, 0, 0, Vector2.down, socket: "TopLeft")); f.slots.Add(Slot(4, 0, 0, Vector2.up, socket: "BottomRight")); });
        var sideTurrets = Asset<EnemyFormation>("05b Turret Side Corners", f =>
        { f.slots.Add(Slot(4, 0, 0, Vector2.right, socket: "SideLeftTop")); f.slots.Add(Slot(4, 0, 0, Vector2.left, socket: "SideRightBottom")); });
        var carrier = Asset<EnemyFormation>("06 Carrier Upper Left", f => f.slots.Add(Slot(5, -.52f, .6f, Vector2.down)));
        var alternateCarrier = Asset<EnemyFormation>("06b Carrier Lower Right", f => f.slots.Add(Slot(5, .52f, -.6f, Vector2.up)));
        var timeline = Asset<EnemyEncounterTimeline>("Enemy Lab Timeline", t =>
        {
            t.duration = 85; t.maxPressure = 60;
            var forms = new[] { rows, flanks, seekers, dyson, turrets, carrier };
            var names = new[] { "Drone rows", "Ranged flanks", "Seeker stagger", "Dyson flanks", "Wall turrets", "Carrier" };
            for (int i = 0; i < forms.Length; i++) t.cues.Add(new EnemyEncounterTimeline.Cue
            { label = names[i], arrivalSeconds = 3 + 12 * i, formation = forms[i], allowedLateness = 3,
                fallback = i == 4 ? sideTurrets : i == 5 ? alternateCarrier : null });
            float[] costs = { 1, 2, 3, 3, 5, 6 };
            for (int i = 0; i < defs.Length; i++) t.pressureCosts.Add(new EnemyEncounterTimeline.PressureCost { enemy = defs[i], cost = costs[i] });
        });
        var profile = Asset<EnemySpawnProfile>("Enemy Lab Limits", p =>
        { p.maxAliveTotal = 40; p.maxAliveInert = 8; p.maxAliveRanged = 12; p.maxAliveMelee = 32; });
        Undo.RecordObject(lab, "Add encounter preview mode");
        var root = new GameObject("Encounter Timeline Preview"); root.transform.SetParent(lab.transform, false); root.SetActive(false);
        Undo.RegisterCreatedObjectUndo(root, "Create encounter preview");
        var preview = root.AddComponent<EnemyEncounterLab>(); var layout = root.AddComponent<EnemyArenaLayout>(); var director = root.AddComponent<EnemyDirector>();
        var bounds = Object.FindObjectsByType<ArenaBoundsFromVectorGrid>(FindObjectsSortMode.None).First(x => x.gameObject.scene == scene);
        bounds.RefreshNow(); layout.arena = bounds;
        layout.regions.Add(new EnemyArenaLayout.Region { id = "Arena", rectangle = new Rect(-1, -1, 2, 2) });
        layout.regions.Add(new EnemyArenaLayout.Region { id = "Upper", rectangle = new Rect(-.85f, .45f, 1.7f, .35f) });
        layout.regions.Add(new EnemyArenaLayout.Region { id = "Lower", rectangle = new Rect(-.85f, -.8f, 1.7f, .35f) });
        layout.regions.Add(new EnemyArenaLayout.Region { id = "Left", rectangle = new Rect(-.85f, -.65f, .35f, 1.3f) });
        layout.regions.Add(new EnemyArenaLayout.Region { id = "Right", rectangle = new Rect(.5f, -.65f, .35f, 1.3f) });
        void Socket(string id, Vector2 position, Vector2 inward) => layout.sockets.Add(new EnemyArenaLayout.Socket
        { id = id, position = position, inward = inward, clearanceOffset = 1.12f, aimArc = 75, enabled = !id.StartsWith("Side") });
        Socket("TopLeft", new(-.5f, 1), Vector2.down); Socket("TopRight", new(.5f, 1), Vector2.down);
        Socket("BottomLeft", new(-.5f, -1), Vector2.up); Socket("BottomRight", new(.5f, -1), Vector2.up);
        Socket("SideLeftTop", new(-1, .75f), Vector2.right); Socket("SideLeftBottom", new(-1, -.75f), Vector2.right);
        Socket("SideRightTop", new(1, .75f), Vector2.left); Socket("SideRightBottom", new(1, -.75f), Vector2.left);
        var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.name = "Center exclusion test obstacle";
        obstacle.transform.SetParent(root.transform, false); obstacle.transform.position = layout.World(Vector2.zero);
        obstacle.transform.localScale = new Vector3(3, 1, 3); obstacle.layer = LayerMask.NameToLayer("Obstacle");
        obstacle.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scripts/Player/Demonstrations/Enemy Lab Labels.mat");
        var outline = new GameObject("Outline").AddComponent<LineRenderer>(); outline.transform.SetParent(obstacle.transform, false);
        outline.useWorldSpace = false; outline.loop = true; outline.positionCount = 4; outline.widthMultiplier = .012f;
        outline.SetPositions(new[] { new Vector3(-.5f,.51f,-.5f),new Vector3(.5f,.51f,-.5f),new Vector3(.5f,.51f,.5f),new Vector3(-.5f,.51f,.5f) });
        outline.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scripts/Player/Demonstrations/Enemy Lab Dividers.mat");
        layout.exclusions = new Collider[] { obstacle.GetComponent<Collider>() }; obstacle.SetActive(false);
        director.spawnProfile = profile; director.encounterTimeline = timeline; director.arenaLayout = layout; director.arenaBounds = bounds;
        director.borderBufferWorld = .2f; director.spawnCheckRadiusWorld = .55f; director.minDistanceFromPlayers = 1.4f;
        director.spawnBlockMask = LayerMask.GetMask("Obstacle"); director.waitForScoring = false;
        preview.director = director; preview.layout = layout; preview.centerObstacle = obstacle;
        preview.playerPrefab = lab.columns[0].playerPrefab;
        var template = lab.columns[0].transform.Find("Enemy name").GetComponent<TMP_Text>();
        var label = Object.Instantiate(template, root.transform); label.name = "Timeline status";
        label.transform.position = new Vector3(0, template.transform.position.y, template.transform.position.z);
        label.rectTransform.sizeDelta = new Vector2(27.5f, .4f); label.fontSize = 2f;
        label.text = "ENCOUNTER TIMELINE  |  Press Play to preview"; label.enableAutoSizing = false; preview.status = label;
        var plate = Object.Instantiate(lab.columns[0].transform.Find("Name backing").gameObject, root.transform);
        plate.name = "Timeline status backing"; plate.transform.position = label.transform.position - Vector3.up * .02f;
        plate.transform.localScale = new Vector3(27.5f, .4f, 1f);
        lab.timelinePreview = preview; lab.mode = EnemyLab.LabMode.EncounterTimeline;
        EditorUtility.SetDirty(lab); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = lab.gameObject;
        Debug.Log("[Encounter Lab] Installed six-cue timeline and arena layout preview.");
    }
    private static T Asset<T>(string name, Action<T> initialize) where T : ScriptableObject
    {
        string path = Folder + "/" + name + ".asset";
        var value = AssetDatabase.LoadAssetAtPath<T>(path); if (value) return value;
        value = ScriptableObject.CreateInstance<T>(); initialize(value); AssetDatabase.CreateAsset(value, path); return value;
    }
}
#endif
