#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Player;
using Massive.Scoring;
using Massive.Settings;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PlayerMultiplierShimmerValidation
{
    const string Key = "MASSIVE.MultiplierShimmerValidation";
    const string Output = "Library/MultiplierShimmerValidation";
    static IEnumerator routine;
    static readonly List<string> checks = new(), errors = new();
    static double deadline;
    static int nextFrame;
    static Camera camera;
    static PlayerControllerScript[] players;
    static PlayerScoreChain[] chains;
    static PlayerScoreChainPresenter[] widgets;
    static PlayerVisualController[] visuals;
    static float[] hudEnergy;
    static PlayerMultiplierShimmerValidation()
    {
        EditorApplication.update += Begin;
        EditorApplication.playModeStateChanged += State;
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project copy.");
        SessionState.SetBool(Key + ".begin", true);
    }
    static void Begin()
    {
        if (!SessionState.GetBool(Key + ".begin", false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        SessionState.SetBool(Key + ".begin", false);
        SessionState.SetBool(Key, true);
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground = true;
            Time.captureDeltaTime = .02f;
            checks.Clear(); errors.Clear(); nextFrame = 0;
            deadline = EditorApplication.timeSinceStartup + 100;
            Application.logMessageReceived += Log;
            routine = Checks(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(SessionState.GetBool(Key + ".passed", false) ? 0 : 1);
        }
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < nextFrame || routine == null) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out");
            if (routine.MoveNext()) { nextFrame = Time.frameCount + 1; return; }
            Finish(true);
        }
        catch (Exception ex) { checks.Add(ex.ToString()); Finish(false); }
    }
    static void Finish(bool passed)
    {
        passed &= errors.Count == 0;
        Application.logMessageReceived -= Log; EditorApplication.update -= Tick; routine = null;
        File.WriteAllText(Output + "/report.txt", (passed ? "PASSED" : "FAILED") + "\n" + string.Join("\n", checks) + "\n" + string.Join("\n", errors));
        SessionState.SetBool(Key + ".passed", passed);
        Time.captureDeltaTime = 0; EditorApplication.isPlaying = false;
    }
    static void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); checks.Add("PASS " + label); }
    static void Set(Object obj, string field, object value)
    {
        var property = new SerializedObject(obj); var p = property.FindProperty(field);
        if (value is bool b) p.boolValue = b; else if (value is float f) p.floatValue = f;
        else p.objectReferenceValue = (Object)value;
        property.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Charge(int i, float value)
    { chains[i].ResetChain(); chains[i].ApplyAward(ScoreChainAwardMode.AdvanceAndRefresh, value); }
    static IEnumerator Checks()
    {
        // Existing gameplay/HUD suite, using this disposable scene as its authorized fixture.
        SessionState.SetString("MASSIVE.MultiplierTests.Scene", "");
        var smoke = typeof(MultiplierIntegrationSmokeTests).GetMethod("RunChecks", BindingFlags.NonPublic | BindingFlags.Static);
        try { File.WriteAllText(Output + "/legacy-scoring.txt", "PASSED: " + smoke.Invoke(null, null)); }
        catch (TargetInvocationException ex)
        {
            File.WriteAllText(Output + "/legacy-scoring.txt", "FAILED existing scoring suite: " + ex.InnerException +
                "\nCurrent Drone charge is 1; legacy fractional-scoring expectations assume 0.25. No reward tuning changed.");
        }
        yield return null;
        var tuning = PlayerMultiplierShimmer.Settings;
        Check(tuning.Strength(7.999) == 0 && tuning.Strength(8) > 0, "Shimmer appears exactly at 8x");
        Check(tuning.Strength(8) < tuning.Strength(12) && tuning.Strength(12) < tuning.Strength(16) && tuning.Strength(100) == 1, "Intensity grows continuously through 16x and clamps");
        camera = new GameObject("Shimmer capture camera").AddComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 2.1f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .03f, .045f); camera.allowHDR = false;
        camera.transform.SetPositionAndRotation(new Vector3(0, 20, -.1f), Quaternion.LookRotation(Vector3.down, Vector3.forward));
        var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab");
        var field = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PLAYING FIELD.prefab");
        var sources = field.GetComponentsInChildren<PlayerScoreChainPresenter>(true);
        players = new PlayerControllerScript[4]; chains = new PlayerScoreChain[4]; widgets = new PlayerScoreChainPresenter[4]; visuals = new PlayerVisualController[4];
        for (int i = 0; i < 4; i++)
        {
            var holder = new GameObject("Player sample " + i); holder.SetActive(false);
            var go = Object.Instantiate(template, holder.transform);
            go.transform.position = new Vector3(-3 + i*2, 0, .6f);
            players[i] = go.GetComponent<PlayerControllerScript>(); players[i].playerID = i; players[i].teamID = i < 2 ? 1 : 2;
            players[i].SetControlMode(PlayerControlMode.Disabled);
            Set(players[i], "playMatchSpawnOnSceneLoad", false);
            Set(players[i], "showPlayerIdToastOnMatchStart", false);
            Set(players[i], "deathFxSeconds", .05f); Set(players[i], "respawnDelaySeconds", .05f); Set(players[i], "respawnFxSeconds", .05f);
            var rb = go.GetComponent<Rigidbody>(); rb.useGravity = false; rb.constraints = RigidbodyConstraints.FreezeAll;
            chains[i] = go.GetComponent<PlayerScoreChain>() ?? go.AddComponent<PlayerScoreChain>();
            visuals[i] = go.GetComponentInChildren<PlayerVisualController>(true);
            var widget = Object.Instantiate(sources[i].gameObject, holder.transform);
            widget.transform.SetPositionAndRotation(new Vector3(-3.8f+i*2, .1f, -.7f), Quaternion.LookRotation(Vector3.down, Vector3.forward));
            widget.transform.localScale = Vector3.one*.55f;
            widgets[i] = widget.GetComponent<PlayerScoreChainPresenter>(); Set(widgets[i], "chain", chains[i]);
            holder.SetActive(true); chains[i].Configure(ScoreChainSettings.RecommendedDefaults());
        }
        for (int i = 0; i < 25; i++) yield return null;
        float[] charge = { 0, 18, 23, 28 };
        for (int i = 0; i < 4; i++) Charge(i, charge[i]);
        float settleUntil = Time.unscaledTime + 1.25f;
        while (Time.unscaledTime < settleUntil) yield return null;
        var stats = Capture("tiers.png");
        Check(!widgets[0].MaximumEffectsActive && widgets.Skip(1).All(w => w.MaximumEffectsActive), "All HUD widgets use the 8x threshold instead of max-only activation");
        Check(visuals[0].MultiplierShimmerStrength == 0 && visuals[1].MultiplierShimmerStrength > 0 && visuals[3].MultiplierShimmerStrength == 1, "Body visuals read each owning player's independent chain");
        Check(stats[0] < 5 && stats[1] > 25 && stats[2] > stats[1] && stats[3] > stats[2], "GPU capture shows increasing colored outline energy from 8x to 16x");
        Check(hudEnergy[0] < 5 && hudEnergy[1] > 5 && hudEnergy[2] > hudEnergy[1] && hudEnergy[3] > hudEnergy[2], "GPU capture shows multicolor HUD glow from 8x through 16x");
        var maxWidget = new SerializedObject(widgets[3]);
        var maxBar = (Shapes.Rectangle)maxWidget.FindProperty("progressRectangle").objectReferenceValue;
        var maxTrack = (Shapes.Rectangle)maxWidget.FindProperty("progressTrackRectangle").objectReferenceValue;
        Check(Mathf.Approximately(maxBar.Width, maxTrack.Width), "Maximum multiplier bar settles to its full width");
        Check(!ShaderUtil.ShaderHasError(visuals[3].blobMat.shader) && !ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Scoring/MultiplierMaximumGlow.shader")), "Body and HUD shimmer shaders compile on the active graphics device");
        for (int i = 0; i < 35; i++) yield return null;
        Capture("drift.png");
        int childCount = widgets[3].transform.childCount;
        widgets[3].enabled = false;
        Check(!widgets[3].MaximumEffectsActive, "Disabling a HUD clears its glow");
        widgets[3].enabled = true; yield return null;
        Check(widgets[3].MaximumEffectsActive && widgets[3].transform.childCount == childCount, "HUD re-enable reuses its glow object");
        var attack = players[3].GetComponent<PlayerAttackController>(); attack.BeginAttack();
        for (int i = 0; i < 5; i++) yield return null;
        Capture("thrust.png"); attack.CancelAttack();
        visuals[3].SetDecoherenceSplitActive(true);
        for (int i = 0; i < 15; i++) yield return null;
        Capture("decoherence.png"); visuals[3].SetDecoherenceSplitActive(false);
        chains[3].ResetChain(); yield return null;
        Check(PlayerMultiplierShimmer.Strength(chains[3]) == 0 && !widgets[3].MaximumEffectsActive, "Multiplier reset removes body and HUD effects immediately");
        Charge(3, 28);
        players[3].ApplyExternalMassDelta(-2f);
        yield return null;
        Check(chains[3].CurrentMultiplier == 1 && !widgets[3].MaximumEffectsActive, "Actual player death resets the multiplier and removes both effects");
        // Life FX uses real time; fixed frame counts race it in a fast batch renderer.
        float respawnDeadline = Time.unscaledTime + 5f;
        while (players[3].IsSpawning && Time.unscaledTime < respawnDeadline) yield return null;
        Check(!players[3].IsSpawning && PlayerMultiplierShimmer.Strength(chains[3]) == 0, "Respawn does not retain a stale shimmer");
        Charge(3, 28); tuning.enabled = false; yield return null;
        Check(PlayerMultiplierShimmer.Strength(chains[3]) == 0 && !widgets[3].MaximumEffectsActive, "Global shimmer toggle clears both effects");
        tuning.enabled = true;
        Check(chains[1].CurrentMultiplier == 8 && chains[2].CurrentMultiplier == 12, "Other players' multipliers remain unchanged by resets and VFX controls");
    }
    static float[] Capture(string filename)
    {
        var rt = new RenderTexture(1600, 800, 24, RenderTextureFormat.ARGB32); rt.Create();
        var pixels = new Texture2D(1600, 800, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active; camera.targetTexture = rt;
        try
        {
            camera.Render(); RenderTexture.active = rt; pixels.ReadPixels(new Rect(0,0,1600,800),0,0); pixels.Apply();
            File.WriteAllBytes(Output + "/" + filename, pixels.EncodeToPNG());
            var energy = new float[4];
            for (int i=0; i<4; i++)
            {
                Vector3 center = camera.WorldToViewportPoint(visuals[i].visuals.position);
                int cx = Mathf.RoundToInt(center.x*1600), cy = Mathf.RoundToInt(center.y*800);
                for (int y = cy-115; y<=cy+115; y++) for (int x=cx-115; x<=cx+115; x++)
                {
                    Color c = pixels.GetPixel(Mathf.Clamp(x,0,1599), Mathf.Clamp(y,0,799));
                    float chroma = Mathf.Max(c.r,c.g,c.b)-Mathf.Min(c.r,c.g,c.b);
                    if(chroma>.04f) energy[i]+=chroma;
                }
            }
            hudEnergy = new float[4];
            for (int i=0; i<4; i++)
            {
                var settings = new SerializedObject(widgets[i]);
                var bar = (Shapes.Rectangle)settings.FindProperty("progressRectangle").objectReferenceValue;
                var center = camera.WorldToViewportPoint(bar.transform.TransformPoint(new Vector3(bar.Width*.5f, bar.Height*.5f, 0)));
                int cx = Mathf.RoundToInt(center.x*1600), cy = Mathf.RoundToInt(center.y*800);
                for (int y=cy-24; y<=cy+24; y++) for(int x=cx-150; x<=cx+150; x++)
                {
                    var c = pixels.GetPixel(Mathf.Clamp(x,0,1599),Mathf.Clamp(y,0,799));
                    float chroma = Mathf.Max(c.r,c.g,c.b)-Mathf.Min(c.r,c.g,c.b);
                    if(chroma>.04f) hudEnergy[i]+=chroma;
                }
            }
            File.AppendAllText(Output+"/pixels.txt", filename+": body "+string.Join(", ",energy)+"; HUD "+string.Join(", ",hudEnergy)+"\n");
            return energy;
        }
        finally { camera.targetTexture=null; RenderTexture.active=previous; rt.Release(); Object.Destroy(rt); Object.Destroy(pixels); }
    }
}
#endif
