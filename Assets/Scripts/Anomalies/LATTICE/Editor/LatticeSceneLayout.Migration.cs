#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Enemies;
using Massive.Levels;
using Massive.Multiplier;
using Massive.Scoring;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.Lattice.EditorTools
{
    public static partial class LatticeSceneLayout
    {
        static readonly string[] Roots = { "00_Systems", "01_Presentation", "02_Arena", "GameplayObjects", "04_UI", "90_EditorOnly" };
        static Transform At(Scene s, string path) => All<Transform>(s).Single(t => PathOf(t) == path);
        static Transform Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Organize LATTICE scene");
            return go.transform;
        }
        static void Move(Transform child, Transform parent) => Undo.SetTransformParent(child, parent, "Organize LATTICE scene");
        static void Rename(Transform t, string name) { Undo.RecordObject(t.gameObject, "Organize LATTICE scene"); t.name = name; }
        static void Set(Object target, string field, Object value) => NovaLevelSetup.Set(target, field, value);
        static void Require(bool ok, string label) { if (!ok) throw new InvalidOperationException("LATTICE layout: " + label); }

        [MenuItem("MASSIVE/LATTICE/Apply standard scene layout %#&b")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && scene.path == LatticeSetup.ScenePath, "open LATTICE in Edit Mode");
            Require(!scene.isDirty, "save authored edits before reorganizing");
            if (scene.GetRootGameObjects().Any(g => g.name == "00_Systems"))
            {
                if (RemoveLegacyInitializer(scene))
                {
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                    File.AppendAllText(Output + "/preservation.txt", "Lifecycle correction: removed redundant Rewired Initializer and its empty Input group. LevelSceneContext remains the sole input bootstrap; scene-local match systems no longer become persistent.\n");
                }
                ValidateSaved(); return;
            }
            Directory.CreateDirectory(Output);
            if (!File.Exists(Output + "/S-1_LATTICE.before.unity")) File.Copy(scene.path, Output + "/S-1_LATTICE.before.unity");

            var gm = All<GameManagerScript>(scene).Single();
            var context = All<LevelSceneContext>(scene).Single();
            var score = All<MatchScoreService>(scene).Single();
            var timer = All<MatchTimerPresenter>(scene).Single();
            var original = All<Transform>(scene).GroupBy(PathOf).ToDictionary(g => g.Key, g => g.First());
            // Snapshot existing instances, world poses, activation and authored
            // gameplay data before unpacking only the old playing-field wrapper.
            var poses = All<Transform>(scene).ToDictionary(t => t, t => (t.position, t.rotation, t.lossyScale, t.gameObject.activeSelf));
            var tuning = All<MonoBehaviour>(scene).Where(c => c && c != gm && c != context && c != timer && !(c is Rewired.Initializer))
                .ToDictionary(c => c, c => JsonUtility.ToJson(c));
            var references = new List<(Component owner, string path, Object value)>();
            foreach (var c in All<Component>(scene).Where(c => c && !(c is Transform) && !(c is Rewired.Initializer)))
            {
                var p = new SerializedObject(c).GetIterator();
                while (p.Next(true))
                    if (p.propertyType == SerializedPropertyType.ObjectReference && !p.propertyPath.StartsWith("m_") && p.objectReferenceValue)
                        references.Add((c, p.propertyPath, p.objectReferenceValue));
            }
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Organize LATTICE scene");
            var systems = Group("00_Systems");
            var presentation = Group("01_Presentation");
            var arena = Group("02_Arena");
            var ui = Group("04_UI");
            var editorOnly = Group("90_EditorOnly"); editorOnly.tag = "EditorOnly";
            var match = Group("MatchRuntime", systems);
            Move(gm.transform, match); Move(context.roster.transform, match); Move(score.transform, match);
            Move(context.anomalies.transform, systems); Rename(context.anomalies.transform, "AnomalyRuntime");
            Move(context.transform, systems); Rename(context.transform, "LevelSceneContext");
            RemoveLegacyInitializer(scene);

            Move(original["Main Camera"], Group("CameraRig", presentation));
            Move(original["Directional Light"], Group("Lighting", presentation));
            Move(original["Scene Managers/SFX Manager"], Group("Audio", presentation));
            Move(original["DEATH SPHERE"], Group("MatchEndFX", presentation));
            Move(original["Post process volume"], Group("PostProcessing", presentation));

            var playingField = original["PLAYING FIELD"].gameObject;
            if (PrefabUtility.IsPartOfPrefabInstance(playingField))
                PrefabUtility.UnpackPrefabInstance(playingField, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
            var hud = original["PLAYING FIELD/UI"];
            Move(hud, ui); Rename(hud, "MatchHUD");
            Move(original["PLAYING FIELD/GRID"], Group("Surface", arena));
            var collision = Group("BoundsAndCollision", arena);
            Move(original["PLAYING FIELD/Ground Plane"], collision); Move(original["PLAYING FIELD/Mask Planes"], collision);
            var goals = Group("Goals", arena);
            Move(original["PLAYING FIELD/TEAM 1 goal"], goals); Move(original["PLAYING FIELD/TEAM 2 goal"], goals);
            var anchors = Group("SpawnAnchors", arena);
            foreach (var p in All<PlayerControllerScript>(scene).OrderBy(p => p.playerID))
                Group("P" + (p.playerID + 1), anchors).SetPositionAndRotation(p.transform.position, p.transform.rotation);
            Group("NoSpawnZones", arena);

            var gameplay = original["GameplayObjects"];
            var encounters = Group("Encounters", gameplay);
            var amplifier = All<AmplifierResonanceSpawner>(scene).Single();
            Move(amplifier.transform, encounters); Rename(amplifier.transform, "AmplifierResonance");
            var spawners = Group("Spawners", gameplay);
            Move(original["Scene Managers/POWERUP_SPAWNER"], spawners);
            Move(All<EnemyDirector>(scene).Single().transform, Group("LATTICE", Group("LevelContent", gameplay)));
            Move(original["GameplayObjects/Power Ups"], Group("Runtime", gameplay));

            Move(timer.transform, hud);
            var feedback = Group("Feedback", ui);
            Move(original["Scene Managers/PlayerIDToastSystem"], feedback);
            var toast = All<TeamAmplifierToastPresenter>(scene).SingleOrDefault();
            if (!toast)
            {
                toast = Undo.AddComponent<TeamAmplifierToastPresenter>(Group("TeamAmplifierToast", feedback).gameObject);
                var nova = EditorSceneManager.OpenPreviewScene(NovaLevelSetup.NovaPath);
                try
                {
                    var source = All<TeamAmplifierToastPresenter>(nova).Single();
                    EditorUtility.CopySerialized(source, toast);
                    // This standard presenter creates its own overlay and uses
                    // shared settings. Never retain a reference into NOVA.
                    toast.Configure(null, null);
                }
                finally { EditorSceneManager.ClosePreviewScene(nova); }
            }
            Move(original["ANOMALY CANVAS"], Group("AnomalyUI", ui));
            Move(original["MANAGERS/EventSystem"], ui);
            Group("LATTICE", Group("LevelUI", ui));

            Set(context, "spawners", spawners.gameObject);
            Set(context, "stageTitleText", hud.Find("Text Objects/Stage Title/STAGE TITLE").GetComponent<TMP_Text>());
            Set(context, "stageNumberText", hud.Find("Text Objects/Stage Title/STAGE NUMBER").GetComponent<TMP_Text>());
            Set(gm, "scoreService", score);
            Set(gm, "scoreEconomyProfile", AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset"));
            Set(timer, "phaseText", hud.Find("Text Objects/Timer/TIME title").GetComponent<TMP_Text>());
            foreach (string old in new[] { "PLAYING FIELD", "Scene Managers", "MANAGERS" })
            {
                Require(original[old].childCount == 0 && original[old].GetComponents<Component>().Length == 1, "legacy wrapper is empty: " + old);
                Undo.DestroyObjectImmediate(original[old].gameObject);
            }
            for (int i = 0; i < Roots.Length; i++) scene.GetRootGameObjects().Single(g => g.name == Roots[i]).transform.SetSiblingIndex(i);

            foreach (var entry in poses.Where(p => p.Key))
            {
                var t = entry.Key; var before = entry.Value;
                Require(Vector3.Distance(t.position, before.position) < .001f && Quaternion.Angle(t.rotation, before.rotation) < .01f &&
                    Vector3.Distance(t.lossyScale, before.lossyScale) < .001f && t.gameObject.activeSelf == before.activeSelf, "world pose/activation preserved: " + PathOf(t));
            }
            foreach (var entry in tuning) Require(entry.Key && JsonUtility.ToJson(entry.Key) == entry.Value, "authored component data preserved: " + entry.Key);
            foreach (var r in references)
            {
                if (r.owner == context && (r.path == "stageTitleText" || r.path == "stageNumberText")) continue;
                Require(r.owner && new SerializedObject(r.owner).FindProperty(r.path).objectReferenceValue == r.value,
                    "existing reference preserved: " + r.owner + "." + r.path);
            }
            Validate(scene);
            foreach (var c in All<Component>(scene).Where(c => c && PrefabUtility.IsPartOfPrefabInstance(c)))
                PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Output + "/preservation.txt", $"PASSED\n{poses.Count} original world poses/activation states checked; only three empty legacy wrappers and the redundant input initializer removed.\n{tuning.Count} retained MonoBehaviour configurations unchanged.\n{references.Count} existing object references checked; only the two inactive stage-label targets replaced.\n");
            ValidateSaved();
            Debug.Log("LATTICE standard hierarchy saved; existing gameplay and presentation preserved.");
        }

        static void Validate(Scene scene)
        {
            Require(scene.GetRootGameObjects().Select(g => g.name).SequenceEqual(Roots), "NOVA's six ordered roots");
            var context = All<LevelSceneContext>(scene).Single();
            var gm = All<GameManagerScript>(scene).Single();
            var score = All<MatchScoreService>(scene).Single();
            Require(context.match == gm && context.roster && context.anomalies && context.inputManagerPrefab && context.level.levelTitle == "LATTICE", "complete LATTICE scene context");
            Require(context.spawners == At(scene, "GameplayObjects/Spawners").gameObject, "match phase controls standard spawner group");
            Require(context.stageTitleText.gameObject.activeInHierarchy && context.stageNumberText.gameObject.activeInHierarchy, "live stage HUD bindings");
            Require(new SerializedObject(gm).FindProperty("scoreService").objectReferenceValue == score && new SerializedObject(gm).FindProperty("scoreEconomyProfile").objectReferenceValue, "explicit shared score authority and economy");
            Require(All<TeamAmplifierToastPresenter>(scene).Length == 1 && All<MatchTimerPresenter>(scene).Length == 1, "single timer and team amplifier feedback");
            Require(All<AudioListener>(scene).Count(c => c.enabled && c.gameObject.activeInHierarchy) == 1, "one active audio listener");
            Require(All<Rewired.Initializer>(scene).Length == 0, "LevelSceneContext owns input startup without a persistent legacy parent");
            var field = All<LatticeDisruptionField>(scene).Single();
            Require(All<LatticePlayerMotor>(scene).Length == 4 && All<LatticePlayerMotor>(scene).All(m => m.field == field), "four original lattice movement/attack adapters");
            Require(At(scene, "02_Arena/Surface/GRID/VectorGridGPU") == field.transform && field.Grid.size == new Vector2(28,12), "original grid and arena footprint");
            Require(All<EnemyDirector>(scene).Length == 1 && All<AmplifierResonanceSpawner>(scene).Length == 1, "both existing encounters retained");
            foreach (var t in All<Transform>(scene)) Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "no missing scripts at " + PathOf(t));
            foreach (var c in All<Component>(scene).Where(c => c))
            {
                var p = new SerializedObject(c).GetIterator();
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference || p.propertyPath.StartsWith("m_")) continue;
                    Require(p.objectReferenceValue || p.objectReferenceInstanceIDValue == 0, "no broken reference: " + c + "." + p.propertyPath);
                    var value = p.objectReferenceValue;
                    var owner = value is GameObject go ? go.transform : (value as Component)?.transform;
                    Require(!owner || !owner.gameObject.scene.IsValid() || owner.gameObject.scene == scene, "no cross-scene reference: " + c + "." + p.propertyPath);
                }
            }
        }

        static bool RemoveLegacyInitializer(Scene scene)
        {
            var initializers = All<Rewired.Initializer>(scene);
            foreach (var initializer in initializers)
            {
                // The legacy initializer persists its root. Under 00_Systems
                // that would persist the match/roster, invalidating scene bindings.
                // LevelSceneContext already creates the same input prefab once.
                var parent = initializer.transform.parent;
                Undo.DestroyObjectImmediate(initializer.gameObject);
                if (parent && parent.name == "Input" && parent.childCount == 0 && parent.GetComponents<Component>().Length == 1)
                    Undo.DestroyObjectImmediate(parent.gameObject);
            }
            return initializers.Length > 0;
        }

        [MenuItem("MASSIVE/LATTICE/Validate saved scene layout")]
        public static void ValidateSaved()
        {
            Directory.CreateDirectory(Output);
            var scene = EditorSceneManager.OpenPreviewScene(LatticeSetup.ScenePath);
            try { Validate(scene); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            File.WriteAllText(Output + "/authored.txt", "PASSED\nSaved hierarchy, LATTICE identity/adapters, shared systems, live HUD bindings, prefab import and all serialized scene references verified.\n");
        }
    }
}
#endif
