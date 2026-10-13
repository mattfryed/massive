using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Multiplier;
using Massive.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.EditorTools
{
    public static class AmplifierCorePreviewValidation
    {
        public static string Run(string output)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
            var originalScene = SceneManager.GetActiveScene();
            var source = SharedSettingsRuntime.Load<AmplifierSharedProfile>();
            string originalSettings = JsonUtility.ToJson(source);
            var copy = Object.Instantiate(source);
            var checks = new List<string>();
            void Check(bool pass, string label) { if (!pass) throw new Exception(label); checks.Add("PASS " + label); }
            Directory.CreateDirectory(output);
            // A clean, unsaved authoring scene reveals accidental preview leakage or dirtying.
            var clean = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(clean);
            int previewCount = EditorSceneManager.previewSceneCount;
            try
            {
                using (var v = new AmplifierCoreInteractionPreview(copy))
                {
                    Check(v.IsIsolated, "Preview has its own physics world");
                    Check(v.CoreObject.scene != clean && EditorSceneManager.IsPreviewScene(v.CoreObject.scene), "Real Core clone belongs to a preview scene");
                    Check(!AmplifierCoreGameplay.ActiveCores.Contains(v.CoreObject.GetComponent<AmplifierCoreGameplay>()), "Preview never registers with gameplay");
                    Check(!clean.isDirty && clean.rootCount == 0, "Creating preview leaves a clean authoring scene untouched");
                    v.SetArena(60f, 20f, false); v.AimAngle = 0f;
                    // Establish a fixture independent of the user's live tuning.
                    copy.core.possession.mode = AmplifierInteractionMode.PossessionShot;
                    copy.core.possession.distanceDrivenShots = false;
                    copy.core.possession.shotSpeed = 16f; copy.core.possession.maximumRange = 26f;
                    copy.core.possession.maximumFlightSeconds = 15f; copy.core.possession.settlingSeconds = .3f;
                    copy.core.linearDrag = .42f; copy.core.maximumPlanarSpeed = 25f;
                    copy.core.possession.settledSpeed = .15f; copy.core.possession.reentryDelay = .4f;
                    v.SyncSettings(true); v.Launch(); var start = v.Position;
                    v.Advance(.02f);
                    Check(v.Position.x > start.x && v.Position.x - start.x < .4f, "First simulation step starts at the displayed launch position");
                    v.Advance(.98f);
                    Check(v.Distance > 9f && v.Distance < 14f && v.Speed > 5f && v.Speed < 12f, "Actual Rigidbody simulates a full-charge coast");
                    float before = v.Speed;
                    copy.core.linearDrag = 4f; v.SyncSettings(true); v.Advance(.2f);
                    Check(v.Speed < before * .6f, "Live drag edits change the running simulation");
                    copy.core.linearDrag = .1f; v.SyncSettings(true); v.Advance(.02f);
                    Check(v.CoreObject.GetComponent<Rigidbody>().linearDamping < 1f, "Live drag can be reduced as well as increased");
                    copy.core.possession.settledSpeed = 3f; copy.core.possession.settlingSeconds = .1f;
                    for (int i = 0; i < 150 && v.State == AmplifierCoreInteractionPreview.Phase.Flying; i++) v.Advance(.02f);
                    Check(v.State == AmplifierCoreInteractionPreview.Phase.ReentryDelay && v.Speed == 0f, "Stop threshold and settling time end the coast");
                    v.Advance(.5f);
                    Check(v.State == AmplifierCoreInteractionPreview.Phase.Enterable, "Re-entry delay completes in edit mode");
                    v.SetArena(12f, 10f, true); copy.core.linearDrag = .42f;
                    copy.core.possession.settledSpeed = .15f; v.SyncSettings(true); v.Launch();
                    v.Advance(1f);
                    Check(v.Bounces == 1 && v.CoreObject.GetComponent<Rigidbody>().linearVelocity.x < 0f, "A wall reflects a shot once, without repeated overlap bounces");
                    Check(Mathf.Abs(v.Position.x) < 6f && v.CoreObject.GetComponent<Rigidbody>().angularVelocity.magnitude > .01f, "Wall contact preserves position containment and rolling spin");
                    Capture(v, output + "/01-arena-flight.png", false);
                    v.Reset(); Check(v.Speed == 0f && v.Distance == 0f && v.Clock == 0f, "Stop / reset clears motion, path and clock");
                    v.SetAppearance(AmplifierCoreInteractionPreview.Look.LightAim); v.Advance(.5f);
                    Capture(v, output + "/02-light-charge.png", true);
                    float time = v.Clock; Vector3 point = v.Position;
                    var first = Pixels(v, true); var second = Pixels(v, true);
                    try { Check(Equal(first, second) && v.Clock == time && v.Position == point, "Repeated paused rendering freezes position, shell flow and visual clock"); }
                    finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
                    v.SetAppearance(AmplifierCoreInteractionPreview.Look.Contest); v.Advance(.5f);
                    Capture(v, output + "/03-two-markers.png", true);
                    var shell = v.CoreObject.transform.Find("Visual Root/Neutral Shell").GetComponent<Renderer>();
                    var block = new MaterialPropertyBlock(); shell.GetPropertyBlock(block);
                    Check(block.GetVector("_PossessionMarker").x > .1f && block.GetVector("_PossessionMarker2").x > .1f, "Both possession openings render in contest preview");
                    Check(shell.sharedMaterial.GetFloat("_PreviewTime") < 0f && block.GetFloat("_PreviewTime") == v.Clock, "Preview clock is instance-only; gameplay materials keep game time");
                    v.SetAppearance(AmplifierCoreInteractionPreview.Look.TimeoutWarning); v.Advance(.5f);
                    Capture(v, output + "/04-warning.png", true);
                    v.SetAppearance(AmplifierCoreInteractionPreview.Look.Spawn); v.Advance(.12f);
                    float small = v.Radius; v.Advance(1f);
                    Check(v.Radius > small * 1.5f, "Spawn grows and reveals the actual Core");
                    v.SetAppearance(AmplifierCoreInteractionPreview.Look.Despawn); v.Advance(1f);
                    Check(v.Radius < .01f, "Despawn shrinks and hides the Core");
                    v.SetAppearance(AmplifierCoreInteractionPreview.Look.LightAim); v.Launch(true); v.Advance(.2f);
                    Check(v.State == AmplifierCoreInteractionPreview.Phase.Charging && v.DisplayCharge < 1f, "Charge preview animates from the first aim dot toward the last");
                    v.Advance(copy.core.possession.fullChargeSeconds);
                    Check(v.State == AmplifierCoreInteractionPreview.Phase.Flying, "Charge preview releases into the physics shot");
                    CheckDistances(v, copy, Check);
                    CheckInteractionFeedback(v, copy, output, Check);
                    CheckControlUI(v, copy, output, Check);
                    copy.core.possession.mode = AmplifierInteractionMode.Physical; copy.core.mass = 5f; copy.core.attackImpulse = 20f;
                    v.SyncSettings(true); v.Launch(); v.Advance(.02f); float heavy = v.Speed;
                    copy.core.mass = 1f; v.SyncSettings(true); v.Launch(); v.Advance(.02f);
                    Check(v.Speed > heavy * 4f, "Mass changes impulse response in Physical mode");
                    Check(!clean.isDirty && clean.rootCount == 0, "Animation and physics never dirty or populate the authoring scene");
                }
                Check(EditorSceneManager.previewSceneCount == previewCount, "Disposal removes preview scene, objects and physics world");
                Check(JsonUtility.ToJson(source) == originalSettings, "Validation preserves the user's shared tuning");
                Check(!EditorApplication.isPlaying, "All preview checks ran without entering Play Mode");
                string report = "PASSED (" + checks.Count + " edit-mode preview checks)\n" + string.Join("\n", checks);
                File.WriteAllText(output + "/preview-validation.txt", report); return report;
            }
            finally
            {
                Object.DestroyImmediate(copy); EditorSceneManager.CloseScene(clean, true);
                if (originalScene.IsValid()) SceneManager.SetActiveScene(originalScene);
            }
        }
        static void CheckInteractionFeedback(AmplifierCoreInteractionPreview v, AmplifierSharedProfile copy, string output, Action<bool, string> check)
        {
            var settings = copy.core.possession;
            settings.chargeVibration = true; settings.ejectionRipple = true;
            var visual = v.CoreObject.GetComponent<AmplifierCoreVisual>();
            var shell = v.CoreObject.transform.Find("Visual Root/Neutral Shell");
            var renderer = shell.GetComponent<Renderer>(); var block = new MaterialPropertyBlock();
            v.Charge = 0f; v.SetAppearance(AmplifierCoreInteractionPreview.Look.LightAim); v.Advance(.1f);
            Vector3 rest = shell.localPosition, physics = v.Position;
            v.Charge = 1f; v.Advance(.1f);
            check(visual.ChargeVibration01 > .99f && Vector3.Distance(shell.localPosition, rest) > .001f && v.Position == physics,
                "Full-charge preview vibrates the rendered Core without shifting the physics origin");
            settings.chargeVibration = false; v.Advance(.1f);
            check(Vector3.Distance(shell.localPosition, rest) < .0001f, "Disabling preview vibration restores its exact resting position");
            settings.chargeVibration = true;
            foreach (var look in new[] { AmplifierCoreInteractionPreview.Look.PostContestLight, AmplifierCoreInteractionPreview.Look.PostContestDark })
            {
                v.SetAppearance(look); v.Advance(.5f); renderer.GetPropertyBlock(block);
                check(block.GetVector("_PossessionMarker").x > .1f && block.GetVector("_PossessionMarker2").x > .1f,
                    "Post-contest replay begins with both opposing indicators");
                v.Advance(.52f); renderer.GetPropertyBlock(block);
                bool light = look == AmplifierCoreInteractionPreview.Look.PostContestLight;
                check((light ? block.GetVector("_PossessionMarker").x : block.GetVector("_PossessionMarker2").x) > .1f &&
                    (light ? block.GetVector("_PossessionMarker2").x : block.GetVector("_PossessionMarker").x) == 0f && visual.EjectionRippleActive,
                    "Post-contest keeps the winner, ejects the loser and starts the surface ripple: " + look);
                Capture(v, output + (light ? "/05-light-wins-ripple.png" : "/06-dark-wins-ripple.png"), true);
                var rippleFrame = Pixels(v, true);
                try
                {
                    settings.ejectionRipple = false;
                    var plain = Pixels(v, true);
                    try { check(!Equal(rippleFrame, plain), "The ripple visibly deforms the rendered Core surface"); }
                    finally { Object.DestroyImmediate(plain); }
                }
                finally { Object.DestroyImmediate(rippleFrame); settings.ejectionRipple = true; }
                v.Advance(1.5f); renderer.GetPropertyBlock(block);
                check(!visual.EjectionRippleActive && block.GetVector("_PossessionMarkerOffsets").sqrMagnitude < .0001f,
                    "Post-contest settles to one centered winner with no residual ripple");
            }
            v.SetAppearance(AmplifierCoreInteractionPreview.Look.Ejected); v.Advance(1.04f); renderer.GetPropertyBlock(block);
            check(visual.EjectionRippleActive && block.GetVector("_PossessionMarker").x == 0f && block.GetVector("_PossessionMarker2").x == 0f,
                "Empty ejection preview clears both possession openings while its ripple continues");
            Capture(v, output + "/07-empty-ejection-ripple.png", true);
            v.Reset(); check(!visual.EjectionRippleActive, "Preview reset clears outstanding ejection feedback");
        }
        static void CheckControlUI(AmplifierCoreInteractionPreview v, AmplifierSharedProfile copy, string output, Action<bool, string> check)
        {
            var s = copy.core.possession;
            s.showContestRing = s.showControlText = s.showControlTimer = true;
            s.contestRingGap = .2f; s.contestRingWidth = .14f; s.contestRingOutline = .02f;
            s.controlTextSize = 2.4f; s.controlTextOffset = new Vector2(0, .55f);
            v.SetAppearance(AmplifierCoreInteractionPreview.Look.Contest); v.ContestBalance = .5f;
            var pixels = Pixels(v, true);
            try
            {
                Color At(float fraction, float radius)
                {
                    var d = AmplifierCoreControlUI.SplitDirection(fraction);
                    var p = v.Camera.WorldToViewportPoint(v.Position + new Vector3(d.x, 0, d.y) * radius);
                    return pixels.GetPixel(Mathf.RoundToInt(p.x * pixels.width), Mathf.RoundToInt(p.y * pixels.height));
                }
                check(v.ControlUI.RingVisible && v.ControlUI.TextVisible && v.ControlUI.Caption.text.Contains("ALTERNATE"), "Contested preview renders the real ring and gameplay prompt");
                check(At(.25f, v.ControlUI.RingRadius).r > .9f && At(.75f, v.ControlUI.RingRadius).r < .1f,
                    "Rendered equal contest ring is white on the left and black on the right");
                check(Mathf.Max(At(.75f, v.ControlUI.RingRadius + v.ControlUI.RingWidth * .5f + v.Radius * .005f).r,
                    At(.75f, v.ControlUI.RingRadius + v.ControlUI.RingWidth * .5f + v.Radius * .015f).r) > .4f,
                    "Dark ring remains visible through its white outline");
                File.WriteAllBytes(output + "/08-contest-ring-even.png", pixels.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(pixels); }
            check(Vector2.Distance(AmplifierCoreControlUI.SplitDirection(.5f), Vector2.up) < .001f &&
                AmplifierCoreControlUI.SplitDirection(.65f).x > 0f && AmplifierCoreControlUI.SplitDirection(.35f).x < 0f,
                "Top is halfway; Light gains clockwise and Dark gains counterclockwise");
            foreach (float balance in new[] { .25f, .75f })
            {
                v.ContestBalance = balance;
                var frame = Pixels(v, true);
                try
                {
                    var d = AmplifierCoreControlUI.SplitDirection(.6f);
                    var p = v.Camera.WorldToViewportPoint(v.Position + new Vector3(d.x, 0, d.y) * v.ControlUI.RingRadius);
                    float value = frame.GetPixel(Mathf.RoundToInt(p.x * frame.width), Mathf.RoundToInt(p.y * frame.height)).r;
                    check(balance > .5f ? value > .9f : value < .1f, "Shader fill follows control fraction " + balance);
                    File.WriteAllBytes(output + (balance < .5f ? "/09-dark-leading.png" : "/10-light-leading.png"), frame.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(frame); }
            }
            s.showContestRing = false; v.Render(new Rect(0,0,800,500), true);
            check(!v.ControlUI.RingVisible && v.ControlUI.TextVisible, "Ring can be hidden independently of text");
            s.showContestRing = true; s.showControlText = false; v.Render(new Rect(0,0,800,500), true);
            check(v.ControlUI.RingVisible && !v.ControlUI.TextVisible, "Text can be hidden independently of ring");
            s.showControlText = true; s.showControlTimer = false;
            s.contestedText = "CUSTOM CONTEST\n{time}"; s.controlTextColor = Color.cyan; s.controlTextSize = 5f;
            v.Render(new Rect(0,0,800,500), true);
            check(v.ControlUI.Caption.text == "CUSTOM CONTEST" && v.ControlUI.Caption.color == Color.cyan &&
                Mathf.Abs(v.ControlUI.Caption.fontSize - 5f * v.Radius) < .001f, "Text wording, color, size and timer toggle update live");
            s.controlTextOffset = new Vector2(.4f, .8f); v.Render(new Rect(0,0,800,500), true);
            var delta = v.ControlUI.Caption.transform.position - v.Position;
            float radiusBefore = v.ControlUI.RingRadius, fontBefore = v.ControlUI.Caption.fontSize;
            copy.core.coreScale *= .5f; v.SyncSettings(true); v.Render(new Rect(0,0,800,500), true);
            check(Mathf.Abs(v.ControlUI.RingRadius / radiusBefore - .5f) < .001f &&
                Mathf.Abs(v.ControlUI.Caption.fontSize / fontBefore - .5f) < .001f &&
                Vector3.Distance(v.ControlUI.Caption.transform.position - v.Position, delta * .5f + Vector3.up * .025f) < .002f,
                "Core scaling preserves ring, text and placement proportions");
            copy.core.coreScale *= 2f; v.SyncSettings(true);
            s.controlTextSize = 2.4f; s.controlTextColor = Color.white; s.controlTextOffset = new Vector2(0,.55f); s.showControlTimer = true;
            s.contestedText = new AmplifierPossessionSettings().contestedText;
            foreach (var look in new[] { AmplifierCoreInteractionPreview.Look.LightAim, AmplifierCoreInteractionPreview.Look.DarkAim,
                AmplifierCoreInteractionPreview.Look.PostContestLight, AmplifierCoreInteractionPreview.Look.PostContestDark })
            {
                v.SetAppearance(look); v.Advance(1f); v.Render(new Rect(0,0,800,500), true);
                string team = look is AmplifierCoreInteractionPreview.Look.LightAim or AmplifierCoreInteractionPreview.Look.PostContestLight ? s.lightTeamText : s.darkTeamText;
                check(!v.ControlUI.RingVisible && v.ControlUI.TextVisible && v.ControlUI.Caption.text.Contains(team), "Matching possession/winner UI without contest ring: " + look);
            }
            v.SetAppearance(AmplifierCoreInteractionPreview.Look.Ejected); v.Advance(1f); v.Render(new Rect(0,0,800,500), true);
            check(!v.ControlUI.RingVisible && !v.ControlUI.TextVisible, "Empty ejection removes possession UI");
            v.SetAppearance(AmplifierCoreInteractionPreview.Look.LightAim); v.Launch(); v.Render(new Rect(0,0,800,500), true);
            check(!v.ControlUI.RingVisible && !v.ControlUI.TextVisible, "Launched preview removes possession UI");
        }
        static void CheckDistances(AmplifierCoreInteractionPreview v, AmplifierSharedProfile copy, Action<bool, string> check)
        {
            var settings = copy.core.possession;
            settings.distanceDrivenShots = true; settings.minimumRange = 5f; settings.maximumRange = 15f;
            settings.shotSpeed = 14f; settings.settledSpeed = 3f; settings.settlingSeconds = .3f;
            settings.maximumFlightSeconds = 15f; settings.finalBrakeDrag = 5f;
            copy.core.linearDrag = 2.1f;
            v.SetArena(60f, 20f, false); v.AimAngle = 0f;
            float originalStep = Time.fixedDeltaTime;
            try
            {
                foreach (float step in new[] { .02f, .01f, 1f / 60f })
                {
                    Time.fixedDeltaTime = step;
                    foreach (float charge in new[] { 0f, .25f, .5f, .75f, 1f })
                    {
                        v.Charge = charge; v.Launch(); float initialSpeed = v.Speed;
                        for (int i = 0; i < 1000 && v.State == AmplifierCoreInteractionPreview.Phase.Flying; i++) v.Advance(step);
                        float expected = Mathf.Lerp(5f, 15f, charge);
                        check(v.StopTime > 0 && v.Speed == 0f && Mathf.Abs(v.Distance - expected) < .09f,
                            $"{charge:P0} charge / {step:0.000}s step travels {v.Distance:0.000} of {expected:0.0} units then stops");
                        check(Mathf.Abs(initialSpeed - 14f) < .001f, "Charge distance preserves launch speed");
                    }
                }
                Time.fixedDeltaTime = originalStep;
                copy.core.maximumPlanarSpeed = 8f; settings.settlingSeconds = 0f;
                v.Charge = 1f; v.Launch();
                for (int i = 0; i < 1000 && v.State == AmplifierCoreInteractionPreview.Phase.Flying; i++) v.Advance(originalStep);
                check(Mathf.Abs(v.Distance - 15f) < .09f && v.StopTime > 0, "Range compensates for speed cap and zero settling time");
                copy.core.maximumPlanarSpeed = 25f; settings.settlingSeconds = .3f;
                settings.maximumFlightSeconds = 1f; settings.finalBrakeDrag = 5f;
                v.Charge = 1f; v.Launch();
                for (int i = 0; i < 1000 && v.State == AmplifierCoreInteractionPreview.Phase.Flying; i++) v.Advance(originalStep);
                check(Mathf.Abs(v.Distance - 15f) < .12f && v.StopTime > 1f, "Distance calculation includes the safety brake when it starts during travel");
                settings.maximumFlightSeconds = 15f;
                v.SetArena(12f, 10f, true); v.Launch(); v.Advance(1f);
                check(v.Bounces == 1 && v.Speed > 0f, "Distance-driven shot still rebounds and coasts naturally");
            }
            finally { Time.fixedDeltaTime = originalStep; }
        }
        static Texture2D Pixels(AmplifierCoreInteractionPreview preview, bool closeup)
        {
            var texture = preview.Render(new Rect(0, 0, 800, 500), closeup);
            var target = RenderTexture.GetTemporary(800, 500, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, target); RenderTexture.active = target;
                var pixels = new Texture2D(800, 500, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 800, 500), 0, 0); pixels.Apply(); return pixels;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
        static void Capture(AmplifierCoreInteractionPreview preview, string path, bool closeup)
        { var pixels = Pixels(preview, closeup); try { File.WriteAllBytes(path, pixels.EncodeToPNG()); } finally { Object.DestroyImmediate(pixels); } }
        static bool Equal(Texture2D first, Texture2D second)
        {
            var a = first.GetRawTextureData<byte>(); var b = second.GetRawTextureData<byte>();
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
