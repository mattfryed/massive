#if UNITY_EDITOR
using System;
using Massive.Dynamo;
using UnityEditor;
using UnityEngine;

public static class DynamoVisualPreviewValidation
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [MenuItem("MASSIVE/Dynamo/Validate Visual Preview (Edit Mode)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        Require(!Application.isPlaying, "Edit Mode required.");
        DynamoSelectiveBloom source = null;
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<DynamoSelectiveBloom>(FindObjectsSortMode.None))
            if ((candidate.gameObject.hideFlags & HideFlags.DontSave) == 0) { source = candidate; break; }
        Require(source && source.flow && source.field, "Open the Dynamo scene first.");
        var controller = source.flow.controller;
        string fieldBefore = EditorJsonUtility.ToJson(source.field), flowBefore = EditorJsonUtility.ToJson(source.flow);
        string bloomBefore = EditorJsonUtility.ToJson(source), controllerBefore = EditorJsonUtility.ToJson(controller);
        string materialBefore = EditorJsonUtility.ToJson(source.field.lineMaterial);
        string randomBefore = JsonUtility.ToJson(UnityEngine.Random.state);
        bool dirtyBefore = source.gameObject.scene.isDirty;
        int cameraCommands = source.targetCamera.commandBufferCount;
        MagnetosphereFieldLinesGPU2D copyField;
        DynamoFlowBlanketRenderer copyFlow;
        DynamoSelectiveBloom copyBloom;
        using (var preview = new DynamoVisualPreviewSession(source))
        {
            copyField = preview.Field; copyFlow = preview.Flow; copyBloom = preview.Bloom;
            int cases = 0;
            foreach (Vector2 direction in new[] { Vector2.left, Vector2.right, Vector2.up, new Vector2(-1, -1) })
            foreach (float fraction in new[] { 0f, .01f, .15f, .5f, .9f, 1f })
            {
                preview.Render(24 * fraction, 24, .4f, direction, 0, 640);
                var state = preview.State;
                Require(float.IsFinite(state.Travel) && float.IsFinite(preview.Pressure), "Scrubbed storm state must be finite.");
                Require(Mathf.Abs(state.Flow.magnitude - 1) < .0001f, "Preview direction must remain normalized.");
                Require(preview.Field != source.field && preview.Field.lineMaterial != source.field.lineMaterial, "Field and its material must be isolated copies.");
                int expected = (source.fieldBloom.enabled && source.fieldBloom.intensity > 0 && preview.Field.CanRecordBloom ? 1 : 0) +
                    (source.flowBloom.enabled && source.flowBloom.intensity > 0 && preview.Flow.CanRecordBloom ? 1 : 0);
                Require(preview.Bloom.RecordedChannels == expected, "Preview must render the enabled bloom channels.");
                if (fraction == 0 || fraction == 1)
                    Require(state.Strength == 0 && state.Opacity == 0 && !preview.Flow.HasCoreCommands, "Calm/end points must have no storm streaks.");
                if (fraction == .5f) Require(Mathf.Abs(state.Strength - .4f) < .0001f, "Held peak must match the requested strength.");
                cases++;
            }
            preview.Render(12, 24, .4f, Vector2.left, 0, 640);
            var held = preview.State;
            float pressure = preview.Pressure;
            int allocations = preview.Flow.BufferAllocationCount;
            preview.Render(12, 24, .4f, Vector2.left, 0, 640);
            Require(preview.State.Travel == held.Travel && preview.State.Elapsed == held.Elapsed && preview.Pressure == pressure, "Held preview cannot advance on repaint.");
            preview.Render(12, 24, .4f, Vector2.left, 2, 960);
            Require(preview.State.Strength == held.Strength && preview.Pressure == pressure && preview.State.Travel > held.Travel,
                "Optional motion must animate droplets without advancing storm strength or pressure.");
            Require(preview.Output.width == 960 && preview.Flow.BufferAllocationCount == allocations, "Image resizing must not rebuild flow buffers.");
            Require(cases == 24, "All scrub cases must run.");
        }
        Require(!copyField && !copyFlow && !copyBloom, "Stopping must destroy every temporary renderer.");
        Require(source.targetCamera.commandBufferCount == cameraCommands, "Preview must not attach render work to the source camera.");
        Require(EditorJsonUtility.ToJson(source.field) == fieldBefore && EditorJsonUtility.ToJson(source.flow) == flowBefore &&
            EditorJsonUtility.ToJson(source) == bloomBefore && EditorJsonUtility.ToJson(controller) == controllerBefore,
            "Preview must not mutate source component settings.");
        Require(EditorJsonUtility.ToJson(source.field.lineMaterial) == materialBefore, "Preview must not mutate shared field materials.");
        Require(JsonUtility.ToJson(UnityEngine.Random.state) == randomBefore, "Scrubbing must not consume gameplay randomness.");
        Require(source.gameObject.scene.isDirty == dirtyBefore, "Preview controls must not dirty the scene.");
        return "[Dynamo edit preview] PASS: 24 direction/time samples, calm/peak/recovery, held-state determinism, independent motion, image resizing, stable buffers, isolated materials/settings/randomness, no source-camera commands or scene dirtying, full cleanup.";
    }
}
#endif
