#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Enemies;
using Massive.Player;
using Massive.PowerUps;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static partial class ParticleBeamTurretValidation
{
    private const string Key = "MASSIVE.ParticleBeamTurretValidation", Output = "Library/ParticleBeamTurretValidation";
    private static readonly List<string> results = new(), errors = new();
    private static IEnumerator routine;
    private static double deadline;
    private static int frame;
    private static ParticleBeamTurretController turret;
    private static ParticleBeamTurretVisuals visual;
    private static EnemyBase enemy;
    private static PlayerControllerScript player;
    static ParticleBeamTurretValidation() { EditorApplication.playModeStateChanged += State; }
    [MenuItem("MASSIVE/Enemies/Particle Beam Turret/Validate In Player Actions")]
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
            results.Clear(); errors.Clear(); frame = -1; deadline = EditorApplication.timeSinceStartup + 100;
            Application.logMessageReceived += Log; routine = Checks(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            File.AppendAllText(Output + "/report.txt", "\nReturned to Edit Mode; runtime fixtures discarded.\n");
        }
    }
    private static void Log(string text, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + stack); }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame >= Time.frameCount) return;
        frame = Time.frameCount;
        try
        { if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out at " + (turret ? turret.Phase.ToString() : "setup")); if (!routine.MoveNext()) Finish(null); }
        catch (Exception e) { Finish(e); }
    }
    private static void Finish(Exception e)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Output + "/report.txt", (e == null ? "PASSED" : "FAILED") + "\n" + string.Join("\n", results) + "\n" + e + "\n" + string.Join("\n", errors));
        Debug.Log("[Particle Beam Turret validation] " + (e == null ? "PASSED " + results.Count + " checks" : e.ToString()));
        EditorApplication.isPlaying = false;
    }
    private static void Check(bool passed, string message)
    {
        if (!passed) throw new Exception(message);
        results.Add("PASS " + message); File.WriteAllText(Output + "/report.txt", "RUNNING\n" + string.Join("\n", results));
    }
    private static void Place(Component c, Vector3 p)
    { c.transform.position = p; if (c.TryGetComponent<Rigidbody>(out var rb)) { rb.position = p; if (!rb.isKinematic) rb.linearVelocity = Vector3.zero; } Physics.SyncTransforms(); }
    private static IEnumerator Checks()
    {
        var turrets = Object.FindObjectsByType<ParticleBeamTurretController>(FindObjectsSortMode.None);
        Check(turrets.Length == 2, "Player Actions has one top and one bottom turret");
        turret = turrets.First(t => t.mount == ParticleBeamTurretController.WallMount.Top);
        visual = turret.GetComponent<ParticleBeamTurretVisuals>(); enemy = turret.GetComponent<EnemyBase>();
        var director = turret.sceneDirector;
        foreach (var t in turrets)
        {
            Check(t.GetComponentsInChildren<MonoBehaviour>(true).All(x => x) && t.beamVisual && t.spawnTelegraphPrefab && t.contactPlasma && t.corePlasma,
                t.mount + " prefab references resolve");
            Vector3 up = t.arenaBounds.Current.axisY_WS;
            if (Vector3.Dot(up, Vector3.forward) < 0f) up = -up;
            Vector3 inward = up * (t.mount == ParticleBeamTurretController.WallMount.Top ? -1f : 1f); inward.y = 0;
            Check(Vector3.Dot(t.transform.forward, inward.normalized) > .99f && t.arenaBounds.ContainsWorldPoint(t.BeamOrigin),
                t.mount + " mount faces along the inward wall normal with its core inside the arena");
            Vector3 gridLocal = t.arenaBounds.Grid.transform.InverseTransformPoint(t.transform.position);
            Check(Mathf.Abs(Mathf.Abs(gridLocal.y) - t.arenaBounds.Current.halfSizeLocal.y) < .0001f && t.wallInset == 0f,
                t.mount + " zero-inset baseline lies exactly on the visible grid boundary");
        }
        var points = new Vector3[ParticleBeamTurretGeometry.BaseFaces * 3]; for (int i = 0; i < ParticleBeamTurretGeometry.BaseFaces; i++) ParticleBeamTurretGeometry.BaseTriangle(i, visual.baseRadius, points, i * 3);
        float edge = Vector3.Distance(points[0], points[1]);
        Check(Enumerable.Range(0, points.Length).All(i => Mathf.Abs(Vector3.Distance(points[i], points[i / 3 * 3 + (i + 1) % 3]) - edge) < .0001f),
            "All sixteen base faces are equilateral at the regular antiprism height");
        GeometryTopologyChecks(points);
        Check(turret.GetComponent<Rigidbody>().isKinematic && turret.shellCollider && !turret.shellCollider.isTrigger,
            "Fixed base has a solid shell collider and separate hurtbox");
        while (enemy.IsPaused || !MatchScoreService.Instance || !MatchScoreService.Instance.IsScoringOpen) yield return null;
        foreach (var d in Object.FindObjectsByType<EnemyDirector>(FindObjectsSortMode.None)) if (d != director) d.enabled = false;
        player = PlayerControllerScript.ActivePlayers.First(p => !p.IsPseudoPlayer && !p.temporarilyEliminated);
        foreach (var p in PlayerControllerScript.ActivePlayers.ToArray())
        { p.SetControlMode(PlayerControlMode.Scripted); p.ClearScriptedInput(); if (p != player) p.gameObject.SetActive(false); }
        foreach (var t in turrets) if (t != turret) t.gameObject.SetActive(false);
        while (player.IsMatchInputLocked || player.IsInvulnerable) yield return null;
        turret.mount = ParticleBeamTurretController.WallMount.Authored; turret.transform.rotation = Quaternion.identity;
        // Combat regressions deliberately exercise the optional locked-blast mode;
        // authored moving-fire tuning is covered separately below.
        float authoredFiringSpeed = turret.firingTrackingDegreesPerSecond;
        turret.firingTrackingDegreesPerSecond = 0f;
        turret.GetComponent<Rigidbody>().rotation = Quaternion.identity; Place(turret, Vector3.zero); Place(player, Vector3.back * 3f);
        Check(enemy.Director == director && enemy.HealthRemaining == 3f, "Turret registers through the shared Director with three health");
        while (turret.SpawnAge < .2f) yield return null;
        Check(turret.SpawnWarning && !turret.damageTrigger.enabled, "Arrival uses the shared warning and prevents premature hits");
        director.enabled = false; float age = turret.SpawnAge, ring = visual.RingAngle;
        float until = Time.time + .2f; while (Time.time < until) yield return null;
        Check(Mathf.Approximately(age, turret.SpawnAge) && Mathf.Approximately(ring, visual.RingAngle), "Pause freezes spawn and ring animation");
        director.enabled = true;
        while (!turret.IsReady) yield return null;
        yield return null;
        Check(turret.damageTrigger.enabled && turret.shellCollider.enabled && Enumerable.Range(0,ParticleBeamTurretGeometry.FaceCount).All(i => visual.FacetProgress(i) > .99f),
            "Face tracing completes before collision and combat activate");
        var shapeChecks = ApparatusShapeChecks(); while (shapeChecks.MoveNext()) yield return shapeChecks.Current;
        until = Time.time + .15f; while (Time.time < until) yield return null;
        Check(turret.Target == null && turret.TotalBlasts == 0, "Players behind the mounting wall are not targeted");
        Capture("turret-idle.png", false);
        var block = new MaterialPropertyBlock(); visual.core.GetPropertyBlock(block); float idleRadius = block.GetVectorArray("_Balls")[0].w;
        turret.detectionRange = 4f; Place(player, new Vector3(0,0,5f));
        until = Time.time + .15f; while (Time.time < until) yield return null;
        Check(turret.Target == null, "Detection range prevents distant attacks"); turret.detectionRange = 10f;
        Place(player, new Vector3(1f,0,3f)); Vector3 rootPosition = turret.transform.position; Quaternion rootRotation = turret.transform.rotation;
        while (turret.Phase != ParticleBeamTurretController.AttackPhase.Charging) yield return null;
        Check(Mathf.Abs(turret.AimAngularVelocity) < turret.trackingDegreesPerSecond * .5f,
            "Tracking accelerates from rest instead of jumping directly to its speed limit");
        float chargeStart = Time.time - turret.PhaseAge;
        while (turret.Charge01 < .7f) yield return null;
        Check(turret.Target == player && Vector3.Angle(turret.BeamDirection, player.transform.position - turret.BeamOrigin) < 1f,
            "Firing apparatus pivots and tracks the detected player");
        Check(turret.transform.position == rootPosition && turret.transform.rotation == rootRotation, "Tracking leaves the wall-mounted base fixed");
        visual.core.GetPropertyBlock(block);
        Check(block.GetVectorArray("_Balls")[0].w > idleRadius * 1.2f && !turret.beamVisual.gameObject.activeSelf,
            "Charge grows the core with no damaging beam before firing");
        Check(!turret.corePlasma.IsEmitting && turret.corePlasma.LiveDropCount == 0 && !turret.corePlasma.GetComponent<Renderer>().enabled,
            "Core sparkle stays off before firing");
        Capture("turret-charge.png", false);
        director.enabled = false; age = turret.PhaseAge; ring = visual.RingAngle; float pausedAt = Time.time;
        until = Time.time + .25f; while (Time.time < until) yield return null;
        Check(Mathf.Approximately(age, turret.PhaseAge) && Mathf.Approximately(ring, visual.RingAngle), "Charge, tracking and ring freeze with Director pause");
        chargeStart += Time.time - pausedAt; director.enabled = true;
        float massBefore = player.massScore;
        while (turret.Phase != ParticleBeamTurretController.AttackPhase.Firing) yield return null;
        Check(Mathf.Abs(Time.time - chargeStart - 2f) < .12f, "One blast begins after two active seconds of charge");
        Vector3 locked = turret.BeamDirection;
        while (turret.PhaseAge < .1f) yield return null;
        turret.beamVisual.GetComponent<Renderer>().GetPropertyBlock(block);
        float openingWidth = block.GetVector("_BeamShape").y * turret.beamVisual.VolumeSizeWorld;
        Capture("turret-opening.png", true);
        while (turret.PhaseAge < .4f) yield return null;
        Check(player.massScore < massBefore - .015f && visual.MouthOpening > .9f, "Sustained beam damages the real player while the mouth opens");
        Capture("turret-fire.png", true);
        turret.beamVisual.GetComponent<Renderer>().GetPropertyBlock(block);
        Check(openingWidth > block.GetVector("_BeamShape").y * turret.beamVisual.VolumeSizeWorld * 1.15f,
            "Opening pulse is thicker than the settled sustained beam");
        Check(turret.contactPlasma && turret.contactPlasma.IsEmitting && turret.contactPlasma.LiveDropCount > 0,
            "A real player hit continuously emits pooled contact plasma");
        Check(turret.corePlasma.IsEmitting && turret.corePlasma.LiveDropCount > 0 &&
            Vector3.Distance(turret.corePlasma.ContactPoint, turret.BeamOrigin + turret.BeamDirection * turret.corePlasmaForwardOffset) < .001f,
            "Firing emits sustained sparkle at the core's beam connection");
        Capture("turret-core-fire.png", false);
        director.enabled = false; age = turret.PhaseAge; massBefore = player.massScore;
        float plasmaClock = turret.contactPlasma.AnimationTime; int emitted = turret.contactPlasma.TotalEmitted;
        float coreClock = turret.corePlasma.AnimationTime; int coreEmitted = turret.corePlasma.TotalEmitted;
        turret.beamVisual.GetComponent<Renderer>().GetPropertyBlock(block); Vector4 frozenShape = block.GetVector("_BeamShape");
        Vector4[] frozenStrands = block.GetVectorArray("_PlasmaNodes");
        until = Time.time + .2f; while (Time.time < until) yield return null;
        turret.beamVisual.GetComponent<Renderer>().GetPropertyBlock(block);
        Check(Mathf.Approximately(age, turret.PhaseAge) && Mathf.Approximately(massBefore, player.massScore) && block.GetVector("_BeamShape") == frozenShape
            && frozenStrands.SequenceEqual(block.GetVectorArray("_PlasmaNodes")),
            "Pause freezes the sustained beam, volumetric strands, flicker and damage"); director.enabled = true;
        Check(Mathf.Approximately(plasmaClock, turret.contactPlasma.AnimationTime) && emitted == turret.contactPlasma.TotalEmitted,
            "Pause freezes plasma emission, droplet motion and shrink lifetimes");
        Check(Mathf.Approximately(coreClock, turret.corePlasma.AnimationTime) && coreEmitted == turret.corePlasma.TotalEmitted,
            "Pause also freezes the core sparkle clock and emission");
        Place(player, new Vector3(-2f,0,3f)); until = Time.time + .25f; while (Time.time < until) yield return null;
        Check(Vector3.Angle(locked, turret.BeamDirection) < .001f, "Default blast locks its telegraphed aim so the player can dodge");
        Check(turret.BeamLength <= turret.beamRange && turret.arenaBounds.ContainsWorldPoint(turret.BeamEnd, -.01f), "Missed beam stays within its range and arena boundary");
        Check(turret.corePlasma.IsEmitting && turret.corePlasma.TotalEmitted > coreEmitted,
            "Core sparkle continues when the beam misses its target");
        Capture("turret-long-beam.png", true);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Validation beam blocker";
        wall.transform.position = turret.BeamOrigin + locked * 2f; wall.transform.localScale = new Vector3(.6f, 2f, .15f); Physics.SyncTransforms();
        until = Time.time + .12f; while (Time.time < until) yield return null;
        Check(turret.BeamLength < 2.2f && turret.BeamLength > 1f, "An ordinary wall clips the active beam immediately"); Object.Destroy(wall);
        while (turret.PhaseAge < turret.fireSeconds - turret.beamFadeSeconds - .04f) yield return null;
        float fullLength = turret.BeamLength;
        while (turret.PhaseAge < turret.fireSeconds - turret.beamFadeSeconds * .4f) yield return null;
        Check(turret.BeamLength < fullLength * .6f && !turret.contactPlasma.IsEmitting,
            "Closing beam tip retracts toward the core and leaves no emitting contact at the old endpoint");
        Capture("turret-retract.png", true);
        while (turret.Phase == ParticleBeamTurretController.AttackPhase.Firing) yield return null;
        Check(turret.TotalBlasts == 1 && !turret.beamVisual.gameObject.activeSelf, "Single two-second blast ends cleanly into cooldown");
        float coolStart = Time.time - turret.PhaseAge; massBefore = player.massScore;
        until = Time.time + Mathf.Max(.8f, Mathf.Max(turret.contactPlasma.dropLifetime, turret.corePlasma.dropLifetime) * 1.2f + .04f); while (Time.time < until) yield return null;
        Check(Mathf.Approximately(massBefore, player.massScore), "Cooldown has no residual beam damage");
        Check(turret.contactPlasma.LiveDropCount == 0 && !turret.contactPlasma.GetComponent<Renderer>().enabled,
            "Detached plasma shrinks away completely during cooldown");
        Check(!turret.corePlasma.IsEmitting && turret.corePlasma.LiveDropCount == 0 && !turret.corePlasma.GetComponent<Renderer>().enabled,
            "Core sparkle drains and disables its renderer during cooldown");
        while (turret.Phase == ParticleBeamTurretController.AttackPhase.Cooldown) yield return null;
        Check(Mathf.Abs(Time.time - coolStart - 3f) < .12f, "Cooldown lasts three active seconds");
        while (turret.Phase != ParticleBeamTurretController.AttackPhase.Charging) yield return null;
        Place(player, Vector3.back * 3f);
        until = Time.time + .15f;
        while (turret.Phase == ParticleBeamTurretController.AttackPhase.Charging && Time.time < until) yield return null;
        Check(turret.Phase == ParticleBeamTurretController.AttackPhase.Cooldown && !turret.beamVisual.gameObject.activeSelf,
            "Losing the target during charge cancels without a phantom blast");
        var motionChecks = ApparatusMotionChecks(authoredFiringSpeed); while (motionChecks.MoveNext()) yield return motionChecks.Current;
        turret.chargeSeconds = .3f; turret.fireSeconds = .8f; turret.cooldownSeconds = .2f;
        Place(player, new Vector3(0,0,3f));
        while (turret.Phase != ParticleBeamTurretController.AttackPhase.Charging) yield return null;
        player.SetScriptedInput(new PlayerInputFrame { shieldDown = true, shieldHeld = true, hasAimDirWS = true, aimDirWS = Vector3.back });
        while (turret.Phase != ParticleBeamTurretController.AttackPhase.Firing) yield return null;
        Check(player.shieldOn, "Real held shield activates for beam block check"); massBefore = player.massScore;
        until = Time.time + .3f; while (Time.time < until) yield return null;
        Check(Mathf.Approximately(player.massScore, massBefore) && turret.BeamLength < 3f, "Held shield stops the continuous beam and prevents mass loss");
        turret.enabled = false; yield return null;
        Check(!turret.beamVisual.gameObject.activeSelf, "Disabling the turret immediately removes its beam");
        Check(turret.contactPlasma.LiveDropCount == 0 && !turret.contactPlasma.GetComponent<Renderer>().enabled,
            "Disabling the turret clears the owned contact volume");
        Check(turret.corePlasma.LiveDropCount == 0 && !turret.corePlasma.GetComponent<Renderer>().enabled,
            "Disabling the turret immediately clears the core sparkle");
        player.ClearScriptedInput(); player.GetComponent<PlayerShieldAbility>().ForceStopShield();
        turret.spawnWarningSeconds = 0f; turret.spawnSeconds = .05f; turret.enabled = true;
        while (turret.Phase != ParticleBeamTurretController.AttackPhase.Firing) yield return null;
        Check(turret.TotalBlasts == 1, "Re-enabling starts a fresh cycle with reset counters");
        long score = MatchScoreService.Instance.GetTeamScore(player.teamID);
        enemy.TakeDamage(3f, EnemyDamageSource.Sword, player); yield return null;
        Check(enemy.IsDead && !turret.beamVisual.gameObject.activeSelf && !turret.damageTrigger.enabled, "Death immediately stops firing and disables combat collision");
        Check(!turret.corePlasma.IsEmitting && !turret.corePlasma.GetComponent<Renderer>().enabled,
            "Death clears the core sparkle");
        Check(MatchScoreService.Instance.GetTeamScore(player.teamID) > score, "Turret defeat credits the shared score economy");
        until = Time.time + .3f; while (Time.time < until) yield return null;
        Check(visual.DeathProgress > .25f, "Death explodes and shrinks the individual faces"); Capture("turret-death.png", false);
        until = Time.time + .65f; while (Time.time < until) yield return null;
        Check(!turret, "Defeated turret and owned beam clean up");
        // The power-up keeps its projectile and damage contract while sharing the corrected beam renderer.
        Place(player, new Vector3(0,0,3f)); massBefore = player.massScore;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Power-ups/Particle Accelerator/ParticleAcceleratorProjectile.prefab");
        var pulse = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity).GetComponent<ParticleAcceleratorProjectile>();
        var acceleratorDefinition = AssetDatabase.LoadAssetAtPath<ParticleAcceleratorPowerUpDefinition>("Assets/Power-ups/Particle Accelerator/PU_ParticleAccelerator.asset");
        pulse.Init(null, Vector3.forward, 8f, 8f, .09f, .04f, false, acceleratorDefinition.blockMask, 1f, 6f);
        until = Time.time + 1f; while (Time.time < until) yield return null;
        Check(Mathf.Abs(player.massScore - massBefore + .04f) < .002f && !pulse,
            $"Particle Accelerator still hits once for its configured damage and cleans up (mass loss {massBefore-player.massScore:F3}, alive={(bool)pulse})");
        var beamChecks = BeamRenderChecks(prefab); while (beamChecks.MoveNext()) yield return beamChecks.Current;
        var plasmaChecks = PlasmaRenderChecks(); while (plasmaChecks.MoveNext()) yield return plasmaChecks.Current;
        Check(errors.Count == 0, "No runtime Console errors during turret and beam validation");
    }
    private static IEnumerator BeamRenderChecks(GameObject projectile)
    {
        var beam = Object.Instantiate(projectile.GetComponentInChildren<ParticleAcceleratorBeamVisual>(true).gameObject).GetComponent<ParticleAcceleratorBeamVisual>();
        beam.gameObject.layer = 31;
        Vector3 origin = new Vector3(1000f,0,1000f), middle = origin + Vector3.right * 12f;
        beam.SetSegment(origin, origin + Vector3.right * 24f, .04f, 1f); beam.SetAnimationTime(1f);
        var cameraObject = new GameObject("Beam continuity camera"); var camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = middle + Vector3.up * 30f; camera.transform.LookAt(middle, Vector3.forward);
        camera.orthographic = true; camera.orthographicSize = 4.5f; camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f,.1f,.1f);
        var target = new RenderTexture(1200,400,24); var texture = new Texture2D(1200,400,TextureFormat.RGB24,false);
        var oldActive = RenderTexture.active; camera.targetTexture = target;
        yield return null; yield return null;
        void Read() { camera.Render(); RenderTexture.active = target; texture.ReadPixels(new Rect(0,0,1200,400),0,0); texture.Apply(); }
        bool WhiteAt(Vector3 point)
        {
            Vector3 screen = camera.WorldToScreenPoint(point); int x = Mathf.RoundToInt(screen.x), y = Mathf.RoundToInt(screen.y);
            for(int j=-1;j<=1;j++) for(int i=-1;i<=1;i++) if(texture.GetPixel(Mathf.Clamp(x+i,0,1199),Mathf.Clamp(y+j,0,399)).r > .7f) return true;
            return false;
        }
        GameObject blocker = null; Material material = null;
        try
        {
            Read();
            Check(Enumerable.Range(1,99).All(i => WhiteAt(origin + Vector3.right * (24f * i / 100f))),
                "Rendered 24-unit thin beam stays continuous beyond the sphere-count budget");
            var renderer = beam.GetComponent<Renderer>();
            Check(renderer.bounds.size.y < .8f && renderer.bounds.size.z < .8f,
                "Power-up beam render bounds fit its width instead of a 26-unit cube");
            File.WriteAllBytes(Output + "/beam-continuity.png", texture.EncodeToPNG());
            blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.layer = 31;
            material = new Material(Shader.Find("Unlit/Color")) { color = Color.black }; blocker.GetComponent<Renderer>().sharedMaterial = material;
            blocker.transform.localScale = new Vector3(1f,.08f,1f); blocker.transform.position = middle + Vector3.up * .13f;
            Read();
            Check(!WhiteAt(middle) && WhiteAt(middle + Vector3.right * 2f),
                "Foreground geometry occludes the beam using its real surface depth");
            blocker.transform.position = middle - Vector3.up * .13f; Read();
            Check(WhiteAt(middle), "Geometry behind the beam cannot overwrite its front surface");
            camera.orthographic = false; camera.fieldOfView = 25f; Read();
            Check(WhiteAt(middle), "Shared beam also renders from a perspective camera");
        }
        finally
        {
            RenderTexture.active = oldActive; camera.targetTexture = null; target.Release();
            Object.Destroy(target); Object.Destroy(texture); Object.Destroy(cameraObject); Object.Destroy(beam.gameObject);
            if(blocker) Object.Destroy(blocker); if(material) Object.Destroy(material);
        }
    }
    private static void Capture(string name, bool wide)
    {
        var cameraObject = new GameObject("Turret validation camera"); var camera = cameraObject.AddComponent<Camera>();
        var rt = new RenderTexture(1200, 900, 24); var texture = new Texture2D(1200, 900, TextureFormat.RGB24, false);
        var members = turret.GetComponentsInChildren<Transform>(true); var layers = members.Select(t => t.gameObject.layer).ToArray(); var old = RenderTexture.active;
        try
        {
            foreach (var member in members) member.gameObject.layer = 31;
            Vector3 focus = turret.transform.position + (wide ? turret.BeamDirection * Mathf.Min(3f, turret.BeamLength * .5f) : Vector3.forward * .3f);
            camera.transform.position = focus + (wide ? new Vector3(3,9,-4) : new Vector3(1.8f,2.5f,3.2f)); camera.transform.LookAt(focus);
            camera.orthographic = true; camera.orthographicSize = wide ? 3.5f : 1.1f; camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.018f,.018f,.022f); camera.targetTexture = rt; camera.Render();
            RenderTexture.active = rt; texture.ReadPixels(new Rect(0,0,1200,900),0,0); texture.Apply(); File.WriteAllBytes(Output + "/" + name, texture.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < members.Length; i++) members[i].gameObject.layer = layers[i];
            RenderTexture.active = old; camera.targetTexture = null; Object.Destroy(cameraObject); Object.Destroy(texture); rt.Release(); Object.Destroy(rt);
        }
    }
}
#endif
