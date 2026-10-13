using System;
using Massive.Player;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Massive.EditorTools
{
    // Owns only hidden render resources. Prefabs are read, never instantiated or edited.
    internal sealed class PlayerMultiplierShimmerPreview : IDisposable
    {
        internal const string PlayerPath = "Assets/Prefabs/PlayerActor.prefab";
        internal const string FieldPath = "Assets/Prefabs/PLAYING FIELD.prefab";
        private PreviewRenderUtility preview;
        private CommandBuffer commands;
        private Mesh quad, grid;
        private Material solid;
        private readonly MaterialPropertyBlock body = new(), bar = new(), color = new();
        private GameObject playerAsset, fieldAsset;
        private ScoreEconomyProfile economy;
        private PlayerVisualController visual;
        private PlayerScoreChainPresenter[] presenters;

        internal float BodyRadius { get; private set; }
        internal float Reach { get; private set; }
        internal float BarWidth { get; private set; }
        internal float BarHeight { get; private set; }
        internal float FullBarWidth { get; private set; }
        internal float BarReach { get; private set; }
        internal Vector2 BodyCenter { get; private set; }
        internal Vector2 BarCenter { get; private set; }
        internal Vector2 BarScale { get; private set; }
        internal float PointsPerUnit { get; private set; }
        internal string SizeSource { get; private set; }

        internal bool Resolve(int slot, PlayerMultiplierShimmerSettings settings)
        {
            if (!playerAsset) playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            if (!fieldAsset) fieldAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FieldPath);
            if (!playerAsset || !fieldAsset) return false;
            if (!visual) visual = playerAsset.GetComponentInChildren<PlayerVisualController>(true);
            if (!visual || !visual.blobMat || !visual.visuals) return false;
            presenters ??= fieldAsset.GetComponentsInChildren<PlayerScoreChainPresenter>(true);
            var modifier = playerAsset.GetComponent<PlayerScaleAdjuster>();
            var globals = PlayerGlobalModifiers.Current;
            bool global = modifier && modifier.UseGlobalModifiers && globals && globals.GlobalSizeEnabled;
            float size = global ? globals.ForPlayer(slot).size : modifier ? modifier.LocalSize : 1f;
            // The saved prefab root can already contain a size multiplier. Replace that
            // root scale with its reference scale * current size; do not multiply twice.
            Vector3 scale = visual.visuals.lossyScale;
            if (modifier)
            {
                Vector3 root = playerAsset.transform.localScale;
                Vector3 desired = modifier.ReferenceLocalScale * size;
                scale = Vector3.Scale(scale, new Vector3(
                    desired.x / Mathf.Max(.0001f, Mathf.Abs(root.x)),
                    desired.y / Mathf.Max(.0001f, Mathf.Abs(root.y)),
                    desired.z / Mathf.Max(.0001f, Mathf.Abs(root.z))));
            }
            bodyScale = scale;
            BodyRadius = visual.baseRadius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            Reach = BodyRadius * settings.outlineReach;
            SizeSource = global ? $"P{slot + 1} global size {size:P0}" : $"PlayerActor local size {size:P0}";
            presenter = Array.Find(presenters, p => p && p.PlayerID == slot);
            if (!presenter) return false;
            using var data = new SerializedObject(presenter);
            rectangle = data.FindProperty("progressRectangle").objectReferenceValue as Shapes.Rectangle;
            track = data.FindProperty("progressTrackRectangle").objectReferenceValue as Shapes.Rectangle;
            glow = data.FindProperty("maximumGlowMaterial").objectReferenceValue as Material;
            if (!rectangle || !track || !glow) return false;
            BarScale = new Vector2(Mathf.Abs(rectangle.transform.lossyScale.x), Mathf.Abs(rectangle.transform.lossyScale.y));
            FullBarWidth = track.Width * BarScale.x;
            BarHeight = rectangle.Height * BarScale.y;
            BarReach = rectangle.Height * data.FindProperty("maximumGlowSpread").floatValue;
            return true;
        }
        private Vector3 bodyScale;
        private PlayerScoreChainPresenter presenter;
        private Shapes.Rectangle rectangle, track;
        private Material glow;

        internal float FitZoom(Rect rect) => Mathf.Clamp(Mathf.Min(
            rect.width * .40f / Mathf.Max(.01f, (BodyRadius + Reach) * 2f),
            rect.width * .53f / Mathf.Max(.01f, FullBarWidth + 2f * BarReach * BarScale.x),
            (rect.height - 75f) / Mathf.Max(.01f, (BodyRadius + Reach) * 2f)) / 100f, .25f, 3f);

        private void Ensure()
        {
            if (preview != null) return;
            preview = new PreviewRenderUtility();
            var camera = preview.camera;
            camera.orthographic = true; camera.nearClipPlane = .01f; camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .065f, .08f, 1f);
            camera.allowHDR = false; camera.allowMSAA = false; camera.cullingMask = 0;
            camera.transform.SetPositionAndRotation(new Vector3(0, 10, 0), Quaternion.LookRotation(Vector3.down, Vector3.forward));
            commands = new CommandBuffer { name = "Player multiplier tuning preview" };
            camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, commands);
            quad = new Mesh { name = "Multiplier preview quad", hideFlags = HideFlags.HideAndDontSave };
            quad.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 }; quad.RecalculateBounds();
            solid = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
            solid.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            solid.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            solid.SetInt("_Cull", (int)CullMode.Off); solid.SetInt("_ZWrite", 0);
            solid.SetInt("_ZTest", (int)CompareFunction.Always);
            grid = new Mesh { name = "World-unit preview grid", hideFlags = HideFlags.HideAndDontSave };
            var vertices = new Vector3[164]; var indices = new int[164]; var colors = new Color[164];
            for (int n = 0; n <= 40; n++)
            {
                float p = n - 20;
                vertices[n*4] = new Vector3(p, 0, -20); vertices[n*4+1] = new Vector3(p, 0, 20);
                vertices[n*4+2] = new Vector3(-20, 0, p); vertices[n*4+3] = new Vector3(20, 0, p);
            }
            for (int n = 0; n < indices.Length; n++) { indices[n] = n; colors[n] = Color.white; }
            grid.vertices = vertices; grid.colors = colors; grid.SetIndices(indices, MeshTopology.Lines, 0);
        }

        internal Texture Render(Rect rect, PlayerMultiplierShimmerSettings settings, float multiplier, float seconds, int slot, float zoom)
        {
            if (!Resolve(slot, settings)) return null;
            Ensure();
            PointsPerUnit = 100f * zoom;
            BodyCenter = new Vector2(rect.width * .24f, rect.height * .48f);
            BarCenter = new Vector2(rect.width * .69f, rect.height * .48f);
            var camera = preview.camera;
            camera.orthographicSize = rect.height / (2f * PointsPerUnit);
            camera.aspect = rect.width / rect.height;
            preview.BeginPreview(rect, GUIStyle.none);
            commands.Clear();
            var view = camera.worldToCameraMatrix;
            var projection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            commands.SetViewProjectionMatrices(view, camera.projectionMatrix);
            color.Clear(); color.SetColor("_Color", new Color(.12f, .14f, .17f, 1));
            commands.DrawMesh(grid, Matrix4x4.identity, solid, 0, 0, color);

            // Each sample has its own pane; zoom may crop, but cannot overlap the other sample.
            commands.EnableScissorRect(new Rect(0, 0, camera.pixelWidth * .46f, camera.pixelHeight));
            float strength = settings.Strength(multiplier);
            Vector3 center = ToWorld(BodyCenter, rect);
            body.Clear();
            body.SetMatrix("_MVP", projection * view * Matrix4x4.TRS(center, Quaternion.identity, bodyScale));
            body.SetFloat("_Radius", visual.baseRadius); body.SetFloat("_OutlineHalf", visual.outlineHalf);
            body.SetFloat("_Stretch", 1); body.SetFloat("_IdleWobble", visual.idleWobble);
            body.SetFloat("_NoisePhase", seconds); body.SetFloat("_DeformLerp", 0);
            body.SetFloat("_HitImpulse", 0); body.SetFloat("_DamageImpulse", 0);
            body.SetFloat("_ContactStrength", 0); body.SetFloat("_SingularityEnabled", 0);
            body.SetVector("_AmplifierTransitCenter", Vector4.zero);
            // Gameplay's child visual uses its authored team-1 palette at rest.
            body.SetColor("_FillColor", visual.team1_Fill); body.SetColor("_OutlineColor", visual.team1_Outline);
            PlayerMultiplierShimmer.Apply(body, settings, strength * settings.outlineOpacity,
                visual.baseRadius * settings.outlineReach, seconds, slot * .73f);
            if (strength > 0) commands.DrawProcedural(Matrix4x4.identity, visual.blobMat, 3, MeshTopology.Triangles, 128*3, 1, body);
            commands.DrawProcedural(Matrix4x4.identity, visual.blobMat, 0, MeshTopology.Triangles, 128*3, 1, body);
            commands.DrawProcedural(Matrix4x4.identity, visual.blobMat, 1, MeshTopology.Triangles, 128*3, 1, body);

            commands.DisableScissorRect();
            commands.EnableScissorRect(new Rect(camera.pixelWidth * .46f, 0, camera.pixelWidth * .54f, camera.pixelHeight));
            using var data = new SerializedObject(presenter);
            // Same continuous progress mapping as PlayerScoreChain.Progress01.
            if (!economy) economy = AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
            var chain = economy ? economy.ChainSettings : ScoreChainSettings.RecommendedDefaults();
            float progress = Mathf.InverseLerp(chain.GetMultiplier(0), chain.GetMultiplier(chain.MaxIndex), multiplier);
            float width = Mathf.Lerp(rectangle.Width, track.Width, progress);
            BarWidth = width * BarScale.x;
            Vector3 barCenter = ToWorld(BarCenter, rect);
            float left = barCenter.x - FullBarWidth * .5f;
            barCenter.x = left + BarWidth * .5f;
            DrawQuad(ToWorld(BarCenter, rect), FullBarWidth + .018f, BarHeight + .018f, track.Color * .6f + Color.white * .4f);
            DrawQuad(ToWorld(BarCenter, rect), FullBarWidth, BarHeight, track.Color);
            if (strength > 0 && data.FindProperty("maximumEffectsEnabled").boolValue)
            {
                bar.Clear();
                bar.SetVector("_BarSize", new Vector4(width, rectangle.Height, Mathf.Max(.001f, BarReach), 0));
                bar.SetColor("_Tint", data.FindProperty("maximumEffectColor").colorValue);
                float pulse = .7f + .3f * Mathf.Sin(seconds * Mathf.PI * 2f * data.FindProperty("maximumPulseFrequency").floatValue);
                bar.SetVector("_GlowStrength", new Vector4(data.FindProperty("maximumGlowOpacity").floatValue, pulse, 0, 0));
                bar.SetFloat("_SweepPhase", Mathf.Repeat(seconds / Mathf.Max(.1f, data.FindProperty("maximumSweepSeconds").floatValue), 1));
                PlayerMultiplierShimmer.Apply(bar, settings, strength * settings.barIntensity, BarReach, seconds, slot * .73f);
                commands.DrawMesh(quad, QuadMatrix(barCenter,
                    (width + 2 * BarReach) * BarScale.x, (rectangle.Height + 2 * BarReach) * BarScale.y), glow, 0, 0, bar);
            }
            DrawQuad(barCenter, BarWidth, BarHeight, rectangle.Color);
            commands.DisableScissorRect();
            camera.Render();
            return preview.EndPreview();
        }
        private Vector3 ToWorld(Vector2 point, Rect rect) =>
            new((point.x - rect.width * .5f) / PointsPerUnit, 0, (rect.height * .5f - point.y) / PointsPerUnit);
        private static Matrix4x4 QuadMatrix(Vector3 center, float width, float height) =>
            Matrix4x4.TRS(center, Quaternion.Euler(90, 0, 0), new Vector3(width, height, 1));
        private void DrawQuad(Vector3 center, float width, float height, Color tint)
        {
            color.Clear(); color.SetColor("_Color", tint);
            commands.DrawMesh(quad, QuadMatrix(center, width, height), solid, 0, 0, color);
        }
        public void Dispose()
        {
            if (preview != null && commands != null) preview.camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, commands);
            commands?.Dispose(); commands = null;
            preview?.Cleanup(); preview = null;
            if (quad) Object.DestroyImmediate(quad); if (grid) Object.DestroyImmediate(grid);
            if (solid) Object.DestroyImmediate(solid);
        }
    }
}
