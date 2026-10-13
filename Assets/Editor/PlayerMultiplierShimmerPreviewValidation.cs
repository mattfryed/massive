using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Player;
using Massive.Scoring;
using Massive.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.EditorTools
{
    public static class PlayerMultiplierShimmerPreviewValidation
    {
        const string Output = "Library/MultiplierShimmerPreviewValidation";
        static readonly List<string> checks = new(), errors = new();
        static PlayerTuningWindow window;
        static int ticks;
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); checks.Add("PASS " + message); }
        static void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project.");
            Directory.CreateDirectory(Output);
            Application.logMessageReceived += Log;
            try
            {
                RenderChecks();
                window = ScriptableObject.CreateInstance<PlayerTuningWindow>();
                window.position = new Rect(50, 50, 820, 1050);
                Set("tab", 4); Set("multiplierPreviewAnimated", false);
                window.Show(); window.Repaint();
                EditorApplication.update += WindowCheck;
            }
            catch (Exception ex) { checks.Add(ex.ToString()); Finish(false); }
        }
        static void Set(string field, object value) =>
            typeof(PlayerTuningWindow).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window, value);
        static object Get(string field) =>
            typeof(PlayerTuningWindow).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
        static void WindowCheck()
        {
            if (++ticks < 20) { window.Repaint(); return; }
            EditorApplication.update -= WindowCheck;
            try
            {
                Check(Get("multiplierPreview") != null, "Appearance window executes the embedded preview GUI");
                float time = (float)Get("multiplierPreviewSeconds");
                Set("multiplierPreviewAnimated", false);
                typeof(PlayerTuningWindow).GetMethod("TickMultiplierPreview", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(window, new object[] { EditorApplication.timeSinceStartup, .05f });
                Check((float)Get("multiplierPreviewSeconds") == time, "Pause freezes preview time");
                window.Close(); Object.DestroyImmediate(window); window = null;
                Check(!Resources.FindObjectsOfTypeAll<Mesh>().Any(m => m.name == "Multiplier preview quad" || m.name == "World-unit preview grid"),
                    "Closing the window disposes preview meshes");
                Check(errors.Count == 0, "No Unity errors during GPU rendering or window layout/repaint");
                Finish(true);
            }
            catch (Exception ex) { checks.Add(ex.ToString()); Finish(false); }
        }
        static void Finish(bool passed)
        {
            EditorApplication.update -= WindowCheck;
            if (window) { window.Close(); Object.DestroyImmediate(window); }
            Application.logMessageReceived -= Log;
            File.WriteAllText(Output + "/report.txt", (passed && errors.Count == 0 ? "PASSED" : "FAILED") + "\n" +
                string.Join("\n", checks) + "\n" + string.Join("\n", errors));
            EditorApplication.Exit(passed && errors.Count == 0 ? 0 : 1);
        }
        static Texture2D Capture(PlayerMultiplierShimmerPreview preview, PlayerMultiplierShimmerSettings settings,
            float multiplier, float time, float zoom, string name)
        {
            var source = preview.Render(new Rect(0,0,800,300), settings, multiplier, time, 0, zoom);
            if (!source) throw new Exception("Preview assets unavailable.");
            var target = RenderTexture.GetTemporary(800, 300, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var image = new Texture2D(800,300,TextureFormat.RGBA32,false);
            try
            {
                Graphics.Blit(source, target); RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,800,300),0,0); image.Apply();
                if (name != null) File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
                return image;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
        static double Energy(Texture2D image, bool hud = false)
        {
            double energy = 0;
            for (int y=0;y<image.height;y++) for (int x=hud?340:0; x<(hud?800:340); x++)
            {
                Color c=image.GetPixel(x,y);
                float hi=Mathf.Max(c.r,c.g,c.b), lo=Mathf.Min(c.r,c.g,c.b);
                if (hi > .16f && hi-lo > .08f) energy += hi-lo;
            }
            return energy;
        }
        static int BodyWidth(Texture2D image)
        {
            int min=800,max=0;
            for (int y=30;y<260;y++) for (int x=0;x<280;x++)
            {
                Color c=image.GetPixel(x,y);
                if (Mathf.Min(c.r,c.g,c.b)>.7f) { min=Mathf.Min(min,x); max=Mathf.Max(max,x); }
            }
            return max-min+1;
        }
        static void RenderChecks()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<PlayerTuningProfile>(PlayerTuningEditing.AssetPath);
            var savedSettings = JsonUtility.ToJson(tuning);
            var settings = JsonUtility.FromJson<PlayerMultiplierShimmerSettings>(JsonUtility.ToJson(tuning.multiplierShimmer));
            var globals = PlayerGlobalModifiers.Current;
            var savedGlobals = JsonUtility.ToJson(globals);
            int sceneCount=SceneManager.sceneCount;
            bool dirty=SceneManager.GetActiveScene().isDirty;
            int actors=Object.FindObjectsByType<PlayerControllerScript>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            using (var renderer = new PlayerMultiplierShimmerPreview())
            {
                var images = new List<Texture2D>();
                try
                {
                    var baseImage = Capture(renderer,settings,1,.5f,1,"base"); images.Add(baseImage);
                    Check(Mathf.Abs(renderer.BodyRadius-.5f*globals.ForPlayer(0).size)<.0001f,
                        "Player radius uses prefab dimensions and replaces saved root scale with the current global size");
                    int width=BodyWidth(baseImage);
                    Check(Mathf.Abs(width-renderer.BodyRadius*200f)<=3, "GPU outline diameter matches world radius and view scale");
                    var energy=new List<double>(); var hud=new List<double>();
                    foreach (float m in new[]{8f,12f,16f})
                    {
                        var image=Capture(renderer,settings,m,.5f,1,"multiplier-"+m); images.Add(image);
                        energy.Add(Energy(image)); hud.Add(Energy(image,true));
                    }
                    File.WriteAllText(Output+"/pixels.txt", $"Body width={width}; radius={renderer.BodyRadius}; HUD full width={renderer.FullBarWidth}; HUD height={renderer.BarHeight}\nOutline energy: {string.Join(",",energy)}\nHUD energy: {string.Join(",",hud)}");
                    Check(Energy(baseImage)<1 && energy[0]>1 && energy[1]>energy[0] && energy[2]>energy[1], "Actual outline shader intensifies at 8x, 12x, 16x");
                    Check(hud[0]>1 && hud[1]>hud[0] && hud[2]>hud[1], "Actual HUD shader intensifies through 16x");
                    float radius=renderer.BodyRadius, fullWidth=renderer.FullBarWidth;
                    Check(Mathf.Abs(renderer.BarWidth-fullWidth)<.0001f, "16x HUD sample fills its authored track");
                    var zoom=Capture(renderer,settings,1,.5f,2,"zoom"); images.Add(zoom);
                    Check(Mathf.Abs(BodyWidth(zoom)-width*2)<=3 && Mathf.Approximately(renderer.BodyRadius,radius), "Zoom doubles screen size without changing world dimensions");
                    float saved=globals.ForPlayer(0).size;
                    try
                    {
                        globals.ForPlayer(0).size=saved*2;
                        var bigger=Capture(renderer,settings,1,.5f,1,"size-double"); images.Add(bigger);
                        Check(Mathf.Abs(BodyWidth(bigger)-width*2)<=3 && Mathf.Approximately(renderer.BodyRadius,radius*2), "Live global-size change doubles player geometry");
                        Check(Mathf.Approximately(renderer.FullBarWidth,fullWidth), "Player-size tuning does not scale the HUD");
                    }
                    finally { globals.ForPlayer(0).size=saved; }
                    settings.outlineReach=.7f;
                    var wide=Capture(renderer,settings,16,.5f,1,"reach-wide"); images.Add(wide);
                    Check(Mathf.Abs(renderer.Reach-radius*.7f)<.0001f && Energy(wide)>energy[2], "Outline reach updates at the correct world scale");
                    var drift=Capture(renderer,settings,16,1.4f,1,"drift"); images.Add(drift);
                    Check(!wide.GetRawTextureData<byte>().SequenceEqual(drift.GetRawTextureData<byte>()), "Explicit preview clock animates the real shader");
                    settings.enabled=false;
                    var disabled=Capture(renderer,settings,16,.5f,1,"disabled"); images.Add(disabled);
                    Check(Energy(disabled)<1 && Energy(disabled,true)<1, "Disabling shimmer removes both preview effects");
                    Check(renderer.Resolve(3,settings), "All four player slots have valid prefab and HUD bindings");
                    Check(!ShaderUtil.ShaderHasError(Shader.Find("MASSIVE/PlayerBlobVector")) &&
                        !ShaderUtil.ShaderHasError(Shader.Find("MASSIVE/UI/Multiplier Maximum Glow")), "Gameplay shaders compile for the preview graphics device");
                }
                finally { foreach (var image in images) Object.DestroyImmediate(image); }
            }
            Check(savedSettings==JsonUtility.ToJson(tuning) && savedGlobals==JsonUtility.ToJson(globals), "Preview controls leave shared tuning and global sizes unchanged");
            Check(SceneManager.sceneCount==sceneCount && SceneManager.GetActiveScene().isDirty==dirty &&
                actors==Object.FindObjectsByType<PlayerControllerScript>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,
                "Preview creates no gameplay actors and leaves scene state unchanged");
        }
    }
}
