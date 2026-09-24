#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Massive.Enemies;
using Massive.Player;
using UnityEngine;

/// <summary>Temporary live-physics NPC coverage. Never edits a scene or the shared profile asset.</summary>
public sealed class PlayerRepulsorCombatPlayValidationRunner : MonoBehaviour
{
    public static string Status { get; private set; } = "Idle";
    public static string Result { get; private set; } = "";
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<string> checks = new List<string>();
    readonly List<AttackStageType> stages = new List<AttackStageType>();
    readonly List<InputSnapshot> inputs = new List<InputSnapshot>();
    readonly List<GameObject> fixtures = new List<GameObject>();
    PlayerControllerScript player;
    PlayerAttackController attack;
    PlayerRepulsorAOE pulse;
    Rigidbody playerBody;
    PlayerAttackProfile originalProfile, testProfile;
    EnemyDefinition definition;
    EnemyBase inside, outside, weaponOwner, lateArrival;
    Transform weapon;
    Vector3 oldPosition, oldVelocity, oldAngularVelocity;
    Quaternion oldRotation;
    float oldTimeScale, beganAt, maximumPhysicalRadius;
    int oldCaptureRate, emissions, defeats;
    EnemyDefeatContext lastDefeat;
    bool saved, cleaned, lethalSection, hitWhileActive;

    struct InputSnapshot
    {
        public PlayerControllerScript player;
        public PlayerControlMode mode;
        public PlayerInputFrame frame;
        public bool hadFrame;
    }

    public static void StartValidation()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode before combat validation.");
        if (FindFirstObjectByType<PlayerRepulsorCombatPlayValidationRunner>() != null)
            throw new InvalidOperationException("A Repulsor combat validation is already running.");
        var runner = new GameObject("Repulsor NPC combat validation") { hideFlags = HideFlags.DontSave };
        runner.AddComponent<PlayerRepulsorCombatPlayValidationRunner>().Run();
    }

    public void Run() { Status = "Starting"; Result = ""; StartCoroutine(Guarded()); }

    IEnumerator Guarded()
    {
        IEnumerator routine = Validate();
        while (true)
        {
            object instruction = null;
            bool next;
            try
            {
                if (saved && Time.time - beganAt > 9.5f)
                    throw new InvalidOperationException("Validation exceeded 9.5 simulated seconds.");
                next = routine.MoveNext();
                if (next) instruction = routine.Current;
            }
            catch (Exception ex)
            {
                Status = "FAILED";
                Result = string.Join("\n", checks) + "\nFAIL: " + ex.GetBaseException().Message;
                Debug.LogError(Result);
                Cleanup(); Destroy(gameObject); yield break;
            }
            if (!next) break;
            yield return instruction;
        }
        Status = "PASSED"; Result = string.Join("\n", checks);
        Debug.Log(Result);
        Cleanup(); Destroy(gameObject);
    }

    IEnumerator Validate()
    {
        foreach (var candidate in FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None))
            if (candidate.isActiveAndEnabled && candidate.transform.parent && candidate.transform.parent.name == "Players" && candidate.playerID == 0)
                player = candidate;
        Require(player, "Active P1 was not found under Players.");
        attack = player.GetComponent<PlayerAttackController>();
        pulse = player.GetComponentInChildren<PlayerRepulsorAOE>();
        playerBody = player.GetComponent<Rigidbody>();
        Require(attack && pulse && playerBody && attack.Profile, "P1 is missing the attack controller, Repulsor, rigidbody or profile.");
        Require(!attack.IsAttacking && !player.temporarilyEliminated && !player.IsStunned && !player.IsExternallyStunned && !player.IsMatchInputLocked,
            "P1 must be idle, alive, unstunned and unlocked.");
        Require(attack.Profile.GetStage(2) != null && attack.Profile.GetStage(2).StageType == AttackStageType.FinisherRepulsor,
            "This combo fixture expects Repulsor at stage 3.");
        oldPosition = player.transform.position; oldRotation = player.transform.rotation;
        oldVelocity = playerBody.linearVelocity; oldAngularVelocity = playerBody.angularVelocity;
        oldTimeScale = Time.timeScale; oldCaptureRate = Time.captureFramerate;
        originalProfile = attack.Profile;
        saved = true; beganAt = Time.time;
        testProfile = Instantiate(originalProfile);
        testProfile.name = "Repulsor combat validation profile (temporary)";
        testProfile.hideFlags = HideFlags.DontSave;
        Set(testProfile.GetStage(2), "repulsorScale", 1.5f);
        Set(testProfile.GetStage(2), "repulsorEnemyDamage", 2.5f);
        Set(attack, "attackProfile", testProfile);
        definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        definition.hideFlags = HideFlags.DontSave;
        definition.healthMassEq = 10f;
        definition.damageTakenPerSwordHit = 0f;
        // Blank rewards keep validation from mutating live score, chains or award telemetry.
        definition.defeatRewardKey = "";
        foreach (var candidate in FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None))
        {
            if (!candidate.transform.parent || candidate.transform.parent.name != "Players") continue;
            inputs.Add(new InputSnapshot { player = candidate, mode = candidate.ControlMode,
                frame = Get<PlayerInputFrame>(candidate, "_scriptedInput"), hadFrame = Get<bool>(candidate, "_hasScriptedInput") });
            candidate.SetControlMode(PlayerControlMode.Disabled);
        }
        player.SetControlMode(PlayerControlMode.Scripted);
        player.ClearScriptedInput(); playerBody.linearVelocity = Vector3.zero;
        Time.timeScale = 1f; Time.captureFramerate = 60;
        attack.OnStageStarted.AddListener(OnStage);
        pulse.PulseStarted += OnPulse;
        Status = "Normal combo and actual NPC physics contacts";
        float settleUntil = Time.time + .2f;
        while (Time.time < settleUntil) yield return null;
        var press = PlayerInputFrame.Neutral; press.attackDown = true;
        player.SetScriptedInput(press);
        int nextCombo = 1;
        float deadline = Time.time + 4f;
        while (Time.time < deadline)
        {
            yield return null;
            SamplePhysics();
            var input = PlayerInputFrame.Neutral;
            if (nextCombo < 3 && attack.IsAttacking && attack.CurrentStageIndex == nextCombo - 1 && attack.StageNormalizedTime >= .86f)
            { input.attackDown = true; nextCombo++; }
            player.SetScriptedInput(input);
            if (stages.Count >= 3 && !attack.IsAttacking) break;
        }
        Check(stages.Count == 3 && stages[0] == AttackStageType.PrimaryLunge && stages[1] == AttackStageType.ComboSwipe && stages[2] == AttackStageType.FinisherRepulsor,
            "Normal three-press input completes thrust, sweep and Repulsor in order.");
        Check(emissions == 1 && inside && hitWhileActive, "The actual expanding trigger hits an NPC during its live damage window.");
        Check(Mathf.Abs(inside.HealthRemaining - 7.5f) < .001f, "Root and child hurtboxes take exactly 2.5 total damage (10 → 7.5), once per NPC.");
        Check(outside && Mathf.Abs(outside.HealthRemaining - 10f) < .001f && weaponOwner && Mathf.Abs(weaponOwner.HealthRemaining - 10f) < .001f,
            "Outside NPC and an overlapping weapon-only collider receive no damage.");
        float expected = testProfile.GetStage(2).GetRepulsorRadius(PlayerScaleAdjuster.SizeOf(player), pulse.StartRadiusWorld);
        Check(Mathf.Abs(pulse.EndRadiusWorld - expected) < .001f && maximumPhysicalRadius > expected * .9f,
            "Overall scale 1.5 composes with Player Size " + PlayerScaleAdjuster.SizeOf(player).ToString("F3") + "; physical max radius is " + pulse.EndRadiusWorld.ToString("F3") + ".");
        Check(!pulse.IsPulseActive && !pulse.GetComponent<SphereCollider>().enabled, "Completion disables the damaging collider.");
        lateArrival = MakeEnemy("Late arrival after live window", false);
        lateArrival.transform.position = pulse.OriginWorld + Vector3.forward * .3f;
        Physics.SyncTransforms();
        float residueEnd = Time.time + .65f;
        while (Time.time < residueEnd) yield return new WaitForFixedUpdate();
        Check(Mathf.Abs(lateArrival.HealthRemaining - 10f) < .001f && Mathf.Abs(inside.HealthRemaining - 7.5f) < .001f,
            "Lingering artwork causes no residual damage to existing or newly arriving enemies.");

        Status = "Isolated lethal Repulsor and defeat attribution";
        ClearFixtures(); lethalSection = true;
        Set(testProfile.GetStage(2), "repulsorEnemyDamage", 25f);
        // Direct stage entry only for this lifecycle/attribution test; the first section uses normal input.
        Invoke(attack, "StartStage", 2);
        deadline = Time.time + 1.5f;
        while ((attack.IsAttacking || defeats == 0) && Time.time < deadline) { SamplePhysics(); yield return null; }
        Check(defeats == 1 && inside && inside.IsDead && lastDefeat.creditedPlayer == player && lastDefeat.source == EnemyDamageSource.Repulsor,
            "Lethal damage emits one defeat attributed to P1 and the Repulsor damage source.");
        Check(!string.IsNullOrEmpty(lastDefeat.sourceLifeToken), "Defeat carries the enemy life token used for score deduplication.");
        Check(!pulse.IsPulseActive && !pulse.GetComponent<SphereCollider>().enabled, "Lethal pulse also ends with no live collider.");
        checks.Add("PASS: Profiles and NPC definitions were temporary; blank fixture rewards leave match scoring untouched. Score awarding itself is not exercised by this runner.");
    }

    void SamplePhysics()
    {
        if (!pulse || !pulse.IsPulseActive) return;
        maximumPhysicalRadius = Mathf.Max(maximumPhysicalRadius, pulse.RadiusWorld);
        if (inside && inside.HealthRemaining < 10f) hitWhileActive = true;
    }

    void OnStage(AttackStage stage)
    {
        stages.Add(stage.StageType);
        if (stage.StageType != AttackStageType.FinisherRepulsor) return;
        // Spawn after thrust/sweep, far away until the actual Repulsor release origin is known.
        inside = MakeEnemy(lethalSection ? "Lethal NPC fixture" : "Double-hurtbox NPC fixture", true);
        if (lethalSection) { inside.Defeated += OnDefeated; return; }
        outside = MakeEnemy("Outside NPC fixture", false);
        weaponOwner = MakeEnemy("Outside weapon owner fixture", false);
        var weaponObject = new GameObject("Weapon collider (no hurtbox)");
        weaponObject.transform.SetParent(weaponOwner.transform, false);
        weaponObject.layer = weaponOwner.gameObject.layer;
        var collider = weaponObject.AddComponent<SphereCollider>(); collider.radius = .1f; collider.isTrigger = true;
        weapon = weaponObject.transform;
    }

    EnemyBase MakeEnemy(string label, bool doubleHurtbox)
    {
        var root = new GameObject(label) { hideFlags = HideFlags.DontSave };
        root.SetActive(false); fixtures.Add(root);
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        root.layer = enemyLayer >= 0 ? enemyLayer : 0;
        root.transform.position = player.transform.position + Vector3.up * 100f;
        var rigidbody = root.AddComponent<Rigidbody>(); rigidbody.useGravity = false; rigidbody.isKinematic = true;
        var sphere = root.AddComponent<SphereCollider>(); sphere.radius = .09f; sphere.isTrigger = true;
        var enemy = root.AddComponent<EnemyBase>(); enemy.despawnDelaySeconds = 20f;
        root.AddComponent<EnemyHurtbox>();
        if (doubleHurtbox)
        {
            var child = new GameObject("Second body hurtbox"); child.transform.SetParent(root.transform, false); child.layer = root.layer;
            var childCollider = child.AddComponent<SphereCollider>(); childCollider.radius = .07f; childCollider.isTrigger = true;
            child.AddComponent<EnemyHurtbox>();
        }
        root.SetActive(true); enemy.Init(definition, null);
        // Normal player body and sword contact cannot contaminate this isolated AOE measurement.
        foreach (var fixtureCollider in root.GetComponentsInChildren<Collider>())
            foreach (var playerCollider in player.GetComponentsInChildren<Collider>(true))
                if (playerCollider != pulse.GetComponent<SphereCollider>()) Physics.IgnoreCollision(fixtureCollider, playerCollider, true);
        return enemy;
    }

    void OnPulse(PlayerRepulsorAOE source)
    {
        emissions++;
        float contactDistance = Mathf.Lerp(source.StartRadiusWorld, source.EndRadiusWorld, .6f);
        inside.transform.position = source.OriginWorld + Vector3.forward * contactDistance;
        if (!lethalSection)
        {
            outside.transform.position = source.OriginWorld + Vector3.back * (source.EndRadiusWorld + .6f);
            weaponOwner.transform.position = source.OriginWorld + Vector3.right * (source.EndRadiusWorld + .75f);
            weapon.position = source.OriginWorld + Vector3.right * contactDistance;
        }
        Physics.SyncTransforms();
    }

    void OnDefeated(EnemyDefeatContext context) { defeats++; lastDefeat = context; }
    void Check(bool condition, string message) { Require(condition, message); checks.Add("PASS: " + message); }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
    static void Invoke(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, PrivateInstance).Invoke(target, arguments);

    void ClearFixtures()
    {
        if (inside) inside.Defeated -= OnDefeated;
        foreach (var fixture in fixtures) if (fixture) { fixture.SetActive(false); Destroy(fixture); }
        fixtures.Clear(); inside = outside = weaponOwner = lateArrival = null; weapon = null;
    }

    void Cleanup()
    {
        if (cleaned) return; cleaned = true;
        if (attack) { attack.OnStageStarted.RemoveListener(OnStage); if (saved) attack.CancelAttack(); }
        if (pulse) pulse.PulseStarted -= OnPulse;
        ClearFixtures();
        if (attack && originalProfile) Set(attack, "attackProfile", originalProfile);
        if (testProfile) Destroy(testProfile);
        if (definition) Destroy(definition);
        if (!saved) return;
        Time.timeScale = oldTimeScale; Time.captureFramerate = oldCaptureRate;
        if (player)
        {
            player.transform.SetPositionAndRotation(oldPosition, oldRotation);
            if (playerBody) { playerBody.position = oldPosition; playerBody.rotation = oldRotation;
                playerBody.linearVelocity = oldVelocity; playerBody.angularVelocity = oldAngularVelocity; }
            var gridPulse = player.GetComponent<PlayerRepulsorGridPulse>();
            if (gridPulse) gridPulse.ClearPulses();
        }
        foreach (var snapshot in inputs)
        {
            if (!snapshot.player) continue;
            snapshot.player.ClearScriptedInput();
            if (snapshot.hadFrame) snapshot.player.SetScriptedInput(snapshot.frame);
            snapshot.player.SetControlMode(snapshot.mode);
        }
        Physics.SyncTransforms();
    }

    void OnDisable() { Cleanup(); }
    void OnDestroy() { Cleanup(); }
}
#endif
