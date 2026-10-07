#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Massive.Dynamo;
using UnityEditor;
using UnityEngine;

/// <summary>Read back isolated GPU traces to check connectivity, draping and pole resolution.</summary>
public static class DynamoFieldStabilityValidation
{
    [MenuItem("MASSIVE/Dynamo/Validate Field Stability (Edit Mode)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        Require(!Application.isPlaying, "Edit Mode required.");
        DynamoSelectiveBloom source = null;
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<DynamoSelectiveBloom>(FindObjectsSortMode.None))
            if ((candidate.gameObject.hideFlags & HideFlags.DontSave) == 0) { source = candidate; break; }
        Require(source, "Open the Dynamo scene first.");
        string before = EditorJsonUtility.ToJson(source.field);
        bool dirty = source.gameObject.scene.isDirty;
        int samples = 0, cases = 0, worstReversals = 0, poleSamples = 0;
        using (var preview = new DynamoVisualPreviewSession(source))
        {
            foreach (var direction in new[] { Vector2.left, Vector2.up, new Vector2(-1, -1), new Vector2(1, -1) })
            foreach (float peak in new[] { 0f, .35f, .7f })
            {
                preview.Render(12, 24, peak, direction, 0, 640);
                var f = preview.Field;
                var data = Read(f);
                var ends = new HashSet<Vector3>();
                var starts = new Dictionary<Vector3, int>();
                for (int i = 0; i < data.Length; i += 7)
                {
                    ends.Add(Point(data, i + 3));
                    starts[Point(data, i)] = i;
                }
                int openStarts = 0, reversals = 0;
                for (int i = 0; i < data.Length; i += 7)
                {
                    Vector3 a = Point(data, i), b = Point(data, i + 3);
                    if (!ends.Contains(a)) openStarts++;
                    if (!starts.TryGetValue(b, out int next)) continue;
                    Vector3 u = b - a, v = Point(data, next + 3) - b;
                    if ((b - f.dipolePosition).sqrMagnitude < 1 || u.sqrMagnitude < .000001f || v.sqrMagnitude < .000001f) continue;
                    if (Vector3.Dot(u.normalized, v.normalized) < -.5f) reversals++;
                }
                Require(openStarts <= 2 * f.seedCount, $"Disconnected strands: {openStarts} starts for {f.seedCount} seeds at {direction}, peak {peak}.");
                Require(reversals < Math.Max(8, data.Length / 7000), $"Boundary tracing repeatedly reverses: {reversals} at {direction}, {peak}.");
                worstReversals = Math.Max(worstReversals, reversals);
                samples += data.Length / 7; cases++;
            }

            // A step larger than the tiny planet used to skip its termination
            // region and launch a second, highly unstable vertical strand.
            var field = preview.Field;
            field.seedCount = 16; field.useProceduralSeeds = false;
            field.pseudo3DEnabled = false; field.pathWarbleStrength = 0;
            field.warpAmount = 0; field.pressureGain = 0;
            field.step = .08f; field.planetRadius = .03f;
            field.BuildVisualPreview(12);
            var seeds = new Vector3[16];
            for (int i = 0; i < seeds.Length; i++) seeds[i] = field.dipolePosition + new Vector3(.2f, 0, 2);
            DynamoOrganicFieldValidation.Buffer(field, "_seedsBuf").SetData(seeds);
            field.BuildVisualPreview(12);
            var poleData = Read(field);
            for (int i = 0; i < poleData.Length; i += 7)
            {
                Vector3 a = Point(poleData, i), b = Point(poleData, i + 3);
                float radius = (a - field.dipolePosition).magnitude;
                if (radius <= field.planetRadius * Mathf.Sqrt(1.3f) || radius >= .32f) continue;
                Require((b - a).magnitude <= radius * .251f + .00001f, "Integration overshoots the pole region.");
                poleSamples++;
            }
            Require(poleSamples > 0, "Pole probe must reach the adaptive region.");
        }
        Require(EditorJsonUtility.ToJson(source.field) == before && source.gameObject.scene.isDirty == dirty, "Validation changed source settings.");
        return $"[Dynamo field stability] PASS: {cases} direction/pressure cases, {samples} connected GPU segments, " +
            $"at most {worstReversals} isolated sharp reversals per case, {poleSamples} adaptive pole steps; source unchanged.";
    }

    private static float[] Read(MagnetosphereFieldLinesGPU2D field)
    {
        int count = DynamoOrganicFieldValidation.SegmentCount(field);
        var values = new float[count * 7];
        DynamoOrganicFieldValidation.Buffer(field, "_segBuf").GetData(values);
        foreach (float value in values) Require(float.IsFinite(value), "Nonfinite field output.");
        return values;
    }
    private static Vector3 Point(float[] data, int i) => new Vector3(data[i], data[i + 1], data[i + 2]);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
