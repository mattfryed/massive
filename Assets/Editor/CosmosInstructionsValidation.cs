#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Cosmos;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CosmosInstructionsValidation
{
    const string Key = "COSMOS.InstructionsValidation", Folder = CosmosInstructionsSetup.Folder;
    static readonly List<string> checks = new(), errors = new();
    static readonly HashSet<int> pickupIDs = new();
    static double deadline;
    static int frame;
    static bool baseline, warning, peak, cloud, seam, reenabled, background;
    static float firstLoopTime;
    static MatchScoreService score;
    static long light, dark;

    static CosmosInstructionsValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Folder + "/test.request")) return;
            File.Delete(Folder + "/test.request"); Run();
        };
    }
    [MenuItem("MASSIVE/COSMOS/Instructions/Validate Supernova Loop")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Run from a saved Edit Mode scene.");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/runtime.txt", "STARTING\n");
        AuthoredChecks();
        SessionState.SetString(Key + ".restore", SceneManager.GetActiveScene().path);
        SessionState.SetString(Key + ".sceneHash", Hash128.Compute(File.ReadAllText(CosmosInstructionsSetup.GameplayScene)).ToString());
        SessionState.SetString(Key + ".panelHash", Hash128.Compute(File.ReadAllText(CosmosInstructionsSetup.PanelPath)).ToString());
        SessionState.SetBool(Key + ".finished", false); SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/S-0_INSTRUCTIONS.unity");
        EditorApplication.isPlaying = true;
    }
    static void AuthoredChecks()
    {
        var panel = PrefabUtility.LoadPrefabContents(CosmosInstructionsSetup.PanelPath);
        var scene = EditorSceneManager.OpenPreviewScene(CosmosInstructionsSetup.GameplayScene);
        try
        {
            var demo = panel.GetComponentInChildren<CosmosInstructionsDemo>(true);
            if (!demo || !demo.web || !demo.normal || !demo.superluminous || !demo.viewport || !demo.panelFrame)
                throw new Exception("Missing preview references.");
            if (panel.GetComponentsInChildren<Transform>(true).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0))
                throw new Exception("Missing scripts in instructions prefab.");
            if (demo.GetComponentInChildren<GameManagerScript>(true) || demo.GetComponentInChildren<MatchScoreService>(true) || demo.web.match)
                throw new Exception("Presentation must not own or reference a match clock.");
            var source = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CosmicWebBackground>(true)).Single();
            var sourceGrid = source.GetComponent<VectorGridGPU>();
            var grid = demo.web.GetComponent<VectorGridGPU>();
            if (grid.registerAsDefault || grid.size != sourceGrid.size || grid.gridScale != sourceGrid.gridScale ||
                demo.web.particleShader != source.particleShader || demo.web.bakedTopology != source.bakedTopology ||
                demo.web.particleCount != source.particleCount || demo.web.opaqueParticles != source.opaqueParticles || demo.web.brightness != source.brightness)
                throw new Exception("Background differs from gameplay settings.");
            foreach (var layer in new[] { demo.normal, demo.superluminous })
            {
                var actual = source.GetComponents<CosmicSupernovae>().Single(s => s.kind == layer.kind);
                if (!layer.IsPresentation || actual.IsPresentation || layer.presentationCamera != demo.viewport.GetComponent<Camera>())
                    throw new Exception("Preview/gameplay modes or camera isolation are incorrect.");
                foreach (var field in typeof(CosmicSupernovae).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    if (field.Name == "web" || field.Name == "presentationCamera") continue;
                    var a = field.GetValue(actual); var b = field.GetValue(layer);
                    bool equal = a is AnimationCurve curve ? b is AnimationCurve other && curve.keys.SequenceEqual(other.keys) : Equals(a, b);
                    if (!equal) throw new Exception(layer.kind + " differs from gameplay: " + field.Name);
                }
                if (!layer.flashShader.isSupported || ShaderUtil.ShaderHasError(layer.flashShader)) throw new Exception("Supernova shader failed.");
            }
            File.WriteAllText(Folder + "/authored.txt", "PASSED\n" +
                "PASS Both production layers, every public gameplay setting, curve, shader and pickup prefab match the saved level\n" +
                "PASS Gameplay layers retain match mode; preview layers use isolated presentation mode/camera\n" +
                "PASS Production grid proportions, web shader, bake, particle count, rendering mode and brightness\n" +
                "PASS No missing scripts or match authority in the preview\n" +
                "PASS Shader supported and compiled\n");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); PrefabUtility.UnloadPrefabContents(panel); }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Prepare()
    {
        if (!SessionState.GetBool(Key, false)) return;
        checks.Clear(); errors.Clear(); pickupIDs.Clear(); Application.logMessageReceived += Log;
        GameFlowContext.EnsureExists();
        GameFlowContext.Instance.SelectLevel(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Scripts/Anomalies/COSMOS/LevelDefinition-COSMOS.asset"));
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            deadline = EditorApplication.timeSinceStartup + 100; frame = -1;
            baseline = warning = peak = cloud = seam = reenabled = false;
            background = Application.runInBackground; Application.runInBackground = true;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            Application.runInBackground = background;
            if (!SessionState.GetBool(Key + ".finished", false)) File.AppendAllText(Folder + "/runtime.txt", "\nINTERRUPTED\n");
            EditorSceneManager.OpenScene(SessionState.GetString(Key + ".restore", ""));
        }
    }
    static void Check(bool value, string label)
    {
        checks.Add((value ? "PASS " : "FAIL ") + label);
        File.WriteAllText(Folder + "/runtime.txt", "RUNNING\n" + string.Join("\n", checks));
        if (!value) throw new Exception(label);
    }
    static void Capture(string name) => ScreenCapture.CaptureScreenshot(Folder + "/" + name + ".png");
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Instructions preview timed out.");
            var demo = Object.FindFirstObjectByType<CosmosInstructionsDemo>();
            if (!demo || demo.web.RenderedParticleCount == 0) return;
            if (!baseline)
            {
                baseline = true; score = MatchScoreService.Instance;
                light = score ? score.LightScoreMilliElectronVolts : 0; dark = score ? score.DarkScoreMilliElectronVolts : 0;
                Check(demo.normal.IsPresentation && demo.superluminous.IsPresentation && !demo.web.match, "Both layers run without a match clock");
                Check(demo.web.Age == demo.universeAge && demo.web.UsingBakedTopology, "Current baked cosmic web runs at representative middle age");
                Check(VectorGridGPU.Instance != demo.web.GetComponent<VectorGridGPU>(), "Preview does not replace the gameplay grid singleton");
                Check(demo.normal.nuggletPrefab.rewardMultiplier == .1f && demo.superluminous.nuggletPrefab.rewardMultiplier == 1f,
                    "Normal drops the actual nugglet; superluminous drops the actual full-mass nugget");
            }
            if (!reenabled && !warning && demo.LoopAge > 1.2f && demo.LoopAge < 2.2f)
            {
                warning = true;
                Check(demo.normal.ActiveFlashCount == 1 && demo.superluminous.ActiveFlashCount == 1 &&
                    demo.normal.ExplosionsReleased == 0 && demo.superluminous.ExplosionsReleased == 0, "Both real buildups are visible before any pickup release");
                Capture("01-buildup");
            }
            if (!reenabled && !peak && demo.normal.ExplosionsReleased == 1 && demo.superluminous.ExplosionsReleased == 1)
            {
                peak = true;
                Check(warning, "Both peaks follow the full gameplay buildup");
                Capture("02-explosion");
            }
            if (!reenabled && !cloud && demo.LoopAge > 4.3f && demo.LoopAge < 5.4f)
            {
                cloud = true;
                Check(peak && demo.normal.ActiveFlashCount == 0 && demo.superluminous.ActiveFlashCount == 1, "Superluminous cloud keeps its gameplay expansion and slow dissolve after the normal flash ends");
                var pickups = demo.GetComponentsInChildren<MatterNuggetScript>(true).Where(p => p.gameObject.activeInHierarchy).ToArray();
                Check(pickups.Length == 2 && pickups.All(p => p.VisualScale > .99f && !p.IsDespawning), "Both real pickup prefabs remain at full visual size after ejection");
                Capture("03-cloud-and-pickups");
            }
            foreach (var pickup in demo.GetComponentsInChildren<MatterNuggetScript>(true)) pickupIDs.Add(pickup.GetInstanceID());
            if (!reenabled && !seam && demo.CompletedLoops == 1)
            {
                seam = true; firstLoopTime = demo.web.MotionTime;
                Check(demo.normal.ActiveFlashCount == 0 && demo.superluminous.ActiveFlashCount == 0, "Loop seam has no live flash or cloud to cut off");
                Check(demo.normal.ExplosionsReleased == 2 && demo.superluminous.ExplosionsReleased == 1, "Normal cadence is twice superluminous cadence");
                Capture("04-loop-seam");
            }
            if (!reenabled && demo.CompletedLoops >= 2)
            {
                Check(warning && peak && cloud && seam && demo.normal.ExplosionsReleased == 4 && demo.superluminous.ExplosionsReleased == 2, "Two complete loops release four normal and two superluminous events");
                Check(demo.web.MotionTime > firstLoopTime + 10, "Web motion continues across the loop without rewinding");
                Check(demo.normal.ActiveFlashCount == 0 && demo.superluminous.ActiveFlashCount == 0, "Second loop also ends after the effects fully dissolve");
                var camera = demo.viewport.GetComponent<Camera>();
                Check(camera.enabled && camera.rect.width > .1f && camera.rect.height > .1f && camera.rect.xMin > .5f && camera.rect.xMax <= 1f &&
                    camera.rect.yMin > .15f && camera.rect.yMax < .85f, "Camera stays within the middle-right instructions pane");
                Check(pickupIDs.Count <= 6, "Pickup pooling stays bounded through repeated loops");
                demo.gameObject.SetActive(false);
                Check(!camera.enabled && demo.normal.ActiveFlashCount == 0 && demo.superluminous.ActiveFlashCount == 0 &&
                    demo.GetComponentsInChildren<MatterNuggetScript>(true).All(p => !p.gameObject.activeSelf), "Closing the panel disables its camera and clears all event/pickup visuals");
                demo.gameObject.SetActive(true); reenabled = true;
                Check(demo.CompletedLoops == 0 && demo.normal.ExplosionsReleased == 0 && demo.superluminous.ExplosionsReleased == 0, "Reopening begins a fresh loop without stale effects");
            }
            if (reenabled && demo.normal.ExplosionsReleased > 0 && demo.superluminous.ExplosionsReleased > 0)
            {
                Check(demo.viewport.GetComponent<Camera>().enabled && demo.web.RenderedParticleCount > 0, "Both explosions, web and viewport resume after reopening");
                Check(MatchScoreService.Instance == score && (!score || score.LightScoreMilliElectronVolts == light && score.DarkScoreMilliElectronVolts == dark), "Preview never changes the real score authority or totals");
                Check(Hash128.Compute(File.ReadAllText(CosmosInstructionsSetup.GameplayScene)).ToString() == SessionState.GetString(Key + ".sceneHash", "") &&
                    Hash128.Compute(File.ReadAllText(CosmosInstructionsSetup.PanelPath)).ToString() == SessionState.GetString(Key + ".panelHash", ""), "Runtime validation preserves saved gameplay and instruction assets");
                Check(errors.Count == 0, "No runtime errors or exceptions"); Finish(null);
            }
        }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception failure)
    {
        SessionState.SetBool(Key + ".finished", true);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Folder + "/runtime.txt", (failure == null && errors.Count == 0 ? "PASSED" : "FAILED") + "\n" +
            string.Join("\n", checks) + "\n" + failure + "\nErrors: " + errors.Count + "\n" + string.Join("\n", errors));
        Debug.Log("COSMOS instructions validation complete: " + Folder + "/runtime.txt");
        EditorApplication.isPlaying = false;
    }
}
#endif
