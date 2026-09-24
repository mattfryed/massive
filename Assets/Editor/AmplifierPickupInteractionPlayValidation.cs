using System;
using System.Collections.Generic;
using Massive.PowerUps;
using Massive.Resonance;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Multiplier.Editor
{
    /// <summary>Isolated Play Mode fixtures use real generated walls and Physics.IgnoreCollision state.</summary>
    public static class AmplifierPickupInteractionPlayValidation
    {
        public static string Status { get; private set; } = "Not run";
        public static string Evidence { get; private set; } = "";
        private static GameObject root;
        private static Scene scene;
        private static ResonancePatternController pattern;
        private static ResonancePatternDefinition definition;
        private static ResonanceInteractionDriver driver;
        private static PowerUpPickup early, late;
        private static Collider[] earlyColliders, lateColliders;
        private static readonly List<Collider> walls = new List<Collider>();
        private static readonly List<string> checks = new List<string>();
        private static int stage;
        private static double began;
        private static float lastFixed;
        private static bool running;

        public static string RunBegin()
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resonance/Spawn Patterns/345 Hz Particle Encounter.prefab");
            if (template == null) throw new InvalidOperationException("Configured Resonance pattern prefab is missing.");
            return RunBegin(template.GetComponentInChildren<ResonancePatternController>(true));
        }

        public static string RunBegin(ResonancePatternController template)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            if (running) throw new InvalidOperationException("Pickup interaction validation is already running.");
            if (Time.timeScale <= 0f || UnityEditor.EditorApplication.isPaused)
                throw new InvalidOperationException("Unpause the simulation first; this validation does not change its clock.");
            if (template == null || template.definition == null)
                throw new ArgumentException("A configured pattern is required.");
            checks.Clear(); walls.Clear(); Evidence = ""; stage = 0;
            began = EditorApplication.timeSinceStartup;
            running = true;
            try
            {
                scene = SceneManager.CreateScene("Amplifier pickup interaction validation (runtime only)");
                root = new GameObject("Pickup exclusion fixtures");
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.position = new Vector3(20000f, 0f, 20000f);
                var clone = UnityEngine.Object.Instantiate(template.gameObject, root.transform, false);
                clone.name = "Configured pattern fixture";
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localScale = Vector3.one;
                foreach (var manifestation in clone.GetComponentsInChildren<ResonanceManifestation>(true)) manifestation.enabled = false;
                pattern = clone.GetComponent<ResonancePatternController>();
                definition = UnityEngine.Object.Instantiate(template.definition);
                definition.name = "Runtime-only hard wall fixture definition";
                // Keep the authored geometry, but exercise hard collision for every exposed arc.
                foreach (var arc in definition.arcs)
                    if (arc != null && arc.IsVisible) arc.behavior = ResonanceBehavior.HardWall;
                pattern.definition = definition;
                pattern.attractGrid = false; pattern.grid = null; pattern.arenaBounds = null;
                pattern.SetInteractionEnabled(false);
                early = MakePickup("Pickup present during formation", new Vector3(0f, 0f, 0f), out earlyColliders);
                root.SetActive(true);
                driver = pattern.GetComponentInChildren<ResonanceInteractionDriver>(true);
                foreach (var segment in pattern.InteractiveSegments)
                    foreach (var wall in segment.HardColliders) if (wall != null) walls.Add(wall);
                Check(driver != null && walls.Count > 0, "Authored pattern generated real hard colliders");
                Check(earlyColliders[1].attachedRigidbody != earlyColliders[2].attachedRigidbody,
                    "Fixture includes independently owned child rigidbodies");
                lastFixed = Time.fixedTime;
                Status = "Running pickup / Resonance physics exclusion checks";
                EditorApplication.update += Tick;
                return Status;
            }
            catch (Exception ex)
            {
                Finish("FAIL: " + ex.Message);
                return Status;
            }
        }

        private static PowerUpPickup MakePickup(string name, Vector3 localPosition, out Collider[] colliders)
        {
            var go = new GameObject(name);
            go.SetActive(false); go.transform.SetParent(root.transform, false); go.transform.localPosition = localPosition;
            var pickup = go.AddComponent<PowerUpPickup>();
            var settings = new SerializedObject(pickup);
            settings.FindProperty("neverDespawnFromTimeout").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            colliders = new Collider[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject item = go;
                if (i > 0)
                {
                    item = new GameObject("Independent body " + i);
                    item.transform.SetParent(go.transform, false);
                    item.transform.localPosition = Vector3.right * (i * 2f);
                }
                var body = item.AddComponent<Rigidbody>();
                body.useGravity = false; body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
                var sphere = item.AddComponent<SphereCollider>(); sphere.radius = .12f; colliders[i] = sphere;
            }
            go.SetActive(true);
            return pickup;
        }

        private static void Tick()
        {
            if (!running) return;
            try
            {
                if (!Application.isPlaying || root == null) { Finish("Interrupted"); return; }
                if (EditorApplication.timeSinceStartup - began > 10d) throw new TimeoutException("Physics validation exceeded 10 seconds at step " + stage);
                if (Time.fixedTime <= lastFixed) return;
                lastFixed = Time.fixedTime;
                switch (stage++)
                {
                    case 0:
                        Check(!driver.enabled && WallsEnabled(false), "Formation keeps walls and actor driver disabled");
                        pattern.SetInteractionEnabled(true);
                        // A pre-existing per-pair ignore must survive the driver's temporary ownership.
                        Physics.IgnoreCollision(earlyColliders[0], walls[0], true);
                        Check(Physics.GetIgnoreCollision(earlyColliders[0], walls[0]), "Prior ignore state installed before first driver step");
                        break;
                    case 1:
                        Check(WallsEnabled(true) && AllIgnored(earlyColliders), "Existing pickup excludes all descendant colliders when formation ends");
                        late = MakePickup("Pickup spawned after pattern", new Vector3(0f, 0f, 10f), out lateColliders);
                        break;
                    case 2:
                        Check(AllIgnored(lateColliders), "Late pickup excludes all descendant colliders before physics continues");
                        earlyColliders[1].enabled = false;
                        break;
                    case 3:
                        earlyColliders[1].enabled = true;
                        break;
                    case 4:
                        Check(AllIgnored(earlyColliders), "Re-enabled child collider regains every wall exclusion");
                        driver.enabled = false;
                        Check(Restored(earlyColliders, true) && Restored(lateColliders, false), "Driver disable restores false and pre-existing true pair values");
                        driver.enabled = true;
                        break;
                    case 5:
                        Check(AllIgnored(earlyColliders) && AllIgnored(lateColliders), "Driver re-enable reinstalls both pickup exclusions");
                        early.enabled = false;
                        break;
                    case 6:
                        Check(Restored(earlyColliders, true) && AllIgnored(lateColliders), "Pickup disable restores its own pairs without affecting another pickup");
                        early.enabled = true;
                        break;
                    case 7:
                        Check(AllIgnored(earlyColliders), "Pickup re-enable restores exclusions");
                        pattern.SetInteractionEnabled(false);
                        break;
                    case 8:
                        Check(!driver.enabled && WallsEnabled(false), "Dissolve gate disables walls and driver again");
                        pattern.SetInteractionEnabled(true);
                        break;
                    case 9:
                        Check(AllIgnored(earlyColliders) && AllIgnored(lateColliders), "Second formation gate replays collision exclusion");
                        CheckContactSkip(ResonanceBehavior.DampingMembrane);
                        CheckContactSkip(ResonanceBehavior.LensDeflector);
                        Finish("PASS: " + checks.Count + " pickup interaction checks");
                        break;
                }
            }
            catch (Exception ex) { Finish("FAIL: " + ex.Message); }
        }

        private static bool WallsEnabled(bool value)
        { foreach (var wall in walls) if (wall.enabled != value) return false; return true; }

        private static bool AllIgnored(Collider[] colliders)
        {
            foreach (var collider in colliders)
                foreach (var wall in walls)
                    if (!Physics.GetIgnoreCollision(collider, wall)) return false;
            return true;
        }

        private static bool Restored(Collider[] colliders, bool hasPriorIgnore)
        {
            for (int c = 0; c < colliders.Length; c++)
                for (int w = 0; w < walls.Count; w++)
                    if (Physics.GetIgnoreCollision(colliders[c], walls[w]) != (hasPriorIgnore && c == 0 && w == 0)) return false;
            return true;
        }

        private static void CheckContactSkip(ResonanceBehavior behavior)
        {
            var go = new GameObject(behavior + " contact fixture");
            go.transform.SetParent(root.transform, false);
            var segment = go.AddComponent<ResonanceSegment>();
            var arc = new ResonanceArc { behavior = behavior, endTaperFraction = 0f, dampingPerSecond = 8f, deflectionDegreesPerSecond = 180f };
            segment.Initialize(arc, new[] { Vector3.zero, Vector3.right }, ~0, false);
            Rigidbody pickupBody = earlyColliders[1].attachedRigidbody;
            Vector3 velocity = new Vector3(3f, 0f, 4f);
            pickupBody.linearVelocity = velocity;
            segment.Contact(earlyColliders[1]);
            Check((pickupBody.linearVelocity - velocity).sqrMagnitude < .000001f, behavior + " leaves child pickup velocity unchanged");

            var control = new GameObject("Unrelated body control");
            control.transform.SetParent(root.transform, false); control.transform.localPosition = Vector3.forward * 30f;
            var controlBody = control.AddComponent<Rigidbody>(); controlBody.useGravity = false;
            var controlCollider = control.AddComponent<SphereCollider>();
            controlBody.linearVelocity = velocity;
            segment.Contact(controlCollider);
            Check((controlBody.linearVelocity - velocity).sqrMagnitude > .000001f, behavior + " still affects an unrelated eligible body");
        }

        private static void Check(bool condition, string text)
        {
            if (!condition) throw new InvalidOperationException(text);
            checks.Add(text);
            Evidence = string.Join("\n", checks);
        }

        private static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            running = false; Status = result; Evidence = string.Join("\n", checks);
            if (root != null) { root.SetActive(false); Release(root); }
            if (definition != null) Release(definition);
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            root = null; definition = null; pattern = null; driver = null; early = null; late = null;
            earlyColliders = lateColliders = null;
            walls.Clear();
        }

        private static void Release(UnityEngine.Object item)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(item);
            else UnityEngine.Object.DestroyImmediate(item);
        }
    }
}
