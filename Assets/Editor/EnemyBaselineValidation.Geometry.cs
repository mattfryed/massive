#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Massive.Enemies;
using Massive.PowerUps;
using UnityEngine;

public static partial class EnemyBaselineValidation
{
    private static void BeamChecks(ParticleBeamTurretController turret)
    {
        var visual = turret.beamVisual;
        var one = new ParticleAcceleratorBeamVisual.ThicknessRange(1f, 1f);
        visual.startThickness = visual.bodyThickness = visual.endThickness = one;
        visual.overallThickness = new ParticleAcceleratorBeamVisual.ThicknessRange(2f, 2f);
        visual.strandSeparation = 0f; visual.strandTaper = 0f; visual.thicknessWaveAmplitude = 0f;
        visual.curveAmplitude = 2f;
        Vector3 farthest = Vector3.zero; float maxOffset = 0f;
        for (int i = 10; i < 70; i++)
        {
            float distance = i * .1f;
            visual.SampleCollisionPath(turret.BeamOrigin, turret.BeamDirection, 8f, distance, .18f, turret.PhaseAge, out var center, out _);
            float offset = Mathf.Abs(center.x - turret.BeamOrigin.x);
            if (offset > maxOffset) { maxOffset = offset; farthest = center; }
        }
        var obstacle = Own(GameObject.CreatePrimitive(PrimitiveType.Cube)); obstacle.name = "Curved beam contact probe";
        obstacle.transform.localScale = new Vector3(.025f, 2f, .025f);
        obstacle.transform.position = farthest; Physics.SyncTransforms();
        Collider Trace(float radius)
        {
            object[] arguments = { 8f, radius, null };
            Call(turret, "TraceAnimatedBeam", arguments); return (Collider)arguments[2];
        }
        Check(maxOffset > .2f && Trace(.18f) == obstacle.GetComponent<Collider>(), "Curved beam hits a probe outside its straight axis");
        visual.curveAmplitude = 0f;
        Check(!Trace(.18f), "Removing the curve makes that off-axis probe miss");
        obstacle.transform.position = turret.BeamOrigin + turret.BeamDirection * 3f + Vector3.right * .15f;
        visual.overallThickness = one; Physics.SyncTransforms();
        Check(!Trace(.18f), "Narrow plasma misses a nearby off-axis probe");
        visual.overallThickness = new ParticleAcceleratorBeamVisual.ThicknessRange(4f, 4f);
        Check(Trace(.18f) == obstacle.GetComponent<Collider>(), "Increasing visual thickness expands collision reach");
        visual.overallThickness = new ParticleAcceleratorBeamVisual.ThicknessRange(0f, 0f);
        Check(!Trace(.18f), "Zero visual thickness has no damaging collision envelope");
        visual.overallThickness = one;
        obstacle.transform.position = turret.BeamOrigin + turret.BeamDirection * 2f;
        obstacle.transform.localScale = new Vector3(2f, 2f, .03f); Physics.SyncTransforms();
        object[] wallTrace = { 8f, .18f, null };
        float wallLength = (float)Call(turret, "TraceAnimatedBeam", wallTrace);
        Check((Collider)wallTrace[2] == obstacle.GetComponent<Collider>() && wallLength < 2.01f, "Thin solid wall stops the sampled beam at first contact");
        obstacle.SetActive(false);

        // Read the actual shader node buffer and compare every rendered node against the sampled damage envelope.
        visual.curveAmplitude = 1.7f; visual.strandSeparation = 1.2f; visual.strandTaper = 1f;
        visual.thicknessWaveAmplitude = .6f;
        visual.overallThickness = new ParticleAcceleratorBeamVisual.ThicknessRange(.8f, 1.4f);
        visual.startThickness = new ParticleAcceleratorBeamVisual.ThicknessRange(.2f, .6f);
        visual.bodyThickness = new ParticleAcceleratorBeamVisual.ThicknessRange(.8f, 1.5f);
        visual.endThickness = new ParticleAcceleratorBeamVisual.ThicknessRange(.5f, 1f);
        visual.SetClippedPath(turret.BeamOrigin, turret.BeamDirection, 8f, 6f, .18f, turret.PhaseAge);
        Call(visual, "LateUpdate");
        var block = new MaterialPropertyBlock(); visual.GetComponent<Renderer>().GetPropertyBlock(block);
        var nodes = block.GetVectorArray("_PlasmaNodes"); var sampling = block.GetVector("_PlasmaTubeSampling");
        int checkedNodes = 0;
        for (int strand = 0; strand < (int)sampling.x; strand++)
            for (int n = 0; n < (int)sampling.y; n++)
            {
                Vector4 node = nodes[strand * 65 + n]; float distance = n * sampling.z * visual.VolumeSizeWorld;
                visual.SampleCollisionPath(turret.BeamOrigin, turret.BeamDirection, 8f, distance, .18f, turret.PhaseAge, out var center, out float radius);
                Vector3 world = visual.transform.TransformPoint(new Vector3(node.x, node.y, node.z));
                if (node.w > .000001f && Vector3.Distance(world, center) + node.w * visual.VolumeSizeWorld > radius + .0002f)
                    throw new System.Exception("Rendered strand escaped the sampled collision envelope");
                checkedNodes++;
            }
        Check(checkedNodes > 100, "Rendered curved plasma nodes and collision share one width/path sampler (" + checkedNodes + " nodes)");
    }

    private static IEnumerator CarrierChecks()
    {
        var arena = Own(new GameObject("Boundary validation grid"));
        arena.SetActive(false);
        arena.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var grid = arena.AddComponent<VectorGridGPU>(); grid.size = new Vector2(28f, 12f);
        grid.enabled = false; // Bounds-only fixture; no GPU renderer or compute shader is required.
        var bounds = arena.AddComponent<ArenaBoundsFromVectorGrid>(); bounds.RefreshNow();
        arena.SetActive(true);
        var go = Prefab(CarrierPrototypeSetup.PrefabPath, Vector3.zero);
        var carrier = go.GetComponent<CarrierController>(); carrier.enabled = false;
        carrier.GetComponent<CarrierVisuals>().enabled = false;
        Field(carrier, "bounds", bounds);
        carrier.GetComponent<EnemyBase>().Init(carrier.definition, null);
        yield return null;
        Field(carrier, "bounds", bounds);
        carrier.shellCollider.enabled = true;
        int planned = 0, bent = 0, held = 0;
        var positions = new[] { Vector3.zero, new Vector3(12.7f, 0, 0), new Vector3(-12.7f, 0, 0),
            new Vector3(0, 0, 4.7f), new Vector3(0, 0, -4.7f), new Vector3(12.7f, 0, 4.7f),
            new Vector3(-12.7f, 0, 4.7f), new Vector3(12.7f, 0, -4.7f), new Vector3(-12.7f, 0, -4.7f) };
        DroneLaunchTrajectory chosen = default; Vector3 chosenStart = default, chosenCarrier = default;
        Quaternion chosenRotation = default;
        foreach (var position in positions)
            for (int yaw = 0; yaw < 60; yaw += 10)
            {
                go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0)); Physics.SyncTransforms();
                foreach (var dock in carrier.docks)
                {
                    if (!carrier.TryPlanLaunch(dock, carrier.droneDefinition.spawnRadiusWorld, out var path)) { held++; continue; }
                    planned++; if (Mathf.Abs(path.turnDegrees) > 1f) bent++;
                    Vector3 point = dock.position;
                    int steps = Mathf.CeilToInt(path.Duration / .005f); float dt = path.Duration / steps;
                    for (int i = 0; i < steps; i++)
                    {
                        point += path.Velocity((i + .5f) * dt) * dt;
                        if (!bounds.ContainsWorldPoint(point, carrier.droneDefinition.spawnRadiusWorld))
                            throw new System.Exception("Carrier departure crosses boundary at " + position + " / " + yaw);
                    }
                    if (Mathf.Abs(path.turnDegrees) > 80f && chosen.Duration == 0f)
                    { chosen = path; chosenStart = dock.position; chosenCarrier = position; chosenRotation = go.transform.rotation; }
                }
            }
        Check(planned > 100 && bent > 30 && held > 0, $"324 bay/rotation/wall/corner cases: {planned} safe plans, {bent} curved, {held} held for clearance");
        Check(chosen.Duration > 0f, "Near-wall departures include a substantial turn away from the boundary");
        go.transform.SetPositionAndRotation(chosenCarrier, chosenRotation); Physics.SyncTransforms();
        var droneObject = Prefab(DronePrototypeSetup.PrefabPath, chosenStart);
        droneObject.transform.rotation = Quaternion.LookRotation(chosen.direction, Vector3.up);
        var drone = droneObject.GetComponent<DroneController>();
        drone.GetComponent<EnemyBase>().Init(carrier.droneDefinition, null);
        drone.Launch(chosen, carrier.shellCollider);
        float until = Time.time + chosen.Duration; bool leftHull = false;
        float maximumTurn = 0f;
        while (Time.time < until)
        {
            yield return null;
            if (!bounds.ContainsWorldPoint(drone.transform.position, drone.bodyRadius)) throw new System.Exception("Live launch escaped arena");
            var bodyCollider = drone.GetComponent<CapsuleCollider>();
            bool overlap = Physics.ComputePenetration(bodyCollider, drone.transform.position, drone.transform.rotation,
                carrier.shellCollider, carrier.shellCollider.transform.position, carrier.shellCollider.transform.rotation, out _, out _);
            if (!overlap) leftHull = true;
            else if (leftHull) throw new System.Exception("Live curved launch re-entered Carrier shell");
            maximumTurn = Mathf.Max(maximumTurn, Vector3.Angle(chosen.direction, drone.GetComponent<Rigidbody>().linearVelocity));
        }
        Check(leftHull && maximumTurn > 45f, "Real Drone physics clears the shell and follows the boundary-away arc");
        droneObject.SetActive(false); go.SetActive(false); arena.SetActive(false);
    }
}
#endif
