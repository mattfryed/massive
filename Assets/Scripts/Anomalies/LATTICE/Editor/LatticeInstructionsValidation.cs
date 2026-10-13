#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.Lattice.Editor
{
    [InitializeOnLoad]
    public static class LatticeInstructionsValidation
    {
        const string Key = "MASSIVE.LatticeInstructionsValidation";
        const string Output = "Library/LatticeInstructions";
        static readonly List<string> results = new(), errors = new();
        static IEnumerator routine;
        static double deadline;
        static int frame;
        static bool firstFrameObserved, firstFrameReady;
        static string firstFrameState;
        static LatticeInstructionsValidation() { EditorApplication.playModeStateChanged += State; }

        [MenuItem("MASSIVE/LATTICE/Install and validate instructions close-up %#&m")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            LatticeInstructionsSetup.Install();
            Validate();
        }
        [MenuItem("MASSIVE/LATTICE/Validate instructions playback %#&w")]
        public static void Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/report.txt", "RUNNING\n");
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }
        static void State(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                results.Clear(); errors.Clear(); frame = -1;
                deadline = EditorApplication.timeSinceStartup + 150;
                SessionState.SetBool(Key + "Background", Application.runInBackground);
                Application.runInBackground = true;
                routine = Checks(); Application.logMessageReceived += Log; EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
                Camera.onPreCull -= ObserveFirstFrame;
                routine = null;
                Application.runInBackground = SessionState.GetBool(Key + "Background", true);
                SessionState.SetBool(Key, false);
            }
        }
        static void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || Time.frameCount == frame) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Instructions validation timed out.");
                frame = Time.frameCount;
                if (!routine.MoveNext()) Finish(null);
            }
            catch (Exception e) { Finish(e); }
        }
        static void Finish(Exception e)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            Camera.onPreCull -= ObserveFirstFrame;
            bool ok = e == null && errors.Count == 0;
            File.WriteAllText(Output + "/report.txt", (ok ? "PASSED\n" : "FAILED\n") + string.Join("\n", results) + "\n" + e + "\n" + string.Join("\n", errors));
            routine = null; EditorApplication.isPlaying = false;
            Debug.Log("LATTICE instructions validation " + (ok ? "PASSED" : "FAILED"));
        }
        static void Check(bool ok, string label)
        { if (!ok) throw new Exception(label); results.Add("PASS " + label); }
        static void WatchFirstFrame()
        {
            firstFrameObserved = firstFrameReady = false;
            Camera.onPreCull -= ObserveFirstFrame;
            Camera.onPreCull += ObserveFirstFrame;
        }
        static void ObserveFirstFrame(Camera camera)
        {
            if (camera != Camera.main || SceneManager.GetActiveScene().name != SceneFlow.InstructionsScene) return;
            var demo = Object.FindFirstObjectByType<LatticeInstructionsDemo>();
            if (!demo) return;
            Camera.onPreCull -= ObserveFirstFrame;
            firstFrameObserved = true;
            bool playerReady = demo.Player && demo.Player.gameObject.activeInHierarchy;
            bool cameraReady = demo.viewport && demo.viewport.GetComponent<Camera>().enabled;
            firstFrameReady = demo.field.IsReady && playerReady && cameraReady;
            firstFrameState = $"field={demo.field.IsReady}, player={playerReady}, camera={cameraReady}";
            ScreenCapture.CaptureScreenshot(Output + "/first-frame.png");
        }
        static IEnumerator Checks()
        {
            var def = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LatticePlayableSetup.DefinitionPath);
            Check(def.instructionsPanelPrefab == AssetDatabase.LoadAssetAtPath<GameObject>(LatticeInstructionsSetup.PanelPath), "Playable LATTICE references the updated instructions prefab");
            GameFlowContext.EnsureExists(); GameFlowContext.Instance.SelectLevel(def);
            WatchFirstFrame();
            SceneFlow.GoToInstructions();
            while (!firstFrameObserved) yield return null;
            Check(firstFrameReady, "Close-up is ready for the first instructions render: " + firstFrameState);
            while (SceneManager.GetActiveScene().name != SceneFlow.InstructionsScene) yield return null;
            LatticeInstructionsDemo demo;
            do { yield return null; demo = Object.FindFirstObjectByType<LatticeInstructionsDemo>(); } while (!demo || !demo.Player);
            Check(demo.Player.IsPseudoPlayer && demo.Player.UsesGameplayTuning && !demo.Player.ParticipatesInMatch && demo.Player.SimulationRoot,
                "Canonical demo player uses shared tuning, an isolated simulation scope and no match participation");
            Check(!demo.field.Grid.registerAsDefault && VectorGridGPU.Instance != demo.field.Grid, "Close-up does not replace the gameplay grid singleton");
            var scale = demo.Player.GetComponent<Massive.Player.PlayerScaleAdjuster>();
            Check(demo.field.transform.lossyScale == Vector3.one && demo.Player.transform.parent.lossyScale == Vector3.one
                && scale && Vector3.Distance(demo.Player.transform.lossyScale, scale.ReferenceLocalScale * scale.Size) < .0001f,
                $"Gameplay scale preserved: grid spacing {demo.field.Spacing:F2}, player scale {demo.Player.transform.lossyScale}");
            Check(demo.field.DisruptionAt(demo.NormalEnd) < .1f && demo.field.DisruptionAt(demo.DisruptedEnd) > .9f, "Visible lane spans connected grid and disrupted dots");
            Check(demo.field.NoiseLoopSeconds == 24f && Mathf.Abs(demo.field.NoiseAt(Vector2.one, 3f)
                - demo.field.NoiseAt(Vector2.one, 27f)) < .00001f, "Presentation noise repeats smoothly while gameplay retains its default unbounded clock");
            float until = Time.time + 60f, nextSample = Time.time;
            bool normalCaptured = false, dotsCaptured = false;
            int maxSteps = 0;
            while (Time.time < until)
            {
                maxSteps = Mathf.Max(maxSteps, demo.Motor.CompletedSteps);
                if (!normalCaptured && demo.CompletedLoops > 0 && !demo.Motor.IsQuantized)
                { ScreenCapture.CaptureScreenshot(Output + "/normal.png"); normalCaptured = true; }
                if (!dotsCaptured && demo.Motor.IsQuantized && demo.Motor.CompletedSteps > 1)
                { ScreenCapture.CaptureScreenshot(Output + "/quantized.png"); dotsCaptured = true; }
                if (Time.time >= nextSample)
                {
                    File.WriteAllText(Output + "/progress.txt", $"time={Time.time:F1} phase={demo.Phase} loops={demo.CompletedLoops} entries={demo.DisruptionEntries} exits={demo.DisruptionExits} steps={maxSteps} pos={demo.Player.transform.position} normal={demo.NormalEnd} disrupted={demo.DisruptedEnd} strength={demo.stickStrength}\n");
                    nextSample = Time.time + 2f;
                }
                yield return null;
            }
            Check(demo.CompletedLoops >= 3 && demo.DisruptionEntries >= 3 && demo.DisruptionExits >= 3 && maxSteps >= 12,
                $"60-second live loop: {demo.CompletedLoops} loops, {demo.DisruptionEntries} entries, {demo.DisruptionExits} exits, {maxSteps} real grid hops");
            Check(normalCaptured && dotsCaptured, "Captured normal and quantized phases in the actual instructions scene");
            var camera = demo.viewport.GetComponent<Camera>();
            Check(camera.enabled && camera.rect.width > .1f && camera.rect.height > .1f && camera.rect.xMax <= 1.01f && camera.rect.yMax <= 1.01f, "Camera is cropped inside the visible instructions panel");
            var old = demo.Player;
            demo.gameObject.SetActive(false); yield return null; yield return null;
            Check(!old && !camera.enabled, "Disabling panel removes the demo actor and stops the preview camera");
            WatchFirstFrame();
            demo.gameObject.SetActive(true);
            while (!firstFrameObserved) yield return null;
            Check(firstFrameReady, "Reopened close-up is ready for its first render: " + firstFrameState);
            while (!demo.Player) yield return null;
            Check(demo.Player != old && demo.field.IsReady, "Re-enabling recreates the isolated actor and field resources");
            Check(errors.Count == 0, "No errors or exceptions during instructions playback and lifecycle checks");
        }
    }
}
#endif
