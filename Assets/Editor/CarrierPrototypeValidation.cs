#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real Play Mode checks on the Carrier placed in Player Actions. Runtime mutations are discarded on exit.</summary>
[InitializeOnLoad]
public static class CarrierPrototypeValidation
{
    private const string Key = "MASSIVE.CarrierValidation";
    public const string Output = "Library/CarrierValidation";
    private static IEnumerator routine;
    private static readonly List<string> results = new();
    private static readonly List<float> launches = new(), rebuilds = new();
    private static readonly List<int> launchOrder = new(), rebuildOrder = new();
    private static readonly List<EnemyBase> drones = new();
    private static readonly List<string> errors = new();
    private static double deadline;
    private static CarrierController carrier;
    private static EnemyDirector director;
    private static EnemyBase enemy;
    private static int frame;
    static CarrierPrototypeValidation() { EditorApplication.playModeStateChanged += OnState; }

    [MenuItem("MASSIVE/Enemies/Carrier/Validate In Player Actions")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != CarrierPrototypeSetup.ScenePath)
            throw new InvalidOperationException("Run from Player Actions in Edit Mode.");
        if (!Object.FindFirstObjectByType<CarrierController>()) throw new InvalidOperationException("Place a Carrier first.");
        Directory.CreateDirectory(Output);
        SessionState.SetBool(Key, true);
        File.WriteAllText(Output + "/report.txt", "RUNNING — awaiting Play Mode\n");
        EditorApplication.isPlaying = true;
    }
    private static void OnState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            results.Clear(); launches.Clear(); rebuilds.Clear(); launchOrder.Clear(); rebuildOrder.Clear(); drones.Clear(); errors.Clear();
            Application.logMessageReceived += Log;
            deadline = EditorApplication.timeSinceStartup + 100; frame = 0;
            routine = Checks(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            File.AppendAllText(Output + "/report.txt", "Returned to Edit Mode; runtime test mutations discarded.\n");
        }
    }
    private static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        results.Add("PASS " + message);
        File.WriteAllText(Output + "/report.txt", "RUNNING\n" + string.Join("\n", results));
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || Time.frameCount <= frame) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Carrier validation timed out.");
            if (!routine.MoveNext()) Finish(true, null);
        }
        catch (Exception e) { Finish(false, e); }
    }
    private static void Finish(bool passed, Exception exception)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log; routine = null;
        File.WriteAllText(Output + "/report.txt", (passed ? "PASSED" : "FAILED") + "\n" + string.Join("\n", results) + "\n" + exception + "\n" + string.Join("\n", errors));
        Debug.Log("[Carrier validation] " + (passed ? "PASSED" : "FAILED: " + exception));
        EditorApplication.isPlaying = false;
    }
    private static IEnumerator Checks()
    {
        carrier = Object.FindFirstObjectByType<CarrierController>(); enemy = carrier.GetComponent<EnemyBase>(); director = carrier.sceneDirector;
        var visuals = carrier.GetComponent<CarrierVisuals>(); var hitbox = carrier.damageTrigger;
        Check(carrier.docks.Length == 6 && carrier.DockedCount == 0 && !hitbox.enabled && carrier.shellCollider && !carrier.shellCollider.enabled,
            "Six bays and both body colliders remain inactive before arrival");
        Check(carrier.GetComponentsInChildren<EnemyBase>(true).Length == 1 && carrier.GetComponentsInChildren<Collider>(true).Length == 2 &&
            carrier.docks.All(d => d.GetComponentsInChildren<Collider>(true).Length == 0),
            "Docked visuals have no enemy registration, collision or scoring components");
        Check(carrier.GetComponentsInChildren<MonoBehaviour>(true).All(x => x), "No missing prefab scripts");
        CheckGeometry();
        carrier.DroneLaunched += (slot, drone) => { launches.Add(Time.time); launchOrder.Add(slot); drones.Add(drone); };
        carrier.DroneRebuilt += slot => { rebuilds.Add(Time.time); rebuildOrder.Add(slot); };
        yield return null;
        Check(enemy.Director == director && director.AliveCount == 1, "Scene Carrier registered with shared EnemyDirector");
        Check(enemy.Definition == carrier.definition && enemy.HealthRemaining == 6f, "Shared definition initializes Carrier health");
        float before = carrier.RemainingSeconds;
        if (enemy.IsPaused)
        {
            float until = Time.time + .3f; while (Time.time < until) yield return null;
            Check(carrier.TotalLaunched == 0 && Mathf.Approximately(before, carrier.RemainingSeconds), "Match startup gate freezes launch clock");
        }
        // The real roster must remain active until startup validation and countdown finish.
        while (enemy.IsPaused) yield return null;
        foreach (var player in PlayerControllerScript.ActivePlayers.ToArray()) if (player) player.gameObject.SetActive(false);
        while (carrier.SpawnAge < .8f) yield return null;
        Check(carrier.SpawnWarning && carrier.SpawnWarning.Opacity > .3f && carrier.DockedCount == 0 && !hitbox.enabled && carrier.TotalLaunched == 0,
            "Shared red spawn indicator precedes the hidden, non-interactive Carrier");
        Capture("carrier-warning.png", carrier.transform.position, 2f);
        float warningAge = carrier.SpawnWarning.Age, spawnAge = carrier.SpawnAge, warningSpin = visuals.SpinAngle;
        director.enabled = false;
        float warningPause = Time.time + .3f; while (Time.time < warningPause) yield return null;
        Check(Mathf.Approximately(warningAge, carrier.SpawnWarning.Age) && Mathf.Approximately(spawnAge, carrier.SpawnAge) && Mathf.Approximately(warningSpin, visuals.SpinAngle),
            "Match pause freezes warning, arrival clock and rotation together");
        director.enabled = true;
        while (carrier.RevealProgress < .45f) yield return null;
        Check(carrier.SpawnWarning && carrier.SpawnWarning.IsCompleting && carrier.SpawnAge >= carrier.spawnWarningSeconds && !hitbox.enabled,
            "Three-second warning transitions into face assembly before collision is enabled");
        var progress = Enumerable.Range(0, CarrierHullGeometry.FaceCount).Select(visuals.FacetProgress).ToArray();
        Check(progress.Any(p => p > .01f && p < .99f) && progress.Max() - progress.Min() > .1f,
            "Face outlines trace with varied timing during assembly");
        Capture("carrier-forming.png", carrier.transform.position, 1.6f);
        var docked = carrier.docks[0].GetComponentInChildren<DroneVisuals>(); float dockProgress = docked.FacetProgress(0);
        director.enabled = false;
        float formPause = Time.time + .2f; while (Time.time < formPause) yield return null;
        Check(Enumerable.Range(0, CarrierHullGeometry.FaceCount).All(i => Mathf.Approximately(progress[i], visuals.FacetProgress(i))) && Mathf.Approximately(dockProgress, docked.FacetProgress(0)),
            "Carrier and docked Drone assembly freeze together when paused");
        director.enabled = true;
        while (!carrier.IsSpawnReady) yield return null;
        yield return null;
        Check(carrier.DockedCount == 6 && hitbox.enabled && Enumerable.Range(0, CarrierHullGeometry.FaceCount).All(i => visuals.FacetProgress(i) > .999f) && carrier.TotalLaunched == 0,
            "All six Drones and hull faces finish assembly before launching");
        Check(visuals.assembly && visuals.assembly.localEulerAngles.y > 10f && carrier.docks.All(d => d.parent == visuals.assembly) &&
            Mathf.Abs(carrier.transform.eulerAngles.y) < .01f, "Subtle Y spin carries the hull and docks without rotating the physics root");
        var hull = carrier.shellCollider as MeshCollider;
        Check(hull && hull.enabled && hull.convex && !hull.isTrigger && hull.transform == visuals.shell.transform && hull.sharedMesh.vertexCount == 96,
            "Solid convex collider uses the exact hull vertices and follows the rotating shell");
        Physics.SyncTransforms();
        bool fitted = true;
        foreach (var dock in carrier.docks)
        {
            fitted &= hull.Raycast(new Ray(dock.position + dock.forward * .5f, -dock.forward), out var faceHit, 1f) &&
                Vector3.Distance(faceHit.point, dock.position) < .01f;
        }
        Check(fitted, "Physics surfaces coincide with all six rotated docking faces");
        Capture("carrier-loaded.png", carrier.transform.position, 1.6f);
        Capture("carrier-oblique.png", carrier.transform.position, 1.6f, new Vector3(3f, 3f, -4f));
        while (launches.Count == 0) yield return null;
        var first = drones[0]; var start = first.transform.position;
        float fuelUntil = Time.time + .16f; while (Time.time < fuelUntil) yield return null;
        Vector3 fuelSource = visuals.assembly.TransformPoint(visuals.coreCenter), fuelLine = first.transform.position - fuelSource;
        Check(visuals.TotalFuelBursts == 1 && visuals.ActiveFuelCount == 3 && Enumerable.Range(0, 3).All(i =>
            visuals.FuelPosition(i).y <= fuelSource.y + .001f && visuals.FuelPosition(i).y >= first.transform.position.y - .001f &&
            Vector3.Cross(visuals.FuelPosition(i) - fuelSource, fuelLine).magnitude / fuelLine.magnitude < .025f),
            "Three fuel metaballs follow the straight interior core-to-Drone line with no upward arc");
        Vector3 firstFuel = visuals.FuelPosition(0);
        Capture("carrier-fueling.png", carrier.transform.position, 2.2f, null, first.transform);
        float ejectUntil = Time.time + .25f; while (Time.time < ejectUntil) yield return null;
        Check(Vector3.Distance(start, first.transform.position) > .3f, "Launched Drone physically leaves its docking bay");
        Check(first.GetComponent<DroneController>().enabled && first.Definition == carrier.droneDefinition && first.Director == director,
            "Launched Drone uses canonical AI, definition and Director ownership");
        Check(first.GetComponent<EnemyObstacleAvoidance>().LaunchClearanceCollider == null && first.GetComponent<EnemyObstacleAvoidance>().ShouldAvoid(hull),
            "Launched Drone restores hull avoidance as soon as its tail clears");
        while (launches.Count < 6) yield return null;
        Check(launchOrder.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5 }), "Six launches occur in clockwise slot order");
        Check(Gaps(launches, .5f), "Launch gaps are 0.5 seconds (one-frame tolerance)");
        Check(carrier.DockedCount == 0 && carrier.Phase == CarrierController.CyclePhase.Cooldown, "Empty Carrier enters cooldown after sixth launch");
        float lastFuelUntil = Time.time + .08f; while (Time.time < lastFuelUntil) yield return null;
        Check(visuals.TotalFuelBursts == 6 && visuals.ActiveFuelCount > 0 && Vector3.Distance(firstFuel, visuals.FuelPosition(0)) > .05f,
            "Fuel travels toward departing Drones and repeats once per successful launch");
        float pauseStart = Time.time; float remaining = carrier.RemainingSeconds;
        float spin = visuals.SpinAngle; Vector3 packet = visuals.FuelPosition(15); int packets = visuals.ActiveFuelCount;
        director.enabled = false;
        var pausedPositions = drones.Select(d => d.transform.position).ToArray();
        float pauseUntil = Time.time + .6f; while (Time.time < pauseUntil) yield return null;
        float pausedDuration = Time.time - pauseStart;
        Check(Mathf.Approximately(remaining, carrier.RemainingSeconds) && drones.All(d => d.IsPaused), "Director pause freezes Carrier timer and all launched Drones");
        Check(drones.Select((d, i) => Vector3.Distance(d.transform.position, pausedPositions[i])).All(d => d < .01f), "Paused Drones remain stationary");
        Check(Mathf.Approximately(spin, visuals.SpinAngle) && visuals.ActiveFuelCount == packets && Vector3.Distance(packet, visuals.FuelPosition(15)) < .0001f,
            "Carrier rotation and in-flight fuel freeze with Director pause");
        director.enabled = true;
        while (rebuilds.Count < 6) yield return null;
        Check(Mathf.Abs(rebuilds[0] - launches[5] - pausedDuration - 5f) < .12f, "First respawn follows five active seconds of cooldown");
        Check(rebuildOrder.SequenceEqual(launchOrder), "Drones rebuild in the same order as launch");
        Check(Gaps(rebuilds, .5f), "Respawn gaps are 0.5 seconds (one-frame tolerance)");
        Check(carrier.DockedCount == 6 && carrier.CompletedCycles == 1, "All six bays refill before the next volley");
        Check(visuals.ActiveFuelCount == 0 && !carrier.SpawnWarning, "Spent fuel and completed spawn indicator clean up");
        while (launches.Count < 12) yield return null;
        Check(launchOrder.Skip(6).SequenceEqual(new[] { 0, 1, 2, 3, 4, 5 }), "A second complete launch cycle repeats in the same order");
        Capture("carrier-empty.png", carrier.transform.position, 1.6f);
        var runtimeProfile = Object.Instantiate(director.spawnProfile); director.spawnProfile = runtimeProfile;
        runtimeProfile.maxAliveTotal = director.AliveCount;
        while (carrier.CompletedCycles < 2) yield return null;
        float capUntil = Time.time + .8f; while (Time.time < capUntil) yield return null;
        Check(launches.Count == 12 && carrier.DockedCount == 6 && carrier.NextSlot == 0, "Shared Director cap retains the next docked Drone without skipping slots");
        Check(visuals.TotalFuelBursts == 12 && visuals.ActiveFuelCount == 0, "Blocked launches do not emit phantom fuel bursts");
        // Force six real trigger-bodied Drones inward at high speed; the normal swept guard must stop penetration.
        var probes = new DroneController[6]; var closest = Enumerable.Repeat(float.PositiveInfinity, 6).ToArray();
        float worstPenetration = 0f;
        for (int i = 0; i < probes.Length; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward;
            var probe = Object.Instantiate(carrier.droneDefinition.prefab, carrier.transform.position + direction * 2f, Quaternion.LookRotation(-direction));
            probe.GetComponent<EnemyBase>().Init(carrier.droneDefinition, null);
            probes[i] = probe.GetComponent<DroneController>(); probes[i].Launch(-direction, 8f, 2f);
        }
        Physics.SyncTransforms();
        float incomingUntil = Time.time + 1f;
        while (Time.time < incomingUntil)
        {
            yield return null;
            for (int i = 0; i < probes.Length; i++)
            {
                var probe = probes[i]; var collider = probe.GetComponent<CapsuleCollider>(); var body = probe.GetComponent<Rigidbody>();
                closest[i] = Mathf.Min(closest[i], Vector3.Distance(body.position, carrier.transform.position));
                if (Physics.ComputePenetration(collider, body.position, body.rotation, hull, hull.transform.position, hull.transform.rotation, out _, out float depth))
                    worstPenetration = Mathf.Max(worstPenetration, depth);
            }
        }
        Check(closest.All(d => d > .65f && d < 1.4f) && worstPenetration < .04f,
            "Six incoming Drones at 8 units/second are blocked by the spinning shell without crossing it");
        foreach (var probe in probes) Object.Destroy(probe.gameObject);
        yield return null; yield return null;
        drones[0].Kill(EnemyDamageSource.Environmental);
        while (launches.Count < 13) yield return null;
        Check(launchOrder[12] == 0, "Destroying a Drone releases capacity and resumes the blocked bay");
        enemy.Pause(true); float hp = enemy.HealthRemaining; enemy.TakeDamage(1f, EnemyDamageSource.Sword);
        Check(enemy.HealthRemaining == hp, "Paused Carrier rejects damage through shared EnemyBase");
        enemy.Pause(false); enemy.TakeDamage(1f, EnemyDamageSource.Sword);
        Check(enemy.HealthRemaining == hp - 1f, "Carrier accepts shared sword damage");
        float beforeContact = enemy.HealthRemaining;
        var sword = new GameObject("Carrier validation sword contact"); sword.SetActive(false);
        sword.transform.position = carrier.transform.position;
        var swordCollider = sword.AddComponent<SphereCollider>(); swordCollider.radius = 1.1f;
        sword.AddComponent<PlayerMelee>(); sword.SetActive(true); swordCollider.enabled = true;
        float contactUntil = Time.time + .12f; while (Time.time < contactUntil) yield return null;
        swordCollider.enabled = false; Object.Destroy(sword);
        Check(Mathf.Approximately(enemy.HealthRemaining, beforeContact - carrier.definition.damageTakenPerSwordHit),
            $"One physical sword overlap damages the Carrier once despite its solid shell and damage trigger ({beforeContact} to {enemy.HealthRemaining} health)");
        int deaths = 0; enemy.Defeated += _ => deaths++;
        enemy.TakeDamage(100f, EnemyDamageSource.Projectile); enemy.TakeDamage(100f, EnemyDamageSource.Projectile);
        Check(deaths == 1 && enemy.IsDead && carrier.DockedCount == 0, "Carrier death is resolved once and clears docked Drones immediately");
        Check(!hitbox.enabled && !hull.enabled && visuals.ActiveFuelCount == 0, "Death disables damage trigger and solid shell before visual breakup");
        float deathUntil = Time.time + .25f; while (Time.time < deathUntil) yield return null;
        Check(carrier && visuals.DeathProgress > .2f && Enumerable.Range(0, CarrierHullGeometry.FaceCount).All(i => visuals.FaceDisplacement(i) > .3f),
            "Death retains the body while every face explodes outward independently");
        Capture("carrier-shattering.png", carrier.transform.position, 2.7f);
        while (carrier) yield return null;
        Check(!carrier && drones.Skip(1).Any(d => d && !d.IsDead), "Carrier despawns while already released Drones remain independent");
        Check(director.AliveCount == drones.Count(d => d && !d.IsDead), "Director counts release the destroyed Carrier and Drone");
        var reuse = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CarrierPrototypeSetup.PrefabPath)).GetComponent<CarrierController>();
        float reuseUntil = Time.time + .2f; while (Time.time < reuseUntil) yield return null;
        var abandonedWarning = reuse.SpawnWarning; reuse.gameObject.SetActive(false);
        yield return null; yield return null;
        Check(abandonedWarning == null && Object.FindObjectsByType<EnemySpawnTelegraph>(FindObjectsSortMode.None).Length == 0,
            "Disabling a Carrier during its warning leaves no indicator behind");
        reuse.gameObject.SetActive(true); yield return null; yield return null;
        Check(reuse.SpawnAge < .2f && reuse.DockedCount == 0 && !reuse.IsSpawnReady && reuse.GetComponent<CarrierVisuals>().TotalFuelBursts == 0,
            "Re-enabling resets the arrival sequence, rotation and fuel state");
        reuse.GetComponent<EnemyBase>().TakeDamage(100f, EnemyDamageSource.Projectile);
        float cancelUntil = Time.time + 1f; while (Time.time < cancelUntil) yield return null;
        Check(!reuse && Object.FindObjectsByType<EnemySpawnTelegraph>(FindObjectsSortMode.None).Length == 0,
            "Death during the warning cancels the sequence and cleans up");
        Check(errors.Count == 0, "No runtime Console errors during Carrier checks");
        Object.Destroy(runtimeProfile);
    }
    private static bool Gaps(List<float> times, float expected)
    { return times.Skip(1).Select((t, i) => t - times[i]).All(gap => gap >= expected - .02f && gap <= expected + .12f); }

    private static void CheckGeometry()
    {
        CarrierHullGeometry.Create(out var vertices, out var faces);
        Check(vertices.Length == 96 && faces.Count(f => f.Length == 4) == 24 && faces.Count(f => f.Length == 6) == 8 && faces.Count(f => f.Length == 8) == 18,
            "Truncated hull has 96 vertices, 24 quadrilaterals, eight hexagons and 18 octagons");
        var edges = new Dictionary<Vector2Int, int>();
        var lengths = new List<float>();
        bool regular = true;
        foreach (var face in faces)
        {
            var normal = Vector3.Cross(vertices[face[1]] - vertices[face[0]], vertices[face[2]] - vertices[face[0]]).normalized;
            regular &= Vector3.Dot(normal, vertices[face[0]]) > 0f;
            foreach (var vertex in vertices) regular &= Vector3.Dot(normal, vertex - vertices[face[0]]) < .0001f;
            for (int i = 0; i < face.Length; i++)
            {
                int a = face[i], b = face[(i + 1) % face.Length], c = face[(i + 2) % face.Length];
                var edge = new Vector2Int(Mathf.Min(a, b), Mathf.Max(a, b));
                edges.TryGetValue(edge, out int count); edges[edge] = count + 1;
                lengths.Add(Vector3.Distance(vertices[a], vertices[b]));
                regular &= Mathf.Abs(Vector3.Dot(normal, vertices[a] - vertices[face[0]])) < .0001f;

            }
        }
        Check(edges.Count == 144 && edges.Values.All(n => n == 2) && regular,
            "All 144 hull edges are shared by two planar, convex, outward-facing faces");
        Check(faces.Count(f => CarrierHullGeometry.IsCrown(vertices, f)) == 1 &&
            carrier.GetComponent<CarrierVisuals>().shell.sharedMesh.triangles.Length == CarrierHullGeometry.OpenTriangleCount * 3,
            "Only the upward hexagonal crown is open for the core");
        bool docksFit = true;
        var drone = carrier.droneDefinition.prefab.GetComponent<DroneVisuals>();
        foreach (var dock in carrier.docks)
        {
            Vector3 p = dock.localPosition, normal = dock.localRotation * Vector3.forward;
            var face = faces.FirstOrDefault(f => f.Length == 8 &&
                f.All(i => Mathf.Abs(Vector3.Dot(vertices[i] - p, normal)) < .0001f));
            docksFit &= face != null && Mathf.Abs(p.y) < .0001f && Mathf.Abs(normal.y) < .0001f;
            if (face != null)
            {
                var center = face.Aggregate(Vector3.zero, (sum, i) => sum + vertices[i]) / 8f;
                docksFit &= Vector3.Distance(center, p) < .0001f;
            }
            var display = dock.GetComponentInChildren<DroneVisuals>(true);
            docksFit &= display.transform.localPosition.sqrMagnitude < .0001f && display.transform.localScale == drone.transform.localScale;
            Vector3 tail = p - normal * drone.noseLength * drone.tailLengthRatio * drone.transform.localScale.z;
            docksFit &= faces.All(f => Vector3.Dot(Vector3.Cross(vertices[f[1]] - vertices[f[0]], vertices[f[2]] - vertices[f[0]]).normalized, tail - vertices[f[0]]) < .0001f);
        }
        Check(docksFit, "Six full-size Drone midsections sit on horizontal octagonal-face centers with tails inside the hull");
        Check(Mathf.Approximately(carrier.damageTrigger.radius, .775f) && Mathf.Approximately(carrier.definition.spawnRadiusWorld, 1.25f),
            "Hitbox and spawn clearance match the compact hull");
    }

    private static void Capture(string name, Vector3 center, float size, Vector3? direction = null, Transform extra = null)
    {
        var go = new GameObject("Carrier validation camera"); var camera = go.AddComponent<Camera>();
        var rt = new RenderTexture(1000, 1000, 24); var texture = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
        var old = RenderTexture.active;
        var members = carrier.GetComponentsInChildren<Transform>(true).Concat(extra ? extra.GetComponentsInChildren<Transform>(true) : Array.Empty<Transform>()).ToArray();
        var layers = members.Select(t => t.gameObject.layer).ToArray();
        try
        {
            // Isolate this actual prefab instance for the render, then immediately restore gameplay layers.
            foreach (var member in members) member.gameObject.layer = 31;
            go.transform.position = center + (direction ?? new Vector3(0f, 8f, -3f)); go.transform.LookAt(center);
            camera.orthographic = true; camera.orthographicSize = size; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 31; camera.targetTexture = rt; camera.Render();
            RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0); texture.Apply();
            File.WriteAllBytes(Output + "/" + name, texture.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < members.Length; i++) members[i].gameObject.layer = layers[i];
            RenderTexture.active = old; camera.targetTexture = null; Object.Destroy(go); Object.Destroy(texture); rt.Release(); Object.Destroy(rt);
        }
    }
}
#endif
