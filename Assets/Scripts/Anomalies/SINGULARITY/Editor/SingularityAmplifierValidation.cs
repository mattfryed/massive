#if UNITY_EDITOR
using System;
using Massive.Multiplier;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    /// <summary>Scene-isolated treatment projection checks. Does not edit shared
    /// presets, authored scenes, score totals or real capture state.</summary>
    public static class SingularityAmplifierValidation
    {
        [MenuItem("MASSIVE/SINGULARITY/Validate Amplifier Border Feedback")]
        public static void Run()
        {
            int checks = 0;
            Action<bool, string> check = (valid, message) => {
                if (!valid) throw new InvalidOperationException("SINGULARITY amplifier: " + message);
                checks++;
            };
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Isolated amplifier field test");
                SceneManager.MoveGameObjectToScene(root, preview);
                var surface = root.AddComponent<SingularitySurface>();
                surface.ConfigureProjectedBounds(28f, 12f, .875f, 3f, 2.1f);
                var lattice = new GameObject("Lattice");
                SceneManager.MoveGameObjectToScene(lattice, preview);
                lattice.transform.SetParent(root.transform, false);
                var grid = lattice.AddComponent<SingularityGridRenderer>();
                grid.surface = surface;
                var treatments = lattice.AddComponent<AmplifierGoalTreatments>();
                treatments.UseSharedSettings = false;
                treatments.ShowControls = false;
                var goals = new AmplifierGoalCapture[2];
                for (int i = 0; i < goals.Length; i++)
                {
                    var goal = new GameObject("Goal " + (i + 1));
                    SceneManager.MoveGameObjectToScene(goal, preview);
                    goal.AddComponent<SphereCollider>().isTrigger = true;
                    goals[i] = goal.AddComponent<AmplifierGoalCapture>();
                    var serialized = new SerializedObject(goals[i]);
                    serialized.FindProperty("teamID").intValue = i + 1;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                treatments.Configure(null, goals, null, null);
                treatments.ConfigurePresentationGrid(surface.transform, new Vector2(28f, 12f), true);
                grid.amplifierTreatments = treatments;
                var settings = treatments.Settings;
                settings.heldCharge = false; settings.gridDischarge = false; settings.emissions = 1;
                settings.gridCoupling = 1f;
                Vector2 left = new Vector2(-14f, surface.FrontHeight * .5f + 1.7f);
                check(grid.EvaluateAmplifierOffset(left) == Vector2.zero, "idle neutral border is still");
                check(grid.AmplifierBoundsPadding == 0f, "neutral field adds no culling padding");
                check(grid.EvaluateAmplifierColor(left, Color.white) == Color.white, "idle neutral border is pure white");

                goals[0].PlayCaptureFeedback();
                check(Mathf.Approximately(treatments.GetGridOrigin(1, Vector2.zero).z, -settings.absorptionSeconds), "actual goal capture forwards to one shared clock");
                check(treatments.GetGridOrigin(2, Vector2.zero).z > 100f, "team one capture does not trigger team two");
                treatments.SetPreviewTime(.1f);
                check(grid.EvaluateAmplifierOffset(left) == Vector2.zero, "packet waits for absorption");
                treatments.SetPreviewTime(.82f);
                check(grid.EvaluateAmplifierOffset(left).sqrMagnitude > .00001f, "capture packet reaches front border");
                float fixedClock = treatments.Clock;
                Vector2 once = grid.EvaluateAmplifierOffset(left);
                check(grid.EvaluateAmplifierOffset(left) == once && treatments.Clock == fixedClock, "sampling does not advance capture clock");
                settings.capturePackets = false;
                check(grid.EvaluateAmplifierOffset(left) == Vector2.zero, "capture packet toggle isolates effect");
                settings.capturePackets = true;
                settings.gridCoupling = 0f;
                check(grid.EvaluateAmplifierOffset(left) == Vector2.zero, "zero coupling mutes displacement");
                settings.gridCoupling = 1f; settings.allowEffectsOverGrid = false;
                check(grid.EvaluateAmplifierOffset(left).sqrMagnitude > .00001f, "containment keeps boundary packets active");

                // Both curved regions and the rear must receive packets, not fade
                // out at the old flat front face's top or bottom endpoints.
                float[] samples = { surface.TopStart + .35f, surface.RearStart - .35f,
                    surface.RearStart + .7f, surface.BottomStart + .35f, surface.LoopLength - .35f };
                for (int i = 0; i < samples.Length; i++)
                {
                    bool arrived = false;
                    for (int time = 1; time < 65; time++)
                    {
                        treatments.SetPreviewTime(settings.absorptionSeconds + time * .1f);
                        Vector2 value = grid.EvaluateAmplifierOffset(new Vector2(-14f, samples[i]));
                        arrived |= value.sqrMagnitude > .00001f;
                    }
                    check(arrived, "packet reaches folded region " + i);
                }
                treatments.SetPreviewTime(2.3f);
                Vector2 seamA = grid.EvaluateAmplifierOffset(new Vector2(-14f, 0f));
                Vector2 seamB = grid.EvaluateAmplifierOffset(new Vector2(-14f, surface.LoopLength));
                check(Vector2.Distance(seamA, seamB) < .00001f, "packet field closes at loop seam");
                treatments.SetPreviewTime(10f);
                check(grid.EvaluateAmplifierOffset(left) == Vector2.zero, "capture settles after bounded lifetime");

                settings.heldCharge = true; settings.capturePackets = false;
                treatments.Preview = true; treatments.PreviewTeam = 1; treatments.HeldLevel = 3;
                treatments.SetPreviewTime(10.1f);
                check(grid.EvaluateAmplifierOffset(new Vector2(-14f, surface.TopStart + 1f)).sqrMagnitude > 0f, "held wave runs through turn");
                check(grid.EvaluateAmplifierOffset(new Vector2(-14f, surface.RearStart + 1f)).sqrMagnitude > 0f, "held wave reaches rear");
                check(grid.EvaluateAmplifierOffset(new Vector2(14f, surface.TopStart + 1f)).sqrMagnitude < .0000001f, "opponent neutral border remains still");
                Color tinted = grid.EvaluateAmplifierColor(left, new Color(1f, 1f, 1f, .4f));
                check(tinted.r < 1f || tinted.g < 1f || tinted.b < 1f, "existing held tint recolors hard edge");
                check(Mathf.Approximately(tinted.a, .4f), "tint preserves layer alpha");
                treatments.HeldLevel = 0;
                check(grid.EvaluateAmplifierOffset(left) == Vector2.zero, "x1 held level remains neutral");
                treatments.HeldLevel = 3; treatments.PreviewPaused = true;
                float pausedClock = treatments.Clock;
                treatments.AdvancePreview(1f);
                check(treatments.Clock == pausedClock, "paused preview clock stays frozen");
                treatments.PreviewPaused = false;

                settings.gridDischarge = true; settings.allowEffectsOverGrid = true;
                settings.heldCharge = false;
                treatments.ResetPreview(); goals[0].PlayCaptureFeedback(); treatments.SetPreviewTime(1f);
                Vector2 interior = new Vector2(-11.5f, surface.FrontHeight * .5f);
                check(grid.EvaluateAmplifierOffset(interior).sqrMagnitude > .00001f, "existing capture discharge enters field");
                settings.allowEffectsOverGrid = false;
                check(grid.EvaluateAmplifierOffset(interior) == Vector2.zero, "containment suppresses discharge");

                settings.allowEffectsOverGrid = true;
                float antipode = surface.FrontHeight * .5f + surface.LoopLength * .5f;
                treatments.SetPreviewTime(settings.absorptionSeconds + surface.LoopLength * .5f / settings.gridSpeed);
                for (int x = 0; x < 3; x++)
                {
                    Vector2 before = new Vector2(-14f + x, antipode - .0001f);
                    Vector2 after = new Vector2(-14f + x, antipode + .0001f);
                    check(Vector2.Distance(grid.EvaluateAmplifierOffset(before), grid.EvaluateAmplifierOffset(after)) < .001f,
                        "discharge fronts meet continuously at rear antipode " + x);
                    check(Vector2.Distance(grid.EvaluateAmplifierOffset(before), grid.EvaluateAmplifierOffset(before + Vector2.up * surface.LoopLength)) < .0001f,
                        "discharge field is exactly periodic " + x);
                }
                settings.allowEffectsOverGrid = false;

                grid.Rebuild();
                var block = new MaterialPropertyBlock();
                grid.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Vector4[] origins = block.GetVectorArray("_AmpOrigins");
                check(origins.Length == 2 && Mathf.Approximately(origins[0].x, -14f)
                    && Mathf.Approximately(origins[1].x, 14f), "both team origins explicitly bound");
                check(Mathf.Approximately(origins[0].y, surface.FrontHeight * .5f), "capture origin aligns with world center Z");
                check(Mathf.Approximately(origins[0].z, treatments.GetGridOrigin(1, Vector2.zero).z), "GPU shares capture age with goal effects");
                settings.capturePackets = true;
                float paddedHalfWidth = surface.Width * .5f + grid.AmplifierBoundsPadding;
                bool insideBounds = true;
                for (int sample = 0; sample < 48; sample++)
                {
                    Vector2 logical = new Vector2(-14f, surface.LoopLength * sample / 48f);
                    Vector2 offset = grid.EvaluateAmplifierOffset(logical);
                    check(!float.IsNaN(offset.x) && !float.IsInfinity(offset.x)
                        && !float.IsNaN(offset.y) && !float.IsInfinity(offset.y), "finite closed-loop field sample " + sample);
                    Vector3 mapped = surface.Evaluate(logical.x + offset.x, logical.y + offset.y);
                    insideBounds &= Mathf.Abs(mapped.x) <= paddedHalfWidth + .0001f;
                }
                check(insideBounds, "conservative X padding contains deformed folded border");
                Vector2 shifted = left + grid.EvaluateAmplifierOffset(left);
                Color mappedColor;
                check(Vector3.Distance(grid.EvaluateAmplifierPoint(left, out mappedColor), surface.WorldPosition(shifted.x, shifted.y)) < .00001f, "goal extension query uses same world-space deformation");
                treatments.enabled = false; grid.RefreshPresentation();
                grid.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                check(block.GetVector("_AmpTiming").w == 0f, "disabled source clears GPU effect gate");
                check(grid.EvaluateAmplifierOffset(left) == Vector2.zero, "disabled source clears CPU deformation");
                check(grid.AmplifierBoundsPadding == 0f, "disabled source clears culling padding");
                check(grid.EvaluateAmplifierColor(left, Color.white) == Color.white, "disabled source clears CPU tint");
                grid.amplifierTreatments = null; grid.RefreshPresentation();
                grid.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                check(block.GetVector("_AmpTiming").w == 0f, "missing reference cannot leave stale GPU feedback");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            Debug.Log("SINGULARITY amplifier: " + checks + " checks passed (capture, held, discharge, periodic projection, cleanup).");
        }
    }
}
#endif
