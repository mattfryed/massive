#if UNITY_EDITOR
using System;
using System.Reflection;
using Massive.Dynamo;
using UnityEditor;
using UnityEngine;

/// <summary>Checks the actual buffers used by the hybrid field without changing storm/gameplay state.</summary>
public static class DynamoGameplayFieldValidation
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    [MenuItem("MASSIVE/Dynamo/Validate Gameplay Scientific Field (Play Mode)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        Require(Application.isPlaying, "Enter Play Mode in S-6_DYNAMO first.");
        var renderer = UnityEngine.Object.FindFirstObjectByType<DynamoScientificFieldRenderer>();
        Require(renderer && renderer.Source && renderer.Source.Ready, "Scientific source ready");
        var field = renderer.GameplayField;
        Require(field && field.scientificRenderer == renderer && renderer.CanRenderFor(field), "Replacement wiring");
        Require(!renderer.Source.Playing, "Gameplay uses a fixed recorded reference, not a competing timeline");
        Require(ReadBuffer(field, "_segBuf") == null, "Legacy GPU drawing released while scientific presentation is active");

        var buffer = ReadBuffer(renderer, "segments");
        Require(buffer != null && buffer.count == renderer.SegmentCapacity, "Scientific GPU allocation");
        var points = new Vector4[buffer.count * 2];
        buffer.GetData(points);
        var serialized = new SerializedObject(renderer);
        float step = serialized.FindProperty("stepRe").floatValue;
        int steps = serialized.FindProperty("steps").intValue;
        int valid = 0, directionChecks = 0;
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < points.Length; i += 2)
        {
            if (points[i + 1].w == 0) continue;
            Vector3 a = points[i], b = points[i + 1];
            Require(Finite(a) && Finite(b), "Finite GPU positions");
            Require(Mathf.Abs(Vector3.Distance(a, b) - step) < .001f, "Bounded reconstructed-field trace steps");
            Require(a.magnitude >= renderer.Source.Episode.manifest.innerBoundaryRe &&
                    b.magnitude >= renderer.Source.Episode.manifest.innerBoundaryRe, "Excluded inner domain respected");
            minY = Mathf.Min(minY, a.y); maxY = Mathf.Max(maxY, a.y);
            // Check the GPU tangent against an independent CPU reconstruction of the same physical fields.
            if (valid % 113 == 0)
            {
                Require(renderer.TrySampleReconstructedField((a + b) * .5f, out var sample), "GPU midpoint available on CPU");
                float sign = (((i / 2) / steps) % 2) == 0 ? 1 : -1;
                Require(Vector3.Dot((b - a).normalized, sample.normalized * sign) > .98f,
                    "GPU line tangent agrees with independent CPU magnetic sample");
                directionChecks++;
            }
            valid++;
        }
        Require(valid > 1000 && maxY - minY > 5, "Volumetric reconstructed field");

        // Storm pressure now changes the reconstructed field, so it must be retraced, not endpoint-morphed.
        float oldPressure = field.pressureGain;
        try
        {
            field.pressureGain = 0;
            Invoke(renderer, "LateUpdate");
            Require(renderer.ExternalDisturbance01 == 0 && renderer.ExtraCompression01 == 0, "Calm removes all recorded disturbance");
            int dispatches = renderer.TraceDispatchCount;
            field.GetGameplayEnvelopeScales(-5, out float calmA, out float calmB);
            field.pressureGain = 1;
            field.GetGameplayEnvelopeScales(-5, out float stormA, out float stormB);
            Require(stormA < calmA && stormB < calmB, "Dayside and flank respond to storm pressure");
            field.GetGameplayEnvelopeScales(20, out float tailA, out _);
            Require(tailA > 1, "Downstream envelope stretches");
            Invoke(renderer, "LateUpdate");
            Require(renderer.TraceDispatchCount > dispatches && renderer.ExternalDisturbance01 == 1,
                "Storm retraces the reconstructed field and restores the recorded residual");
        }
        finally { field.pressureGain = oldPressure; Invoke(renderer, "LateUpdate"); }

        // Read the actual CPU-to-GPU envelope table and check it against the particle/hazard authority.
        var scalesBuffer = ReadBuffer(renderer, "envelopeBuffer");
        var scales = new Vector4[scalesBuffer.count]; scalesBuffer.GetData(scales);
        float extent = Mathf.Max(1, field.magnetosphereRadius * 4);
        for (int i = 0; i < scales.Length; i++)
        {
            field.GetGameplayEnvelopeScales(Mathf.Lerp(-extent, extent, i / (float)(scales.Length - 1)), out float a, out float b);
            Require(Vector2.Distance(new Vector2(scales[i].x, scales[i].y), new Vector2(a, b)) < .00001f, "GPU envelope agrees with gameplay authority");
            if (i > 0) Require(scales[i].z > scales[i - 1].z, "Axial deformation is monotonic");
        }
        return $"[Dynamo gameplay field] PASS: {valid} finite volumetric segments; {directionChecks} independent reconstructed-field tangent checks; " +
               "excluded domain, pressure-driven retracing, compression, flank pinch, tail stretch and 257 envelope samples verified.";
    }

    [MenuItem("MASSIVE/Dynamo/Validate Destressed Calm Field (Play Mode)")]
    public static void RunCalmMenu() => Debug.Log(ValidateDestressedBaseline());

    public static string ValidateDestressedBaseline()
    {
        Require(Application.isPlaying, "Play Mode required");
        var renderer = UnityEngine.Object.FindFirstObjectByType<DynamoScientificFieldRenderer>();
        Require(renderer && renderer.Source.Ready, "Live integration required");
        var field = renderer.GameplayField;
        float oldPressure = field.pressureGain;
        Vector3 oldWind = field.solarWindDirection;
        Vector3 oldDisplay = renderer.DisplayWind;
        try
        {
            field.pressureGain = 0;
            field.solarWindDirection = Vector3.right;
            Invoke(field, "Update"); Invoke(renderer, "LateUpdate");
            Require(renderer.ExternalDisturbance01 == 0, "No recorded residual at zero drive");
            var buffer = ReadBuffer(renderer, "segments");
            var first = new Vector4[buffer.count * 2]; buffer.GetData(first);
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            int valid = 0;
            for (int i = 0; i < first.Length; i += 2)
            {
                if (first[i + 1].w == 0) continue;
                Vector3 p = first[i];
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                // r / sin(theta)^2 is constant along a centered dipole line.
                Vector3 next = first[i + 1];
                float cosA = Vector3.Dot(p.normalized, renderer.CalmDipoleAxis);
                float cosB = Vector3.Dot(next.normalized, renderer.CalmDipoleAxis);
                float shellA = p.magnitude / Mathf.Max(.0001f, 1 - cosA * cosA);
                float shellB = next.magnitude / Mathf.Max(.0001f, 1 - cosB * cosB);
                Require(Mathf.Abs(shellA - shellB) / shellA < .004f, "Calm follows the analytic dipole-shell invariant");
                valid++;
            }
            Require(valid > 1000 && Mathf.Abs(minX + maxX) < .002f && Mathf.Abs(minZ + maxZ) < .002f,
                "Calm GPU geometry has no pre-compressed side or swept-back tail");
            int before = renderer.TraceDispatchCount;
            field.solarWindDirection = Vector3.left;
            Invoke(field, "Update"); Invoke(renderer, "LateUpdate");
            var second = new Vector4[first.Length]; ReadBuffer(renderer, "segments").GetData(second);
            Require(before == renderer.TraceDispatchCount, "Wind changes cannot change the zero-drive field");
            for (int i = 0; i < first.Length; i++) Require(first[i] == second[i], "Calm geometry unchanged by reversed wind");
            field.pressureGain = new SerializedObject(renderer).FindProperty("recordedDisturbanceAtPressureGain").floatValue * .5f;
            Invoke(renderer, "LateUpdate");
            Require(renderer.ExternalDisturbance01 > 0 && renderer.ExternalDisturbance01 < 1 && renderer.ExtraCompression01 == 0,
                "Partial stress restores residual without applying a second compression");
            field.pressureGain = 0;
            Invoke(renderer, "LateUpdate");
            ReadBuffer(renderer, "segments").GetData(second);
            for (int i = 0; i < first.Length; i++) Require(first[i] == second[i], "Recovery returns to the same intrinsic calm field");
            return $"[Dynamo destress] PASS: {valid} analytic dipole segments; symmetric bounds; wind-independent calm; residual ramp; exact recovery.";
        }
        finally
        {
            field.pressureGain = oldPressure; field.solarWindDirection = oldWind;
            typeof(DynamoScientificFieldRenderer).GetField("displayWind", PrivateInstance).SetValue(renderer, oldDisplay);
            Invoke(field, "Update"); Invoke(renderer, "LateUpdate");
        }
    }

    public static string ValidateLifecycle()
    {
        Require(Application.isPlaying, "Play Mode required");
        var renderer = UnityEngine.Object.FindFirstObjectByType<DynamoScientificFieldRenderer>();
        Require(renderer && renderer.Source.Ready, "Live integration required");
        var field = renderer.GameplayField;
        try
        {
            renderer.enabled = false;
            Require(renderer.SegmentCapacity == 0 && !renderer.CanRenderFor(field), "Renderer disable releases resources");
            Invoke(field, "Update");
            Require(ReadBuffer(field, "_segBuf") != null, "Original field resumes as fallback");
            renderer.enabled = true;
            Invoke(renderer, "LateUpdate"); Invoke(field, "Update");
            Require(renderer.SegmentCapacity > 0 && ReadBuffer(field, "_segBuf") == null, "Scientific field resumes exclusively");
            renderer.Source.enabled = false;
            Invoke(renderer, "LateUpdate"); Invoke(field, "Update");
            Require(renderer.SegmentCapacity == 0 && ReadBuffer(field, "_segBuf") != null, "Missing source safely falls back");
        }
        finally
        {
            renderer.Source.enabled = true;
            renderer.enabled = true;
            Invoke(renderer, "LateUpdate"); Invoke(field, "Update");
        }
        Require(renderer.Source.Ready && renderer.SegmentCapacity > 0, "Source reload restored");
        return "[Dynamo gameplay field] PASS: renderer disable/re-enable and source disable/reload; original fallback and buffer cleanup verified.";
    }

    private static ComputeBuffer ReadBuffer(object target, string name) =>
        (ComputeBuffer)target.GetType().GetField(name, PrivateInstance).GetValue(target);
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, PrivateInstance).Invoke(target, null);
    private static bool Finite(Vector3 p) => ScientificStormEpisode.Finite(p.x) && ScientificStormEpisode.Finite(p.y) && ScientificStormEpisode.Finite(p.z);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
