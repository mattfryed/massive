#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Isolated geometry, size, arrival and launch regression checks; never saves the user's open scene.</summary>
[InitializeOnLoad]
public static class CarrierHullValidation
{
    private const string Key = "MASSIVE.CarrierHull.Validation.";
    private static IEnumerator run;
    private static float resumeAt;
    private static int resumeFrame, passed;
    private static double deadline;
    private static readonly List<string> errors = new();
    public static string LastReport => SessionState.GetString(Key + "Report", "Not run");
    static CarrierHullValidation() { EditorApplication.playModeStateChanged += OnState; }

    [MenuItem("MASSIVE/Enemies/Carrier/Validate Hull and Body Scale")]
    public static void Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Wait for Edit Mode and compilation.");
        Scene original = SceneManager.GetActiveScene();
        string path = "Assets/CarrierHullValidation-" + Guid.NewGuid().ToString("N") + ".unity";
        Scene fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(fixture);
            var camera = new GameObject("Carrier hull validation camera"); camera.AddComponent<Camera>(); camera.AddComponent<AudioListener>();
            new GameObject("Carrier hull validation light").AddComponent<Light>().type = LightType.Directional;
            if (!EditorSceneManager.SaveScene(fixture, path)) throw new Exception("Cannot save fixture.");
        }
        finally { EditorSceneManager.CloseScene(fixture, true); SceneManager.SetActiveScene(original); }
        SessionState.SetString(Key + "Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + "Scene", path); SessionState.SetString(Key + "Report", "Running");
        SessionState.SetBool(Key + "Pending", true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.isPlaying = true;
    }
    private static void OnState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Pending", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetBool(Key + "Background", Application.runInBackground); Application.runInBackground = true;
            passed = 0; errors.Clear(); run = Checks(); resumeAt = 0f; resumeFrame = 0;
            deadline = EditorApplication.timeSinceStartup + 90d;
            Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log; run = null;
            Application.runInBackground = SessionState.GetBool(Key + "Background", Application.runInBackground);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Previous", ""));
            string path = SessionState.GetString(Key + "Scene", "");
            if (path.StartsWith("Assets/CarrierHullValidation-", StringComparison.Ordinal) && path.EndsWith(".unity", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(path);
            SessionState.SetBool(Key + "Pending", false);
        }
    }
    private static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish("FAILED: timed out"); return; }
        if (!EditorApplication.isPlaying || run == null || Time.unscaledTime < resumeAt || Time.frameCount < resumeFrame) return;
        try
        {
            if (run.MoveNext()) { resumeAt = Time.unscaledTime + (run.Current is float seconds ? seconds : .02f); resumeFrame = Time.frameCount + 2; return; }
            Finish(passed + " Carrier hull/scale checks passed (isolated Play Mode).");
        }
        catch (Exception ex) { Finish("FAILED after " + passed + " checks: " + ex); }
    }
    private static void Finish(string report)
    {
        SessionState.SetString(Key + "Report", report); Debug.Log("[Carrier hull validation] " + report);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log; run = null; EditorApplication.isPlaying = false;
    }
    private static void Check(bool valid, string description)
    { if (!valid) throw new Exception(description); passed++; }

    private static IEnumerator Checks()
    {
        CarrierHullGeometry.Create(out var vertices, out var faces);
        Check(vertices.Length == 96 && faces.Length == 50 && faces.Count(f => f.Length == 8) == 18 && faces.Count(f => f.Length == 6) == 8 && faces.Count(f => f.Length == 4) == 24, "50-face topology");
        var edges = new Dictionary<Vector2Int, int>(); bool convex = true;
        foreach (var f in faces)
        {
            Vector3 n = Vector3.Cross(vertices[f[1]] - vertices[f[0]], vertices[f[2]] - vertices[f[0]]).normalized;
            convex &= Vector3.Dot(n, vertices[f[0]]) > 0;
            convex &= vertices.All(p => Vector3.Dot(n, p - vertices[f[0]]) < .0001f);
            convex &= f.All(i => Mathf.Abs(Vector3.Dot(n, vertices[i] - vertices[f[0]])) < .0001f);
            for (int i = 0; i < f.Length; i++) { int a = f[i], b = f[(i + 1) % f.Length]; var edge = new Vector2Int(Mathf.Min(a, b), Mathf.Max(a, b)); edges.TryGetValue(edge, out int count); edges[edge] = count + 1; }
        }
        Check(convex && edges.Count == 144 && edges.Values.All(n => n == 2), "Closed, convex, outward-facing collision geometry with shared edges");
        var crowns = faces.Where(f => CarrierHullGeometry.IsCrown(vertices, f)).ToArray();
        Check(crowns.Length == 1 && crowns[0].Length == 6, "One upward hexagonal crown");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarrierPrototypeSetup.PrefabPath);
        var root = Object.Instantiate(prefab); var carrier = root.GetComponent<CarrierController>();
        var visual = root.GetComponent<CarrierVisuals>(); var enemy = root.GetComponent<EnemyBase>();
        carrier.sceneDirector = null; carrier.spawnWarningSeconds = .6f; carrier.initialLaunchDelay = 100f;
        carrier.launchInterval = .12f; carrier.cooldownSeconds = .3f; carrier.respawnInterval = .1f; carrier.maxActiveDrones = 6;
        carrier.enabled = false; carrier.enabled = true;
        visual.spinDegreesPerSecond = 0f;
        var dronePrefab = carrier.droneDefinition.prefab;
        var droneVisual = dronePrefab.GetComponent<DroneVisuals>();
        Check(visual.shell.sharedMesh.triangles.Length == 184 * 3, "Visible shell omits the crown's four fill triangles");
        Check(((MeshCollider)carrier.shellCollider).sharedMesh.vertexCount == 96, "Persistent collision mesh matches the new hull");

        foreach (float scale in new[] { .75f, 1f, 1.5f, 2.5f, 1f })
        {
            visual.bodyScale = scale; visual.ApplyBodyScale();
            bool aligned = true, embedded = true, unchanged = true;
            for (int i = 0; i < 6; i++)
            {
                var dock = carrier.docks[i]; Vector3 p = dock.localPosition, n = dock.localRotation * Vector3.forward;
                var f = faces.FirstOrDefault(face => face.Length == 8 && face.All(j => Mathf.Abs(Vector3.Dot(vertices[j] * scale - p, n)) < .0001f));
                aligned &= f != null && Mathf.Abs(p.y) < .0001f && Mathf.Abs(n.y) < .0001f;
                if (f != null) aligned &= Vector3.Distance(f.Aggregate(Vector3.zero, (sum, j) => sum + vertices[j] * scale) / f.Length, p) < .0001f;
                var display = dock.GetComponentInChildren<DroneVisuals>(true);
                unchanged &= display.transform.localScale == dronePrefab.transform.localScale && display.transform.localPosition == Vector3.zero;
                Vector3 tail = p - n * droneVisual.noseLength * droneVisual.tailLengthRatio * dronePrefab.transform.localScale.z;
                embedded &= faces.All(face => Vector3.Dot(Vector3.Cross(vertices[face[1]] - vertices[face[0]], vertices[face[2]] - vertices[face[0]]).normalized, tail - vertices[face[0]] * scale) < .0001f);
                aligned &= Mathf.Abs(Vector3.Angle(n, carrier.docks[(i + 1) % 6].forward) - 60f) < .01f;
            }
            Check(aligned, "Six centered, horizontal, 60-degree docks at scale " + scale);
            Check(embedded && unchanged, "Full-size Drones retain inset tails at scale " + scale);
            Check(visual.shell.transform.localScale == Vector3.one * scale && Mathf.Approximately(carrier.damageTrigger.radius, visual.damageRadiusAtUnitScale * scale), "Collision and damage trigger follow body scale " + scale);
            Check(carrier.definition.GetSpawnRadiusWorld(root.transform) >= visual.SpawnRadiusLocal - .0001f, "Director clearance covers scaled hull and docked noses");
            var outline = visual.CreateSpawnOutline();
            Check(outline.vertexCount == (144 + 6 * 12) * 4, "Warning has all hull edges and six unscaled Drone noses");
            var outlinePoints = outline.vertices;
            CarrierHullGeometry.GetDockPose(0, out var bay, out var rotation);
            Vector3 nose = bay * scale + rotation * Vector3.forward * droneVisual.noseLength * dronePrefab.transform.localScale.z;
            Check(outlinePoints.Any(v => Vector3.Distance(v, nose) < .0001f), "Warning nose follows its bay without scaling the Drone");
            Object.Destroy(outline);
        }
        yield return .1f;
        Check(carrier.SpawnWarning && !carrier.damageTrigger.enabled && !carrier.shellCollider.enabled, "Warning precedes collision");
        float warningAge = carrier.SpawnWarning.Age;
        var oldOutline = carrier.SpawnWarning.GetComponent<MeshFilter>().sharedMesh;
        visual.bodyScale = 1.5f; visual.ApplyBodyScale();
        Check(carrier.SpawnWarning.Age == warningAge && carrier.SpawnWarning.GetComponent<MeshFilter>().sharedMesh != oldOutline, "Live body-scale adjustment refreshes warning without restarting it");
        yield return .2f;
        enemy.Pause(true); float pausedAge = carrier.SpawnAge; yield return .15f;
        Check(carrier.SpawnAge == pausedAge, "Pause freezes arrival"); enemy.Pause(false);
        while (!carrier.IsSpawnReady) yield return .1f;
        Check(carrier.DockedCount == 6 && carrier.shellCollider.enabled && carrier.damageTrigger.enabled, "All six bays assemble before collision resumes");
        Check(Enumerable.Range(0, 50).All(i => visual.FacetProgress(i) > .999f), "All 50 faces finish tracing");
        Capture(root.transform.position, "carrier-50-face-body.png", 2f);
        visual.bodyScale = 1f; visual.ApplyBodyScale(); yield return .05f;
        Capture(root.transform.position, "carrier-50-face-body-scale-1.png", 1.5f);
        visual.bodyScale = 2f; visual.ApplyBodyScale(); yield return .05f;
        Capture(root.transform.position, "carrier-50-face-body-scale-2.png", 2.5f);
        visual.bodyScale = 1.5f; visual.ApplyBodyScale();

        var launched = new List<EnemyBase>(); var slots = new List<int>(); bool launchPose = true;
        carrier.DroneLaunched += (slot, drone) => { launched.Add(drone); slots.Add(slot); launchPose &= Vector3.Distance(drone.transform.position, carrier.docks[slot].position) < .0001f && drone.transform.localScale == dronePrefab.transform.localScale; };
        carrier.enabled = false; carrier.initialLaunchDelay = .1f; carrier.spawnWarningSeconds = 0f; carrier.enabled = true;
        while (carrier.TotalLaunched < 6) yield return .05f;
        Check(launchPose && slots.SequenceEqual(Enumerable.Range(0, 6)), "All six real Drones launch in order at the scaled bays with original size");
        enemy.Pause(true); yield return .6f;
        Check(launched.All(d => d && Vector3.Distance(d.transform.position, root.transform.position) > CarrierHullGeometry.Radius * 1.5f), "Every launched Drone clears the larger hull: " + string.Join("; ", launched.Select(DescribeLaunch)));
        enemy.Pause(false);
        while (carrier.CompletedCycles == 0) yield return .05f;
        Check(carrier.DockedCount == 6, "Six docked visuals rebuild at the scaled face centers");
        enemy.TakeDamage(100f, EnemyDamageSource.Projectile); yield return .3f;
        Check(enemy.IsDead && !carrier.shellCollider.enabled && !carrier.damageTrigger.enabled && carrier.DockedCount == 0, "Death removes docking visuals and collision");
        Check(Enumerable.Range(0, 50).All(i => visual.FaceDisplacement(i) > .1f), "All 50 panels participate in breakup");

        Object.Destroy(root); foreach (var drone in launched) if (drone) Object.Destroy(drone.gameObject);
        yield return .05f;
        foreach (float scale in new[] { .75f, 1f, 2.5f })
        {
            root = Object.Instantiate(prefab); carrier = root.GetComponent<CarrierController>();
            visual = root.GetComponent<CarrierVisuals>(); enemy = root.GetComponent<EnemyBase>();
            carrier.enabled = false;
            carrier.sceneDirector = null; carrier.spawnWarningSeconds = 0f; carrier.initialLaunchDelay = .1f;
            carrier.launchInterval = .1f; carrier.cooldownSeconds = 100f; carrier.maxActiveDrones = 6;
            visual.spawnSeconds = .05f; visual.spinDegreesPerSecond = 0f; visual.bodyScale = scale; visual.ApplyBodyScale();
            launched.Clear(); carrier.DroneLaunched += (slot, drone) => launched.Add(drone);
            carrier.enabled = true;
            while (carrier.TotalLaunched < 6) yield return .05f;
            enemy.Pause(true); yield return .6f;
            Check(launched.All(d => d && Vector3.Distance(d.transform.position, root.transform.position) > CarrierHullGeometry.Radius * scale), "All six bays clear at body scale " + scale + ": " + string.Join("; ", launched.Select(DescribeLaunch)));
            Check(launched.All(d => !d.GetComponent<EnemyObstacleAvoidance>().LaunchClearanceCollider), "Hull avoidance resumes after departure at body scale " + scale);
            Object.Destroy(root); foreach (var drone in launched) if (drone) Object.Destroy(drone.gameObject);
            yield return .05f;
        }
        Check(errors.Count == 0, "No runtime errors: " + string.Join("; ", errors));
    }

    private static string DescribeLaunch(EnemyBase drone)
    {
        if (!drone) return "missing";
        var controller = drone.GetComponent<DroneController>(); var body = drone.GetComponent<Rigidbody>();
        return drone.transform.position.ToString("F3") + " velocity=" + body.linearVelocity.ToString("F2") + " ready=" + controller.IsReady;
    }

    private static void Capture(Vector3 center, string name, float size)
    {
        string output = SessionState.GetString(Key + "CaptureDirectory", ""); if (string.IsNullOrEmpty(output)) return;
        var camera = Object.FindFirstObjectByType<Camera>(); camera.transform.position = center + new Vector3(2f, 5f, -3f); camera.transform.LookAt(center);
        camera.orthographic = true; camera.orthographicSize = size; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var rt = new RenderTexture(1000, 1000, 24) { antiAliasing = 4 }; var image = new Texture2D(1000, 1000, TextureFormat.RGB24, false); var old = RenderTexture.active;
        try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); }
        finally { camera.targetTexture = null; RenderTexture.active = old; Object.Destroy(image); rt.Release(); Object.Destroy(rt); }
    }
}
#endif
