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
        }
        public Vector3 World(Vector2 normalized)
        {
            if (!arena || !arena.Grid) return new Vector3(transform.position.x, gameplayHeight, transform.position.z);
            var half = arena.Current.halfSizeLocal;
            float sign = Vector3.Dot(arena.Current.axisY_WS, Vector3.forward) >= 0 ? 1 : -1;
            var p = arena.Grid.transform.TransformPoint(new Vector3(normalized.x * half.x, normalized.y * half.y * sign, 0));
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
        public bool Resolve(EnemyFormation.Slot slot, bool mirror, out Pose pose, out string reason)
        {
            pose = default; reason = null;
            if (!arena || !arena.IsValid) { reason = "Missing arena bounds"; return false; }
            if (slot == null || !slot.enemy || !slot.enemy.prefab || !slot.telegraph)
            { reason = "Missing enemy or telegraph"; return false; }
            Vector2 point, facing;
            if (!string.IsNullOrEmpty(slot.socket))
            {
                // Socket selection is explicit: mirrored variants never silently jump to another wall.
                pose.socket = sockets.Find(s => s.enabled && s.id == slot.socket);
                if (pose.socket == null) { reason = "Unavailable socket: " + slot.socket; return false; }
                point = pose.socket.position; facing = pose.socket.inward;
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
            pose.position = World(point); pose.rotation = Quaternion.LookRotation(Direction(facing), Vector3.up);
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
                var r = region.rectangle; var a = World(r.min); var b = World(new Vector2(r.xMax, r.yMin));
                var c = World(r.max); var d = World(new Vector2(r.xMin, r.yMax));
                Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
            }
            foreach (var socket in sockets)
            {
                if (!socket.enabled) continue; Gizmos.color = Color.yellow;
                var p = World(socket.position); var d = Direction(socket.inward);
                Gizmos.DrawWireSphere(p, .2f); Gizmos.DrawRay(p, d * 1.5f);
                Gizmos.DrawRay(p, Quaternion.AngleAxis(socket.aimArc, Vector3.up) * d * 2);
                Gizmos.DrawRay(p, Quaternion.AngleAxis(-socket.aimArc, Vector3.up) * d * 2);
            }
            Gizmos.color = Color.red;
            foreach (var volume in exclusions) if (volume && volume.gameObject.activeInHierarchy) Gizmos.DrawWireCube(volume.bounds.center, volume.bounds.size);
        }
    }
}
