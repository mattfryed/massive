#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    /// <summary>Isolated GPU reproduction of a rear body erasing a front outline.</summary>
    public static class SingularityOutlineValidation
    {
        [MenuItem("MASSIVE/SINGULARITY/Validate Player Outline Occlusion")]
        public static string Run()
        {
            int checks = 0;
            Action<bool, string> check = (passed, message) => {
                if (!passed) throw new InvalidOperationException("SINGULARITY outline: " + message);
                checks++;
            };
            Scene preview = EditorSceneManager.NewPreviewScene();
            RenderTexture previous = RenderTexture.active;
            Material material = null;
            Texture2D lookup = null, readback = null;
            RenderTexture target = null;
            var commands = new CommandBuffer { name = "Isolated player outline regression" };
            try
            {
                var owner = new GameObject("outline test surface");
                SceneManager.MoveGameObjectToScene(owner, preview);
                var surface = owner.AddComponent<SingularitySurface>();
                Shader shader = Shader.Find("MASSIVE/PlayerBlobVector");
                check(shader != null && !ShaderUtil.ShaderHasError(shader), "player shader compiles");
                material = new Material(shader);
                int depthPass = material.FindPass("FOLDED_RING_DEPTH");
                check(depthPass == 2, "existing fill and ring pass indices preserved");
                material.SetFloat("_Radius", 1f);
                material.SetFloat("_OutlineHalf", .1f);
                material.SetFloat("_Stretch", 1f);
                material.SetFloat("_IdleWobble", 0f);
                material.SetFloat("_DeformLerp", 0f);
                material.SetFloat("_ContactStrength", 0f);
                material.SetFloat("_HitImpulse", 0f);
                material.SetFloat("_DamageImpulse", 0f);

                const int samples = 2048;
                lookup = new Texture2D(samples, 1, TextureFormat.RGBAFloat, false, true) {
                    wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear
                };
                var pixels = new Color[samples];
                for (int i = 0; i < samples; i++)
                {
                    Vector4 frame = surface.SamplePacked(surface.LoopLength * i / samples);
                    pixels[i] = new Color(frame.x, frame.y, frame.z, frame.w);
                }
                lookup.SetPixels(pixels); lookup.Apply();
                target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                target.Create();
                readback = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(
                    new Vector3(0f, 20f, 0f), Quaternion.LookRotation(Vector3.down, Vector3.forward), Vector3.one).inverse;
                Matrix4x4 vp = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-2f, 2f, -2f, 2f, .1f, 100f), true) * view;
                Matrix4x4 rearChart = Matrix4x4.Translate(new Vector3(.7f / surface.RearScale, 0f,
                    surface.RearStart + surface.FrontHeight * surface.RearScale * .5f - surface.FrontHeight * .5f));
                MaterialPropertyBlock front = Properties(surface, lookup, vp, Matrix4x4.identity, Color.black, Color.white);
                MaterialPropertyBlock rear = Properties(surface, lookup, vp, rearChart, Color.red, Color.red);
                rear.SetFloat("_Radius", 1f / surface.RearScale);

                // Omit the new pass to retain an exact reproduction of the old
                // draw sequence. Rear paint erases the ring, but not its fill.
                Render(commands, target, readback, material, front, rear, false, true, true);
                check(IsRed(readback.GetPixel(190, 128)), "old front-first sequence reproduces disappearing border");
                check(IsBlack(readback.GetPixel(180, 128)), "old interior already occludes correctly");
                Render(commands, target, readback, material, rear, front, false, true, true);
                check(IsWhite(readback.GetPixel(190, 128)), "old behavior depended on draw order");

                for (int order = 0; order < 2; order++)
                {
                    Render(commands, target, readback, material, order == 0 ? front : rear,
                        order == 0 ? rear : front, true, true, true);
                    check(IsWhite(readback.GetPixel(190, 128)), "front ring survives rear overlap in order " + order);
                    check(IsBlack(readback.GetPixel(180, 128)), "front fill remains opaque in order " + order);
                    check(IsRed(readback.GetPixel(205, 128)), "rear remains visible outside front silhouette in order " + order);
                    check(readback.GetPixel(5, 5).b > .9f, "empty target remains unobstructed in order " + order);
                }

                Render(commands, target, readback, material, front, rear, true, false, true);
                check(IsRed(readback.GetPixel(128, 128)), "outline depth fan discards its empty interior");
                front.SetColor("_OutlineColor", new Color(1f, 1f, 1f, .5f));
                Render(commands, target, readback, material, front, rear, true, false, true);
                check(IsRed(readback.GetPixel(190, 128)), "translucent ghost outline does not block later rear draw");
                front.SetColor("_OutlineColor", Color.white);
                front.SetFloat("_SingularityEnabled", 0f);
                rear.SetFloat("_SingularityEnabled", 0f);
                rear.SetMatrix("_MVP", vp * Matrix4x4.Translate(new Vector3(.7f, -3f, 0f)));
                rear.SetFloat("_Radius", 1f);
                Render(commands, target, readback, material, front, rear, true, true, true);
                check(IsRed(readback.GetPixel(190, 128)), "depth pass remains a no-op for ordinary planar players");

                ValidatePortalShape(commands, target, readback, material, surface, lookup, vp, check);
            }
            finally
            {
                RenderTexture.active = previous;
                commands.Release();
                EditorSceneManager.ClosePreviewScene(preview);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
                if (lookup != null) UnityEngine.Object.DestroyImmediate(lookup);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            }
            string result = "SINGULARITY outline: " + checks + " GPU checks passed, including both player draw orders, non-occluding ghosts, and portal shape deformation.";
            Debug.Log(result);
            return result;
        }

        private static void ValidatePortalShape(CommandBuffer commands, RenderTexture target, Texture2D readback,
            Material material, SingularitySurface surface, Texture2D lookup, Matrix4x4 vp, Action<bool, string> check)
        {
            for (int face = 0; face < 2; face++)
            {
                // Keep the same projected one-unit radius on both faces. The
                // rear map compresses X; its Z coordinate is already arc length.
                float centerZ = face == 0 ? 0f : surface.RearStart + surface.FrontHeight * surface.RearScale * .5f - surface.FrontHeight * .5f;
                Matrix4x4 chart = Matrix4x4.TRS(new Vector3(0f, 0f, centerZ), Quaternion.identity,
                    new Vector3(face == 0 ? 1f : 1f / surface.RearScale, 1f, 1f));
                var properties = Properties(surface, lookup, vp, chart, Color.black, Color.white);
                properties.SetVector("_SingularityPortalCenter", new Vector4(0f, 0f, centerZ, 0f));
                properties.SetVector("_SingularityPortalShape", new Vector4(1f, 0f, .5f, 2f));
                RenderSingle(commands, target, readback, material, properties);
                Color32[] baseline = readback.GetPixels32();
                check(IsWhite(At(readback, .96f, 0f)) && IsBlack(At(readback, .8f, 0f)), "inactive portal keeps the original fill and border on face " + face);

                properties.SetVector("_SingularityPortalCenter", new Vector4(0f, 0f, centerZ, 1f));
                properties.SetVector("_SingularityPortalShape", new Vector4(1f, 0f, .5f, 1f));
                RenderSingle(commands, target, readback, material, properties);
                check(IsWhite(At(readback, .48f, 0f)), "uniform portal shrink moves the ring to half radius on face " + face);
                check(IsBlack(At(readback, .25f, 0f)), "uniform portal shrink keeps the fill inside its ring on face " + face);
                check(IsBlue(At(readback, .8f, 0f)) && IsBlue(At(readback, 0f, .8f)), "uniform portal shrink clears both original outer axes on face " + face);

                // Scale .5 and stretch 2 => longitudinal radius 1, transverse
                // radius .5 / sqrt(2). Sample safely inside fully opaque ring.
                properties.SetVector("_SingularityPortalShape", new Vector4(1f, 0f, .5f, 2f));
                RenderSingle(commands, target, readback, material, properties);
                check(IsWhite(At(readback, .96f, 0f)), "axial portal stretch extends the ring along X on face " + face);
                check(IsBlack(At(readback, .8f, 0f)), "axial portal stretch extends fill consistently on face " + face);
                check(IsWhite(At(readback, 0f, .34f)), "axial portal stretch contracts the transverse ring on face " + face);
                check(IsBlue(At(readback, 0f, .5f)), "axial portal stretch removes the former transverse silhouette on face " + face);

                properties.SetVector("_SingularityPortalShape", new Vector4(0f, 1f, .5f, 2f));
                RenderSingle(commands, target, readback, material, properties);
                check(IsWhite(At(readback, 0f, .96f)) && IsBlack(At(readback, 0f, .8f)), "portal direction rotates the stretched ring and fill onto Z on face " + face);
                check(IsWhite(At(readback, .34f, 0f)) && IsBlue(At(readback, .5f, 0f)), "rotated portal direction contracts X on face " + face);

                // Disabled state must ignore retained scale/stretch values,
                // restoring every pixel, not merely an approximate radius.
                properties.SetVector("_SingularityPortalCenter", new Vector4(0f, 0f, centerZ, 0f));
                RenderSingle(commands, target, readback, material, properties);
                Color32[] restored = readback.GetPixels32();
                bool identical = baseline.Length == restored.Length;
                for (int i = 0; i < baseline.Length && identical; i++) identical &= baseline[i].Equals(restored[i]);
                check(identical, "ending portal deformation restores the complete rendered image on face " + face);
            }
        }

        private static Color At(Texture2D readback, float x, float z)
        {
            // The isolated orthographic projection spans [-2, 2] in both axes.
            return readback.GetPixel(Mathf.FloorToInt((x + 2f) * readback.width / 4f), Mathf.FloorToInt((z + 2f) * readback.height / 4f));
        }

        private static void RenderSingle(CommandBuffer commands, RenderTexture target, Texture2D readback,
            Material material, MaterialPropertyBlock properties)
        {
            commands.Clear(); commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.blue);
            Draw(commands, material, properties, true, true);
            Graphics.ExecuteCommandBuffer(commands); RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); readback.Apply();
        }

        private static MaterialPropertyBlock Properties(SingularitySurface surface, Texture2D lookup,
            Matrix4x4 vp, Matrix4x4 chart, Color fill, Color outline)
        {
            var properties = new MaterialPropertyBlock();
            properties.SetFloat("_SingularityEnabled", 1f);
            properties.SetMatrix("_MVP", vp * chart);
            properties.SetMatrix("_SingularitySourceToWorld", chart);
            properties.SetMatrix("_SingularityViewProjection", vp);
            properties.SetMatrix("_SingularityWorldToLocal", Matrix4x4.identity);
            properties.SetMatrix("_SingularityLocalToWorld", Matrix4x4.identity);
            properties.SetVector("_SingularityShape", new Vector4(surface.FrontHeight, surface.LoopLength, 0f, .05f));
            properties.SetTexture("_SingularityLookup", lookup);
            properties.SetVector("_SingularityLookup_TexelSize", new Vector4(1f / lookup.width, 1f, lookup.width, 1f));
            properties.SetColor("_FillColor", fill);
            properties.SetColor("_OutlineColor", outline);
            return properties;
        }

        private static void Render(CommandBuffer commands, RenderTexture target, Texture2D readback,
            Material material, MaterialPropertyBlock first, MaterialPropertyBlock second,
            bool outlineDepth, bool firstFill, bool secondFill)
        {
            commands.Clear(); commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.blue);
            Draw(commands, material, first, outlineDepth, firstFill);
            Draw(commands, material, second, outlineDepth, secondFill);
            Graphics.ExecuteCommandBuffer(commands); RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); readback.Apply();
        }

        private static void Draw(CommandBuffer commands, Material material, MaterialPropertyBlock properties,
            bool outlineDepth, bool fill)
        {
            if (outlineDepth) commands.DrawProcedural(Matrix4x4.identity, material, 2, MeshTopology.Triangles, 128 * 3, 1, properties);
            if (fill) commands.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 128 * 3, 1, properties);
            commands.DrawProcedural(Matrix4x4.identity, material, 1, MeshTopology.Triangles, 128 * 3, 1, properties);
        }

        private static bool IsWhite(Color value) => value.r > .95f && value.g > .95f && value.b > .95f;
        private static bool IsBlack(Color value) => value.r < .05f && value.g < .05f && value.b < .05f;
        private static bool IsRed(Color value) => value.r > .95f && value.g < .05f && value.b < .05f;
        private static bool IsBlue(Color value) => value.r < .05f && value.g < .05f && value.b > .95f;
    }
}
#endif
