#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Massive.Multiplier;
using Massive.Player;
using Massive.PowerUps;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Isolated Play Mode fixtures: real trigger delivery and sustained beam traces.</summary>
[InitializeOnLoad]
public static class AmplifierAttackContactValidation
{
    const string Key = "MASSIVE.AmplifierAttackContactValidation";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator routine;
    static GameObject scope;
    static PlayerControllerScript player;
    static PlayerAttackController attack;
    static PlayerMelee melee;
    static PlayerRepulsorAOE pulse;
    static PlayerAttackProfile profile;
    static ParticleAcceleratorPowerUpDefinition beamSettings;
    static readonly List<string> checks = new List<string>();
    static int frame;
    static double deadline;
    public static string Result => SessionState.GetString(Key + ".Result", "Not run");
    static AmplifierAttackContactValidation() { EditorApplication.playModeStateChanged += State; }

    [MenuItem("MASSIVE/Amplifier/Validate Attack and Beam Contacts")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run in Edit Mode after compilation.");
        SessionState.SetString(Key + ".Result", "RUNNING");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); frame = -1; deadline = EditorApplication.timeSinceStartup + 120;
            routine = Checks(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; SessionState.SetBool(Key, false);
            if (routine != null) { routine = null; SessionState.SetString(Key + ".Result", "INTERRUPTED"); }
        }
    }

    static void Tick()
    {
        if (routine == null || !EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out");
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e.GetBaseException()); }
    }

    static void Finish(Exception failure)
    {
        routine = null; EditorApplication.update -= Tick;
        if (scope) Object.Destroy(scope);
        if (profile) Object.Destroy(profile);
        if (beamSettings) Object.Destroy(beamSettings);
        string result = (failure == null ? "PASSED" : "FAILED: " + failure.Message) + "\n" + string.Join("\n", checks);
        SessionState.SetString(Key + ".Result", result);
        if (failure == null) Debug.Log("AMPLIFIER CONTACT VALIDATION " + result);
        else Debug.LogError("AMPLIFIER CONTACT VALIDATION " + result);
        EditorApplication.isPlaying = false;
    }

    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        checks.Add("PASS " + label);
        SessionState.SetString(Key + ".Result", "RUNNING\n" + string.Join("\n", checks));
    }

    static void Set(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, Hidden);
        if (field == null) throw new Exception("Missing test fixture field: " + name);
        field.SetValue(target, value);
    }

    static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Hidden).Invoke(target, args);

    static AmplifierCoreGameplay Core(Vector3 offset)
    {
        var go = new GameObject("Contact test Core"); go.SetActive(false);
        go.transform.SetParent(scope.transform, false); go.transform.localPosition = offset;
        var body = go.AddComponent<Rigidbody>(); body.useGravity = false;
        var shape = go.AddComponent<SphereCollider>(); shape.radius = .08f;
        var core = go.AddComponent<AmplifierCoreGameplay>(); core.UseSharedSettings = false;
        Set(core, "playSpawnAnimationOnEnable", false);
        Set(core, "attackImpulse", 2f); Set(core, "mass", 1f);
        Set(core, "linearDrag", 0f); Set(core, "angularDrag", 0f);
        Set(core, "coreScale", 1f); Set(core, "maximumPlanarSpeed", 0f);
        go.SetActive(true); core.CompleteSpawnImmediately(); core.ApplyTuning();
        Physics.SyncTransforms(); return core;
    }

    static void Place(AmplifierCoreGameplay core, Vector3 position)
    {
        core.Body.position = position; core.transform.position = position;
        core.Body.linearVelocity = Vector3.zero; core.Body.angularVelocity = Vector3.zero;
        Physics.SyncTransforms();
    }

    static void Stage(int index)
    {
        attack.CancelAttack(true);
        attack.SetAimDirection(Vector3.right); attack.ExternalMoveInput = Vector2.right;
        Call(attack, "StartStage", index);
    }

    static IEnumerator Checks()
    {
        scope = new GameObject("Amplifier contact isolated fixtures");
        scope.SetActive(false); scope.transform.position = new Vector3(5000, 0, 5000);
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), scope.transform);
        actor.transform.localPosition = Vector3.zero;
        player = actor.GetComponent<PlayerControllerScript>(); player.ConfigureDemonstration(scope.transform, 0, 1);
        player.SetControlMode(PlayerControlMode.Disabled);
        attack = actor.GetComponent<PlayerAttackController>(); attack.UseSharedSettings = false;
        profile = Object.Instantiate(attack.Profile);
        Set(attack, "attackProfile", profile); Set(attack, "lockOnEnabled", false);
        foreach (var stage in profile.Stages)
        {
            Set(stage, "duration", .9f); Set(stage, "activationFrameStart", 6f);
            Set(stage, "activationFrameEnd", 42f); Set(stage, "travelDistance", 0f);
            Set(stage, "rotationArc", 0f); Set(stage, "repulsorMaxRadius", 3f);
        }
        melee = actor.GetComponentInChildren<PlayerMelee>(true);
        pulse = actor.GetComponentInChildren<PlayerRepulsorAOE>(true);
        scope.SetActive(true);
        yield return null; yield return null;
        player.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
        // Only attack triggers can reach these Cores; no passive player-body push.
        foreach (var collider in actor.GetComponentsInChildren<Collider>(true))
            if (collider != melee.TuningHitbox && collider != pulse.GetComponent<Collider>()) collider.enabled = false;

        Stage(0);
        float until = Time.time + .6f;
        while (!melee.TuningHitbox.enabled && Time.time < until) yield return null;
        Check(melee.TuningHitbox.enabled, "Thrust hitbox opens");
        var thrust = Core(Vector3.zero); Place(thrust, melee.TuningHitbox.bounds.center);
        until = Time.time + .3f;
        while (thrust.Body.linearVelocity.sqrMagnitude < .01f && Time.time < until) yield return null;
        Check(thrust.Body.linearVelocity.x > 1f && !attack.IsAttacking,
            "Thrust trigger launches the Core and retains solid-impact stop");
        Object.Destroy(thrust.gameObject);
        until = Time.time + .3f; while (Time.time < until) yield return null;

        Stage(1);
        var sweep = Core(Vector3.zero); Place(sweep, melee.TuningHitbox.bounds.center);
        Check(!sweep.TryApplyAttackImpact(attack), "Sweep windup cannot add attack force");
        until = Time.time + .6f;
        while (!melee.TuningHitbox.enabled && Time.time < until) yield return null;
        Place(sweep, melee.TuningHitbox.bounds.center);
        until = Time.time + .25f;
        while (sweep.Body.linearVelocity.sqrMagnitude < .01f && Time.time < until) yield return null;
        Check(sweep.Body.linearVelocity.x > 1f && attack.IsAttacking,
            "Sweep trigger launches the Core without cancelling the arc");
        float firstSpeed = sweep.Body.linearVelocity.x;
        until = Time.time + .2f;
        while (Time.time < until)
        {
            // Hold position in the trigger, but retain velocity to expose repeat impulses.
            sweep.Body.position = melee.TuningHitbox.bounds.center;
            Physics.SyncTransforms(); yield return null;
        }
        Check(Mathf.Abs(sweep.Body.linearVelocity.x - firstSpeed) < .02f,
            "Sweep stay callbacks do not repeat the launch after the lockout");
        attack.CancelAttack(true); Place(sweep, melee.TuningHitbox.bounds.center);
        until = Time.time + .12f; while (Time.time < until) yield return null;
        Check(sweep.Body.linearVelocity.sqrMagnitude < .0001f,
            "Cancelled Sweep and lingering visuals cannot push");
        Object.Destroy(sweep.gameObject);

        Stage(2);
        var north = Core(Vector3.forward * .8f);
        var south = Core(Vector3.back * .8f);
        until = Time.time + .8f;
        while ((north.Body.linearVelocity.sqrMagnitude < .01f || south.Body.linearVelocity.sqrMagnitude < .01f) && Time.time < until)
            yield return null;
        Check(north.Body.linearVelocity.z > 1f && south.Body.linearVelocity.z < -1f &&
            Mathf.Abs(north.Body.linearVelocity.x) < .05f && attack.IsAttacking,
            "Repulsor physically pushes both sides outward instead of using aim direction");
        Vector3 firstNorth = north.Body.linearVelocity;
        until = Time.time + .15f; while (Time.time < until) yield return null;
        Check((north.Body.linearVelocity - firstNorth).sqrMagnitude < .001f,
            "Repulsor only launches each Core once per pulse");
        attack.CancelAttack(true);
        var late = Core(Vector3.forward * .7f);
        until = Time.time + .15f; while (Time.time < until) yield return null;
        Check(late.Body.linearVelocity.sqrMagnitude < .0001f && !pulse.IsPulseActive,
            "Cancelled Repulsor has no residual Core force");
        Object.Destroy(north.gameObject); Object.Destroy(south.gameObject); Object.Destroy(late.gameObject);

        beamSettings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ParticleAcceleratorPowerUpDefinition>(
            ParticleAcceleratorBeamSetup.DefinitionPath));
        Check(beamSettings.amplifierPushForce > 0f, "Existing Accelerator asset inherits a nonzero push default");
        beamSettings.beamCollisionMask = ~0;
        var beam = Object.Instantiate(beamSettings.sustainedBeamPrefab, scope.transform); beam.Initialize(player, beamSettings);
        var target = Core(Vector3.right * 4);
        Vector3 origin = scope.transform.position + Vector3.right + Vector3.up * .05f;
        var wall = new GameObject("Beam blocker");
        wall.transform.SetParent(scope.transform, false); wall.transform.localPosition = Vector3.right * 2;
        var wallShape = wall.AddComponent<BoxCollider>(); wallShape.size = new Vector3(.2f, 2, 2);
        Physics.SyncTransforms(); beam.Begin(origin, Vector3.right);
        for (int n = 0; n < 20; n++) beam.Advance(.02f, origin, Vector3.right);
        until = Time.time + .08f; while (Time.time < until) yield return null;
        Check(beam.Contact == wallShape && target.Body.linearVelocity.sqrMagnitude < .0001f,
            "Beam is blocked by the wall and cannot push a Core behind it");
        Object.Destroy(wall); yield return null; Physics.SyncTransforms();
        for (int n = 0; n < 20; n++) beam.Advance(.02f, origin, Vector3.right);
        Check(beam.Contact == target.GetComponent<Collider>(), "Actual beam trace reaches the Core");
        until = Time.time + .08f; while (Time.time < until) yield return null;
        // The real plasma centerline warbles, so its arriving tangent can include lateral force.
        Check(target.Body.linearVelocity.x > .5f &&
            Vector3.Dot(target.Body.linearVelocity.normalized, Vector3.right) > .95f &&
            Mathf.Abs(target.Body.linearVelocity.y) < .02f,
            "Sustained plasma contact pushes the Core predominantly forward on the playing plane");
        beam.Stop(); Place(target, scope.transform.position + Vector3.right * 4);
        for (int n = 0; n < 10; n++) beam.Advance(.02f, origin, Vector3.right);
        until = Time.time + .08f; while (Time.time < until) yield return null;
        Check(target.Body.linearVelocity.sqrMagnitude < .0001f, "Retracting beam visuals apply no force");

        beamSettings.amplifierPushForce = 0; beam.Begin(origin, Vector3.right);
        for (int n = 0; n < 20; n++) beam.Advance(.02f, origin, Vector3.right);
        until = Time.time + .08f; while (Time.time < until) yield return null;
        Check(beam.Contact == target.GetComponent<Collider>() && target.Body.linearVelocity.sqrMagnitude < .0001f,
            "Zero beam push preserves blocking without moving the Core");
        // Fix the path for the integration comparison; changing animated tangents legitimately
        // changes the resulting velocity and is already exercised by the real plasma test above.
        beam.GetComponentInChildren<ParticleAcceleratorBeamVisual>(true).plasmaLayers = false;
        beamSettings.amplifierPushForce = 10;
        for (int n = 0; n < 20; n++) beam.Advance(.01f, origin, Vector3.right);
        until = Time.time + .06f; while (Time.time < until) yield return null;
        float fineSpeed = target.Body.linearVelocity.x;
        Place(target, scope.transform.position + Vector3.right * 4);
        for (int n = 0; n < 5; n++) beam.Advance(.04f, origin, Vector3.right);
        until = Time.time + .06f; while (Time.time < until) yield return null;
        Check(fineSpeed > 1f && Mathf.Abs(target.Body.linearVelocity.x - fineSpeed) < .025f,
            "Equal beam contact time produces equal impulse at 25 and 100 samples/second");
        beam.Stop(); target.gameObject.SetActive(false);
        Check(!target.TryApplyBeamPush(Vector3.right, 10, .1f) &&
            !target.TryApplyRepulsorImpact(origin, Vector3.right), "Inactive Core rejects force");
        target.gameObject.SetActive(true); target.SetTreatmentPreview();
        Check(!target.TryApplyBeamPush(Vector3.right, 10, .1f) &&
            !target.TryApplyRepulsorImpact(origin, Vector3.right), "Presentation-only Core rejects force");
    }
}
#endif
