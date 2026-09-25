#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    /// <summary>Isolated real-controller sequence validation. No authored scene,
    /// player prefab, shared material, or serialized movement value is edited.</summary>
    public static class SingularityTransitValidation
    {
        [MenuItem("MASSIVE/SINGULARITY/Validate Black Hole Transit")]
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run transit validation in Edit Mode.");
            int checks = 0;
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                CheckCurves(ref checks);
                var root = new GameObject("Isolated transit validation");
                SceneManager.MoveGameObjectToScene(root, preview);
                var surface = root.AddComponent<SingularitySurface>();
                var portal = root.AddComponent<SingularityBlackHolePortal>();
                var a = MakeActor("Front participant", preview, surface);
                var b = MakeActor("Rear participant", preview, surface);
                portal.Configure(surface, new[] { a, b });
                var bodyA = a.GetComponent<Rigidbody>();
                var bodyB = b.GetComponent<Rigidbody>();
                a.Player.SetControlMode(PlayerControlMode.Scripted);
                b.Player.SetControlMode(PlayerControlMode.Disabled);
                a.Player.massScore = .63f;
                int life = a.Player.LifeSequence;
                int transfers = 0, completed = 0, cancelled = 0;
                portal.FaceTransferred += (actor, rear) => transfers++;
                portal.TransitCompleted += actor => completed++;
                portal.TransitCancelled += actor => cancelled++;

                a.SetSurfacePosition(portal.FaceToSurface(new Vector2(-1.5f, 0f), false));
                b.SetSurfacePosition(portal.FaceToSurface(new Vector2(1.5f, 0f), true));
                portal.EvaluateCapture(10f);
                Check(portal.PhaseOf(a) == SingularityBlackHolePortal.TransitPhase.Entering &&
                    portal.PhaseOf(b) == SingularityBlackHolePortal.TransitPhase.Entering,
                    "Both faces start independent entry", ref checks);
                Check(bodyA.isKinematic && bodyB.isKinematic && !bodyA.detectCollisions && !bodyB.detectCollisions,
                    "Transit owns physics without contacts", ref checks);
                Check(a.Player.IsSingularityTransitControlled && b.Player.IsSingularityTransitControlled,
                    "Independent input ownership tokens", ref checks);
                Check(a.Player.ControlMode == PlayerControlMode.Scripted && b.Player.ControlMode == PlayerControlMode.Disabled,
                    "Control modes remain untouched", ref checks);
                portal.AdvanceTransit(0f, 20f);
                Check(portal.ElapsedOf(a) == 0f && transfers == 0, "Paused step never moves or transfers", ref checks);
                portal.AdvanceTransit(portal.entrySeconds * .5f, 20.1f);
                Check(transfers == 0 && a.RearWeight < .01f && b.RearWeight > .99f,
                    "Half entry retains original face", ref checks);
                Check(a.HasPortalVisual && a.PortalVisualScale < 1f && a.PortalVisualStretch > 1f,
                    "Entry deforms only presentation", ref checks);
                Check(bodyA.transform.localScale == Vector3.one, "Physics transform scale is unchanged", ref checks);
                portal.AdvanceTransit(portal.entrySeconds * .5f + .001f, 20.5f);
                Check(transfers == 2 && portal.TeleportCount == 2,
                    "Exactly one transfer per participant", ref checks);
                Check(a.RearWeight > .99f && b.RearWeight < .01f,
                    "Both directions reach opposite face", ref checks);
                Check(portal.PhaseOf(a) == SingularityBlackHolePortal.TransitPhase.Exiting,
                    "Transfer immediately begins exit", ref checks);
                portal.TryGetFaceCoordinates(a.SurfacePosition, out Vector2 centerA, out _);
                Check(centerA.sqrMagnitude < .000001f && a.PortalVisualScale <= portal.minimumVisualScale + .00001f,
                    "Transfer occurs centered at minimum visual size", ref checks);
                portal.AdvanceTransit(portal.exitSeconds * .5f, 20.8f);
                portal.TryGetFaceCoordinates(a.SurfacePosition, out Vector2 exitingA, out _);
                portal.TryGetFaceCoordinates(b.SurfacePosition, out Vector2 exitingB, out _);
                Check(exitingA.x > 0f && exitingB.x < 0f,
                    "Exit continues through to opposite entry side", ref checks);
                Check(portal.IsInTransit(a) && a.PortalVisualScale > portal.minimumVisualScale,
                    "Exit restores scale while retaining movement ownership", ref checks);
                portal.AdvanceTransit(portal.exitSeconds, 21.2f);
                portal.TryGetFaceCoordinates(a.SurfacePosition, out Vector2 endA, out _);
                Check(completed == 2 && !portal.IsInTransit(a) && !portal.IsInTransit(b),
                    "Both sequences finish independently", ref checks);
                Check(endA.magnitude > portal.EffectiveAttractionRadius + .1f,
                    "Exit clears attraction before returning control", ref checks);
                Check(!bodyA.isKinematic && bodyA.detectCollisions && !a.Player.IsSingularityTransitControlled && !a.HasPortalVisual,
                    "Physics, collision, controls and visual state released", ref checks);
                Check(bodyA.linearVelocity.x > 0f && bodyB.linearVelocity.x < 0f,
                    "Outward release momentum points away from mouth", ref checks);
                Check(Mathf.Abs(a.Player.massScore - .63f) < .00001f && a.Player.LifeSequence == life && !a.Player.temporarilyEliminated,
                    "Transit preserves mass and life", ref checks);
                a.SetSurfacePosition(portal.FaceToSurface(Vector2.zero, true));
                portal.EvaluateCapture(21.3f);
                portal.EvaluateCapture(100f);
                Check(!portal.IsInTransit(a) && transfers == 2,
                    "Time alone cannot recapture a latched actor inside mouth", ref checks);
                a.SetSurfacePosition(portal.FaceToSurface(new Vector2(5f, 0f), true));
                portal.EvaluateCapture(101f);
                a.SetSurfacePosition(portal.FaceToSurface(new Vector2(.5f, 0f), true));
                portal.EvaluateCapture(102f);
                Check(portal.IsInTransit(a), "Leaving and reentering rear mouth rearms", ref checks);
                portal.CancelAll();
                Check(cancelled == 1 && !bodyA.isKinematic && bodyA.detectCollisions && !a.HasPortalVisual,
                    "Explicit cancellation fully releases living actor", ref checks);

                // An unrelated owner may change match/life state at any point.
                portal.Configure(surface, new[] { a });
                a.SetSurfacePosition(portal.FaceToSurface(new Vector2(1f, 0f), false));
                a.Player.SetMatchInputLocked(true);
                portal.EvaluateCapture(110f);
                Check(!portal.IsInTransit(a), "Existing match input lock blocks capture", ref checks);
                a.Player.SetMatchInputLocked(false);
                portal.EvaluateCapture(111f);
                a.Player.SetMatchInputLocked(true);
                portal.AdvanceTransit(0f, 111f);
                Check(!portal.IsInTransit(a) && a.Player.IsMatchInputLocked && !a.Player.IsSingularityTransitControlled,
                    "New external lock cancels without clearing external lock", ref checks);
                a.Player.SetMatchInputLocked(false);
                portal.Configure(surface, new[] { a });
                portal.EvaluateCapture(111.5f);
                a.Player.SetWorldGameplaySuppressed(true);
                portal.AdvanceTransit(0f, 111.5f);
                Check(!portal.IsInTransit(a) && !bodyA.isKinematic && bodyA.detectCollisions &&
                    !a.Player.IsSingularityTransitControlled && bodyA.linearVelocity == Vector3.zero,
                    "World suppression releases only portal-owned physics", ref checks);
                a.Player.SetWorldGameplaySuppressed(false);
                Check(!bodyA.isKinematic && a.Player.CanStartSingularityTransit,
                    "Un-suppression cannot inherit a frozen portal body", ref checks);
                portal.Configure(surface, new[] { a });
                portal.EvaluateCapture(112f);
                a.Player.temporarilyEliminated = true;
                portal.AdvanceTransit(0f, 112f);
                Check(!portal.IsInTransit(a) && bodyA.isKinematic && bodyA.detectCollisions && !a.Player.IsSingularityTransitControlled,
                    "Death cancellation preserves death-owned kinematic state", ref checks);
                a.Player.temporarilyEliminated = false;
                bodyA.isKinematic = false;
                portal.Configure(surface, new[] { a });
                portal.EvaluateCapture(113f);
                a.Player.enabled = false;
                portal.AdvanceTransit(0f, 113f);
                Check(!portal.IsInTransit(a) && !a.Player.enabled && bodyA.detectCollisions,
                    "Disabled controller remains disabled and ownership releases", ref checks);
                a.Player.enabled = true;
                portal.Configure(surface, new[] { a });
                portal.EvaluateCapture(114f);
                portal.enabled = false;
                portal.AdvanceTransit(0f, 114f);
                Check(!portal.IsInTransit(a) && !a.Player.IsSingularityTransitControlled && bodyA.detectCollisions && !a.HasPortalVisual,
                    "Portal disable clears all outstanding leases", ref checks);
                string report = "SINGULARITY black-hole transit: PASS " + checks +
                    " isolated checks (field curves, both-face sequences, visual handoff, controls, no damage, cancellation and re-entry).";
                Debug.Log(report);
                return report;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static SingularityPlayerAdapter MakeActor(string name, Scene scene, SingularitySurface surface)
        {
            var obj = new GameObject(name);
            SceneManager.MoveGameObjectToScene(obj, scene);
            var body = obj.AddComponent<Rigidbody>();
            body.useGravity = false;
            obj.AddComponent<PlayerControllerScript>();
            var adapter = obj.AddComponent<SingularityPlayerAdapter>();
            adapter.Configure(surface);
            return adapter;
        }
        private static void CheckCurves(ref int checks)
        {
            Check(Mathf.Abs(SingularityBlackHolePortal.ExponentialEaseIn(0f, 3.5f)) < .000001f &&
                Mathf.Abs(SingularityBlackHolePortal.ExponentialEaseIn(1f, 3.5f) - 1f) < .000001f,
                "Exponential curve exact endpoints", ref checks);
            float previous = 0f, previousStep = 0f;
            for (int i = 1; i <= 10; i++)
            {
                float next = SingularityBlackHolePortal.ExponentialEaseIn(i / 10f, 3.5f);
                float step = next - previous;
                Check(next > previous && step > previousStep, "Entry speed increases continuously " + i, ref checks);
                previous = next; previousStep = step;
            }
            Vector2 inward = SingularityBlackHolePortal.FieldAcceleration(new Vector2(2f, 0f), 4f, 10f, 0f, 0f, 1f);
            Vector2 mirrored = SingularityBlackHolePortal.FieldAcceleration(new Vector2(-2f, 0f), 4f, 10f, 0f, 0f, 1f);
            Check(inward.x < 0f && inward.y == 0f && (inward + mirrored).sqrMagnitude < .000001f,
                "Attraction is radially symmetric", ref checks);
            Check(SingularityBlackHolePortal.FieldAcceleration(Vector2.zero, 4f, 10f, 3f, 1f, 1f) == Vector2.zero,
                "No center singularity", ref checks);
            Check(SingularityBlackHolePortal.FieldAcceleration(new Vector2(4f, 0f), 4f, 10f, 3f, 1f, 1f) == Vector2.zero,
                "Field has finite support", ref checks);
            Check(SingularityBlackHolePortal.FieldAcceleration(new Vector2(3.999f, 0f), 4f, 10f, 3f, 1f, 1f).magnitude < .00001f,
                "Outer field feathers to zero smoothly", ref checks);
            Check(SingularityBlackHolePortal.FieldAcceleration(new Vector2(2f, 0f), 4f, 0f, 0f, 10f, 1f).x > 0f,
                "Centrifugal dial is independently outward", ref checks);
            Check(SingularityBlackHolePortal.FieldAcceleration(new Vector2(2f, 0f), 4f, 0f, 10f, 0f, 1f).y > 0f &&
                SingularityBlackHolePortal.FieldAcceleration(new Vector2(2f, 0f), 4f, 0f, -10f, 0f, 1f).y < 0f,
                "Swirl sign reverses tangential acceleration", ref checks);
            Check(SingularityBlackHolePortal.FieldAcceleration(new Vector2(float.NaN, 0f), 4f, 10f, 3f, 1f, 1f) == Vector2.zero,
                "Invalid field coordinates rejected", ref checks);
            previous = 0f; previousStep = float.PositiveInfinity;
            for (int i = 1; i <= 10; i++)
            {
                float next = SingularityBlackHolePortal.ExitTravel(i / 10f, 3.5f, .6f, 4.2f, 2.5f);
                float step = next - previous;
                Check(next > previous && step < previousStep, "Exit speed decreases continuously " + i, ref checks);
                previous = next; previousStep = step;
            }
            float terminalDerivative = (SingularityBlackHolePortal.ExitTravel(1f, 3.5f, .6f, 4.2f, 2.5f) -
                SingularityBlackHolePortal.ExitTravel(.999f, 3.5f, .6f, 4.2f, 2.5f)) * 4.2f / (.001f * .6f);
            Check(Mathf.Abs(terminalDerivative - 2.5f) < .015f, "Exit joins release velocity continuously", ref checks);
        }
        private static void Check(bool condition, string message, ref int count)
        { if (!condition) throw new InvalidOperationException("SINGULARITY transit: " + message); count++; }
    }
}
#endif
