using Massive.Multiplier;
using UnityEditor;
using UnityEngine;

namespace Massive.Multiplier.Editor
{
    [CustomEditor(typeof(AmplifierSpawnRegion)), CanEditMultipleObjects]
    public sealed class AmplifierSpawnRegionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Scene view dots show candidate Core centers: green = clear, red = excluded. " +
                "The teal outline includes the Core radius and safety margins. Physics is checked again at the actual spawn time. " +
                "For NOVA, add the star's entry collider to No Go Colliders, or add a Power Up No Spawn Zone marker to an authored volume. " +
                "Ordinary triggers are deliberately not all treated as obstacles.", MessageType.Info);
            foreach (UnityEngine.Object obj in targets)
            {
                var region = (AmplifierSpawnRegion)obj;
                Rect rect; string reason;
                if (!region.TryGetNeutralRect(region.previewObjectRadius, out rect, out reason))
                    EditorGUILayout.HelpBox(reason, MessageType.Warning);
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawRegion(AmplifierSpawnRegion region, GizmoType type)
        {
            if (!region.drawZones || !region.gameObject.activeInHierarchy || region.arenaBounds == null || !region.arenaBounds.IsValid) return;
            Color previous = Handles.color;
            region.RefreshPlacementCache(region.previewPattern);
            Rect rect; string reason;
            bool hasRoom = region.TryGetNeutralRect(region.previewObjectRadius, out rect, out reason);
            float lift = .08f;
            if (hasRoom)
            {
                Vector3[] border = {
                    region.GridPointToWorld(new Vector2(rect.xMin, rect.yMin)) + Vector3.up * lift,
                    region.GridPointToWorld(new Vector2(rect.xMax, rect.yMin)) + Vector3.up * lift,
                    region.GridPointToWorld(new Vector2(rect.xMax, rect.yMax)) + Vector3.up * lift,
                    region.GridPointToWorld(new Vector2(rect.xMin, rect.yMax)) + Vector3.up * lift
                };
                Handles.DrawSolidRectangleWithOutline(border, new Color(.1f, .8f, .6f, .055f), new Color(.2f, 1f, .75f, .8f));
                Handles.color = new Color(.6f, 1f, .8f);
                Handles.Label(border[2], "Amplifier safe-center region\nGreen: clear   Red: no-go / occupied");
            }
            else Handles.Label(region.transform.position, reason);

            // Preview uses the same shape, goal, player and authored-pattern checks as spawning.
            Vector2 half = region.arenaBounds.GetHalfSizeLocalInset();
            int columns = Mathf.Clamp(region.previewColumns, 5, 41), rows = Mathf.Clamp(region.previewRows, 5, 25);
            float pointSize = Mathf.Min(half.x * 2f / columns, half.y * 2f / rows) * .09f;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    Vector2 local = new Vector2(Mathf.Lerp(-half.x, half.x, (x + .5f) / columns), Mathf.Lerp(-half.y, half.y, (y + .5f) / rows));
                    Vector3 point = region.GridPointToWorld(local);
                    bool valid = region.IsValidCached(point, region.previewObjectRadius, out reason);
                    Handles.color = valid ? new Color(.15f, 1f, .35f, .85f) : new Color(1f, .2f, .15f, .4f);
                    Handles.DrawSolidDisc(point + Vector3.up * lift, Vector3.up, Mathf.Max(.02f, pointSize));
                }
            Handles.color = new Color(1f, .35f, .1f, .65f);
            foreach (Collider zone in region.CachedNoGoColliders)
            {
                if (zone == null) continue;
                Vector3 center = zone.bounds.center; center.y = region.spawnHeightWorld + lift;
                Handles.DrawWireCube(center, new Vector3(zone.bounds.size.x, .02f, zone.bounds.size.z));
                Handles.Label(center, "No-go: " + zone.name);
            }
            foreach (AmplifierGoalCapture goal in region.CachedGoals)
            {
                if (goal == null) continue;
                Vector3 center = goal.CapturePoint.position; center.y = region.spawnHeightWorld + lift;
                Handles.DrawWireDisc(center, Vector3.up, goal.AttractionRadius + region.goalExclusionPadding + region.clearanceWorld + region.previewObjectRadius);
            }
            Handles.color = previous;
        }
    }
}
