#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Massive.Resonance;
using Massive.Singularity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Massive.Lattice.EditorTools
{
    public static partial class LatticeValidation
    {
        static IEnumerator ResonanceChecks()
        {
            ResonanceGpuChecks();
            field.mode = LatticeDisruptionField.FieldMode.Connected;
            field.smoothResonance = true;
            yield return Seconds(1.2f);
            field.Grid.ResetGrid(); yield return null; yield return null;
            var before = SimulationPositions();
            CaptureFixture(field.transform, "resonance-flat.png");
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resonance/Spawn Patterns/345 Hz Particle Encounter.prefab"), field.transform.parent);
            go.transform.position = field.transform.position + Vector3.up * .09f;
            owned.Add(go);
            var pattern = go.GetComponent<ResonancePatternController>();
            pattern.grid = field.Grid; pattern.attractGrid = true; pattern.gridEnergyPulse = 0;
            pattern.SetInteractionEnabled(true); pattern.Rebuild();
            // Isolate grid pixels from the obstacle's own animated particles.
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            yield return Until(() => field.ActiveResonanceSampleCount > 0, "Live pattern routes Resonance to the LATTICE presentation");
            yield return Seconds(.5f);
            Check(field.ActiveResonanceSampleCount == pattern.GridSampleCount, "Every exposed arc sample reaches the smooth field");
            Check(before.SequenceEqual(SimulationPositions()), "Smooth Resonance does not also inject spring forces");
            CaptureFixture(field.transform, "resonance-connected.png");
            Check(!ReadCapture("resonance-flat.png").SequenceEqual(ReadCapture("resonance-connected.png")),
                "Live Resonance changes the rendered connected grid");

            pattern.SetInteractionEnabled(false); yield return null; yield return null;
            Check(field.ActiveResonanceSampleCount == 0, "Dissolving or forming patterns leave no stale attraction");
            pattern.SetInteractionEnabled(true); yield return null; yield return null;
            Check(field.ActiveResonanceSampleCount > 0, "Attraction resumes after the interaction gate opens");
            field.enabled = false; yield return null;
            Check(field.ActiveResonanceSampleCount == 0 && !field.TryPresentResonance(pattern), "Disabled LATTICE releases the presentation route");
            // No frame with a live legacy force should pollute the following fixed-dot comparison.
            pattern.attractGrid = false; field.enabled = true;
            yield return Until(() => field.IsReady, "Resonance field rebuilds after disable and enable");
            yield return Seconds(3f);

            field.mode = LatticeDisruptionField.FieldMode.Disconnected;
            yield return Seconds(1.2f);
            Time.timeScale = 0; yield return null; yield return null;
            field.rgbDotJitter = true; field.dotJitterRadius = 4; field.dotGhostTrails = true;
            yield return null; yield return null;
            CaptureResonanceGrid("resonance-dots-off.png");
            pattern.attractGrid = true; yield return null; yield return null;
            CaptureResonanceGrid("resonance-dots-on.png");
            Check(ReadCapture("resonance-dots-off.png").SequenceEqual(ReadCapture("resonance-dots-on.png")),
                "Disconnected RGB dots and their trails remain on the exact hop coordinates under Resonance");
            Time.timeScale = 1;
            field.mode = LatticeDisruptionField.FieldMode.DriftingNoise;
            field.keepStrandsAtNoiseBoundary = true;
            yield return Seconds(1.2f);
            CaptureFixture(field.transform, "resonance-disrupted.png");
            CheckBoundaryTips("Resonance preserves the noise contour's held strand tips");

            field.smoothResonance = false;
            before = SimulationPositions(); yield return Seconds(.5f);
            Check(field.ActiveResonanceSampleCount == 0 && !before.SequenceEqual(SimulationPositions()),
                "Turning smooth Resonance off restores the legacy simulated response");
            field.smoothResonance = true; Object.Destroy(go); yield return null; yield return null;
            Check(field.ActiveResonanceSampleCount == 0, "Destroyed patterns leave no stale Resonance field");
        }

        static Vector3[] SimulationPositions()
        {
            var buffer = (ComputeBuffer)typeof(VectorGridGPU).GetField("_posBuf", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(field.Grid);
            var positions = new Vector3[buffer.count]; buffer.GetData(positions); return positions;
        }

        static void CaptureResonanceGrid(string name)
        {
            // Render the actual field mesh/block independently of the camera's
            // queued draws and the pattern's other animated graphics.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(LatticeDisruptionField);
            var mesh = (Mesh)type.GetField("mesh", flags).GetValue(field);
            var material = (Material)type.GetField("material", flags).GetValue(field);
            var block = (MaterialPropertyBlock)type.GetField("block", flags).GetValue(field);
            var go = new GameObject("Isolated Resonance grid capture");
            var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.cullingMask = 0;
            go.transform.SetPositionAndRotation(field.transform.position + Vector3.up*25, Quaternion.Euler(90,0,0));
            camera.orthographic = true; camera.orthographicSize = 7.1f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            using var commands = new CommandBuffer { name = "LATTICE field pixels only" };
            commands.DrawMesh(mesh, field.transform.localToWorldMatrix, material, 0, 0, block);
            camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, commands);
            try { Capture(camera, name); }
            finally { camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, commands); Object.Destroy(go); }
        }

        static void ResonanceGpuChecks()
        {
            var shader = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/Scripts/Anomalies/LATTICE/Editor/LatticeResonanceProbe.compute"));
            try
            {
                // Dense samples cross the center and outer cutoff, plus the pinned arena edge.
                var queries = Enumerable.Range(0, 401).Select(i => new Vector2(-2.1f + i * .0105f, 0)).Concat(
                    new[] { Vector2.zero, new Vector2(2,0), new Vector2(14,0), new Vector2(13.9999f,0) }).ToArray();
                var samples = new[] { new Vector4(0,0,2,.8f), new Vector4(13.5f,0,2,.8f) };
                var reference = samples.Select(s => new Vector4(s.x, s.y + 6, s.z, s.w)).ToArray();
                using var input = new ComputeBuffer(queries.Length, 8);
                using var output = new ComputeBuffer(queries.Length, 8);
                input.SetData(queries);
                shader.SetVector("_GridSize", new Vector4(28,12,0,0));
                shader.SetInt("_LatticeResonanceCount", samples.Length);
                shader.SetVectorArray("_LatticeResonanceSamples", samples);
                shader.SetVectorArray("_LatticeResonanceProfiles", new[] { new Vector4(.8f,.5f,0,0), new Vector4(.8f,.5f,0,0) });
                shader.SetVector("_LatticeResonanceMetric", Vector4.one);
                shader.SetVector("_LatticeResonanceResponse", new Vector4(.65f,.8f,0,0));
                shader.SetInt("_QueryCount", queries.Length);
                shader.SetBuffer(0,"_Queries",input); shader.SetBuffer(0,"_Results",output);
                shader.Dispatch(0,Mathf.CeilToInt(queries.Length/64f),1,1);
                var actual = new Vector2[queries.Length]; output.GetData(actual);
                float maximumError = queries.Select((p,i) => Vector2.Distance(actual[i],
                    SingularityGridRenderer.EvaluateResonanceOffset(p + Vector2.up*6, reference, reference.Length,12,28,.8f,.8f,.5f,.65f))).Max();
                Check(maximumError < .00001f, "Production GPU field matches SINGULARITY across 405 center, feather and boundary samples");
                Check(actual.All(p => float.IsFinite(p.x) && float.IsFinite(p.y) && p.magnitude < .65f),
                    "Resonance is finite and softly bounded across the field");
                Check(actual[401] == Vector2.zero && actual[402] == Vector2.zero && actual[403] == Vector2.zero,
                    "Sample centers, radius cutoffs and arena edges remain pinned");
            }
            finally { Object.Destroy(shader); }
        }
    }
}
#endif
