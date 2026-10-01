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
                Vector3 p = turret.firingPivot.InverseTransformPoint(visual.FacetCornerWorld(i, j)) - Vector3.forward * visual.DomeOffset;
                follows &= Mathf.Abs(p.magnitude - visual.domeRadius) < .0001f && p.z < .0001f;
            }
        Check(follows && (visual.FacetCornerWorld(0, 0) - baseCorner).sqrMagnitude < .000001f,
            "Dome radius and pivot rotation update all dome panels while the wall base stays fixed");
        Capture("turret-apparatus-pivot.png", false);
        turret.firingPivot.rotation = beforeRotation; visual.baseHeight = originalHeight; visual.domeRadius = originalRadius;
        yield return null; yield return null;
        var motion = FloatingApparatusChecks(); while (motion.MoveNext()) yield return motion.Current;
    }
    private static IEnumerator ArrivalApparatusChecks()
    {
        var warning = turret.SpawnWarning;
        var outline = warning.GetComponent<MeshFilter>().sharedMesh;
        var pose = new Vector3[ParticleBeamTurretGeometry.FaceCount * 3]; visual.CopyPoseCorners(pose);
        var vertices = outline.vertices;
        Check(vertices.Length == pose.Length * 4 && Enumerable.Range(0, pose.Length).All(i => (vertices[i * 4] - pose[i]).sqrMagnitude < .000001f),
            "Arrival warning includes the full base, eight offset triangles and forty dome faces at the instance dimensions");
        Check(turret.spawnTelegraphPrefab.GetComponent<MeshFilter>().sharedMesh.vertexCount == pose.Length * 4,
            "Saved warning prefab includes the complete firing apparatus");
        while (turret.SpawnAge < .75f) yield return null;
        Check(warning.Opacity > .9f && warning.VisibleGhostCount > 0 && warning.DiffuseGlowOpacity > 0f &&
            warning.GetComponentsInChildren<MeshFilter>().Where(f => f.name.StartsWith("Outward ghost")).All(f => f.sharedMesh == outline),
            "Full-shape warning and outward ghosts animate with the shared spawn glow");
        Capture("turret-full-spawn-warning.png", false);
    }
    private static IEnumerator FloatingApparatusChecks()
    {
        int firstRing = ParticleBeamTurretGeometry.BaseFaces * 3;
        int firstDome = (ParticleBeamTurretGeometry.BaseFaces + ParticleBeamTurretGeometry.RingFaces) * 3;
        float offset = visual.panelFloatOffset, amplitude = visual.domeVibrationAmplitude, frequency = visual.domeVibrationFrequency;
        Vector3 corePosition = visual.core.transform.position;
        var before = new Vector3[ParticleBeamTurretGeometry.FaceCount * 3]; var after = new Vector3[before.Length];
        visual.CopyPoseCorners(before); visual.panelFloatOffset += .2f; visual.CopyPoseCorners(after);
        bool intact = true, translated = true;
        Vector3 axialDelta = visual.transform.InverseTransformVector(visual.firingPivot.TransformVector(Vector3.forward * .2f));
        for (int i = firstRing; i < firstDome; i += 3)
        {
            Vector3 delta = after[i] - before[i];
            translated &= (delta - axialDelta).sqrMagnitude < .00000001f;
            for (int j = 0; j < 3; j++) intact &= (after[i + j] - before[i + j] - delta).sqrMagnitude < .000001f;
        }
        Check(intact && translated && Enumerable.Range(0, before.Length).Where(i => i < firstRing || i >= firstDome).All(i => before[i] == after[i]),
            "Panel Float Offset slides the complete ring along the beam without radial movement, distortion or base/dome movement");
        // A second size/offset configuration must flow into runtime warnings, including ghosts.
        var owned = visual.CreateSpawnOutline();
        var probe = Object.Instantiate(turret.spawnTelegraphPrefab);
        probe.Begin(1f, Vector3.one, owned);
        var joined = probe.GetComponent<MeshFilter>().sharedMesh;
        Check(Enumerable.Range(0, after.Length).All(i => (joined.vertices[i * 4] - after[i]).sqrMagnitude < .000001f),
            "Per-instance triangle clearance updates the warning without editing its shared mesh asset");
        probe.Cancel(); yield return null; yield return null;
        Check(!owned && !joined, "Cancelling a generated warning releases its outline and joined mesh");
        visual.panelFloatOffset = offset;
        Quaternion rotation = visual.firingPivot.localRotation;
        visual.firingPivot.localRotation *= Quaternion.Euler(0f, 35f, 0f);
        Check(RingOffsetStaysAxial(), "Spinning ring offset follows the aimed beam axis and preserves its radius at a rotated pivot");
        visual.firingPivot.localRotation = rotation;
        visual.ApplyDimensions(); float hurtRadius = ((CapsuleCollider)turret.damageTrigger).radius;
        visual.panelFloatOffset += 2f; visual.ApplyDimensions();
        Check(((CapsuleCollider)turret.damageTrigger).radius == hurtRadius, "Increasing axial ring offset does not widen the hurtbox");
        visual.panelFloatOffset = offset; visual.ApplyDimensions();
        visual.domeVibrationAmplitude = .06f; visual.domeVibrationFrequency = 4f;
        float min = 1f, max = -1f, until = Time.time + .4f;
        bool axial = true;
        while (Time.time < until)
        {
            yield return null;
            min = Mathf.Min(min, visual.DomeOffset); max = Mathf.Max(max, visual.DomeOffset);
            visual.CopyPoseCorners(before); visual.CopyPoseCorners(after, true);
            Vector3 expected = turret.transform.InverseTransformVector(turret.firingPivot.TransformVector(Vector3.forward * visual.DomeOffset));
            for (int i = firstDome; i < before.Length; i++) axial &= (after[i] - before[i] - expected).sqrMagnitude < .000001f;
        }
        Check(axial && min < -.04f && max > .04f && min >= -.06001f && max <= .06001f && visual.core.transform.position == corePosition,
            "Dome vibrates both toward and away from the fixed core as one rigid shell within its amplitude limit");
        turret.sceneDirector.enabled = false; float frozen = visual.DomeOffset;
        until = Time.time + .15f; while (Time.time < until) yield return null;
        Check(visual.DomeOffset == frozen, "Director pause freezes dome vibration");
        turret.sceneDirector.enabled = true;
        visual.domeVibrationAmplitude = 0f; yield return null; yield return null;
        Check(visual.DomeOffset == 0f, "Zero dome amplitude restores a stationary backing shell");
        visual.domeVibrationAmplitude = amplitude; visual.domeVibrationFrequency = frequency;
    }
    private static bool RingOffsetStaysAxial()
    {
        var before = new Vector3[ParticleBeamTurretGeometry.FaceCount * 3]; var after = new Vector3[before.Length];
        float offset = visual.panelFloatOffset;
        try
        {
            visual.CopyPoseCorners(before, true); visual.panelFloatOffset += .2f; visual.CopyPoseCorners(after, true);
            int first = ParticleBeamTurretGeometry.BaseFaces * 3, end = first + ParticleBeamTurretGeometry.RingFaces * 3;
            for (int i = 0; i < before.Length; i++)
            {
                Vector3 delta = visual.firingPivot.InverseTransformVector(visual.transform.TransformVector(after[i] - before[i]));
                Vector3 expected = i >= first && i < end ? Vector3.forward * .2f : Vector3.zero;
                if ((delta - expected).sqrMagnitude > .00000001f) return false;
            }
            return true;
        }
        finally { visual.panelFloatOffset = offset; }
    }
    private static IEnumerator DomeStateChecks()
    {
        float idle = visual.domeVibrationAmplitude, charge = visual.domeChargeAmplitude, fire = visual.domeFireAmplitude;
        float chargeHz = visual.domeChargeFrequency, fireHz = visual.domeFireFrequency, damage = turret.damagePerSecond;
        visual.domeVibrationAmplitude = 0f;
        visual.domeChargeAmplitude = .04f; visual.domeChargeFrequency = 4f;
        visual.domeFireAmplitude = .075f; visual.domeFireFrequency = 8f;
        turret.damagePerSecond = 0f;
        Place(player, new Vector3(0, 0, 3f));
        foreach (var phase in new[] { ParticleBeamTurretController.AttackPhase.Charging, ParticleBeamTurretController.AttackPhase.Firing })
        {
            while (turret.Phase != phase || turret.PhaseAge < .25f) yield return null;
            Check(RingOffsetStaysAxial(), phase + " ring translates along the beam while preserving animated panel positions relative to one another");
            float amplitude = phase == ParticleBeamTurretController.AttackPhase.Charging ? .04f : .075f;
            float hz = phase == ParticleBeamTurretController.AttackPhase.Charging ? 4f : 8f;
            float min = 1f, max = -1f, previous = visual.DomeOffset, startTime = Time.time, until = startTime + .55f;
            int crossings = 0;
            while (Time.time < until)
            {
                yield return null;
                float current = visual.DomeOffset;
                min = Mathf.Min(min, current); max = Mathf.Max(max, current);
                if ((previous < 0f) != (current < 0f)) crossings++;
                previous = current;
            }
            Check(turret.Phase == phase && min < -amplitude * .85f && max > amplitude * .85f && min >= -amplitude - .0001f && max <= amplitude + .0001f
                && Mathf.Abs(crossings - (Time.time - startTime) * hz * 2f) < 2f,
                phase + " uses its own dome amplitude and measured oscillation frequency while idle vibration is disabled");
            turret.sceneDirector.enabled = false; float frozen = visual.DomeOffset;
            until = Time.time + .15f; while (Time.time < until) yield return null;
            Check(visual.DomeOffset == frozen, phase + " dome vibration freezes with Director pause");
            turret.sceneDirector.enabled = true;
        }
        Place(player, Vector3.back * 3f);
        while (turret.Phase != ParticleBeamTurretController.AttackPhase.Cooldown || turret.PhaseAge < .25f) yield return null;
        Check(visual.DomeOffset == 0f, "Cooldown returns to independent idle dome settings after the firing blend");
        visual.domeVibrationAmplitude = idle; visual.domeChargeAmplitude = charge; visual.domeFireAmplitude = fire;
        visual.domeChargeFrequency = chargeHz; visual.domeFireFrequency = fireHz; turret.damagePerSecond = damage;
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
