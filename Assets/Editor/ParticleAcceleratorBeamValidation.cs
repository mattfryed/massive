#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Demonstrations;
using Massive.Enemies;
using Massive.Player;
using Massive.PowerUps;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ParticleAcceleratorBeamValidation
{
    const string Key = "MASSIVE.AcceleratorBeamValidation", Output = "Library/ParticleAcceleratorBeamValidation";
    const string EnemiesOnlyKey = Key + ".EnemiesOnly";
    const string MovementOnlyKey = Key + ".MovementOnly";
    const string RefinementsKey = Key + ".Refinements";
    static string ReportPath => Output + (SessionState.GetBool(RefinementsKey, false) ? "/refinements-report.txt" :
        SessionState.GetBool(MovementOnlyKey, false) ? "/movement-report.txt" :
        SessionState.GetBool(EnemiesOnlyKey, false) ? "/enemy-report.txt" : "/report.txt");
    static readonly List<string> checks = new(), errors = new();
    static IEnumerator routine;
    static int frame;
    static double deadline;
    static GameObject scope;
    static ParticleAcceleratorPowerUpDefinition settings;
    static ParticleAcceleratorBeamValidation() { EditorApplication.playModeStateChanged += State; }
    [MenuItem("MASSIVE/Power-ups/Validate Sustained Particle Accelerator")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Use Edit Mode.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != PlayerActionTestArenaSetup.ScenePath)
            throw new Exception("Open PLAYER ACTIONS before running beam validation.");
        ParticleAcceleratorBeamSetup.EnsureInstalled();
        SessionState.SetBool(RefinementsKey, false);
        SessionState.SetBool(MovementOnlyKey, false);
        SessionState.SetBool(EnemiesOnlyKey, false);
        Directory.CreateDirectory(Output); File.WriteAllText(ReportPath, "STARTING\n");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    [MenuItem("MASSIVE/Power-ups/Validate Particle Accelerator Enemy Damage")]
    public static void RunEnemyDamage()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new Exception("Use Edit Mode after compilation.");
        SessionState.SetBool(RefinementsKey, false);
        SessionState.SetBool(MovementOnlyKey, false);
        SessionState.SetBool(EnemiesOnlyKey, true);
        Directory.CreateDirectory(Output); File.WriteAllText(ReportPath, "STARTING\n");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    [MenuItem("MASSIVE/Power-ups/Validate Particle Accelerator Firing Movement")]
    public static void RunFiringMovement()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new Exception("Use Edit Mode after compilation.");
        SessionState.SetBool(RefinementsKey, false);
        SessionState.SetBool(MovementOnlyKey, true); SessionState.SetBool(EnemiesOnlyKey, false);
        Directory.CreateDirectory(Output); File.WriteAllText(ReportPath, "STARTING\n");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    [MenuItem("MASSIVE/Power-ups/Validate Particle Accelerator Refinements")]
    public static void RunRefinements()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new Exception("Use Edit Mode after compilation.");
        SessionState.SetBool(RefinementsKey, true);
        Directory.CreateDirectory(Output); File.WriteAllText(ReportPath, "STARTING\n");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); frame = -1; deadline = EditorApplication.timeSinceStartup + 150;
            routine = SessionState.GetBool(RefinementsKey, false) ? RefinementChecks() :
                SessionState.GetBool(MovementOnlyKey, false) ? MovementChecks() :
                SessionState.GetBool(EnemiesOnlyKey, false) ? EnemyChecks() : Checks();
            Application.logMessageReceived += Log; EditorApplication.update += Tick;
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
        { if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out."); if (!routine.MoveNext()) Finish(null); }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception failure)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        if (scope) Object.Destroy(scope); if (settings) Object.Destroy(settings);
        bool pass = failure == null && errors.Count == 0;
        File.WriteAllText(ReportPath, (pass ? "PASSED" : "FAILED") + "\n" + string.Join("\n", checks) + "\n" + failure + "\n" + string.Join("\n", errors));
        Debug.Log("PARTICLE ACCELERATOR VALIDATION " + (pass ? "PASSED " + checks.Count : "FAILED " + failure));
        EditorApplication.isPlaying = false;
    }
    static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        checks.Add("PASS " + label); File.WriteAllText(ReportPath, "RUNNING\n" + string.Join("\n", checks));
    }
    static PlayerControllerScript Actor(int slot, Vector3 local)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), scope.transform);
        go.transform.localPosition = local;
        var p = go.GetComponent<PlayerControllerScript>(); p.ConfigureDemonstration(scope.transform, slot, slot + 1);
        p.GetComponent<PlayerScaleAdjuster>().ApplyScale(); return p;
    }
    static void Place(PlayerControllerScript p, Vector3 position)
    {
        p.transform.position = position; p.GetComponent<Rigidbody>().position = position;
        if (p.visualsController && p.visualsController.visuals) p.visualsController.visuals.position = position;
        Physics.SyncTransforms();
    }
    static void Input(PlayerControllerScript p, bool down, bool held, bool up = false)
    {
        p.SetScriptedInput(new PlayerInputFrame { hasAimDirWS = true, aimDirWS = Vector3.right,
            attackDown = down, attackHeld = held, attackUp = up });
    }
    static IEnumerator Checks()
    {
        var trail = new ParticleBeamAimTrail(); trail.Reset(Vector3.forward);
        for (int n = 0; n < 30; n++) trail.Advance(.02f, Vector3.forward, 20, 40, .4f, 40);
        Check(Mathf.Abs(trail.OffsetAt(20)) < .0001f, "Steady aim is straight");
        trail.Advance(.02f, Quaternion.Euler(0, 25, 0) * Vector3.forward, 20, 40, .4f, 40);
        Check(Mathf.Abs(trail.OffsetAt(1)) < Mathf.Abs(trail.OffsetAt(15)) * .1f && trail.OffsetAt(15) < -1,
            "Near muzzle responds first; distant beam retains previous heading");
        Check(trail.OffsetAt(0) == 0, "Arc stays anchored at the muzzle");
        for (int n = 0; n < 30; n++) trail.Advance(.02f, Quaternion.Euler(0, 25, 0) * Vector3.forward, 20, 40, .4f, 40);
        Check(Mathf.Abs(trail.OffsetAt(20)) < .001f, "Beam straightens after turn history catches up");
        trail.Advance(.02f, Vector3.left, 20, 40, 0, 40);
        Check(Mathf.Abs(trail.OffsetAt(20)) < .001f, "Zero delay disables arcing");
        trail.Reset(Quaternion.Euler(0, 179, 0) * Vector3.forward);
        trail.Advance(.02f, Quaternion.Euler(0, -179, 0) * Vector3.forward, 20, 40, .4f, 40);
        Check(Mathf.Abs(trail.OffsetAt(20)) < 1, "Heading wrap does not produce a full-turn arc");
        for (int n = 0; n < 180; n++) trail.Advance(1f / 240, Quaternion.Euler(0, n * .2f, 0) * Vector3.forward, 20, 40, .4f, 40);
        Check(Mathf.Abs(trail.OffsetAt(20)) > 1, "Aim history also propagates above 120 FPS");

        settings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ParticleAcceleratorPowerUpDefinition>(ParticleAcceleratorBeamSetup.DefinitionPath));
        Check(settings.sustainedBeamPrefab && settings.sustainedBeamPrefab.beamVisual.plasmaLayers &&
            settings.sustainedBeamPrefab.corePlasma && settings.sustainedBeamPrefab.contactPlasma,
            "Shared definition references the turret-derived plasma rig");
        scope = new GameObject("Accelerator isolated validation"); scope.SetActive(false); scope.transform.position = Vector3.right * 3000;
        var owner = Actor(0, Vector3.zero); var victim = Actor(2, Vector3.right * 6);
        scope.SetActive(true); yield return null; yield return null;
        owner.enabled = victim.enabled = false; owner.powerUps.enabled = victim.powerUps.enabled = false;
        owner.GetComponent<Rigidbody>().isKinematic = victim.GetComponent<Rigidbody>().isKinematic = true;
        var beam = Object.Instantiate(settings.sustainedBeamPrefab, scope.transform); beam.Initialize(owner, settings);
        Vector3 origin = scope.transform.position + Vector3.right + Vector3.up * .2f;
        beam.Begin(origin, Vector3.right);
        float initialMass = victim.massScore;
        for (int n = 0; n < 15; n++) beam.Advance(.02f, origin, Vector3.right);
        Check(beam.Contact && beam.Contact.GetComponentInParent<PlayerControllerScript>() == victim && victim.massScore < initialMass,
            "Sustained beam collides with and continuously damages the intended player");
        Check(beam.corePlasma.IsEmitting && beam.contactPlasma.IsEmitting && beam.contactPlasma.TotalEmitted > 0,
            "Turret emitter and impact plasma emit at beam contact");
        float mass = victim.massScore, clock = beam.AnimationTime;
        beam.Advance(0, origin, Vector3.right);
        Check(beam.AnimationTime == clock && victim.massScore == mass, "Zero gameplay delta freezes visuals and damage");
        beam.Stop(); beam.Advance(.04f, origin, Vector3.right);
        Check(!beam.IsFiring && !beam.contactPlasma.IsEmitting && victim.massScore == mass && beam.BeamLength > 0,
            "Release stops damage and contact emission immediately while retracting visuals");
        beam.Advance(settings.beamFadeSeconds + 1, origin, Vector3.right);
        Check(beam.BeamLength == 0 && !beam.beamVisual.gameObject.activeSelf, "Release completes the turret-style fade");

        Place(victim, scope.transform.position + Vector3.forward * 15);
        beam.Begin(origin, Vector3.right);
        for (int n = 0; n < 30; n++) beam.Advance(.02f, origin, Vector3.right);
        Vector3 turn = Quaternion.Euler(0, -25, 0) * Vector3.right;
        for (int n = 1; n <= 12; n++) beam.Advance(.02f, origin, Quaternion.Euler(0, -25f * n / 12, 0) * Vector3.right);
        yield return null;
        Capture(beam, "turn-propagation.png", scope.transform.position);
        float range = settings.maxDistance * PlayerScaleAdjuster.ProjectileReachOf(owner);
        beam.beamVisual.SampleCollisionPath(origin, turn, range, 8, settings.beamRadius * PlayerScaleAdjuster.SizeOf(owner), beam.AnimationTime,
            out var arcPoint, out _);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Arc collision fixture";
        wall.transform.SetParent(scope.transform); wall.transform.position = arcPoint; wall.transform.localScale = Vector3.one * .5f;
        Physics.SyncTransforms(); beam.Advance(.001f, origin, turn);
        Check(beam.Contact == wall.GetComponent<Collider>() && beam.BeamLength < 8.5f,
            "Off-axis obstacle clips the curved beam and its actual hitbox");
        Check(Vector3.Distance(beam.contactPlasma.ContactPoint, wall.GetComponent<Collider>().ClosestPoint(beam.contactPlasma.ContactPoint)) < .01f,
            "Impact plasma is placed on the curved path's actual contact surface");
        wall.SetActive(false);
        var otherScope = new GameObject("Other simulation"); otherScope.transform.SetParent(scope.transform);
        victim.ConfigureDemonstration(otherScope.transform, 2, 3); Place(victim, origin + turn * 4);
        beam.Begin(origin, turn); beam.Advance(.5f, origin, turn);
        Check(!beam.Contact, "Player from a different simulation cannot block or receive beam damage");
        Object.Destroy(beam.gameObject); victim.gameObject.SetActive(false);
        yield return null;

        owner.enabled = true; owner.powerUps.enabled = true;
        Check(settings.minimumBurstSeconds == 1f, "Shared default tap duration is one second");
        owner.powerUps.Equip(settings, float.PositiveInfinity);
        var meter = owner.GetComponentInChildren<ParticleAcceleratorVFXModule>(true);
        Check(owner.powerUps.ParticleAcceleratorEnergy01 == 1 && meter && meter.IsEnergyMeterVisible && meter.EnergyMeter01 == 1,
            "Acquisition immediately fills and displays the energy ring");
        int shots = 0; ParticleAcceleratorBeam live = null;
        owner.powerUps.ProjectileFired += go => { shots++; live = go.GetComponent<ParticleAcceleratorBeam>(); };
        Input(owner, true, true); yield return null; Input(owner, false, true);
        yield return null;
        Check(shots == 1 && live && live.IsFiring, "First Sword press fires immediately without a charging phase");
        float until = Time.time + .08f; while (Time.time < until) yield return null;
        Input(owner, false, false, true); yield return null; Input(owner, false, false);
        Check(live.IsFiring && owner.powerUps.ParticleAcceleratorEnergy01 < 1,
            "A released tap keeps firing and draining during its minimum burst");
        owner.SetMatchInputLocked(true); clock = live.AnimationTime;
        float pausedEnergy = owner.powerUps.ParticleAcceleratorEnergy01;
        until = Time.time + .1f; while (Time.time < until) yield return null;
        Check(live.AnimationTime == clock && owner.powerUps.ParticleAcceleratorEnergy01 == pausedEnergy,
            "Match pause freezes the beam, minimum burst and meter clocks");
        owner.SetMatchInputLocked(false);
        until = Time.time + 2; while (live.IsFiring && Time.time < until) yield return null;
        Check(!live.IsFiring && live.AnimationTime >= .999f && live.AnimationTime < 1.15f + Time.deltaTime * 3,
            "Tap stops after one second of actual firing time");
        Check(owner.powerUps.CooldownRemaining == 0, "The meter replaces the old post-shot cooldown");
        float energyBefore = owner.powerUps.ParticleAcceleratorEnergy01;
        until = Time.time + .2f; while (Time.time < until) yield return null;
        Check(owner.powerUps.ParticleAcceleratorEnergy01 > energyBefore &&
            Mathf.Abs(meter.EnergyMeter01 - owner.powerUps.ParticleAcceleratorEnergy01) < .00001f,
            "Idle refill starts immediately and the visible ring matches available energy");

        // Shorter capacity exercises exhaustion quickly without modifying the shared asset.
        settings.fullMeterFireSeconds = 1.5f;
        Input(owner, true, true); yield return null; Input(owner, false, true);
        until = Time.time + .3f; while (Time.time < until) yield return null;
        Check(shots == 2 && live.IsFiring && owner.powerUps.ParticleAcceleratorEnergy01 < energyBefore,
            "Held fire sustains continuously and drains the meter");
        Capture(live, "energy-ring-firing.png", scope.transform.position);
        until = Time.time + 2; while (live.IsFiring && Time.time < until) yield return null;
        Check(!live.IsFiring && owner.powerUps.ParticleAcceleratorEnergy01 < .25f,
            "Exhausting available energy stops a held beam");
        energyBefore = owner.powerUps.ParticleAcceleratorEnergy01;
        until = Time.time + .2f; while (Time.time < until) yield return null;
        Check(shots == 2 && !live.IsFiring && owner.powerUps.ParticleAcceleratorEnergy01 > energyBefore,
            "Holding after exhaustion permits refill without automatic repeated shots");
        Input(owner, false, false, true); yield return null; Input(owner, false, false);
        float partialEnergy = owner.powerUps.ParticleAcceleratorEnergy01;
        Check(partialEnergy > 0 && partialEnergy * settings.fullMeterFireSeconds < settings.minimumBurstSeconds,
            "Partial-charge fixture has less than a minimum burst available");
        Input(owner, true, true); yield return null; Input(owner, false, false, true);
        Check(shots == 3, "A fresh press fires with partial energy and no full-charge threshold");
        until = Time.time + 1; while (live.IsFiring && Time.time < until) yield return null;
        Check(!live.IsFiring && live.AnimationTime < .7f && owner.powerUps.ParticleAcceleratorEnergy01 < .25f,
            "Low energy caps a tap instead of granting a free one-second burst");
        Input(owner, false, false);
        until = Time.time + settings.emptyToFullRefillSeconds + .2f;
        while (owner.powerUps.ParticleAcceleratorEnergy01 < 1 && Time.time < until) yield return null;
        Check(owner.powerUps.ParticleAcceleratorEnergy01 == 1 && meter.EnergyMeter01 == 1,
            "Idle meter refills to full and clamps at full capacity");
        Input(owner, true, true); yield return null; Input(owner, false, true);
        until = Time.time + 1.1f; while (Time.time < until) yield return null;
        Check(live.IsFiring, "Holding beyond the minimum burst continues firing");
        Input(owner, false, false, true); yield return null; yield return null;
        Check(!live.IsFiring && owner.powerUps.ParticleAcceleratorEnergy01 > 0,
            "Release after the minimum stops firing with remaining energy");
        owner.powerUps.Clear(); yield return null;
        Check(!live && !meter.IsEnergyMeterVisible && owner.powerUps.MovementMultiplierWhileCharging == 1,
            "Unequip hides the ring, destroys beam effects and clears movement modifiers");
        owner.powerUps.Equip(settings, float.PositiveInfinity);
        Check(owner.powerUps.ParticleAcceleratorEnergy01 == 1, "Reacquiring the power-up restores full energy");
        owner.powerUps.Clear();

        var lab = Object.FindFirstObjectByType<PlayerActionTestArena>();
        lab.SetMode(PlayerActionTestArena.TestMode.AllDemos);
        var gallery = Object.FindFirstObjectByType<PlayerDemoGallery>();
        until = Time.time + 20; while (!gallery.IsPlaying && Time.time < until) yield return null;
        var demos = Object.FindObjectsByType<PlayerDemoDirector>(FindObjectsSortMode.None)
            .Where(d => d.Scenario && d.Scenario.powerUp is ParticleAcceleratorPowerUpDefinition).ToArray();
        Check(demos.Length > 0, "Player Actions contains accelerator demonstrations");
        until = Time.time + 55;
        while (demos.Any(d => d.SuccessfulLoops == 0 && d.FailedLoops == 0) && Time.time < until) yield return null;
        foreach (var demo in demos)
            Check(demo.SuccessfulLoops > 0 && demo.FailedLoops == 0, demo.name + " completes with sustained plasma: " + demo.LastFailure);
        Check(errors.Count == 0, "No runtime Console errors");
    }

    static IEnumerator RefinementChecks()
    {
        settings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ParticleAcceleratorPowerUpDefinition>(ParticleAcceleratorBeamSetup.DefinitionPath));
        settings.movementWhileFiring = 0; settings.turningWhileFiring = .5f;
        settings.aimDirectionEaseSeconds = .25f; settings.minimumBurstSeconds = .1f;
        settings.fullMeterFireSeconds = 6; settings.maxTurnDelay = 0;
        scope = new GameObject("Accelerator refinement validation"); scope.SetActive(false);
        scope.transform.position = Vector3.right * 3000;
        var owner = Actor(0, Vector3.zero);
        scope.SetActive(true); yield return null; yield return null;
        owner.visualsController.maxYawSpeed = 120;
        // A physics pickup can equip before the same rendered frame routes its Sword press.
        Action<PlayerInputFrame> acquire = null;
        acquire = input => { if (!input.attackDown) return; owner.InputApplied -= acquire; owner.powerUps.Equip(settings, float.PositiveInfinity); };
        owner.InputApplied += acquire;
        Input(owner, true, true); yield return null; yield return null;
        Check(owner.powerUps.HasActive && !owner.powerUps.HasMovementAction,
            "The attack that acquires the accelerator cannot fire it");
        float until = Time.time + .15f; while (Time.time < until) yield return null;
        Check(!owner.powerUps.HasMovementAction && owner.powerUps.ParticleAcceleratorEnergy01 == 1,
            "Holding the pickup attack does not fire or spend energy");
        var meter = owner.GetComponentInChildren<ParticleAcceleratorVFXModule>(true);
        var live = scope.GetComponentInChildren<ParticleAcceleratorBeam>(true);
        var guide = live.AimGuide;
        Check(guide && guide.IsVisible && guide.Length > 10 && meter.IsEnergyMeterVisible,
            "Equipped idle player shows a full energy ring and a dotted aim guide");
        CaptureView(guide.Origin + guide.Direction * guide.Length, "idle-guide.png", owner.transform.position);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Guide blocker";
        wall.transform.SetParent(scope.transform); wall.transform.localPosition = new Vector3(6, 0, 0);
        wall.transform.localScale = new Vector3(1, 3, 3); Physics.SyncTransforms();
        yield return null; yield return null;
        Check(guide.Length > 1 && guide.Length < 6, "Idle guide clips to the first solid obstacle");
        CaptureView(guide.Origin + guide.Direction * guide.Length, "clipped-guide.png", owner.transform.position);
        wall.SetActive(false);
        owner.SetScriptedInput(new PlayerInputFrame { hasAimDirWS = true, aimDirWS = Vector3.forward, attackUp = true });
        yield return null; yield return null;
        Check(Vector3.Angle(guide.Direction, Vector3.forward) < .1f && guide.Length > 10,
            "Idle guide tracks requested aim without firing turn restrictions");
        Input(owner, true, true); yield return null; Input(owner, false, true); yield return null;
        Check(live.IsFiring && !guide.IsVisible, "A fresh press fires immediately and hides the dotted guide");
        owner.SetScriptedInput(new PlayerInputFrame { hasAimDirWS = true, aimDirWS = Vector3.forward, attackHeld = true });
        until = Time.time + .35f; while (Time.time < until) yield return null;
        Vector3 before = live.AimDirection;
        Check(before.z > .05f && Vector3.Angle(Vector3.right, before) < 22,
            "Eased aim accelerates into the configured limited turn");
        owner.SetScriptedInput(new PlayerInputFrame { hasAimDirWS = true, aimDirWS = Vector3.back, attackHeld = true });
        until = Time.time + .04f; while (Time.time < until) yield return null;
        Check(Vector3.SignedAngle(before, live.AimDirection, Vector3.down) > -.5f,
            "Hard reversal brakes the existing turn instead of instantly reversing at full speed");
        before = live.AimDirection;
        until = Time.time + .4f; while (Time.time < until) yield return null;
        Check(Vector3.SignedAngle(before, live.AimDirection, Vector3.down) < -5,
            "After braking the firing aim accelerates toward the opposite direction");
        settings.aimDirectionEaseSeconds = 0; before = live.AimDirection; float clock = live.AnimationTime;
        until = Time.time + .12f; while (Time.time < until) yield return null;
        Check(Mathf.Abs(Vector3.Angle(before, live.AimDirection) - 60 * (live.AnimationTime - clock)) < 2,
            "Zero easing restores the original constant-speed turn live");
        settings.aimDirectionEaseSeconds = .25f; settings.turningWhileFiring = 0; before = live.AimDirection;
        until = Time.time + .08f; while (Time.time < until) yield return null;
        Check(Vector3.Angle(before, live.AimDirection) < .1f, "Zero turning scale locks aim even when easing had momentum");
        Input(owner, false, false, true);
        until = Time.time + 1; while (live.IsFiring && Time.time < until) yield return null;
        yield return null;
        Check(!live.IsFiring && guide.IsVisible, "Guide returns when firing stops, including during beam retraction");
        Input(owner, true, true); yield return null; yield return null;
        Check(live.IsFiring, "Player can start another shot before the death test");
        bool hidden = false; owner.DeathHidden += _ => hidden = true;
        var death = owner.ApplyExternalMassDelta(-10, null, allowDeath: true);
        Check(death.causedDeath && !live.IsFiring && !guide.IsVisible,
            "Death immediately cancels the beam and aim guide");
        bool shrinking = false;
        until = Time.time + 2;
        while (!hidden && Time.time < until)
        {
            if (!shrinking && meter.LifeVisibilityScale > .01f && meter.LifeVisibilityScale < .9f)
            {
                shrinking = true;
                CaptureView(owner.transform.position + Vector3.right * 4, "death-ring-shrink.png", owner.transform.position);
            }
            yield return null;
        }
        Check(shrinking, "Charge ring visibly shrinks through the player's death animation");
        Check(death.causedDeath && hidden && !meter.IsEnergyMeterVisible, "Actual player death hides the charge ring with the player");
        CaptureView(owner.transform.position + Vector3.right * 4, "death-ring-hidden.png", owner.transform.position);
        Check(owner.powerUps.HasActive, "Death preserves the existing power-up duration policy");
        until = Time.time + 8; while (owner.temporarilyEliminated && Time.time < until) yield return null;
        yield return null; yield return null;
        Check(!owner.temporarilyEliminated && meter.IsEnergyMeterVisible && meter.LifeVisibilityScale == 1 && guide.IsVisible,
            "Respawn restores the ring and idle guide at the living player");
        Check(!live.IsFiring, "Holding the pre-death attack cannot fire on respawn");
        Input(owner, false, false, true); yield return null; yield return null;
        Input(owner, true, true); yield return null; yield return null;
        Check(live.IsFiring && !guide.IsVisible, "A fresh post-respawn press fires normally");
        owner.powerUps.Clear(); yield return null;
        Check(!live && !guide && !meter.IsEnergyMeterVisible, "Unequip removes the guide, beam and ring");
        Check(errors.Count == 0, "No runtime Console errors during accelerator refinement checks");
    }

    static IEnumerator MovementChecks()
    {
        settings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ParticleAcceleratorPowerUpDefinition>(ParticleAcceleratorBeamSetup.DefinitionPath));
        settings.movementWhileFiring = settings.turningWhileFiring = 0;
        settings.aimDirectionEaseSeconds = 0;
        settings.minimumBurstSeconds = 1; settings.fullMeterFireSeconds = 6; settings.maxTurnDelay = 0;
        scope = new GameObject("Accelerator firing movement validation"); scope.SetActive(false);
        scope.transform.position = Vector3.right * 3000;
        var owner = Actor(0, Vector3.zero);
        scope.SetActive(true); yield return null; yield return null;
        var body = owner.GetComponent<Rigidbody>();
        var visuals = owner.visualsController;
        visuals.maxYawSpeed = 120;
        owner.powerUps.Equip(settings, float.PositiveInfinity);
        Check(owner.powerUps.MovementMultiplierWhileCharging == 1, "Idle equipped accelerator does not slow movement");
        Input(owner, false, false); yield return null; yield return null;
        Check(Vector3.Angle(visuals.gameplayFacing.right, Vector3.right) < .1f, "Idle facing follows requested aim with turning scale zero");
        ParticleAcceleratorBeam live = null;
        owner.powerUps.ProjectileFired += go => live = go.GetComponent<ParticleAcceleratorBeam>();
        Input(owner, true, true); yield return null; Input(owner, false, true); yield return null;
        Check(live && live.IsFiring && owner.powerUps.MovementMultiplierWhileCharging == 0,
            "Firing immediately applies zero movement scale");
        owner.SetScriptedInput(new PlayerInputFrame { move = Vector2.right, hasAimDirWS = true, aimDirWS = Vector3.forward, attackHeld = true });
        float until = Time.time + .15f; while (Time.time < until) yield return null;
        Check(Vector3.Angle(live.AimDirection, Vector3.right) < .1f && Vector3.Angle(visuals.gameplayFacing.right, live.AimDirection) < .1f,
            "Zero turning scale locks both beam aim and combat facing despite new input");
        Check(new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude < .01f,
            "Zero movement scale prevents locomotion even with held movement input");

        settings.turningWhileFiring = .5f; settings.movementWhileFiring = .35f;
        Vector3 beforeAim = live.AimDirection; float beforeClock = live.AnimationTime;
        until = Time.time + .2f; while (Time.time < until) yield return null;
        float elapsed = live.AnimationTime - beforeClock;
        Check(Mathf.Abs(Vector3.Angle(beforeAim, live.AimDirection) - 60 * elapsed) < 2f,
            "Half turning scale rotates the firing heading at half the normal maximum turn rate");
        Check(Mathf.Abs(owner.powerUps.MovementMultiplierWhileCharging - .35f) < .0001f,
            "Live movement scale changes apply during the current beam");
        Check(Vector3.Angle(visuals.gameplayFacing.right, live.AimDirection) < .1f,
            "Combat facing follows the limited firing heading rather than raw aim input");
        Vector3 beamAxis = live.BeamEnd - (visuals.visuals.position + live.AimDirection * settings.muzzleOffset + Vector3.up * settings.muzzleHeight);
        Check(Vector3.Angle(beamAxis, live.AimDirection) < 1f,
            "The rendered beam and its collision path follow the limited heading");

        owner.SetMatchInputLocked(true); beforeAim = live.AimDirection; beforeClock = live.AnimationTime;
        until = Time.time + .1f; while (Time.time < until) yield return null;
        Check(Vector3.Angle(beforeAim, live.AimDirection) < .1f && live.AnimationTime == beforeClock,
            "Match pause freezes turn progression and the beam clock");
        owner.SetMatchInputLocked(false);
        settings.turningWhileFiring = 1; settings.movementWhileFiring = 1;
        beforeAim = live.AimDirection; beforeClock = live.AnimationTime;
        until = Time.time + .15f; while (Time.time < until) yield return null;
        elapsed = live.AnimationTime - beforeClock;
        Check(Mathf.Abs(Vector3.Angle(beforeAim, live.AimDirection) - 120 * elapsed) < 2f && owner.powerUps.MovementMultiplierWhileCharging == 1,
            "Full turning and movement scales restore full rates during firing");

        settings.turningWhileFiring = 0; settings.movementWhileFiring = .25f;
        owner.SetScriptedInput(new PlayerInputFrame { hasAimDirWS = true, aimDirWS = Vector3.left, attackUp = true });
        yield return null; yield return null;
        Check(live.IsFiring && owner.powerUps.MovementMultiplierWhileCharging == .25f,
            "Releasing a tap keeps firing movement restrictions for its minimum burst");
        until = Time.time + 2; while (live.IsFiring && Time.time < until) yield return null;
        Check(!live.IsFiring && owner.powerUps.MovementMultiplierWhileCharging == 1,
            "Minimum burst completion immediately restores movement during the visual fade");
        owner.SetScriptedInput(new PlayerInputFrame { hasAimDirWS = true, aimDirWS = Vector3.left });
        yield return null; yield return null;
        Check(Vector3.Angle(visuals.gameplayFacing.right, Vector3.left) < .1f,
            "Turning is unrestricted again after firing stops");

        owner.powerUps.Clear(); Input(owner, false, false); yield return null; yield return null;
        settings.fullMeterFireSeconds = .3f;
        owner.powerUps.Equip(settings, float.PositiveInfinity);
        Input(owner, true, true); yield return null; Input(owner, false, true); yield return null;
        until = Time.time + 1; while (live.IsFiring && Time.time < until) yield return null;
        Check(!live.IsFiring && owner.powerUps.MovementMultiplierWhileCharging == 1,
            "Energy exhaustion clears firing movement restrictions while the button remains held");
        owner.powerUps.Clear(); Input(owner, false, false); yield return null; yield return null;
        owner.powerUps.Equip(settings, float.PositiveInfinity);
        Input(owner, true, true); yield return null; yield return null;
        Check(live.IsFiring && owner.powerUps.MovementMultiplierWhileCharging == .25f, "Re-equip applies firing restrictions on the next shot");
        owner.powerUps.Clear(); yield return null;
        Check(!live && owner.powerUps.MovementMultiplierWhileCharging == 1, "Unequip destroys the beam and restores movement");
        Check(errors.Count == 0, "No runtime Console errors during firing movement checks");
    }

    static IEnumerator EnemyChecks()
    {
        settings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ParticleAcceleratorPowerUpDefinition>(ParticleAcceleratorBeamSetup.DefinitionPath));
        float authoredEnemyDps = settings.enemyDamagePerSecond;
        scope = new GameObject("Accelerator enemy validation"); scope.SetActive(false);
        scope.transform.position = Vector3.right * 3000;
        var owner = Actor(0, Vector3.zero);
        var victim = Actor(2, new Vector3(6, 0, 120));
        string[] definitions = { "Melee/Drone/ED_Drone", "Ranged/Ranged Drone/ED_RangedDrone",
            "Melee/Seeker/ED_Seeker", "Ranged/Particle Beam Turret/ED_ParticleBeamTurret",
            "Melee/Carrier/ED_Carrier", "Melee/DysonSphere/ED_DysonSphere", "Melee/DysonSphere/ED_DysonSphere_Repulsor" };
        var targets = new List<EnemyBase>();
        for (int i = 0; i < definitions.Length; i++)
        {
            var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Enemy System/Enemy Types/" + definitions[i] + ".asset");
            Check(def && def.prefab, definitions[i] + ": canonical enemy definition and prefab exist");
            var go = Object.Instantiate(def.prefab, scope.transform);
            go.transform.localPosition = new Vector3(6, 0, i * 15);
            go.transform.rotation = Quaternion.LookRotation(Vector3.left);
            var enemy = go.GetComponent<EnemyBase>();
            enemy.ConfigureDemonstration(scope.transform, owner); enemy.Init(def, null);
            enemy.HoldPosition = true; enemy.AttacksEnabled = false; enemy.DemonstrationCounterkill = false;
            if (go.TryGetComponent<ParticleBeamTurretController>(out var turret)) turret.mount = ParticleBeamTurretController.WallMount.Authored;
            targets.Add(enemy);
        }
        scope.SetActive(true);
        // Allow each authored arrival sequence to enable its real colliders naturally.
        float until = Time.time + 8; while (Time.time < until) yield return null;
        owner.powerUps.enabled = victim.powerUps.enabled = false; owner.enabled = victim.enabled = false;
        owner.GetComponent<Rigidbody>().isKinematic = victim.GetComponent<Rigidbody>().isKinematic = true;
        var beam = Object.Instantiate(settings.sustainedBeamPrefab, scope.transform); beam.Initialize(owner, settings);
        var failures = new List<string>();
        for (int i = 0; i < targets.Count; i++)
        {
            var enemy = targets[i]; string label = enemy.Definition.name;
            // Contact/DPS assertions need a living target. High user tuning can
            // kill it before those checks; only this temporary definition changes.
            settings.enemyDamagePerSecond = .08f;
            // Some authored controllers clamp their arrival position to the active arena.
            // Move the ready target back to the isolated lane before advancing the beam clock.
            enemy.transform.position = scope.transform.position + new Vector3(6, 0, i * 15);
            if (enemy.TryGetComponent<Rigidbody>(out var body)) body.position = enemy.transform.position;
            Place(owner, enemy.transform.position + Vector3.left * 6);
            Vector3 origin = owner.transform.position + Vector3.right * settings.muzzleOffset + Vector3.up * settings.muzzleHeight;
            Physics.SyncTransforms(); beam.Begin(origin, Vector3.right);
            float initial = enemy.HealthRemaining;
            for (int n = 0; n < 50; n++) beam.Advance(.02f, origin, Vector3.right);
            if (!beam.Contact || beam.Contact.GetComponentInParent<EnemyBase>() != enemy || enemy.HealthRemaining >= initial)
            {
                string path = beam.Contact ? beam.Contact.name : "none";
                if (beam.Contact) for (var parent = beam.Contact.transform.parent; parent; parent = parent.parent) path = parent.name + "/" + path;
                string detail = label + ": health=" + enemy.HealthRemaining + ", contact=" + path + ", beam length=" + beam.BeamLength +
                    ", origin=" + origin + ", target=" + enemy.transform.position + ", shares simulation=" + enemy.SharesSimulationWith(owner) +
                    ", colliders=" + string.Join("; ", enemy.GetComponentsInChildren<Collider>().Select(c => c.name + " enabled=" + c.enabled + " bounds=" + c.bounds));
                failures.Add(detail); checks.Add("FAIL " + detail);
                enemy.gameObject.SetActive(false); beam.Clear();
                continue;
            }
            Check(beam.Contact && beam.Contact.GetComponentInParent<EnemyBase>() == enemy && enemy.HealthRemaining < initial && !enemy.IsDead,
                label + ": real beam collision reduces health (" + initial.ToString("F4") + " -> " + enemy.HealthRemaining.ToString("F4") + " after 1s)");
            Check(beam.contactPlasma.IsEmitting, label + ": impact plasma emits on the enemy");
            float health = enemy.HealthRemaining;
            beam.Advance(.2f, origin, Vector3.right);
            Check(Mathf.Abs(health - enemy.HealthRemaining - settings.enemyDamagePerSecond * .2f) < .0001f,
                label + ": continuous damage matches configured DPS without a hit cooldown");
            float playerDps = settings.playerDamagePerSecond, enemyDps = settings.enemyDamagePerSecond;
            settings.playerDamagePerSecond = 0; settings.enemyDamagePerSecond = .3f;
            health = enemy.HealthRemaining; beam.Advance(.2f, origin, Vector3.right);
            Check(Mathf.Abs(health - enemy.HealthRemaining - .06f) < .0001f,
                label + ": live enemy DPS changes independently while player DPS is zero");
            settings.playerDamagePerSecond = 3; settings.enemyDamagePerSecond = 0;
            health = enemy.HealthRemaining; beam.Advance(.2f, origin, Vector3.right);
            Check(enemy.HealthRemaining == health && beam.contactPlasma.IsEmitting,
                label + ": zero enemy DPS stops damage while preserving contact effects, regardless of player DPS");
            settings.playerDamagePerSecond = playerDps; settings.enemyDamagePerSecond = enemyDps;
            enemy.Pause(true); health = enemy.HealthRemaining; beam.Advance(.2f, origin, Vector3.right);
            Check(enemy.HealthRemaining == health, label + ": paused enemy rejects damage");
            enemy.Pause(false);
            enemy.ConfigureDemonstration(null, null); beam.Advance(.2f, origin, Vector3.right);
            Check((!beam.Contact || beam.Contact.GetComponentInParent<EnemyBase>() != enemy) && enemy.HealthRemaining == health,
                label + ": a different simulation is ignored (next contact=" + (beam.Contact ? beam.Contact.name : "none") + ")");
            enemy.ConfigureDemonstration(scope.transform, owner);
            beam.Stop(); beam.Advance(.2f, origin, Vector3.right);
            Check(enemy.HealthRemaining == health, label + ": fading beam deals no residual damage");

            // Exercise lethal attribution with authored DPS (a nonzero test
            // fallback also supports tuning damage off without hanging validation).
            // Advance the beam clock directly; this is not a claim that one energy tank can kill it.
            settings.enemyDamagePerSecond = authoredEnemyDps > 0 ? authoredEnemyDps : .08f;
            int defeats = 0; EnemyDefeatContext defeat = default;
            enemy.Defeated += context => { defeats++; defeat = context; };
            beam.Begin(origin, Vector3.right);
            int limit = Mathf.CeilToInt(enemy.HealthRemaining / Mathf.Max(.0001f, settings.enemyDamagePerSecond * .2f)) + 5;
            for (int n = 0; n < limit && !enemy.IsDead; n++) beam.Advance(.2f, origin, Vector3.right);
            beam.Advance(.2f, origin, Vector3.right);
            Check(enemy.IsDead && defeats == 1 && defeat.creditedPlayer == owner && defeat.source == EnemyDamageSource.Projectile,
                label + ": sustained damage kills once and attributes the projectile defeat to the shooter");
            enemy.gameObject.SetActive(false); beam.Clear();
            yield return null;
        }
        Check(failures.Count == 0, failures.Count == 0 ? "All seven enemy prefabs receive beam damage" : string.Join("\n", failures));

        Place(owner, scope.transform.position + Vector3.forward * 120);
        Place(victim, owner.transform.position + Vector3.right * 6);
        Vector3 playerOrigin = owner.transform.position + Vector3.right * settings.muzzleOffset + Vector3.up * settings.muzzleHeight;
        settings.playerDamagePerSecond = 0; settings.enemyDamagePerSecond = 99;
        beam.Begin(playerOrigin, Vector3.right); beam.Advance(.5f, playerOrigin, Vector3.right);
        Check(beam.Contact && beam.Contact.GetComponentInParent<PlayerControllerScript>() == victim,
            "Independent player damage fixture hits the actual player collider");
        float mass = victim.massScore;
        beam.Advance(.2f, playerOrigin, Vector3.right);
        Check(victim.massScore == mass && beam.contactPlasma.IsEmitting,
            "Zero player DPS prevents mass loss despite high enemy DPS and preserves impact effects");

        settings.playerDamagePerSecond = .2f; settings.enemyDamagePerSecond = 0;
        owner.massScore = (owner.massScoreMin + owner.massScoreMax) * .5f;
        float shooterMass = owner.massScore;
        settings.transferMassToShooter = true;
        beam.Advance(.2f, playerOrigin, Vector3.right);
        Check(Mathf.Abs(mass - victim.massScore - .04f) < .0001f,
            "Player DPS applies independently while enemy DPS is zero");
        Check(Mathf.Abs(owner.massScore - shooterMass - .04f) < .0001f,
            "Player damage still transfers only the actual removed player mass");

        settings.enemyDamagePerSecond = 99; mass = victim.massScore;
        beam.Advance(.2f, playerOrigin, Vector3.right);
        Check(Mathf.Abs(mass - victim.massScore - .04f) < .0001f,
            "Changing enemy DPS during firing does not change player damage");
        settings.playerDamagePerSecond = 0; mass = victim.massScore; shooterMass = owner.massScore;
        beam.Advance(.2f, playerOrigin, Vector3.right);
        Check(victim.massScore == mass && owner.massScore == shooterMass,
            "Disabling player damage live also stops player mass transfer");
        beam.Clear();
        Check(errors.Count == 0, "No runtime Console errors during enemy damage checks");
    }

    static void Capture(ParticleAcceleratorBeam beam, string filename, Vector3 anchor)
        => CaptureView(beam.BeamEnd, filename, anchor);

    static void CaptureView(Vector3 endpoint, string filename, Vector3 anchor)
    {
        var go = new GameObject("Beam validation camera"); var camera = go.AddComponent<Camera>(); camera.enabled = false;
        camera.transform.position = (anchor + endpoint) * .5f + Vector3.up * 30; camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        camera.orthographic = true; camera.orthographicSize = Mathf.Max(5, Vector3.Distance(anchor, endpoint) * .32f); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .12f, .14f); camera.farClipPlane = 80;
        var rt = RenderTexture.GetTemporary(1400, 700, 24); var texture = new Texture2D(1400, 700, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1400, 700), 0, 0); texture.Apply(); File.WriteAllBytes(Output + "/" + filename, texture.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(rt); Object.Destroy(texture); Object.Destroy(go); }
    }
}
#endif
