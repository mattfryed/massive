using Massive.Player;
using UnityEditor;
using UnityEngine;

namespace Massive.EditorTools
{
    public sealed partial class PlayerTuningWindow
    {
        private static readonly Color WindupColor = new Color(.46f, .5f, .57f);
        private static readonly Color HitColor = new Color(.95f, .52f, .23f);
        private static readonly Color RecoveryColor = new Color(.31f, .37f, .45f);
        private static readonly Color InputColor = new Color(.13f, .72f, .65f);
        private static readonly Color TailColor = new Color(.62f, .43f, .89f);
        private int dragId, dragGroup;
        private float dragRange, dragMouseTime, dragStartValue, dragEndValue;
        internal Vector2 EngagementHandlePoint { get; private set; }
        internal Vector2 HandoffHandlePoint { get; private set; }
        internal Vector2 DurationHandlePoint { get; private set; }
        private static GUIStyle brightBarLabel;

        private void DrawTimeline()
        {
            if (!tuning.attackProfile || tuning.attackProfile.Stages.Count == 0)
            { EditorGUILayout.HelpBox("Assign an attack profile to edit its stages.", MessageType.Info); return; }
            int count = tuning.attackProfile.Stages.Count;
            selectedStage = Mathf.Clamp(selectedStage, 0, count - 1);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < count; i++)
                    if (GUILayout.Toggle(selectedStage == i, StageLabel(i), EditorStyles.miniButton))
                    {
                        if (selectedStage != i) { Stop(); selectedStage = i; }
                    }
                frames = GUILayout.Toggle(frames, "Frames", GUILayout.Width(68));
            }
            GUILayout.Label("Orange: damage   ·   Teal: accept next press   ·   Violet: visual aftermath / handoff marker", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label("Drag timing handles or move the teal bar. Dragging an engagement window creates a per-stage override.", EditorStyles.wordWrappedMiniLabel);
            Rect area = GUILayoutUtility.GetRect(200, (selectedOnly ? 1 : count) * 112 + 32, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(area, new Color(.1f, .11f, .13f));
            const float labelWidth = 86;
            Rect plot = new Rect(area.x + labelWidth, area.y + 25, area.width - labelWidth - 16, area.height - 25);
            float range = dragId != 0 ? dragRange : Mathf.Max(.5f, PreviewLength() * 1.09f);
            float rate = tuning.attackProfile.GetStage(selectedStage).AnimationFrameRate;
            int ticks = Mathf.Clamp((int)(plot.width / 70), 3, 16);
            float roughStep = range / ticks;
            float magnitude = Mathf.Pow(10, Mathf.Floor(Mathf.Log10(roughStep)));
            float factor = roughStep / magnitude;
            float step = magnitude * (factor <= 1 ? 1 : factor <= 2 ? 2 : factor <= 5 ? 5 : 10);
            for (int n = 0; n * step <= range; n++)
            {
                float t = n * step;
                float x = TimeX(plot, range, t);
                EditorGUI.DrawRect(new Rect(x, plot.y - 3, 1, plot.height), new Color(.3f, .32f, .36f, .4f));
                GUI.Label(new Rect(x - 18, area.y + 4, 64, 20), frames ? $"{t * rate:0.#}f" : $"{t:0.##}s", EditorStyles.miniLabel);
            }
            float offset = 0; int row = 0;
            for (int i = 0; i < count; i++)
            {
                var stage = tuning.attackProfile.GetStage(i);
                if (selectedOnly && i != selectedStage) continue;
                float y = plot.y + row++ * 112;
                float start = selectedOnly ? 0 : offset;
                bool chainable = stage.AllowComboCancel && i + 1 < count;
                if (GUI.Button(new Rect(area.x + 3, y + 8, labelWidth - 8, 25), StageLabel(i), EditorStyles.miniButton))
                { selectedStage = i; cursor = 0; }
                GUI.Label(new Rect(area.x + 8, y + 49, labelWidth - 12, 20), chainable ? "Next press" : "No next stage", EditorStyles.miniLabel);
                GUI.Label(new Rect(area.x + 8, y + 80, labelWidth - 12, 20), "VFX tail", EditorStyles.miniLabel);
                float activeStart = stage.ActivationStartNormalized * stage.Duration;
                float activeEnd = stage.ActivationEndNormalized * stage.Duration;
                Bar(plot, range, start, start + activeStart, y + 17, 20, WindupColor, "Windup");
                Bar(plot, range, start + activeStart, start + activeEnd, y + 17, 20, HitColor, "Hit");
                Bar(plot, range, start + activeEnd, start + stage.Duration, y + 17, 20, RecoveryColor, "Recovery");
                GUI.Label(new Rect(TimeX(plot, range, start + stage.Duration) + 4, y + 17, 58, 20), $"{stage.Duration:0.###}s", EditorStyles.miniLabel);
                if (i == selectedStage)
                {
                    Handle(plot, range, i, 0, start, activeStart, y + 16, "Hit begins");
                    Handle(plot, range, i, 1, start, activeEnd, y + 16, "Hit ends");
                    Handle(plot, range, i, 2, start, stage.Duration, y - 2, "Stage ends (without early handoff)");
                }
                if (chainable)
                {
                    Vector2 window = PlayerTuningEditing.Window(stage, tuning);
                    var buffer = Bar(plot, range, start + Mathf.Max(0, window.x - tuning.combat.comboInputBuffer), start + window.x,
                        y + 54, 11, new Color(.16f, .34f, .35f), "");
                    GUI.Label(buffer, new GUIContent("", "Early input buffer — a recent press here is accepted when the engagement window opens."));
                    Rect bar = Bar(plot, range, start + window.x, start + window.y, y + 51, 17, InputColor,
                        stage.CustomComboWindow ? "Engage" : "Inherited");
                    var body = new Rect(bar.x + 6, bar.y, Mathf.Max(0, bar.width - 12), bar.height);
                    if (i == selectedStage) Drag(body, plot, range, i, 5, start, "Move engagement window");
                    if (i == selectedStage)
                    {
                        Handle(plot, range, i, 3, start, window.x, y + 48, "Accept next press from");
                        Handle(plot, range, i, 4, start, window.y, y + 48, "Accept next press until");
                        Handle(plot, range, i, 6, start, stage.ComboHandoffSeconds, y + 70, "Earliest next stage — drag to enable early handoff", TailColor);
                    }
                    float handoff = Mathf.Max(window.x, stage.ComboHandoffSeconds);
                    float hx = TimeX(plot, range, start + handoff);
                    EditorGUI.DrawRect(new Rect(hx, y + 38, 1, 41), TailColor);
                    GUI.Label(new Rect(Mathf.Min(hx + 7, plot.xMax - 62), y + 67, 66, 18), new GUIContent("Handoff", "Earliest start if the next press has been queued."), EditorStyles.miniLabel);
                    if (stage.EarlyComboHandoff && handoff < stage.Duration)
                    {
                        Rect cut = new Rect(hx, y + 17, Mathf.Max(0, TimeX(plot, range, start + stage.Duration) - hx), 20);
                        EditorGUI.DrawRect(cut, new Color(.08f, .1f, .12f, .52f));
                        GUI.Label(cut, new GUIContent("", "This portion is skipped when the next attack is queued at the earliest handoff."));
                    }
                }
                float tailStart = PlayerTuningEditing.TailStart(stage);
                // Thrust starts dissipating when it hands off, rather than waiting for its original end.
                if (chainable && stage.EarlyComboHandoff) tailStart = Mathf.Min(tailStart, Mathf.Max(PlayerTuningEditing.Window(stage, tuning).x, stage.ComboHandoffSeconds));
                float tailEnd = tailStart + PlayerTuningEditing.Tail(stage, visuals);
                Bar(plot, range, start + tailStart, start + tailEnd, y + 90, 7, TailColor, "");
                if (i == selectedStage) Handle(plot, range, i, 7, start + tailStart, PlayerTuningEditing.Tail(stage, visuals), y + 82, "Visual tail ends — no damage", TailColor);
                offset += chainable ? Mathf.Max(stage.ComboHandoffSeconds, PlayerTuningEditing.Window(stage, tuning).x) : stage.Duration;
            }
            float cx = TimeX(plot, range, cursor);
            EditorGUI.DrawRect(new Rect(cx, plot.y - 4, 2, plot.height), Color.white);
        }
        private static float TimeX(Rect plot, float range, float time) => plot.x + time / range * plot.width;
        private static Rect Bar(Rect plot, float range, float start, float end, float y, float height, Color color, string label)
        {
            var r = new Rect(TimeX(plot, range, start), y, Mathf.Max(2, (end - start) / range * plot.width), height);
            EditorGUI.DrawRect(r, color);
            if (r.width > 45 && height > 10)
            {
                if (brightBarLabel == null)
                {
                    brightBarLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                    brightBarLabel.normal.textColor = new Color(.06f, .08f, .10f);
                }
                GUI.Label(r, label, color == HitColor || color == InputColor || color == WindupColor ? brightBarLabel : EditorStyles.whiteMiniLabel);
            }
            return r;
        }
        private void Handle(Rect plot, float range, int stage, int kind, float offset, float value, float y, string label, Color? color = null)
        {
            var rect = new Rect(TimeX(plot, range, offset + value) - 5, y, 10, 15);
            if (kind == 3 && Event.current.type == EventType.Repaint)
                EngagementHandlePoint = GUIUtility.GUIToScreenPoint(rect.center) - position.position;
            if (kind == 6 && Event.current.type == EventType.Repaint)
                HandoffHandlePoint = GUIUtility.GUIToScreenPoint(rect.center) - position.position;
            if (kind == 2 && Event.current.type == EventType.Repaint)
                DurationHandlePoint = GUIUtility.GUIToScreenPoint(rect.center) - position.position;
            EditorGUI.DrawRect(rect, color ?? Color.white);
            GUI.Label(rect, new GUIContent("", label));
            Drag(rect, plot, range, stage, kind, offset, label);
        }
        private void Drag(Rect target, Rect plot, float range, int stageIndex, int kind, float offset, string label)
        {
            int id = GUIUtility.GetControlID(6000 + stageIndex * 20 + kind, FocusType.Passive, target);
            EditorGUIUtility.AddCursorRect(target, MouseCursor.ResizeHorizontal);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && target.Contains(e.mousePosition))
            {
                GUIUtility.hotControl = id; dragId = id; dragRange = range;
                GUIUtility.keyboardControl = 0;
                selectedStage = stageIndex;
                dragMouseTime = (e.mousePosition.x - plot.x) / plot.width * range - offset;
                var w = PlayerTuningEditing.Window(tuning.attackProfile.GetStage(stageIndex), tuning);
                dragStartValue = w.x; dragEndValue = w.y;
                Undo.IncrementCurrentGroup(); dragGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(label);
                Undo.RegisterCompleteObjectUndo(kind == 7 ? (Object)visuals : tuning.attackProfile, label);
                e.Use();
            }
            if (GUIUtility.hotControl != id || dragId != id) return;
            if (e.type == EventType.MouseDrag)
            {
                float value = Mathf.Max(0, (e.mousePosition.x - plot.x) / plot.width * dragRange - offset);
                if (frames) value = Mathf.Round(value * tuning.attackProfile.GetStage(stageIndex).AnimationFrameRate) / tuning.attackProfile.GetStage(stageIndex).AnimationFrameRate;
                ApplyDrag(stageIndex, kind, value); e.Use(); Repaint();
            }
            if (e.type == EventType.MouseUp || e.type == EventType.Ignore)
            {
                GUIUtility.hotControl = 0; dragId = 0;
                Undo.CollapseUndoOperations(dragGroup);
                PlayerTuningEditing.Save(kind == 7 ? (Object)visuals : tuning.attackProfile);
                if (e.type == EventType.MouseUp) e.Use();
            }
        }
        private void ApplyDrag(int stageIndex, int kind, float seconds)
        {
            var stage = tuning.attackProfile.GetStage(stageIndex);
            using (var data = new SerializedObject(kind == 7 ? (Object)visuals : tuning.attackProfile))
            {
                data.Update();
                if (kind == 7) data.FindProperty(PlayerTuningEditing.TailField(stage)).floatValue = Mathf.Clamp(seconds, 0, 1.5f);
                else
                {
                    var s = data.FindProperty("stages").GetArrayElementAtIndex(stageIndex);
                    if (kind == 0) s.FindPropertyRelative("activationFrameStart").floatValue = Mathf.Clamp(seconds, 0, stage.ActivationEndNormalized * stage.Duration) * stage.AnimationFrameRate;
                    if (kind == 1) s.FindPropertyRelative("activationFrameEnd").floatValue = Mathf.Clamp(seconds, stage.ActivationStartNormalized * stage.Duration, stage.Duration) * stage.AnimationFrameRate;
                    if (kind == 2) s.FindPropertyRelative("duration").floatValue = Mathf.Clamp(seconds, .01f, 10);
                    if (kind >= 3 && kind <= 6)
                    {
                        PlayerTuningEditing.EnableCustomWindow(s, stage, tuning);
                        var start = s.FindPropertyRelative("comboWindowStartSeconds");
                        var end = s.FindPropertyRelative("comboWindowEndSeconds");
                        if (kind == 3) start.floatValue = Mathf.Clamp(seconds, 0, Mathf.Min(stage.Duration, end.floatValue));
                        if (kind == 4) end.floatValue = Mathf.Clamp(seconds, Mathf.Min(stage.Duration, start.floatValue), stage.Duration);
                        if (kind == 5)
                        {
                            float width = dragEndValue - dragStartValue;
                            start.floatValue = Mathf.Clamp(dragStartValue + seconds - dragMouseTime, 0, Mathf.Max(0, stage.Duration - width));
                            end.floatValue = start.floatValue + width;
                        }
                        if (kind == 6)
                        {
                            s.FindPropertyRelative("earlyComboHandoff").boolValue = true;
                            s.FindPropertyRelative("comboHandoffSeconds").floatValue = Mathf.Clamp(seconds, .01f, stage.Duration);
                        }
                    }
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data.targetObject);
            }
        }
    }
}
