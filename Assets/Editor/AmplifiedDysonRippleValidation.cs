#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Massive.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Focused, synchronous checks of the real panel pose in an unsaved preview scene.</summary>
public static class AmplifiedDysonRippleValidation
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const float Step = .025f;

    [MenuItem("MASSIVE/Enemies/Amplified Dyson/Validate Point Ripple")]
    private static void RunFromMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Use Edit Mode after compilation.");

        var checks = new List<string>();
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var normal = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DysonRepulsorSetup.DefinitionPath);
            Check(normal && normal.prefab && !normal.prefab.GetComponentInChildren<DysonSpherePanels>(true).shimmerEnabled,
                "Normal Dyson keeps panel flipping disabled", checks);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AmplifiedDysonSetup.PrefabPath);
            if (!prefab) throw new InvalidOperationException("Amplified Dyson prefab is missing.");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            var panels = root.GetComponentInChildren<DysonSpherePanels>(true);
            var serialized = new SerializedObject(panels);
            serialized.FindProperty("generateInEditMode").boolValue = true;
            // This visual fixture has no live enemy health or core simulation.
            serialized.FindProperty("enemy").objectReferenceValue = null;
            serialized.FindProperty("core").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            panels.breatheLiftAmplitude = panels.breatheInsetAmplitude = 0f;
            panels.shimmerEnabled = true;
            panels.shimmerAmplitudeDegrees = 180f;
            panels.shimmerFrequency = .15f;
            panels.shimmerWaveTravelSeconds = 1.8f;
            panels.shimmerPanelFlipSeconds = .28f;
            panels.shimmerOriginDirection = Vector3.up;
            panels.enabled = true;
            var fixture = new Fixture(panels);
            Check(fixture.Items.Length == 80, "Canonical 80-panel shell generated in the isolated preview scene", checks);
            Check(AmplifiedDysonValidation.ValidateFlipOutline(panels) < .00001f,
                "Full flips expose reverse faces within the original triangle outlines", checks);

            ValidatePropagation(fixture, 10f, false, checks);
            ValidatePropagation(fixture, 30f, true, checks);

            fixture.Reset();
            fixture.Pose(50f);
            float period = Mathf.Max(1f / panels.shimmerFrequency,
                panels.shimmerWaveTravelSeconds + panels.shimmerPanelFlipSeconds);
            fixture.Pose(50f + period - .001f);
            Check(fixture.Angles().All(a => a > 179.9f), "Every panel settles on its reverse face before the next ripple", checks);
            var held = fixture.Rotations();
            fixture.Pose(50f + period);
            Check(SameRotations(held, fixture.Rotations()), "First repeat boundary keeps the held face without a reset pop", checks);
            fixture.Pose(50f + 2f * period - .001f);
            Check(fixture.Angles().All(a => a < .05f), "The second outward ripple restores the original faces", checks);
            held = fixture.Rotations();
            fixture.Pose(50f + 2f * period);
            Check(SameRotations(held, fixture.Rotations()), "Second repeat boundary also remains continuous", checks);

            fixture.Reset();
            fixture.Pose(80f);
            fixture.Pose(80.7f);
            held = fixture.Rotations();
            var arrivals = fixture.Arrivals();
            fixture.Prepare(80.7f);
            fixture.Pose(80.7f);
            fixture.Pose(80.7f);
            Check(SameRotations(held, fixture.Rotations()) && arrivals.SequenceEqual(fixture.Arrivals()),
                "Repeated poses at the same clock freeze both rotation and arrival timing", checks);
            fixture.Pose(79f);
            Check(fixture.Angles().All(a => a < .05f), "Clock rewind starts a fresh outward ripple", checks);
            fixture.Pose(79.7f);
            Check(fixture.Angles().Any(a => a > 10f), "A fresh ripple progresses after clock rewind", checks);
            panels.PlaySpawnIn();
            fixture.Pose(79.7f);
            Check(fixture.Angles().All(a => a < .05f), "Explicit spawn restart resets the ripple for the next life", checks);

            fixture.Pose(80.4f);
            panels.shimmerFrequency = 0f;
            fixture.Pose(80.5f);
            Check(fixture.Angles().All(a => a < .05f), "Zero frequency disables flipping", checks);
            panels.shimmerFrequency = .15f;
            fixture.Pose(81f);
            fixture.Pose(81.7f);
            Check(fixture.Angles().Any(a => a > 10f), "Re-enabling frequency starts a functional fresh ripple", checks);
            panels.shimmerAmplitudeDegrees = 0f;
            fixture.Pose(81.8f);
            Check(fixture.Angles().All(a => a < .05f), "Zero amplitude disables flipping", checks);

            return "PASSED (" + checks.Count + " checks)\n" + string.Join("\n", checks);
        }
        catch (Exception error)
        {
            return "FAILED after " + checks.Count + " checks\n" + string.Join("\n", checks) + "\n" + error;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void ValidatePropagation(Fixture fixture, float start, bool spin, List<string> checks)
    {
        fixture.Reset();
        fixture.Pose(start);
        var arrivals = fixture.Arrivals();
        var distances = fixture.Items.Select(p => Vector3.Angle(
            fixture.Panels.transform.TransformDirection(p.BaseNormal), fixture.Panels.shimmerOriginDirection)).ToArray();
        Check(arrivals.Select((arrival, i) => Mathf.Abs(arrival - distances[i] / 180f * fixture.Panels.shimmerWaveTravelSeconds)).All(error => error < .00001f),
            (spin ? "Spinning" : "Stationary") + " wave arrival times correspond to angular distance from the selected origin", checks);
        var firstMovement = Enumerable.Repeat(float.PositiveInfinity, arrivals.Length).ToArray();
        Quaternion shellRotation = fixture.Panels.transform.rotation;
        float duration = fixture.Panels.shimmerWaveTravelSeconds + fixture.Panels.shimmerPanelFlipSeconds;
        bool fixedArrivals = true, waitsForArrival = true, actualPoseMatches = true;
        int steps = Mathf.CeilToInt(duration / Step);
        for (int step = 0; step <= steps; step++)
        {
            float elapsed = step * Step;
            if (spin) fixture.Panels.transform.rotation = Quaternion.AngleAxis(elapsed * 22f, Vector3.forward) * shellRotation;
            fixture.Pose(start + elapsed);
            fixedArrivals &= arrivals.SequenceEqual(fixture.Arrivals());
            var angles = fixture.Angles();
            for (int i = 0; i < angles.Length; i++)
            {
                if (elapsed + .0001f < arrivals[i] && angles[i] > .05f) waitsForArrival = false;
                if (angles[i] > .5f && float.IsPositiveInfinity(firstMovement[i])) firstMovement[i] = elapsed;
                actualPoseMatches &= Quaternion.Angle(fixture.Items[i].Transform.localRotation,
                    fixture.Apply(i, start + elapsed)) < .05f;
            }
        }
        var order = Enumerable.Range(0, arrivals.Length).OrderBy(i => distances[i]).ToArray();
        bool ordered = firstMovement.All(t => !float.IsPositiveInfinity(t));
        for (int i = 1; i < order.Length; i++)
            if (distances[order[i]] - distances[order[i - 1]] > .001f)
                ordered &= firstMovement[order[i - 1]] <= firstMovement[order[i]] + .0001f;
        string mode = spin ? "With shell spin" : "Without shell spin";
        Check(waitsForArrival && ordered && firstMovement.Max() - firstMovement.Min() > .8f,
            mode + ", geometry flips progressively outward from the origin", checks);
        Check(fixedArrivals, mode + ", each panel retains its captured arrival time", checks);
        Check(actualPoseMatches, mode + ", SetRadialPose applies the actual ripple rotation", checks);
        Check(fixture.Angles().All(a => a > 179.9f), mode + ", every panel finishes the full flip", checks);
    }

    private static bool SameRotations(Quaternion[] a, Quaternion[] b) =>
        a.Length == b.Length && a.Select((q, i) => Quaternion.Angle(q, b[i])).All(angle => angle < .05f);

    private static void Check(bool pass, string message, List<string> checks)
    {
        if (!pass) throw new InvalidOperationException(message);
        checks.Add("PASS " + message);
    }

    private sealed class Fixture
    {
        public readonly DysonSpherePanels Panels;
        public readonly PanelData[] Items;
        private readonly MethodInfo reset, prepare, apply;

        public Fixture(DysonSpherePanels panels)
        {
            Panels = panels;
            var type = typeof(DysonSpherePanels);
            reset = type.GetMethod("ResetShimmerWave", Hidden);
            prepare = type.GetMethod("PrepareShimmerWave", Hidden);
            apply = type.GetMethod("ApplyShimmer", Hidden);
            Items = ((IEnumerable)type.GetField("_panels", Hidden).GetValue(panels)).Cast<object>().Select(p => new PanelData(p)).ToArray();
        }
        public void Reset() => reset.Invoke(Panels, null);
        public void Prepare(float clock) => prepare.Invoke(Panels, new object[] { clock });
        public void Pose(float clock) => Panels.SetRadialPose(clock, 1f, 0f, 1f, 0f, 0f);
        public Quaternion Apply(int i, float clock) => (Quaternion)apply.Invoke(Panels, new object[] { Items[i].Value, Items[i].BaseRotation, clock });
        public float[] Arrivals() => Items.Select(p => p.Arrival).ToArray();
        public Quaternion[] Rotations() => Items.Select(p => p.Transform.localRotation).ToArray();
        public float[] Angles() => Items.Select(p => Quaternion.Angle(p.BaseRotation, p.Transform.localRotation)).ToArray();
    }

    private sealed class PanelData
    {
        public readonly object Value;
        public readonly Transform Transform;
        public readonly Quaternion BaseRotation;
        public readonly Vector3 BaseNormal;
        private readonly FieldInfo arrival;
        public float Arrival => (float)arrival.GetValue(Value);

        public PanelData(object value)
        {
            Value = value;
            var type = value.GetType();
            Transform = (Transform)type.GetField("tf").GetValue(value);
            BaseRotation = (Quaternion)type.GetField("baseRotLocal").GetValue(value);
            BaseNormal = (Vector3)type.GetField("baseNormalLocal").GetValue(value);
            arrival = type.GetField("shimmerArrivalSeconds");
        }
    }
}
#endif
