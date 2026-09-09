using System;
using System.IO;
using System.Collections.Generic;
using Massive.Multiplier;
using UnityEditor;
using UnityEngine;

// Deterministic Unity camera renders, one frame per editor tick; no game-time changes.
public static class AmplifierTreatmentRecorder
{
    public static bool IsRecording { get; private set; }
    public static string Status { get; private set; } = "Idle";
    private static AmplifierGoalTreatments target;
    private static Camera camera;
    private static RenderTexture render;
    private static Texture2D texture;
    private static string folder, saved;
    private static int frame, frameCount;
    private static bool states;
    private static float clock;
    private static bool wasPreview, controls;
    private static int teamBefore, levelBefore, presetBefore;

    public static void Start(string directory, int preset, bool multiplierStates, bool matchView, int team = 1, AmplifierSeamMode seam = AmplifierSeamMode.Off)
    {
        if (IsRecording) throw new InvalidOperationException("A recording is already running.");
        if (Application.isPlaying) throw new InvalidOperationException("Record in edit mode to isolate the presentation clock.");
        target = UnityEngine.Object.FindFirstObjectByType<AmplifierGoalTreatments>();
        if (target == null || Camera.main == null) throw new InvalidOperationException("Dynamo treatment workbench and main camera required.");
        saved = JsonUtility.ToJson(target.Settings); clock = target.Clock;
        wasPreview = target.Preview; controls = target.ShowControls; teamBefore = target.PreviewTeam; levelBefore = target.HeldLevel;
        presetBefore = target.PresetIndex;
        folder = directory; Directory.CreateDirectory(folder); states = multiplierStates;
        target.ApplyPreset(preset); target.Preview = true; target.PreviewTeam = team;
        target.Settings.seamMode=seam;
        target.HeldLevel = 0; target.ShowControls = false;
        target.ResetPreview();
        var obj = new GameObject("Amplifier export camera") { hideFlags = HideFlags.HideAndDontSave };
        camera = obj.AddComponent<Camera>(); camera.CopyFrom(Camera.main); camera.enabled = false;
        camera.transform.SetPositionAndRotation(Camera.main.transform.position, Camera.main.transform.rotation);
        if (!matchView) { camera.orthographic = true; camera.orthographicSize = 3.9f; camera.transform.position = new Vector3(team == 1 ? -13.3f : 13.3f, 60, 0); }
        // Match Game-view coverage AA, including the score-void silhouette.
        render = new RenderTexture(1280, 720, 24) { hideFlags = HideFlags.HideAndDontSave, antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing) }; render.Create();
        camera.targetTexture = render; texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        frame = 0; frameCount = multiplierStates ? 300 : 210;
        IsRecording = true; Status = "Recording " + folder;
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        try
        {
            float time = frame / 30f;
            if (states) target.HeldLevel = Mathf.Min(3, frame / 75);
            // One second of baseline, then a capture at each new multiplier.
            bool capture = states ? frame == 75 || frame == 150 || frame == 225 : frame == 30;
            target.SetPreviewTime(time);
            if (capture) { if (!states) target.HeldLevel = 1; target.TriggerPreview(); }
            target.RenderTreatment();
            // Refresh the actual grid renderer's property block at this exact effect time.
            target.GetComponent<VectorGridGPU>().SendMessage("ApplyMaterialBindings", SendMessageOptions.RequireReceiver);
            RenderFrame(camera); var previous = RenderTexture.active; RenderTexture.active = render;
            texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply(); RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(folder, frame.ToString("D5") + ".png"), texture.EncodeToPNG());
            frame++; Status = frame + "/" + frameCount + " frames: " + folder;
            if (frame >= frameCount) Stop();
        }
        catch (Exception e) { Debug.LogException(e); Stop(); Status = "Failed: " + e.Message; }
    }
    internal static void RenderFrame(Camera captureCamera)
    {
        // Never change serialized Renderer.enabled, or leave gameplay hidden
        // between editor ticks. Suppression exists only during this camera render.
        var suppressed = new List<Renderer>();
        try
        {
            foreach (var core in UnityEngine.Object.FindObjectsByType<AmplifierCoreGameplay>(FindObjectsSortMode.None))
                if (!core.IsPresentationOnly)
                    foreach (var r in core.GetComponentsInChildren<Renderer>())
                        if (!r.forceRenderingOff) { suppressed.Add(r); r.forceRenderingOff=true; }
            captureCamera.Render();
        }
        finally
        {
            foreach (var r in suppressed) if (r!=null) r.forceRenderingOff=false;
        }
    }
    public static void Stop()
    {
        EditorApplication.update -= Tick;
        AssemblyReloadEvents.beforeAssemblyReload -= Stop;
        if (camera != null) UnityEngine.Object.DestroyImmediate(camera.gameObject);
        if (render != null) { render.Release(); UnityEngine.Object.DestroyImmediate(render); }
        if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        if (target != null && saved != null) { target.ApplyPreset(presetBefore); target.RestoreSettings(saved); target.Preview = wasPreview; target.ShowControls = controls; target.PreviewTeam = teamBefore; target.HeldLevel = levelBefore; target.ResetPreview(); target.SetPreviewTime(clock); }
        IsRecording = false; Status = "Complete: " + folder;
    }
}
