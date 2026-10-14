#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real damage-event checks in a disposable scene; never saves the user's open scene.</summary>
[InitializeOnLoad]
public static class CarrierHitValidation
{
    const string Key = "MASSIVE.CarrierHitValidation.";
    static IEnumerator routine;
    static double deadline;
    static float resumeAt;
    static int resumeFrame;
    static readonly List<string> checks = new(), errors = new();
    public static string Report => SessionState.GetString(Key + "Report", "Not run");
    static CarrierHitValidation() { EditorApplication.playModeStateChanged += State; }

    [MenuItem("MASSIVE/Enemies/Carrier/Validate Hit Feedback")]
    static void MenuRun() => Run();

    public static void Run(string captureDirectory = "")
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run in Edit Mode after compilation.");
        var original = SceneManager.GetActiveScene();
        SessionState.SetString(Key + "Original", original.path);
        SessionState.SetBool(Key + "Dirty", original.isDirty);
        SessionState.SetString(Key + "Hash", File.Exists(original.path) ? Hash128.Compute(File.ReadAllText(original.path)).ToString() : "");
        SessionState.SetString(Key + "Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key + "Background", Application.runInBackground);
        SessionState.SetString(Key + "Capture", captureDirectory ?? "");
        string path = "Assets/CarrierHitValidation-" + Guid.NewGuid().ToString("N") + ".unity";
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(fixture);
            var camera = new GameObject("Carrier hit validation camera").AddComponent<Camera>();
            camera.gameObject.AddComponent<AudioListener>(); camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            new GameObject("Carrier hit validation light").AddComponent<Light>().type = LightType.Directional;
            if (!EditorSceneManager.SaveScene(fixture, path)) throw new Exception("Cannot save isolated fixture.");
        }
        finally { EditorSceneManager.CloseScene(fixture, true); SceneManager.SetActiveScene(original); }
        SessionState.SetString(Key + "Scene", path); SessionState.SetString(Key + "Report", "RUNNING");
        SessionState.SetBool(Key + "Pending", true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.isPlaying = true;
    }

    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Pending", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground = true; checks.Clear(); errors.Clear();
            deadline = EditorApplication.timeSinceStartup + 90; resumeAt = 0; resumeFrame = 0;
            routine = Checks(); Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log; routine = null;
            Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Previous", ""));
            string path = SessionState.GetString(Key + "Scene", "");
            if (path.StartsWith("Assets/CarrierHitValidation-", StringComparison.Ordinal) && path.EndsWith(".unity", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(path);
            var original = SceneManager.GetActiveScene();
            bool preserved = original.path == SessionState.GetString(Key + "Original", "") && original.isDirty == SessionState.GetBool(Key + "Dirty", false);
            string hash = SessionState.GetString(Key + "Hash", "");
            preserved &= string.IsNullOrEmpty(hash) || File.Exists(original.path) && Hash128.Compute(File.ReadAllText(original.path)).ToString() == hash;
            SessionState.SetString(Key + "Report", Report + (preserved ? "\nPASS original scene, dirty state and saved content preserved." : "\nFAILED original scene preservation check."));
            SessionState.SetBool(Key + "Pending", false);
        }
    }
    static void Log(string text, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text); }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(new TimeoutException("Carrier hit checks exceeded 90 seconds.")); return; }
        if (!EditorApplication.isPlaying || routine == null || Time.unscaledTime < resumeAt || Time.frameCount < resumeFrame) return;
        try
        {
            if (!routine.MoveNext()) { Finish(null); return; }
            resumeAt = Time.unscaledTime + (routine.Current is float delay ? delay : 0f); resumeFrame = Time.frameCount + 1;
        }
        catch (Exception exception) { Finish(exception); }
    }
    static void Finish(Exception exception)
    {
        string text = (exception == null && errors.Count == 0 ? "PASSED" : "FAILED") + " (" + checks.Count + " checks)\n" + string.Join("\n", checks);
        if (exception != null) text += "\n" + exception;
        if (errors.Count > 0) text += "\nConsole errors: " + string.Join("\n", errors);
        SessionState.SetString(Key + "Report", text);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log; routine = null;
        Application.runInBackground = SessionState.GetBool(Key + "Background", false);
        EditorApplication.isPlaying = false;
    }
    static void Check(bool valid, string description)
    { if (!valid) throw new Exception(description); checks.Add("PASS " + description); }

    sealed class Pose
    {
        public Transform target;
        public Vector3 position, scale;
        public Quaternion rotation;
        public Pose(Transform value) { target = value; position = value.localPosition; rotation = value.localRotation; scale = value.localScale; }
        public bool Unchanged => target && position.Equals(target.localPosition) && rotation.Equals(target.localRotation) && scale.Equals(target.localScale);
    }
    static float Difference(Vector3[] a, Vector3[] b)
    { if (a.Length != b.Length) return float.PositiveInfinity; float max = 0; for (int i = 0; i < a.Length; i++) max = Mathf.Max(max, Vector3.Distance(a[i], b[i])); return max; }
    static bool Rest(CarrierVisuals visual, Pose[] dockVisuals) => visual.HitReaction01 == 0f && visual.CoreHitScale == 1f && visual.ActiveHitSparkCount == 0 && dockVisuals.All(p => p.Unchanged);
    static float Recovery(CarrierVisuals visual) => Mathf.Max(visual.hitReactionSeconds, visual.hitSparkLifetime * 1.2f) + .1f;

    static IEnumerator Checks()
    {
        Check(SceneManager.GetActiveScene().path == SessionState.GetString(Key + "Scene", ""), "Only the disposable scene runs.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarrierPrototypeSetup.PrefabPath);
        foreach (float scale in new[] { .75f, 1f, 2.5f })
        {
            var root = Object.Instantiate(prefab);
            var enemy = root.GetComponent<EnemyBase>(); var carrier = root.GetComponent<CarrierController>(); var visual = root.GetComponent<CarrierVisuals>();
            carrier.sceneDirector = null; carrier.spawnWarningSeconds = 0f; carrier.initialLaunchDelay = 100f;
            carrier.enabled = false; carrier.enabled = true;
            enemy.Init(carrier.definition, null); enemy.HoldPosition = true; enemy.AttacksEnabled = false;
            visual.spawnSeconds = .05f; visual.spinDegreesPerSecond = 0f; visual.bodyScale = scale;
            while (!carrier.IsSpawnReady) yield return null;
            yield return null;
            Check(carrier.DockedCount == 6 && visual.HitReaction01 == 0f, "Six ready docks and neutral hit state at scale " + scale);
            var anchors = new[] { root.transform, visual.assembly, visual.shell.transform, carrier.damageTrigger.transform }.Concat(carrier.docks).Select(t => new Pose(t)).ToArray();
            var displays = carrier.docks.Select(d => new Pose(d.GetComponentInChildren<DroneVisuals>(true).transform)).ToArray();
            var collider = (MeshCollider)carrier.shellCollider; Mesh collisionMesh = collider.sharedMesh;
            var collisionVertices = collisionMesh.vertices; var restVertices = visual.shell.sharedMesh.vertices;
            var body = root.GetComponent<Rigidbody>(); Vector3 bodyPosition = body.position; Quaternion bodyRotation = body.rotation;
            Material shellMaterial = visual.shell.GetComponent<Renderer>().sharedMaterial, coreMaterial = visual.core.sharedMaterial;
            int damageEvents = 0; enemy.Damaged += context => damageEvents++;
            float health = enemy.HealthRemaining;
            if (scale == 1f) { Capture(root.transform.position, "rest", scale); yield return null; }
            enemy.TakeDamage(.25f, EnemyDamageSource.Sword);
            Check(enemy.HealthRemaining == health - .25f && damageEvents == 1 && !enemy.IsDead, "Real nonlethal damage event at scale " + scale);
            Check(visual.HitReaction01 > .99f && visual.CoreHitScale < 1f && visual.ActiveHitSparkCount == visual.hitSparkCount, "Hit starts contraction and one spark burst at scale " + scale);
            yield return .04f;
            Check(visual.HitReaction01 > 0f && Difference(restVertices, visual.shell.sharedMesh.vertices) > .001f && displays.Any(p => !p.Unchanged), "Rendered hull and docked Drone children recoil at scale " + scale);
            Check(anchors.All(p => p.Unchanged) && body.position.Equals(bodyPosition) && body.rotation.Equals(bodyRotation) && collider.sharedMesh == collisionMesh && Difference(collisionVertices, collisionMesh.vertices) == 0f,
                "Root, Rigidbody, collider mesh and six launch anchors remain unchanged at scale " + scale);
            Check(visual.shell.GetComponent<Renderer>().sharedMaterial == shellMaterial && visual.core.sharedMaterial == coreMaterial, "Authored shell/core materials remain shared and unchanged at scale " + scale);
            if (scale == 1f) Capture(root.transform.position, "hit", scale);
            yield return Recovery(visual);
            Check(Rest(visual, displays) && Difference(restVertices, visual.shell.sharedMesh.vertices) == 0f, "Exact render and dock visual recovery at scale " + scale);

            if (scale == 1f)
            {
                Capture(root.transform.position, "recovered", scale); yield return null;
                // Inactive attribution-only player: no Awake/OnEnable, registration,
                // input or physics. The shared null simulation scope accepts it.
                var attackerObject = new GameObject("Inactive hit attribution probe"); attackerObject.SetActive(false);
                var attacker = attackerObject.AddComponent<PlayerControllerScript>();
                attacker.transform.position = root.transform.position + Vector3.right * 3f;
                Check(enemy.SharesSimulationWith(attacker), "Directional probe belongs to the Carrier's simulation.");
                Vector3 dockCenter = Vector3.zero; foreach (var display in displays) dockCenter += display.target.position / displays.Length;
                enemy.TakeDamage(.1f, EnemyDamageSource.Sword, attacker);
                CarrierHullGeometry.Create(out var points, out var polygons);
                var edgeWidth = typeof(CarrierVisuals).GetMethod("HitEdgeWidth", BindingFlags.Instance | BindingFlags.NonPublic);
                float nearest = float.NegativeInfinity, farthest = float.PositiveInfinity, impactWidth = 0f, oppositeWidth = 0f;
                Vector3 towardAttacker = (attacker.transform.position - root.transform.position).normalized;
                foreach (var polygon in polygons)
                    for (int i = 0; i < polygon.Length; i++)
                    {
                        Vector3 a = points[polygon[i]], b = points[polygon[(i + 1) % polygon.Length]];
                        float facing = Vector3.Dot((visual.shell.transform.TransformPoint((a + b) * .5f) - root.transform.position).normalized, towardAttacker);
                        float width = (float)edgeWidth.Invoke(visual, new object[] { a, b });
                        if (facing > nearest) { nearest = facing; impactWidth = width; }
                        if (facing < farthest) { farthest = facing; oppositeWidth = width; }
                    }
                Check(impactWidth > visual.outlineWidth * 1.5f && Mathf.Approximately(oppositeWidth, visual.outlineWidth), "Credited-player hit thickens the impacted side while opposite edges keep their normal width.");
                yield return .04f;
                Vector3 recoiledCenter = Vector3.zero; foreach (var display in displays) recoiledCenter += display.target.position / displays.Length;
                Check(Vector3.Dot(recoiledCenter - dockCenter, towardAttacker) < -.001f, "Cosmetic body/docked Drone recoil travels away from the credited player's side.");
                EnemyBase launched = null; bool launchAtAnchor = false, launchedWhileHit = false;
                Action<int, EnemyBase> onLaunch = (slot, drone) =>
                {
                    launched = drone; launchAtAnchor = Vector3.Distance(drone.transform.position, carrier.docks[slot].position) < .0001f;
                    launchedWhileHit = visual.HitReaction01 > 0f;
                };
                carrier.DroneLaunched += onLaunch;
                enemy.AttacksEnabled = true;
                typeof(CarrierController).GetProperty("RemainingSeconds").SetValue(carrier, 0f);
                yield return null;
                enemy.AttacksEnabled = false; carrier.DroneLaunched -= onLaunch;
                Check(carrier.TotalLaunched == 1 && launched && launched.Definition == carrier.droneDefinition && launchAtAnchor && launchedWhileHit && visual.HitReaction01 > 0f,
                    "Normal Carrier update launches an actual Drone from its unchanged anchor during active recoil.");
                Check(anchors.All(p => p.Unchanged) && carrier.DockedCount == 5 && visual.TotalFuelBursts == 1, "Launch, fuel and dock bookkeeping continue while hit feedback remains cosmetic.");
                Object.Destroy(launched.gameObject); Object.Destroy(attackerObject);
                yield return Recovery(visual);
                Check(Rest(visual, displays), "Directional hit and concurrent launch leave no residual dock offsets.");
                for (int i = 0; i < 12; i++) enemy.TakeDamage(.01f, EnemyDamageSource.Projectile);
                Check(visual.ActiveHitSparkCount <= 18 && visual.ActiveHitSparkCount > 0 && visual.HitReaction01 <= 1f && visual.CoreHitScale >= 1f - visual.hitCoreContraction, "Rapid hits stay within spark and contraction bounds.");
                yield return .04f;
                Check(anchors.All(p => p.Unchanged), "Rapid hits do not accumulate physics or dock-anchor movement.");
                enemy.Pause(true);
                float pulse = visual.HitReaction01, coreScale = visual.CoreHitScale, hp = enemy.HealthRemaining; int sparks = visual.ActiveHitSparkCount, events = damageEvents;
                var pausedDisplay = displays.Select(p => new Pose(p.target)).ToArray(); var pausedVertices = visual.shell.sharedMesh.vertices;
                var pausedCore = new Pose(visual.core.transform);
                enemy.TakeDamage(.25f, EnemyDamageSource.Sword);
                yield return .2f;
                Check(enemy.HealthRemaining == hp && damageEvents == events && pulse == visual.HitReaction01 && coreScale == visual.CoreHitScale && sparks == visual.ActiveHitSparkCount && pausedDisplay.All(p => p.Unchanged) && pausedCore.Unchanged && Difference(pausedVertices, visual.shell.sharedMesh.vertices) == 0f,
                    "Pause freezes hit/core/sparks/poses and rejects additional damage.");
                enemy.Pause(false); yield return Recovery(visual);
                Check(Rest(visual, displays), "Resume completes the existing reaction cleanly.");
                enemy.TakeDamage(.1f, EnemyDamageSource.Sword); yield return .04f;
                visual.enabled = false;
                Check(Rest(visual, displays), "Disabling during recoil immediately restores dock visuals and clears effects.");
                visual.enabled = true; yield return null;
                enemy.TakeDamage(.1f, EnemyDamageSource.Sword);
                Check(visual.ActiveHitSparkCount == visual.hitSparkCount, "Re-enable subscribes once, producing one fresh spark burst.");
                yield return Recovery(visual);
                Check(Rest(visual, displays) && Difference(restVertices, visual.shell.sharedMesh.vertices) == 0f, "Re-enabled visual returns to its exact original hull and dock pose.");
                enemy.TakeDamage(.1f, EnemyDamageSource.Sword); yield return .04f;
                enemy.TakeDamage(100f, EnemyDamageSource.Projectile);
                Check(enemy.IsDead && carrier.DockedCount == 0 && visual.HitReaction01 == 0f && visual.ActiveHitSparkCount == 0 && visual.CoreHitScale == 1f && !carrier.shellCollider.enabled && !carrier.damageTrigger.enabled,
                    "Lethal damage supersedes recoil/sparks, clears docks and disables collision.");
                yield return .15f;
                Check(visual && visual.DeathProgress > 0f && visual.FaceDisplacement(0) > 0f, "Existing rigid-face death breakup still progresses.");
                Capture(root.transform.position, "death", scale);
                while (root) yield return null;
                Check(!root, "Carrier despawns after its normal death animation.");
            }
            else { Object.Destroy(root); yield return null; }
        }
        Check(errors.Count == 0, "No runtime Console errors.");
    }

    static void Capture(Vector3 center, string name, float scale)
    {
        string directory = SessionState.GetString(Key + "Capture", ""); if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        var camera = Object.FindFirstObjectByType<Camera>(); camera.transform.position = center + new Vector3(2f, 5f, -3f); camera.transform.LookAt(center);
        camera.orthographic = true; camera.orthographicSize = 1.8f * scale;
        var rt = new RenderTexture(900, 900, 24) { antiAliasing = 4 }; var image = new Texture2D(900, 900, TextureFormat.RGB24, false);
        var previous = RenderTexture.active; var target = camera.targetTexture;
        try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(directory, "carrier-hit-" + name + ".png"), image.EncodeToPNG()); }
        finally { camera.targetTexture = target; RenderTexture.active = previous; Object.Destroy(image); rt.Release(); Object.Destroy(rt); }
    }
}
#endif
