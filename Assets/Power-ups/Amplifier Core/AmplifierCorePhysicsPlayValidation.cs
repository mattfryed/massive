#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Massive.Multiplier
{
    /// <summary>Temporary, distant Play Mode fixture. Does not edit the source prefab or scene.</summary>
    public sealed class AmplifierCorePhysicsPlayValidation : MonoBehaviour
    {
        public static string Status { get; private set; } = "Not started";
        public static string Evidence { get; private set; } = "";
        public static bool Finished { get; private set; } = true;

        private static AmplifierCorePhysicsPlayValidation current;
        private GameObject fixture;
        private AmplifierCoreGameplay core;
        private IEnumerator steps;
        private float previousTimeScale;
        private float deadline;
        private int passed, captures;
        private bool cleanedUp;

        public static string Begin()
        {
            if (!Application.isPlaying) return "Enter Play Mode first.";
            if (current != null) return "Amplifier Core physics validation is already running.";
            Status = "Running Amplifier Core low-speed contact and lifecycle checks";
            Evidence = ""; Finished = false;
            var go = new GameObject("Amplifier Core physics validation") { hideFlags = HideFlags.HideAndDontSave };
            current = go.AddComponent<AmplifierCorePhysicsPlayValidation>();
            current.previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            current.deadline = Time.realtimeSinceStartup + 10f;
            current.steps = current.RunChecks();
            return Status;
        }

        private void Update()
        {
            if (steps == null || cleanedUp) return;
            try
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Core fixture exceeded its ten-second deadline.");
                if (steps.MoveNext()) return;
                Status = passed + " Amplifier Core Play Mode checks passed.";
                Finish();
            }
            catch (Exception exception)
            {
                Status = "FAILED after " + passed + " checks: " + exception.Message;
                Finish();
            }
        }

        private IEnumerator RunChecks()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resonance/Spawn Patterns/Amplifier Core - Spawn Cycle.prefab");
            Require(prefab != null, "spawn-cycle Core prefab exists");
            fixture = new GameObject("Amplifier physics fixture — remote") { hideFlags = HideFlags.HideAndDontSave };
            fixture.transform.position = new Vector3(100f, 0f, 100f);
            fixture.SetActive(false);
            core = Instantiate(prefab, fixture.transform).GetComponent<AmplifierCoreGameplay>();
            core.gameObject.SetActive(true);
            core.transform.localPosition = Vector3.zero;
            core.transform.localRotation = Quaternion.identity;
            Set(core, "playSpawnAnimationOnEnable", false);
            Set(core, "linearDrag", 0f);
            Set(core, "angularDrag", 0f);
            Set(core, "maximumPlanarSpeed", 0f);
            Set(core, "wallRestitution", .85f);
            Set(core, "mass", 3f);
            core.SetExternalRespawnManaged(true);
            var interactor = core.GetComponent<GridInteractor>();
            if (interactor != null) interactor.enabled = false;
            fixture.SetActive(true); // Awake captures the remote position before any restore operation.
            core.CompleteSpawnImmediately();
            core.Captured += OnCaptured;
            var body = core.Body;
            var shape = core.GetComponent<SphereCollider>();
            Physics.SyncTransforms();
            Require(body != null && shape != null && shape.enabled && !body.isKinematic, "spawned Core has a live physical collider");
            float radius = shape.bounds.extents.x;
            Vector3 originalScale = core.transform.localScale;
            float originalDial = core.CoreScale;
            Vector3 home = core.transform.position;
            var wall = new GameObject("Static wall");
            wall.transform.SetParent(fixture.transform, false);
            wall.transform.position = home + new Vector3(radius + .1f + .025f, 0f, 0f);
            wall.AddComponent<BoxCollider>().size = new Vector3(.2f, 4f, 5f);
            Physics.SyncTransforms();

            Vector3 incoming = new Vector3(.2f, 0f, .12f);
            body.linearVelocity = incoming;
            float waitUntil = Time.realtimeSinceStartup + 3f;
            while (body.linearVelocity.x >= 0f && Time.realtimeSinceStartup < waitUntil) yield return null;
            Vector3 outgoing = body.linearVelocity;
            Evidence = "Low-speed glancing contact: " + incoming.ToString("F3") + " -> " + outgoing.ToString("F3") + ". ";
            Require(outgoing.x < -.1f, "slow wall contact rebounds instead of sticking");
            Require(Mathf.Abs(outgoing.z - incoming.z) < .02f, "glancing hit preserves the tangent");
            Require(outgoing.magnitude <= incoming.magnitude + .01f, "contact never adds energy");
            Require(Mathf.Abs(outgoing.x + incoming.x * .85f) < .03f, "wall bounce dial controls restitution");

            wall.SetActive(false);
            Set(core, "coreScale", originalDial * 2f);
            Set(core, "mass", 5f);
            Set(core, "linearDrag", .7f);
            Set(core, "angularDrag", .9f);
            core.ApplyTuning();
            Physics.SyncTransforms();
            Require(Vector3.Distance(core.transform.localScale, originalScale * 2f) < .0001f,
                "live size dial doubles authored dimensions");
            Require(Mathf.Abs(shape.bounds.extents.x - radius * 2f) < .001f, "physical radius follows live size");
            Require(Mathf.Abs(body.mass - 5f) < .001f && Mathf.Abs(body.linearDamping - .7f) < .001f &&
                Mathf.Abs(body.angularDamping - .9f) < .001f, "mass and both drag dials reach the live body");
            core.ApplyTuning(); core.CompleteSpawnImmediately(); core.CompleteSpawnImmediately();
            Require(Vector3.Distance(core.transform.localScale, originalScale * 2f) < .0001f,
                "reapplication and spawn reset do not compound size");
            Require(Vector3.Distance(core.transform.position, home) < .0001f && shape.enabled && !body.isKinematic,
                "spawn reset restores the original position and physics");

            Set(core, "linearDrag", 0f); Set(core, "maximumPlanarSpeed", .15f); core.ApplyTuning();
            body.linearVelocity = new Vector3(3f, 0f, 4f);
            float fixedBefore = Time.fixedTime;
            while (Time.fixedTime <= fixedBefore) yield return null;
            Require(body.linearVelocity.magnitude <= .151f, "maximum speed caps planar travel");
            core.CompleteSpawnImmediately();
            var visualRoot = core.transform.Find("Visual Root");
            float fullVisualScale = visualRoot.localScale.magnitude;
            Vector3 timeoutOrigin = core.transform.position;
            Require(core.BeginTimeoutDespawn(), "timeout begins once");
            Require(!core.BeginTimeoutDespawn(), "duplicate timeout is ignored");
            Require(core.IsCaptured && core.IsDespawning && !core.HasBeenCaptured && body.isKinematic && !shape.enabled,
                "timeout immediately makes the Core unavailable without a capture");
            bool observedShrink = visualRoot.localScale.magnitude < fullVisualScale * .99f;
            waitUntil = Time.realtimeSinceStartup + 2f;
            while (core.gameObject.activeInHierarchy && Time.realtimeSinceStartup < waitUntil)
            {
                observedShrink |= visualRoot.localScale.magnitude < fullVisualScale * .99f;
                yield return null;
            }
            Require(observedShrink && !core.gameObject.activeSelf, "timeout plays its shrink then disables the Core");
            Require(Vector3.Distance(core.transform.position, timeoutOrigin) < .0001f, "timeout remains in place");
            Require(captures == 0 && !core.HasBeenCaptured, "timeout never emits a capture event");
            Evidence += "Scaled physics and repeat spawn reset passed; timeout shrank in place with zero capture events.";
        }

        private void OnCaptured(AmplifierCoreGameplay item) { captures++; }

        private void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            passed++;
        }

        private static void Set<T>(AmplifierCoreGameplay target, string name, T value)
        {
            var field = typeof(AmplifierCoreGameplay).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(name);
            field.SetValue(target, value);
        }

        private void Finish()
        {
            Cleanup();
            Destroy(gameObject);
        }

        private void Cleanup()
        {
            if (cleanedUp) return;
            cleanedUp = true;
            if (core != null) core.Captured -= OnCaptured;
            if (fixture != null) Destroy(fixture);
            Time.timeScale = previousTimeScale;
            if (current == this) current = null;
            Finished = true;
        }

        private void OnDisable() { Cleanup(); }
        private void OnDestroy() { Cleanup(); }
    }
}
#endif
