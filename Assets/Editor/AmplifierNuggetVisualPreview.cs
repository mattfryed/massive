#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Scene-independent preview of the two visual-only Amplifier pickup prefabs.</summary>
public sealed class AmplifierNuggetVisualPreview : EditorWindow
{
    public const string Folder = "Assets/Power-ups/Amplifier Nuggets/";
    public const string NuggetPath = Folder + "Amplifier Nugget Visual.prefab";
    public const string NuggletPath = Folder + "Amplifier Nugglet Visual.prefab";
    PreviewRenderUtility preview;
    GameObject large, small;
    ParticleSystem[] particles;
    ParticleSystemRenderer[] prismatic;
    MaterialPropertyBlock tint;
    float time, zoom = .52f;
    bool playing = true;
    double previousTime;
    Color background = new(.045f, .045f, .045f, 1);

    [MenuItem("MASSIVE/Amplifier/Preview Amplifier Nuggets")]
    public static void Open() => GetWindow<AmplifierNuggetVisualPreview>("Amplifier Nuggets");

    void OnEnable() { previousTime = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
    void OnDisable() { EditorApplication.update -= Tick; Release(); }
    void Release() { preview?.Cleanup(); preview = null; }
    void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = (float)Math.Min(.05, now - previousTime); previousTime = now;
        if (!playing || preview == null) return;
        Advance(dt); Repaint();
    }
    void EnsurePreview()
    {
        if (preview != null) return;
        var largeAsset = AssetDatabase.LoadAssetAtPath<GameObject>(NuggetPath);
        var smallAsset = AssetDatabase.LoadAssetAtPath<GameObject>(NuggletPath);
        if (!largeAsset || !smallAsset) return;
        preview = new PreviewRenderUtility();
        preview.camera.orthographic = true;
        preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 20;
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.transform.SetPositionAndRotation(new Vector3(0, 5, 0), Quaternion.Euler(90, 0, 0));
        preview.ambientColor = Color.white;
        preview.lights[0].intensity = .8f; preview.lights[1].intensity = .4f;
        large = Instantiate(largeAsset); small = Instantiate(smallAsset);
        preview.AddSingleGO(large); preview.AddSingleGO(small);
        large.transform.position = new Vector3(-.45f, 0, 0);
        small.transform.position = new Vector3(.45f, 0, 0);
        var systems = new System.Collections.Generic.List<ParticleSystem>();
        var colorRenderers = new System.Collections.Generic.List<ParticleSystemRenderer>();
        foreach (var root in new[] { large, small })
        {
            systems.AddRange(root.GetComponentsInChildren<ParticleSystem>(true));
            colorRenderers.Add(root.transform.Find("Prismatic Swirl").GetComponent<ParticleSystemRenderer>());
        }
        particles = systems.ToArray(); prismatic = colorRenderers.ToArray();
        Restart();
    }
    void Restart()
    {
        time = 0;
        foreach (var ps in particles) ps.Simulate(1.2f, false, true, true);
        ApplyClock();
    }
    void Advance(float dt)
    {
        time += dt;
        foreach (var ps in particles) ps.Simulate(dt, false, false, false);
        ApplyClock();
    }
    void ApplyClock()
    {
        if (tint == null) tint = new MaterialPropertyBlock();
        foreach (var renderer in prismatic)
        {
            renderer.GetPropertyBlock(tint); tint.SetFloat("_PreviewTime", time);
            renderer.SetPropertyBlock(tint);
        }
    }
    Texture Render(Rect rect)
    {
        preview.camera.orthographicSize = Mathf.Max(zoom, .9f / Mathf.Max(.1f, rect.width / rect.height));
        preview.camera.backgroundColor = background;
        preview.BeginPreview(rect, GUIStyle.none);
        preview.camera.Render();
        return preview.EndPreview();
    }
    void OnGUI()
    {
        EnsurePreview();
        EditorGUILayout.LabelField("AMPLIFIER NUGGET / NUGGLET", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Visual study: black + white + a cycling prismatic cloud. No pickup rewards or colliders. The nugglet is one third of the nugget's linear size.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(playing ? "Pause" : "Play")) playing = !playing;
            if (GUILayout.Button("Restart") && preview != null) Restart();
            if (GUILayout.Button("Select Nugget")) Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(NuggetPath);
            if (GUILayout.Button("Select Nugglet")) Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(NuggletPath);
        }
        zoom = EditorGUILayout.Slider("View distance", zoom, .25f, 1.2f);
        background = EditorGUILayout.ColorField("Background", background);
        if (GUILayout.Button("Select Prismatic Material"))
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Amplifier Prismatic.mat");
        if (preview == null) { EditorGUILayout.HelpBox("The visual prefabs are unavailable.", MessageType.Warning); return; }
        Rect rect = GUILayoutUtility.GetRect(320, 220, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (Event.current.type == EventType.Repaint) GUI.DrawTexture(rect, Render(rect), ScaleMode.StretchToFill, false);
        EditorGUILayout.LabelField("LARGE NUGGET                                               SMALL NUGGLET", EditorStyles.centeredGreyMiniLabel);
    }

    // Deterministic capture of the actual prefabs and shader; doesn't enter Play Mode or touch a gameplay scene.
    public static string CaptureFrames(string directory, int count = 120)
    {
        Directory.CreateDirectory(directory);
        var tool = CreateInstance<AmplifierNuggetVisualPreview>();
        Texture2D frame = null;
        try
        {
            tool.EnsurePreview();
            if (tool.preview == null) throw new InvalidOperationException("Missing Amplifier visual prefabs.");
            tool.playing = false; tool.Restart();
            frame = new Texture2D(960, 540, TextureFormat.RGB24, false);
            for (int i = 0; i < count; i++)
            {
                var texture = tool.Render(new Rect(0, 0, 960, 540));
                var previous = RenderTexture.active;
                // PreviewRenderUtility returns device pixels on high-DPI displays.
                var output = RenderTexture.GetTemporary(960, 540, 0, RenderTextureFormat.ARGB32);
                try
                {
                    Graphics.Blit(texture, output);
                    RenderTexture.active = output;
                    frame.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); frame.Apply();
                    File.WriteAllBytes(Path.Combine(directory, "frame-" + i.ToString("D3") + ".png"), frame.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(output); }
                tool.Advance(1f / 30);
            }
            return count + " frames captured; live particles=" + string.Join(",", Array.ConvertAll(tool.particles, p => p.particleCount.ToString()));
        }
        finally { if (frame) DestroyImmediate(frame); DestroyImmediate(tool); }
    }
}
#endif
