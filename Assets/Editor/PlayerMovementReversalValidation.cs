#if UNITY_EDITOR
using System;
using Massive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.EditorTools
{
    /// <summary>Checks joystick decisions against known input histories without running a player controller.</summary>
    public static class PlayerMovementReversalValidation
    {
        [MenuItem("MASSIVE/Player/Validate Joystick Reversal")]
        public static void RunMenu()
        {
            Debug.Log(RunChecks());
        }

        public static string RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run joystick reversal validation in Edit Mode.");

            int passed = 0;
            Action<bool, string> check = (condition, message) =>
            {
                if (!condition) throw new InvalidOperationException("Joystick reversal: " + message);
                passed++;
            };
            Action<Vector3, Vector3, string> same = (actual, expected, message) =>
                check(actual.Equals(expected), message + " (expected " + expected + ", got " + actual + ")");

            Scene fixture = EditorSceneManager.NewPreviewScene();
            try
            {
                Vector3 velocity = new Vector3(8f, 3f, 0f);
                Vector3 result;
                const float deadzone = 0.18f;
                float now = Time.time;

                var drop = Make(fixture, "Drop drift", 0f);
                check(!drop.TryApply(velocity, Vector3.right, deadzone, true, now, out result),
                    "first eligible sample establishes history");
                same(result, velocity, "history sampling preserves the full velocity");
                check(drop.TryApply(velocity, Vector3.left, deadzone, true, now + 0.01f, out result),
                    "a direct stick reversal drops drift");
                same(result, new Vector3(0f, 3f, 0f), "zero retention affects only horizontal velocity");

                var half = Make(fixture, "Retain half", 0.5f);
                half.TryApply(velocity, Vector3.right, deadzone, true, now, out result);
                check(half.TryApply(velocity, Vector3.left, deadzone, true, now + 0.01f, out result),
                    "half retention activates on reversal");
                same(result, new Vector3(4f, 3f, 0f), "half retention preserves vertical momentum");
                Vector3 retained = result;
                for (int frame = 0; frame < 8; frame++)
                {
                    check(!half.TryApply(retained, Vector3.left, deadzone, true, now + 0.02f + frame * 0.02f, out result),
                        "holding the reverse direction must not reapply assist");
                    same(result, retained, "held reversal must not compound momentum loss");
                }
                half.TryApply(retained, Vector3.zero, deadzone, true, now + 0.2f, out result);
                check(half.TryApply(retained, Vector3.left, deadzone, true, now + 0.21f, out result),
                    "release and repress can start another deliberate reversal");
                same(result, new Vector3(2f, 3f, 0f), "repress applies one new reduction");
                half.TryApply(velocity, Vector3.right, deadzone, true, now + 0.3f, out result);
                check(half.TryApply(velocity, Vector3.left, deadzone, true, now + 0.31f, out result),
                    "leaving and reentering the cone rearms the assist");

                var cone = Make(fixture, "Full-width cone", 0f);
                cone.backwardConeDegrees = 100f;
                cone.TryApply(velocity, Vector3.right, deadzone, true, now, out result);
                check(!cone.TryApply(velocity, Vector3.forward, deadzone, true, now + 0.01f, out result),
                    "sideways input is outside the backward cone");
                same(result, velocity, "sideways steering leaves momentum intact");
                Vector3 outside = Quaternion.AngleAxis(51f, Vector3.up) * Vector3.left;
                check(!cone.TryApply(velocity, outside, deadzone, true, now + 0.02f, out result),
                    "100-degree cone excludes 51 degrees from directly backward");
                same(result, velocity, "outside-cone steering is unchanged");
                Vector3 inside = Quaternion.AngleAxis(49f, Vector3.up) * Vector3.left;
                check(cone.TryApply(velocity, inside, deadzone, true, now + 0.03f, out result),
                    "100-degree cone includes 49 degrees from directly backward");
                same(result, new Vector3(0f, 3f, 0f), "inside-cone entry applies the configured reduction");

                var impact = Make(fixture, "Velocity changed by contact", 0f);
                impact.TryApply(velocity, Vector3.right, deadzone, true, now, out result);
                Vector3 bounced = new Vector3(-8f, 3f, 0f);
                check(!impact.TryApply(bounced, Vector3.right, deadzone, true, now + 0.01f, out result),
                    "a collision reversing velocity under an unchanged stick is not a stick reversal");
                same(result, bounced, "external momentum is not silently canceled by a held stick");

                var first = Make(fixture, "No earlier joystick input", 0f);
                check(!first.TryApply(velocity, Vector3.left, deadzone, true, now, out result),
                    "first input already pointing backward cannot cancel preexisting motion");
                same(result, velocity, "no-history sample preserves motion");
                first.ResetHistory();
                check(!first.TryApply(velocity, Vector3.left, deadzone, true, now + 0.01f, out result),
                    "explicit history reset requires a new eligible sample");

                var enabled = Make(fixture, "Assist switches", 0f);
                enabled.TryApply(velocity, Vector3.right, deadzone, true, now, out result);
                enabled.assistEnabled = false;
                check(!enabled.TryApply(velocity, Vector3.left, deadzone, true, now + 0.01f, out result),
                    "disabled assist does not change movement");
                same(result, velocity, "disabled assist preserves exact input velocity");
                enabled.assistEnabled = true;
                check(!enabled.TryApply(velocity, Vector3.left, deadzone, true, now + 0.02f, out result),
                    "reenabling does not reuse stale input history");
                enabled.enabled = false;
                check(!enabled.TryApply(velocity, Vector3.right, deadzone, true, now + 0.03f, out result),
                    "disabled component cannot apply assistance");
                same(result, velocity, "disabled component preserves exact velocity");

                var normal = Make(fixture, "100 percent retention", 1f);
                normal.TryApply(velocity, Vector3.right, deadzone, true, now, out result);
                check(!normal.TryApply(velocity, Vector3.left, deadzone, true, now + 0.01f, out result),
                    "100 percent retention remains ordinary movement");
                same(result, velocity, "100 percent retention is an exact no-op");

                var action = Make(fixture, "Protected action", 0f);
                action.TryApply(velocity, Vector3.right, deadzone, true, now, out result);
                check(!action.TryApply(velocity, Vector3.left, deadzone, false, now + 0.01f, out result),
                    "non-joystick motion excludes assistance");
                same(result, velocity, "protected action momentum stays intact");
                check(!action.TryApply(velocity, Vector3.left, deadzone, true, now + 0.02f, out result),
                    "leaving a protected action requires fresh history");
                same(result, velocity, "first ordinary sample after an action cannot brake it");

                var timed = Make(fixture, "Timed impact protection", 0f);
                timed.TryApply(velocity, Vector3.right, deadzone, true, now, out result);
                float blockedAt = Time.time;
                timed.BlockFor(0.2f);
                check(!timed.TryApply(velocity, Vector3.left, deadzone, true, blockedAt + 0.1f, out result),
                    "impact protection excludes a reversal during its 0.2-second window");
                same(result, velocity, "timed protection preserves full momentum");
                check(!timed.TryApply(velocity, Vector3.left, deadzone, true, blockedAt + 0.25f, out result),
                    "first sample after protection expires establishes history");
                timed.TryApply(velocity, Vector3.right, deadzone, true, blockedAt + 0.26f, out result);
                check(timed.TryApply(velocity, Vector3.left, deadzone, true, blockedAt + 0.27f, out result),
                    "a new reversal works after impact protection expires");

                var idle = Make(fixture, "Deadzone and rest", 0f);
                idle.TryApply(velocity, Vector3.right, deadzone, true, now, out result);
                foreach (Vector3 input in new[] { Vector3.zero, Vector3.left * 0.1f, Vector3.left * deadzone })
                {
                    check(!idle.TryApply(velocity, input, deadzone, true, now + 0.01f, out result),
                        "zero/deadzone input cannot trigger assistance");
                    same(result, velocity, "zero/deadzone input preserves velocity");
                }
                check(!idle.TryApply(new Vector3(0f, 3f, 0f), Vector3.left, deadzone, true, now + 0.02f, out result),
                    "vertical-only motion has no horizontal direction to reverse");
                same(result, new Vector3(0f, 3f, 0f), "vertical-only momentum is preserved");

                foreach (float size in new[] { 0.25f, 0.5f, 1f, 2f })
                {
                    var scaled = Make(fixture, "Velocity scale " + size, 0.5f);
                    Vector3 scaledVelocity = new Vector3(8f * size, 3f, 4f * size);
                    Vector3 forward = new Vector3(8f, 0f, 4f).normalized;
                    scaled.TryApply(scaledVelocity, forward, deadzone, true, now, out result);
                    check(scaled.TryApply(scaledVelocity, -forward, deadzone, true, now + 0.01f, out result),
                        "the same direction reversal works at different movement scales");
                    same(result, new Vector3(4f * size, 3f, 2f * size),
                        "retention is proportional and preserves Y at every movement scale");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(fixture);
            }

            return "Joystick reversal: " + passed + " checks passed. Temporary preview-scene fixtures removed; no player gameplay was started.";
        }

        private static PlayerMovementReversal Make(Scene scene, string name, float retention)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);
            SceneManager.MoveGameObjectToScene(go, scene);
            var assist = go.AddComponent<PlayerMovementReversal>();
            assist.assistEnabled = true;
            assist.backwardConeDegrees = 100f;
            assist.retainedMomentum = retention;
            // Only this isolated component is active; it has no gameplay dependencies.
            go.SetActive(true);
            return assist;
        }
    }
}
#endif
