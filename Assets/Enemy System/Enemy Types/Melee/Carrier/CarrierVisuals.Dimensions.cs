using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    public sealed partial class CarrierVisuals
    {
        private float appliedBodyScale = -1f;
        // Keep the assembly itself unscaled: it carries full-size docked Drones as well as the shell.
        public void ApplyBodyScale()
        {
            if (!carrier) carrier = GetComponent<CarrierController>();
            float scale = BodyScale;
            if (shell && shell.transform.localScale != Vector3.one * scale) shell.transform.localScale = Vector3.one * scale;
            if (!carrier) return;
            for (int i = 0; i < carrier.docks.Length; i++)
            {
                var dock = carrier.docks[i]; if (!dock) continue;
                CarrierHullGeometry.GetDockPose(i, out var position, out var rotation);
                position *= scale;
                if (dock.localPosition != position) dock.localPosition = position;
                if (dock.localRotation != rotation) dock.localRotation = rotation;
            }
            if (carrier.damageTrigger && carrier.damageTrigger.radius != damageRadiusAtUnitScale * scale)
                carrier.damageTrigger.radius = damageRadiusAtUnitScale * scale;
            if (appliedBodyScale != scale)
            {
                appliedBodyScale = scale;
                if (carrier.SpawnWarning) carrier.SpawnWarning.SetOutline(CreateSpawnOutline());
            }
        }

        /// <summary>Planar footprint including the full-size docked noses, in this root's local units.</summary>
        public float SpawnRadiusLocal
        {
            get
            {
                CarrierHullGeometry.GetDockPose(0, out var dock, out _);
                var controller = carrier ? carrier : GetComponent<CarrierController>();
                var prefab = controller && controller.droneDefinition ? controller.droneDefinition.prefab : null;
                var drone = prefab ? prefab.GetComponent<DroneVisuals>() : null;
                float nose = drone ? drone.noseLength * Mathf.Abs(prefab.transform.localScale.z) : .4f;
                return Mathf.Max(CarrierHullGeometry.Radius * BodyScale, dock.magnitude * BodyScale + nose) + .05f;
            }
        }

        /// <summary>Builds a warning from dimensions, without mutating the prefab or scaling its Drone silhouettes.</summary>
        public Mesh CreateSpawnOutline()
        {
            var outline = new Mesh { name = "Carrier scaled arrival outline (generated)", hideFlags = HideFlags.DontSave };
            BuildSpawnOutline(outline);
            return outline;
        }

        public void BuildSpawnOutline(Mesh outline)
        {
            var vertices = new List<Vector3>(1008); var tangents = new List<Vector4>(1008);
            var uv = new List<Vector2>(1008); var indices = new List<int>(1512);
            Matrix4x4 frame = assembly ? transform.worldToLocalMatrix * assembly.localToWorldMatrix : Matrix4x4.identity;
            void Edge(Vector3 a, Vector3 b)
            {
                a = frame.MultiplyPoint3x4(a); b = frame.MultiplyPoint3x4(b);
                int n = vertices.Count; Vector3 direction = b - a;
                vertices.Add(a); vertices.Add(a); vertices.Add(b); vertices.Add(b);
                for (int k = 0; k < 4; k++) tangents.Add(new Vector4(direction.x, direction.y, direction.z, 1f));
                uv.Add(new Vector2(0f, -1f)); uv.Add(new Vector2(0f, 1f)); uv.Add(new Vector2(1f, -1f)); uv.Add(new Vector2(1f, 1f));
                indices.Add(n); indices.Add(n + 1); indices.Add(n + 2); indices.Add(n + 2); indices.Add(n + 1); indices.Add(n + 3);
            }
            CarrierHullGeometry.Create(out var points, out var polygons);
            var unique = new HashSet<Vector2Int>();
            foreach (var polygon in polygons)
                for (int i = 0; i < polygon.Length; i++)
                {
                    int a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                    if (unique.Add(new Vector2Int(Mathf.Min(a, b), Mathf.Max(a, b)))) Edge(points[a] * BodyScale, points[b] * BodyScale);
                }
            var controller = carrier ? carrier : GetComponent<CarrierController>();
            var prefab = controller && controller.droneDefinition ? controller.droneDefinition.prefab : null;
            var drone = prefab ? prefab.GetComponent<DroneVisuals>() : null;
            if (drone)
                for (int slot = 0; slot < 6; slot++)
                {
                    CarrierHullGeometry.GetDockPose(slot, out var position, out var rotation); position *= BodyScale;
                    Vector3 Pose(Vector3 p) => position + rotation * Vector3.Scale(p, prefab.transform.localScale);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f, b = (i + 1) * Mathf.PI / 3f;
                        Vector3 p = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * drone.radius;
                        Vector3 q = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f) * drone.radius;
                        Edge(Pose(p), Pose(q)); Edge(Pose(p), Pose(Vector3.forward * drone.noseLength));
                    }
                }
            outline.Clear(); outline.SetVertices(vertices); outline.SetTangents(tangents); outline.SetUVs(0, uv); outline.SetTriangles(indices, 0);
            outline.RecalculateBounds(); var bounds = outline.bounds; bounds.Expand(.65f); outline.bounds = bounds;
        }
    }
}
