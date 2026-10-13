#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Massive.Player;
using Massive.PowerUps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real player rendering, curved motion history and ability cleanup in a disposable scene.</summary>
[InitializeOnLoad]
public static class PowerUpEquippedVisualValidation
{
    const string Key = "MASSIVE.PowerUpEquippedVisualValidation";
    const string Folder = "Library/PowerUpEquippedVisualValidation";
    static readonly List<string> checks = new(), errors = new();
    static readonly List<Object> owned = new();
    static IEnumerator routine;
    static int frame;
    static double deadline;
    static bool runInBackground;
    static PowerUpEquippedVisualValidation() => EditorApplication.playModeStateChanged += State;

    [MenuItem("MASSIVE/Power-ups/Validate Equipped Appearance")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run in Edit Mode after compilation.");
        var original = SceneManager.GetActiveScene();
        string path = "Assets/PowerUpEquippedVisualValidation-" + Guid.NewGuid().ToString("N") + ".unity";
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try { if (!EditorSceneManager.SaveScene(fixture, path)) throw new Exception("Could not save fixture."); }
        finally { EditorSceneManager.CloseScene(fixture, true); SceneManager.SetActiveScene(original); }
        SessionState.SetString(Key + ".Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + ".Scene", path); SessionState.SetBool(Key, true);
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/report.txt", "STARTING");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); frame = -1; deadline = EditorApplication.timeSinceStartup + 120;
            runInBackground = Application.runInBackground; Application.runInBackground = true;
            routine = Expand(Checks()); Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            (routine as IDisposable)?.Dispose(); routine = null;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + ".Previous", ""));
            string path = SessionState.GetString(Key + ".Scene", "");
            if (path.StartsWith("Assets/PowerUpEquippedVisualValidation-", StringComparison.Ordinal) && path.EndsWith(".unity", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(path);
            SessionState.SetBool(Key, false);
        }
    }
    static void Log(string text, string stack, LogType type)
    { if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(text + "\n" + stack); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out.");
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; (routine as IDisposable)?.Dispose(); routine = null;
        foreach (var asset in owned) if (asset) Object.DestroyImmediate(asset);
        owned.Clear(); Application.runInBackground = runInBackground;
        Application.logMessageReceived -= Log;
        bool passed = error == null && errors.Count == 0;
        string report = (passed ? "PASSED" : "FAILED") + " (" + checks.Count + " checks)\n" + string.Join("\n", checks) +
            "\n" + error + "\n" + string.Join("\n", errors);
        File.WriteAllText(Folder + "/report.txt", report);
        Debug.Log("EQUIPPED POWER-UP VISUALS " + report);
        EditorApplication.isPlaying = false;
    }
    static IEnumerator Expand(IEnumerator root)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(root);
        try
        {
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { (top as IDisposable)?.Dispose(); stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return top.Current;
            }
        }
        finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
    }
    static void Check(bool pass, string label)
    { if (!pass) throw new Exception(label); checks.Add("PASS " + label); }
    static IEnumerator Wait(float seconds)
    { float end = Time.time + seconds; while (Time.time < end) yield return null; }
    static void Place(PlayerControllerScript player, Vector3 position)
    { player.GetComponent<Rigidbody>().position = position; player.transform.position = position; Physics.SyncTransforms(); }
    static Texture2D Pixels(Camera camera, string name = null)
    {
        var rt = new RenderTexture(800, 800, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
        var pixels = new Texture2D(800, 800, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            pixels.ReadPixels(new Rect(0, 0, 800, 800), 0, 0); pixels.Apply();
            if (name != null) File.WriteAllBytes(Folder + "/" + name + ".png", pixels.EncodeToPNG());
            return pixels;
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; rt.Release(); Object.DestroyImmediate(rt); }
    }
    static void AppearanceDifference(Camera camera, Action<bool> toggle, string name)
    {
        toggle(false); var before = Pixels(camera);
        toggle(true); var after = Pixels(camera, name);
        int changed = 0;
        try
        {
            var a = before.GetPixels32(); var b = after.GetPixels32();
            for (int i = 0; i < a.Length; i++) if (Mathf.Abs(a[i].r - b[i].r) > 8) changed++;
            Check(changed > 40, name + " changes visible pixels on the real player (" + changed + ")");
        }
        finally { Object.DestroyImmediate(before); Object.DestroyImmediate(after); }
    }

    static IEnumerator Checks()
    {
        Check(SceneManager.GetActiveScene().path == SessionState.GetString(Key + ".Scene", ""), "Disposable scene protects authored gameplay scenes");
        var shader = Shader.Find("MASSIVE/PlayerBlobVector");
        Check(shader && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "Player shader compiles on the active graphics device");
        var camera = new GameObject("Appearance camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 1.7f; camera.aspect = 1;
        camera.transform.SetPositionAndRotation(Vector3.up * 20, Quaternion.Euler(90, 0, 0));
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var root = new GameObject("Appearance fixtures"); root.SetActive(false);
        var players = new PlayerControllerScript[2];
        for (int i = 0; i < 2; i++)
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), root.transform);
            players[i] = go.GetComponent<PlayerControllerScript>(); players[i].ConfigureDemonstration(root.transform, i, i + 1);
        }
        var deco = Object.Instantiate(AssetDatabase.LoadAssetAtPath<DecoherencePowerUpDefinition>("Assets/Power-ups/Decoherence/PU_Decoherence.asset")); owned.Add(deco);
        var time = Object.Instantiate(AssetDatabase.LoadAssetAtPath<TimeDilationPowerUpDefinition>("Assets/Power-ups/Time Dilation/PU_TimeDilation.asset")); owned.Add(time);
        root.SetActive(true); yield return null; yield return null;
        // Keep the controller's normal velocity writes valid while the fixture positions the bodies.
        foreach (var p in players) { p.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll; Place(p, Vector3.left * 20); }
        for (int team = 0; team < 2; team++)
        {
            var player = players[team]; var other = players[1 - team];
            var visual = player.visualsController; var host = player.GetComponent<PlayerPowerUpController>();
            Place(player, Vector3.zero); Place(other, Vector3.left * 20);
            camera.transform.position = Vector3.up * 20; camera.orthographicSize = 1.7f;
            player.SetScriptedInput(new PlayerInputFrame { hasAimDirWS = true, aimDirWS = Vector3.right });
            host.Equip(deco); host.OverrideRemainingSeconds(60); yield return Wait(.3f);
            Check(visual.DecoherenceContoursVisible && visual.DecoSplit01 < .001f, "Team " + team + " has idle contours without activating its shield");
            AppearanceDifference(camera, v => deco.showEquippedContours = v, "deco-idle-team-" + team);
            var shield = player.GetComponent<PlayerShieldAbility>();
            player.SetScriptedInput(new PlayerInputFrame { shieldDown = true, shieldHeld = true }); yield return Wait(.22f);
            Check(visual.DecoSplit01 > .9f, "Shield input expands into the existing Decoherence split");
            float normalSeparation = visual.DecoSplitSeparationWorld;
            Check(host.TryHandleShieldImpact(other, player.shield.GetComponent<Collider>(), Vector3.right), "Decoherence still handles a shield impact");
            Check(visual.DecoSplitSeparationWorld > normalSeparation && other.IsExternallyStunned, "Successful phase adds separation feedback and retains attacker stun");
            Object.DestroyImmediate(Pixels(camera, "deco-phase-team-" + team));
            yield return Wait(.4f);
            Check(Mathf.Abs(visual.DecoSplitSeparationWorld - normalSeparation) < .001f, "Phase separation pulse settles");
            Place(other, Vector3.left * 20);
            shield.ForceStopShield(); player.ClearScriptedInput(); yield return Wait(.15f);
            Check(visual.DecoSplit01 < .001f && visual.DecoherenceContoursVisible, "Releasing shield restores the crisp central body and equipped contours");

            host.Equip(time); host.OverrideRemainingSeconds(60); yield return Wait(.25f);
            Check(!visual.DecoherenceContoursVisible && visual.TimeDilationMarksVisible && Mathf.Approximately(host.MovementMultiplier, time.moveSpeedMultiplier),
                "Replacing Decoherence switches appearance and retains the Time Dilation speed multiplier");
            Check(!visual.TryGetTimeDilationEcho(0, out _, out _, out _), "Standing still produces no piled-up ghost silhouettes");
            AppearanceDifference(camera, v => time.showPerimeterMarks = v, "time-idle-team-" + team);
            float start = Time.time;
            var positions = new List<Vector3>();
            while (Time.time - start < 1f)
            {
                float angle = (Time.time - start) * 1.8f;
                Place(player, new Vector3(Mathf.Sin(angle) * 3, 0, (1 - Mathf.Cos(angle)) * 3));
                positions.Add(player.transform.position);
                player.SetScriptedInput(new PlayerInputFrame { hasAimDirWS = true, aimDirWS = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) });
                yield return null;
            }
            int echoes = 0; float priorOpacity = 2f;
            for (int i = 0; i < 3; i++)
            {
                if (!visual.TryGetTimeDilationEcho(i, out var point, out _, out float opacity)) continue;
                echoes++; float nearest = float.PositiveInfinity;
                foreach (var position in positions) nearest = Mathf.Min(nearest, Vector3.Distance(point, position));
                Check(nearest < .2f && opacity < priorOpacity, "Echo " + i + " follows the recorded curved path and fades with age");
                priorOpacity = opacity;
            }
            Check(echoes == 3, "Continuous movement creates exactly three readable historical silhouettes");
            camera.transform.position = player.transform.position + Vector3.up * 20;
            AppearanceDifference(camera, v => time.showMotionEchoes = v, "time-moving-team-" + team);
            yield return Wait(.7f);
            Check(!visual.TryGetTimeDilationEcho(0, out _, out _, out _) && visual.TimeDilationMarksVisible, "Stopping fades the echoes and keeps the idle cue");
            Place(player, Vector3.right * 40); yield return null; yield return null;
            Check(!visual.TryGetTimeDilationEcho(0, out _, out _, out _), "Teleport clears old path history");
            visual.enabled = false; yield return null;
            Check(!visual.TimeDilationMarksVisible && !visual.TimeDilationEchoesVisible, "Hiding the player hides its equipped effects");
            visual.enabled = true; yield return null; yield return null;
            Check(!visual.TryGetTimeDilationEcho(0, out _, out _, out _) && visual.TimeDilationMarksVisible, "Re-enabling resumes the cue without stale silhouettes");
            player.temporarilyEliminated = true; yield return null; yield return null;
            Check(!visual.TimeDilationMarksVisible && !visual.TimeDilationEchoesVisible, "Death state suppresses all new effects immediately");
            player.temporarilyEliminated = false;
            host.OverrideRemainingSeconds(.01f); yield return Wait(.1f);
            Check(!host.HasActive && !visual.TimeDilationMarksVisible && !visual.TimeDilationEchoesVisible && Mathf.Approximately(host.MovementMultiplier, 1f),
                "Expiry removes the visuals and restores normal movement");
            host.Equip(deco); host.Clear();
            Check(!visual.DecoherenceContoursVisible && Mathf.Approximately(visual.DecoherencePhaseScale, 1f), "Clearing Decoherence removes its idle cue and phase pulse");
        }
        Check(errors.Count == 0, "No runtime errors during equipment, motion, phase, replacement and cleanup");
    }
}
#endif
