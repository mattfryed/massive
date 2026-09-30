#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Massive.Enemies;
using Massive.Resonance;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class RangedDroneValidation
{
    private static ResonancePatternController ProjectileResonanceBarrier()
    {
        var definition = ScriptableObject.CreateInstance<ResonancePatternDefinition>();
        definition.showCenter = false;
        definition.curves = new List<ResonanceCurve> { new ResonanceCurve { kind = ResonanceCurveKind.ClosedPolyline,
            points = new List<Vector2> { new Vector2(0, -4), new Vector2(0, 4) } } };
        definition.arcs = new List<ResonanceArc> { new ResonanceArc { startDegrees = 0, sweepDegrees = 180,
            behavior = ResonanceBehavior.HardWall, thickness = .8f, colliderThickness = .8f, endTaperFraction = 0f } };
        var go = new GameObject("Ranged validation — generated Resonance"); go.SetActive(false);
        go.transform.position = new Vector3(1.5f, 0, 0);
        var pattern = go.AddComponent<ResonancePatternController>();
        pattern.definition = definition;
        pattern.segmentMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resonance/Resonance Ribbon.mat");
        pattern.obstacleLayer = 11; pattern.interactionReach = .1f; pattern.sampleDegrees = 10f;
        pattern.globalVisualProfile.enabled = pattern.globalCollisionProfile.enabled = false;
        go.SetActive(true); pattern.Rebuild(); return pattern;
    }

    private static IEnumerator ResonanceProjectileChecks()
    {
        var pattern = ProjectileResonanceBarrier(); var second = ProjectileResonanceBarrier();
        yield return null; Physics.SyncTransforms();
        Check(pattern.InteractiveSegments.SelectMany(s => s.HardColliders).Any(c => c.enabled && !c.isTrigger),
            "Resonance fixture uses the real generated solid colliders");
        Check(Mathf.Approximately(ranged.projectilePrefab.resonanceSpeedMultiplier, .75f),
            "Projectile prefab exposes a Resonance speed slider defaulting to 75 percent");
        Check(ranged.ShouldHoldPosition(player), "Resonance does not obstruct the Ranged Drone's firing line of sight");
        ranged.TickAttack(.02f, player);
        Check(ranged.Phase == RangedDroneController.AttackPhase.Charging, "Drone can begin charging through Resonance without seeking a clear position");
        ranged.enabled = false; ranged.enabled = true;

        var shot = Object.Instantiate(ranged.projectilePrefab, new Vector3(.6f, 0, 0), Quaternion.LookRotation(Vector3.right));
        shot.Init(enemy, Vector3.right);
        float mass = player.massScore, previousAge = shot.ActiveAge, previousX = shot.transform.position.x;
        bool slowed = false, recovered = false; float deadline = Time.time + 3f;
        while (shot && shot.Phase == RangedDroneProjectile.ShotPhase.Flying && Time.time < deadline)
        {
            yield return null;
            if (!shot || shot.Phase != RangedDroneProjectile.ShotPhase.Flying || shot.ActiveAge == previousAge) continue;
            float x = shot.transform.position.x, speed = (x - previousX) / (shot.ActiveAge - previousAge);
            if (previousX > 1.25f && x < 1.75f) slowed |= Mathf.Abs(speed - shot.speed * .75f) < .02f;
            if (previousX > 2.05f && x < 2.45f) recovered |= Mathf.Abs(speed - shot.speed) < .02f;
            previousAge = shot.ActiveAge; previousX = x;
        }
        Check(slowed, "Bullet crosses overlapping Resonance colliders at 75 percent speed without stacking");
        Check(recovered, "Bullet resumes full speed after leaving Resonance");
        Check(shot && shot.Phase == RangedDroneProjectile.ShotPhase.Impact && Mathf.Abs(mass - player.massScore - shot.damageToPlayerMass01) < .001f,
            "Bullet passes through Resonance and still impacts/damages the player once");
        while (shot) yield return null;

        var probe = Object.Instantiate(ranged.projectilePrefab, new Vector3(1.5f, 0, 0), Quaternion.LookRotation(Vector3.right));
        probe.Init(enemy, Vector3.right); probe.resonanceSpeedMultiplier = .5f;
        float age = probe.ActiveAge, distance = probe.DistanceTravelled;
        while (probe.ActiveAge == age) yield return null;
        Check(Mathf.Abs((probe.DistanceTravelled - distance) / (probe.ActiveAge - age) - probe.speed * .5f) < .02f,
            "Changing the slider changes speed for a bullet born inside Resonance");
        pattern.SetInteractionEnabled(false); age = probe.ActiveAge; distance = probe.DistanceTravelled;
        while (probe.ActiveAge == age) yield return null;
        Check(Mathf.Abs((probe.DistanceTravelled - distance) / (probe.ActiveAge - age) - probe.speed * .5f) < .02f,
            "Removing one overlapping field preserves the remaining slowdown");
        second.SetInteractionEnabled(false); age = probe.ActiveAge; distance = probe.DistanceTravelled;
        while (probe.ActiveAge == age) yield return null;
        Check(Mathf.Abs((probe.DistanceTravelled - distance) / (probe.ActiveAge - age) - probe.speed) < .02f,
            "Disabling the final Resonance field immediately restores bullet speed");
        Object.Destroy(probe.gameObject); pattern.SetInteractionEnabled(true);

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer = 11;
        wall.transform.position = new Vector3(2.2f, 0, 0); wall.transform.localScale = new Vector3(.1f, 2, 3);
        Physics.SyncTransforms();
        Check(!ranged.ShouldHoldPosition(player), "A normal wall behind Resonance still blocks firing line of sight");
        var fast = Object.Instantiate(ranged.projectilePrefab, new Vector3(.6f, 0, 0), Quaternion.LookRotation(Vector3.right));
        fast.Init(enemy, Vector3.right); fast.speed = 100f; mass = player.massScore;
        deadline = Time.time + .3f;
        while (fast && fast.Phase == RangedDroneProjectile.ShotPhase.Flying && Time.time < deadline) yield return null;
        Check(fast && fast.Phase == RangedDroneProjectile.ShotPhase.Impact && fast.transform.position.x < 2.2f && Mathf.Approximately(mass, player.massScore),
            "A fast shot passes Resonance but still hits the solid wall behind it");
        Object.Destroy(fast.gameObject); Object.Destroy(wall);
        Object.Destroy(pattern.definition); Object.Destroy(second.definition);
        Object.Destroy(pattern.gameObject); Object.Destroy(second.gameObject);
        yield return null;
    }
}
#endif
