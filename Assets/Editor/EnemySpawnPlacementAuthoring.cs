#if UNITY_EDITOR
using Massive.Enemies;
using UnityEditor;
using UnityEngine;

// Shared by the miniature arena and validation; runtime always resolves the same slot data.
internal static class EnemySpawnPlacementAuthoring
{
    public static bool Project(EnemyArenaLayout layout, EnemyFormation.Slot source, bool mirror, bool flip,
        Vector3 target, out EnemyFormation.Slot candidate)
    {
        candidate = null;
        if (!layout || !layout.Resolve(source, mirror, out var pose, out _, flip)) return false;
        candidate = source.Copy();
        if (pose.socket != null)
        {
            if (!pose.socket.useRange) return false;
            float Distance(float t) => (layout.WallPose(pose.socket, t).clearance - target).sqrMagnitude;
            float best = 0, distance = float.MaxValue;
            for (int i = 0; i <= 64; i++)
            {
                float t = i / 64f, d = Distance(t);
                if (d < distance) { best = t; distance = d; }
            }
            float lo = Mathf.Max(0, best - 1 / 64f), hi = Mathf.Min(1, best + 1 / 64f);
            for (int i = 0; i < 18; i++)
            {
                float a = Mathf.Lerp(lo, hi, 1 / 3f), b = Mathf.Lerp(lo, hi, 2 / 3f);
                if (Distance(a) < Distance(b)) hi = b; else lo = a;
            }
            candidate.overrideWallPosition = true; candidate.wallPosition = (lo + hi) * .5f;
            return true;
        }
        // Invert Resolve a column at a time. This includes oval warping, insets,
        // region bounds, grid transforms and seeded horizontal reflection.
        var probe = source.Copy();
        Vector3 At(Vector2 position)
        { probe.position = position; layout.Resolve(probe, mirror, out var p, out _, flip); return p.clearance; }
        float Fraction(Vector3 a, Vector3 b, Vector3 axis)
        {
            float length = Vector3.Dot(b - a, axis);
            return Mathf.Abs(length) < .00001f ? .5f : Mathf.Clamp01(Vector3.Dot(target - a, axis) / length);
        }
        float x = Fraction(At(new Vector2(0, .5f)), At(new Vector2(1, .5f)), layout.Direction(Vector2.right));
        float y = Fraction(At(new Vector2(x, 0)), At(new Vector2(x, 1)), layout.Direction(Vector2.up));
        candidate.position = new Vector2(x, y);
        return true;
    }

    public static bool Commit(EnemyEncounterTimeline timeline, int cueIndex, EnemyFormation formation, int slotIndex, EnemyFormation.Slot candidate)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !formation || candidate == null || slotIndex < 0 || slotIndex >= formation.slots.Count) return false;
        if (cueIndex >= 0 && (!timeline || cueIndex >= timeline.cues.Count)) return false;
        var cue = cueIndex >= 0 ? timeline.cues[cueIndex] : null;
        var original = cue != null ? cue.GetSlot(formation, slotIndex) : formation.slots[slotIndex];
        if (original == null || original.position == candidate.position && original.overrideWallPosition == candidate.overrideWallPosition &&
            Mathf.Approximately(original.wallPosition, candidate.wallPosition)) return false;
        Object owner = cue != null ? timeline : formation;
        Undo.RegisterCompleteObjectUndo(owner, "Move enemy spawn");
        if (cue != null)
        {
            cue.placementOverrides ??= new();
            cue.placementOverrides.RemoveAll(p => p != null && p.formation == formation && p.slotIndex == slotIndex);
            cue.placementOverrides.Add(new EnemyEncounterTimeline.SlotPlacement {
                formation = formation, slotIndex = slotIndex, position = candidate.position,
                overrideWallPosition = candidate.overrideWallPosition, wallPosition = candidate.wallPosition });
        }
        else
        {
            original.position = candidate.position; original.overrideWallPosition = candidate.overrideWallPosition;
            original.wallPosition = candidate.wallPosition;
        }
        Save(owner); return true;
    }

    public static bool Reset(EnemyEncounterTimeline timeline, int cueIndex, EnemyFormation formation)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !timeline || cueIndex < 0 || cueIndex >= timeline.cues.Count) return false;
        var entries = timeline.cues[cueIndex].placementOverrides;
        if (entries == null || !entries.Exists(p => p != null && p.formation == formation)) return false;
        Undo.RegisterCompleteObjectUndo(timeline, "Reset enemy spawn placements");
        entries.RemoveAll(p => p != null && p.formation == formation); Save(timeline); return true;
    }
    private static void Save(Object owner)
    { EditorUtility.SetDirty(owner); if (AssetDatabase.Contains(owner)) AssetDatabase.SaveAssetIfDirty(owner); }
}
#endif
