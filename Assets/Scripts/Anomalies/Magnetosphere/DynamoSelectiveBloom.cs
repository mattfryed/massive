using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Dynamo
{
    /// <summary>Independent HDR bloom from just the two procedural Dynamo renderers.</summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class DynamoSelectiveBloom : MonoBehaviour
    {
        [Serializable]
        public sealed class BloomSettings
        {
            public bool enabled = true;
            [Tooltip("Brightness of the scattered light, independent of the source color.")]
            [Min(0f)] public float intensity = 1.2f;
            [Tooltip("Only source brightness above this HDR level contributes fully to bloom.")]
            [Min(0f)] public float threshold = .9f;
            [Tooltip("Soft transition around the brightness threshold.")]
            [Range(0f, 1f)] public float softKnee = .5f;
            [Tooltip("Balance between tight highlights and the wider blur scales.")]
            [Range(.05f, .95f)] public float scatter = .8f;
            [Tooltip("Number of blur scales. More scales spread highlights farther.")]
            [Range(3, 8)] public int diffusion = 6;
        }

        [Header("Sources")]
        public Camera targetCamera;
        public MagnetosphereFieldLinesGPU2D field;
        public DynamoFlowBlanketRenderer flow;
        public Shader bloomShader;

        [Header("Independent Bloom Controls")]
        public BloomSettings fieldBloom = new BloomSettings();
        public BloomSettings flowBloom = new BloomSettings { intensity = 1f, threshold = .55f, scatter = .75f, diffusion = 5 };

        private const CameraEvent InjectionPoint = CameraEvent.BeforeImageEffects;
        private CommandBuffer commands;
        private Camera attachedCamera;
        private Material filter;
        private readonly int[] down = new int[8], up = new int[8];
        private readonly int[] widths = new int[8], heights = new int[8];
        private static readonly int Source = Shader.PropertyToID("_DynamoBloomSource");
        private static readonly int Parameters = Shader.PropertyToID("_DynamoBloomParameters");
        private static readonly int LowMip = Shader.PropertyToID("_DynamoBloomLowMip");
        private static readonly int LowTexel = Shader.PropertyToID("_DynamoBloomLowTexel");
        public bool HasCommandBuffer => commands != null;
        public int RecordedChannels { get; private set; }
        // Enabled only on temporary editor-preview copies, never serialized into scenes.
        public bool PreviewRendering { get; set; }

        private void OnEnable()
        {
            for (int i = 0; i < 8; i++)
            {
                down[i] = Shader.PropertyToID("_DynamoBloomDown" + i);
                up[i] = Shader.PropertyToID("_DynamoBloomUp" + i);
            }
            Camera.onPreRender += BeforeCameraRender;
        }

        private void BeforeCameraRender(Camera camera)
        {
            if (attachedCamera && attachedCamera != targetCamera) Detach();
            if ((!Application.IsPlaying(gameObject) && !PreviewRendering) || camera != targetCamera) return;
            bool drawField = fieldBloom.enabled && fieldBloom.intensity > 0 && field && field.CanRecordBloom;
            bool drawFlow = flowBloom.enabled && flowBloom.intensity > 0 && flow && flow.CanRecordBloom;
            RecordedChannels = 0;
            if ((!drawField && !drawFlow) || !bloomShader || !bloomShader.isSupported)
            {
                Detach();
                return;
            }
            if (attachedCamera != camera) Detach();
            if (commands == null)
            {
                commands = new CommandBuffer { name = "Dynamo: isolated field + flow HDR bloom" };
                attachedCamera = camera;
                camera.AddCommandBuffer(InjectionPoint, commands);
            }
            if (!filter || filter.shader != bloomShader)
            {
                DestroyFilter();
                filter = new Material(bloomShader) { name = "Dynamo bloom filters (runtime)", hideFlags = HideFlags.HideAndDontSave };
            }
            commands.Clear();
            int width = Mathf.Max(1, camera.pixelWidth), height = Mathf.Max(1, camera.pixelHeight);
            int samples = camera.targetTexture ? camera.targetTexture.antiAliasing :
                camera.allowMSAA && camera.actualRenderingPath == RenderingPath.Forward ? Mathf.Max(1, QualitySettings.antiAliasing) : 1;
            // Match camera depth/MSAA so occluded lines cannot become bloom emitters.
            commands.GetTemporaryRT(Source, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, samples);
            if (drawField) RecordChannel(camera, fieldBloom, true, width, height);
            if (drawFlow) RecordChannel(camera, flowBloom, false, width, height);
            commands.ReleaseTemporaryRT(Source);
            commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            commands.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
        }

        private void RecordChannel(Camera camera, BloomSettings settings, bool magneticField, int width, int height)
        {
            commands.BeginSample(magneticField ? "Dynamo.FieldBloom" : "Dynamo.FlowBloom");
            // CameraTarget supplies the actual depth attachment. Builtin Depth is
            // a sampled depth texture and cannot be used here as that attachment.
            commands.SetRenderTarget(new RenderTargetIdentifier(Source), new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget));
            commands.ClearRenderTarget(false, true, Color.clear);
            // Use the camera geometry's projection for matching depth comparisons.
            commands.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
            if (magneticField) field.RecordBloomSource(commands); else flow.RecordBloomSource(commands);
            commands.SetGlobalVector(Parameters, new Vector4(Mathf.Max(0, settings.threshold), Mathf.Max(.0001f, settings.threshold * settings.softKnee), settings.scatter, settings.intensity));

            int levels = Mathf.Clamp(settings.diffusion, 3, 8);
            for (int i = 0; i < levels; i++)
            {
                width = Mathf.Max(1, width / 2); height = Mathf.Max(1, height / 2);
                widths[i] = width; heights[i] = height;
                commands.GetTemporaryRT(down[i], width, height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                commands.Blit(i == 0 ? Source : down[i-1], down[i], filter, i == 0 ? 0 : 1);
            }
            int low = down[levels-1];
            for (int i = levels - 2; i >= 0; i--)
            {
                commands.GetTemporaryRT(up[i], widths[i], heights[i], 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                commands.SetGlobalTexture(LowMip, low);
                commands.SetGlobalVector(LowTexel, new Vector4(1f / widths[i+1], 1f / heights[i+1], 0, 0));
                commands.Blit(down[i], up[i], filter, 2);
                low = up[i];
            }
            commands.Blit(low, BuiltinRenderTextureType.CameraTarget, filter, 3);
            for (int i = 0; i < levels; i++) commands.ReleaseTemporaryRT(down[i]);
            for (int i = 0; i < levels - 1; i++) commands.ReleaseTemporaryRT(up[i]);
            commands.EndSample(magneticField ? "Dynamo.FieldBloom" : "Dynamo.FlowBloom");
            RecordedChannels++;
        }

        private void Detach()
        {
            if (commands != null)
            {
                if (attachedCamera) attachedCamera.RemoveCommandBuffer(InjectionPoint, commands);
                commands.Release(); commands = null;
            }
            attachedCamera = null;
        }

        private void OnDisable()
        {
            Camera.onPreRender -= BeforeCameraRender;
            Detach();
            DestroyFilter();
            filter = null;
            RecordedChannels = 0;
        }

        private void DestroyFilter()
        {
            if (!filter) return;
            if (Application.isPlaying) Destroy(filter); else DestroyImmediate(filter);
        }
    }
}
