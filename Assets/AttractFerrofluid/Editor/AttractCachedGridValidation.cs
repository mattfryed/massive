using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Massive.AttractStudy;

public static class AttractCachedGridValidation
{
    [MenuItem("MASSIVE/Attract/Validate Cached Grid")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run cached-grid validation in Edit Mode.");
        var passed = new List<string>();
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/AttractFerrofluid/AttractCachedGrid.shader");
        Require(shader != null && shader.isSupported, "cached shader available", passed);
        Require(!ShaderUtil.GetShaderMessages(shader).Any(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),
            "shader compiles without errors", passed);
        Require(AttractCachedGrid.SettledPull(25, 20, 103.9f, 12, .2f, .8f, .6f) == 0, "outside radius stays flat", passed);
        Require(AttractCachedGrid.SettledPull(15, 20, 0, 12, .2f, .8f, .6f) == 0, "zero strength stays flat", passed);
        Require(AttractCachedGrid.SettledPull(0, 20, 103.9f, 12, .2f, .8f, .6f) == 0, "center is finite", passed);
        for (int i = 1; i <= 1000; i++)
        {
            float distance = i * .02f;
            float pull = AttractCachedGrid.SettledPull(distance, 20, 103.9f, 12, .2f, .8f, .6f);
            if (float.IsNaN(pull) || pull < 0 || pull > distance * .95001f) throw new Exception("Invalid pull at " + distance);
        }
        passed.Add("1000 radial samples are finite and cannot cross the center");
        foreach (float height in new[] { 5.65f, 10.05f, 14.96f }) ValidateOriginalSimulation(passed, height);
        ValidateRenderedWidth(shader, passed);

        var go = new GameObject("Cached grid validation") { hideFlags = HideFlags.HideAndDontSave };
        var source = new GameObject("Cached attraction validation") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var grid = go.AddComponent<AttractCachedGrid>();
            var serialized = new SerializedObject(grid);
            serialized.FindProperty("lineShader").objectReferenceValue = shader;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            source.transform.position = new Vector3(0, 0, 14.96f);
            grid.attractor = source.transform;
            grid.RefreshCache();
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            var material = go.GetComponent<MeshRenderer>().sharedMaterial;
            Require(mesh != null && mesh.vertexCount > 0 && mesh.vertexCount < 250000, "authored mesh stays below 250k vertices", passed);
            Require(material.passCount == 1, "one render pass; no gameplay border pass", passed);
            int meshBakes = grid.MeshBakeCount, fieldBakes = grid.FieldBakeCount;
            for (int i = 0; i < 200; i++)
            {
                go.transform.rotation = Quaternion.Euler(i * .1f, i * .2f, i * .5f);
                go.transform.position = new Vector3(0, Mathf.Sin(i * .1f), 0);
                grid.RefreshCache();
            }
            Require(meshBakes == grid.MeshBakeCount && fieldBakes == grid.FieldBakeCount, "rotation, tilt and bob never rebake", passed);
            grid.lineWidth = 4; grid.baseColor = Color.red; grid.attractionColor = Color.blue;
            grid.RefreshCache();
            var block = new MaterialPropertyBlock(); go.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            Require(block.GetFloat("_LineWidth") == 4 && block.GetColor("_BaseColor") == Color.red && block.GetColor("_AttractionColor") == Color.blue,
                "width and independent colors reach the shader", passed);
            Require(meshBakes == grid.MeshBakeCount && fieldBakes == grid.FieldBakeCount, "appearance edits do not rebake", passed);
            grid.gridSpacing = 2; grid.RefreshCache();
            Require(grid.MeshBakeCount == meshBakes + 1 && mesh.vertexCount < 100000 && grid.FieldBakeCount == fieldBakes,
                "spacing rebuilds only the mesh", passed);
            grid.gridSize = new Vector2(48, 48); grid.RefreshCache();
            Require(grid.MeshBakeCount == meshBakes + 2 && grid.FieldBakeCount == fieldBakes, "grid size rebuilds only the mesh", passed);
            grid.attractionStrength *= .5f; grid.RefreshCache();
            Require(grid.FieldBakeCount == fieldBakes + 1 && grid.MeshBakeCount == meshBakes + 2, "attraction changes rebuild only the lookup", passed);
            source.transform.position += Vector3.right; grid.RefreshCache();
            go.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            Require(block.GetVector("_Attractor").x == 1 && grid.FieldBakeCount == fieldBakes + 1, "attractor translation updates without rebaking", passed);
            grid.attractor = null; grid.RefreshCache(); go.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            Require(block.GetVector("_Attractor").w == 0, "missing attractor produces an unbent grid", passed);
            grid.Rebake(); grid.RefreshCache();
            Require(grid.FieldBakeCount == fieldBakes + 2 && grid.MeshBakeCount == meshBakes + 3, "manual rebake refreshes both caches", passed);
            grid.enabled = false;
            Require(go.GetComponent<MeshFilter>().sharedMesh == null && mesh == null && material == null, "disable releases owned resources", passed);
            grid.enabled = true; grid.RefreshCache();
            Require(grid.VertexCount > 0, "reenabling reconstructs resources", passed);
            grid.gridSize = new Vector2(256, 256); grid.gridSpacing = .25f; grid.segmentsPerCell = 16; grid.RefreshCache();
            Require(grid.VertexCount <= 500000 && grid.SamplingLimited, "extreme authoring settings respect the memory budget", passed);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(source); }
        return string.Join("\n", passed.Select(p => "PASS " + p));
    }

    static void ValidateRenderedWidth(Shader shader, List<string> passed)
    {
        var gridObject = new GameObject("Width validation grid") { hideFlags = HideFlags.HideAndDontSave, layer = 31 };
        var cameraObject = new GameObject("Width validation camera") { hideFlags = HideFlags.HideAndDontSave };
        var target = new RenderTexture(512, 512, 24) { antiAliasing = 8 };
        var readback = new Texture2D(512, 512, TextureFormat.RGB24, false);
        var previousTarget = RenderTexture.active;
        try
        {
            var grid = gridObject.AddComponent<AttractCachedGrid>();
            var data = new SerializedObject(grid);
            data.FindProperty("lineShader").objectReferenceValue = shader;
            data.ApplyModifiedPropertiesWithoutUndo();
            grid.gridSize = new Vector2(20, 20); grid.baseColor = Color.white;
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 8;
            camera.transform.position = new Vector3(0, 0, -20); camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.targetTexture = target;
            int[] counts = new int[2];
            for (int i = 0; i < 2; i++)
            {
                grid.lineWidth = i == 0 ? 1 : 4; grid.RefreshCache();
                camera.Render(); RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); readback.Apply();
                for (int x = 0; x < 512; x++) if (readback.GetPixel(x, 272).maxColorComponent > .05f) counts[i]++;
            }
            Require(counts[0] > 0 && counts[1] >= counts[0] * 1.8f,
                "rendered width responds (lit row pixels " + counts[0] + " -> " + counts[1] + ")", passed);
        }
        finally
        {
            RenderTexture.active = previousTarget;
            UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(gridObject);
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(readback);
        }
    }

    static void ValidateOriginalSimulation(List<string> passed, float height)
    {
        // Compare against the existing GPU simulation, not another copy of the bake algorithm.
        var original = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/VectorGridNu/GridUnlitLines.compute");
        var compute = UnityEngine.Object.Instantiate(original);
        const int width = 32, count = width * width;
        var rest = new Vector3[count];
        for (int y = 0; y < width; y++) for (int x = 0; x < width; x++)
            rest[x + y * width] = new Vector3(-20 + 40f * x / (width - 1), -20 + 40f * y / (width - 1), 0);
        Vector3 center = new Vector3(0, 0, height);
        using (var positions = new ComputeBuffer(count, 12))
        using (var velocities = new ComputeBuffer(count, 12))
        using (var origins = new ComputeBuffer(count, 12))
        using (var forces = new ComputeBuffer(1, System.Runtime.InteropServices.Marshal.SizeOf<VectorGridGPU.Force>()))
        {
            try
            {
                positions.SetData(rest); origins.SetData(rest); velocities.SetData(new Vector3[count]);
                forces.SetData(new[] { VectorGridGPU.MakeRadial(center, 20, 103.9f) });
                int kernel = compute.FindKernel("CSMain");
                compute.SetBuffer(kernel, "_Pos", positions); compute.SetBuffer(kernel, "_Vel", velocities);
                compute.SetBuffer(kernel, "_OrigPos", origins); compute.SetBuffer(kernel, "_Forces", forces);
                compute.SetInt("_Count", count); compute.SetInt("_GridX", width); compute.SetInt("_GridY", width);
                compute.SetInt("_PinEdges", 1); compute.SetInt("_ForceCount", 1); compute.SetInt("_FalloffMode", 1);
                compute.SetFloat("_DeltaTime", 1f / 120); compute.SetFloat("_Kspring", 12); compute.SetFloat("_Damping", 2.2f);
                compute.SetFloat("_FalloffExp", 1); compute.SetFloat("_InnerFrac", .2f); compute.SetFloat("_Sharpness", 2);
                compute.SetFloat("_MaxSpeed", 12); compute.SetFloat("_WeightCap", .8f); compute.SetFloat("_CrowdStiffness", .6f);
                compute.SetInt("_FeatherCells", 0); compute.SetFloat("_FeatherSharp", .25f); compute.SetInt("_FeatherUseExp", 1);
                compute.SetFloat("_FeatherExpK", 6); compute.SetFloat("_EdgeSpringMul", 2); compute.SetFloat("_EdgeDampMul", 2.695f);
                compute.SetFloat("_EdgeForceMin", 0); compute.SetInt("_ProjectAtEdge", 1); compute.SetFloat("_EdgeAllowance", 0);
                compute.SetFloat("_Restitution", .6f);
                for (int i = 0; i < 1800; i++) compute.Dispatch(kernel, count / 256, 1, 1);
                var settled = new Vector3[count]; positions.GetData(settled);
                float error = 0;
                for (int i = 0; i < count; i++)
                {
                    Vector3 delta = center - rest[i];
                    Vector3 expected = rest[i] + delta.normalized * AttractCachedGrid.SettledPull(delta.magnitude, 20, 103.9f, 12, .2f, .8f, .6f);
                    error = Mathf.Max(error, Vector3.Distance(settled[i], expected));
                }
                Require(error < .005f, "cached field matches settled original GPU simulation at height " + height + " (max error " + error.ToString("F6") + ")", passed);
            }
            finally { UnityEngine.Object.DestroyImmediate(compute); }
        }
    }

    static void Require(bool condition, string name, List<string> passed)
    {
        if (!condition) throw new InvalidOperationException("FAIL " + name);
        passed.Add(name);
    }
}
