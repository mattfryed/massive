#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Text;
using System.Linq;
using Unity.Profiling;
using Massive.Dynamo;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit diagnostics in the open Dynamo scene. All Run() settings restore in finally.</summary>
[InitializeOnLoad]
public static class DynamoFlowBlanketValidation
{
    private static ProfilerRecorder flowTime;
    static DynamoFlowBlanketValidation()
    {
        AssemblyReloadEvents.beforeAssemblyReload += () => flowTime.Dispose();
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode) flowTime.Dispose(); };
    }
    public static string BeginCapture(string label)
    {
        flowTime.Dispose();
        flowTime = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Dynamo.FlowBlanket.Update", 300);
        return DynamoOrganicFieldValidation.BeginCapture(label);
    }
    public static string EndCapture()
    {
        flowTime.Stop();
        var samples = flowTime.ToArray().Select(s => s.Value * 1e-6).Where(s => s > 0).OrderBy(s => s).ToArray();
        string result = samples.Length > 0 ? $"Flow CPU: n={samples.Length}, median={samples[samples.Length/2]:F3} ms; " : "Flow CPU unavailable; ";
        flowTime.Dispose();
        return result + DynamoOrganicFieldValidation.EndCapture();
    }
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static object Get(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
    private static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
    private static void Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    // Runtime preview only: a held storm for visual inspection and matched profiling.
    public static string Preview(Vector2 source, float strength)
    {
        Require(Application.isPlaying, "Play Mode required");
        var c = UnityEngine.Object.FindFirstObjectByType<DynamoStormController>();
        foreach (var b in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (b.GetType().Name == "GameManagerScript") b.enabled = false;
        Set(c, "randomizeDirectionEachStorm", false);
        c.SetScreenSourceDirection(source);
        Call(c, "StartStorm", Time.time);
        Set(c, "_stormTotal", 10000f); Set(c, "_easeIn", 0f); Set(c, "_hold", 9999f); Set(c, "_easeOut", .5f);
        Set(c, "_stormTimer", 8f); Set(c, "_stormPeak01", strength); Set(c, "showDebugUI", false);
        Call(c, "AdvanceStorm", 0f, Time.time);
        var renderer = UnityEngine.Object.FindFirstObjectByType<DynamoFlowBlanketRenderer>(FindObjectsInactive.Include);
        if (c.UseFlowBlanket && renderer) { renderer.enabled = false; renderer.enabled = true; }
        return "Held storm preview: source=" + source + ", strength=" + strength + ". Exit Play Mode to restore scene settings.";
    }

    [MenuItem("MASSIVE/Dynamo/Validate Flow Blanket (Play Mode)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        Require(Application.isPlaying, "Play Mode required");
        var c = UnityEngine.Object.FindFirstObjectByType<DynamoStormController>();
        var r = UnityEngine.Object.FindFirstObjectByType<DynamoFlowBlanketRenderer>();
        Require(c && r && c.UseFlowBlanket, "Open Dynamo with flow blanket enabled");
        var field = c.GameplayField;
        string fieldJson = JsonUtility.ToJson(field);
        FieldInfo[] fields = c.GetType().GetFields(Flags);
        var values = new object[fields.Length];
        for (int i = 0; i < fields.Length; i++) values[i] = fields[i].GetValue(c);
        float timeScale = Time.timeScale;
        int oldPaths = r.maxPaths;
        bool wasEnabled = r.enabled;
        var report = new StringBuilder();
        try
        {
            Time.timeScale = 0;
            Set(c, "_nextStormStartTime", float.MaxValue);
            Set(c, "randomizeDirectionEachStorm", false);
            c.SetScreenSourceDirection(Vector2.left);
            Call(c, "StartStorm", Time.time);
            float begin = c.StormElapsed;
            Call(c, "AdvanceStorm", .125f, Time.time);
            Require(Mathf.Abs(c.StormElapsed - begin - .125f) < .00001f, "Storm clock must advance exactly once");
            begin = c.StormElapsed;
            Call(c, "AdvanceStorm", 0f, Time.time);
            Require(c.StormElapsed == begin, "Paused clock must not advance");
            Require(c.StormDuration >= 22 && c.StormDuration <= 26, "Reversed 26/22 duration range must resolve to 22..26 seconds");

            Set(c, "_stormTimer", 0f); Call(c, "PublishFlowState");
            var fp = c.CurrentState.Footprint;
            Require(c.CurrentState.VisibilityAt(fp.BottomLeft) == 0 && c.CurrentState.VisibilityAt(fp.TopRight) == 0, "No visible front before arrival");
            Set(c, "_stormTimer", .4f); Call(c, "PublishFlowState");
            Require(c.CurrentState.VisibilityAt(fp.BottomLeft) > .9f && c.CurrentState.VisibilityAt(fp.BottomRight) == 0, "Front enters promptly from the assigned edge");
            Set(c, "_stormTimer", 2f); Call(c, "PublishFlowState");
            Require(c.CurrentState.VisibilityAt(fp.BottomRight) > .99f, "Front fills view after crossing duration");

            int samples = 0, minCount = int.MaxValue, maxCount = 0;
            float worstCoverage = 1;
            for (int direction = 0; direction < 16; direction++)
            {
                float angle = direction * Mathf.PI / 8;
                foreach (float pressure in new[] { 0f, .35f, 1f })
                {
                    c.SetScreenSourceDirection(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
                    Set(c, "_stormTimer", 8f); Set(c, "_directionStarted", 0f); Set(c, "_stormStrength01", .6f); Set(c, "_stormTotal", 20f);
                    field.SetExternalStorm(c.GetStormFlowDirWS(), pressure);
                    Call(c, "PublishFlowState");
                    Call(r, "RenderFrame");
                    var layout = Get(r, "current");
                    int lanes = (int)Get(layout, "lanes"), nodes = r.samplesPerPath;
                    var data = new Vector4[lanes * nodes]; ((ComputeBuffer)Get(layout, "paths")).GetData(data);
                    for (int i = 0; i < data.Length; i++)
                    {
                        var p = data[i];
                        Require(Finite(p.x) && Finite(p.y) && Finite(p.z) && Finite(p.w), "Path positions must be finite");
                        Require(field.GetGameplayEnvelopeQ(p) >= .999f, "Path must stay outside shared envelope");
                        if (i % nodes != 0) Require(p.w >= data[i-1].w, "Arc length must be monotonic");
                    }
                    int count = Count(layout);
                    Require(count > 50 && count <= r.maxPaths * r.maxStreaksPerPath, "Bounded visible streak count");
                    minCount = Mathf.Min(minCount, count); maxCount = Mathf.Max(maxCount, count);
                    var streaks = new float[count * 7]; ((ComputeBuffer)Get(layout, "streaks")).GetData(streaks);
                    var occupied = new bool[32];
                    for (int i = 0; i < count; i++)
                    {
                        var p = new Vector3(streaks[i*7+3], streaks[i*7+4], streaks[i*7+5]);
                        var v = c.StormCamera.WorldToViewportPoint(p);
                        Require(v.x >= -.01 && v.x <= 1.01 && v.y >= -.01 && v.y <= 1.01, "Streak heads must be in the viewport");
                        int x = Mathf.Clamp((int)(v.x * 8), 0, 7), y = Mathf.Clamp((int)(v.y * 4), 0, 3);
                        occupied[y*8+x] = true;
                    }
                    int wanted = 0, filled = 0;
                    for (int y = 0; y < 4; y++) for (int x = 0; x < 8; x++)
                    {
                        Vector3 p = Vector3.Lerp(Vector3.Lerp(fp.BottomLeft, fp.BottomRight, (x+.5f)/8), Vector3.Lerp(fp.TopLeft, fp.TopRight, (x+.5f)/8), (y+.5f)/4);
                        if (field.GetGameplayEnvelopeQ(p) > 1.1f) { wanted++; if (occupied[y*8+x]) filled++; }
                    }
                    float coverage = wanted > 0 ? filled / (float)wanted : 1;
                    worstCoverage = Mathf.Min(worstCoverage, coverage);
                    Require(coverage >= .9f, "Visible exterior coverage has a gap at angle " + direction + ", pressure=" + pressure + ", coverage=" + coverage);
                    samples += data.Length;
                }
            }
            report.Append($"48 direction/pressure cases; {samples} finite exterior path samples; {minCount}..{maxCount} visible streaks; exterior grid coverage >= {worstCoverage:P0}. ");

            // Rapid reversals may keep only one outgoing layout, and must expire on the controller clock.
            c.SetScreenSourceDirection(Vector2.left); Call(r, "RenderFrame");
            Set(c, "_stormTimer", 8.1f); c.SetScreenSourceDirection(Vector2.right); Call(r, "RenderFrame");
            Require((bool)Get(Get(r, "outgoing"), "valid"), "Reversal keeps an outgoing layout for its short fade");
            Require(Vector3.Dot(c.CurrentState.Flow, (Vector3.right * -1)) > .99f, "A right-side source must flow left");
            Set(c, "_stormTimer", 8.2f); c.SetScreenSourceDirection(Vector2.up); Call(r, "RenderFrame");
            Set(c, "_stormTimer", 8.6f); Call(c, "PublishFlowState"); Call(r, "RenderFrame");
            Require(!(bool)Get(Get(r, "outgoing"), "valid"), "Outgoing flow must expire without waiting for particle lifetimes");
            var legacy = UnityEngine.Object.FindFirstObjectByType<StormWindFlowVisualizer>(FindObjectsInactive.Include);
            Require(!legacy.enabled && legacy.GetComponent<ParticleSystem>().particleCount == 0, "Legacy simulation must be stopped");

            // Both formats of viewport, without changing the scene camera.
            var cameraObject = new GameObject("Dynamo validation camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.transform.SetPositionAndRotation(c.StormCamera.transform.position, c.StormCamera.transform.rotation);
                camera.orthographic = true; camera.orthographicSize = 9;
                foreach (float aspect in new[] { .5625f, 1.777778f, 3.33f })
                {
                    camera.aspect = aspect;
                    var view = DynamoStormFootprint.FromCamera(camera, 0, Vector3.zero, Vector2.one);
                    Require(Vector3.Distance(view.BottomLeft, view.BottomRight) > 0, "Valid camera footprint");
                    Set(c, "stormCamera", camera); Call(c, "PublishFlowState"); Call(r, "RenderFrame");
                    Require(Count(Get(r, "current")) > 0, "Portrait/landscape/ultrawide must produce visible flow");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(cameraObject); Set(c, "stormCamera", values[Array.FindIndex(fields, f => f.Name == "stormCamera")]); }

            Call(c, "PublishFlowState"); Call(r, "RenderFrame");
            int allocations = r.BufferAllocationCount;
            var render = (Action)Delegate.CreateDelegate(typeof(Action), r, r.GetType().GetMethod("RenderFrame", Flags));
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 32; i++) render();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Require(r.BufferAllocationCount == allocations, "Steady frames must retain buffers");
            report.Append($"Stable buffers; {bytes} managed bytes over 32 frames. ");
            r.maxPaths = oldPaths + 8; render(); Require(r.BufferAllocationCount == allocations + 1, "Capacity change reallocates once");
            r.maxPaths = oldPaths; render();
            r.enabled = false; Require(!r.HasBuffers, "Disable must release GPU resources");
            r.enabled = true; render(); Require(Count(Get(r, "current")) > 0, "Re-enable must recover drawing");
            Set(c, "_stormTimer", c.StormDuration); Call(c, "AdvanceStorm", .1f, Time.time);
            Require(!c.CurrentState.Active && c.GetStormHazard01(new Vector3(-100,0,0)) == 0, "End must clear visuals and damage");
            render(); Require(!(bool)Get(Get(r, "current"), "valid"), "No outgoing layout survives a stopped storm");
            report.Append("Clock, pause, front, duration range, rapid reversals, legacy shutdown, viewport sizes, resize, disable/re-enable, and stop passed.");
            return "[Dynamo flow blanket] PASS: " + report;
        }
        finally
        {
            for (int i = 0; i < fields.Length; i++) if (!fields[i].IsInitOnly) fields[i].SetValue(c, values[i]);
            JsonUtility.FromJsonOverwrite(fieldJson, field);
            r.maxPaths = oldPaths; r.enabled = wasEnabled;
            Time.timeScale = timeScale;
        }
    }

    private static int Count(object layout)
    {
        var count = new uint[1]; ((ComputeBuffer)Get(layout, "count")).GetData(count);
        var args = new uint[4]; ((ComputeBuffer)Get(layout, "args")).GetData(args);
        Require(args[0] == count[0] * 6 && args[1] == 1 && args[2] == 0 && args[3] == 0, "Indirect draw count must match output");
        return (int)count[0];
    }
    private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
}
#endif
