#if UNITY_EDITOR
using System;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;

public sealed partial class EnemyEncounterComposer
{
    private sealed class ArenaDrag
    {
        public EnemyFormation formation;
        public EnemyArenaLayout layout;
        public int cue, slot, control;
        public bool mirror, flip, moved;
        public Vector2 start;
        public Vector3 grabOffset;
        public EnemyFormation.Slot source, candidate;
    }
    private ArenaDrag arenaDrag;
    private string placementHint;

    private void CancelArenaDrag()
    {
        if (arenaDrag != null && GUIUtility.hotControl == arenaDrag.control) GUIUtility.hotControl = 0;
        arenaDrag = null; placementHint = null;
    }
    private EnemyFormation.Slot PreviewSlot(EnemyFormation formation, int cue, int index)
    {
        if (arenaDrag != null && arenaDrag.formation == formation && arenaDrag.cue == cue && arenaDrag.slot == index)
            return arenaDrag.candidate;
        return cue >= 0 && cue < timeline.cues.Count ? timeline.cues[cue].GetSlot(formation, index) : formation.slots[index];
    }
    private void HandleArenaDrag(Rect rect, EnemyFormation formation, bool mirror, float scale,
        Func<Vector3, Vector2> map, Func<Vector2, Vector3> unmap)
    {
        int control = GUIUtility.GetControlID("EnemySpawnDrag".GetHashCode(), FocusType.Passive);
        bool flip = selected >= 0 && timeline.cues[selected].flipWallOrientation;
        var e = Event.current;
        if (e.type == EventType.MouseMove) Repaint();
        if (arenaDrag != null && (!CanEdit || arenaDrag.formation != formation || arenaDrag.cue != selected ||
            arenaDrag.layout != layout || arenaDrag.mirror != mirror || arenaDrag.flip != flip)) CancelArenaDrag();
        if (!CanEdit) return;
        if (arenaDrag != null)
        {
            if (e.type == EventType.MouseDrag)
            {
                arenaDrag.moved |= (e.mousePosition - arenaDrag.start).sqrMagnitude > 4;
                if (arenaDrag.moved && EnemySpawnPlacementAuthoring.Project(layout, arenaDrag.source, mirror, flip,
                    unmap(e.mousePosition) - arenaDrag.grabOffset, out var candidate)) arenaDrag.candidate = candidate;
                e.Use(); Repaint();
            }
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                var drag = arenaDrag; CancelArenaDrag();
                if (drag.moved && EnemySpawnPlacementAuthoring.Commit(timeline, drag.cue, drag.formation, drag.slot, drag.candidate)) Changed(false);
                e.Use(); Repaint();
            }
            return;
        }
        placementHint = null;
        if (!rect.Contains(e.mousePosition)) return;
        int nearest = -1; float best = float.MaxValue;
        EnemyArenaLayout.Pose nearestPose = default;
        for (int i = 0; i < formation.slots.Count; i++)
        {
            var slot = PreviewSlot(formation, selected, i);
            if (!layout.Resolve(slot, mirror, out var pose, out _, flip)) continue;
            float distance = Vector2.Distance(e.mousePosition, map(pose.clearance));
            if (distance > Mathf.Max(7, slot.enemy.GetSpawnRadiusWorld() * scale + 3) || distance >= best) continue;
            best = distance; nearest = i; nearestPose = pose;
        }
        if (nearest < 0) return;
        if (nearestPose.socket != null && !nearestPose.socket.useRange)
        { placementHint = "Fixed wall mount: enable its wall range to drag this spawn."; return; }
        EditorGUIUtility.AddCursorRect(rect, MouseCursor.MoveArrow);
        placementHint = $"Slot {nearest + 1}: drag " + (nearestPose.socket == null ? "within its formation region." : "along its wall range.");
        if (e.type != EventType.MouseDown || e.button != 0 || GUIUtility.hotControl != 0) return;
        var source = PreviewSlot(formation, selected, nearest).Copy();
        arenaDrag = new ArenaDrag { formation = formation, layout = layout, cue = selected, slot = nearest,
            control = control, mirror = mirror, flip = flip, source = source, candidate = source.Copy(),
            start = e.mousePosition, grabOffset = unmap(e.mousePosition) - nearestPose.clearance };
        GUIUtility.hotControl = control; GUIUtility.keyboardControl = 0;
        e.Use(); Repaint();
    }
    private void DrawPlacementControls(EnemyFormation formation)
    {
        GUILayout.Label(!CanEdit ? "Placement editing is available outside Play Mode." : selected >= 0
            ? "Drag a spawn icon to move it for this encounter. Escape cancels; Undo restores."
            : "Drag a spawn icon to edit this shared formation for every encounter using it.", EditorStyles.wordWrappedMiniLabel);
        // Keep the control count/height identical in Layout and Repaint. Hit testing
        // uses the resolved rect, which is only available after the Layout pass.
        GUILayout.Label(placementHint ?? " ", EditorStyles.wordWrappedMiniLabel, GUILayout.Height(30));
        if (selected < 0) return;
        var entries = timeline.cues[selected].placementOverrides;
        bool hasOverrides = entries != null && entries.Exists(p => p != null && p.formation == formation);
        using (new EditorGUI.DisabledScope(!CanEdit || !hasOverrides))
            if (GUILayout.Button("Reset placements to formation"))
            { CancelArenaDrag(); if (EnemySpawnPlacementAuthoring.Reset(timeline, selected, formation)) Changed(false); }
    }
}
#endif
