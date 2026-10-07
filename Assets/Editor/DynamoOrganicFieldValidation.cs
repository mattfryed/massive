#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit Play Mode diagnostics; no scene changes are saved.</summary>
[InitializeOnLoad]
public static class DynamoOrganicFieldValidation
{
    private static ProfilerRecorder fieldTime, mainTime, gpuTime;
    private static string captureLabel;
    private static MagnetosphereFieldLinesGPU2D captureField;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static DynamoOrganicFieldValidation()
    {
        AssemblyReloadEvents.beforeAssemblyReload += DisposeCapture;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode) DisposeCapture();
        };
    }

    public static string BeginCapture(string label)
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play Mode required.");
        DisposeCapture();
        captureLabel = label;
        captureField = UnityEngine.Object.FindFirstObjectByType<MagnetosphereFieldLinesGPU2D>();
        Require(captureField, "Open the Dynamo scene before capturing.");
        fieldTime = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Dynamo.OrganicField.Update", 300);
        mainTime = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 300);
        gpuTime = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 300);
        return "Capturing " + label + "; enable CPU/GPU profiling, allow several seconds to settle, then end. Zero Editor samples are excluded.";
    }

    public static string EndCapture()
    {
        if (fieldTime.Valid) fieldTime.Stop();
        if (mainTime.Valid) mainTime.Stop();
        if (gpuTime.Valid) gpuTime.Stop();
        string result = (captureField ? "" : "INVALID: field/scene disappeared during capture. ") + captureLabel + ": " + Stats("field CPU submission", fieldTime) + "; " +
            Stats("whole-frame main thread", mainTime) + "; " + Stats("whole-frame GPU", gpuTime);
        DisposeCapture();
        return result;
    }

    private static string Stats(string label, ProfilerRecorder recorder)
    {
        if (!recorder.Valid) return label + " unavailable";
        var values = recorder.ToArray().Select(x => x.Value * 1e-6).Where(x => x > 0).OrderBy(x => x).ToArray();
        if (values.Length == 0) return label + " unavailable";
        return $"{label}: n={values.Length}, mean={values.Average():F3} ms, median={values[values.Length / 2]:F3} ms, p95={values[Math.Min(values.Length - 1, (int)(values.Length * .95))]:F3} ms";
    }

    private static void DisposeCapture()
    {
        fieldTime.Dispose(); mainTime.Dispose(); gpuTime.Dispose();
    }

    public static ComputeBuffer Buffer(MagnetosphereFieldLinesGPU2D field, string name) =>
        (ComputeBuffer)typeof(MagnetosphereFieldLinesGPU2D).GetField(name, Private).GetValue(field);

    public static void Tick(MagnetosphereFieldLinesGPU2D field) =>
        typeof(MagnetosphereFieldLinesGPU2D).GetMethod("Update", Private).Invoke(field, null);

    public static int SegmentCount(MagnetosphereFieldLinesGPU2D field)
    {
        var count = new uint[1]; Buffer(field, "_segCountBuf").GetData(count);
        var args = new uint[4]; Buffer(field, "_argsBuf").GetData(args);
        Require(args[0] == count[0] * 2 && args[1] == 1 && args[2] == 0 && args[3] == 0,
            "GPU indirect arguments exactly match the append count");
        return (int)count[0];
    }

    [MenuItem("MASSIVE/Dynamo/Validate Organic Field (Play Mode)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        Require(Application.isPlaying, "Play Mode required.");
        var f = UnityEngine.Object.FindFirstObjectByType<MagnetosphereFieldLinesGPU2D>();
        Require(f && (!f.scientificRenderer || !f.scientificRenderer.CanRenderFor(f)), "Organic renderer must be active");
        int seeds = f.seedCount;
        bool assist = f.forceReturnToPoles, depth = f.coherentLoopDepth, pseudo = f.pseudo3DEnabled;
        float minMag = f.minFieldMag, pressure = f.pressureGain;
        Vector3 wind = f.solarWindDirection;
        try
        {
            Tick(f);
            var buffer = Buffer(f, "_segBuf");
            int allocations = f.BufferAllocationCount;
            var tick = (Action)Delegate.CreateDelegate(typeof(Action), f,
                typeof(MagnetosphereFieldLinesGPU2D).GetMethod("Update", Private));
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 32; i++) tick();
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Require(ReferenceEquals(buffer, Buffer(f, "_segBuf")) && allocations == f.BufferAllocationCount,
                "Stable frames retain the same buffers");
            int count = SegmentCount(f);
            Require(count > 1000, "Dense organic field draws");

            f.seedCount = seeds + 8; Tick(f);
            Require(f.BufferAllocationCount == allocations + 1, "Seed change reallocates once");
            f.seedCount = seeds; Tick(f);
            f.forceReturnToPoles = false; f.minFieldMag = float.MaxValue; Tick(f);
            Require(SegmentCount(f) == 0, "An empty field clears the indirect draw count");
            f.forceReturnToPoles = assist; f.minFieldMag = minMag; Tick(f);
            Require(SegmentCount(f) > 1000, "Drawing recovers after empty output");

            f.coherentLoopDepth = true; f.pseudo3DEnabled = true;
            int samples = 0;
            foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, new Vector3(1, 0, 1).normalized })
            foreach (float drive in new[] { 0f, .35f, 1f })
            {
                f.solarWindDirection = direction; f.pressureGain = drive; Tick(f);
                int n = SegmentCount(f);
                buffer = Buffer(f, "_segBuf");
                int stride = buffer.stride / 4;
                var data = new float[n * stride]; buffer.GetData(data, 0, 0, data.Length);
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int i = 0; i < n; i++)
                {
                    for (int end = 0; end < 2; end++)
                    {
                        int j = i * stride + end * 3;
                        var p = new Vector3(data[j], data[j + 1], data[j + 2]);
                        Require(Finite(p.x) && Finite(p.y) && Finite(p.z), "Finite coherent loop positions");
                        minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                        Vector3 r = p - f.dipolePosition; r.y = 0;
                        float along = Vector3.Dot(r, f.EffectiveWindDirection);
                        float across = (r - f.EffectiveWindDirection * along).magnitude;
                        f.GetGameplayEnvelopeScales(along, out float a, out float b);
                        float q = new Vector2(along / (a * f.magnetosphereRadius), across / (b * f.magnetosphereRadius)).magnitude;
                        if (q > 1.002f) throw new InvalidOperationException($"Depth outside envelope: q={q}, drive={drive}, wind={direction}, point={p}");
                    }
                }
                Require(minY < f.dipolePosition.y - .1f && maxY > f.dipolePosition.y + .1f, "Loops have front and rear depth");
                samples += n;
            }
            f.enabled = false;
            Require(Buffer(f, "_segBuf") == null && Buffer(f, "_argsBuf") == null, "Disable releases buffers");
            f.enabled = true; Tick(f);
            Require(SegmentCount(f) > 1000, "Re-enable recovers drawing");
            return $"[Dynamo organic] PASS: {count} base segments; stable buffers; {allocatedBytes} managed bytes over 32 updates; " +
                $"GPU counts, resizing, empty output, lifecycle, and {samples} segments across 12 depth/pressure/direction cases.";
        }
        finally
        {
            f.seedCount = seeds; f.forceReturnToPoles = assist; f.minFieldMag = minMag;
            f.coherentLoopDepth = depth; f.pseudo3DEnabled = pseudo; f.pressureGain = pressure; f.solarWindDirection = wind;
            f.enabled = true; Tick(f);
        }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // Sort because concurrent GPU append order is deliberately unspecified.
    public static string[] ReadSegments(MagnetosphereFieldLinesGPU2D field, int count)
    {
        var buffer = Buffer(field, "_segBuf");
        var floats = new float[count * (buffer.stride / 4)];
        buffer.GetData(floats, 0, 0, floats.Length);
        int stride = buffer.stride / 4;
        var lines = new string[count];
        for (int i = 0; i < count; i++)
            lines[i] = string.Join(",", Enumerable.Range(0, 7).Select(j => floats[i * stride + j].ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        Array.Sort(lines, StringComparer.Ordinal);
        return lines;
    }
}
#endif
