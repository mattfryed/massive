#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Massive.Player;
using Massive.PowerUps;
using Massive.Multiplier;
using Massive.Enemies;
using Massive.Resonance;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Core possession, contests, trajectory, combat and cleanup in a disposable Play Mode scene.</summary>
[InitializeOnLoad]
public static partial class AmplifierPossessionValidation
{
    const string Key = "MASSIVE.AmplifierPossessionValidation";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new(), errors = new();
    static IEnumerator routine;
    static int frame;
    static double deadline;
    public static string Result => SessionState.GetString(Key + ".Result", "Not run");

    static AmplifierPossessionValidation() => EditorApplication.playModeStateChanged += State;

    [MenuItem("MASSIVE/Amplifier/Validate Possession and Shots")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run in Edit Mode after compilation.");
        var original = SceneManager.GetActiveScene();
        string path = "Assets/AmplifierPossessionValidation-" + Guid.NewGuid().ToString("N") + ".unity";
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var camera = new GameObject("Fixture camera", typeof(Camera), typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(camera, fixture);
            var light = new GameObject("Fixture light", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
            SceneManager.MoveGameObjectToScene(light, fixture);
            if (!EditorSceneManager.SaveScene(fixture, path)) throw new Exception("Could not save fixture.");
        }
        finally { EditorSceneManager.CloseScene(fixture, true); SceneManager.SetActiveScene(original); }
        SessionState.SetString(Key + ".Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + ".Scene", path);
        SessionState.SetString(Key + ".Result", "RUNNING"); SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".CaptureOnly", false);
        SessionState.SetBool(Key + ".RecordContest", false);
        SessionState.SetBool(Key + ".PostContestOnly", false);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.isPlaying = true;
    }

    public static void CaptureReview()
    {
        Run();
        SessionState.SetBool(Key + ".CaptureOnly", true);
    }

    [MenuItem("MASSIVE/Amplifier/Record Possession Contest Demo")]
    public static void RecordContest()
    {
        Run();
        SessionState.SetBool(Key + ".RecordContest", true);
    }

    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); frame = -1;
            deadline = EditorApplication.timeSinceStartup + (SessionState.GetBool(Key + ".RecordContest", false) ? 420 : 180);
            routine = Expand(Checks()); Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            (routine as IDisposable)?.Dispose(); routine = null;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + ".Previous", ""));
            string path = SessionState.GetString(Key + ".Scene", "");
            if (path.StartsWith("Assets/AmplifierPossessionValidation-", StringComparison.Ordinal) && path.EndsWith(".unity", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(path);
            SessionState.SetBool(Key, false);
        }
    }

    static void Log(string message, string stack, LogType type)
    { if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(message + "\n" + stack); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out.");
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        (routine as IDisposable)?.Dispose(); routine = null;
        Application.logMessageReceived -= Log;
        bool pass = error == null && errors.Count == 0;
        SessionState.SetString(Key + ".Result", (pass ? "PASSED" : "FAILED") + " (" + checks.Count + " checks)\n" +
            string.Join("\n", checks) + (error == null ? "" : "\n" + error) + "\n" + string.Join("\n", errors));
        if (pass) Debug.Log(Result); else Debug.LogError(Result);
        EditorApplication.isPlaying = false;
    }
    static void Check(bool pass, string label)
    { if (!pass) throw new Exception(label); checks.Add("PASS " + label); }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
    static IEnumerator Expand(IEnumerator root)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(root);
        try
        {
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { (top as IDisposable)?.Dispose(); stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return top.Current;
            }
        }
        finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
    }
    static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Hidden).Invoke(target, args);

    static GameObject scope;
    static PlayerControllerScript light, dark;
    static AmplifierCoreGameplay core;
    static readonly List<Object> owned = new();
    static IEnumerator Wait(float seconds)
    { float end = Time.time + seconds; while (Time.time < end) yield return null; }
    static IEnumerator Until(Func<bool> predicate, float seconds, string reason)
    { float end = Time.time + seconds; while (!predicate() && Time.time < end) yield return null; Check(predicate(), reason); }
    static PlayerControllerScript Player(int team)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), scope.transform);
        var p = go.GetComponent<PlayerControllerScript>(); p.ConfigureDemonstration(scope.transform, team - 1, team);
        Set(p, "tuningMode", PlayerTuningMode.Local);
        var attack = go.GetComponent<PlayerAttackController>(); attack.UseSharedSettings = false;
        var profile = Object.Instantiate(attack.Profile); owned.Add(profile);
        Set(attack, "attackProfile", profile); Set(attack, "lockOnEnabled", false);
        foreach (var stage in profile.Stages)
        {
            Set(stage, "duration", .8f); Set(stage, "activationFrameStart", 0f);
            Set(stage, "activationFrameEnd", 42f); Set(stage, "travelDistance", 0f);
        }
        return p;
    }
    static void Input(PlayerControllerScript p, bool attack = false, bool shield = false, bool release = false, Vector3 aim = default)
    {
        p.SetScriptedInput(new PlayerInputFrame { attackDown = attack, attackHeld = attack, attackUp = release,
            shieldDown = shield, shieldHeld = shield, hasAimDirWS = true, aimDirWS = aim == default ? Vector3.right : aim });
    }
    static void PlacePlayer(PlayerControllerScript p, Vector3 point)
    {
        p.attackController.CancelAttack(); p.ClearScriptedInput(); p.GetComponent<PlayerShieldAbility>().ForceStopShield();
        Set(p, "externalStunUntil", 0f);
        var rb = p.GetComponent<Rigidbody>(); rb.position = point; p.transform.position = point;
        if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
    }
    static IEnumerator Enter(PlayerControllerScript p, int side = -1)
    {
        float r = core.GetComponent<Collider>().bounds.extents.x + PlayerScaleAdjuster.BodyRadiusOf(p);
        PlacePlayer(p, core.transform.position + Vector3.right * side * (r + .015f));
        Input(p, aim: Vector3.right * -side);
        yield return null; yield return null;
        yield return Wait(p.attackController.CooldownRemaining + .03f);
        Input(p, attack: true, aim: Vector3.right * -side);
        yield return null; yield return null;
        Input(p, release: true, aim: Vector3.right * -side);
        yield return Until(() => p.IsInsideAmplifier, 1.2f, (p.teamID == 1 ? "Light" : "Dark") + " enters through normal Thrust input and physical contact");
        Check(p.IsAmplifierTransitioning && p.visualsController.enabled, "Entry keeps the player's procedural body visible during tidal collapse");
        if (SessionState.GetBool(Key + ".CaptureOnly", false))
        {
            yield return Wait(.14f);
            SaveFrame(Camera.main, p.teamID == 1 ? "01-light-entry" : "04-dark-entry");
        }
        yield return Until(() => !p.IsAmplifierTransitioning, .6f, "Entry animation completes before held-state hiding");
        Input(p); yield return null;
    }
    static IEnumerator ResetCore()
    {
        core.CompleteSpawnImmediately();
        PlacePlayer(light, Vector3.left * 5f); PlacePlayer(dark, Vector3.left * 8f);
        yield return Wait(.1f);
    }
    static IEnumerator Fire(PlayerControllerScript p, float held, Vector3 direction)
    {
        Input(p, attack: true, aim: direction); yield return Wait(held);
        Input(p, release: true, aim: direction);
        yield return Until(() => core.IsLaunched, .3f, "Releasing Attack launches the charged Core");
        Input(p);
    }
    static GameObject Wall(Vector3 point, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Validation wall";
        wall.transform.SetParent(scope.transform); wall.transform.position = point; wall.transform.localScale = scale;
        return wall;
    }
    static EnemyBase Enemy(Vector3 point, float health = 10f)
    {
        var go = new GameObject("Shot NPC", typeof(Rigidbody), typeof(SphereCollider), typeof(EnemyBase), typeof(EnemyHurtbox));
        go.transform.SetParent(scope.transform); go.transform.position = point;
        go.GetComponent<Rigidbody>().useGravity = false; go.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezePositionY;
        go.GetComponent<SphereCollider>().radius = .35f;
        var e = go.GetComponent<EnemyBase>();
        var def = ScriptableObject.CreateInstance<EnemyDefinition>(); owned.Add(def);
        def.healthMassEq = health; def.damageTakenPerSwordHit = 2f;
        e.ConfigureDemonstration(scope.transform, light); e.Init(def, null);
        return e;
    }

    static IEnumerator StoppingChecks()
    {
        string savedPossession = JsonUtility.ToJson(core.Possession);
        float savedDrag = core.Effective_linearDrag;
        try
        {
            yield return ResetCore();
            Set(core, "linearDrag", 3f); core.ApplyTuning();
            yield return Enter(light); yield return Fire(light, .36f, Vector3.right);
            yield return Wait(.1f);
            float fastDragSpeed = core.ShotVelocity.magnitude;
            Check(Mathf.Approximately(core.Body.linearDamping, 3f) && fastDragSpeed < 9f,
                "Coasting drag actively slows a moving shot");
            Set(core, "linearDrag", .1f); core.ApplyTuning();
            yield return Wait(.1f);
            Check(core.Body.linearDamping < 1f && core.ShotVelocity.magnitude > fastDragSpeed * .8f,
                "Lowering drag during a shot removes the old body drag floor while preserving charge drag");

            core.Possession.settledSpeed = .2f;
            core.Possession.settlingSeconds = .5f;
            core.Body.linearVelocity = Vector3.right * .1f;
            yield return Wait(.15f);
            Check(core.IsLaunched, "Custom settling time keeps a briefly slow shot in flight");
            core.Body.linearVelocity = Vector3.right * 2f;
            yield return Wait(.1f);
            core.Body.linearVelocity = Vector3.right * .1f;
            yield return Wait(.25f);
            Check(core.IsLaunched, "Regaining speed resets the continuous settling interval");
            yield return Until(() => !core.IsLaunched, .6f, "Shot rests after the configured continuous settling interval");
            Check(core.Body.isKinematic, "Settled Core stops drifting and returns to available physics");

            yield return ResetCore();
            core.Possession.settlingSeconds = 0f;
            yield return Enter(light); yield return Fire(light, .36f, Vector3.right);
            yield return Wait(.1f);
            Check(core.IsLaunched && core.ShotVelocity.magnitude > 1f, "Zero settling time never stops a shot above Stop Speed");
            core.Body.linearVelocity = Vector3.right * .1f;
            yield return Until(() => !core.IsLaunched, .2f, "Zero settling time rests a slow shot on its next physics step");

            yield return ResetCore();
            Set(core, "linearDrag", 0f); core.ApplyTuning();
            core.Possession.minimumRange = core.Possession.maximumRange = 1000f;
            core.Possession.settlingSeconds = .3f;
            core.Possession.maximumFlightSeconds = 1f;
            core.Possession.finalBrakeDrag = 0f;
            yield return Enter(light); yield return Fire(light, .36f, Vector3.right);
            yield return Wait(1.15f);
            Check(core.IsLaunched && core.ShotVelocity.magnitude > 8f,
                "Zero final brake drag leaves the physical coast intact after Brake After");
            core.Possession.finalBrakeDrag = 8f;
            yield return Wait(.1f);
            Check(core.IsLaunched && core.ShotVelocity.magnitude < 7f && core.ShotVelocity.magnitude > .2f,
                "Live Brake Drag change decelerates the shot instead of instantly stopping it");
            yield return Until(() => !core.IsLaunched, 1.5f, "Configured final braking brings a long-coasting shot back to rest");
        }
        finally
        {
            JsonUtility.FromJsonOverwrite(savedPossession, core.Possession);
            Set(core, "linearDrag", savedDrag); core.ApplyTuning();
        }
    }

    static IEnumerator Checks()
    {
        Check(SceneManager.GetActiveScene().path == SessionState.GetString(Key + ".Scene", ""), "Disposable scene isolates all gameplay checks");
        scope = new GameObject("Possession fixtures"); scope.SetActive(false);
        var profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset")); owned.Add(profile);
        var serviceGO = new GameObject("Fixture score service"); serviceGO.SetActive(false);
        var service = serviceGO.AddComponent<MatchScoreService>(); Set(service, "profile", profile); serviceGO.SetActive(true);
        light = Player(1); dark = Player(2);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Power-ups/Amplifier Core/Amplifier Core.prefab");
        var coreGO = Object.Instantiate(prefab, scope.transform); core = coreGO.GetComponent<AmplifierCoreGameplay>();
        core.UseSharedSettings = false; Set(core, "playSpawnAnimationOnEnable", false);
        core.Possession.mode = AmplifierInteractionMode.PossessionShot;
        core.Possession.reentryDelay = .05f; core.Possession.fullChargeSeconds = .3f;
        core.Possession.shotSpeed = 10f; core.Possession.minimumRange = 6f; core.Possession.maximumRange = 12f;
        scope.SetActive(true); service.ResetForMatch(true);
        var cam = Camera.main ? Camera.main : Object.FindFirstObjectByType<Camera>();
        cam.tag = "MainCamera"; cam.transform.position = new Vector3(0, 20, 0); cam.transform.rotation = Quaternion.Euler(90, 0, 0);
        cam.orthographic = true; cam.orthographicSize = 9f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        try
        {
            yield return null; yield return null; core.CompleteSpawnImmediately();
            if (SessionState.GetBool(Key + ".RecordContest", false))
            {
                yield return ContestRecording(cam);
                yield break;
            }
            if (SessionState.GetBool(Key + ".PostContestOnly", false))
            {
                yield return PostContestInputChecks();
                yield break;
            }
            yield return PresentationChecks(cam);
            if (SessionState.GetBool(Key + ".CaptureOnly", false))
            {
                cam.orthographicSize = 2f;
                yield return ResetCore(); yield return Enter(light);
                yield return Wait(.15f); SaveFrame(cam, "02-light-held");
                yield return Fire(light, .2f, Vector3.right);
                yield return Wait(.10f); SaveFrame(cam, "03-light-exit");
                yield return Wait(.5f);
                yield return ResetCore(); yield return Enter(dark, 1);
                yield return Wait(.15f); SaveFrame(cam, "05-dark-held");
                yield return Fire(dark, .2f, Vector3.left);
                yield return Wait(.10f); SaveFrame(cam, "06-dark-exit");
                yield return Wait(.5f);
                Check(!light.IsInsideAmplifier && !dark.IsInsideAmplifier && errors.Count == 0, "Both teams finish visible entry/exit with restored controls");
                yield break;
            }
            yield return ResetCore();
            Check(core.Body.isKinematic && !core.TryApplyBeamPush(Vector3.right, 50f, 1f) && !core.TryApplyRepulsorImpact(Vector3.left, Vector3.right), "Alternate Core is stationary and rejects beam/Repulsor pushing");
            yield return Enter(light);
            Check(core.ControlState == AmplifierControlState.Aiming && core.Occupant == light && !light.attackController.IsAttacking, "Thrust transfers input ownership and cancels its old attack");
            Check(light.IsInvulnerable && !light.TryApplyHit(dark.gameObject).accepted && !light.ApplyExternalMassDelta(-.1f, dark.gameObject).accepted, "Possession blocks melee and external NPC damage");
            Check(!light.visualsController.enabled && !light.GetComponent<Rigidbody>().detectCollisions, "Occupied player hides and leaves physical contacts");
            Check(!core.BeginTimeoutDespawn(), "Encounter expiry cannot despawn an occupied Core");
            var goalGO = new GameObject("Light goal", typeof(SphereCollider), typeof(AmplifierGoalCapture)); goalGO.transform.SetParent(scope.transform); goalGO.transform.position = Vector3.right * 20;
            var goal = goalGO.GetComponent<AmplifierGoalCapture>(); goal.GetComponent<Collider>().isTrigger = true;
            Check(!core.TryCapture(goal), "Goal cannot swallow a player inside the Core");
            var wall = Wall(new Vector3(3.5f, 0f, 0f), new Vector3(.2f, 4f, 15f));
            Input(light, attack: true); yield return Wait(.16f);
            var path = new List<Vector3>(); core.BuildTrajectory(path);
            Check(path.Count > 8 && path.Last().x > 3.5f && path.All(v => Mathf.Abs(v.z) < .001f), "Aim guide stays straight and makes no wall-rebound promise");
            SaveFrame(cam, "01-aim");
            yield return Until(() => core.IsLaunched, .3f, "Maximum charge launches automatically without releasing Attack");
            Input(light);
            Check(light.IsAmplifierTransitioning && light.visualsController.enabled, "Shot starts visible tidal ejection instead of teleporting the shooter");
            Check(!core.Body.isKinematic && !core.GetComponent<Collider>().isTrigger, "Shot restores solid dynamic physics");
            Check(core.ShotRangeRemaining > 10f && !core.BeginTimeoutDespawn(), "Full charge grants range and protects the flight from encounter expiry");
            var initialRotation = core.Body.rotation;
            yield return Until(() => core.ShotVelocity.x < 0f, .8f, "Live shot rebounds through the existing wall response");
            Check(core.transform.position.x < 3.5f, "Fast Core does not tunnel through the wall");
            Check(Quaternion.Angle(initialRotation, core.Body.rotation) > 1f && core.Body.angularVelocity.sqrMagnitude > .01f, "Shot has real body rotation through a wall rebound");
            float coastSpeed = core.ShotVelocity.magnitude;
            yield return Wait(.5f);
            Check(core.ShotVelocity.magnitude > .1f && core.ShotVelocity.magnitude < coastSpeed, "Shot coasts with progressive drag after bouncing");
            Check(!light.IsInsideAmplifier && light.visualsController.enabled && !light.GetComponent<Rigidbody>().isKinematic, "Ejection restores shooter physics and rendering after animation");
            float lastMovingSpeed = core.ShotVelocity.magnitude;
            float settleDeadline = Time.time + 8f;
            while (core.IsLaunched && Time.time < settleDeadline) { lastMovingSpeed = core.ShotVelocity.magnitude; yield return null; }
            Check(!core.IsLaunched && lastMovingSpeed <= core.Possession.settledSpeed + .01f, "Flight becomes enterable only after naturally slowing below the settling speed");
            Object.DestroyImmediate(wall); Object.DestroyImmediate(goalGO);
            yield return StoppingChecks();
            yield return DistanceChecks();
            yield return GoalAndFeedbackChecks(service);
            // Exercise contests, combat, shields and capture with the new tuning too.
            core.Possession.distanceDrivenShots = true;

            yield return ResetCore(); yield return Enter(light);
            core.Possession.possessionSeconds = .25f; // Takes effect on the next entry.
            yield return ResetCore(); yield return Enter(light);
            yield return Until(() => !light.IsInsideAmplifier, .9f, "Possession timeout animates ejection without shooting");
            Check(core.ControlState == AmplifierControlState.Available && light.visualsController.enabled, "Timeout restores presentation and availability");
            core.Possession.possessionSeconds = 5f;

            yield return ResetCore(); yield return Enter(light); yield return Enter(dark, 1);
            Check(core.ControlState == AmplifierControlState.Contested && Mathf.Approximately(core.LightControl01, .5f), "Opposing Thrust opens a 50/50 team contest");
            Input(light); Input(dark); yield return Wait(.05f);
            Input(light, attack: true, shield: true); yield return Wait(.2f);
            Check(Mathf.Approximately(core.LightControl01, .5f), "Simultaneous buttons earn no contest progress");
            Input(light); yield return null;
            Input(light, attack: true); yield return Wait(.2f); float first = core.LightControl01;
            Input(light, attack: true); yield return Wait(.2f);
            Check(core.LightControl01 == first && first > .5f, "Holding/repeating the same button cannot win a contest");
            SaveFrame(cam, "02-contest");
            for (int i = 0; i < 7 && core.ControlState == AmplifierControlState.Contested; i++)
            { Input(light, attack: i % 2 != 0, shield: i % 2 == 0); yield return Wait(.18f); }
            yield return Until(() => !dark.IsInsideAmplifier, .5f, "Contest loser finishes animated ejection");
            Check(core.Occupant == light && core.ControlState == AmplifierControlState.Aiming && dark.IsExternallyStunned, "Alternating inputs win control and eject/stun the loser");
            Check(core.GetComponent<AmplifierCoreVisual>().EjectionRippleActive, "Contest loss ripples the Core while the winner retains possession");
            CheckPossessionMarkers(false, 1);
            var winnerUI = core.GetComponent<AmplifierPossessionPresentation>().ControlUI;
            Check(core.IsContestWinnerAiming && winnerUI.TextVisible && !winnerUI.RingVisible && winnerUI.Caption.text.Contains("LIGHT WINS"),
                "Contest victory removes the ring and displays the winning team's aiming prompt");
            Input(light, release: true); yield return Wait(.08f);
            Check(!core.IsLaunched && core.Charge01 == 0f, "Contest button release cannot accidentally fire the winner's shot");
            Input(light); yield return Wait(core.PostContestGraceRemaining + core.Possession.postContestReleaseSeconds + .04f);
            yield return Fire(light, .1f, Vector3.right);
            Check(core.ShotRangeRemaining < 11f, "Shorter charge grants less distance at the same shot speed");
            Check(core.ShotVelocity.magnitude > 9.4f && core.ShotVelocity.magnitude <= 10f, "Short charge launches at the same initial speed before drag acts");

            yield return ResetCore(); core.Possession.contestSeconds = .3f;
            yield return Enter(light); yield return Enter(dark, 1);
            Input(light); Input(dark);
            yield return Until(() => !light.IsInsideAmplifier && !dark.IsInsideAmplifier, .9f, "Tied contest deadline ejects both players");
            core.Possession.contestSeconds = 3f;

            yield return ResetCore(); yield return Enter(light);
            core.enabled = false; yield return null;
            Check(!light.IsInsideAmplifier && light.visualsController.enabled && !light.GetComponent<Rigidbody>().isKinematic, "Disabling Core restores occupant controls, rendering and physics");
            core.enabled = true; yield return ResetCore(); yield return Enter(light);
            light.SetMatchInputLocked(true); yield return Wait(.04f);
            Check(!light.IsInsideAmplifier && !light.GetComponent<Rigidbody>().isKinematic, "Match lock ejects occupant without leaving a movement lease");
            light.SetMatchInputLocked(false);

            foreach (float hold in new[] { .04f, .4f })
            {
                yield return ResetCore(); yield return Enter(light);
                var enemy = Enemy(Vector3.right * 3f); Physics.SyncTransforms();
                float before = enemy.HealthRemaining;
                yield return Fire(light, hold, Vector3.right);
                yield return Until(() => enemy.HealthRemaining < before, .8f, "Traveling Core hits NPC hurtbox");
                Check(Mathf.Abs(before - enemy.HealthRemaining - 2f) < .001f, "NPC damage is one normal hit independent of charge " + hold);
                Check(enemy.GetComponent<Rigidbody>().linearVelocity.x > 0f, "Surviving NPC receives outward impulse");
                enemy.transform.position = core.transform.position; Physics.SyncTransforms(); yield return Wait(.08f);
                Check(Mathf.Abs(before - enemy.HealthRemaining - 2f) < .001f, "Same NPC is damaged once per launched shot");
                Object.DestroyImmediate(enemy.gameObject);
            }
            foreach (int team in new[] { 1, 2 })
            {
                yield return ResetCore(); yield return Enter(light);
                dark.teamID = team;
                Vector3 target = core.transform.position + Vector3.right * 3f;
                PlacePlayer(dark, target); Input(dark); float mass = dark.massScore;
                yield return Fire(light, .3f, Vector3.right);
                Check(!Physics.GetIgnoreCollision(core.GetComponent<Collider>(), dark.GetComponent<Collider>()),
                    "Shot keeps player body collisions enabled for team " + team);
                yield return Until(() => dark.transform.position.x > target.x + .08f, .9f,
                    "Physical Core contact pushes an unshielded player on team " + team);
                Check(dark.massScore == mass && !dark.IsInsideAmplifier && core.IsLaunched,
                    "Player contact causes no damage, possession or premature shot end");
                Check(!Physics.GetIgnoreCollision(core.GetComponent<Collider>(), light.GetComponent<Collider>()),
                    "Shot does not exempt its shooter from returning collisions");
            }
            dark.teamID = 2;

            yield return ResetCore(); yield return Enter(light);
            PlacePlayer(dark, Vector3.right * 3f);
            Input(dark, shield: true, aim: Vector3.left);
            yield return Fire(light, .05f, Vector3.right);
            float originalRange = core.ShotRangeRemaining;
            yield return Until(() => core.ShotOwner == dark, .6f, "Opening parry transfers shot attribution to defender");
            Check(core.ShotVelocity.x < 0f && core.ShotRangeRemaining < originalRange, "Parry redirects without refilling the flight budget");
            SaveFrame(cam, "03-parry");
            Set(dark, "isPseudoPlayer", false); service.RegisterPlayer(dark);
            var rewardTarget = Enemy(core.transform.position - Vector3.right * 1.3f, 1f);
            rewardTarget.ConfigureDemonstration(scope.transform, dark);
            rewardTarget.Definition.defeatRewardKey = "ENEMY_DRONE_DEFEAT";
            PlayerControllerScript credited = null; rewardTarget.Defeated += context => credited = context.creditedPlayer;
            double scoreBefore = service.GetTeamScore(2);
            yield return Until(() => credited != null, .6f, "Parried Core defeats an NPC through the normal enemy damage transaction");
            Check(credited == dark && service.GetTeamScore(2) > scoreBefore, "NPC kill and score credit go to the last parrying player's team");
            service.UnregisterPlayer(dark); Set(dark, "isPseudoPlayer", true);

            yield return ResetCore(); yield return Enter(light);
            PlacePlayer(dark, Vector3.right * 3f);
            yield return Wait(dark.GetComponent<PlayerShieldAbility>().ActivationCooldownRemaining + .03f);
            Input(dark, shield: true, aim: Vector3.left);
            yield return Wait(dark.GetComponent<PlayerShieldAbility>().DurationSeconds + .1f);
            Check(dark.GetComponent<PlayerShieldAbility>().BlocksAllDamage && !dark.GetComponent<PlayerShieldAbility>().IsParryWindow, "Held block fixture is outside its parry window");
            yield return Fire(light, .05f, Vector3.right);
            yield return Wait(.35f);
            Check(core.ShotOwner == light && core.ShotVelocity.x >= 0f && core.ShotVelocity.magnitude < 10f, "Late held block absorbs energy without an aimed parry");

            // Kinematic NPCs still yield to a shot without changing their motor type.
            yield return ResetCore(); yield return Enter(light);
            var anchored = Enemy(Vector3.right * 3f); anchored.GetComponent<Rigidbody>().isKinematic = true;
            yield return Fire(light, .1f, Vector3.right); yield return Wait(.6f);
            Check(anchored.transform.position.x > 3.1f && anchored.GetComponent<Rigidbody>().isKinematic, "Kinematic NPC receives a bounded push while retaining its motor mode");
            Object.DestroyImmediate(anchored.gameObject);

            foreach (var playerPolicy in new[] { ResonancePlayerResponse.PatternDefault, ResonancePlayerResponse.Sludge })
            {
                var patternGO = new GameObject("Magnetic curve fixture"); patternGO.SetActive(false); patternGO.transform.SetParent(scope.transform);
                var pattern = patternGO.AddComponent<ResonancePatternController>();
                pattern.segmentMaterial = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resonance/Resonance 345 Hz.prefab").GetComponent<ResonancePatternController>().segmentMaterial;
                var definition = ScriptableObject.CreateInstance<ResonancePatternDefinition>(); owned.Add(definition);
                definition.curves.Add(new ResonanceCurve { radius = 2f, offset = new Vector2(5f, 0f) });
                definition.arcs.Add(new ResonanceArc { startDegrees = 0f, sweepDegrees = 360f, endTaperFraction = 0f, thickness = .15f, colliderThickness = .15f });
                pattern.playerResponse = playerPolicy;
                pattern.definition = definition; pattern.coreResponse = ResonanceCoreResponse.MagneticRepulsion;
                pattern.magneticPath = ResonanceMagneticPath.CurvedGlide;
                pattern.magneticBrakeSeconds = .1f; pattern.magneticReleaseSeconds = .2f; pattern.magneticReach = .6f;
                patternGO.SetActive(true); pattern.Rebuild(); yield return Wait(.55f);
                Check(pattern.InteractiveSegments.Count > 0, "Resonance fixture builds actual curved contacts");
                yield return ResetCore(); yield return Enter(light);
                Vector3 oblique = new Vector3(1f, 0f, .14f).normalized;
                Input(light, attack: true, aim: oblique); yield return Wait(.16f);
                path.Clear(); core.BuildTrajectory(path);
                Check(path.Count > 15 && Vector3.Cross(path.Last() - path[0], oblique).magnitude < .01f, "Guide remains a straight general direction near Resonance");
                SaveFrame(cam, "04-resonance-guide");
                yield return Until(() => core.IsLaunched, .3f, "Resonance fixture auto-fires through normal held input");
                Input(light);
                yield return Until(() => core.ShotVelocity.x < 0f, 1.5f, "Physical shot follows the existing Resonance curved turnaround");
                Check(core.Body.angularVelocity.sqrMagnitude > .01f && !core.Body.isKinematic, "Resonance turnaround retains dynamic body and spin");
                pattern.magneticPath = ResonanceMagneticPath.StopAndReflect;
                yield return ResetCore(); yield return Enter(light); yield return Fire(light, .4f, Vector3.right);
                bool zeroSpeedFlight = false; float turnEnd = Time.time + 1f;
                while (Time.time < turnEnd && core.IsLaunched)
                {
                    if (core.ShotVelocity.magnitude < .05f) { zeroSpeedFlight = true; Check(core.IsPossessionBusy, "Magnetic zero-speed turnaround stays in flight and cannot be entered"); break; }
                    yield return null;
                }
                Check(zeroSpeedFlight, "Stop-and-reflect exercised its zero-speed point");
                Object.DestroyImmediate(patternGO);
            }

            yield return ResetCore(); yield return Enter(light);
            goalGO = new GameObject("Capture goal", typeof(SphereCollider), typeof(AmplifierGoalCapture));
            goalGO.transform.SetParent(scope.transform); goalGO.transform.position = Vector3.right * 4f;
            goal = goalGO.GetComponent<AmplifierGoalCapture>(); goal.GetComponent<Collider>().isTrigger = true;
            Set(goal, "teamID", 2); yield return Wait(.55f);
            yield return Fire(light, .3f, Vector3.right);
            yield return Until(() => core.HasBeenCaptured, .8f, "A launched Core still captures through the normal goal transaction");
            Check(service.GetTeamAmplifierMultiplier(2) == 2 && service.GetTeamAmplifierMultiplier(1) == 1, "Receiving goal team gets amplification regardless of shooter team");
            while (service.AdvanceTeamAmplifier(2)) { }
            goalGO.SetActive(false);
            yield return ResetCore(); yield return Enter(light);
            goalGO.SetActive(true);
            yield return Fire(light, .2f, Vector3.right);
            yield return Until(() => core.ShotVelocity.x < 0f, 1.4f, "Maxed goal repels a shot back into the field");
            Check(!core.HasBeenCaptured && service.IsTeamAmplifierMaxed(2), "Maxed goal cannot accept another multiplier");
            Object.DestroyImmediate(goalGO);
            yield return ResetCore();
            core.Possession.mode = AmplifierInteractionMode.Physical; yield return null; yield return null;
            Check(!core.Body.isKinematic && core.TryApplyBeamPush(Vector3.right, 20f, .1f), "Switching back restores original physical Core and beam response");
            Check(errors.Count == 0, "No runtime errors during possession scenarios");
        }
        finally
        {
            Object.DestroyImmediate(scope); Object.DestroyImmediate(serviceGO);
            foreach (var item in owned) if (item) Object.DestroyImmediate(item); owned.Clear();
        }
    }

    static IEnumerator DistanceChecks()
    {
        string savedPossession = JsonUtility.ToJson(core.Possession);
        float savedDrag = core.Effective_linearDrag;
        try
        {
            var settings = core.Possession;
            settings.distanceDrivenShots = true; settings.minimumRange = 5f; settings.maximumRange = 15f;
            settings.shotSpeed = 14f; settings.settledSpeed = 3f; settings.settlingSeconds = .3f;
            settings.maximumFlightSeconds = 15f; settings.finalBrakeDrag = 5f;
            Set(core, "linearDrag", 2.1f); core.ApplyTuning();
            foreach (float charge in new[] { 0f, .5f, 1f })
            {
                yield return ResetCore(); yield return Enter(light);
                // Fix charge exactly so frame duration cannot skew the requested distance.
                Set(core, "chargeSeconds", charge * settings.fullChargeSeconds);
                Set(core, "aimDirection", Vector3.right);
                Vector3 origin = core.Body.position;
                Invoke(core, "Launch");
                Check(Mathf.Abs(core.ShotVelocity.magnitude - 14f) < .001f, "Distance-driven shot starts at the same speed at every charge");
                yield return Until(() => !core.IsLaunched, 6f, "Distance-driven shot settles through gameplay physics");
                float distance = Vector3.Distance(origin, core.Body.position), expected = Mathf.Lerp(5f, 15f, charge);
                Check(Mathf.Abs(distance - expected) < .12f, $"Gameplay {charge:P0} charge travels {distance:0.000} of {expected:0.0} units");
                Check(core.Body.isKinematic && core.ShotVelocity.sqrMagnitude < .0001f, "Distance tuning preserves a clear stop and available Core");
            }
        }
        finally
        {
            JsonUtility.FromJsonOverwrite(savedPossession, core.Possession);
            Set(core, "linearDrag", savedDrag); core.ApplyTuning();
        }
    }
    static void SaveFrame(Camera camera, string name)
    {
        string directory = "C:/Users/mattf/.codex/visualizations/2026/09/07/01a07e08-a16a-7133-8c7e-374af73abfe7/" +
            "Core-possession-v3";
        System.IO.Directory.CreateDirectory(directory);
        var rt = new RenderTexture(1280, 720, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply(); System.IO.File.WriteAllBytes(directory + "/" + name + ".png", texture.EncodeToPNG()); }
        finally { camera.targetTexture = previous; RenderTexture.active = active; Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt); }
    }
}
#endif

