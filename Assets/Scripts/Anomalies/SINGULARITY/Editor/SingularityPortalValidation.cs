using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    /// <summary>Dependency-free checks for the portal's two-face coordinate contract.</summary>
    public static class SingularityPortalValidation
    {
        [MenuItem("MASSIVE/SINGULARITY/Validate Portal Face Mapping")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Run portal face mapping validation in Edit Mode.");
            Scene preview = EditorSceneManager.NewPreviewScene();
            int checks = 0;
            try
            {
                var root = new GameObject("Isolated portal validation") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(root, preview);
                var surface = root.AddComponent<SingularitySurface>();
                var portal = root.AddComponent<SingularityBlackHolePortal>();
                portal.surface = surface;

                foreach (float scale in new[] { .5f, .875f, 1f })
                {
                    surface.Configure(28f, 12f, scale, 3f, 2.1f);
                    foreach (Vector2 expected in new[] { Vector2.zero, new Vector2(2f, 3f), new Vector2(-4f, -2f) })
                    {
                        Vector2 front = new Vector2(expected.x, expected.y + surface.FrontHeight * .5f);
                        Vector2 rear = new Vector2(expected.x,
                            surface.RearStart + (surface.FrontHeight * .5f - expected.y) * scale);
                        Require(portal.TryGetFaceCoordinates(front, out Vector2 frontPoint, out bool frontFlag) &&
                            !frontFlag && Vector2.Distance(frontPoint, expected) < .0001f,
                            "Front mapping mismatch", ref checks);
                        Require(portal.TryGetFaceCoordinates(rear, out Vector2 rearPoint, out bool rearFlag) &&
                            rearFlag && Vector2.Distance(rearPoint, expected) < .0001f,
                            "Rear mapping mismatch", ref checks);
                    }

                    Require(!portal.TryGetFaceCoordinates(new Vector2(0f,
                        (surface.TopStart + surface.RearStart) * .5f), out _, out _),
                        "Top bend must not capture", ref checks);
                    Require(!portal.TryGetFaceCoordinates(new Vector2(0f,
                        (surface.BottomStart + surface.LoopLength) * .5f), out _, out _),
                        "Bottom bend must not capture", ref checks);
                    Require(portal.TryGetFaceCoordinates(new Vector2(0f,
                        surface.LoopLength + surface.FrontHeight * .5f), out Vector2 wrapped, out bool wrappedRear) &&
                        !wrappedRear && wrapped.sqrMagnitude < .000001f,
                        "Periodic front mapping mismatch", ref checks);
                }

                Require(!portal.TryGetFaceCoordinates(new Vector2(float.NaN, 2f), out _, out _),
                    "NaN coordinate accepted", ref checks);
                Require(!portal.TryGetFaceCoordinates(new Vector2(0f, float.PositiveInfinity), out _, out _),
                    "Infinite coordinate accepted", ref checks);
                portal.surface = null;
                Require(!portal.TryGetFaceCoordinates(Vector2.zero, out _, out _),
                    "Missing surface accepted", ref checks);
                string result = "SINGULARITY portal face mapping: " + checks + " checks passed. " +
                    "Capture/re-entry, mass preservation, and visual order require Play Mode integration checks.";
                return result;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void Require(bool passed, string message, ref int count)
        {
            if (!passed) throw new InvalidOperationException("SINGULARITY portal validation: " + message);
            count++;
        }
    }
}
