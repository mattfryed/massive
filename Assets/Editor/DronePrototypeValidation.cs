#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Massive.Enemies;
using Massive.Multiplier;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real Play Mode physics in a temporary scene; preserves the user's open dirty scenes.</summary>
[InitializeOnLoad]
public static partial class DronePrototypeValidation
{
    private const string Key = "MASSIVE.Drone.Validation.";
    private static IEnumerator run;
    private static float resumeAt;
    private static int resumeFrame;
    private static int passed;
    private static readonly List<Object> owned = new List<Object>();
    public static string LastReport => SessionState.GetString(Key + "Report", "Not run");
    static DronePrototypeValidation() { EditorApplication.playModeStateChanged += OnState; }

    [MenuItem("MASSIVE/Enemies/Drone/Run Prototype Validation")]
    public static void Start()
    {
        SessionState.SetBool(Key + "FeedbackOnly", false);
        SessionState.SetBool(Key + "TelegraphOnly", false);
        SessionState.SetBool(Key + "EnemyUpgradeOnly", false);
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Wait for Edit Mode.");
        Scene original = SceneManager.GetActiveScene();
        string path = "Assets/DroneValidation-" + Guid.NewGuid().ToString("N") + ".unity";
        Scene fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(fixture);
            var camera = new GameObject("Validation Camera"); camera.tag = "MainCamera";
            camera.AddComponent<Camera>().orthographic = true; camera.AddComponent<AudioListener>();
            new GameObject("Validation Light").AddComponent<Light>().type = LightType.Directional;
            camera.transform.position = new Vector3(0, 3, -1.5f); camera.transform.LookAt(Vector3.zero);
            if (!EditorSceneManager.SaveScene(fixture, path)) throw new Exception("Cannot save fixture.");
        }
        finally { EditorSceneManager.CloseScene(fixture, true); SceneManager.SetActiveScene(original); }
        SessionState.SetString(Key + "Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + "Scene", path); SessionState.SetString(Key + "Report", "Running"); SessionState.SetBool(Key + "Pending", true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.isPlaying = true;
    }
    private static void OnState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Pending", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetBool(Key + "Background", Application.runInBackground); Application.runInBackground = true;
            passed = 0; owned.Clear();
            run = SessionState.GetBool(Key + "EnemyUpgradeOnly", false) ? EnemyUpgradeChecks() :
                SessionState.GetBool(Key + "TelegraphOnly", false) ? SpawnTelegraphChecks() :
                SessionState.GetBool(Key + "FeedbackOnly", false) ? EnemyHitFeedbackChecks() : Checks();
            resumeAt = 0f; resumeFrame = 0; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; run = null;
            Application.runInBackground = SessionState.GetBool(Key + "Background", Application.runInBackground);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Previous", ""));
            string path = SessionState.GetString(Key + "Scene", "");
            if (path.StartsWith("Assets/DroneValidation-", StringComparison.Ordinal) && path.EndsWith(".unity", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(path);
            SessionState.SetBool(Key + "Pending", false);
        }
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || run == null || Time.time < resumeAt || Time.frameCount < resumeFrame) return;
        try
        {
            if (run.MoveNext()) { resumeAt = Time.time + (run.Current is float ? (float)run.Current : .02f); resumeFrame = Time.frameCount + 2; return; }
            Finish(passed + " Drone checks passed.");
        }
        catch (Exception e) { Finish("FAILED after " + passed + " checks: " + e); }
    }
    private static void Finish(string report)
    {
        SessionState.SetString(Key + "Report", report); Debug.Log("[Drone Validation] " + report);
        EditorApplication.update -= Tick; run = null;
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.Destroy(owned[i]);
        owned.Clear(); EditorApplication.isPlaying = false;
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); passed++; }
    private static T Own<T>(T obj) where T : Object { owned.Add(obj); return obj; }
    private static void Place(PlayerControllerScript player, Vector3 position)
    { player.transform.position = position; player.GetComponent<Rigidbody>().position = position; Physics.SyncTransforms(); }

    private static PlayerControllerScript Player(int id, int team, Vector3 position)
    {
        var go = Own(new GameObject("Validation player " + id)); go.SetActive(false); go.transform.position = position;
        var rb = go.AddComponent<Rigidbody>(); rb.useGravity = false; rb.constraints = RigidbodyConstraints.FreezeAll;
        go.AddComponent<SphereCollider>().radius = .25f;
        var player = go.AddComponent<PlayerControllerScript>(); player.playerID = id; player.teamID = team;
        player.SetControlMode(PlayerControlMode.Scripted);
        var so = new SerializedObject(player); so.FindProperty("playMatchSpawnOnSceneLoad").boolValue = false; so.ApplyModifiedPropertiesWithoutUndo();
        go.SetActive(true); return player;
    }
    private static DroneController Drone(Vector3 position, bool behavior = true)
    {
        var go = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DronePrototypeSetup.PrefabPath), position, Quaternion.identity));
        var controller = go.GetComponent<DroneController>(); controller.enabled = behavior;
        go.GetComponent<EnemyBase>().Init(controller.definition, null);
        return controller;
    }
    private static void Remove(DroneController drone) { if (drone != null) Object.Destroy(drone.gameObject); }

    private static IEnumerator Checks()
    {
        Check(SceneManager.GetActiveScene().path == SessionState.GetString(Key + "Scene", ""), "Isolated test scene required");
        var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DronePrototypeSetup.DefinitionPath);
        var profile = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset")));
        Check(def.healthMassEq == def.damageTakenPerSwordHit && def.healthMassEq > 0, "One-hit definition");
        Check(profile.TryGetReward(def.defeatRewardKey, out var row) && row.BaseMilliElectronVolts == 100, "100 meV central reward");
        Check(row.repeatPolicy == ScoreRepeatPolicy.OncePerSourceToken && row.chainCharge == .25f, "Small charge and deduplication");
        var service = Own(new GameObject("Validation score service")).AddComponent<MatchScoreService>(); service.Configure(profile);
        var p1 = Player(0, 1, new Vector3(-12, 0, 0)); var p2 = Player(1, 2, new Vector3(12, 0, 0));
        service.RegisterPlayer(p1); service.RegisterPlayer(p2);
        service.OpenScoring();
        yield return .08f;

        // Real physics sword collider enters the actual prefab's hurtbox.
        var d = Drone(new Vector3(-10, 0, 0), false); var e = d.GetComponent<EnemyBase>();
        var sword = new GameObject("Validation sword"); sword.SetActive(false); sword.transform.SetParent(p1.transform, false);
        sword.AddComponent<SphereCollider>().radius = .35f; var melee = sword.AddComponent<PlayerMelee>();
        var ms = new SerializedObject(melee); ms.FindProperty("gateHitboxToActivationWindow").boolValue = false; ms.ApplyModifiedPropertiesWithoutUndo();
        sword.transform.position = d.transform.position; sword.SetActive(true); Physics.SyncTransforms();
        yield return .08f;
        Check(e.IsDead && service.GetTeamScore(1) == 100 && service.GetTeamScore(2) == 0,
            "Sword physics: dead=" + e.IsDead + " score=" + service.GetTeamScore(1) + " owner=" + melee.Owner +
            " collider=" + sword.GetComponent<Collider>().enabled + " sword=" + sword.transform.position + " enemy=" + e.transform.position + " hp=" + e.HealthRemaining);
        Check(Mathf.Approximately(service.GetPlayerChain(0).Charge, .25f), "Kill adds only authored charge");
        string token = e.SourceLifeToken;
        Check(!service.TryAwardToPlayer(def.defeatRewardKey, p1, token, e.transform.position, out _), "Duplicate defeat rejected");
        Check(!e.GetComponent<Collider>().enabled, "Dead Drone stops colliding before animation finishes");
        var toast = Object.FindFirstObjectByType<EnemyScoreToast>();
        Check(toast != null && toast.Amount == 100 && toast.label.text == "+100 meV" && EnemyScoreToast.ActiveCount == 1, "Exactly one toast shows the accepted base score");
        Capture(d, "drone-score-toast.png");
        Object.Destroy(sword); yield return .4f; Check(d == null, "Death animation cleans up the prefab");

        service.ResetForMatch(true); service.GetPlayerChain(0).ApplyAward(ScoreChainAwardMode.AdvanceAndRefresh, 4f); service.AdvanceTeamAmplifier(1);
        d = Drone(Vector3.zero, false); d.GetComponent<EnemyBase>().TakeDamage(1f, EnemyDamageSource.Sword, p1);
        Check(service.GetTeamScore(1) == 400, "Personal x2 and team x2 combine with central reward");
        Check(Object.FindObjectsByType<EnemyScoreToast>(FindObjectsSortMode.None).Any(t => t.Amount == 400 && t.label.text == "+400 meV"), "Toast includes personal and team multipliers");
        yield return .35f;
        service.ResetForMatch(true); d = Drone(Vector3.zero, false); d.GetComponent<EnemyBase>().Kill();
        Check(service.GetTeamScore(1) == 0, "Administrative removal awards no energy"); yield return .9f;
        Check(EnemyScoreToast.ActiveCount == 0, "Toasts expire; administrative removal creates none");

        // Slow the lifecycle only in the isolated fixture so intermediate states remain observable at varying frame rates.
        d = Drone(Vector3.zero, false); var trace = d.GetComponent<DroneVisuals>(); trace.spawnSeconds = 2f;
        yield return .65f;
        var partial = Enumerable.Range(0, 12).Select(i => trace.FacetProgress(i)).ToArray();
        Check(partial.Any(t => t > 0f && t < 1f) && partial.Max() - partial.Min() > .02f, "Facet paths have partial lengths and independent start/duration variation");
        Capture(d, "drone-spawn-trace.png");
        yield return 1.5f;
        Check(Enumerable.Range(0, 12).All(i => trace.FacetProgress(i) == 1f), "All outlines finish by the spawn deadline");
        d.GetComponent<EnemyBase>().despawnDelaySeconds = 2f; d.GetComponent<EnemyBase>().Kill();
        yield return .7f;
        Check(Enumerable.Range(0, 12).Any(i => trace.FacetProgress(i) > 0f && trace.FacetProgress(i) < 1f), "Death retracts partially drawn paths");
        Capture(d, "drone-death-trace.png"); yield return 1.4f;
        Check(d == null, "Traced breakup finishes and destroys the Drone");

        d = Drone(Vector3.zero); d.idleSpeed = 0f;
        var visual = d.GetComponent<DroneVisuals>(); float roll = visual.RollAngle;
        yield return .65f;
        Check(d.Target == null && d.transform.position.sqrMagnitude < .001f, "No global chase outside detection range");
        Check(Mathf.Abs(Mathf.DeltaAngle(roll, visual.RollAngle)) > 15f, "Idle axial rotation continues");
        Check(visual.noseMesh.sharedMesh.vertexCount == 18 && visual.tailLengthRatio == .5f, "Six triangular front facets and half-length tail");
        Place(p1, new Vector3(4, 0, 0)); yield return .35f;
        Check(d.Target == p1 && d.GetComponent<Rigidbody>().linearVelocity.x > 0, "Detects and accelerates toward nearby player");
        Check(visual.ActiveExhaustCount > 0, "Motion emits bounded metaball exhaust");
        var block = new MaterialPropertyBlock(); visual.engineRenderer.GetPropertyBlock(block);
        var engineBalls = block.GetVectorArray("_Balls"); int ballCount = block.GetInt("_BallCount");
        Check(DroneVisuals.CoreBallCount == 7 && ballCount > 14 && ballCount <= 39, "Seven-lobe core and a denser bounded exhaust");
        Check(engineBalls.Take(7).Select(v => v.w).Max() - engineBalls.Take(7).Select(v => v.w).Min() > .001f, "Core lobes have varied radii");
        yield return .35f; Capture(d, "drone-moving.png");
        Place(p2, p1.transform.position + Vector3.forward * .2f); yield return .25f;
        Check(d.Target == p1, "Similar-distance target does not cause target flicker");
        p1.temporarilyEliminated = true; yield return .25f;
        Check(d.Target == p2, "Eliminated target is replaced");
        p1.temporarilyEliminated = false; Place(p1, new Vector3(-12, 0, 0)); Place(p2, new Vector3(12, 0, 0)); yield return .3f;
        Check(d.Target == null, "Disengages after target leaves retention range");
        e = d.GetComponent<EnemyBase>(); e.Pause(true); Vector3 frozen = d.transform.position; roll = visual.RollAngle;
        yield return .25f;
        Check((d.transform.position - frozen).sqrMagnitude < .000001f && visual.RollAngle == roll, "Pause freezes motion and visual clock");
        e.Pause(false); Remove(d); yield return .05f;

        Place(p1, Vector3.zero); d = Drone(Vector3.zero); d.idleSpeed = 0f; float mass = p1.massScore;
        yield return .15f;
        Check(p1.massScore == mass && !d.GetComponent<EnemyBase>().IsDead, "Spawn grace blocks surprise contact damage");
        yield return .43f;
        Check(d.GetComponent<EnemyBase>().IsDead && Mathf.Abs(p1.massScore - (mass - def.damageToPlayerMass01)) < .0001f, "Undefended contact damages once and consumes Drone");
        Check(service.GetTeamScore(1) == 0, "Contact suicide grants no defeat score"); yield return .4f;

        p1.SetScriptedInput(new PlayerInputFrame { shieldHeld = true }); yield return .05f;
        Check(p1.shieldOn, "Shield input has reached gameplay state");
        d = Drone(Vector3.zero); d.spawnGraceSeconds = 0f;
        mass = p1.massScore; yield return .2f;
        Check(p1.massScore == mass && !d.GetComponent<EnemyBase>().IsDead, "Shield blocks contact damage");
        Check(d.transform.position.sqrMagnitude > .001f, "Shield repels Drone"); Remove(d); p1.ClearScriptedInput(); yield return .05f;

        var attack = p1.gameObject.AddComponent<Massive.Player.PlayerAttackController>(); p1.attackController = attack;
        var attackData = new SerializedObject(attack);
        attackData.FindProperty("attackProfile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Massive.Player.PlayerAttackProfile>("Assets/Scripts/Player/Actions/PlayerAttackProfile.asset");
        attackData.ApplyModifiedPropertiesWithoutUndo();
        d = Drone(Vector3.zero); d.spawnGraceSeconds = 0f; mass = p1.massScore;
        attack.BeginAttack(); Check(attack.IsAttacking, "Real attack state activated");
        d.SendMessage("ResolveContact", p1.GetComponent<Collider>());
        Check(p1.massScore == mass && !d.GetComponent<EnemyBase>().IsDead, "Attacking player is protected from body contact");
        attack.CancelAttack(); Remove(d); yield return .05f;

        Place(p1, new Vector3(5, 0, 0)); var neighbor = Drone(new Vector3(0f, 0f, .2f));
        d = Drone(new Vector3(0f, 0f, -.2f)); d.spawnGraceSeconds = neighbor.spawnGraceSeconds = 0f;
        yield return .5f;
        Check(Vector3.Distance(d.transform.position, neighbor.transform.position) > .5f, "Swarm separation keeps followers apart");
        Remove(neighbor); Remove(d); yield return .05f;

        // A thin solid wall must not be crossed by the trigger body during pursuit.
        Place(p1, new Vector3(3f, 0, 0)); var wall = Own(GameObject.CreatePrimitive(PrimitiveType.Cube)); wall.layer = 11;
        wall.transform.position = Vector3.zero; wall.transform.localScale = new Vector3(.1f, 2f, 5f);
        d = Drone(new Vector3(-1, 0, 0)); d.spawnGraceSeconds = 0f; Physics.SyncTransforms();
        yield return .8f; Check(d.transform.position.x < -.15f, "Swept obstacle guard prevents tunneling into wall"); Remove(d); Object.Destroy(wall); yield return .05f;

        foreach (string path in new[] { "Assets/Power-ups/Amplifier Core/Amplifier Core.prefab", "Assets/Enemy System/Enemy Types/Melee/DysonSphere/Enemy_DysonSphere.prefab" })
        {
            Place(p1, new Vector3(6f, 0, 0));
            var obstacle = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), Vector3.zero, Quaternion.identity));
            var amplifier = obstacle.GetComponent<AmplifierCoreGameplay>();
            if (amplifier != null)
            {
                // Let its real spawn animation restore physics before freezing it for a steering test.
                float spawnDeadline = Time.time + 4f;
                while (amplifier.IsSpawning && Time.time < spawnDeadline) yield return .05f;
                Check(!amplifier.IsSpawning, "Amplifier has completed its collider-disabled spawn phase");
            }
            foreach (var behavior in obstacle.GetComponentsInChildren<MonoBehaviour>()) behavior.enabled = false;
            foreach (var rb in obstacle.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
            var obstacleCollider = obstacle.GetComponentsInChildren<Collider>().First(c => !c.isTrigger);
            d = Drone(new Vector3(-2f, 0, 0)); d.spawnGraceSeconds = 0; d.detectionRange = 12; d.disengageRange = 14;
            var avoid = d.GetComponent<EnemyObstacleAvoidance>(); Physics.SyncTransforms();
            Check(avoid.HasObstacleInDirection(Vector3.right, 4f, out var obstacleHit) && obstacleHit.collider == obstacleCollider, "Actual prefab detected: " + path + " enabled=" + obstacleCollider.enabled + " bounds=" + obstacleCollider.bounds + " hit=" + obstacleHit.collider);
            Check(!avoid.ShouldAvoid(p1.GetComponent<Collider>()) && !avoid.ShouldAvoid(d.GetComponent<Collider>()), "Player and self remain contact targets, not navigation obstacles");
            bool penetrated = false; float maxSide = 0f; float deadline = Time.time + 3.1f;
            while (Time.time < deadline && d != null)
            {
                yield return .04f;
                if (d == null) break;
                maxSide = Mathf.Max(maxSide, Mathf.Abs(d.transform.position.z));
                if (Physics.ComputePenetration(d.GetComponent<Collider>(), d.transform.position, d.transform.rotation,
                    obstacleCollider, obstacleCollider.transform.position, obstacleCollider.transform.rotation, out _, out float depth) && depth > .035f) penetrated = true;
            }
            Check(d != null && !penetrated && d.transform.position.x > .6f && maxSide > .35f,
                "Routes around actual " + obstacle.name + ": position=" + (d != null ? d.transform.position.ToString() : "destroyed") + " penetration=" + penetrated + " side=" + maxSide);
            // Simulate a moving obstacle entering the Drone's current volume.
            d.idleSpeed = 0f; Place(p1, new Vector3(20f, 0, 0)); d.detectionRange = 1; d.disengageRange = 1;
            d.GetComponent<Rigidbody>().position = obstacleCollider.bounds.center + Vector3.right * .1f; Physics.SyncTransforms();
            yield return .15f;
            Check(!Physics.ComputePenetration(d.GetComponent<Collider>(), d.transform.position, d.transform.rotation,
                obstacleCollider, obstacleCollider.transform.position, obstacleCollider.transform.rotation, out _, out float remainingDepth) || remainingDepth < .025f,
                "Recovers from an obstacle overlapping the trigger body: " + obstacle.name);
            Remove(d); Object.Destroy(obstacle); yield return .05f;
        }

        var toastPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrototypeSetup.ToastPath).GetComponent<EnemyScoreToast>();
        for (int i = 0; i < 40; i++) EnemyScoreToast.Show(toastPrefab, new ScoreAwardResult { accepted = true, finalMilliElectronVolts = 1600, worldPosition = new Vector3(0, 0, 0) }, SceneManager.GetActiveScene());
        Check(EnemyScoreToast.ActiveCount == 24, "Burst feedback is capped at 24 reusable labels");
        Check(Object.FindObjectsByType<EnemyScoreToast>(FindObjectsSortMode.None).All(t => t.label.text == "+1.6 eV"), "Fractional unit display preserves credited energy");
        yield return .9f; Check(EnemyScoreToast.ActiveCount == 0, "Pooled burst labels all retire");

        var resonanceChecks = ResonanceSludgeChecks(p1, p2);
        while (resonanceChecks.MoveNext()) yield return resonanceChecks.Current;
        var feedbackChecks = EnemyHitFeedbackChecks();
        while (feedbackChecks.MoveNext()) yield return feedbackChecks.Current;

        // Actual director, safe placement and staggered batch scheduling.
        Place(p1, new Vector3(-12, 0, 0)); Place(p2, new Vector3(12, 0, 0));
        var gridGo = Own(new GameObject("Validation bounds")); gridGo.SetActive(false); gridGo.transform.rotation = Quaternion.Euler(270, 0, 0);
        var grid = gridGo.AddComponent<VectorGridGPU>(); grid.enabled = false; grid.size = new Vector2(28, 12);
        gridGo.SetActive(true);
        var bounds = gridGo.AddComponent<ArenaBoundsFromVectorGrid>(); bounds.RefreshNow();
        var spawn = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(DronePrototypeSetup.ProfilePath)));
        var r = spawn.batches[0]; Check(r.SizeAt(0) == 3 && r.SizeAt(120) == 7 && r.DelayAt(120) < r.DelayAt(0), "Batch sizes and cadence ramp over 120 seconds");
        spawn.rules.Clear(); r.telegraphPrefab = null; // Keep the original unannounced-batch compatibility check.
        r.firstBatchDelay = .1f; r.intervalWithinBatch = .2f; r.initialBatchSize = r.finalBatchSize = 3;
        r.initialBatchDelay = r.finalBatchDelay = .3f; r.maxAlive = 3; spawn.maxAliveTotal = 3;
        var spawner = Own(new GameObject("Validation director")); var region = spawner.AddComponent<AmplifierSpawnRegion>();
        region.arenaBounds = bounds; region.neutralWidthFraction = 1f; region.clearanceWorld = .05f; region.drawZones = false;
        var director = spawner.AddComponent<EnemyDirector>(); director.spawnProfile = spawn; director.arenaBounds = bounds;
        director.placementRegion = region; director.spawnCheckRadiusWorld = .25f; director.waitForScoring = true;
        service.CloseScoring(); yield return .25f; Check(director.TotalSpawned == 0, "Batches wait for scoring to open");
        service.OpenScoring(); yield return .75f;
        var batchMembers = director.GetComponentsInChildren<EnemyBase>();
        Array.Sort(batchMembers, (a, b) => a.SpawnTime.CompareTo(b.SpawnTime));
        Check(batchMembers.Length == 3, "First complete batch has three members");
        Check(batchMembers[1].SpawnTime - batchMembers[0].SpawnTime >= .19f && batchMembers[2].SpawnTime - batchMembers[1].SpawnTime >= .19f,
            "Batch members arrive sequentially rather than as one burst");
        yield return .55f; Check(director.AliveCount == 3 && director.TotalSpawned == 3, "Alive cap prevents later batch overflow");
        float age = director.GameplayAge; service.SetScoringState(true, false); yield return .25f;
        Check(Mathf.Abs(director.GameplayAge - age) < .025f, "Bonus pause freezes batch progression");
        service.CloseScoring();
        Check(Shader.Find("MASSIVE/MetaballSDF") != null, "Existing metaball shader available");
        Object.Destroy(spawner); Object.Destroy(gridGo); yield return .05f;
        var telegraphChecks = SpawnTelegraphChecks();
        while (telegraphChecks.MoveNext()) yield return telegraphChecks.Current;
        var upgradeChecks = EnemyUpgradeChecks();
        while (upgradeChecks.MoveNext()) yield return upgradeChecks.Current;
    }

    private static void Capture(DroneController drone, string filename)
    {
        // Optional local image, under the same disposable temp directory as Unity's test output.
        var go = Own(new GameObject("Drone capture")); var camera = go.AddComponent<Camera>();
        camera.transform.position = drone.transform.TransformPoint(new Vector3(0f, 3f, -1.5f));
        camera.transform.LookAt(drone.transform.position);
        camera.orthographic = true; camera.orthographicSize = .85f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.085f, .1f, .12f);
        camera.cullingMask = 1 << LayerMask.NameToLayer("Enemy");
        var rt = new RenderTexture(900, 900, 24) { antiAliasing = 8 };
        var tex = new Texture2D(900, 900, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); tex.Apply();
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), filename);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG()); SessionState.SetString(Key + "Capture", path);
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; Object.Destroy(tex); Object.Destroy(rt); Object.Destroy(go); }
    }
}
#endif
