#if UNITY_EDITOR
using System;
using Massive.Multiplier;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    /// <summary>Isolated Core transit checks. No real score service, authored
    /// scene, shared prefab, tuning asset or player controller is changed.</summary>
    public static class SingularityCoreTransitValidation
    {
        [MenuItem("MASSIVE/SINGULARITY/Validate Core Black Hole Transit")]
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run isolated Core transit validation in Edit Mode.");
            int checks = 0;
            Action<bool, string> check = (value, message) => {
                if (!value) throw new InvalidOperationException("SINGULARITY Core transit: " + message);
                checks++;
            };
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Isolated Core portal validation");
                SceneManager.MoveGameObjectToScene(root, preview);
                var surface = root.AddComponent<SingularitySurface>();
                var portal = root.AddComponent<SingularityBlackHolePortal>();
                portal.Configure(surface, new SingularityPlayerAdapter[0]);
                var a = MakeCore("Front Core", preview, surface, portal);
                var b = MakeCore("Rear Core", preview, surface, portal);
                int captures = 0, transfers = 0, completions = 0, cancellations = 0;
                a.Core.Captured += core => captures++;
                b.Core.Captured += core => captures++;
                portal.CoreFaceTransferred += (actor, rear) => transfers++;
                portal.CoreTransitCompleted += actor => completions++;
                portal.CoreTransitCancelled += actor => cancellations++;
                Vector3 visualScale = a.VisualRoot.localScale;
                Vector3 physicalScale = a.transform.localScale;
                float mass = a.Body.mass;
                Transform visualParent = a.VisualRoot.parent;
                portal.RegisterCore(a); // Duplicate registration must be harmless.

                a.SetSurfacePosition(portal.FaceToSurface(new Vector2(-1.5f, 0f), false));
                b.SetSurfacePosition(portal.FaceToSurface(new Vector2(1.5f, 0f), true));
                portal.EvaluateCapture(10f);
                check(portal.PhaseOf(a) == SingularityBlackHolePortal.TransitPhase.Entering &&
                    portal.PhaseOf(b) == SingularityBlackHolePortal.TransitPhase.Entering, "Both mouths accept Core independently");
                check(a.Core.IsTransitOwnedBy(portal) && b.Core.IsTransitOwnedBy(portal), "Core owns explicit transit lease");
                check(!a.Core.IsCaptured && !b.Core.IsCaptured, "Portal never represents a goal capture");
                check(a.Body.isKinematic && !a.Body.detectCollisions && !a.GetComponent<Collider>().enabled,
                    "Core body and contacts are suspended");
                check(!a.Core.TryAcquireTransit(surface), "Second owner cannot steal transit");
                a.Core.ReleaseTransit(surface, Vector3.one);
                check(a.Core.IsTransitOwnedBy(portal) && a.Body.isKinematic, "Foreign release cannot affect transit");
                portal.AdvanceTransit(0f, 10.1f);
                check(portal.ElapsedOf(a) == 0f && transfers == 0, "Zero-delta step preserves pause");
                portal.AdvanceTransit(portal.entrySeconds * .5f, 10.4f);
                check(a.RearWeight < .01f && b.RearWeight > .99f && transfers == 0, "Entry remains on original face");
                check(a.HasPortalVisual && a.PortalVisualScale < 1f && a.PortalVisualStretch > 1f,
                    "Visible shrink and directional stretch during entry");
                check(a.VisualRoot.localScale == visualScale && a.transform.localScale == physicalScale,
                    "Portal never edits lifecycle or physics scale");
                check(a.VisualRoot.lossyScale.magnitude < visualScale.magnitude * physicalScale.x,
                    "Deformation parent produces real visible size reduction; visual local=" + a.VisualRoot.localScale +
                    " world=" + a.VisualRoot.lossyScale + " holder local=" + a.VisualRoot.parent.localScale +
                    " world=" + a.VisualRoot.parent.lossyScale + " body=" + a.transform.lossyScale +
                    " resting=" + visualScale + "/" + physicalScale + " shape=" + a.PortalVisualScale + "/" + a.PortalVisualStretch);
                check(a.Body.mass == mass, "Mass unchanged");
                Vector3 once = a.VisualRoot.lossyScale;
                a.RefreshPresentation(); a.RefreshPresentation();
                check((once - a.VisualRoot.lossyScale).sqrMagnitude < .000001f, "Repeated rendering cannot compound scale");
                portal.AdvanceTransit(portal.entrySeconds, 11f);
                check(transfers == 2 && portal.CoreTeleportCount == 2 && portal.TeleportCount == 0,
                    "Exactly one transfer per Core, no player counter changes");
                check(a.RearWeight > .99f && b.RearWeight < .01f, "Front and rear swap at horizon");
                portal.TryGetFaceCoordinates(a.SurfacePosition, out Vector2 center, out _);
                check(center.sqrMagnitude < .000001f && a.PortalVisualScale <= portal.minimumVisualScale + .00001f,
                    "Transfer happens at center, fully inside event horizon");
                check(portal.PhaseOf(a) == SingularityBlackHolePortal.TransitPhase.Exiting, "Transfer begins inverse exit");
                portal.AdvanceTransit(portal.exitSeconds * .5f, 11.3f);
                portal.TryGetFaceCoordinates(a.SurfacePosition, out Vector2 exitingA, out _);
                portal.TryGetFaceCoordinates(b.SurfacePosition, out Vector2 exitingB, out _);
                check(exitingA.x > 0f && exitingB.x < 0f, "Exit proceeds opposite the approach side");
                portal.AdvanceTransit(portal.exitSeconds, 12f);
                check(completions == 2 && !portal.IsInTransit(a) && !portal.IsInTransit(b), "Both Core sequences complete");
                check(!a.Body.isKinematic && a.Body.detectCollisions && a.GetComponent<Collider>().enabled,
                    "Core collision and dynamics restored exactly");
                check(a.Body.linearVelocity.x > 0f && b.Body.linearVelocity.x < 0f, "Outward release momentum retained");
                portal.TryGetFaceCoordinates(a.SurfacePosition, out Vector2 endpoint, out _);
                check(endpoint.magnitude > portal.EffectiveAttractionRadius + .1f, "Core exits beyond attraction field");
                check(!a.HasPortalVisual && a.VisualRoot.localScale == visualScale && a.VisualRoot.parent == visualParent,
                    "Resting hierarchy and lifecycle size fully restored");
                check(captures == 0 && !a.Core.HasBeenCaptured && !b.Core.HasBeenCaptured, "No score awards or despawns during transit");
                a.SetSurfacePosition(portal.FaceToSurface(Vector2.zero, true));
                portal.EvaluateCapture(12.1f); portal.EvaluateCapture(100f);
                check(!portal.IsInTransit(a) && transfers == 2, "Time alone cannot rearm a Core inside mouth");
                a.SetSurfacePosition(portal.FaceToSurface(new Vector2(5f, 0f), true));
                portal.EvaluateCapture(101f);
                a.SetSurfacePosition(portal.FaceToSurface(new Vector2(.8f, 0f), true));
                portal.EvaluateCapture(102f);
                check(portal.IsInTransit(a), "Leaving field rearms reverse trip");
                portal.AdvanceTransit(portal.entrySeconds * .3f, 102.2f);
                a.Core.CompleteSpawnImmediately();
                check(!portal.IsInTransit(a) && !a.Core.IsInExternalTransit && !a.HasPortalVisual,
                    "Core lifecycle takeover synchronously cancels portal");
                check(a.VisualRoot.parent == visualParent && a.VisualRoot.localScale == visualScale && !a.Body.isKinematic,
                    "Lifecycle reset cannot inherit distorted size or frozen body");
                check(cancellations == 1, "Lifecycle cancellation reported exactly once");

                portal.CancelAll();
                a.SetSurfacePosition(portal.FaceToSurface(new Vector2(1f, 0f), false));
                portal.EvaluateCapture(110f);
                check(portal.IsInTransit(a), "Registration survives portal state reset");
                a.ConfigurePortal(null);
                check(!portal.IsInTransit(a) && !a.Core.IsInExternalTransit && !a.HasPortalVisual,
                    "Removing portal registration releases all ownership");
                a.ConfigurePortal(portal);
                portal.EvaluateCapture(111f);
                portal.enabled = false;
                portal.AdvanceTransit(0f, 111f);
                check(!portal.IsInTransit(a) && !a.Body.isKinematic && a.Body.detectCollisions && !a.HasPortalVisual,
                    "Portal disabled: live Core is never stranded");
                portal.enabled = true;
                portal.EvaluateCapture(112f);
                check(portal.IsInTransit(a), "Portal re-enable still knows live Core");
                portal.UnregisterCore(a);
                check(!a.Core.IsInExternalTransit && !a.HasPortalVisual, "Explicit Core removal cleans up");
                portal.UnregisterCore(b);
                check(captures == 0, "All repeated and interrupted sequences remain non-scoring");

                string report = "SINGULARITY Core transit: PASS " + checks +
                    " isolated checks (both-face capture, shared curves, shape, lease, lifecycle interruption, no scoring and re-entry).";
                Debug.Log(report);
                return report;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static SingularityAmplifierAdapter MakeCore(string name, Scene scene,
            SingularitySurface surface, SingularityBlackHolePortal portal)
        {
            var obj = new GameObject(name);
            SceneManager.MoveGameObjectToScene(obj, scene);
            obj.SetActive(false);
            var visual = new GameObject("Visual Root");
            visual.transform.SetParent(obj.transform, false);
            visual.transform.localScale = new Vector3(.9f, 1.1f, 1f);
            var body = obj.AddComponent<Rigidbody>();
            body.useGravity = false;
            obj.AddComponent<SphereCollider>().radius = .6f;
            var core = obj.AddComponent<AmplifierCoreGameplay>();
            core.UseSharedSettings = false;
            var serialized = new SerializedObject(core);
            serialized.FindProperty("playSpawnAnimationOnEnable").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            obj.SetActive(true);
            // Ordinary runtime Awake is not invoked in an Editor preview scene.
            // SendMessage also skips inactive GameObjects, so activate first:
            // otherwise the uncaptured lifecycle baseline is Vector3.zero.
            core.SendMessage("Awake");
            core.CompleteSpawnImmediately();
            if (obj.transform.localScale.sqrMagnitude < .01f || visual.transform.localScale.sqrMagnitude < .01f)
                throw new InvalidOperationException("Core validation fixture failed to initialize nonzero lifecycle scales.");
            var adapter = obj.AddComponent<SingularityAmplifierAdapter>();
            adapter.Configure(surface);
            adapter.ConfigurePortal(portal);
            return adapter;
        }
    }
}
#endif
