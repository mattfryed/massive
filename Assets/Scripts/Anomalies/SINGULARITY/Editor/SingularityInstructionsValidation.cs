#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Massive.Singularity.Editor
{
    /// <summary>Opt-in, bounded check in the real instructions scene. Never saves runtime state.</summary>
    public static class SingularityInstructionsValidation
    {
        public static string Result { get; private set; } = "Not run";
        private static SingularityInstructionsDemo demo;
        private static readonly HashSet<string> captures = new HashSet<string>();
        private static string output;
        private static double began;
        private static int life, stage, resetFrame;
        private static int initialPasses, initialTransfers;
        private static float mass;
        private static bool sawFront, sawRear, sawTidal, sawRipple;

        public static string ValidatePrefab()
        {
            var panel = AssetDatabase.LoadAssetAtPath<GameObject>(SingularityInstructionsSetup.PanelPath);
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/Anomalies/ORBITAL/InstructionsPanel-ORBITAL.prefab");
            var icon = panel.GetComponentsInChildren<LevelIcon>(true).Single();
            var referenceIcon = reference.GetComponentsInChildren<LevelIcon>(true).Single();
            Check(icon.transform.localPosition == referenceIcon.transform.localPosition, "standard center icon position");
            Check(icon.transform.localScale == referenceIcon.transform.localScale, "standard icon size");
            var preview = panel.GetComponentInChildren<SingularityInstructionsDemo>(true);
            Check(preview && preview.portal && preview.grid && preview.surface && preview.feedback, "complete demonstration references");
            Check(preview.panelFrame == panel.transform.Find("Mid panel"), "middle-right frame assigned");
            Check(preview.portal.surface == preview.surface && preview.grid.blackHole == preview.portal, "local portal and grid wiring");
            Check(preview.feedback.portal == preview.portal && preview.feedback.grid == preview.grid, "local feedback wiring");
            Check(preview.feedback.renderCamera == preview.previewCamera && preview.previewShader, "isolated preview rendering");
            Check(preview.portal.players.Length == 0 && !preview.feedback.enableHaptics, "no match players or controller rumble");
            Check(AssetDatabase.GetAssetPath(preview.playerPrefab) == "Assets/Prefabs/PlayerActor.prefab", "canonical player prefab");
            Check(preview.GetComponentsInChildren<Collider>(true).Length == 0, "no black-hole damage/physics colliders");
            Check(preview.GetComponentsInChildren<AttractScript>(true).Length == 0, "no legacy attraction script");
            Check(panel.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "no missing scripts");
            return "PASS: 12 instructions prefab checks";
        }

        public static string Begin(string captureDirectory)
        {
            if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != SceneFlow.InstructionsScene)
                throw new InvalidOperationException("Run in Play Mode in SINGULARITY's real instructions scene.");
            EditorApplication.update -= Tick;
            demo = Object.FindFirstObjectByType<SingularityInstructionsDemo>();
            Check(demo && demo.Player, "live preview actor");
            ValidatePrefab();
            output = captureDirectory;
            Directory.CreateDirectory(output);
            captures.Clear(); stage = 0; sawFront = sawRear = sawTidal = sawRipple = false;
            mass = demo.Player.massScore; life = demo.Player.LifeSequence;
            initialPasses = demo.CompletedPasses; initialTransfers = demo.FaceTransfers;
            began = EditorApplication.timeSinceStartup;
            Result = "Running: two directions, repeat, cleanup and restart";
            EditorApplication.update += Tick;
            return Result;
        }

        private static void Tick()
        {
            try
            {
                Check(Application.isPlaying && demo, "instructions remain active");
                Check(EditorApplication.timeSinceStartup - began < 80, "80-second preview deadline");
                if (stage == 1)
                {
                    if (Time.frameCount <= resetFrame + 2) return;
                    Check(!demo.Player && !demo.Actor && !demo.grid.enabled && !demo.portal.enabled, "disable releases actor and transit");
                    Check(!demo.previewCamera.enabled && !demo.previewCamera.targetTexture, "disable releases preview camera and render texture");
                    Check(!demo.panelFrame.Find("Black-hole gameplay panel (owned)"), "disable releases panel image");
                    Check(demo.GetComponentsInChildren<PlayerControllerScript>(true).Length == 0, "destroyed actor cleanup");
                    demo.enabled = true;
                    resetFrame = Time.frameCount; stage = 2;
                    return;
                }
                if (stage == 2)
                {
                    if (Time.frameCount <= resetFrame + 2) return;
                    mass = demo.Player.massScore; life = demo.Player.LifeSequence;
                    stage = 3;
                }
                Check(demo.Player && demo.Player.IsPseudoPlayer && !demo.Player.ParticipatesInMatch, "non-scoring demo player");
                Check(demo.Player.SimulationRoot && demo.Player.SimulationRoot.IsChildOf(demo.transform), "isolated simulation scope");
                Check(demo.Player.LifeSequence == life && Mathf.Abs(demo.Player.massScore - mass) < .0001f, "nonlethal passage");
                Check(demo.GetComponentsInChildren<PlayerControllerScript>(true).Length == 1, "one owned actor");
                var camera = demo.previewCamera;
                Check(camera.enabled && camera.targetTexture && camera.rect == new Rect(0,0,1,1), "isolated panel camera target");
                Check(camera.depth < Camera.main.depth, "preview rendered before instructions UI");
                Check(demo.feedback.HapticCommandsSent == 0, "no controller rumble");
                sawFront |= demo.Actor.RearWeight < .01f;
                sawRear |= demo.Actor.RearWeight > .99f;
                sawTidal |= demo.Actor.PortalVisualScale < .8f && demo.Actor.PortalVisualStretch > 1.3f;
                sawRipple |= demo.grid.ActiveRippleCount > 0 && demo.feedback.ActiveBurstCount > 0;
                string sample = null;
                if (demo.Phase == "Entering" && demo.Actor.PortalVisualStretch > 1.6f) sample = "tidal-entry";
                else if (demo.Phase == "Exiting to rear") sample = "rear-exit";
                else if (demo.Phase == "Exiting to front") sample = "front-exit";
                else if (demo.Phase == "Front approach" && Mathf.Abs(demo.Actor.SurfacePosition.x) < 3f) sample = "instructions-layout";
                if (sample != null && captures.Add(sample)) CaptureGameView(Path.Combine(output, sample + ".png"));
                if (stage == 0 && demo.CompletedPasses - initialPasses >= 3 && demo.FaceTransfers - initialTransfers >= 3)
                {
                    Check(sawFront && sawRear, "repeated two-way transfers");
                    Check(sawTidal && sawRipple, "actual tidal deformation and transfer feedback");
                    demo.enabled = false; resetFrame = Time.frameCount; stage = 1;
                }
                else if (stage == 3 && demo.CompletedPasses >= 1)
                {
                    Finish("PASS: prefab layout/wiring; three alternating nonlethal passages; tidal entry, rear dimming and ripples; disable cleanup; fresh actor and complete passage after re-enable.");
                }
            }
            catch (Exception e) { Finish("FAIL: " + e.Message); }
        }

        private static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); }

        private static void CaptureGameView(string path)
        {
            // Read the actual Game view instead of a screenshot re-render, without
            // asking cameras to draw again or changing their targets/viewport.
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            var view = Resources.FindObjectsOfTypeAll(type).FirstOrDefault();
            var field = type.GetField("m_RenderTexture", BindingFlags.Instance | BindingFlags.NonPublic);
            var target = view != null ? field?.GetValue(view) as RenderTexture : null;
            Check(target, "rendered Game view available for visual capture");
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                // The built-in Game view target is vertically inverted on Direct3D.
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    var pixels = image.GetPixels32();
                    int width = target.width, height = target.height;
                    for (int y = 0; y < height / 2; y++)
                        for (int x = 0; x < width; x++)
                        {
                            int a = y * width + x, b = (height - 1 - y) * width + x;
                            var pixel = pixels[a]; pixels[a] = pixels[b]; pixels[b] = pixel;
                        }
                    image.SetPixels32(pixels);
                }
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }

        private static void Finish(string message)
        {
            EditorApplication.update -= Tick;
            Result = message;
            Debug.Log("[SINGULARITY instructions validation] " + message);
        }
    }
}
#endif
