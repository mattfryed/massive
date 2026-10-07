#if UNITY_EDITOR
using System;
using Massive.Dynamo;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Owns only temporary visuals. No controller Update, gameplay, or scene settings are run.</summary>
internal sealed class DynamoVisualPreviewSession : IDisposable
{
    public readonly DynamoSelectiveBloom Source;
    public RenderTexture Output { get; private set; }
    public DynamoStormState State { get; private set; }
    public float Pressure => field ? field.pressureGain : 0f;
    public MagnetosphereFieldLinesGPU2D Field => field;
    public DynamoFlowBlanketRenderer Flow => flow;
    public DynamoSelectiveBloom Bloom => bloom;
    private GameObject root;
    private Camera camera;
    private MagnetosphereFieldLinesGPU2D field;
    private DynamoFlowBlanketRenderer flow;
    private DynamoSelectiveBloom bloom;
    private Material fieldMaterial;
    private CommandBuffer fieldCommands;
    private const HideFlags Temporary = HideFlags.HideAndDontSave;

    public DynamoVisualPreviewSession(DynamoSelectiveBloom source)
    {
        if (!source || !source.field || !source.flow || !source.flow.controller || !source.targetCamera || !source.field.lineMaterial)
            throw new ArgumentException("Choose the scene's Dynamo Bloom with its field, flow, controller and camera assigned.");
        Source = source;
        try
        {
            root = new GameObject("Dynamo visual preview (temporary)") { hideFlags = Temporary };
            root.SetActive(false);
            field = root.AddComponent<MagnetosphereFieldLinesGPU2D>();
            flow = root.AddComponent<DynamoFlowBlanketRenderer>();
            bloom = root.AddComponent<DynamoSelectiveBloom>();
            camera = root.AddComponent<Camera>(); camera.enabled = false;
            field.hideFlags = flow.hideFlags = bloom.hideFlags = camera.hideFlags = Temporary;
            fieldCommands = new CommandBuffer { name = "Dynamo preview: magnetic field" };
            camera.AddCommandBuffer(CameraEvent.AfterForwardAlpha, fieldCommands);
            root.SetActive(true);
        }
        catch { Dispose(); throw; }
    }

    public void Render(float elapsed, float duration, float peak, Vector2 direction, float motionOffset, int width)
    {
        if (!Source || !Source.gameObject.scene.isLoaded) throw new InvalidOperationException("The source scene has closed.");
        var sourceCamera = Source.targetCamera;
        width = Mathf.Clamp(width, 320, 4096);
        int height = Mathf.Clamp(Mathf.RoundToInt(width / Mathf.Max(.1f, sourceCamera.aspect)), 180, 4096);
        int samples = sourceCamera.allowMSAA ? Mathf.Max(1, QualitySettings.antiAliasing) : 1;
        if (!Output || Output.width != width || Output.height != height || Output.antiAliasing != samples)
        {
            ReleaseOutput();
            Output = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf)
            {
                name = "Dynamo visual preview image", hideFlags = Temporary, antiAliasing = samples
            };
            Output.Create();
        }
        camera.CopyFrom(sourceCamera);
        camera.enabled = false; camera.targetTexture = Output;
        camera.rect = new Rect(0, 0, 1, 1); camera.aspect = sourceCamera.aspect;
        camera.transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);

        EditorUtility.CopySerialized(Source.field, field);
        field.hideFlags = Temporary;
        field.scientificRenderer = null; field.showDebugEnvelope = false;
        if (!fieldMaterial || fieldMaterial.shader != Source.field.lineMaterial.shader)
        {
            if (fieldMaterial) UnityEngine.Object.DestroyImmediate(fieldMaterial);
            fieldMaterial = new Material(Source.field.lineMaterial) { name = "Dynamo preview field material", hideFlags = Temporary };
        }
        fieldMaterial.CopyPropertiesFromMaterial(Source.field.lineMaterial);
        field.lineMaterial = fieldMaterial;
        State = Source.flow.controller.SampleVisualStorm(field, camera, direction, elapsed, duration, peak, motionOffset);
        field.BuildVisualPreview(State.Elapsed);

        EditorUtility.CopySerialized(Source.flow, flow);
        flow.hideFlags = Temporary; flow.field = field;
        flow.BuildVisualPreview(State, camera);
        EditorUtility.CopySerialized(Source, bloom);
        bloom.hideFlags = Temporary; bloom.field = field; bloom.flow = flow; bloom.targetCamera = camera;
        bloom.PreviewRendering = true;

        fieldCommands.Clear();
        fieldCommands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        fieldCommands.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
        field.RecordBloomSource(fieldCommands);
        var previous = RenderTexture.active;
        try { camera.Render(); }
        finally { RenderTexture.active = previous; }
    }

    private void ReleaseOutput()
    {
        if (camera) camera.targetTexture = null;
        if (Output) { Output.Release(); UnityEngine.Object.DestroyImmediate(Output); }
        Output = null;
    }

    public void Dispose()
    {
        if (bloom) bloom.enabled = false;
        if (flow) flow.enabled = false;
        if (fieldCommands != null)
        {
            if (camera) camera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, fieldCommands);
            fieldCommands.Release(); fieldCommands = null;
        }
        if (field) field.ReleaseVisualPreview();
        ReleaseOutput();
        if (root) UnityEngine.Object.DestroyImmediate(root);
        if (fieldMaterial) UnityEngine.Object.DestroyImmediate(fieldMaterial);
        root = null; fieldMaterial = null;
    }
}

public sealed class DynamoVisualPreview : EditorWindow
{
    [SerializeField] private DynamoSelectiveBloom source;
    [SerializeField] private float progress = .45f, duration = 24f, peak = .35f;
    [SerializeField] private Vector2 direction = Vector2.left;
    [SerializeField] private int width = 1920;
    [SerializeField] private bool animateMotion;
    private DynamoVisualPreviewSession session;
    private SerializedObject flowSettings, bloomSettings;
    private Vector2 scroll;
    private double nextRefresh, lastTick;
    private float motionOffset;
    private string error;

    [MenuItem("MASSIVE/Dynamo/Visual Preview")]
    public static void OpenMenu()
    {
        var found = FindSceneSource();
        if (found) { Open(found); return; }
        GetWindow<DynamoVisualPreview>("Dynamo Visual Preview").Show();
    }

    private static DynamoSelectiveBloom FindSceneSource()
    {
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<DynamoSelectiveBloom>(FindObjectsSortMode.None))
        {
            if ((candidate.hideFlags & HideFlags.DontSave) != 0 || (candidate.gameObject.hideFlags & HideFlags.DontSave) != 0) continue;
            if (candidate.gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene()) continue;
            return candidate;
        }
        return null;
    }

    public static void Open(DynamoSelectiveBloom selectedSource)
    {
        var window = GetWindow<DynamoVisualPreview>("Dynamo Visual Preview");
        window.minSize = new Vector2(820, 540);
        if (!window.source || window.source != selectedSource)
        {
            window.StopPreview(); window.source = selectedSource;
            if (selectedSource && selectedSource.flow && selectedSource.flow.controller)
            {
                window.duration = selectedSource.flow.controller.PreviewDuration;
                window.peak = selectedSource.flow.controller.PreviewPeak;
                if (selectedSource.targetCamera) window.width = Mathf.Clamp(selectedSource.targetCamera.pixelWidth, 320, 4096);
            }
        }
        // A Play Mode transition can leave a destroyed managed wrapper with the
        // same instance ID as its restored scene object. Always take the live wrapper.
        window.source = selectedSource;
        window.flowSettings = window.bloomSettings = null;
        window.Show(); window.Focus();
    }

    private void OnEnable()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
    }
    private void OnDisable()
    {
        StopPreview();
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
    }
    private void PlayModeChanged(PlayModeStateChange state)
    {
        StopPreview();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            source = FindSceneSource();
            flowSettings = bloomSettings = null;
        }
    }

    public void StartPreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        StopPreview(); error = null;
        if (!source) source = FindSceneSource();
        try
        {
            session = new DynamoVisualPreviewSession(source);
            lastTick = EditorApplication.timeSinceStartup; nextRefresh = 0;
            Tick();
        }
        catch (Exception ex) { error = ex.Message; StopPreview(); }
        Repaint();
    }

    public void StopPreview()
    {
        session?.Dispose(); session = null;
        motionOffset = 0; animateMotion = false;
        Repaint();
    }

    private void Tick()
    {
        if (session == null) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || !source || !source.gameObject.scene.isLoaded)
        {
            StopPreview(); return;
        }
        double now = EditorApplication.timeSinceStartup;
        if (now < nextRefresh) return;
        if (animateMotion) motionOffset += (float)Math.Min(.1, now - lastTick);
        lastTick = now; nextRefresh = now + (animateMotion ? 1.0 / 30 : .1);
        try { session.Render(progress * duration, duration, peak, direction, motionOffset, width); }
        catch (Exception ex) { error = ex.Message; StopPreview(); }
        Repaint();
    }

    private void OnGUI()
    {
        if (!source && !EditorApplication.isPlayingOrWillChangePlaymode) source = FindSceneSource();
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(310)))
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUI.BeginChangeCheck();
                var chosen = (DynamoSelectiveBloom)EditorGUILayout.ObjectField("Dynamo Bloom", source, typeof(DynamoSelectiveBloom), true);
                if (EditorGUI.EndChangeCheck()) { StopPreview(); source = chosen; flowSettings = bloomSettings = null; }
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || !source))
                {
                    if (GUILayout.Button(session == null ? "Start Visual Preview" : "Stop Visual Preview", GUILayout.Height(28)))
                    {
                        if (session == null) StartPreview(); else StopPreview();
                    }
                }
                EditorGUILayout.HelpBox("Preview controls affect temporary visuals only. Appearance changes below edit the scene and support Undo.", MessageType.Info);
                EditorGUILayout.LabelField("Storm moment", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                progress = EditorGUILayout.Slider("Progress", progress, 0, 1);
                duration = Mathf.Max(.5f, EditorGUILayout.FloatField("Duration (seconds)", duration));
                peak = EditorGUILayout.Slider("Peak strength", peak, 0, 1);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Calm")) progress = 0;
                    if (GUILayout.Button("Arrival")) progress = Mathf.Clamp01(.7f / duration);
                    if (GUILayout.Button("Peak")) progress = .5f;
                    if (GUILayout.Button("Recovery")) progress = .9f;
                }
                direction = EditorGUILayout.Vector2Field("Comes from (screen X/Y)", direction);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Left")) direction = Vector2.left;
                    if (GUILayout.Button("Right")) direction = Vector2.right;
                    if (GUILayout.Button("Top")) direction = Vector2.up;
                    if (GUILayout.Button("Bottom")) direction = Vector2.down;
                }
                if (EditorGUI.EndChangeCheck()) { motionOffset = 0; nextRefresh = 0; }
                animateMotion = EditorGUILayout.Toggle("Animate at held moment", animateMotion);
                if (GUILayout.Button("Reset motion")) { motionOffset = 0; nextRefresh = 0; }
                width = EditorGUILayout.IntPopup("Preview width", width, new[] { "960 px", "1920 px", "3840 px" }, new[] { 960, 1920, 3840 });
                if (session != null)
                    EditorGUILayout.LabelField($"{progress * duration:F2} / {duration:F1} seconds\nStrength {session.State.Strength:P0}   Pressure {session.Pressure:F2}", GUILayout.Height(36));
                DrawAppearance();
                EditorGUILayout.EndScrollView();
            }
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Field + storm flow", EditorStyles.boldLabel);
                Rect imageRect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                EditorGUI.DrawRect(imageRect, new Color(.035f, .035f, .035f));
                if (session?.Output) EditorGUI.DrawPreviewTexture(imageRect, session.Output, null, ScaleMode.ScaleToFit);
                else GUI.Label(imageRect, "Start Visual Preview to inspect the storm without entering Play Mode.", EditorStyles.centeredGreyMiniLabel);
                if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
                EditorGUILayout.LabelField("Visual preview only • no players, damage or match clock are simulated", EditorStyles.miniLabel);
            }
        }
    }

    private void DrawAppearance()
    {
        if (!source || !source.flow) return;
        if (flowSettings == null || flowSettings.targetObject != source.flow) flowSettings = new SerializedObject(source.flow);
        if (bloomSettings == null || bloomSettings.targetObject != source) bloomSettings = new SerializedObject(source);
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Flow appearance (scene settings)", EditorStyles.boldLabel);
        flowSettings.Update();
        foreach (string property in new[] { "sharpCoreIntensity", "opaqueTaperedStreaks", "streakLength", "streakWidth", "color", "brightness" })
            EditorGUILayout.PropertyField(flowSettings.FindProperty(property));
        if (flowSettings.ApplyModifiedProperties()) nextRefresh = 0;
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Bloom (scene settings)", EditorStyles.boldLabel);
        bloomSettings.Update();
        EditorGUILayout.PropertyField(bloomSettings.FindProperty("fieldBloom"), true);
        EditorGUILayout.PropertyField(bloomSettings.FindProperty("flowBloom"), true);
        if (bloomSettings.ApplyModifiedProperties()) nextRefresh = 0;
        if (GUILayout.Button("Select field for detailed tuning")) Selection.activeObject = source.field;
    }
}

[CustomEditor(typeof(DynamoSelectiveBloom)), CanEditMultipleObjects]
public sealed class DynamoSelectiveBloomInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        using (new EditorGUI.DisabledScope(targets.Length != 1 || EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Open Visual Preview")) DynamoVisualPreview.Open((DynamoSelectiveBloom)target);
    }
}
#endif
