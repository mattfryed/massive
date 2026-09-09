using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace Massive.Resonance.Editor
{
    /// <summary>Uses the production compute kernel with private buffers; never steps the live match.</summary>
    public static class ResonanceGridFalloffValidation
    {
        [MenuItem("MASSIVE/Resonance/Validate Grid Attraction Falloff")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            var passed = new List<string>(); AppendChecks(passed);
            return passed.Count + " grid falloff checks passed:\n" + string.Join("\n", passed);
        }

        public static void AppendChecks(List<string> passed)
        {
            var source = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/VectorGridNu/GridUnlitLines.compute");
            Check(source != null && SystemInfo.supportsComputeShaders && ShaderUtil.GetComputeShaderMessages(source).Length == 0,
                "falloff production compute shader compiles", passed);
            var force = Soft(0, .5f);
            Check(force.flags == 6u && Marshal.SizeOf(typeof(VectorGridGPU.Force)) == 56,
                "profile is opt-in and preserves force buffer layout", passed);
            var clamped = VectorGridGPU.MakeSoftenedBalancedRadial(Vector3.zero, -1, 1, -1, 2);
            Check(clamped.radius > 0 && clamped.direction.x == 0 && clamped.direction.y == 1,
                "profile factory clamps authoring ranges", passed);
            var probe = new[] { Vector3.zero, Vector3.right * .001f, Vector3.right * .01f, Vector3.right * .1f, Vector3.right * .25f };
            var values = Evaluate(source, probe, new[] { force });
            Check(values.All(Finite) && values[0] == Vector3.zero && values[1].magnitude < .000001f && values[2].magnitude < .0001f,
                "pull tends continuously to zero at sample center", passed);
            Check(Mathf.Abs(values[4].x + .5f) < .00001f && values[4].y == 0 && values[4].z == 0,
                "inner midpoint applies half-strength radial pull", passed);
            var opposite = Evaluate(source, new[] { Vector3.left * .25f }, new[] { force });
            Check((values[4] + opposite[0]).magnitude < .00001f, "inner profile mirrors without directional bias", passed);
            var off = Evaluate(source, probe, new[] { Soft(0, 0) });
            Check(Mathf.Abs(off[4].x + 1) < .00001f, "zero Inner Softening disables only the center fade", passed);

            var repeated = Enumerable.Repeat(force, 8).ToArray();
            var capped = Evaluate(source, probe, repeated, .8f);
            Check(Mathf.Abs(capped[4].x + .4f) < .00001f && capped[2].magnitude < .0001f,
                "overlap normalization does not cancel inner softening", passed);
            var offset = VectorGridGPU.MakeSoftenedBalancedRadial(Vector3.right * .3f, 1, 1, .8f, .5f);
            var aroundCenter = Evaluate(source, new[] { Vector3.left * .00001f, Vector3.zero, Vector3.right * .00001f }, new[] { force, offset }, .8f);
            Check((aroundCenter[0] - aroundCenter[1]).magnitude < .0001f && (aroundCenter[2] - aroundCenter[1]).magnitude < .0001f,
                "exact-center overlap weight stays continuous", passed);

            var outerQueries = new[] { .4f, .7f, .998f, .999f, 1f, 1.1f, 2f }.Select(x => Vector3.right * x).ToArray();
            var outer = Evaluate(source, outerQueries, new[] { Soft(.6f, 0) });
            Check(Mathf.Abs(outer[0].x + 1) < .00001f && Mathf.Abs(outer[1].x + .5f) < .00001f,
                "Outer Feather starts at requested radius fraction", passed);
            Check(outer.Skip(4).All(v => v == Vector3.zero), "profile has zero attraction at and beyond its radius", passed);
            Check(outer[2].magnitude < .00001f && outer[3].magnitude < .00001f,
                "outer edge reaches zero without a force step", passed);
            var nearEdge = new[] { Vector3.right * .75f };
            var narrow = Evaluate(source, nearEdge, new[] { Soft(.2f, 0) });
            var broad = Evaluate(source, nearEdge, new[] { Soft(.8f, 0) });
            Check(broad[0].magnitude < narrow[0].magnitude * .5f, "wider feather gives more gradual outer influence", passed);
            var hard = Evaluate(source, new[] { Vector3.right * .999f, Vector3.right }, new[] { Soft(0, 0) });
            Check(Mathf.Abs(hard[0].magnitude - 1) < .00001f && hard[1] == Vector3.zero,
                "zero Outer Feather is an explicit hard-cutoff option", passed);

            var local = Evaluate(source, outerQueries, new[] { Soft(.8f, .5f) });
            var differentGlobals = Evaluate(source, outerQueries, new[] { Soft(.8f, .5f) }, 0, 3, .8f, 3);
            Check(local.Select((v, i) => (v - differentGlobals[i]).magnitude).Max() < .00001f,
                "Resonance profile is independent of global falloff and inner plateau", passed);
            var half = new[] { Vector3.right * .5f };
            const float legacyWeight = .68359375f; // Smooth global falloff at t=.5 with a .2 plateau.
            var legacy = Evaluate(source, half, new[] { VectorGridGPU.MakeRadial(Vector3.zero, 1, 1) });
            var balanced = Evaluate(source, half, new[] { VectorGridGPU.MakeBalancedRadial(Vector3.zero, 1, 1) });
            Check(Mathf.Abs(legacy[0].x + legacyWeight) < .00001f && (legacy[0] - balanced[0]).magnitude < .00001f,
                "legacy radial and inherited balanced falloff remain unchanged", passed);
            var direction = VectorGridGPU.MakeDirectional(Vector3.zero, 1, Vector3.up, 1);
            var directional = Evaluate(source, half, new[] { direction });
            Check(Mathf.Abs(directional[0].y - legacyWeight) < .00001f && directional[0].x == 0,
                "legacy directional forces preserve direction and global falloff", passed);
            var mix = Evaluate(source, half, new[] { Soft(.8f, .5f), direction });
            var alone = Evaluate(source, half, new[] { Soft(.8f, .5f) });
            Check((mix[0] - alone[0] - directional[0]).magnitude < .00001f,
                "local and legacy forces coexist in the same queue", passed);
            var a = VectorGridGPU.MakeSoftenedBalancedRadial(Vector3.left * .3f, 1, 1, .8f, .5f);
            var pair = Evaluate(source, new[] { Vector3.zero }, new[] { a, offset }, .8f);
            var reverse = Evaluate(source, new[] { Vector3.zero }, new[] { offset, a }, .8f);
            Check(pair[0].magnitude < .00001f && reverse[0].magnitude < .00001f,
                "softened opposite fields cancel in either sample order", passed);
            var doubled = VectorGridGPU.MakeSoftenedBalancedRadial(Vector3.zero, 2, 1, 0, .5f);
            var scaled = Evaluate(source, new[] { Vector3.right * .5f }, new[] { doubled });
            Check((scaled[0] - values[4]).magnitude < .00001f, "profile fractions scale with attraction radius", passed);
        }

        static VectorGridGPU.Force Soft(float feather, float inner) =>
            VectorGridGPU.MakeSoftenedBalancedRadial(Vector3.zero, 1, 1, feather, inner);
        static bool Finite(Vector3 v) => !float.IsNaN(v.x + v.y + v.z) && !float.IsInfinity(v.x + v.y + v.z);
        static void Check(bool condition, string name, List<string> passed)
        { if (!condition) throw new InvalidOperationException("Grid falloff check failed: " + name); passed.Add(name); }

        // Place test positions in the interior row of a private grid. Pin the
        // surrounding rows, disable spring/damping, dispatch one known timestep,
        // then read the actual kernel's velocity / dt as an acceleration probe.
        static Vector3[] Evaluate(ComputeShader source, Vector3[] queries, VectorGridGPU.Force[] input,
            float cap = 0, int globalMode = 1, float globalInner = .2f, float exponent = 1)
        {
            int width = queries.Length + 2, count = width * 3;
            var shader = UnityEngine.Object.Instantiate(source);
            try
            {
                using (var orig = new ComputeBuffer(count, 12))
                using (var pos = new ComputeBuffer(count, 12))
                using (var vel = new ComputeBuffer(count, 12))
                using (var forces = new ComputeBuffer(input.Length, Marshal.SizeOf(typeof(VectorGridGPU.Force))))
                {
                    var points = new Vector3[count]; Array.Copy(queries, 0, points, width + 1, queries.Length);
                    orig.SetData(points); pos.SetData(points); vel.SetData(new Vector3[count]); forces.SetData(input);
                    int k = shader.FindKernel("CSMain");
                    shader.SetBuffer(k, "_OrigPos", orig); shader.SetBuffer(k, "_Pos", pos);
                    shader.SetBuffer(k, "_Vel", vel); shader.SetBuffer(k, "_Forces", forces);
                    shader.SetInt("_Count", count); shader.SetInt("_ForceCount", input.Length);
                    shader.SetInt("_GridX", width); shader.SetInt("_GridY", 3); shader.SetInt("_PinEdges", 1);
                    shader.SetInt("_FeatherCells", 0); shader.SetInt("_FeatherUseExp", 0); shader.SetInt("_ProjectAtEdge", 0);
                    shader.SetInt("_FalloffMode", globalMode); shader.SetFloat("_InnerFrac", globalInner);
                    shader.SetFloat("_FalloffExp", exponent); shader.SetFloat("_Sharpness", 2);
                    shader.SetFloat("_DeltaTime", .02f); shader.SetFloat("_Kspring", 0); shader.SetFloat("_Damping", 0);
                    shader.SetFloat("_WeightCap", cap); shader.SetFloat("_CrowdStiffness", 0); shader.SetFloat("_MaxSpeed", 0);
                    shader.SetFloat("_FeatherSharp", 1); shader.SetFloat("_EdgeSpringMul", 1);
                    shader.SetFloat("_EdgeDampMul", 1); shader.SetFloat("_EdgeForceMin", 0);
                    shader.Dispatch(k, Mathf.CeilToInt(count / 256f), 1, 1);
                    var result = new Vector3[count]; vel.GetData(result);
                    return result.Skip(width + 1).Take(queries.Length).Select(v => v / .02f).ToArray();
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(shader); }
        }
    }
}
