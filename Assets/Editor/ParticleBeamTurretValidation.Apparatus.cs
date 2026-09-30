#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Massive.Enemies;
using UnityEngine;

public static partial class ParticleBeamTurretValidation
{
    private static void GeometryTopologyChecks(Vector3[] band)
    {
        void Topology(Vector3[] corners, int expectedVertices, int expectedEdges, int boundaryEdges, string label)
        {
            var vertices = new List<Vector3>(); var indices = new List<int>();
            foreach (var p in corners)
            {
                int index = vertices.FindIndex(v => (v - p).sqrMagnitude < .00000001f);
                if (index < 0) { index = vertices.Count; vertices.Add(p); }
                indices.Add(index);
            }
            var edges = new Dictionary<(int, int), int>();
            for (int i = 0; i < indices.Count; i++)
            {
                int a = indices[i], b = indices[i / 3 * 3 + (i + 1) % 3];
                var edge = (Mathf.Min(a, b), Mathf.Max(a, b));
                edges.TryGetValue(edge, out int uses); edges[edge] = uses + 1;
            }
            Check(vertices.Count == expectedVertices && edges.Count == expectedEdges && edges.Count(e => e.Value == 1) == boundaryEdges && edges.All(e => e.Value <= 2), label);
        }
        Topology(band, 16, 32, 16, "Sixteen triangles form a continuous base band with only the two octagon rims open");
        var dome = new Vector3[ParticleBeamTurretGeometry.DomeFaces * 3];
        for (int i = 0; i < ParticleBeamTurretGeometry.DomeFaces; i++) ParticleBeamTurretGeometry.DomeTriangle(i, 1f, dome, i * 3);
        Topology(dome, 26, 65, 10, "2V dome has 40 triangles, 26 vertices, 65 edges and one open decagonal rim");
        Check(dome.All(p => Mathf.Abs(p.magnitude - 1f) < .00001f && p.z <= .00001f),
            "Dome vertices lie on the requested sphere behind the core");
    }
    private static IEnumerator ApparatusShapeChecks()
    {
        float originalHeight = visual.baseHeight, originalRadius = visual.domeRadius;
        Vector3 originalOrigin = turret.BeamOrigin;
        visual.baseHeight *= 1.5f; yield return null; yield return null;
        Check(Mathf.Abs(Vector3.Dot(turret.BeamOrigin - originalOrigin, turret.transform.forward) - originalHeight * .5f) < .0001f,
            "Base Height moves the firing apparatus forward with the rim");
        Physics.SyncTransforms();
        var ray = new Ray(turret.transform.TransformPoint(new Vector3(0, 0, visual.baseHeight + 1f)), -turret.transform.forward);
        Check(turret.shellCollider.Raycast(ray, out var hit, 2f) && Mathf.Abs(turret.transform.InverseTransformPoint(hit.point).z - visual.baseHeight) < .001f,
            "Shell collision matches the edited base height");
        Check(Enumerable.Range(0, ParticleBeamTurretGeometry.BaseFaces).All(i =>
            Enumerable.Range(0, 3).All(j => { float z = turret.transform.InverseTransformPoint(visual.FacetCornerWorld(i, j)).z; return Mathf.Abs(z) < .0001f || Mathf.Abs(z - visual.baseHeight) < .0001f; })),
            "All base faces stay attached to the height-adjusted rims");
        visual.domeRadius *= 1.4f;
        Quaternion beforeRotation = turret.firingPivot.rotation;
        Vector3 baseCorner = visual.FacetCornerWorld(0, 0);
        turret.firingPivot.rotation *= Quaternion.Euler(0, 35f, 0); yield return null; yield return null;
        bool follows = true;
        for (int i = ParticleBeamTurretGeometry.BaseFaces + ParticleBeamTurretGeometry.RingFaces; i < ParticleBeamTurretGeometry.FaceCount; i++)
            for (int j = 0; j < 3; j++)
            {
                Vector3 p = turret.firingPivot.InverseTransformPoint(visual.FacetCornerWorld(i, j));
                follows &= Mathf.Abs(p.magnitude - visual.domeRadius) < .0001f && p.z < .0001f;
            }
        Check(follows && (visual.FacetCornerWorld(0, 0) - baseCorner).sqrMagnitude < .000001f,
            "Dome radius and pivot rotation update all dome panels while the wall base stays fixed");
        Capture("turret-apparatus-pivot.png", false);
        turret.firingPivot.rotation = beforeRotation; visual.baseHeight = originalHeight; visual.domeRadius = originalRadius;
        yield return null; yield return null;
    }
    private static IEnumerator ApparatusMotionChecks(float authoredFiringSpeed)
    {
        turret.chargeSeconds = .8f; turret.fireSeconds = 1.2f; turret.cooldownSeconds = .2f;
        turret.firingTrackingDegreesPerSecond = authoredFiringSpeed;
        Place(player, new Vector3(2.5f, 0, 3f));
        while (turret.Phase != ParticleBeamTurretController.AttackPhase.Charging) yield return null;
        while (turret.Charge01 < .85f) yield return null;
        Place(player, new Vector3(-2.5f, 0, 3f));
        while (turret.Phase != ParticleBeamTurretController.AttackPhase.Firing) yield return null;
        float startVelocity = turret.AimAngularVelocity;
        var direction = turret.BeamDirection;
        float until = Time.time + .55f;
        while (Time.time < until) yield return null;
        Check(Vector3.Angle(direction, turret.BeamDirection) > .2f && Mathf.Abs(turret.AimAngularVelocity) <= authoredFiringSpeed * 1.3f,
            "Moving blast eases down to the authored firing speed and continues following its target");
        Check(Mathf.Abs(startVelocity) > Mathf.Abs(turret.AimAngularVelocity),
            "Transition into firing retains and smoothly brakes the prior tracking motion");
        Place(player, Vector3.back * 3f);
        float moving = Mathf.Abs(turret.AimAngularVelocity);
        until = Time.time + .08f; while (Time.time < until) yield return null;
        Check(Mathf.Abs(turret.AimAngularVelocity) < moving && Mathf.Abs(turret.AimAngularVelocity) > .001f,
            "Target loss eases firing motion toward rest instead of snapping the angular speed to zero");
        while (turret.Phase == ParticleBeamTurretController.AttackPhase.Firing) yield return null;
        until = Time.time + 1.4f; while (Time.time < until) yield return null;
        Check(Mathf.Abs(turret.AimAngularVelocity) < .05f, "Tracking settles to rest after target loss");
        turret.firingTrackingDegreesPerSecond = 0f;
        turret.firingPivot.rotation = turret.transform.rotation;
    }
}
#endif
