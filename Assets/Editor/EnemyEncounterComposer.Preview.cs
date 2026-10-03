#if UNITY_EDITOR
using System.Linq;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;

public sealed partial class EnemyEncounterComposer
{
    private void DrawDetails(Rect rect)
    {
        EditorGUI.DrawRect(rect, Panel);
        GUILayout.BeginArea(new Rect(rect.x + 10, rect.y + 8, rect.width - 20, rect.height - 16));
        detailScroll = GUILayout.BeginScrollView(detailScroll);
        DrawPlayerControl();
        var serialized = new SerializedObject(timeline); serialized.Update();
        GUILayout.Label("TIMELINE", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!CanEdit))
        {
            EditorGUILayout.PropertyField(serialized.FindProperty("duration"));
        }
        DrawPopulation(serialized);
        var chosenLayout = (EnemyArenaLayout)EditorGUILayout.ObjectField("Arena layout", layout, typeof(EnemyArenaLayout), true);
        if (chosenLayout != layout) { layout = chosenLayout; validationDirty = true; }
        GUILayout.Space(12);
        EnemyFormation formation = librarySelection; bool mirror = false;
        if (selected >= 0 && selected < timeline.cues.Count)
        {
            GUILayout.Label("SELECTED ENCOUNTER", EditorStyles.boldLabel);
            var cue = serialized.FindProperty("cues").GetArrayElementAtIndex(selected);
            using (new EditorGUI.DisabledScope(!CanEdit))
            {
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("label"));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("formation"));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("arrivalSeconds"), new GUIContent("First arrival (s)"));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("allowedLateness"), new GUIContent("Max lateness (s)"));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("allowHorizontalMirror"), new GUIContent("Allow seeded mirroring"));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("variants"), true);
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("fallback"));
                EditorGUILayout.PropertyField(cue.FindPropertyRelative("overrideSpawnPolicy"), new GUIContent("Override spawn policy"));
                using (new EditorGUI.DisabledScope(!cue.FindPropertyRelative("overrideSpawnPolicy").boolValue))
                {
                    if (cue.FindPropertyRelative("overrideSpawnPolicy").boolValue)
                    {
                        EditorGUILayout.PropertyField(cue.FindPropertyRelative("integrity"), new GUIContent("Formation integrity"));
                        EditorGUILayout.PropertyField(cue.FindPropertyRelative("maxPositionAdjustment"), new GUIContent("Max adjustment (units)"));
                        EditorGUILayout.PropertyField(cue.FindPropertyRelative("blockedSlotGrace"), new GUIContent("Blocked-slot grace (s)"));
                    }
                }
            }
            formation = EnemyEncounterAuthoring.Choose(timeline, selected, seed, out mirror);
            using (new EditorGUI.DisabledScope(!CanEdit))
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Duplicate")) { Duplicate(); GUIUtility.ExitGUI(); }
                if (GUILayout.Button("Remove")) { Remove(); GUIUtility.ExitGUI(); }
                GUILayout.EndHorizontal();
            }
            if (formation)
            {
                var value = timeline.cues[selected];
                GUILayout.Label($"{value.IntegrityFor(formation)} · Adjustment ≤ {value.AdjustmentFor(formation):0.##} units · Slot grace {value.GraceFor(formation):0.##}s" +
                    (value.overrideSpawnPolicy ? " (cue override)" : " (formation defaults)"), small);
                GUILayout.Label($"Seed {seed}: {formation.name}" + (mirror ? " (mirrored)" : ""), small);
                GUILayout.Label($"Warning {value.arrivalSeconds - formation.warningSeconds:0.00}s → First {value.arrivalSeconds:0.00}s → Last {value.arrivalSeconds + EnemyEncounterAuthoring.LastDelay(formation):0.00}s", small);
                if (Live)
                {
                    var state = Director.CueStates.FirstOrDefault(c => c.index == selected);
                    if (state != null)
                    {
                        GUILayout.Label($"Live: {state.state} · {state.Progress}", EditorStyles.wordWrappedLabel);
                        if (!string.IsNullOrEmpty(state.reason)) EditorGUILayout.HelpBox(state.reason, MessageType.Warning);
                        // Fallbacks are chosen during reservation, so show the actual runtime footprint.
                        if (state.formation) { formation = state.formation; mirror = state.mirror; }
                        foreach (var slot in state.slots.Where(s => !string.IsNullOrEmpty(s.reason)).Take(4))
                            GUILayout.Label($"Slot {slot.index + 1}: {slot.reason}", small);
                    }
                }
            }
        }
        else GUILayout.Label("Select a timeline block or library formation.", EditorStyles.wordWrappedLabel);
        if (serialized.ApplyModifiedProperties())
        {
            timeline.duration = Mathf.Max(1, timeline.duration); timeline.maxPressure = Mathf.Max(0, timeline.maxPressure);
            timeline.maxAliveTotal = Mathf.Max(0, timeline.maxAliveTotal);
            if (selected >= 0) { timeline.cues[selected].arrivalSeconds = Mathf.Max(0, timeline.cues[selected].arrivalSeconds); timeline.cues[selected].allowedLateness = Mathf.Max(0, timeline.cues[selected].allowedLateness); }
            Changed();
        }
        GUILayout.Space(12);
        if (formation)
        {
            GUILayout.Label("ARENA PLACEMENT", EditorStyles.boldLabel);
            GUILayout.Label($"{formation.slots.Count} enemies · Warning {formation.warningSeconds:0.##}s · Spawn span {EnemyEncounterAuthoring.LastDelay(formation):0.##}s", small);
            DrawArena(GUILayoutUtility.GetRect(100, 142, GUILayout.ExpandWidth(true)), formation, mirror);
            GUILayout.Label("Blue: planned · Orange: adjusted · Red: blocked/skipped · Green: spawned", small);
            if (selected < 0)
            {
                var shared = new SerializedObject(formation); shared.Update();
                GUILayout.Label("SHARED SPAWN SETTINGS", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(!CanEdit))
                {
                    EditorGUILayout.PropertyField(shared.FindProperty("integrity"));
                    EditorGUILayout.PropertyField(shared.FindProperty("maxPositionAdjustment"), new GUIContent("Max adjustment"));
                    EditorGUILayout.PropertyField(shared.FindProperty("blockedSlotGrace"), new GUIContent("Blocked-slot grace"));
                    EditorGUILayout.PropertyField(shared.FindProperty("adjustmentPattern"));
                }
                if (shared.ApplyModifiedProperties()) { AssetDatabase.SaveAssetIfDirty(formation); Changed(false); }
            }
            if (GUILayout.Button("Inspect shared formation asset")) { Selection.activeObject = formation; EditorGUIUtility.PingObject(formation); }
            GUILayout.Label("Count, spacing and stagger are shared formation settings. Timing and alternatives belong to this cue.", small);
        }
        GUILayout.Space(12); GUILayout.Label("AUTHORING CHECKS", EditorStyles.boldLabel);
        if (!layout) EditorGUILayout.HelpBox("Assign an arena layout to preview placement and check boundaries, exclusions and mount sockets.", MessageType.Info);
        var relevant = selected < 0 ? issues : issues.Where(i => i.cue == selected).ToList();
        if (relevant.Count == 0) GUILayout.Label("No issues found for the selected seed.", small);
        foreach (var issue in relevant.Take(20))
            EditorGUILayout.HelpBox((selected < 0 ? timeline.cues[issue.cue].label + ": " : "") + issue.message, MessageType.Warning);
        if (relevant.Count > 20) GUILayout.Label($"{relevant.Count - 20} additional issues. Select a cue to inspect it.", small);
        GUILayout.Label("Checks cover authored placements and batch budgets. Live enemies and player movement can still block a later arrival. Blocks show spawning, not enemy lifetime.", small);
        if (GUILayout.Button("Recheck layout")) validationDirty = true;
        GUILayout.EndScrollView(); GUILayout.EndArea();
    }

    private void DrawPlayerControl()
    {
        if (!lab) return;
        GUILayout.Label("PREVIEW PLAYER", EditorStyles.boldLabel);
        var preview = new SerializedObject(lab); preview.Update();
        EditorGUILayout.PropertyField(preview.FindProperty("manualPlayerControl"), new GUIContent("Manual control (P1)"));
        preview.ApplyModifiedProperties();
        if (lab.manualPlayerControl)
            EditorGUILayout.HelpBox("Click the Game view. The left actor uses your normal P1 movement, attack and shield controls. The other actor stays automated. Turn off to return control to the Lab.", MessageType.Info);
        GUILayout.Space(8);
    }

    private void DrawPopulation(SerializedObject serialized)
    {
        GUILayout.Space(6);
        GUILayout.Label("POPULATION", EditorStyles.boldLabel);
        GUILayout.Label("Timeline owns these limits. 0 = unlimited. Counts alive + reserved arrivals, including Carrier-launched Drones.", small);
        var budget = Live ? Director.CapturePopulationBudget() : null;
        using (new EditorGUI.DisabledScope(!CanEdit))
        {
            var total = serialized.FindProperty("maxAliveTotal");
            total.intValue = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Total limit", total.tooltip), total.intValue));
            EditorGUILayout.PropertyField(serialized.FindProperty("maxPressure"), new GUIContent("Pressure limit"));
            var limits = serialized.FindProperty("populationLimits");
            var definitions = types.Concat(timeline.populationLimits.Where(l => l != null).Select(l => l.enemy))
                .Where(d => d).Distinct().OrderBy(EnemyEncounterAuthoring.Name);
            foreach (var definition in definitions)
            {
                int current = timeline.Limit(definition);
                string label = EnemyEncounterAuthoring.Name(definition);
                string tooltip = "Optional per-enemy limit. 0 = unlimited.";
                if (budget != null) tooltip += " Current: " + budget.For(definition);
                int value = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent(label, tooltip), current));
                if (value == current) continue;
                // Normalize duplicate entries when edited so Inspector and Composer agree.
                for (int i = limits.arraySize - 1; i >= 0; i--)
                    if (limits.GetArrayElementAtIndex(i).FindPropertyRelative("enemy").objectReferenceValue == definition)
                        limits.DeleteArrayElementAtIndex(i);
                int index = limits.arraySize++; var entry = limits.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("enemy").objectReferenceValue = definition;
                entry.FindPropertyRelative("maxAlive").intValue = value;
            }
            EditorGUILayout.PropertyField(serialized.FindProperty("pressureCosts"), new GUIContent("Pressure costs"), true);
        }
        if (budget != null)
        {
            GUILayout.Label($"Live: {budget.Total.alive} alive + {budget.Total.reserved} reserved / {LimitText(timeline.maxAliveTotal)} total", small);
            GUILayout.Label($"Pressure: {budget.Pressure:0.##} / {LimitText(timeline.maxPressure)}", small);
        }
        if (!lab) return;
        var preview = new SerializedObject(lab); preview.Update();
        EditorGUILayout.PropertyField(preview.FindProperty("ignorePopulationLimits"), new GUIContent("Unlimited Lab preview", "Ignore population/pressure limits in this Lab only. Placement and warnings still apply."));
        preview.ApplyModifiedProperties();
        if (lab.ignorePopulationLimits)
            EditorGUILayout.HelpBox("UNLIMITED PREVIEW — population and pressure checks bypassed in the Lab. Timeline limits remain saved for gameplay.", MessageType.Warning);
    }
    private static string LimitText(float limit) => limit > 0 ? limit.ToString("0.##") : "Unlimited";
    private void DrawArena(Rect rect, EnemyFormation formation, bool mirror)
    {
        EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(.11f, .11f, .11f) : new Color(.95f, .95f, .95f));
        if (!layout || !layout.arena || !layout.arena.IsValid)
        { GUI.Label(rect, "Choose an arena layout", EditorStyles.centeredGreyMiniLabel); return; }
        Vector3 center = layout.World(Vector2.zero), axisX = layout.World(Vector2.right) - center, axisY = layout.World(Vector2.up) - center;
        if (axisX.sqrMagnitude < .01f || axisY.sqrMagnitude < .01f) return;
        float scale = Mathf.Min((rect.width - 18) / (axisX.magnitude * 2), (rect.height - 18) / (axisY.magnitude * 2));
        Vector2 Map(Vector3 p) => rect.center + new Vector2(Vector3.Dot(p - center, axisX.normalized), -Vector3.Dot(p - center, axisY.normalized)) * scale;
        var a = Map(layout.World(new Vector2(-1, 1))); var b = Map(layout.World(new Vector2(1, -1)));
        Color old = Handles.color; Handles.color = Grid;
        Handles.DrawAAPolyLine(1, new Vector3(a.x, a.y, 0), new Vector3(b.x, a.y, 0), new Vector3(b.x, b.y, 0), new Vector3(a.x, b.y, 0), new Vector3(a.x, a.y, 0));
        foreach (var volume in layout.exclusions)
        {
            if (!volume || !volume.enabled || !volume.gameObject.activeInHierarchy) continue;
            var bounds = volume.bounds; var min = new Vector2(float.MaxValue, float.MaxValue); var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var p = Map(bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            var r = Rect.MinMaxRect(Mathf.Max(rect.x, min.x), Mathf.Max(rect.y, min.y), Mathf.Min(rect.xMax, max.x), Mathf.Min(rect.yMax, max.y));
            if (r.width > 0 && r.height > 0) EditorGUI.DrawRect(r, Grid);
        }
        void Draw(EnemyFormation value, bool reflected, bool active)
        {
            var live = active && Live && selected >= 0 ? Director.CueStates.FirstOrDefault(c => c.index == selected) : null;
            for (int i = 0; i < value.slots.Count; i++)
            {
                var slot = value.slots[i];
                if (slot == null || !slot.enemy || !layout.Resolve(slot, reflected, out var pose, out _)) continue;
                float radius = Mathf.Max(Director ? Director.spawnCheckRadiusWorld : 0, slot.enemy.GetSpawnRadiusWorld());
                bool clear = layout.Clear(pose, radius, Director ? Director.borderBufferWorld : 0, out _);
                var status = live != null && i < live.slots.Count ? live.slots[i] : null;
                Vector2 authored = Map(pose.clearance);
                if (status != null) { pose.position = status.position; pose.clearance = status.clearance; pose.rotation = status.rotation; }
                Vector2 p = Map(pose.clearance);
                if (!rect.Contains(p)) continue;
                Handles.color = !active ? new Color(.5f, .5f, .5f, .5f) : clear ? ArrivalColor : new Color(1, .35f, .3f);
                if (status != null)
                {
                    if (status.adjusted) { Handles.color = new Color(1, .65f, .2f); Handles.DrawDottedLine(authored, p, 3); }
                    if (status.state == "Spawned") Handles.color = new Color(.4f, .85f, .5f);
                    if (status.state == "Blocked" || status.state == "Skipped") Handles.color = new Color(1, .35f, .3f);
                }
                Handles.DrawWireDisc(p, Vector3.forward, Mathf.Max(2, radius * scale));
                Handles.DrawLine(p, Map(pose.clearance + pose.rotation * Vector3.forward * radius * 1.5f));
            }
        }
        if (selected >= 0) for (int i = 0; i < timeline.cues.Count; i++)
        {
            if (i == selected || Mathf.Abs(timeline.cues[i].arrivalSeconds - TimeOf(selected)) > .01f) continue;
            var other = EnemyEncounterAuthoring.Choose(timeline, i, seed, out bool reflected); if (other) Draw(other, reflected, false);
        }
        Draw(formation, mirror, true); Handles.color = old;
    }
}
#endif
