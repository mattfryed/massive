#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Levels;
using Massive.Scoring;
using Massive.Singularity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Saved folded-domain bindings and GPU cutout checks; runtime fixtures are discarded.</summary>
[InitializeOnLoad]
public static class SingularityMaskValidation
{
    const string Key = "MASSIVE.SingularityMaskValidation";
    const string Folder = SingularityLevelSetup.Folder + "/mask";
    static readonly string[] Scenes = { SingularityLevelSetup.ScenePath };
    static readonly List<string> checks = new(), errors = new();
    static IEnumerator routine;
    static int frame;
    static double deadline;
    static bool background;
    static readonly Vector3 Origin = new(1000, 0, 1000);
    static SingularityMaskValidation() => EditorApplication.playModeStateChanged += State;
    static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    static void Check(bool value, string label)
    { if (!value) throw new Exception(label); checks.Add("PASS " + label); File.WriteAllText(Folder + "/report.txt", "RUNNING\n" + string.Join("\n", checks)); }
    [MenuItem("MASSIVE/Levels/Validate SINGULARITY Outside Mask")]
    public static void Run()
    {
        var active = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || !Scenes.Contains(active.path) || active.isDirty)
            throw new InvalidOperationException("Open a saved supported arena in Edit Mode.");
        Directory.CreateDirectory(Folder); checks.Clear(); errors.Clear();
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ArenaOutsideMaskSetup.ShaderPath);
        Check(shader && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "Shared mask shader compiles on this graphics device");
        foreach (string path in Scenes)
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var mask = All<ArenaOutsideMask>(scene).Single();
                Check(mask.enabled && mask.Arena && mask.Arena.Grid && mask.MaskShader == shader, "Saved folded scene uses the shared mask and its explicit projected domain");
                var domains = All<SingularityArenaDomains>(scene).Single();
                Check(domains.projectedDomain == mask.Arena.Grid && Mathf.Abs(domains.projectedDomain.size.y - domains.surface.ProjectedHeight) < .001f, "Saved cutout covers both folds as well as the front face");
                Check(All<Transform>(scene).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0),
                    Path.GetFileNameWithoutExtension(path) + ": no missing scripts");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        File.WriteAllText(Folder + "/authored.txt", string.Join("\n", checks));
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".refreshHeld", true);
        AssetDatabase.DisallowAutoRefresh(); EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); frame = -1; deadline = EditorApplication.timeSinceStartup + 90;
            background = Application.runInBackground; Application.runInBackground = true;
            Application.logMessageReceived += Log; routine = Checks(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            if (SessionState.GetBool(Key + ".refreshHeld", false))
            { SessionState.SetBool(Key + ".refreshHeld", false); AssetDatabase.AllowAutoRefresh(); }
        }
    }
    static void Log(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        string entry = message + "\n" + stack;
        if (!errors.Contains(entry)) errors.Add(entry);
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame == Time.frameCount) return;
        try
        {
            frame = Time.frameCount;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Mask validation timed out");
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception error)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Application.runInBackground = background; Time.timeScale = 1;
        bool passed = error == null && errors.Count == 0;
        File.WriteAllText(Folder + "/report.txt", (passed ? "PASSED" : "FAILED") + $" ({checks.Count} runtime checks)\n" +
            string.Join("\n", checks) + "\n" + error + "\nConsole errors: " + errors.Count + "\n" + string.Join("\n", errors));
        Debug.Log("SINGULARITY OUTSIDE MASK " + (passed ? "PASSED" : "FAILED")); EditorApplication.isPlaying = false;
    }
    static Color32[] Capture(Camera camera, string file)
    {
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            if (file != null) File.WriteAllBytes(Folder + "/" + file + ".png", image.EncodeToPNG());
            return image.GetPixels32();
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image); }
    }
    static Color32 Pixel(Camera camera, Color32[] pixels, Vector3 position)
    {
        var uv = camera.WorldToViewportPoint(position);
        return pixels[Mathf.Clamp((int)(uv.y * 900), 0, 899) * 1600 + Mathf.Clamp((int)(uv.x * 1600), 0, 1599)];
    }
    static bool Black(Color32 c) => c.r < 4 && c.g < 4 && c.b < 4;
    static bool Magenta(Color32 c) => c.r > 200 && c.b > 200 && c.g < 4;
    static void IncludeGoals(ArenaOutsideMask mask, bool value)
    {
        var serialized = new SerializedObject(mask);
        serialized.FindProperty("includeTeamGoalHalfCircles").boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    static GameObject Quad(string name, Vector3 position, Vector3 scale, Material material)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = name;
        Object.Destroy(quad.GetComponent<Collider>());
        quad.transform.SetPositionAndRotation(position, Quaternion.Euler(90, 0, 0)); quad.transform.localScale = scale;
        quad.GetComponent<Renderer>().sharedMaterial = material; return quad;
    }
    static IEnumerator Checks()
    {
        var paths = Scenes.Append(ArenaOutsideMaskSetup.Surface).Append(ArenaOutsideMaskSetup.Bounds).Append(ArenaOutsideMaskSetup.LegacyField).Append("Assets/Scenes/S-9_COSMOS.unity");
        var hashes = paths.ToDictionary(p => p, p => Hash128.Compute(File.ReadAllText(p)));
        var match = Object.FindFirstObjectByType<GameManagerScript>();
        while (match.Phase != MatchRuntimePhase.Regulation) yield return null;
        float until = Time.unscaledTime + 10;
        while (Time.unscaledTime < until) yield return null;
        var real = Object.FindFirstObjectByType<ArenaOutsideMask>();
        string sceneName = SceneManager.GetActiveScene().name;
        Check(Rewired.ReInput.isReady, "SINGULARITY LevelSceneContext initializes its input dependency");
        Check(real && real.isActiveAndEnabled && real.Arena.Grid, sceneName + " draws its shared mask during a live match");
        Time.timeScale = 0;
        yield return null; yield return null;
        var camera = Camera.main;
        var masked = Capture(camera, sceneName + "-masked");
        real.enabled = false; yield return null; yield return null;
        var unmasked = Capture(camera, sceneName + "-unmasked");
        int brightHud = 0, coveredHud = 0;
        for (int y = 0; y < 900; y++)
        {
            if (y > 145 && y < 760) continue;
            for (int x = 0; x < 1600; x++)
            {
                int i = y * 1600 + x; var a = unmasked[i]; var b = masked[i];
                if (Mathf.Max(a.r, a.g, a.b) < 70) continue;
                brightHud++; if (Mathf.Max(b.r, b.g, b.b) < 12) coveredHud++;
            }
        }
        Check(brightHud > 1000 && coveredHud < 10, "Mask retains live title, timer and HUD (" + brightHud + " visible pixels, " + coveredHud + " covered)");
        real.enabled = true;

        var staging = new GameObject("Inactive folded mask fixture"); staging.SetActive(false);
        staging.transform.position = Origin;
        var surface = staging.AddComponent<SingularitySurface>();
        surface.ConfigureProjectedBounds(28, 12, .875f, 3, 2.1f);
        var root = Object.Instantiate(real.gameObject, staging.transform);
        var grid = root.GetComponent<VectorGridGPU>(); grid.enabled = false; grid.registerAsDefault = false;
        var mask = root.GetComponent<ArenaOutsideMask>(); var bounds = mask.Arena;
        var domains = staging.AddComponent<SingularityArenaDomains>(); domains.surface = surface; domains.projectedDomain = grid;
        staging.SetActive(true); domains.Refresh();
        var probeMaterial = new Material(Shader.Find("Unlit/Color")) { color = Color.magenta };
        var frontMaterial = new Material(probeMaterial) { color = Color.cyan };
        var probe = Quad("Mask backdrop probe", Origin + Vector3.down, new Vector3(100, 100, 1), probeMaterial);
        var front = Quad("Foreground preservation probe", Origin + new Vector3(0, 4, 9), new Vector3(1, 1, 1), frontMaterial);
        var cameraObject = new GameObject("Mask validation camera"); var testCamera = cameraObject.AddComponent<Camera>();
        testCamera.enabled = false; cameraObject.transform.SetPositionAndRotation(Origin + new Vector3(0, 50, 0), Quaternion.Euler(90, 0, 0));
        testCamera.orthographic = true; testCamera.orthographicSize = 12; testCamera.aspect = 1600f / 900;
        testCamera.clearFlags = CameraClearFlags.SolidColor; testCamera.backgroundColor = Color.green; testCamera.allowHDR = false;
        yield return null; yield return null;
        var pixels = Capture(testCamera, "projected-probe");
        Color32 At(float x, float z) => Pixel(testCamera, pixels, Origin + new Vector3(x, 0, z));
        Check(Magenta(At(0, 0)) && Magenta(At(13.5f, 5.5f)), "Projected field opening includes the front face and folds");
        Check(Magenta(At(17, 0)) && Magenta(At(-17, 0)), "Both rounded goal caps remain open");
        Check(Black(At(0, 7)) && Black(At(15, 6.5f)) && Black(At(-19, 5)), "Black mask seals top, bottom and curved outside corners");
        var foreground = At(0, 9);
        Check(foreground.g > 200 && foreground.b > 200 && foreground.r < 4, "Foreground presentation remains in front of the mask");
        IncludeGoals(mask, false); yield return null; yield return null;
        pixels = Capture(testCamera, "projected-no-goals-probe");
        Check(Black(At(17, 0)) && Black(At(-17, 0)) && Magenta(At(0, 0)) && Magenta(At(13.5f, 5.5f)),
            "Goal toggle off masks both half circles while retaining the folded field opening");
        IncludeGoals(mask, true); yield return null; yield return null;
        pixels = Capture(testCamera, "projected-goals-restored-probe");
        Check(Magenta(At(17, 0)) && Magenta(At(-17, 0)), "Goal toggle on restores both half circles");
        surface.ConfigureProjectedBounds(20, 16, .875f, 3, 2.1f); domains.Refresh(); bounds.RefreshNow(); yield return null; yield return null;
        pixels = Capture(testCamera, "resized-probe");
        Check(Magenta(At(0, 7)) && Black(At(19, 0)) && Magenta(At(15, 0)), "Changing folded surface dimensions resizes the full projected opening and goal caps");
        mask.enabled = false; yield return null; yield return null;
        pixels = Capture(testCamera, "disabled-probe");
        Check(Magenta(At(19, 0)), "Disabling the mask removes exterior coverage");
        mask.enabled = true; yield return null; yield return null;
        pixels = Capture(testCamera, "reenabled-probe");
        Check(Magenta(At(9, 7)) && Black(At(19, 0)), "Re-enabling regenerates the resized folded mask");
        Object.Destroy(staging); Object.Destroy(probe); Object.Destroy(front); Object.Destroy(cameraObject);
        Object.Destroy(probeMaterial); Object.Destroy(frontMaterial); yield return null; yield return null;
        Check(hashes.All(h => Hash128.Compute(File.ReadAllText(h.Key)) == h.Value), "All supported scenes, shared prefabs and COSMOS scene preserved during fixtures");
        Check(errors.Count == 0, "No runtime errors, exceptions or assertions");
    }
}
#endif
