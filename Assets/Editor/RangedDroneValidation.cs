#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Enemies;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Runs actual prefab physics in Player Actions; all fixture mutations disappear on Play Mode exit.</summary>
[InitializeOnLoad]
public static partial class RangedDroneValidation
{
    private const string Key = "MASSIVE.RangedDroneValidation";
    public const string Output = "Library/RangedDroneValidation";
    private static IEnumerator routine;
    private static readonly List<string> results = new(), errors = new();
    private static readonly List<float> shotTimes = new();
    private static readonly List<RangedDroneProjectile> shots = new();
    private static double deadline;
    private static int frame;
    private static RangedDroneController ranged;
    private static DroneController drone;
    private static EnemyBase enemy;
    private static EnemyDirector director;
    private static PlayerControllerScript player;
    private static float pausedTime;
    static RangedDroneValidation() { EditorApplication.playModeStateChanged += OnState; }
    [MenuItem("MASSIVE/Enemies/Ranged Drone/Validate In Player Actions")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != CarrierPrototypeSetup.ScenePath)
            throw new InvalidOperationException("Run from Player Actions in Edit Mode.");
        if (!Object.FindFirstObjectByType<RangedDroneController>()) throw new InvalidOperationException("Place the Ranged Drone first.");
        Directory.CreateDirectory(Output); SessionState.SetBool(Key, true);
        File.WriteAllText(Output + "/report.txt", "RUNNING — awaiting Play Mode\n"); EditorApplication.isPlaying = true;
    }
    private static void OnState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            results.Clear(); errors.Clear(); shots.Clear(); shotTimes.Clear(); pausedTime = 0f; frame = -1;
            Application.logMessageReceived += Log; deadline = EditorApplication.timeSinceStartup + 110;
            routine = Checks(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            File.AppendAllText(Output + "/report.txt", "\nReturned to Edit Mode; runtime fixture changes discarded.\n");
        }
    }
    private static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame >= Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out: " + (ranged ? ranged.Phase.ToString() : "no Drone"));
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception exception) { Finish(exception); }
    }
    private static void Finish(Exception exception)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log; routine = null;
        File.WriteAllText(Output + "/report.txt", (exception == null ? "PASSED" : "FAILED") + "\n" + string.Join("\n", results) + "\n" + exception + "\n" + string.Join("\n", errors));
        Debug.Log("[Ranged Drone validation] " + (exception == null ? "PASSED " + results.Count + " checks" : exception.ToString()));
        EditorApplication.isPlaying = false;
    }
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        results.Add("PASS " + message); File.WriteAllText(Output + "/report.txt", "RUNNING\n" + string.Join("\n", results));
    }
    private static void Place(Component component, Vector3 position)
    {
        component.transform.position = position;
        if (component.TryGetComponent<Rigidbody>(out var body)) { body.position = position; if (!body.isKinematic) body.linearVelocity = Vector3.zero; }
        Physics.SyncTransforms();
    }
    private static IEnumerator Checks()
    {
        ranged = Object.FindFirstObjectByType<RangedDroneController>(); drone = ranged.GetComponent<DroneController>();
        enemy = ranged.GetComponent<EnemyBase>(); director = ranged.sceneDirector;
        var visual = ranged.GetComponent<DroneVisuals>();
        var normal = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrototypeSetup.PrefabPath);
        var normalDefinition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DronePrototypeSetup.DefinitionPath);
        var normalVisual = normal.GetComponent<DroneVisuals>();
        Check(drone.definition.healthMassEq == normalDefinition.healthMassEq && drone.definition.damageTakenPerSwordHit == normalDefinition.damageTakenPerSwordHit && drone.definition.defeatRewardKey == normalDefinition.defeatRewardKey,
            "Ranged Drone shares normal Drone health, sword damage and central score reward");
        Check(ranged.transform.localScale == normal.transform.localScale && visual.radius == normalVisual.radius && visual.noseLength == normalVisual.noseLength && visual.tailLengthRatio == normalVisual.tailLengthRatio,
            "Canonical Drone scale, six-facet geometry and tail proportions preserved");
        Check(visual.floatingFrontFaces && !normalVisual.floatingFrontFaces && normal.GetComponent<RangedDroneController>() == null,
            "Floating panels and ranged attack are opt-in; melee Drone prefab remains unchanged");
        Check(ranged.GetComponentsInChildren<MonoBehaviour>(true).All(x => x) && ranged.projectilePrefab && ranged.projectilePrefab.volume,
            "Saved enemy and projectile prefab references are valid");
        while (enemy.IsPaused || !MatchScoreService.Instance || !MatchScoreService.Instance.IsScoringOpen) yield return null;
        foreach (var other in Object.FindObjectsByType<EnemyDirector>(FindObjectsSortMode.None)) if (other != director) other.enabled = false;
        player = PlayerControllerScript.ActivePlayers.First(p => !p.IsPseudoPlayer && !p.temporarilyEliminated);
        foreach (var other in PlayerControllerScript.ActivePlayers.ToArray())
        {
            other.SetControlMode(PlayerControlMode.Scripted); other.ClearScriptedInput();
            if (other != player) other.gameObject.SetActive(false);
        }
        while (player.IsMatchInputLocked || player.IsInvulnerable) yield return null;
        drone.idleSpeed = 0f; Place(ranged, Vector3.zero); Place(player, new Vector3(7f, 0f, 0f));
        ranged.ShotFired += shot =>
        {
            if (shots.Count == 0) Check(Vector3.Distance(shot.transform.position, ranged.transform.position) < .001f
                && Mathf.Abs(shot.maxTravelDistance - ranged.firingRange * 1.5f) < .001f,
                "Shots originate in the core with a cutoff of 1.5 times positioning range");
            shots.Add(shot); shotTimes.Add(Time.time - pausedTime);
        };
        float until = Time.time + .7f; while (Time.time < until) yield return null;
        Check(enemy.Director == director && director.AliveCount == 1 && enemy.HealthRemaining == 1f, "Scene enemy registers with shared Director and starts with one health");
        Check(drone.Target == null && ranged.TotalShots == 0 && ranged.Phase == RangedDroneController.AttackPhase.Seeking,
            "No attack or global chase outside detection range");
        Check(Enumerable.Range(0, 12).All(i => visual.FacetProgress(i) > .999f), "Shared point-traced spawn completes all twelve facets");
        var block = new MaterialPropertyBlock(); visual.engineRenderer.GetPropertyBlock(block);
        float idleCoreRadius = block.GetVectorArray("_Balls")[0].w;
        Capture("ranged-idle.png", ranged.transform);
        Place(player, new Vector3(5.5f, 0f, 0f)); Vector3 beforeApproach = ranged.transform.position;
        until = Time.time + .4f; while (Time.time < until) yield return null;
        Check(drone.Target == player && Vector3.Distance(beforeApproach, ranged.transform.position) > .05f && ranged.TotalShots == 0,
            "Detects and approaches a player beyond firing range using shared movement");
        Place(ranged, Vector3.zero); Place(player, new Vector3(3f, 0f, 0f));
        while (ranged.Phase != RangedDroneController.AttackPhase.Charging) yield return null;
        float chargeStart = Time.time - pausedTime - ranged.PhaseAge; Vector3 anchor = ranged.transform.position;
        while (ranged.Charge01 < .65f) yield return null;
        Check(Vector3.Distance(anchor, ranged.transform.position) < .01f && ranged.TotalShots == 0, "Drone remains planted throughout the charge telegraph");
        visual.engineRenderer.GetPropertyBlock(block);
        Check(block.GetVectorArray("_Balls")[0].w > idleCoreRadius * 1.2f, "Metaball core grows visibly while charging");
        Capture("ranged-charging.png", ranged.transform);
        director.enabled = false; float pauseStart = Time.time, phaseAge = ranged.PhaseAge, roll = visual.RollAngle;
        until = Time.time + .25f; while (Time.time < until) yield return null;
        Check(enemy.IsPaused && Mathf.Approximately(phaseAge, ranged.PhaseAge) && Mathf.Approximately(roll, visual.RollAngle), "Director pause freezes charge, spin and animation");
        pausedTime += Time.time - pauseStart; director.enabled = true;
        while (shots.Count == 0) yield return null;
        Check(Mathf.Abs(shotTimes[0] - chargeStart - 2f) < .09f, "First shot follows two active seconds of charge");
        yield return null;
        Check(ranged.ShotPulse > .7f && visual.noseMesh.sharedMesh.vertices.Where((v, i) => i % 3 == 2).Average(v => new Vector2(v.x, v.y).magnitude) > .07f,
            "Front panels pivot around their rear edges to open the mouth per shot");
        Capture("ranged-firing.png", ranged.transform, shots[0].transform);
        var first = shots[0]; Vector3 shotPosition = first.transform.position; float shotAge = first.ActiveAge;
        director.enabled = false; pauseStart = Time.time;
        until = Time.time + .25f; while (Time.time < until) yield return null;
        Check(first && first.IsPaused && Mathf.Approximately(shotAge, first.ActiveAge) && Vector3.Distance(shotPosition, first.transform.position) < .001f,
            "In-flight shots freeze travel and lifetime with match pause");
        pausedTime += Time.time - pauseStart; director.enabled = true;
        float massBeforeBurst = player.massScore;
        while (ranged.Phase != RangedDroneController.AttackPhase.Cooldown) yield return null;
        float cooldownStart = Time.time - pausedTime - ranged.PhaseAge;
        Check(shots.Count == 3 && shotTimes.Skip(1).Select((t, i) => Mathf.Abs(t - shotTimes[i] - .5f)).All(d => d < .08f), "Exactly three shots fire at half-second intervals");
        Check(Mathf.Abs(cooldownStart - shotTimes[0] - 1.5f) < .09f, "Burst phase lasts the full 1.5 seconds");
        Check(Vector3.Distance(anchor, ranged.transform.position) < .01f, "Firing holds position through the complete burst");
        Check(player.massScore < massBeforeBurst && !enemy.IsDead, "Metaball projectiles damage the targeted player without consuming the Drone");
        while (ranged.Phase != RangedDroneController.AttackPhase.Charging) yield return null;
        Check(Mathf.Abs(Time.time - pausedTime - ranged.PhaseAge - cooldownStart - 3f) < .1f && shots.Count == 3,
            "Three-second cooldown completes before another charge begins");
        player.temporarilyEliminated = true; until = Time.time + .1f; while (Time.time < until) yield return null;
        Check(ranged.Phase == RangedDroneController.AttackPhase.Cooldown && ranged.TotalShots == 3 && drone.Target == null,
            "Losing an eligible target cancels charging without a phantom shot");
        player.temporarilyEliminated = false;
        foreach (var shot in Object.FindObjectsByType<RangedDroneProjectile>(FindObjectsSortMode.None)) Object.Destroy(shot.gameObject);
        ranged.enabled = false; Place(player, ranged.transform.position); float contactMass = player.massScore;
        until = Time.time + .15f; while (Time.time < until) yield return null;
        Check(!enemy.IsDead && Mathf.Approximately(contactMass, player.massScore), "Ranged body contact neither explodes nor deals melee damage");
        ranged.enabled = true; drone.enabled = false;
        Check(ranged.Phase == RangedDroneController.AttackPhase.Seeking && ranged.TotalShots == 0 && ranged.ShotPulse == 0f,
            "Re-enable clears the old charge, burst and shot animation state");
        Place(player, new Vector3(3f, 0f, 0f));
        var refinements = ProjectileRefinementChecks(); while (refinements.MoveNext()) yield return refinements.Current;
        var resonance = ResonanceProjectileChecks(); while (resonance.MoveNext()) yield return resonance.Current;
        var projectilePrefab = ranged.projectilePrefab;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Validation thin wall"; wall.layer = 11;
        wall.transform.position = new Vector3(1.5f, 0f, 0f); wall.transform.localScale = new Vector3(.025f, 1f, 1f);
        Physics.SyncTransforms();
        var fastShot = Object.Instantiate(projectilePrefab, new Vector3(.6f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
        fastShot.speed = 100f; fastShot.Init(enemy, Vector3.right); float massAtWall = player.massScore;
        until = Time.time + .15f; while (Time.time < until) yield return null;
        Check(fastShot && fastShot.Phase == RangedDroneProjectile.ShotPhase.Impact && Mathf.Approximately(massAtWall, player.massScore), "Swept projectile splinters at a thin wall without tunnelling into the player");
        Object.Destroy(wall); yield return null;
        var expiring = Object.Instantiate(projectilePrefab, new Vector3(0f, 0f, 1f), Quaternion.identity);
        expiring.lifetimeSeconds = .15f; expiring.Init(enemy, Vector3.forward);
        until = Time.time + .6f; while (Time.time < until) yield return null;
        Check(!expiring, "Missed projectile expires and cleans up its visual volume");
        var swarming = SwarmRefinementChecks(normal); while (swarming.MoveNext()) yield return swarming.Current;
        player.SetScriptedInput(new PlayerInputFrame { shieldDown = true, shieldHeld = true, hasAimDirWS = true, aimDirWS = Vector3.left });
        until = Time.time + .1f; while (Time.time < until) yield return null;
        Check(player.shieldOn, "Real player shield enabled for reflection check");
        var reflected = Object.Instantiate(projectilePrefab, new Vector3(.6f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
        reflected.Init(enemy, Vector3.right); float reflectedMass = player.massScore;
        bool sawReflection = false; until = Time.time + 1.4f;
        string reflectionState = "";
        long scoreBefore = MatchScoreService.Instance.GetTeamScore(player.teamID);
        while (Time.time < until && !enemy.IsDead)
        {
            if (reflected)
            {
                if (reflected.ReflectedBy == player) sawReflection = true;
                reflectionState = $"position={reflected.transform.position} direction={reflected.TravelDirection} reflector={reflected.ReflectedBy}, phase={reflected.Phase}, age={reflected.ActiveAge}, paused={reflected.IsPaused}";
            }
            yield return null;
        }
        Check(sawReflection && enemy.IsDead, $"Real shield reflects the shot into its firing Drone and defeats it (reflected={sawReflection}, dead={enemy.IsDead}, owner={enemy.transform.position}, player={player.transform.position}, shield={player.shieldOn}, {reflectionState})");
        Check(MatchScoreService.Instance.GetTeamScore(player.teamID) > scoreBefore, "Reflected defeat awards the normal Drone reward through central scoring");
        Check(player.massScore >= reflectedMass - .03f, "Shield prevents projectile hit damage");
        player.ClearScriptedInput();
        until = Time.time + .6f; while (Time.time < until) yield return null;
        Check(!ranged && director.AliveCount == 0, "Death animation finishes and Director releases the enemy");
        Check(Object.FindObjectsByType<RangedDroneProjectile>(FindObjectsSortMode.None).Length == 0, "No test projectiles remain after impacts and expiry");
        while (player.shieldOn) yield return null;
        var meleeDrone = Object.Instantiate(normal, player.transform.position, Quaternion.identity).GetComponent<DroneController>();
        meleeDrone.spawnGraceSeconds = 0f; var meleeEnemy = meleeDrone.GetComponent<EnemyBase>(); meleeEnemy.Init(normalDefinition, null);
        float meleeMass = player.massScore;
        until = Time.time + .15f; while (Time.time < until) yield return null;
        Check(meleeEnemy.IsDead && Mathf.Abs(player.massScore - (meleeMass - normalDefinition.damageToPlayerMass01)) < .001f,
            "Ordinary Drone still deals contact damage and explodes after shared-code extension");
        Check(errors.Count == 0, "No runtime Console errors during Ranged Drone validation");
    }
    private static void Capture(string name, Transform subject, Transform extra = null)
    {
        var cameraObject = new GameObject("Ranged validation camera"); var camera = cameraObject.AddComponent<Camera>();
        var rt = new RenderTexture(1000, 1000, 24); var texture = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
        var members = subject.GetComponentsInChildren<Transform>(true).Concat(extra ? extra.GetComponentsInChildren<Transform>(true) : Array.Empty<Transform>()).ToArray();
        var layers = members.Select(t => t.gameObject.layer).ToArray(); var old = RenderTexture.active;
        try
        {
            foreach (var member in members) member.gameObject.layer = 31;
            camera.transform.position = subject.position + new Vector3(2f, 3f, -2f); camera.transform.LookAt(subject.position);
            camera.orthographic = true; camera.orthographicSize = .8f; camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.targetTexture = rt; camera.Render();
            RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0); texture.Apply();
            File.WriteAllBytes(Output + "/" + name, texture.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < members.Length; i++) members[i].gameObject.layer = layers[i];
            RenderTexture.active = old; camera.targetTexture = null; Object.Destroy(cameraObject); Object.Destroy(texture); rt.Release(); Object.Destroy(rt);
        }
    }
}
#endif
