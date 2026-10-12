#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Massive.Multiplier;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class AmplifierPossessionValidation
{
    const string ContestOutput = "C:/Users/mattf/.codex/visualizations/2026/09/07/01a07e08-a16a-7133-8c7e-374af73abfe7/Core-contest-demo";
    static Camera recordCamera;
    static RenderTexture recordTarget;
    static Texture2D recordPixels;
    static string recordFolder;
    static int recordFrame;
    static int lastRecordedUnityFrame = -1;
    static readonly List<string> recordEvents = new();

    static void CaptureContestFrame(Camera camera)
    {
        if (camera != recordCamera || !recordTarget || !recordPixels || lastRecordedUnityFrame == Time.frameCount) return;
        lastRecordedUnityFrame = Time.frameCount;
        var active = RenderTexture.active;
        try
        {
            // Read the normal completed render once. Rendering this camera twice
            // per frame can discard TMPro's transient batched geometry.
            RenderTexture.active = recordTarget;
            recordPixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); recordPixels.Apply();
            File.WriteAllBytes(recordFolder + "/frame-" + recordFrame.ToString("D5") + ".png", recordPixels.EncodeToPNG());
            recordFrame++;
        }
        finally { RenderTexture.active = active; }
    }

    static TMPro.TextMeshPro DemoLabel(Camera camera, Vector3 point, float size, string text)
    {
        var go = new GameObject("Contest demonstration caption", typeof(TMPro.TextMeshPro));
        go.transform.SetParent(scope.transform); go.transform.SetPositionAndRotation(point, camera.transform.rotation);
        var label = go.GetComponent<TMPro.TextMeshPro>(); label.fontSize = size;
        label.alignment = TMPro.TextAlignmentOptions.Center; label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        label.color = Color.white; label.text = text;
        return label;
    }

    static void DemoEvent(TMPro.TextMeshPro label, string text)
    {
        label.text = text;
        recordEvents.Add((recordFrame / 30f).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "s: " + text);
    }

    static IEnumerator DemoEnter(PlayerControllerScript player, int side)
    {
        Vector3 aim = Vector3.right * -side;
        player.SetScriptedInput(new PlayerInputFrame { moveInput = new Vector2(-side, 0), hasAimDirWS = true, aimDirWS = aim });
        yield return Wait(.28f);
        player.SetScriptedInput(new PlayerInputFrame { moveInput = new Vector2(-side, 0), attackDown = true,
            attackHeld = true, hasAimDirWS = true, aimDirWS = aim });
        yield return Wait(.1f);
        player.SetScriptedInput(new PlayerInputFrame { moveInput = new Vector2(-side, 0), attackUp = true,
            hasAimDirWS = true, aimDirWS = aim });
        yield return Until(() => player.IsInsideAmplifier, 2f, "Recorded player enters via movement, Thrust and physical contact");
        Input(player, aim: aim);
        yield return Until(() => !player.IsAmplifierTransitioning, .8f, "Recorded entry completes its tidal animation");
    }

    static IEnumerator ContestRecording(Camera camera)
    {
        float previousCapture = Time.captureDeltaTime;
        var previousTarget = camera.targetTexture;
        yield return ResetCore();
        core.Possession.fullChargeSeconds = 2f;
        Set(light.attackController.Profile.GetStage(0), "travelDistance", 1.4f);
        Set(dark.attackController.Profile.GetStage(0), "travelDistance", 1.4f);
        // Keep the canonical actor palettes: Light has a black body with white
        // particles; its white Core indicator is a separate possession symbol.
        PlacePlayer(light, Vector3.left * 2.7f); PlacePlayer(dark, Vector3.right * 2.7f);
        Input(light, aim: Vector3.right); Input(dark, aim: Vector3.left);
        camera.transform.position = Vector3.up * 20; camera.orthographicSize = 1.95f;
        DemoLabel(camera, Vector3.forward * 1.66f, 1f, "AMPLIFIER CORE  /  POSSESSION CONTEST");
        var label = DemoLabel(camera, Vector3.forward * 1.33f, 1.45f, "");
        DemoLabel(camera, Vector3.back * 1.78f, .8f, "LIGHT: WHITE    /    DARK: BLACK");
        LightPalette(camera, "20-recorded-light-resting");
        DarkPalette(camera, "20-recorded-dark-resting");
        try
        {
            Time.captureDeltaTime = 1f / 30f;
            recordFolder = ContestOutput + "/frames-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(recordFolder);
            SessionState.SetString(Key + ".RecordingFolder", recordFolder);
            recordFrame = 0; lastRecordedUnityFrame = -1; recordEvents.Clear();
            recordTarget = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            recordTarget.Create(); recordPixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            recordCamera = camera;
            camera.targetTexture = recordTarget;
            Camera.onPostRender += CaptureContestFrame;
            DemoEvent(label, "LIGHT APPROACHES"); yield return Wait(.6f);
            yield return DemoEnter(light, -1);
            DemoEvent(label, "LIGHT HAS POSSESSION"); yield return Wait(.65f);
            DemoEvent(label, "DARK CHALLENGES"); yield return DemoEnter(dark, 1);
            Check(core.ControlState == AmplifierControlState.Contested && core.Challenger == dark, "Recorded opposing entry opens a contest");
            CheckPossessionMarkers(true, 0);
            DemoEvent(label, "BOTH PLAYERS FIGHT FOR CONTROL");
            Input(light); Input(dark); yield return Wait(.25f);
            for (int i = 0; i < 10 && core.ControlState == AmplifierControlState.Contested; i++)
            {
                Input(dark, attack: i % 2 == 0, shield: i % 2 != 0, aim: Vector3.left);
                yield return Wait(.12f); Input(dark, aim: Vector3.left); yield return Wait(.07f);
                if (i == 0 || i == 2)
                {
                    Input(light, attack: i == 0, shield: i == 2);
                    yield return Wait(.07f); Input(light); yield return Wait(.07f);
                }
            }
            Check(core.ControlState == AmplifierControlState.Aiming && core.Occupant == dark,
                "Dark wins the recorded encounter through successful alternating inputs, without forcing game state");
            DemoEvent(label, "DARK WINS  /  LIGHT IS EJECTED");
            Input(light); Input(dark, aim: Vector3.left);
            yield return Wait(.45f);
            CheckPossessionMarkers(false, 2);
            Check(!light.IsInsideAmplifier && light.IsExternallyStunned, "Losing player visibly exits and receives the normal stun");
            yield return Wait(.95f);
            Check(recordFrame > 150, "Recorded a continuous close-up of both entries, contest and victory");
            File.WriteAllText(ContestOutput + "/recording.txt", "1280x720 / 30 fps / " + recordFrame + " frames\n" +
                "Scripted player inputs using normal gameplay contact and contest rules. No winner or progress override.\n" +
                string.Join("\n", recordEvents) + "\nFrames: " + recordFolder);
        }
        finally
        {
            Camera.onPostRender -= CaptureContestFrame;
            camera.targetTexture = previousTarget;
            recordCamera = null;
            if (recordTarget) { recordTarget.Release(); Object.DestroyImmediate(recordTarget); }
            if (recordPixels) Object.DestroyImmediate(recordPixels);
            recordTarget = null; recordPixels = null;
            Time.captureDeltaTime = previousCapture;
        }
        LightPalette(camera, "21-recorded-light-ejected");
    }
}
#endif
