using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Massive.Dynamo;

/// <summary>Isolated visual cycle; owns no players, hazards, score or gameplay controller.</summary>
public sealed class DynamoInstructionsDemo : MonoBehaviour
{
    public MagnetosphereFieldLinesGPU2D field;
    public DynamoFlowBlanketRenderer flow;
    public DynamoSelectiveBloom bloom;
    public Camera view;
    public RawImage display;
    public float cycleSeconds = 14f;
    private RenderTexture output;
    private Material privateFieldMaterial, sourceFieldMaterial;
    private CommandBuffer fieldCommands;
    private float age, nextRender;

    private void OnEnable()
    {
        if (!field || !flow || !view || !display) return;
        field.ExternalPresentation = flow.ExternalPresentation = true;
        sourceFieldMaterial = field.lineMaterial;
        privateFieldMaterial = new Material(sourceFieldMaterial) { hideFlags = HideFlags.HideAndDontSave };
        field.lineMaterial = privateFieldMaterial;
        output = new RenderTexture(1280, 676, 24, RenderTextureFormat.ARGBHalf)
        { name = "Dynamo instructions demonstration", antiAliasing = 4 };
        output.Create(); display.texture = output;
        view.enabled = false; view.targetTexture = output; view.aspect = 1280f / 676f;
        fieldCommands = new CommandBuffer { name = "Dynamo instructions: magnetic field" };
        view.AddCommandBuffer(CameraEvent.AfterForwardAlpha, fieldCommands);
        field.enabled = flow.enabled = true;
        if (bloom) { bloom.targetCamera = view; bloom.enabled = true; }
        age = nextRender = 0;
    }

    private void LateUpdate()
    {
        if (!output) return;
        age += Time.unscaledDeltaTime;
        if (age < nextRender) return;
        nextRender = age + 1f / 30f;
        RenderSample(age);
    }

    public void RenderSample(float seconds)
    {
        if (!output) return;
        float duration = Mathf.Max(8, cycleSeconds);
        float t = Mathf.Repeat(seconds, duration);
        int cycle = Mathf.FloorToInt(seconds / duration);
        Vector2 source = cycle % 2 == 0 ? new Vector2(-1, .35f) : new Vector2(1, .5f);
        var footprint = DynamoStormFootprint.FromCamera(view, field.dipolePosition.y, field.dipolePosition, new Vector2(36, 19));
        Vector3 wind = footprint.FlowFromScreenSource(source);
        float rise = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.2f, 5.5f, t));
        float fall = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(duration - 4, duration - 1, t));
        float strength = rise * fall * .38f;
        field.SetExternalStorm(wind, strength);
        field.BuildVisualPreview(seconds * .35f);
        footprint.Project(wind, out float min, out float max);
        var state = new DynamoStormState
        {
            Active = t >= 1 && t < duration - .5f, Revision = cycle, Flow = wind,
            Elapsed = seconds, DirectionAge = t, Strength = strength,
            Opacity = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1, 1.8f, t)) * fall,
            Travel = seconds * 7, Front = Mathf.Lerp(min - .5f, max + .5f, Mathf.InverseLerp(1, 3, t)),
            Feather = .5f, Footprint = footprint
        };
        flow.BuildVisualPreview(state, view);
        fieldCommands.Clear();
        fieldCommands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        fieldCommands.SetViewProjectionMatrices(view.worldToCameraMatrix, view.projectionMatrix);
        field.RecordBloomSource(fieldCommands);
        var previous = RenderTexture.active;
        try { view.Render(); }
        finally { RenderTexture.active = previous; }
    }

    private void OnDisable()
    {
        if (view) { view.enabled = false; view.targetTexture = null; }
        if (fieldCommands != null)
        {
            if (view) view.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, fieldCommands);
            fieldCommands.Release(); fieldCommands = null;
        }
        if (bloom) bloom.enabled = false;
        if (flow) flow.enabled = false;
        if (field) { field.enabled = false; field.lineMaterial = sourceFieldMaterial; }
        if (display) display.texture = null;
        if (output) { output.Release(); Destroy(output); output = null; }
        if (privateFieldMaterial) Destroy(privateFieldMaterial);
    }
}
