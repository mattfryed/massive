#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Massive.Player;
using UnityEditor;
using UnityEngine;

/// <summary>Records the actual three-input combo, then repeats at 45% speed. Play-mode only.</summary>
public sealed class PlayerRepulsorRecorder : MonoBehaviour
{
    public static string Status { get; private set; } = "Idle";
    public static string Evidence { get; private set; } = "";
    PlayerControllerScript player;
    PlayerAttackController attack;
    PlayerVisualController body;
    PlayerRepulsorAOE repulsor;
    Camera capture;
    RenderTexture target;
    Texture2D pixels;
    string folder;
    int oldCaptureRate;
    float oldTimeScale;
    Vector3 initialPosition;
    readonly List<PlayerControllerScript> roster = new List<PlayerControllerScript>();
    readonly List<PlayerControlMode> modes = new List<PlayerControlMode>();
    int nextComboIndex;
    bool takeStarted;
    float movementDemoAt = -1f;
    float minScale = 1f, maxScale = 1f, minMovement = 1f, maxRadius;
    readonly List<string> events = new List<string>();

    public static void StartRecording(string directory)
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        if (FindFirstObjectByType<PlayerRepulsorRecorder>()) throw new InvalidOperationException("Already recording.");
        var obj = new GameObject("Repulsor recording (temporary)") { hideFlags = HideFlags.DontSave };
        var recorder = obj.AddComponent<PlayerRepulsorRecorder>();
        recorder.folder = directory;
        recorder.StartCoroutine(recorder.Record());
    }

    IEnumerator Record()
    {
        foreach (var p in FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None))
        {
            if (!p.transform.parent || p.transform.parent.name != "Players") continue;
            roster.Add(p); modes.Add(p.ControlMode); p.SetControlMode(PlayerControlMode.Disabled);
            if (p.playerID == 0) player = p;
        }
        if (!player) { Status = "Failed: P1 not found"; Destroy(gameObject); yield break; }
        attack = player.GetComponent<PlayerAttackController>();
        body = player.GetComponentInChildren<PlayerVisualController>();
        repulsor = player.GetComponentInChildren<PlayerRepulsorAOE>();
        player.SetControlMode(PlayerControlMode.Scripted);
        initialPosition = player.transform.position;
        attack.OnStageStarted.AddListener(OnStage);
        oldCaptureRate = Time.captureFramerate; oldTimeScale = Time.timeScale;
        Time.captureFramerate = 60; Time.timeScale = 1f;
        Directory.CreateDirectory(folder);
        capture = new GameObject("Repulsor export camera") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Camera>();
        capture.CopyFrom(Camera.main); capture.enabled = false;
        capture.transform.SetPositionAndRotation(Camera.main.transform.position, Camera.main.transform.rotation);
        capture.orthographic = true; capture.orthographicSize = 2.45f;
        Vector3 center = initialPosition + Vector3.right * .65f;
        capture.transform.position = new Vector3(center.x, Camera.main.transform.position.y, center.z);
        target = new RenderTexture(1280, 720, 24) { antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing), hideFlags = HideFlags.HideAndDontSave };
        target.Create(); capture.targetTexture = target;
        pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var endOfFrame = new WaitForEndOfFrame();
        for (int frame = 0; frame < 600; frame++)
        {
            int localFrame = frame < 210 ? frame : frame - 210;
            if (localFrame == 0)
            {
                Time.timeScale = frame < 210 ? 1f : .45f;
                player.transform.position = initialPosition;
                var rb = player.GetComponent<Rigidbody>();
                if (rb) { rb.position = initialPosition; rb.linearVelocity = Vector3.zero; }
                nextComboIndex = 1; takeStarted = false; movementDemoAt = -1f;
            }
            PlayerInputFrame input = PlayerInputFrame.Neutral;
            // Establish a genuine right-stick heading before the first button press.
            if (localFrame < 9) input.moveInput = Vector2.right * .25f;
            if (localFrame == 30) { input.attackDown = true; takeStarted = true; }
            if (takeStarted && nextComboIndex < 3 && attack.IsAttacking && attack.CurrentStageIndex == nextComboIndex - 1 && attack.StageNormalizedTime >= .86f)
            { input.attackDown = true; nextComboIndex++; }
            if (takeStarted && nextComboIndex == 3 && !attack.IsAttacking)
            {
                if (movementDemoAt < 0f) movementDemoAt = Time.time;
                if (Time.time - movementDemoAt < .55f) input.moveInput = Vector2.right;
            }
            player.SetScriptedInput(input);
            yield return endOfFrame;
            minScale = Mathf.Min(minScale, body.RepulsorVisualScale);
            maxScale = Mathf.Max(maxScale, body.RepulsorVisualScale);
            minMovement = Mathf.Min(minMovement, player.ExternalMovementMultiplier);
            maxRadius = Mathf.Max(maxRadius, repulsor ? repulsor.RadiusWorld : 0f);
            capture.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(folder, frame.ToString("D5") + ".png"), pixels.EncodeToPNG());
            Status = "Recording " + (frame + 1) + "/600";
        }
        Evidence = string.Join("; ", events) + " | body scale " + minScale.ToString("F3") + ".." + maxScale.ToString("F3") +
            "; lowest movement " + minMovement.ToString("F3") + "; final movement " + player.ExternalMovementMultiplier.ToString("F3") +
            "; max hit radius " + maxRadius.ToString("F3") + "; final collider active " + (repulsor && repulsor.IsPulseActive);
        File.WriteAllText(Path.Combine(folder, "validation.txt"), Evidence);
        Status = "Complete: " + folder;
        Destroy(gameObject);
    }

    void OnStage(AttackStage stage) { events.Add(stage.StageName); }

    void OnDestroy()
    {
        if (attack) attack.OnStageStarted.RemoveListener(OnStage);
        Time.captureFramerate = oldCaptureRate; Time.timeScale = oldTimeScale > 0f ? oldTimeScale : 1f;
        for (int i = 0; i < roster.Count; i++) if (roster[i]) { roster[i].ClearScriptedInput(); roster[i].SetControlMode(modes[i]); }
        if (capture) DestroyImmediate(capture.gameObject);
        if (target) { target.Release(); DestroyImmediate(target); }
        if (pixels) DestroyImmediate(pixels);
    }
}
#endif
