using System;
using Massive.PowerUps;
using Massive.Resonance;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Multiplier.Editor
{
    /// <summary>Dependency-free placement checks; creates and removes an isolated preview scene.</summary>
    public static class AmplifierSpawnPlacementValidation
    {
        [MenuItem("MASSIVE/Amplifier/Validate Spawn Placement")]
        public static void ValidateMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run placement validation outside Play Mode.");
            int passed = 0;
            var scene = EditorSceneManager.NewPreviewScene();
            ResonancePatternDefinition definition = null;
            try
            {
                var root = new GameObject("Amplifier placement validation");
                SceneManager.MoveGameObjectToScene(root, scene);
                var gridObject = Child(root, "Grid", Vector3.zero);
                gridObject.SetActive(false);
                gridObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var grid = gridObject.AddComponent<VectorGridGPU>(); grid.size = new Vector2(20f, 12f);
                var boundsObject = Child(root, "Independent bounds provider", new Vector3(100, 200, 300));
                var bounds = boundsObject.AddComponent<ArenaBoundsFromVectorGrid>();
                var serializedBounds = new SerializedObject(bounds);
                serializedBounds.FindProperty("grid").objectReferenceValue = grid;
                serializedBounds.ApplyModifiedPropertiesWithoutUndo();
                var region = root.AddComponent<AmplifierSpawnRegion>(); region.arenaBounds = bounds;
                region.drawZones = false; region.blockingMask = 0; region.noSpawnMask = 0;
                string reason; Rect rect; Vector3 point;
                Check(region.TryGetNeutralRect(.75f, out rect, out reason), "basic neutral rectangle", ref passed);
                Check(Mathf.Abs(rect.xMax - 1.75f) < .001f && Mathf.Abs(rect.yMax - 4f) < .001f,
                    "whole-Core radius, clearance and border included", ref passed);
                Check(region.IsValidSpawnPoint(Vector3.zero, .75f, null, out reason), "independent bounds transform does not move the region", ref passed);
                Check(!region.IsValidSpawnPoint(new Vector3(2f, 0, 0), .75f, null, out reason), "neutral stripe edge excluded", ref passed);
                Check(!region.IsValidSpawnPoint(new Vector3(0, 0, 4.1f), .75f, null, out reason), "border excluded", ref passed);
                Check(!region.IsValidSpawnPoint(new Vector3(0, 1f, 0), .75f, null, out reason), "wrong gameplay height excluded", ref passed);
                Check(!region.IsValidSpawnPoint(new Vector3(float.NaN, 0, 0), .75f, null, out reason), "non-finite point rejected", ref passed);
                Check(!region.TryFindSpawn(null, .75f, null, out point, out reason), "missing random source rejected", ref passed);
                Check(region.TryFindSpawn(new System.Random(345), .75f, null, out point, out reason), "bounded random search succeeds", ref passed);
                Vector3 repeat;
                region.TryFindSpawn(new System.Random(345), .75f, null, out repeat, out reason);
                Check(point == repeat, "seeded search deterministic", ref passed);

                gridObject.transform.localScale = new Vector3(2, 3, 1);
                gridObject.transform.position = new Vector3(11, -.09f, -7);
                gridObject.transform.rotation = Quaternion.Euler(0, 37, 0) * Quaternion.Euler(90, 0, 0);
                Check(region.TryGetNeutralRect(.75f, out rect, out reason) && Mathf.Abs(rect.xMax - 2.375f) < .001f,
                    "world padding converted through actual grid scale", ref passed);
                point = region.GridPointToWorld(new Vector2(1, 2));
                Check(region.IsValidSpawnPoint(point, .75f, null, out reason), "rotated translated grid sampling agrees with validation", ref passed);
                gridObject.transform.localRotation = Quaternion.identity;
                Check(!region.TryGetNeutralRect(.75f, out rect, out reason), "non-horizontal decorative grid rejected", ref passed);
                gridObject.transform.localRotation = Quaternion.Euler(90, 0, 0);
                gridObject.transform.localScale = Vector3.one; gridObject.transform.position = Vector3.zero;
                region.neutralWidthFraction = .05f;
                Check(!region.TryFindSpawn(new System.Random(1), .75f, null, out point, out reason) && point == Vector3.zero,
                    "no-space request has no unsafe fallback", ref passed);
                region.neutralWidthFraction = .3f;

                var zoneObject = Child(root, "Explicit trigger zone", Vector3.zero);
                var zone = zoneObject.AddComponent<SphereCollider>(); zone.isTrigger = true; zone.radius = 1f;
                region.noGoColliders = new Collider[] { zone };
                Physics.SyncTransforms();
                Check(!region.IsValidSpawnPoint(Vector3.zero, .2f, null, out reason) && reason.Contains("No-go"), "explicit trigger blocks", ref passed);
                zone.enabled = false;
                Check(region.IsValidSpawnPoint(Vector3.zero, .2f, null, out reason), "disabled zone ignored", ref passed);
                zone.enabled = true; region.noGoColliders = new Collider[0];
                var marker = zoneObject.AddComponent<PowerUpNoSpawnZone>();
                Check(!region.IsValidSpawnPoint(Vector3.zero, .2f, null, out reason), "existing no-spawn marker reused", ref passed);
                marker.enabled = false;
                Check(region.IsValidSpawnPoint(Vector3.zero, .2f, null, out reason), "unmarked ordinary trigger not a blanket exclusion", ref passed);

                region.noSpawnMask = 1 << 9; zoneObject.layer = 9; Physics.SyncTransforms();
                Check(!region.IsValidSpawnPoint(Vector3.zero, .2f, null, out reason), "legacy NoSpawnZone layer rejects trigger", ref passed);
                region.noSpawnMask = 0; region.blockingMask = 1 << 9;
                Check(region.IsValidSpawnPoint(Vector3.zero, .2f, null, out reason), "solid blocker mask ignores ordinary triggers", ref passed);
                zone.isTrigger = false; Physics.SyncTransforms();
                Check(!region.IsValidSpawnPoint(Vector3.zero, .2f, null, out reason), "solid blocker rejected", ref passed);
                region.ignoredColliders = new Collider[] { zone };
                Check(region.IsValidSpawnPoint(Vector3.zero, .2f, null, out reason), "explicit physics ignore honored", ref passed);
                region.ignoredColliders = new Collider[0]; zoneObject.SetActive(false); region.blockingMask = 0;

                var boxObject = Child(root, "Rotated thin exclusion", new Vector3(0, 0, 2));
                boxObject.transform.rotation = Quaternion.Euler(0, 45, 0);
                var box = boxObject.AddComponent<BoxCollider>(); box.size = new Vector3(.1f, 1f, 4f); box.isTrigger = true;
                region.noGoColliders = new Collider[] { box }; Physics.SyncTransforms();
                Check(region.IsValidSpawnPoint(new Vector3(1.35f, 0, .65f), .01f, null, out reason),
                    "rotated box uses shape rather than bounding rectangle", ref passed);
                boxObject.SetActive(false); region.noGoColliders = new Collider[0];

                var goalObject = Child(root, "Goal", new Vector3(3f, 0, 0));
                goalObject.AddComponent<SphereCollider>().isTrigger = true;
                var goal = goalObject.AddComponent<AmplifierGoalCapture>();
                Check(!region.IsValidSpawnPoint(Vector3.zero, .2f, null, out reason) && reason.Contains("goal"), "goal attraction exclusion", ref passed);
                goalObject.SetActive(false);

                var playerObject = Child(root, "Player spacing", Vector3.zero);
                playerObject.AddComponent<Rigidbody>();
                playerObject.AddComponent<SphereCollider>();
                var player = playerObject.AddComponent<PlayerControllerScript>();
                Check(!region.IsValidSpawnPoint(new Vector3(0, 0, 1.6f), .2f, null, out reason) && reason.Contains("player"),
                    "player safety distance independent of collision layers", ref passed);
                playerObject.SetActive(false);
                Check(region.IsValidSpawnPoint(Vector3.zero, .2f, null, out reason), "inactive player does not reserve space", ref passed);

                var patternObject = Child(root, "Invisible next pattern", Vector3.zero);
                patternObject.SetActive(false);
                var pattern = patternObject.AddComponent<ResonancePatternController>();
                definition = ScriptableObject.CreateInstance<ResonancePatternDefinition>();
                definition.curves.Add(new ResonanceCurve { radius = 2f, rotationDegrees = 0f });
                definition.arcs.Add(new ResonanceArc { startDegrees = 0f, sweepDegrees = 360f,
                    thickness = .3f, colliderThickness = .2f });
                pattern.definition = definition;
                Check(!region.IsValidSpawnPoint(new Vector3(0, 0, 2), .2f, pattern, out reason) && reason.Contains("Resonance"),
                    "disabled manifestation still reserves authored hard arc", ref passed);
                Check(region.IsValidSpawnPoint(Vector3.zero, .2f, pattern, out reason), "circle interior not incorrectly filled", ref passed);
                definition.arcs[0].behavior = ResonanceBehavior.DampingMembrane;
                Check(!region.IsValidSpawnPoint(new Vector3(0, 0, 2), .2f, pattern, out reason), "soft arc footprint reserved", ref passed);
                definition.arcs[0].behavior = ResonanceBehavior.VisualOnly;
                Check(region.IsValidSpawnPoint(new Vector3(0, 0, 2), .2f, pattern, out reason), "visual-only arc does not block", ref passed);
                definition.arcs[0].behavior = ResonanceBehavior.HardWall;
                pattern.coreResponse = ResonanceCoreResponse.MagneticRepulsion; pattern.magneticReach = .8f;
                Check(!region.IsValidSpawnPoint(new Vector3(0, 0, 3.1f), .2f, pattern, out reason), "magnetic reach clearance included", ref passed);

                region.blockingMask = 1 << 9;
                var crowded = Child(root, "Query saturation", Vector3.zero);
                for (int i = 0; i < 260; i++)
                {
                    var item = Child(crowded, "Blocker", Vector3.zero); item.layer = 9;
                    item.AddComponent<SphereCollider>().radius = .2f;
                }
                Physics.SyncTransforms();
                Check(!region.IsValidSpawnPoint(Vector3.zero, .2f, null, out reason) && reason.Contains("capacity"),
                    "bounded nonalloc query saturation fails closed", ref passed);
                return passed + " Amplifier spawn placement checks passed.";
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static GameObject Child(GameObject parent, string name, Vector3 position)
        { var go = new GameObject(name); go.transform.SetParent(parent.transform, false); go.transform.localPosition = position; return go; }
        private static void Check(bool value, string label, ref int passed)
        { if (!value) throw new InvalidOperationException("Amplifier placement validation failed: " + label); passed++; }
    }
}
