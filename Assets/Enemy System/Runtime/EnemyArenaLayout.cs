using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyArenaLayout : MonoBehaviour
    {
        public ArenaBoundsFromVectorGrid arena;
        [Tooltip("World Y of actors and enemy arrivals. Independent of the decorative grid height.")]
        public float gameplayHeight;
        [Min(0f), Tooltip("Extra world-space inset for formation positions in oval arenas. Wall sockets stay on the boundary. Enemy-radius and Director border checks still apply.")]
        public float ovalSpawnInset = .25f;
        public List<Region> regions = new();
        public List<Socket> sockets = new();
        public Collider[] exclusions = Array.Empty<Collider>();
        [Tooltip("Permit arrivals over Resonance and Amplifier colliders. Does not alter their gameplay collisions or explicitly authored exclusions. Use without an Amplifier placement region when these overlaps are intended.")]
        public bool allowResonanceAndAmplifierOverlap;
        [Tooltip("Colliders in these environmental hierarchies do not block timeline arrivals. Players and enemies are still checked separately.")]
        public Transform[] spawnOverlapRoots = Array.Empty<Transform>();
        [Min(0f)] public float exclusionPadding = .15f;
        public bool showGizmos = true;
        [Serializable] public sealed class Region { public string id; public Rect rectangle = new(-1, -1, 2, 2); }
        [Serializable] public sealed class Socket
        {
            public string id;
            public bool enabled = true;
            [Tooltip("Search this wall range for a free footprint. Fixed mounts remain available when disabled.")]
            public bool useRange;
            [Tooltip("Normalized wall endpoints (-1 to 1). Use the same direction on paired ranges, e.g. center to corner, to prefer symmetrical arrivals.")]
            public Vector2 rangeStart, rangeEnd;
            [Tooltip("Preferred position. For a wall range this is projected onto the range before searching.")]
            public Vector2 position;
            public Vector2 inward = Vector2.down;
            [Range(5, 85)] public float aimArc = 70f;
            [Min(0f), Tooltip("Offset of the occupied footprint into the arena; the root remains on the wall.")]
            public float clearanceOffset = .9f;
            [Tooltip("Optional wall collider supporting this socket, ignored by its placement test only.")]
            public Collider support;
        }
        public struct Pose
        {
            public Vector3 position, clearance;
            public Quaternion rotation;
            public Socket socket;
            public float wallFraction;
        }
        public Vector3 World(Vector2 normalized) => World(normalized, 0f);
        private Vector3 World(Vector2 normalized, float inset)
        {
            if (!arena || !arena.Grid) return new Vector3(transform.position.x, gameplayHeight, transform.position.z);
            var half = arena.Current.halfSizeLocal;
            float sign = Vector3.Dot(arena.Current.axisY_WS, Vector3.forward) >= 0 ? 1 : -1;
            float x = normalized.x * half.x, height = half.y;
            if (arena.ovalOutline)
            {
                // The grid uses the same column-wise ellipse mapping. The goal caps are
                // outside half.x and never become spawn territory.
                var scale = arena.Grid.transform.lossyScale;
                float sx = Mathf.Max(.000001f, Mathf.Abs(scale.x)), sy = Mathf.Max(.000001f, Mathf.Abs(scale.y));
                inset = Mathf.Max(0f, inset);
                x = normalized.x * Mathf.Max(0f, half.x - inset / sx);
                height = arena.OutlineHalfHeightLocal(x);
                if (inset > 0f)
                {
                    // Offset the physical wall's supporting lines, not just its Y value:
                    // this keeps a world-space margin even along the steep outer curve.
                    float top = height * sy;
                    Vector2 a = new(-half.x * sx, arena.OutlineHalfHeightLocal(-half.x) * sy);
                    for (int i = 1; i <= ArenaBoundsFromVectorGrid.OvalSegments; i++)
                    {
                        float nextX = Mathf.Lerp(-half.x, half.x, (float)i / ArenaBoundsFromVectorGrid.OvalSegments);
                        Vector2 b = new(nextX * sx, arena.OutlineHalfHeightLocal(nextX) * sy);
                        float slope = (b.y - a.y) / Mathf.Max(.000001f, b.x - a.x);
                        top = Mathf.Min(top, a.y + slope * (x * sx - a.x) - inset * Mathf.Sqrt(1f + slope * slope));
                        a = b;
                    }
                    height = Mathf.Max(0f, top / sy);
                }
            }
            var p = arena.Grid.transform.TransformPoint(new Vector3(x, normalized.y * height * sign, 0));
            p.y = gameplayHeight; return p;
        }
        public Vector3 Direction(Vector2 direction)
        {
            Vector3 d = World(direction) - World(Vector2.zero);
            // Facing is an angle, independent of the aspect ratio of the arena.
            if (arena && arena.Grid)
            {
                float sign = Vector3.Dot(arena.Current.axisY_WS, Vector3.forward) >= 0 ? 1 : -1;
                d = arena.Current.axisX_WS * direction.x + arena.Current.axisY_WS * (direction.y * sign);
            }
            d.y = 0f; return d.sqrMagnitude > .0001f ? d.normalized : Vector3.forward;
        }
        private Vector3 SocketDirection(Socket socket, Vector2 point)
        {
            Vector3 direction = Direction(socket.inward);
            if (!arena || !arena.ovalOutline || !arena.Grid || Mathf.Abs(point.y) < .9999f) return direction;
            Vector3 local = arena.Grid.transform.InverseTransformPoint(World(point));
            float width = arena.OvalHalfWidthLocal, height = arena.Current.halfSizeLocal.y;
            if (width <= 0f || height <= 0f) return direction;
            Vector3 normal = new(-local.x / (width * width), -local.y / (height * height), 0f);
            normal = arena.Grid.transform.worldToLocalMatrix.transpose.MultiplyVector(normal);
            normal.y = 0f;
            if (normal.sqrMagnitude < .000001f) return direction;
            // Preserve any authored aiming offset relative to the old inward direction.
            Vector3 oldInward = Direction(new Vector2(0f, -Mathf.Sign(point.y)));
            return Quaternion.FromToRotation(oldInward, normal.normalized) * direction;
        }
        public static bool ValidWallRange(Socket socket)
        {
            Vector2 a = socket.rangeStart, b = socket.rangeEnd;
            bool Bounded(Vector2 p) => Mathf.Abs(p.x) <= 1f && Mathf.Abs(p.y) <= 1f;
            return Bounded(a) && Bounded(b) && (a - b).sqrMagnitude > .000001f &&
                (Mathf.Abs(a.x - b.x) < .00001f && Mathf.Abs(a.x) == 1f ||
                 Mathf.Abs(a.y - b.y) < .00001f && Mathf.Abs(a.y) == 1f);
        }
        public static float PreferredWallFraction(Socket socket)
        {
            Vector2 span = socket.rangeEnd - socket.rangeStart;
            return span.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(socket.position - socket.rangeStart, span) / span.sqrMagnitude) : .5f;
        }
        public Pose WallPose(Socket socket, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            Vector2 point = socket.useRange ? Vector2.Lerp(socket.rangeStart, socket.rangeEnd, fraction) : socket.position;
            var pose = new Pose { socket = socket, wallFraction = fraction, position = World(point),
                rotation = Quaternion.LookRotation(SocketDirection(socket, point), Vector3.up) };
            pose.clearance = pose.position + pose.rotation * Vector3.forward * socket.clearanceOffset;
            return pose;
        }
        public bool AllowsSpawnOverlap(Collider shape)
        {
            if (!shape) return false;
            if (allowResonanceAndAmplifierOverlap &&
                (shape.GetComponentInParent<Massive.Resonance.ResonancePatternController>() ||
                 shape.GetComponentInParent<Massive.Multiplier.AmplifierCoreGameplay>())) return true;
            foreach (var root in spawnOverlapRoots)
                if (root && shape.transform.IsChildOf(root)) return true;
            return false;
        }
        public static string FlippedWallMount(string id) => id switch
        {
            "TopLeft" => "TopRight", "TopRight" => "TopLeft",
            "BottomLeft" => "BottomRight", "BottomRight" => "BottomLeft",
            "SideLeftTop" => "SideRightTop", "SideRightTop" => "SideLeftTop",
            "SideLeftBottom" => "SideRightBottom", "SideRightBottom" => "SideLeftBottom",
            _ => null
        };
        public bool Resolve(EnemyFormation.Slot slot, bool mirror, out Pose pose, out string reason, bool flipWallOrientation = false)
        {
            pose = default; reason = null;
            if (!arena || !arena.IsValid) { reason = "Missing arena bounds"; return false; }
            if (slot == null || !slot.enemy || !slot.enemy.prefab || !slot.telegraph)
            { reason = "Missing enemy or telegraph"; return false; }
            Vector2 point, facing;
            if (!string.IsNullOrEmpty(slot.socket))
            {
                // Random mobile mirroring is independent of the explicit wall-orientation toggle.
                string mount = flipWallOrientation ? FlippedWallMount(slot.socket) : slot.socket;
                if (mount == null) { reason = "No flipped wall mount for: " + slot.socket; return false; }
                pose.socket = sockets.Find(s => s.enabled && s.id == mount);
                if (pose.socket == null) { reason = "Unavailable socket: " + mount; return false; }
                if (pose.socket.useRange && !ValidWallRange(pose.socket))
                { reason = "Invalid wall range: " + slot.socket; return false; }
                pose = WallPose(pose.socket, slot.overrideWallPosition ? slot.wallPosition : PreferredWallFraction(pose.socket));
                return true;
            }
            else
            {
                var region = regions.Find(r => r.id == slot.region);
                if (region == null) { reason = "Missing region: " + slot.region; return false; }
                point = new Vector2(Mathf.Lerp(region.rectangle.xMin, region.rectangle.xMax, slot.position.x),
                    Mathf.Lerp(region.rectangle.yMin, region.rectangle.yMax, slot.position.y));
                facing = slot.facing;
                if (mirror) { point.x = -point.x; facing.x = -facing.x; }
            }
            pose.position = World(point, pose.socket == null && arena.ovalOutline ? arena.InsetWorld + Mathf.Max(0f, ovalSpawnInset) : 0f);
            pose.rotation = Quaternion.LookRotation(Direction(facing), Vector3.up);
            pose.clearance = pose.position + pose.rotation * Vector3.forward * (pose.socket?.clearanceOffset ?? 0f);
            return true;
        }
        public bool Clear(Pose pose, float radius, float border, out string reason)
        {
            reason = null;
            // A world/grid round trip can put an exact wall socket a few microns outside.
            // Keep the authored root on the wall; only the comparison gets a tolerance.
            bool rootInside = arena && (pose.socket != null
                ? (arena.ClampWorldPointInside(pose.position) - pose.position).sqrMagnitude <= .000004f
                : arena.ContainsWorldPoint(pose.position, radius + border));
            if (!rootInside || !arena.ContainsWorldPoint(pose.clearance, radius))
            { reason = "Outside playable footprint"; return false; }
            foreach (var collider in exclusions)
                if (collider && collider.enabled && collider.gameObject.activeInHierarchy &&
                    (collider.ClosestPoint(pose.clearance) - pose.clearance).sqrMagnitude < Mathf.Pow(radius + exclusionPadding, 2))
                { reason = "Exclusion: " + collider.name; return false; }
            return true;
        }
        private void OnDrawGizmosSelected()
        {
            if (!showGizmos || !arena) return;
            Gizmos.color = new Color(.2f, .8f, 1f);
            foreach (var region in regions)
            {
                var r = region.rectangle;
                float inset = arena.ovalOutline ? arena.InsetWorld + Mathf.Max(0f, ovalSpawnInset) : 0f;
                int segments = arena.ovalOutline ? 32 : 1;
                for (int i = 0; i < segments; i++)
                {
                    float x0 = Mathf.Lerp(r.xMin, r.xMax, (float)i / segments);
                    float x1 = Mathf.Lerp(r.xMin, r.xMax, (float)(i + 1) / segments);
                    Gizmos.DrawLine(World(new Vector2(x0, r.yMin), inset), World(new Vector2(x1, r.yMin), inset));
                    Gizmos.DrawLine(World(new Vector2(x0, r.yMax), inset), World(new Vector2(x1, r.yMax), inset));
                }
                Gizmos.DrawLine(World(r.min, inset), World(new Vector2(r.xMin, r.yMax), inset));
                Gizmos.DrawLine(World(new Vector2(r.xMax, r.yMin), inset), World(r.max, inset));
            }
            foreach (var socket in sockets)
            {
                if (!socket.enabled) continue; Gizmos.color = Color.yellow;
                var pose = WallPose(socket, PreferredWallFraction(socket));
                var p = pose.position; var d = pose.rotation * Vector3.forward;
                if (socket.useRange && ValidWallRange(socket))
                    for (int i = 0; i < 32; i++)
                        Gizmos.DrawLine(WallPose(socket, i / 32f).position, WallPose(socket, (i + 1) / 32f).position);
                Gizmos.DrawWireSphere(p, .2f); Gizmos.DrawRay(p, d * 1.5f);
                Gizmos.DrawRay(p, Quaternion.AngleAxis(socket.aimArc, Vector3.up) * d * 2);
                Gizmos.DrawRay(p, Quaternion.AngleAxis(-socket.aimArc, Vector3.up) * d * 2);
            }
            Gizmos.color = Color.red;
            foreach (var volume in exclusions) if (volume && volume.gameObject.activeInHierarchy) Gizmos.DrawWireCube(volume.bounds.center, volume.bounds.size);
        }
    }
}
