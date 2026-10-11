#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Enemies;
using Massive.Levels;
using Massive.Multiplier;
using Massive.Player;
using Massive.PowerUps;
using Massive.Scoring;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Targeted corrections found by the playable-catalog audit, with backups and pose checks.</summary>
[InitializeOnLoad]
public static class UniversalLevelRepair
{
    const string Folder = UniversalLevelValidation.Folder;
    static UniversalLevelRepair() => EditorApplication.delayCall += () =>
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Folder + "/presentation.request"))
        {
            string restore = File.ReadAllText(Folder + "/presentation.request").Trim();
            File.Delete(Folder + "/presentation.request");
            try
            {
                HideProductionAuthoringControls(); UniversalLevelValidation.Audit();
                if(!string.IsNullOrEmpty(restore)) EditorSceneManager.OpenScene(restore);
                UniversalLevelValidation.CapturePresentation();
            }
            catch(Exception e) { File.AppendAllText(Folder + "/presentation.txt", "\nFAILED " + e); Debug.LogException(e); }
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Folder + "/repair.request")) return;
        File.Delete(Folder + "/repair.request");
        try { Repair(); UniversalLevelValidation.Audit(); }
        catch(Exception e) { File.AppendAllText(Folder + "/repairs.txt", "\nFAILED " + e); Debug.LogException(e); }
    };
    static T[] All<T>(Scene scene) where T : Component => UniversalLevelValidation.All<T>(scene);
    static void Set(Object target, string property, object value) => NovaLevelSetup.Set(target, property, value);
    static void Backup(string path)
    {
        string copy = Folder + "/before/" + path;
        Directory.CreateDirectory(Path.GetDirectoryName(copy));
        if (!File.Exists(copy)) File.Copy(path, copy);
    }
    static Transform Group(string name, Transform parent)
    { var go = new GameObject(name); if (parent) go.transform.SetParent(parent,false); Undo.RegisterCreatedObjectUndo(go,"Repair level integration"); return go.transform; }
    static void Move(Transform t, Transform parent) => Undo.SetTransformParent(t,parent,"Repair level integration");
    static string PathOf(Transform t) => AnimationUtility.CalculateTransformPath(t,null);
    [MenuItem("MASSIVE/Levels/Hide Production Authoring Controls")]
    public static void HideProductionAuthoringControls()
    {
        var original = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || original.isDirty) throw new InvalidOperationException("Run from a saved scene in Edit Mode.");
        string restore = original.path;
        File.WriteAllText(Folder + "/presentation.txt", "Hide production authoring controls\n");
        try
        {
            const string surfacePath = "Assets/Prefabs/Levels/Arena_Planar_Surface.prefab";
            Backup(surfacePath);
            var surface = PrefabUtility.LoadPrefabContents(surfacePath);
            try
            {
                foreach (var treatment in surface.GetComponentsInChildren<AmplifierGoalTreatments>(true)) Set(treatment,"showControls",false);
                PrefabUtility.SaveAsPrefabAsset(surface,surfacePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(surface); }
            foreach (string definitionPath in File.ReadAllLines(Folder + "/catalog-assets.txt"))
            {
                string path = UniversalLevelValidation.PathFor(AssetDatabase.LoadAssetAtPath<LevelDefinition>(definitionPath));
                Backup(path);
                var scene = EditorSceneManager.OpenScene(path);
                foreach (var treatment in All<AmplifierGoalTreatments>(scene))
                {
                    Set(treatment,"showControls",false);
                    if(PrefabUtility.IsPartOfPrefabInstance(treatment)) PrefabUtility.RecordPrefabInstancePropertyModifications(treatment);
                }
                foreach (var storm in All<DynamoStormController>(scene))
                {
                    Set(storm,"showDebugUI",false);
                    if(PrefabUtility.IsPartOfPrefabInstance(storm)) PrefabUtility.RecordPrefabInstancePropertyModifications(storm);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
                File.AppendAllText(Folder + "/presentation.txt", path + ": saved with preview/debug controls hidden.\n");
            }
        }
        finally { EditorSceneManager.OpenScene(restore); }
    }
    static StageProfile Stage(LevelDefinition level, StageProfile existing)
    {
        if (!existing)
        {
            string path = "Assets/Scripts/Anomalies/HIGGS/StageProfile-HIGGS.asset";
            existing = AssetDatabase.LoadAssetAtPath<StageProfile>(path);
            if (!existing) { existing = ScriptableObject.CreateInstance<StageProfile>(); AssetDatabase.CreateAsset(existing,path); }
        }
        string expected = "STAGE_" + level.levelNumber.ToString("000");
        if (existing.stageId != expected || existing.displayName != level.levelTitle)
        {
            Backup(AssetDatabase.GetAssetPath(existing));
            existing.stageId = expected; existing.displayName = level.levelTitle;
            EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing);
        }
        return existing;
    }
    [MenuItem("MASSIVE/Levels/Repair Audited Universal Integration Gaps")]
    public static void Repair()
    {
        var original = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || original.isDirty) throw new InvalidOperationException("Run repairs from a saved scene in Edit Mode.");
        string restore = original.path;
        var definitions = File.ReadAllLines(Folder + "/catalog-assets.txt");
        File.WriteAllText(Folder + "/repairs.txt", "Targeted catalog repairs\n");
        foreach (string definitionPath in definitions)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(definitionPath);
            string path = UniversalLevelValidation.PathFor(level); Backup(path);
            if (!File.ReadAllText(definitionPath).Contains("sceneName: " + Path.GetFileNameWithoutExtension(path) + "\n"))
            {
                Backup(definitionPath); level.gameplayScene.SyncFromAsset();
                EditorUtility.SetDirty(level); AssetDatabase.SaveAssetIfDirty(level);
            }
            var scene = EditorSceneManager.OpenScene(path);
            // Opening another scene can unload assets referenced by the previous scene.
            level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(definitionPath);
            var poses = All<Transform>(scene).ToDictionary(t=>t,t=>t.localToWorldMatrix);
            var context = All<LevelSceneContext>(scene).SingleOrDefault();
            if (!context) context = ComposeOrbital(scene,level);
            var match = All<GameManagerScript>(scene).Single(); var scores = All<MatchScoreService>(scene).Single();
            var economy = AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
            Set(match,"scoreService",scores); Set(match,"scoreEconomyProfile",economy);
            Set(match,"disableGameplayOnEnd",true);
            Set(context,"stage",Stage(level,context.stage ? context.stage : context.anomalies ? context.anomalies.stageProfile : null));
            if (context.anomalies) { Set(context.anomalies,"stageProfile",context.stage); Set(context.anomalies,"match",match); }
            context.stageTitleText.text = level.levelTitle; context.stageNumberText.text = "STAGE_" + level.levelNumber.ToString("000");
            foreach (var player in All<PlayerControllerScript>(scene).Where(p=>!p.IsPseudoPlayer))
            {
                var areas = player.GetComponentsInChildren<PlayerRepulsorAOE>(true);
                if (areas.Length < 1) throw new InvalidOperationException("Missing Repulsor: " + player.name);
                // Legacy scene-added copies and the newer player prefab both subscribe to
                // the same attack events, causing two independent hit sets per pulse.
                var keep = areas.FirstOrDefault(a=>PrefabUtility.GetCorrespondingObjectFromSource(a)) ?? areas[0];
                foreach (var duplicate in areas.Where(a=>a!=keep))
                {
                    if (duplicate.transform.childCount != 0 || duplicate.GetComponents<Component>().Any(c=>!(c is Transform) && !(c is SphereCollider) && !(c is PlayerRepulsorAOE)))
                        throw new InvalidOperationException("Inspect nontrivial duplicate Repulsor before repair.");
                    Undo.DestroyObjectImmediate(duplicate.gameObject);
                }
                Set(keep,"owner",player); Set(keep,"attackController",player.GetComponent<PlayerAttackController>()); Set(keep,"hitbox",keep.GetComponent<SphereCollider>());
            }
            if (level.levelTitle == "NOVA" && !All<EnemyDirector>(scene).Any()) AddNovaEnemies(scene);
            if (level.levelTitle == "COSMOS")
            {
                var loose = scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="superluminous");
                if (loose) Move(loose.transform, All<Transform>(scene).Single(t=>PathOf(t)=="GameplayObjects/LevelContent/COSMOS"));
            }
            if (level.levelTitle != "COSMOS" && level.levelTitle != "SINGULARITY")
            {
                var bounds = All<ArenaBoundsFromVectorGrid>(scene).Single();
                if (!bounds.GetComponent<ArenaOutsideMask>())
                {
                    var mask = Undo.AddComponent<ArenaOutsideMask>(bounds.gameObject);
                    Set(mask,"arena",bounds); Set(mask,"maskShader",Shader.Find("MASSIVE/Cosmos/ArenaMask"));
                    foreach (var renderer in All<MeshRenderer>(scene).Where(r=>r.name.StartsWith("mask plane",StringComparison.OrdinalIgnoreCase)))
                    { Undo.RecordObject(renderer,"Replace fixed exterior masks"); renderer.enabled = false; }
                }
            }
            foreach (var pose in poses.Where(p=>p.Key)) for (int i=0;i<16;i++)
                if (Mathf.Abs(pose.Key.localToWorldMatrix[i]-pose.Value[i])>.001f) throw new InvalidOperationException("World pose changed: " + PathOf(pose.Key));
            foreach (var c in All<Component>(scene).Where(c=>c))
            { EditorUtility.SetDirty(c); if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c); }
            foreach(var t in All<Transform>(scene)) if(PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
            File.AppendAllText(Folder + "/repairs.txt", level.levelTitle + ": saved; all retained world poses unchanged.\n");
        }
        EditorSceneManager.OpenScene(restore);
    }
    static LevelSceneContext ComposeOrbital(Scene scene, LevelDefinition level)
    {
        if (level.levelTitle != "ORBITAL") throw new InvalidOperationException("Unexpected legacy scene " + level.levelTitle);
        var old = All<Transform>(scene).GroupBy(PathOf).ToDictionary(g=>g.Key,g=>g.First());
        var systems = Group("00_Systems",null); var presentation = Group("01_Presentation",null); var arena=Group("02_Arena",null); var ui=Group("04_UI",null);
        var editor=Group("90_EditorOnly",null); editor.tag="EditorOnly";
        var runtime=Group("MatchRuntime",systems); var gm=All<GameManagerScript>(scene).Single(); var roster=All<PlayerRosterController>(scene).Single();
        var scores=All<MatchScoreService>(scene).Single(); var anomalies=All<AnomalyManager>(scene).Single(); var timer=All<MatchTimerPresenter>(scene).Single();
        Move(gm.transform,runtime); Move(roster.transform,runtime); Move(scores.transform,runtime); Move(anomalies.transform,systems); anomalies.name="AnomalyRuntime";
        var context=Undo.AddComponent<LevelSceneContext>(Group("LevelSceneContext",systems).gameObject);
        context.level=level; context.stage=anomalies.stageProfile; context.match=gm; context.roster=roster; context.anomalies=anomalies;
        context.inputManagerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rewired Input Manager.prefab");
        Move(old["ORBITAL Level Context"],systems);
        foreach(var initializer in All<Rewired.Initializer>(scene)) Undo.DestroyObjectImmediate(initializer.gameObject);
        Move(old["Main Camera"],Group("CameraRig",presentation)); Move(old["Directional Light"],Group("Lighting",presentation));
        Move(old["Scene Managers/SFX Manager"],Group("Audio",presentation)); Move(old["DEATH SPHERE"],Group("MatchEndFX",presentation));
        Move(old["Post process volume"],Group("PostProcessing",presentation));
        var field=old["PLAYING FIELD"].gameObject;
        if(PrefabUtility.IsPartOfPrefabInstance(field)) PrefabUtility.UnpackPrefabInstance(field,PrefabUnpackMode.OutermostRoot,InteractionMode.UserAction);
        Move(old["PLAYING FIELD/GRID"],Group("Surface",arena)); var collision=Group("BoundsAndCollision",arena);
        Move(old["PLAYING FIELD/Ground Plane"],collision); Move(old["PLAYING FIELD/Mask Planes"],collision);
        var goals=Group("Goals",arena); Move(old["PLAYING FIELD/TEAM 1 goal"],goals); Move(old["PLAYING FIELD/TEAM 2 goal"],goals);
        var anchors=Group("SpawnAnchors",arena); foreach(var p in All<PlayerControllerScript>(scene)) Group("P"+(p.playerID+1),anchors).SetPositionAndRotation(p.transform.position,p.transform.rotation);
        Group("NoSpawnZones",arena);
        var gameplay=old["GameplayObjects"]; var encounters=Group("Encounters",gameplay);
        var pair=All<AmplifierResonanceSpawner>(scene).Single(); Move(pair.transform,encounters); pair.name="AmplifierResonance";
        Move(All<EnemyDirector>(scene).Single().transform,encounters);
        var content=Group("ORBITAL",Group("LevelContent",gameplay));
        foreach(Transform child in gameplay.Cast<Transform>().ToArray()) if(child.name=="ORBITAL Cloud") Move(child,content);
        context.spawners=Group("Spawners",gameplay).gameObject; Move(old["Scene Managers/POWERUP_SPAWNER"],context.spawners.transform);
        Move(old["GameplayObjects/Power Ups"],Group("Runtime",gameplay));
        var hud=old["PLAYING FIELD/UI"]; Move(hud,ui); hud.name="MatchHUD"; Move(timer.transform,hud);
        context.stageTitleText=hud.Find("Text Objects/Stage Title/STAGE TITLE").GetComponent<TMP_Text>();
        context.stageNumberText=hud.Find("Text Objects/Stage Title/STAGE NUMBER").GetComponent<TMP_Text>();
        Set(timer,"gameManager",gm); Set(timer,"phaseText",hud.Find("Text Objects/Timer/TIME title").GetComponent<TMP_Text>());
        var feedback=Group("Feedback",ui); Move(old["Scene Managers/PlayerIDToastSystem"],feedback);
        var source=EditorSceneManager.OpenPreviewScene(SingularityLevelSetup.ScenePath);
        try { var toast=Undo.AddComponent<TeamAmplifierToastPresenter>(Group("TeamAmplifierToast",feedback).gameObject); EditorUtility.CopySerialized(All<TeamAmplifierToastPresenter>(source).Single(),toast); toast.Configure(null,null); }
        finally { EditorSceneManager.ClosePreviewScene(source); }
        Move(old["ANOMALY CANVAS"],Group("AnomalyUI",ui)); Move(old["MANAGERS/EventSystem"],ui); Group("ORBITAL",Group("LevelUI",ui));
        foreach(string name in new[]{"PLAYING FIELD","Scene Managers","MANAGERS"})
        { if(old[name].childCount!=0) throw new InvalidOperationException("Nonempty legacy group "+name); Undo.DestroyObjectImmediate(old[name].gameObject); }
        foreach(var root in new[]{systems,presentation,arena,gameplay,ui,editor}) root.SetAsLastSibling();
        return context;
    }
    static void AddNovaEnemies(Scene scene)
    {
        var bounds=All<ArenaBoundsFromVectorGrid>(scene).Single(); var pair=All<AmplifierResonanceSpawner>(scene).Single();
        var source=EditorSceneManager.OpenPreviewScene(SingularityLevelSetup.ReferenceScene);
        try
        {
            var template=All<EnemyDirector>(source).Single();
            var root=(GameObject)PrefabUtility.InstantiatePrefab(PrefabUtility.GetCorrespondingObjectFromSource(template.gameObject),scene);
            root.name="NOVA Enemy Timeline"; root.transform.SetParent(pair.transform.parent,false);
            var director=root.GetComponent<EnemyDirector>(); EditorUtility.CopySerialized(template,director);
            var layout=Undo.AddComponent<EnemyArenaLayout>(root); EditorUtility.CopySerialized(template.arenaLayout,layout);
            layout.arena=bounds; layout.exclusions=pair.spawnRegion.noGoColliders; layout.spawnOverlapRoots=Array.Empty<Transform>();
            foreach(var socket in layout.sockets) socket.support=null;
            director.arenaBounds=bounds; director.arenaLayout=layout; director.enemyRoot=Group("Timeline enemies",root.transform);
            director.anomalyManager=All<AnomalyManager>(scene).Single(); director.resonanceSpawner=pair; director.placementRegion=null; director.previewCue=-1; director.waitForScoring=true;
            root.GetComponent<AmplifierSpawnRegion>().arenaBounds=bounds;
        }
        finally { EditorSceneManager.ClosePreviewScene(source); }
    }
}
#endif
