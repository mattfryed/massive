#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Massive.Enemies;
using Massive.Resonance;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class DronePrototypeValidation
{
    private static ResonancePatternController SludgePattern()
    {
        var def = Own(ScriptableObject.CreateInstance<ResonancePatternDefinition>());
        def.showCenter = false;
        def.curves = new List<ResonanceCurve> { new ResonanceCurve { kind = ResonanceCurveKind.ClosedPolyline,
            points = new List<Vector2> { new Vector2(0, -4), new Vector2(0, 4) } } };
        def.arcs = new List<ResonanceArc> { new ResonanceArc { startDegrees = 0, sweepDegrees = 180,
            behavior = ResonanceBehavior.HardWall, thickness = .4f, colliderThickness = .3f, endTaperFraction = 0f } };
        var go = Own(new GameObject("Validation sludge barrier")); go.SetActive(false);
        var pattern = go.AddComponent<ResonancePatternController>();
        pattern.definition = def; pattern.segmentMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resonance/Resonance Ribbon.mat");
        pattern.obstacleLayer = 11; pattern.fieldAffectedLayers = ~0; pattern.playerResponse = ResonancePlayerResponse.Sludge;
        pattern.sludgeMovementMultiplier = .65f; pattern.sludgeDrag = 8; pattern.interactionReach = .1f;
        pattern.sampleDegrees = 10f; pattern.globalVisualProfile.enabled = pattern.globalCollisionProfile.enabled = false;
        go.SetActive(true); pattern.Rebuild(); return pattern;
    }

    private static IEnumerator ResonanceSludgeChecks(PlayerControllerScript p1, PlayerControllerScript p2)
    {
        Place(p1, new Vector3(8, 0, 0)); Place(p2, new Vector3(12, 0, 0));
        var pattern = SludgePattern(); yield return .08f;
        var walls = pattern.InteractiveSegments.SelectMany(s => s.HardColliders).ToArray();
        Check(pattern.enemiesUseSludge && walls.Length > 0 && walls.All(c => !c.isTrigger), "Sludge preserves generated solid Resonance geometry for other actors");
        foreach (bool dyson in new[] { false, true })
        {
            EnemyBase enemy; DroneController drone = null; DysonSphereController sphere = null;
            if (!dyson)
            {
                drone = Drone(new Vector3(-2, 0, 0)); drone.spawnGraceSeconds = 0; drone.detectionRange = 20; drone.disengageRange = 22;
                enemy = drone.GetComponent<EnemyBase>();
            }
            else
            {
                var go = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy System/Enemy Types/Melee/DysonSphere/Enemy_DysonSphere.prefab"), new Vector3(-2, 0, 0), Quaternion.identity));
                enemy = go.GetComponent<EnemyBase>();
                enemy.Init(AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Enemy System/Enemy Types/Melee/DysonSphere/ED_DysonSphere.asset"), null);
                sphere = go.GetComponent<DysonSphereController>();
                var data = new SerializedObject(sphere); data.FindProperty("lockMovementDuringSpawn").boolValue = false;
                data.FindProperty("attackTriggerDistance").floatValue = .1f; data.FindProperty("chaseSpeedOverride").floatValue = 2.4f;
                data.FindProperty("ignoreTargetsFartherThan").floatValue = 20f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var body = enemy.GetComponent<Rigidbody>(); var avoidance = enemy.GetComponent<EnemyObstacleAvoidance>();
            avoidance.ObstacleMask = LayerMask.GetMask("Obstacle"); Physics.SyncTransforms();
            Check(!avoidance.HasObstacleInDirection(Vector3.right, 4, out _), "Resonance does not block enemy steering/attack line of sight: " + dyson);
            var ordinaryWall = Own(GameObject.CreatePrimitive(PrimitiveType.Cube)); ordinaryWall.layer = 11;
            ordinaryWall.transform.position = new Vector3(1.5f, 0, 0); ordinaryWall.transform.localScale = new Vector3(.1f, 2, 5);
            Physics.SyncTransforms();
            Check(avoidance.HasObstacleInDirection(Vector3.right, 5, out var hit) && hit.collider == ordinaryWall.GetComponent<Collider>(), "A solid wall behind Resonance still blocks the query: " + dyson);
            Object.Destroy(ordinaryWall); yield return .08f;
            if (dyson) Check(walls.All(c => Physics.GetIgnoreCollision(enemy.GetComponent<Collider>(), c)), "Dyson physical collision pairs ignore Resonance");
            bool slowed = false, recovered = false; float maxSide = 0f, deadline = Time.time + 5f;
            while (Time.time < deadline && body.position.x < 2f)
            {
                yield return .04f;
                maxSide = Mathf.Max(maxSide, Mathf.Abs(body.position.z));
                if (enemy.ExternalMovementMultiplier < .8f && body.linearVelocity.magnitude < 2f) slowed = true;
                if (slowed && enemy.ExternalMovementMultiplier > .99f && body.linearVelocity.magnitude > 2.1f) recovered = true;
            }
            Check(body.position.x >= 2f && maxSide < .2f && slowed && recovered,
                "Enemy crosses straight through sludge, slows inside, and regains speed outside: dyson=" + dyson + " pos=" + body.position + " slow=" + slowed + " recovered=" + recovered);

            // Pause/retire AI only, leaving its real body available to the shared driver.
            if (drone != null) drone.enabled = false; if (sphere != null) sphere.enabled = false;
            body.position = new Vector3(0, 0, 2); body.linearVelocity = Vector3.zero;
            enemy.transform.position = body.position; Physics.SyncTransforms(); yield return .08f;
            Check(Mathf.Abs(enemy.ExternalMovementMultiplier - .65f) < .001f, "Enemy receives authored full-contact multiplier: " + dyson);
            if (dyson)
            {
                var data = new SerializedObject(sphere); float lungeSpeed = data.FindProperty("lungeSpeed").floatValue;
                sphere.SendMessage("BeginLunge"); sphere.SendMessage("TickLunge");
                Check(Mathf.Abs(body.linearVelocity.magnitude - lungeSpeed * .65f) < .01f, "Dyson lunge also respects the sludge multiplier");
                body.linearVelocity = Vector3.zero;
            }
            pattern.SetInteractionEnabled(false);
            Check(enemy.ExternalMovementMultiplier == 1f, "Formation/hidden gate immediately clears enemy sludge: " + dyson);
            if (dyson) Check(walls.All(c => !Physics.GetIgnoreCollision(enemy.GetComponent<Collider>(), c)), "Disabling the field restores physical collision pairs");
            pattern.SetInteractionEnabled(true); yield return .08f;
            Check(enemy.ExternalMovementMultiplier < .8f, "Interaction reenable reapplies sludge: " + dyson);
            enemy.Pause(true); yield return .08f; Check(enemy.ExternalMovementMultiplier == 1f, "Paused enemy carries no stale slowdown: " + dyson);
            enemy.Pause(false); yield return .08f;
            var second = SludgePattern(); second.sludgeMovementMultiplier = .8f; yield return .08f;
            Check(Mathf.Abs(enemy.ExternalMovementMultiplier - .65f) < .001f, "Overlapping pattern speed influences choose the strongest: " + dyson);
            pattern.SetInteractionEnabled(false); yield return .08f;
            Check(Mathf.Abs(enemy.ExternalMovementMultiplier - .8f) < .001f, "Removing one field preserves another field's slowdown: " + dyson);
            second.SetInteractionEnabled(false); Object.Destroy(second.gameObject);
            Check(enemy.ExternalMovementMultiplier == 1f, "Removing the final field restores movement: " + dyson);
            Object.Destroy(enemy.gameObject); pattern.SetInteractionEnabled(true); yield return .08f;
        }
        // The player path still uses the same tuning after extending the actor driver.
        Place(p1, Vector3.zero); yield return .08f;
        Check(Mathf.Abs(p1.ExternalMovementMultiplier - .65f) < .001f, "Existing player sludge retains the shared authored multiplier");
        pattern.enemiesUseSludge = false; var probe = Drone(new Vector3(-2, 0, 0), false);
        Check(probe.GetComponent<EnemyObstacleAvoidance>().ShouldAvoid(walls[0]), "Turning enemy sludge off restores Resonance avoidance");
        Remove(probe); Object.Destroy(pattern.gameObject); yield return .08f;
        Check(p1.ExternalMovementMultiplier == 1f, "Removing Resonance clears player slowdown too");
        Place(p1, new Vector3(-12, 0, 0));
    }
}
#endif
