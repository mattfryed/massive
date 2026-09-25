#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    /// <summary>Disposable chart/presentation fixture; never enters Play Mode or saves assets.</summary>
    public static class SingularityAmplifierAdapterValidation
    {
        [MenuItem("MASSIVE/SINGULARITY/Validate Wrapping Amplifier Adapter")]
        public static string Run()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Run wrapping Amplifier adapter validation in Edit Mode.");
            int checks = 0;
            Scene activeScene = SceneManager.GetActiveScene();
            int sceneCount = SceneManager.sceneCount;
            UnityEngine.Object selection = Selection.activeObject;
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var surfaceObject = new GameObject("Wrapping Core validation surface");
                SceneManager.MoveGameObjectToScene(surfaceObject, preview);
                var surface = surfaceObject.AddComponent<SingularitySurface>();
                surface.ConfigureProjectedBounds(28f, 12f, .875f, 3f, 2.1f);
                var actor = new GameObject("Wrapping Core validation actor");
                SceneManager.MoveGameObjectToScene(actor, preview);
                actor.transform.position = new Vector3(2f, 0f, -1f);
                actor.transform.rotation = Quaternion.Euler(12f, 20f, 8f);
                var body = actor.AddComponent<Rigidbody>();
                body.useGravity = false;
                var collider = actor.AddComponent<SphereCollider>(); collider.radius = .5f;
                var visualObject = new GameObject("Visual Root");
                visualObject.transform.SetParent(actor.transform, false);
                Transform visual = visualObject.transform;
                Vector3 authoredPosition = new Vector3(0f, .12f, 0f);
                Quaternion authoredRotation = Quaternion.Euler(7f, 11f, 23f);
                visual.localPosition = authoredPosition;
                visual.localRotation = authoredRotation;
                Vector3 lifecycleScale = new Vector3(.7f, .8f, .9f);
                visual.localScale = lifecycleScale;
                var core = actor.AddComponent<Massive.Multiplier.AmplifierCoreGameplay>();
                var adapter = actor.AddComponent<SingularityAmplifierAdapter>();
                Vector3 physicalStart = actor.transform.position;
                Vector3 visualStart = visual.position;
                Quaternion rotationStart = visual.rotation;
                adapter.Configure(surface, null, visual);

                Check(adapter.Core == core && adapter.Surface == surface && adapter.VisualRoot == visual,
                    "configuration retains original Core and presentation owner", ref checks);
                Check(actor.transform.position == physicalStart,
                    "configuration never projects the physical actor", ref checks);
                Check(Vector3.Distance(visual.position, visualStart) < .0001f,
                    "front presentation preserves authored world position", ref checks);
                Check(Quaternion.Angle(visual.rotation, rotationStart) < .02f,
                    "front presentation preserves authored rotation", ref checks);
                Check(visual.localScale == lifecycleScale && collider.transform == actor.transform,
                    "lifecycle scale and root collider remain independent", ref checks);
                Check(adapter.IsAttractionActive && adapter.AttractionRadius > 0f && adapter.AttractionPull > 0f,
                    "live Core offers independently tuned folded-grid attraction", ref checks);

                // Check the full loop including each join. No cumulative scale or
                // position changes are allowed as the Core's own reveal plays.
                for (int i = 0; i <= 24; i++)
                {
                    float s = surface.LoopLength * i / 24f;
                    adapter.SetSurfacePosition(new Vector2(3f, s));
                    Check(Vector3.Distance(adapter.RenderWorldPosition,
                        surface.WorldPosition(3f, s)) < .001f,
                        "continuous surface projection at sample " + i, ref checks);
                    lifecycleScale = Vector3.one * (.2f + .8f * i / 24f);
                    visual.localScale = lifecycleScale;
                    adapter.RefreshPresentation(); adapter.RefreshPresentation();
                    Check(visual.localScale == lifecycleScale,
                        "reveal scale is untouched at sample " + i, ref checks);
                }
                float rearMiddle = (surface.RearStart + surface.BottomStart) * .5f;
                adapter.SetSurfacePosition(new Vector2(3f, rearMiddle));
                Check(Mathf.Abs(adapter.RearWeight - 1f) < .0001f,
                    "rear face classification", ref checks);
                Check(Mathf.Abs(adapter.RenderWorldPosition.y + surface.Depth) < .001f,
                    "rear Core renders at genuine rear depth", ref checks);
                Check(Mathf.Abs(adapter.RenderWorldPosition.x - 3f * surface.RearScale) < .001f,
                    "rear Core follows narrowed face", ref checks);
                Check(actor.transform.position.z > surface.FrontHeight,
                    "rear physics is separate from overlapping front objects", ref checks);
                Check(Vector2.Distance(adapter.SmoothedAttractionPosition, adapter.SurfacePosition) < .0001f,
                    "placement snaps attraction without crossing faces", ref checks);

                float h = surface.FrontHeight * .5f;
                body.position = new Vector3(0f, 0f, surface.LoopLength - h + .25f);
                actor.transform.position = body.position;
                body.linearVelocity = new Vector3(1f, 0f, 3f);
                adapter.ConstrainMotion();
                Check(Mathf.Abs(body.position.z - (.25f - h)) < .001f,
                    "forward seam wraps to front chart", ref checks);
                Check(Vector3.Distance(body.linearVelocity, new Vector3(1f, 0f, 3f)) < .0001f,
                    "forward seam preserves momentum", ref checks);
                body.position = new Vector3(0f, 0f, -h - .25f);
                actor.transform.position = body.position;
                body.linearVelocity = new Vector3(-1f, 0f, -3f);
                adapter.ConstrainMotion();
                Check(Mathf.Abs(body.position.z - (surface.LoopLength - h - .25f)) < .001f,
                    "reverse seam wraps to bottom turn", ref checks);
                Check(Vector3.Distance(body.linearVelocity, new Vector3(-1f, 0f, -3f)) < .0001f,
                    "reverse seam preserves momentum", ref checks);

                for (int sign = -1; sign <= 1; sign += 2)
                {
                    body.position = new Vector3(sign * 20f, 0f, rearMiddle - h);
                    actor.transform.position = body.position;
                    body.linearVelocity = new Vector3(sign * 4f, 0f, 3f);
                    adapter.ConstrainMotion();
                    Check(Mathf.Abs(body.position.x - sign * (surface.Width * .5f - .55f)) < .001f,
                        "side limit includes Core radius and padding: " + sign, ref checks);
                    Check(Mathf.Abs(body.linearVelocity.x + sign * 4f * core.Effective_wallRestitution) < .0001f
                        && Mathf.Abs(body.linearVelocity.z - 3f) < .0001f,
                        "side rebound uses Core tuning and preserves tangential momentum: " + sign, ref checks);
                    body.position = new Vector3(sign * 20f, 0f, 0f);
                    actor.transform.position = body.position;
                    body.linearVelocity = new Vector3(-sign, 0f, 2f);
                    adapter.ConstrainMotion();
                    Check(Mathf.Abs(body.linearVelocity.x + sign) < .0001f,
                        "already returning Core does not double-bounce: " + sign, ref checks);
                }
                body.linearVelocity = Vector3.zero;
                body.isKinematic = true;
                body.position = new Vector3(20f, 0f, surface.LoopLength);
                actor.transform.position = body.position;
                adapter.ConstrainMotion();
                Check(body.position == new Vector3(20f, 0f, surface.LoopLength),
                    "kinematic lifecycle motion is not constrained", ref checks);
                body.isKinematic = false;

                adapter.SetSurfacePosition(new Vector2(2f, 2f));
                body.linearVelocity = new Vector3(1f, 0f, 2f);
                adapter.SetSurfacePosition(new Vector2(3f, 3f), false);
                Check(body.linearVelocity == new Vector3(1f, 0f, 2f),
                    "explicit placement may retain velocity", ref checks);
                Vector2 beforeInvalid = adapter.SurfacePosition;
                adapter.SetSurfacePosition(new Vector2(float.NaN, 0f));
                Check(adapter.SurfacePosition == beforeInvalid,
                    "non-finite position is rejected", ref checks);
                adapter.SetSurfacePosition(new Vector2(3f, rearMiddle));
                Check(body.linearVelocity == Vector3.zero,
                    "ordinary explicit placement clears velocity", ref checks);
                visual.localScale = new Vector3(.3f, .4f, .5f);
                adapter.RestorePresentation();
                Check(Vector3.Distance(visual.localPosition, authoredPosition) < .0001f
                    && Quaternion.Angle(visual.localRotation, authoredRotation) < .02f,
                    "cleanup restores exact presentation local pose", ref checks);
                Check(visual.localScale == new Vector3(.3f, .4f, .5f),
                    "cleanup never resets lifecycle scale", ref checks);
                adapter.enabled = false;
                Check(!adapter.IsAttractionActive && !adapter.IsRenderingOnSurface,
                    "disabled adapter contributes neither rendering nor attraction", ref checks);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            Check(SceneManager.GetActiveScene() == activeScene && SceneManager.sceneCount == sceneCount
                && Selection.activeObject == selection, "loaded scenes and selection remain unchanged", ref checks);
            string report = "SINGULARITY wrapping Amplifier adapter: PASS " + checks
                + " checks (front/rear projection, lifecycle scale, periodic momentum, side restitution, isolated physics, cleanup).";
            Debug.Log(report); return report;
        }

        private static void Check(bool condition, string message, ref int checks)
        {
            if (!condition) throw new InvalidOperationException("SINGULARITY wrapping Core: " + message);
            checks++;
        }
    }
}
#endif
