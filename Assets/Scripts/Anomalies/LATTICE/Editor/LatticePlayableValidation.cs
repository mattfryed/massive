using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Levels;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.Lattice.Editor
{
    [InitializeOnLoad]
    public static class LatticePlayableValidation
    {
        const string Key = "MASSIVE.LatticePlayableValidation";
        const string Output = "Library/LatticePlayableValidation";
        static readonly List<string> results = new(), errors = new();
        static IEnumerator routine;
        static double deadline;
        static int frame;
        static LatticePlayableValidation() { EditorApplication.playModeStateChanged += State; }

        [MenuItem("MASSIVE/LATTICE/Validate Playable Level %#&y")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (SceneManager.GetActiveScene().name != SceneFlow.LevelSelectScene)
                throw new InvalidOperationException("Open Level Select in Edit Mode to validate its complete LATTICE launch flow.");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/report.txt", "RUNNING\n");
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
        }
        static void State(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                results.Clear(); errors.Clear(); frame = -1; deadline = EditorApplication.timeSinceStartup + 110;
                SessionState.SetBool(Key + "Background", Application.runInBackground); Application.runInBackground = true;
                routine = Checks(); Application.logMessageReceived += Log; EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            { Application.runInBackground = SessionState.GetBool(Key + "Background", true); SessionState.SetBool(Key, false); }
        }
        static void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || Time.frameCount == frame) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Playable LATTICE flow timed out in " + SceneManager.GetActiveScene().name);
                frame = Time.frameCount; if (!routine.MoveNext()) Finish(null);
            }
            catch (Exception e) { Finish(e); }
        }
        static void Finish(Exception e)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log; Time.timeScale = 1;
            bool ok = e == null && errors.Count == 0;
            File.WriteAllText(Output + "/report.txt", (ok ? "PASSED\n" : "FAILED\n") + string.Join("\n", results) + "\n" + e + "\n" + string.Join("\n", errors));
            routine = null; EditorApplication.isPlaying = false;
            Debug.Log("LATTICE playable level validation " + (ok ? "PASSED" : "FAILED"));
        }
        static void Check(bool ok, string label) { if (!ok) throw new Exception(label); results.Add("PASS " + label); }
        static IEnumerator Checks()
        {
            var definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LatticePlayableSetup.DefinitionPath);
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(LatticePlayableSetup.CatalogPath);
            Check(definition && definition.levelTitle == "LATTICE" && definition.scaleExponent == -35, "LATTICE definition represents the Planck-scale end of the ruler");
            Check(catalog.Count == 7 && catalog.Get(0) == definition && catalog.Levels.Distinct().Count() == 7, "Catalog contains LATTICE exactly once before the six existing stages");
            Check(catalog.Levels.Select((level, i) => level.levelNumber == i + 1).All(v => v), "All seven stage numbers follow carousel order");
            Check(definition.SceneName == "S-1_LATTICE" && Application.CanStreamedLevelBeLoaded(definition.SceneName), "Gameplay scene is enabled and loadable through SceneReference");
            Check(definition.audioProfile && definition.iconPrefab && definition.instructionsPanelPrefab, "Definition has standard audio, icon and instructions references");
            yield return null; yield return null;
            var carousel = Object.FindFirstObjectByType<LevelCarouselController>();
            Check(carousel && carousel.SelectedLevel == definition, "Actual carousel starts with selectable LATTICE");
            // The authored carouselRoot may be a sibling of its controller.
            var icon = Object.FindObjectsByType<LatticeLevelIcon>(FindObjectsInactive.Include, FindObjectsSortMode.None).SingleOrDefault(i => i.gameObject.scene.name == SceneFlow.LevelSelectScene);
            float readyUntil = Time.realtimeSinceStartup + 3;
            while (icon && !icon.IsReady && Time.realtimeSinceStartup < readyUntil) yield return null;
            Check(icon && icon.IsReady && icon.cellsPerAxis == 4 && icon.NodeCount == 125 && icon.EdgeCount == 300, $"Carousel instantiates the user's saved 4-cubed volume (icon={icon}, ready={icon?.IsReady}, cells={icon?.cellsPerAxis}, nodes={icon?.NodeCount}, edges={icon?.EdgeCount}; all icons={string.Join(",", Object.FindObjectsByType<LatticeLevelIcon>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(i => i.name + ":" + i.gameObject.scene.name + ":" + i.gameObject.activeInHierarchy))})");
            var authoredIcon = definition.iconPrefab.GetComponentInChildren<LatticeLevelIcon>(true);
            Check(authoredIcon && Mathf.Approximately(icon.noiseFrequency, authoredIcon.noiseFrequency) &&
                Mathf.Approximately(icon.threshold, authoredIcon.threshold) && icon.dotJitterRadius == authoredIcon.dotJitterRadius &&
                icon.dotDiameterPixels == authoredIcon.dotDiameterPixels, "Current user-authored noise and dot tuning is preserved");
            var ruler = Object.FindFirstObjectByType<LevelScaleRuler>();
            Check(ruler && ruler.SelectedLevel == definition, "Ruler selection follows the new Planck-scale stage");
            carousel.StepPrevStage(); Check(carousel.SelectedLevel == catalog.Get(6), "Previous wraps from LATTICE to the last stage");
            float settled = Time.realtimeSinceStartup + .4f;
            while (Time.realtimeSinceStartup < settled) yield return null;
            carousel.StepNextStage(); Check(carousel.SelectedLevel == definition, "Next returns from the last stage to LATTICE");
            settled = Time.realtimeSinceStartup + .4f;
            while (Time.realtimeSinceStartup < settled) yield return null;
            carousel.StepNextStage(); Check(carousel.SelectedLevel == catalog.Get(1), "Next advances from LATTICE to HIGGS");
            settled = Time.realtimeSinceStartup + .4f;
            while (Time.realtimeSinceStartup < settled) yield return null;
            carousel.StepPrevStage();
            float until = Time.realtimeSinceStartup + 1;
            while (Time.realtimeSinceStartup < until) yield return null;
            var authoredRotation = icon.transform.localRotation; var placement = icon.transform.localPosition;
            var initialRotation = icon.VisualRotation; float initialClock = icon.FieldTime;
            Time.timeScale = 0; until = Time.realtimeSinceStartup + 2;
            while (Time.realtimeSinceStartup < until) yield return null;
            Check(Quaternion.Angle(initialRotation, icon.VisualRotation) > .2f && icon.FieldTime > initialClock + .5f, "Slow rotation and disruption keep moving while gameplay time is paused");
            Check(Quaternion.Angle(authoredRotation, icon.transform.localRotation) < .001f && icon.transform.localPosition == placement, "Tumble preserves authored orientation and carousel placement");
            var frozen = icon.VisualRotation; icon.rotateIcon = false; until = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Check(Quaternion.Angle(frozen, icon.VisualRotation) < .001f, "Rotation toggle holds orientation without stopping the field");
            icon.rotateIcon = true; Time.timeScale = 1;
            ScreenCapture.CaptureScreenshot(Output + "/carousel.png"); yield return null; yield return null;
            carousel.ConfirmSelection();
            while (SceneManager.GetActiveScene().name != SceneFlow.InstructionsScene) yield return null;
            yield return null; yield return null;
            Check(GameFlowContext.Instance.SelectedLevel == definition, "Carousel confirmation hands LATTICE to the standard instructions scene");
            Check(Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(t => t.text.Contains("DOT TO DOT")), "LATTICE's own instructions explain quantized movement");
            until = Time.realtimeSinceStartup + 2;
            while (Time.realtimeSinceStartup < until || !SceneFlow.IsSelectedGameplayReady) yield return null;
            ScreenCapture.CaptureScreenshot(Output + "/instructions.png"); yield return null; yield return null;
            SceneFlow.GoToSelectedGameplay();
            while (SceneManager.GetActiveScene().name != definition.SceneName) yield return null;
            yield return null; yield return null;
            var context = Object.FindFirstObjectByType<LevelSceneContext>();
            Check(context && context.level == definition && GameFlowContext.Instance.SelectedLevel == definition, "Gameplay retains LATTICE identity and standard scene context");
            var match = Object.FindFirstObjectByType<GameManagerScript>();
            while (match && !match.IsStartupBlocked && match.Phase != MatchRuntimePhase.Regulation) yield return null;
            Check(match && !match.IsStartupBlocked && match.Phase == MatchRuntimePhase.Regulation, "Roster and countdown reach normal playable regulation");
            Check(context.spawners && context.spawners.activeInHierarchy, "Standard spawner group is bound and enabled in regulation");
            Check(context.stageTitleText.gameObject.activeInHierarchy && context.stageTitleText.text == "LATTICE" &&
                context.stageNumberText.gameObject.activeInHierarchy && context.stageNumberText.text == "STAGE_001", "Visible stage HUD receives the selected level identity");
            Check(match.ScoreService && match.RegulationDurationSeconds == match.ScoreService.Profile.RegulationDurationSeconds, "Match uses the shared scoring economy and regulation duration");
            var field = Object.FindFirstObjectByType<LatticeDisruptionField>();
            Check(field && field.IsReady && field.EdgeCount > 0, "Gameplay disruption starts after a real carousel launch");
            var players = Object.FindObjectsByType<PlayerControllerScript>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(players.Count(p => p.GetComponent<LatticePlayerMotor>()?.field == field) >= 4, "All four roster actors retain their LATTICE movement adapters");
            until = Time.realtimeSinceStartup + 2;
            while (Time.realtimeSinceStartup < until) yield return null;
            ScreenCapture.CaptureScreenshot(Output + "/gameplay.png"); yield return null; yield return null;
            // Shorten only the live fixture's remaining clock, then exercise normal expiry.
            typeof(GameManagerScript).GetField("_regulationRemainingSeconds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(match, .1f);
            while (match && match.Phase == MatchRuntimePhase.Regulation) yield return null;
            Check(match && match.Phase == MatchRuntimePhase.Resolving && !context.spawners.activeSelf && !match.ScoreService.IsScoringOpen,
                "Match resolution stops spawners and closes scoring before the existing end transition");
            while (SceneManager.GetActiveScene().name != SceneFlow.PostGameScene) yield return null;
            yield return null;
            Check(GameFlowContext.Instance.HasLastMatchResult, "Standard match ending reaches PostGame with results");
            SceneFlow.GoToLevelSelect();
            while (SceneManager.GetActiveScene().name != SceneFlow.LevelSelectScene) yield return null;
            yield return null; yield return null;
            Check(Object.FindFirstObjectByType<LevelCarouselController>().SelectedLevel == definition, "Returning to Level Select recreates the selectable LATTICE icon");
            Check(errors.Count == 0, "No runtime errors during carousel, instructions, gameplay and return flow");
        }
    }
}
