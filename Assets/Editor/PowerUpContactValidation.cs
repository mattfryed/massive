#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Player;
using Massive.PowerUps;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Isolated runtime regressions for pickup contacts and beam obstruction release.</summary>
[InitializeOnLoad]
public static class PowerUpContactValidation
{
    const string Key = "MASSIVE.PowerUpContactValidation", Folder = "Library/PowerUpContactValidation";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> results = new(), errors = new();
    static IEnumerator routine;
    static GameObject scope;
    static Scene scene;
    static ParticleAcceleratorPowerUpDefinition definition;
    static int frame, failures;
    static double deadline;
    static PowerUpContactValidation() { EditorApplication.playModeStateChanged += State; }

    [MenuItem("MASSIVE/Power-ups/Validate Pickup Contacts and Beam Growth")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Use Edit Mode after compilation.");
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/report.txt", "STARTING\n");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            results.Clear(); errors.Clear(); failures = 0; frame = -1;
            deadline = EditorApplication.timeSinceStartup + 120;
            routine = Checks(); Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        { SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log; }
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    static void Tick()
    {
        if (routine == null || !EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out.");
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception exception)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        if (scope) Object.DestroyImmediate(scope);
        if (definition) Object.DestroyImmediate(definition);
        if (scene.IsValid()) SceneManager.UnloadSceneAsync(scene);
        bool pass = exception == null && failures == 0 && errors.Count == 0;
        File.WriteAllText(Folder + "/report.txt", (pass ? "PASSED" : "FAILED") + $" ({results.Count} checks, {failures} failures)\n" +
            string.Join("\n", results) + "\n" + exception + "\n" + string.Join("\n", errors));
        Debug.Log("POWER-UP CONTACT VALIDATION " + (pass ? "PASSED" : "FAILED") + $" ({results.Count} checks, {failures} failures)");
        EditorApplication.isPlaying = false;
    }
    static void Check(bool pass, string label)
    { results.Add((pass ? "PASS " : "FAIL ") + label); if (!pass) failures++; }
    static void Invoke(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, Private).Invoke(target, args);
    static void Set(object target, string name, object value)
        => target.GetType().GetField(name, Private).SetValue(target, value);
    static GameObject Clone(string path)
        => Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), scope.transform);
    static void Pose(GameObject go, Vector3 point)
    { go.transform.position = point; if (go.TryGetComponent<Rigidbody>(out var body)) body.position = point; Physics.SyncTransforms(); }
    static PowerUpPickup Pickup(Vector3 point)
    {
        var pickup = PowerUpPickup.Spawn(definition, point, Quaternion.identity, scope.transform);
        pickup.GetComponent<PowerUpIconTetheredBody>().enabled = false;
        pickup.GetComponent<Rigidbody>().isKinematic = true;
        return pickup;
    }
    static IEnumerator Checks()
    {
        scene = SceneManager.CreateScene("Pickup contact validation", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        scope = new GameObject("Isolated pickup validation"); scope.SetActive(false); SceneManager.MoveGameObjectToScene(scope, scene);
        scope.transform.position = new Vector3(4000, 0, 4000);
        var player = Clone("Assets/Prefabs/PlayerActor.prefab").GetComponent<PlayerControllerScript>();
        player.ConfigureDemonstration(scope.transform, 0, 1);
        scope.SetActive(true); yield return null; yield return null;
        var body = player.GetComponent<Rigidbody>(); body.useGravity = false; body.isKinematic = true;
        player.GetComponent<PlayerScaleAdjuster>().ApplyScale();
        var attack = player.GetComponent<PlayerAttackController>();
        var melee = player.GetComponentInChildren<PlayerMelee>(true);
        var sword = melee.GetComponent<Collider>();
        var repulsor = player.GetComponentInChildren<PlayerRepulsorAOE>(true);
        var pulse = repulsor.GetComponent<SphereCollider>();
        var physics = scene.GetPhysicsScene();
        definition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ParticleAcceleratorPowerUpDefinition>(ParticleAcceleratorBeamSetup.DefinitionPath));

        // Real, enabled stage colliders contacting the canonical pickup in an isolated physics scene.
        for (int stage = 0; stage < 3; stage++)
        {
            player.powerUps.Clear(); attack.CancelAttack(); Pose(player.gameObject, scope.transform.position);
            Invoke(attack, "StartStage", stage);
            float until = Time.time + 3;
            while (!(stage == 2 ? pulse.enabled : sword.enabled) && attack.IsAttacking && Time.time < until) yield return null;
            Collider active = stage == 2 ? pulse : sword;
            Check(active.enabled, $"Stage {stage + 1} opens its real attack collider");
            Vector3 center = stage == 2 ? repulsor.OriginWorld + Vector3.right * repulsor.RadiusWorld * .7f : active.bounds.center;
            var pickup = Pickup(center); int claims = 0; pickup.Claimed += _ => claims++;
            Physics.SyncTransforms(); physics.Simulate(.02f);
            Check(claims == 1, $"Stage {stage + 1} claims pickup exactly once through physics (claims={claims})");
            physics.Simulate(.02f);
            Check(claims <= 1, $"Stage {stage + 1} duplicate contact cannot award twice");
            Object.DestroyImmediate(pickup.gameObject); attack.CancelAttack(); player.powerUps.Clear();
        }

        var untouched = Pickup(player.transform.position);
        physics.Simulate(.02f);
        Check(!untouched.GetComponent<PowerUpIconManifestAnimator>().IsDespawning, "Body overlap does not claim attack-only power-up");
        Object.DestroyImmediate(untouched.gameObject);

        // Exercise the stay path when the same sword remains overlapping across stage handoff.
        Invoke(attack, "StartStage", 1);
        float wait = Time.time + 3;
        while (!sword.enabled && attack.IsAttacking && Time.time < wait) yield return null;
        var staying = Pickup(sword.bounds.center); int stays = 0; staying.Claimed += _ => stays++;
        var stayMethod = typeof(PowerUpPickup).GetMethod("OnTriggerStay", Private);
        if (stayMethod != null) stayMethod.Invoke(staying, new object[] { sword });
        Check(stays == 1, "Sweep can claim an existing overlap without a fresh trigger-enter event");
        Object.DestroyImmediate(staying.gameObject); attack.CancelAttack(); player.powerUps.Clear();

        // Collection eligibility is deliberately independent of pseudo-player exclusion.
        Set(player, "isPseudoPlayer", false);
        foreach (string path in new[] { "Assets/Prefabs/Matter nugget.prefab", "Assets/Scripts/Anomalies/ORBITAL/Orbital Mass Nugget.prefab", "Assets/Scripts/Anomalies/ORBITAL/Mass Nugglet.prefab" })
        {
            var nugget = Clone(path).GetComponent<MatterNuggetScript>();
            nugget.Eject(scope.transform.position + Vector3.forward * 20, Vector3.zero, 30);
            typeof(MatterNuggetScript).GetField("age", Private).SetValue(nugget, 1f);
            var collect = typeof(MatterNuggetScript).GetMethod("TryCollect", Private);
            foreach (Collider shape in new[] { sword, pulse, player.GetComponentsInChildren<Collider>(true).First(c => c.CompareTag("Shield")) })
            {
                collect.Invoke(nugget, new object[] { shape });
                Check(!nugget.IsDespawning, nugget.name + " rejects " + shape.name + " collection");
                if (nugget.IsDespawning) { nugget.Eject(scope.transform.position + Vector3.forward * 20, Vector3.zero, 30); typeof(MatterNuggetScript).GetField("age", Private).SetValue(nugget, 1f); }
            }
            float mass = player.massScore = Mathf.Lerp(player.massScoreMin, player.massScoreMax, .3f);
            collect.Invoke(nugget, new object[] { player.GetComponent<SphereCollider>() });
            Check(nugget.IsDespawning && player.massScore > mass, nugget.name + " body restores mass and consumes pickup");
            Object.DestroyImmediate(nugget.gameObject);
        }
        Set(player, "isPseudoPlayer", true);

        // Use Unity's ordinary FixedUpdate/interpolation/render loop for visual
        // contacts; PhysicsScene.Simulate alone does not exercise that path.
        SceneManager.MoveGameObjectToScene(scope, SceneManager.GetActiveScene());
        Invoke(attack, "StartStage", 2);
        wait = Time.time + 3;
        while (!pulse.enabled && attack.IsAttacking && Time.time < wait) yield return null;
        var edgePickup = Pickup(repulsor.OriginWorld + Vector3.right * repulsor.EndRadiusWorld);
        int edgeClaims = 0; edgePickup.Claimed += _ => edgeClaims++;
        Physics.SyncTransforms(); Invoke(repulsor, "CompleteFinalCoverage");
        Check(edgeClaims == 1, "Repulsor final-radius sweep claims pickups between physics ticks");
        Object.DestroyImmediate(edgePickup.gameObject); attack.CancelAttack(); player.powerUps.Clear();
        player.enabled = false; player.powerUps.enabled = false;

        // Freeze live particles before shifting the physics root so animation cannot mask their drift.
        foreach (string name in new[] { "ParticleAccelerator", "TimeDilation", "Decoherence", "MassNode" })
        {
            var icon = Clone("Assets/Power-ups/PU_" + name + ".prefab");
            Pose(icon, scope.transform.position + Vector3.forward * 10);
            icon.GetComponent<PowerUpIconTetheredBody>().enabled = false;
            icon.GetComponent<Rigidbody>().isKinematic = true;
            float ready = Time.time + 2;
            while (Time.time < ready) yield return null;
            var wire = icon.GetComponentInChildren<ParametricPolyhedronWire>();
            var particles = icon.GetComponentsInChildren<ParticleSystem>();
            var before = new List<Vector3>();
            foreach (var ps in particles) { ps.Pause(); before.Add(ParticleCenter(ps)); }
            Vector3 delta = new Vector3(.7f, 0, .25f), shell = wire.transform.position;
            Pose(icon, icon.transform.position + delta);
            Check(Vector3.Distance(wire.transform.position - shell, delta) < .001f, name + " shell follows displaced root");
            for (int i = 0; i < particles.Length; i++)
                Check(Vector3.Distance(ParticleCenter(particles[i]) - before[i], delta) < .005f, name + " live inner particles follow shell displacement");
            foreach (var sdf in icon.GetComponentsInChildren<MetaballSDFInstance>())
                Check(Vector3.Distance(sdf.TargetRenderer.transform.position, wire.transform.position) < .01f, name + " metaball icon remains centered on shell");

            // A physics-driven displacement exercises interpolation and shader
            // matrices that a direct Transform reposition cannot reproduce.
            var iconBody = icon.GetComponent<Rigidbody>();
            foreach (var ps in particles) ps.Play();
            iconBody.isKinematic = false;
            icon.GetComponent<PowerUpIconTetheredBody>().enabled = true;
            icon.GetComponent<PowerUpIconTetheredBody>().ResetAnchorHere();
            Vector3 home = iconBody.position;
            Capture(icon, name + "-before-shove.png");
            var pusher = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pusher.transform.SetParent(scope.transform); pusher.transform.localScale = Vector3.one;
            Pose(pusher, home - Vector3.right * .7f);
            float maxDisplacement = 0, maxSeparation = 0;
            for (int tick = 0; tick < 15; tick++)
            {
                yield return null;
                maxDisplacement = Mathf.Max(maxDisplacement, Vector3.Distance(home, iconBody.position));
                foreach (var sdf in icon.GetComponentsInChildren<MetaballSDFInstance>())
                    maxSeparation = Mathf.Max(maxSeparation, Vector3.Distance(sdf.TargetRenderer.localToWorldMatrix.GetColumn(3), wire.transform.position));
                foreach (var ps in particles)
                    maxSeparation = Mathf.Max(maxSeparation, Vector3.Distance(ps.transform.position, wire.transform.position));
                if (tick == 5) Capture(icon, name + "-during-shove.png");
            }
            Check(maxDisplacement > .03f, name + $" physical contact displaces shell ({maxDisplacement:F3})");
            Check(maxSeparation < .02f, name + $" rendered icon stays centered during physical contact ({maxSeparation:F3})");
            Check(icon.GetComponentsInChildren<Rigidbody>().Length == 1,
                name + " shell and inner visuals share a single physics body");
            Object.DestroyImmediate(pusher);
            Object.DestroyImmediate(icon);
        }

        // Beam queries use the main physics scene, so move only this fixture back there.
        SceneManager.MoveGameObjectToScene(scope, SceneManager.GetActiveScene());
        Pose(player.gameObject, scope.transform.position);
        player.enabled = false; player.powerUps.enabled = false;
        definition.maxDistance = 20; definition.beamGrowSeconds = .4f; definition.maxTurnDelay = 0;
        var beam = Object.Instantiate(definition.sustainedBeamPrefab, scope.transform); beam.Initialize(player, definition);
        Vector3 origin = player.transform.position + Vector3.right + Vector3.up * .2f;
        var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.transform.SetParent(scope.transform);
        blocker.transform.localScale = new Vector3(.5f, 4, 4); Pose(blocker, origin + Vector3.right * 4);
        beam.Begin(origin, Vector3.right); beam.Advance(1, origin, Vector3.right);
        float blockedLength = beam.BeamLength;
        Check(blockedLength < 4 && beam.Contact, "Mature beam stops at near blocker");
        Pose(blocker, origin + Vector3.forward * 10);
        beam.Advance(.02f, origin, Vector3.right);
        Check(beam.BeamLength > blockedLength && beam.BeamLength < blockedLength + 2,
            $"Released beam regrows rather than snapping (before={blockedLength:F3}, after={beam.BeamLength:F3})");
        Check(!beam.Contact && !beam.contactPlasma.IsEmitting, "Growing free beam has no premature impact effects");
        beam.Advance(.5f, origin, Vector3.right);
        Check(beam.BeamLength > 19, "Beam reaches full length within Beam Grow Seconds");
        Pose(blocker, origin + Vector3.right * 3); beam.Advance(.02f, origin, Vector3.right);
        Check(beam.BeamLength < 3 && beam.Contact, "New nearer obstruction clips immediately");
        beam.Stop(); beam.Advance(.5f, origin, Vector3.right);
        Check(beam.BeamLength == 0, "Release still retracts fully");

        var staging = new GameObject("Inactive target staging"); staging.transform.SetParent(scope.transform); staging.SetActive(false);
        var target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), staging.transform).GetComponent<PlayerControllerScript>();
        target.ConfigureDemonstration(scope.transform, 2, 3); target.enabled = false; target.powerUps.enabled = false;
        target.GetComponent<Rigidbody>().isKinematic = true;
        Pose(target.gameObject, origin + Vector3.right * 12);
        staging.SetActive(true);
        definition.playerDamagePerSecond = .2f;
        beam.Begin(origin, Vector3.right); beam.Advance(1, origin, Vector3.right);
        float targetMass = target.massScore;
        Pose(blocker, origin + Vector3.forward * 10);
        beam.Advance(.01f, origin, Vector3.right);
        Check(target.massScore == targetMass && !beam.Contact && !beam.contactPlasma.IsEmitting,
            "Next target takes no damage or impact plasma before the growing tip arrives");
        beam.Advance(definition.beamGrowSeconds, origin, Vector3.right);
        Check(beam.Contact && beam.Contact.GetComponentInParent<PlayerControllerScript>() == target && target.massScore < targetMass && beam.contactPlasma.IsEmitting,
            "Regrown tip reaches, damages and emits impact plasma on the next target");
        target.gameObject.SetActive(false);
        beam.Begin(origin, Vector3.right);
        for (int i = 0; i < 500; i++) beam.Advance(.001f, origin, Vector3.right);
        Check(beam.BeamLength > 19, "Very short frame steps do not stall initial beam growth");
        float[] extensions = new float[2];
        for (int i = 0; i < 2; i++)
        {
            definition.beamGrowSeconds = i == 0 ? .2f : .8f;
            Pose(blocker, origin + Vector3.right * 3); beam.Begin(origin, Vector3.right); beam.Advance(1, origin, Vector3.right);
            float start = beam.BeamLength;
            Pose(blocker, origin + Vector3.forward * 10); beam.Advance(.02f, origin, Vector3.right);
            extensions[i] = beam.BeamLength - start;
        }
        Check(extensions[0] > extensions[1] * 2, "Beam Grow Seconds controls regrowth speed as well as first-shot growth");
        Check(errors.Count == 0, "No runtime Console errors");
    }
    static Vector3 ParticleCenter(ParticleSystem ps)
    {
        var particles = new ParticleSystem.Particle[ps.main.maxParticles]; int count = ps.GetParticles(particles);
        if (count == 0) throw new Exception("Expected live particles in " + ps.name);
        Vector3 center = Vector3.zero;
        for (int i = 0; i < count; i++) center += particles[i].position;
        center /= count;
        return ps.main.simulationSpace == ParticleSystemSimulationSpace.World ? center :
            (ps.main.simulationSpace == ParticleSystemSimulationSpace.Custom ? ps.main.customSimulationSpace : ps.transform).TransformPoint(center);
    }
    static void Capture(GameObject icon, string filename)
    {
        var go = new GameObject("Pickup validation camera"); var camera = go.AddComponent<Camera>(); camera.enabled = false;
        camera.transform.position = icon.transform.position + Vector3.up * 12;
        camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        camera.orthographic = true; camera.orthographicSize = 1.8f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .12f, .14f);
        var rt = RenderTexture.GetTemporary(600, 600, 24); var pixels = new Texture2D(600, 600, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            pixels.ReadPixels(new Rect(0, 0, 600, 600), 0, 0); pixels.Apply();
            File.WriteAllBytes(Folder + "/" + filename, pixels.EncodeToPNG());
        }
        finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(pixels); Object.DestroyImmediate(go); }
    }
}
#endif
