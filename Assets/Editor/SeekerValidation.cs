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

[InitializeOnLoad]
public static partial class SeekerValidation
{
    private const string Key = "MASSIVE.SeekerValidation", Output = "Library/SeekerValidation";
    private static readonly List<string> results = new(), errors = new();
    private static IEnumerator routine;
    private static double deadline;
    private static int frame;
    private static SeekerController seeker;
    private static SeekerVisuals visual;
    private static EnemyBase enemy;
    private static PlayerControllerScript player;
    static SeekerValidation() { EditorApplication.playModeStateChanged += State; }
    [MenuItem("MASSIVE/Enemies/Seeker/Validate In Player Actions")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != CarrierPrototypeSetup.ScenePath)
            throw new InvalidOperationException("Run in Player Actions Edit Mode.");
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/report.txt", "RUNNING — waiting for Play Mode\n");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    private static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            results.Clear(); errors.Clear(); frame = -1; deadline = EditorApplication.timeSinceStartup + 210;
            Application.logMessageReceived += Log; routine = Checks(); EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        { SessionState.SetBool(Key, false); File.AppendAllText(Output + "/report.txt", "Returned to Edit Mode; runtime fixtures discarded.\n"); }
    }
    private static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame >= Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout at " + (seeker ? seeker.Phase + " / " + seeker.PhaseAge : "setup"));
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    private static void Finish(Exception e)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/report.txt", (e == null ? "PASSED" : "FAILED") + "\n" + string.Join("\n", results) + "\n" + e + "\n" + string.Join("\n", errors));
        Debug.Log("[Seeker validation] " + (e == null ? "PASSED " + results.Count + " checks" : e.ToString())); EditorApplication.isPlaying = false;
    }
    private static void Check(bool pass, string message)
    {
        if (!pass) throw new Exception(message); results.Add("PASS " + message);
        File.WriteAllText(Output + "/report.txt", "RUNNING\n" + string.Join("\n", results));
    }
    private static void Place(Component c, Vector3 p, Quaternion? rotation = null)
    {
        c.transform.position = p; if (rotation.HasValue) c.transform.rotation = rotation.Value;
        if (c.TryGetComponent<Rigidbody>(out var body))
        { body.position = p; if (rotation.HasValue) body.rotation = rotation.Value; if (!body.isKinematic) body.linearVelocity = Vector3.zero; }
        Physics.SyncTransforms();
    }
    private static IEnumerator Delay(float seconds)
    { float until = Time.time + seconds; while (Time.time < until) yield return null; }
    private static IEnumerator Checks()
    {
        seeker = Object.FindFirstObjectByType<SeekerController>();
        Check(seeker && seeker.definition && seeker.spawnTelegraphPrefab && seeker.launchPlasma && seeker.shellCollider && seeker.damageTrigger,
            "Authored Seeker prefab references resolve");
        visual = seeker.GetComponent<SeekerVisuals>(); enemy = seeker.GetComponent<EnemyBase>();
        float authoredStalk = seeker.stalkSeconds;
        seeker.stalkSeconds = 0f; // The full stalking cycle is exercised separately below.
        foreach (var other in Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None)) if (other != enemy) other.gameObject.SetActive(false);
        var director = seeker.sceneDirector;
        while (enemy.IsPaused || !MatchScoreService.Instance || !MatchScoreService.Instance.IsScoringOpen) yield return null;
        foreach (var d in Object.FindObjectsByType<EnemyDirector>(FindObjectsSortMode.None)) if (d != director) d.enabled = false;
        player = PlayerControllerScript.ActivePlayers.First(p => !p.IsPseudoPlayer && !p.temporarilyEliminated);
        foreach (var p in PlayerControllerScript.ActivePlayers.ToArray())
        { p.SetControlMode(PlayerControlMode.Scripted); p.ClearScriptedInput(); if (p != player) p.gameObject.SetActive(false); }
        while (player.IsMatchInputLocked || player.IsInvulnerable) yield return null;
        seeker.detectionRange = .1f; seeker.idleSpeed = 0f;
        Place(seeker, Vector3.zero, Quaternion.identity); Place(player, new Vector3(0,0,4f));
        Check(enemy.Director == director && enemy.HealthRemaining == seeker.definition.healthMassEq, "Authored enemy registers with shared Director and health");
        while (seeker.SpawnAge < .2f) yield return null;
        Check(seeker.SpawnWarning && !seeker.shellCollider.enabled && !seeker.damageTrigger.enabled, "Spawn warning includes the full shell before combat collision enables");
        while (seeker.SpawnAge < .8f) yield return null;
        Capture("seeker-warning.png");
        director.enabled = false; float age = seeker.SpawnAge, spin = visual.AnimationTime;
        var delay = Delay(.18f); while (delay.MoveNext()) yield return null;
        Check(Mathf.Approximately(age, seeker.SpawnAge) && Mathf.Approximately(spin, visual.AnimationTime), "Director pause freezes arrival and all rotating shell groups");
        director.enabled = true;
        while (!seeker.IsReady) yield return null; yield return null;
        Check(seeker.shellCollider.enabled && seeker.damageTrigger.enabled && Enumerable.Range(0,18).All(i => visual.FacetProgress(i) > .99f),
            "All 18 faces complete their staggered outline reveal before combat");
        GeometryChecks(); Capture("seeker-idle.png"); Capture("seeker-idle-top.png", true);
        Check(seeker.Target == null && seeker.TotalLunges == 0, "Detection range excludes distant players");
        seeker.detectionRange = 7f; Place(player, new Vector3(0,0,3.5f));
        while (seeker.Phase != SeekerController.AttackPhase.Charging) yield return null;
        Vector3 chargePosition = seeker.transform.position; float coreBefore = visual.CoreLength;
        while (seeker.Charge01 < .72f) yield return null; yield return null;
        Check(Vector3.Distance(chargePosition, seeker.transform.position) < .001f && !seeker.IsStrafing, "Charge is stationary and disables attack avoidance");
        Check(visual.RearDisplacement >= visual.chargeStretch * .65f && visual.CoreLength >= coreBefore + visual.chargeStretch * .4f,
            "Charge pulls both rear rings back and visibly stretches the core");
        var chargedPose = new Vector3[SeekerVisuals.FaceCount * 3]; visual.CopyPoseCorners(chargedPose,true);
        var chargedHurt = (CapsuleCollider)seeker.damageTrigger;
        Check(chargedHurt.center.z - chargedHurt.height*.5f <= chargedPose.Min(p=>p.z) + .001f
            && chargedHurt.center.z + chargedHurt.height*.5f >= chargedPose.Max(p=>p.z) - .001f,
            "Sword hurtbox covers the authored nose and extended rear assembly during charge");
        Capture("seeker-charge.png"); Capture("seeker-charge-top.png", true);
        director.enabled = false; age = seeker.PhaseAge; spin = visual.AnimationTime;
        delay = Delay(.2f); while (delay.MoveNext()) yield return null;
        Check(Mathf.Approximately(age, seeker.PhaseAge) && Mathf.Approximately(spin, visual.AnimationTime), "Pause freezes charge, vibration and core stretch");
        director.enabled = true;
        while (seeker.Compression01 < .35f && seeker.Phase == SeekerController.AttackPhase.Charging) yield return null;
        Check(seeker.Phase == SeekerController.AttackPhase.Charging && seeker.Extension01 < .8f, "Rear assembly snaps together before launch");
        Capture("seeker-compression.png");
        float mass = player.massScore;
        while (seeker.TotalLunges == 0) yield return null;
        Check(seeker.launchPlasma.TotalEmitted == seeker.launchDrops && seeker.launchPlasma.LiveDropCount > 0,
            "Full compression emits one bounded burst of rear plasma immediately");
        Capture("seeker-launch.png", true);
        while (seeker.Phase == SeekerController.AttackPhase.Firing) yield return null;
        Check(seeker.Phase == SeekerController.AttackPhase.Hit && seeker.TotalHits == 1
            && Mathf.Abs(player.massScore - mass + seeker.definition.damageToPlayerMass01) < .002f,
            "Fast swept lunge contacts the real player and applies exactly one hit");
        Check(!enemy.IsDead && seeker.DistanceTravelled > 1f && seeker.DistanceTravelled < seeker.lungeDistance,
            "Seeker survives contact and stops at impact");
        while (seeker.Phase != SeekerController.AttackPhase.Cooldown) yield return null;
        Vector3 rest = seeker.transform.position; mass = player.massScore;
        delay = Delay(.25f); while (delay.MoveNext()) yield return null;
        Check(seeker.transform.position == rest && Mathf.Approximately(mass, player.massScore) && !seeker.IsStrafing,
            "Cooldown remains stationary, vulnerable and cannot repeat contact damage");
        // Miss: move sideways only after the aim lock; the full lunge must retain its telegraphed heading.
        Place(seeker, Vector3.zero, Quaternion.identity); Place(player, new Vector3(0,0,3.5f));
        while (seeker.Phase != SeekerController.AttackPhase.Charging) yield return null;
        while (seeker.Charge01 < .9f) yield return null;
        Vector3 locked = seeker.transform.forward; Place(player, new Vector3(3f,0,3.5f)); int lunges = seeker.TotalLunges;
        while (seeker.TotalLunges == lunges) yield return null;
        Check(Vector3.Dot(locked, seeker.LungeDirection) > .999f, "Late sideways player movement cannot redirect a committed lunge");
        enemy.SetMovementInfluence(seeker, .5f); Vector3 at = seeker.transform.position;
        Vector3 exhaustAt = seeker.launchPlasma.ContactPoint;
        float time = Time.fixedTime; while (Time.fixedTime < time + .06f && seeker.Phase == SeekerController.AttackPhase.Firing) yield return null;
        float speed = Vector3.Distance(at, seeker.transform.position) / Mathf.Max(.001f, Time.fixedTime - time);
        Check(speed > 7f && speed < 15f, "Shared movement influence slows the lunge without altering charge timing");
        var exhaustBounds = seeker.launchPlasma.GetComponent<Renderer>().bounds;
        float exhaustDrift = Vector3.Dot(exhaustBounds.center - exhaustAt, seeker.LungeDirection);
        Check(exhaustDrift < .1f && !seeker.launchPlasma.transform.IsChildOf(seeker.transform),
            "Plasma volume stays at the rear burst in world space as its owner lunges away (drift " + exhaustDrift.ToString("F3") + ")");
        enemy.RemoveMovementInfluence(seeker);
        while (seeker.Phase == SeekerController.AttackPhase.Firing) yield return null;
        while (seeker.Phase == SeekerController.AttackPhase.Gliding) yield return null;
        Check(seeker.Phase == SeekerController.AttackPhase.Cooldown && Mathf.Abs(seeker.DistanceTravelled - seeker.lungeDistance) < .01f,
            "Powered thrust ends at maximum travel before the brief miss glide");
        // Shield: use normal input and the real shield collider.
        Place(seeker, Vector3.zero, Quaternion.identity); Place(player, new Vector3(0,0,3.5f));
        while (seeker.Phase != SeekerController.AttackPhase.Charging) yield return null;
        while (seeker.Charge01 < .85f) yield return null;
        player.SetScriptedInput(new PlayerInputFrame { shieldDown = true, shieldHeld = true, hasAimDirWS = true, aimDirWS = Vector3.back });
        yield return null; yield return null; Check(player.shieldOn, "Real player shield activates");
        mass = player.massScore; lunges = seeker.TotalLunges; int hits = seeker.TotalHits;
        while (seeker.TotalLunges == lunges) yield return null;
        while (seeker.Phase == SeekerController.AttackPhase.Firing) yield return null;
        Check(seeker.Phase == SeekerController.AttackPhase.Hit && Mathf.Approximately(player.massScore, mass) && seeker.TotalHits == hits,
            "Shield stops a lunge without mass damage"); player.ClearScriptedInput();
        Check(!seeker.LastAttackHitPlayer && seeker.CurrentCooldownSeconds == seeker.cooldownSeconds, "Blocked hits retain the longer cooldown");
        // Insert a thin wall after aim lock, so a single fast physics step crosses its thickness.
        Place(seeker, Vector3.zero, Quaternion.identity); Place(player, new Vector3(0,0,3.5f));
        while (seeker.Phase != SeekerController.AttackPhase.Charging) yield return null;
        while (seeker.Charge01 < .85f) yield return null;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Seeker sweep validation wall";
        wall.transform.position = new Vector3(0,0,1.5f); wall.transform.localScale = new Vector3(2f,2f,.03f); Physics.SyncTransforms();
        lunges = seeker.TotalLunges; mass = player.massScore;
        while (seeker.TotalLunges == lunges) yield return null;
        while (seeker.Phase == SeekerController.AttackPhase.Firing) yield return null;
        Check(seeker.Phase == SeekerController.AttackPhase.Hit && seeker.transform.position.z < 1f && Mathf.Approximately(mass, player.massScore),
            "Swept hull stops at a thin obstacle without tunnelling or damage through it");
        Vector3 wallHit = seeker.transform.position;
        while (seeker.Phase == SeekerController.AttackPhase.Hit) yield return null;
        Check(Vector3.Dot(wallHit - seeker.transform.position, seeker.LungeDirection) > .1f, "Obstacle contact produces a swept backward bounce");
        Object.Destroy(wall);
        // Lunge toward the arena edge, then dodge along the wall after aim locks.
        seeker.arenaBounds.RefreshNow(false);
        Vector3 edge = seeker.arenaBounds.ClampWorldPointInside(Vector3.forward * 100f, .8f); edge.y = 0f;
        Place(seeker, edge - Vector3.forward * 2.5f, Quaternion.identity); Place(player, edge);
        while (seeker.Phase != SeekerController.AttackPhase.Charging) yield return null;
        while (seeker.Charge01 < .9f) yield return null;
        Place(player, edge + Vector3.right * 3f); lunges = seeker.TotalLunges;
        while (seeker.TotalLunges == lunges) yield return null;
        while (seeker.Phase == SeekerController.AttackPhase.Firing) yield return null;
        Check(seeker.Phase == SeekerController.AttackPhase.Hit && seeker.arenaBounds.ContainsWorldPoint(seeker.transform.position, .65f),
            "A missed outward lunge stops with its entire collision hull inside the arena boundary");
        // A charge can be interrupted by target loss without firing.
        Place(seeker, Vector3.zero, Quaternion.identity); Place(player, new Vector3(0,0,3.5f));
        while (seeker.Phase != SeekerController.AttackPhase.Charging) yield return null;
        lunges = seeker.TotalLunges; player.gameObject.SetActive(false);
        delay = Delay(.1f); while (delay.MoveNext()) yield return null;
        Check(seeker.Phase == SeekerController.AttackPhase.Cooldown && seeker.TotalLunges == lunges,
            "Target loss cancels charge safely into cooldown (phase " + seeker.Phase + ", paused " + enemy.IsPaused + ")");
        player.gameObject.SetActive(true); player.SetControlMode(PlayerControlMode.Scripted); player.ClearScriptedInput();
        var polishChecks = PolishChecks(); while (polishChecks.MoveNext()) yield return null;
        seeker.stalkSeconds = 0f;
        var avoidanceChecks = AvoidanceChecks(); while (avoidanceChecks.MoveNext()) yield return null;
        seeker.stalkSeconds = authoredStalk;
        // Reuse the turret pool in sustained mode after a one-shot burst.
        var plasma = seeker.launchPlasma; enemy.Pause(true); plasma.Clear();
        plasma.SetContact(Vector3.zero, Vector3.back, 1f); plasma.Advance(.1f);
        Check(plasma.IsEmitting && plasma.TotalEmitted > 0, "Shared plasma still emits continuously for the turret");
        plasma.StopEmission(); for (int i = 0; i < 15; i++) plasma.Advance(.1f);
        Check(plasma.LiveDropCount == 0 && !plasma.GetComponent<Renderer>().enabled, "Rear exhaust and sustained plasma fully shrink and retire"); enemy.Pause(false);
        long score = MatchScoreService.Instance.GetTeamScore(player.teamID);
        enemy.TakeDamage(enemy.HealthRemaining, EnemyDamageSource.Sword, player); yield return null;
        Check(enemy.IsDead && !seeker.damageTrigger.enabled && !seeker.shellCollider.enabled, "Death immediately disables lunge collision");
        Check(MatchScoreService.Instance.GetTeamScore(player.teamID) > score, "Seeker defeat awards central match score");
        delay = Delay(.3f); while (delay.MoveNext()) yield return null;
        Check(visual.DeathProgress > .25f, "Death shatters and shrinks individual shell triangles"); Capture("seeker-death.png");
        var ownedExhaust = seeker.launchPlasma;
        while (enemy) yield return null; yield return null;
        Check(!ownedExhaust, "Detached launch exhaust is destroyed with its owning Seeker");
        Check(errors.Count == 0, "No runtime Console errors during Seeker validation");
    }
    private static IEnumerator AvoidanceChecks()
    {
        var fixtureDefinition = Object.Instantiate(seeker.definition); fixtureDefinition.healthMassEq = 20f;
        enemy.Init(fixtureDefinition, seeker.sceneDirector);
        seeker.attackRange = .1f; seeker.avoidanceChance = 1f; seeker.avoidanceReactionSeconds = .02f;
        seeker.avoidanceRange = 6f; seeker.avoidanceConeDegrees = 65f;
        while (seeker.Phase != SeekerController.AttackPhase.Seeking) yield return null;
        Place(seeker, new Vector3(0,0,3.8f), Quaternion.Euler(0,180,0)); Place(player, Vector3.zero);
        var delay = Delay(.2f); while (delay.MoveNext()) yield return null;
        int dodges = seeker.TotalDodges;
        player.SetScriptedInput(new PlayerInputFrame { attackDown = true, attackHeld = true, hasAimDirWS = true, aimDirWS = Vector3.forward });
        float until = Time.time + 1f;
        while (seeker.TotalDodges == dodges && Time.time < until) yield return null;
        Check(seeker.TotalDodges > dodges && seeker.IsStrafing, "A real directed player attack triggers a lateral dodge at 100 percent test chance");
        Vector3 before = seeker.transform.position;
        delay = Delay(.1f); while (delay.MoveNext()) yield return null;
        Check(Mathf.Abs(seeker.transform.position.x - before.x) > .04f, "Avoidance moves laterally out of the attack line");
        player.ClearScriptedInput();
        while (player.attackController.IsAttacking) yield return null;
        seeker.avoidanceChance = 0f; delay = Delay(1f); while (delay.MoveNext()) yield return null;
        Place(seeker, new Vector3(0,0,3.8f), Quaternion.Euler(0,180,0)); Place(player, Vector3.zero);
        dodges = seeker.TotalDodges;
        player.SetScriptedInput(new PlayerInputFrame { attackDown = true, attackHeld = true, hasAimDirWS = true, aimDirWS = Vector3.forward });
        delay = Delay(.2f); while (delay.MoveNext()) yield return null;
        Check(seeker.TotalDodges == dodges, "Zero avoidance chance disables dodges rather than rerolling every frame"); player.ClearScriptedInput();
    }
    private static void GeometryChecks()
    {
        var a = new Vector3[54]; var b = new Vector3[54]; visual.CopyPoseCorners(a);
        Check(Enumerable.Range(0,6).All(i => (a[i*3+2] - Vector3.forward*(visual.noseOffset+visual.noseLength)).sqrMagnitude < .000001f),
            "Nose is a six-sided pyramid with one shared apex");
        visual.noseOffset += .2f; visual.CopyPoseCorners(b); visual.noseOffset -= .2f;
        Check(Enumerable.Range(0,18).All(i => Vector3.Distance(b[i]-a[i], Vector3.forward*.2f) < .00001f), "Nose offset translates its complete shape without changing radius");
        visual.largeRingOffset += .2f; visual.CopyPoseCorners(b); visual.largeRingOffset -= .2f;
        Check(Enumerable.Range(18,18).All(i => Vector3.Distance(b[i]-a[i], Vector3.back*.2f) < .00001f), "Large rear offset only moves that ring along the attack axis");
        visual.smallRingOffset += .2f; visual.CopyPoseCorners(b); visual.smallRingOffset -= .2f;
        Check(Enumerable.Range(36,18).All(i => Vector3.Distance(b[i]-a[i], Vector3.back*.2f) < .00001f), "Small rear offset is independent of both other groups");
        Check(visual.largeRingSpinSpeed * visual.noseSpinSpeed < 0f && visual.smallRingSpinSpeed * visual.largeRingSpinSpeed < 0f
            && visual.smallRingRadius < visual.largeRingRadius && visual.smallPanelLength < visual.largePanelLength,
            "Three shell groups alternate spin direction with a smaller backward-facing tail ring");
        Mesh outline = visual.CreateSpawnOutline(); Check(outline.vertexCount == 216, "Arrival outline contains all 54 triangle edges"); Object.Destroy(outline);
    }
    private static void Capture(string name, bool top = false, bool wide = false)
    {
        var go = new GameObject("Seeker validation camera"); var camera = go.AddComponent<Camera>();
        var rt = new RenderTexture(1200,900,24); var texture = new Texture2D(1200,900,TextureFormat.RGB24,false);
        var members = seeker.GetComponentsInChildren<Transform>(true).Concat(seeker.launchPlasma.GetComponentsInChildren<Transform>(true)).Distinct().ToArray();
        var layers = members.Select(t => t.gameObject.layer).ToArray(); var old = RenderTexture.active;
        try
        {
            foreach (var t in members) t.gameObject.layer = 31;
            Vector3 focus = seeker.transform.position - seeker.transform.forward * (wide ? 1.5f : .2f);
            camera.transform.position = focus + (top ? Vector3.up * 4f : new Vector3(2.7f,2f,2.4f));
            camera.transform.LookAt(focus, top ? Vector3.forward : Vector3.up);
            camera.orthographic = true; camera.orthographicSize = wide ? 2.8f : 1.3f; camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f,.025f,.03f);
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0,0,1200,900),0,0); texture.Apply(); File.WriteAllBytes(Output + "/" + name, texture.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < members.Length; i++) if (members[i]) members[i].gameObject.layer = layers[i];
            RenderTexture.active = old; camera.targetTexture = null; Object.Destroy(go); Object.Destroy(texture); rt.Release(); Object.Destroy(rt);
        }
    }
}
#endif
