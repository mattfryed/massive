#if UNITY_EDITOR
using System;
using System.Reflection;
using Massive.Dynamo;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Explicit GPU and lifecycle checks; restores all presentation settings.</summary>
public static class DynamoVisualStyleValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [MenuItem("MASSIVE/Dynamo/Validate Visual Styles (Play Mode)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        Require(Application.isPlaying, "Play Mode with a visible storm required.");
        var bloom = UnityEngine.Object.FindFirstObjectByType<DynamoSelectiveBloom>();
        Require(bloom && bloom.field.CanRecordBloom && bloom.flow.CanRecordBloom, "Both Dynamo sources must be visible.");
        CheckPixels(bloom.bloomShader);
        var record = (Action<Camera>)Delegate.CreateDelegate(typeof(Action<Camera>), bloom,
            typeof(DynamoSelectiveBloom).GetMethod("BeforeCameraRender", Private));
        var tickFlow = (Action)Delegate.CreateDelegate(typeof(Action), bloom.flow,
            typeof(DynamoFlowBlanketRenderer).GetMethod("RenderFrame", Private));
        bool fieldOn = bloom.fieldBloom.enabled, flowOn = bloom.flowBloom.enabled;
        bool opaque = bloom.flow.opaqueTaperedStreaks;
        float core = bloom.flow.sharpCoreIntensity;
        var camera = bloom.targetCamera;
        float originalBlend = bloom.flow.flowMaterial.GetFloat("_DstBlend");
        long allocated;
        try
        {
            for (int mask = 0; mask < 4; mask++)
            {
                bloom.fieldBloom.enabled = (mask & 1) != 0;
                bloom.flowBloom.enabled = (mask & 2) != 0;
                record(camera);
                int channels = ((mask & 1) != 0 ? 1 : 0) + ((mask & 2) != 0 ? 1 : 0);
                Require(bloom.RecordedChannels == channels, "Each bloom toggle records only its own source.");
                Require(bloom.HasCommandBuffer == (mask != 0), "Both off must remove the render work.");
            }
            record(camera); // Warm native command storage before the managed-allocation check.
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 32; i++) record(camera);
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Require(allocated == 0, "Bloom recording must not allocate managed memory each frame.");
            bloom.enabled = false;
            Require(!bloom.HasCommandBuffer && bloom.RecordedChannels == 0, "Disabling bloom releases its camera commands.");
            bloom.enabled = true; record(camera);
            Require(bloom.RecordedChannels == 2, "Re-enabling must restore both channels.");
            bloom.targetCamera = null; record(camera);
            Require(!bloom.HasCommandBuffer, "Changing the target releases commands on the former camera.");
            bloom.targetCamera = camera;

            for (int style = 0; style < 2; style++)
            {
                bloom.flow.opaqueTaperedStreaks = style == 1; tickFlow();
                var material = (Material)typeof(DynamoFlowBlanketRenderer).GetField("drawMaterial", Private).GetValue(bloom.flow);
                Require(material && material != bloom.flow.flowMaterial, "Style settings require a private runtime material.");
                Require(material.GetFloat("_DstBlend") == (style == 1 ? 0 : 1), "Opaque uses replacement blending; soft uses additive blending.");
                Require(material.GetFloat("_OpaqueStreaks") == style, "Geometry and blending must switch together.");
                Require(bloom.flow.flowMaterial.GetFloat("_DstBlend") == originalBlend, "Style toggles must not edit the shared asset.");
            }
            var renderCore = (Action<Camera>)Delegate.CreateDelegate(typeof(Action<Camera>), bloom.flow,
                typeof(DynamoFlowBlanketRenderer).GetMethod("BeforeCameraRender", Private));
            foreach (float intensity in new[] { 1f, .25f, 0f })
            {
                bloom.flow.sharpCoreIntensity = intensity; tickFlow(); renderCore(camera);
                var sharpMaterial = (Material)typeof(DynamoFlowBlanketRenderer).GetField("drawMaterial", Private).GetValue(bloom.flow);
                var sourceMaterial = (Material)typeof(DynamoFlowBlanketRenderer).GetField("bloomMaterial", Private).GetValue(bloom.flow);
                Require(sharpMaterial.GetFloat("_CoreIntensity") == intensity, "Core intensity must affect the sharp layer.");
                Require(sourceMaterial.GetFloat("_CoreIntensity") == 1f && bloom.flow.CanRecordBloom, "Core intensity must leave the bloom source unchanged, including at zero.");
                Require(bloom.flow.HasCoreCommands == (intensity > 0f), "Zero core must remove the sharp draw completely.");
            }
        }
        finally
        {
            bloom.targetCamera = camera;
            bloom.fieldBloom.enabled = fieldOn; bloom.flowBloom.enabled = flowOn;
            bloom.flow.opaqueTaperedStreaks = opaque; tickFlow();
            bloom.flow.sharpCoreIntensity = core; tickFlow();
            bloom.enabled = true; record(camera);
        }
        return "[Dynamo visual styles] PASS: black/subthreshold rejection, HDR color preservation, spatial light spread; four independent bloom combinations; camera detach/re-enable; private opaque/additive materials; independent sharp core at 1/.25/0; " + allocated + " managed bytes over 32 bloom recordings.";
    }

    private static void CheckPixels(Shader shader)
    {
        Require(shader && shader.isSupported, "Bloom shader must compile on this device.");
        var material = new Material(shader);
        var input = new Texture2D(32, 32, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var output = RenderTexture.GetTemporary(32, 32, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        var read = new Texture2D(32, 32, TextureFormat.RGBAFloat, false, true);
        var commands = new CommandBuffer();
        var previous = RenderTexture.active;
        var previousParameters = Shader.GetGlobalVector("_DynamoBloomParameters");
        try
        {
            for (int test = 0; test < 4; test++)
            {
                var pixels = new Color[32 * 32];
                Color fill = test == 2 ? new Color(4, 2, 1, 1) : test == 1 ? new Color(.1f, .1f, .1f, 1) : Color.clear;
                for (int i = 0; i < pixels.Length; i++) pixels[i] = fill;
                if (test == 3) pixels[16 * 32 + 16] = new Color(4, 2, 1, 1);
                input.SetPixels(pixels); input.Apply();
                commands.Clear();
                commands.SetGlobalVector("_DynamoBloomParameters", new Vector4(1, .5f, .8f, 1));
                commands.Blit(input, output, material, test == 3 ? 1 : 0);
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active = output;
                read.ReadPixels(new Rect(0, 0, 32, 32), 0, 0); read.Apply();
                pixels = read.GetPixels();
                foreach (Color pixel in pixels)
                {
                    Require(!float.IsNaN(pixel.r) && !float.IsInfinity(pixel.r), "Bloom pixels must remain finite.");
                    if (test < 2) Require(pixel.r + pixel.g + pixel.b < .00001f, "Dark input cannot emit bloom.");
                    if (test == 2) Require(Mathf.Abs(pixel.r - 3) < .01f && Mathf.Abs(pixel.g - 1.5f) < .01f && Mathf.Abs(pixel.b - .75f) < .01f,
                        "HDR thresholding must preserve source hue and use the supplied input texture.");
                }
                if (test == 3)
                {
                    Require(pixels[16 * 32 + 15].r > .1f, "A highlight must scatter light into neighboring pixels.");
                    Require(pixels[0].r < .00001f, "An isolated highlight cannot flood the frame.");
                }
            }
        }
        finally
        {
            Shader.SetGlobalVector("_DynamoBloomParameters", previousParameters);
            RenderTexture.active = previous;
            commands.Release(); RenderTexture.ReleaseTemporary(output);
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(input); UnityEngine.Object.DestroyImmediate(read);
        }
    }
}
#endif
