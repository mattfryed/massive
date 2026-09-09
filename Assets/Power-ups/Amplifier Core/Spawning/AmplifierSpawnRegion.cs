using System.Collections.Generic;
using Massive.PowerUps;
using Massive.Resonance;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Multiplier
{
    /// <summary>Placement policy shared by runtime spawning and the Scene view preview.</summary>
    [DisallowMultipleComponent]
    public sealed class AmplifierSpawnRegion : MonoBehaviour
    {
        public ArenaBoundsFromVectorGrid arenaBounds;
        [Header("Neutral territory")]
        [Tooltip("Central fraction of the arena width along the grid's local X axis. The entire Core must fit inside.")]
        [Range(.01f, 1f)] public float neutralWidthFraction = .3f;
        [Min(0f)] public float borderMarginWorld = .75f;
        [Tooltip("World Y of the Core's center, independent of the decorative grid height.")]
        public float spawnHeightWorld;
        [Min(0f)] public float clearanceWorld = .5f;
        [Min(0f)] public float minDistanceFromPlayers = 1.75f;
        [Min(0f)] public float goalExclusionPadding = .5f;
        [Header("Exclusions — reuse existing enemy no-spawn volumes")]
        [Tooltip("Includes trigger volumes. Defaults to the existing NoSpawnZone layer (9).")]
        public LayerMask noSpawnMask = 1 << 9;
        [Tooltip("Solid colliders on these layers block placement. Triggers are ignored here; use No Go Colliders or No Spawn Mask for trigger exclusions. Colliders on the grid object itself are ignored; child obstacles still block.")]
        public LayerMask blockingMask = ~0;
        [Tooltip("Additional authored exclusion shapes, including triggers such as NOVA's star entry volume. Disabled colliders are not active exclusions.")]
        public Collider[] noGoColliders = new Collider[0];
        public Collider[] ignoredColliders = new Collider[0];
        [Range(1, 256)] public int attemptsPerSpawn = 48;
        [Header("Scene view placement preview")]
        public bool drawZones = true;
        [Min(.01f)] public float previewObjectRadius = .75f;
        [Tooltip("Optional next pattern. Its authored interactive footprint is excluded even before its colliders appear.")]
        public ResonancePatternController previewPattern;
        [Range(5, 41)] public int previewColumns = 21;
        [Range(5, 25)] public int previewRows = 13;

        private const int OverlapCapacity = 256;
        private readonly Collider[] overlaps = new Collider[OverlapCapacity];
        private readonly List<Collider> zones = new List<Collider>();
        private readonly List<AmplifierGoalCapture> goals = new List<AmplifierGoalCapture>();
        private readonly List<PlayerControllerScript> players = new List<PlayerControllerScript>();
        private readonly List<ArcCell> arcCells = new List<ArcCell>();
        private readonly List<GameObject> sceneRoots = new List<GameObject>();
        private readonly List<PowerUpNoSpawnZone> markerBuffer = new List<PowerUpNoSpawnZone>();
        private readonly List<Collider> colliderBuffer = new List<Collider>();
        private readonly List<AmplifierGoalCapture> goalBuffer = new List<AmplifierGoalCapture>();
        private readonly List<PlayerControllerScript> playerBuffer = new List<PlayerControllerScript>();
        private struct ArcCell { public Vector3 a, b; public float radius; }
        private ResonancePatternController cachedPattern;
        public IReadOnlyList<Collider> CachedNoGoColliders => zones;
        public IReadOnlyList<AmplifierGoalCapture> CachedGoals => goals;

        public bool TryFindSpawn(System.Random random, float objectRadius, ResonancePatternController pattern,
            out Vector3 point, out string reason)
        {
            point = default;
            if (random == null) { reason = "A random source is required."; return false; }
            Rect rect;
            if (!TryGetNeutralRect(objectRadius, out rect, out reason)) return false;
            RefreshPlacementCache(pattern);
            string lastReason = "No clear location.";
            for (int i = 0; i < Mathf.Clamp(attemptsPerSpawn, 1, 256); i++)
            {
                Vector2 local = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, (float)random.NextDouble()),
                    Mathf.Lerp(rect.yMin, rect.yMax, (float)random.NextDouble()));
                Vector3 candidate = GridPointToWorld(local);
                if (!IsValidCached(candidate, objectRadius, out lastReason)) continue;
                point = candidate; reason = string.Empty; return true;
            }
            reason = "No safe neutral spawn found; retry without spawning. Last rejection: " + lastReason;
            return false;
        }

        public bool IsValidSpawnPoint(Vector3 point, float objectRadius, ResonancePatternController pattern, out string reason)
        {
            RefreshPlacementCache(pattern);
            return IsValidCached(point, objectRadius, out reason);
        }

        /// <summary>Refresh once per spawn attempt batch or Scene view draw, never once per candidate.</summary>
        public void RefreshPlacementCache(ResonancePatternController pattern)
        {
            zones.Clear(); goals.Clear(); players.Clear(); sceneRoots.Clear();
            cachedPattern = pattern;
            if (noGoColliders != null)
                foreach (Collider c in noGoColliders) AddZone(c);
            if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
            {
                gameObject.scene.GetRootGameObjects(sceneRoots);
                foreach (GameObject root in sceneRoots)
                {
                    markerBuffer.Clear(); root.GetComponentsInChildren(true, markerBuffer);
                    foreach (PowerUpNoSpawnZone marker in markerBuffer)
                    {
                        if (!marker.isActiveAndEnabled) continue;
                        colliderBuffer.Clear(); marker.GetComponentsInChildren(true, colliderBuffer);
                        foreach (Collider c in colliderBuffer) AddZone(c);
                    }
                    goalBuffer.Clear(); root.GetComponentsInChildren(true, goalBuffer);
                    foreach (AmplifierGoalCapture goal in goalBuffer)
                        if (goal.isActiveAndEnabled) goals.Add(goal);
                    playerBuffer.Clear(); root.GetComponentsInChildren(true, playerBuffer);
                    foreach (PlayerControllerScript player in playerBuffer)
                        if (player.isActiveAndEnabled && !player.IsPseudoPlayer) players.Add(player);
                }
            }
            BuildPatternFootprint(pattern);
        }

        private void AddZone(Collider c)
        {
            if (c != null && c.enabled && c.gameObject.activeInHierarchy && c.gameObject.scene == gameObject.scene && !zones.Contains(c))
                zones.Add(c);
        }

        public bool TryGetNeutralRect(float objectRadius, out Rect rect, out string reason)
        {
            rect = default;
            if (arenaBounds == null || !arenaBounds.IsValid)
            { reason = "Assign valid Arena Bounds / Vector Grid."; return false; }
            if (!Finite(objectRadius) || !Finite(spawnHeightWorld) || !Finite(clearanceWorld)
                || !Finite(borderMarginWorld) || !Finite(neutralWidthFraction))
            { reason = "Placement settings must be finite numbers."; return false; }
            Transform t = arenaBounds.Grid.transform;
            // Gameplay is XZ. A decorative tilt cannot define a physical spawn plane.
            if (Mathf.Abs(Vector3.Dot(t.forward.normalized, Vector3.up)) < .999f)
            { reason = "The grid plane must be horizontal for XZ gameplay placement."; return false; }
            Vector3 scale = t.lossyScale;
            float sx = Mathf.Abs(scale.x), sy = Mathf.Abs(scale.y);
            if (sx < .00001f || sy < .00001f)
            { reason = "Grid scale cannot be zero."; return false; }
            Vector2 half = arenaBounds.GetHalfSizeLocalInset();
            float radius = Mathf.Max(0f, objectRadius) + Mathf.Max(0f, clearanceWorld);
            float x = Mathf.Min(half.x - (Mathf.Max(0f, borderMarginWorld) + radius) / sx,
                half.x * Mathf.Clamp(neutralWidthFraction, .01f, 1f) - radius / sx);
            float y = half.y - (Mathf.Max(0f, borderMarginWorld) + radius) / sy;
            if (x <= .00001f || y <= .00001f)
            { reason = "Core radius and margins leave no room in the neutral region."; return false; }
            rect = Rect.MinMaxRect(-x, -y, x, y);
            reason = string.Empty; return true;
        }

        public Vector3 GridPointToWorld(Vector2 local)
        {
            if (arenaBounds == null || !arenaBounds.IsValid) return new Vector3(0f, spawnHeightWorld, 0f);
            Vector3 p = arenaBounds.Grid.transform.TransformPoint(new Vector3(local.x, local.y, 0f));
            p.y = spawnHeightWorld; return p;
        }

        public bool IsValidCached(Vector3 point, float objectRadius, out string reason)
        {
            Rect rect;
            if (!TryGetNeutralRect(objectRadius, out rect, out reason)) return false;
            if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z))
            { reason = "Spawn position is not finite."; return false; }
            Vector3 local = arenaBounds.Grid.transform.InverseTransformPoint(point);
            if (local.x < rect.xMin || local.x > rect.xMax || local.y < rect.yMin || local.y > rect.yMax
                || Mathf.Abs(point.y - spawnHeightWorld) > .01f)
            { reason = "Outside the safe neutral territory."; return false; }
            float radius = Mathf.Max(0f, objectRadius) + Mathf.Max(0f, clearanceWorld);
            foreach (Collider c in zones)
                if (c != null && c.enabled && c.gameObject.activeInHierarchy && ShapeIntersects(c, point, radius))
                { reason = "No-go volume: " + c.name; return false; }
            foreach (AmplifierGoalCapture goal in goals)
            {
                if (goal == null || !goal.isActiveAndEnabled) continue;
                float distance = radius + Mathf.Max(0f, goalExclusionPadding) + goal.AttractionRadius;
                if (PlanarDistanceSquared(point, goal.CapturePoint.position) <= distance * distance)
                { reason = "Inside a goal's attraction exclusion."; return false; }
            }
            foreach (PlayerControllerScript player in players)
            {
                if (player == null || !player.isActiveAndEnabled) continue;
                float distance = Mathf.Max(radius, Mathf.Max(0f, minDistanceFromPlayers));
                if (PlanarDistanceSquared(point, player.transform.position) < distance * distance)
                { reason = "Too close to a player."; return false; }
            }
            foreach (ArcCell cell in arcCells)
            {
                Vector3 delta = cell.b - cell.a; delta.y = 0f;
                Vector3 offset = point - cell.a; offset.y = 0f;
                float u = Mathf.Clamp01(Vector3.Dot(offset, delta) / Mathf.Max(.000001f, delta.sqrMagnitude));
                float distance = radius + cell.radius;
                if ((offset - delta * u).sqrMagnitude <= distance * distance)
                { reason = "Inside the next Resonance pattern's interaction footprint."; return false; }
            }
            if (!CheckPhysics(point, radius, noSpawnMask, QueryTriggerInteraction.Collide, true, out reason)) return false;
            if (!CheckPhysics(point, radius, blockingMask, QueryTriggerInteraction.Ignore, false, out reason)) return false;
            reason = string.Empty; return true;
        }

        private bool CheckPhysics(Vector3 p, float radius, int mask, QueryTriggerInteraction triggers, bool noGo, out string reason)
        {
            reason = string.Empty;
            if (mask == 0) return true;
            var physicsScene = gameObject.scene.GetPhysicsScene();
            if (!physicsScene.IsValid()) { reason = "No valid physics scene."; return false; }
            int count = physicsScene.OverlapSphere(p, radius, overlaps, mask, triggers);
            if (count >= overlaps.Length)
            { reason = "Overlap query reached its safety capacity; placement deferred."; return false; }
            for (int i = 0; i < count; i++)
            {
                Collider c = overlaps[i];
                if (c == null || c.gameObject.scene != gameObject.scene || IsIgnored(c, noGo)) continue;
                reason = (noGo ? "No-spawn layer: " : "Blocking object: ") + c.name; return false;
            }
            return true;
        }

        private bool IsIgnored(Collider c, bool noGo)
        {
            if (ignoredColliders != null) foreach (Collider ignored in ignoredColliders) if (c == ignored) return true;
            if (noGo) return false;
            if (cachedPattern != null && c.transform.IsChildOf(cachedPattern.transform)) return true;
            return arenaBounds != null && arenaBounds.IsValid && c.transform == arenaBounds.Grid.transform;
        }

        private static bool ShapeIntersects(Collider c, Vector3 p, float radius)
        {
            // ClosestPoint measures actual shape, not its axis-aligned bounding box.
            // Concave mesh zones have no unambiguous volume interior; conservatively
            // reserve their bounds. Use primitive/convex volumes for precise no-go authoring.
            MeshCollider mesh = c as MeshCollider;
            if (mesh != null && !mesh.convex) return c.bounds.SqrDistance(p) <= radius * radius;
            return (c.ClosestPoint(p) - p).sqrMagnitude <= radius * radius;
        }

        private void BuildPatternFootprint(ResonancePatternController pattern)
        {
            arcCells.Clear();
            if (pattern == null || pattern.definition == null || pattern.definition.arcs == null || pattern.definition.curves == null) return;
            float worldScale = Mathf.Max(Mathf.Abs(pattern.transform.lossyScale.x), Mathf.Abs(pattern.transform.lossyScale.z));
            foreach (ResonanceArc source in pattern.definition.arcs)
            {
                if (source == null || !source.IsVisible || source.behavior == ResonanceBehavior.VisualOnly
                    || source.curveIndex < 0 || source.curveIndex >= pattern.definition.curves.Count) continue;
                ResonanceCurve curve = pattern.definition.curves[source.curveIndex];
                if (curve == null) continue;
                int count = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp(source.sweepDegrees, 0f, 360f)
                    / Mathf.Clamp(pattern.sampleDegrees, .5f, 10f)), 2, 720);
                var path = new Vector3[count + 1];
                float length = 0f;
                for (int i = 0; i <= count; i++)
                {
                    path[i] = pattern.Evaluate(curve, source.startDegrees + Mathf.Clamp(source.sweepDegrees, 0f, 360f) * i / count);
                    if (i > 0) length += Vector3.Distance(path[i - 1], path[i]);
                }
                ResonanceArc arc = pattern.ResolveArc(source, length);
                if (!arc.IsVisible || arc.colliderThickness <= 0f || pattern.SolidVisualWidth <= 0f) continue;
                float from = arc.independentCollisionProfile ? arc.collisionStart : 0f;
                float to = arc.independentCollisionProfile ? arc.collisionEnd : 1f;
                for (int i = 0; i < count; i++)
                {
                    float a = i / (float)count, b = (i + 1f) / count;
                    if (b <= from || a >= to || to <= from) continue;
                    float lo = Mathf.Clamp01((from - a) / (b - a)), hi = Mathf.Clamp01((to - a) / (b - a));
                    float ta = Mathf.Lerp(a, b, lo), tb = Mathf.Lerp(a, b, hi), tm = (ta + tb) * .5f;
                    // Conservative per-cell radius retains narrow taper tips while covering the real surface.
                    float width = Mathf.Max(ContactWidth(pattern, arc, ta), Mathf.Max(ContactWidth(pattern, arc, tm), ContactWidth(pattern, arc, tb)));
                    if (width <= .00001f) continue;
                    float envelope = Mathf.Max(arc.CollisionEnvelope(ta), Mathf.Max(arc.CollisionEnvelope(tm), arc.CollisionEnvelope(tb)));
                    float reach = pattern.coreResponse == ResonanceCoreResponse.MagneticRepulsion ? Mathf.Max(0f, pattern.magneticReach) * envelope : 0f;
                    arcCells.Add(new ArcCell {
                        a = pattern.transform.TransformPoint(Vector3.Lerp(path[i], path[i + 1], lo)),
                        b = pattern.transform.TransformPoint(Vector3.Lerp(path[i], path[i + 1], hi)),
                        radius = width * worldScale + reach });
                }
            }
        }

        private static float ContactWidth(ResonancePatternController p, ResonanceArc a, float t)
        { return .5f * Mathf.Min(a.colliderThickness * a.CollisionEnvelope(t), a.thickness * p.SolidVisualWidth * a.Envelope(t)); }
        private static float PlanarDistanceSquared(Vector3 a, Vector3 b)
        { float x = a.x - b.x, z = a.z - b.z; return x * x + z * z; }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
