using Massive.Player;
using UnityEditor;
using UnityEngine;

namespace Massive.EditorTools
{
    public sealed partial class PlayerTuningWindow
    {
        private int coneDrag;
        internal Vector2 ReversalHandlePoint { get; private set; }

        private void DrawReversalCone()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Joystick reversal — drag either cone edge", EditorStyles.boldLabel);
            playerSlot = GUILayout.Toolbar(playerSlot, new[] { "P1", "P2", "P3", "P4" });
            var profile = PlayerGlobalModifiersEditing.GetOrCreate();
            using (var d = new SerializedObject(profile))
            {
                d.Update();
                EditorGUILayout.PropertyField(d.FindProperty("globalReversalEnabled"), new GUIContent("Global reversal enabled"));
                var slot = d.FindProperty("players").GetArrayElementAtIndex(playerSlot);
                Field(slot, "reversalEnabled", "Enable for this player");
                Field(slot, "backwardConeDegrees", "Full backward cone (degrees)");
                Field(slot, "retainedMomentum", "Momentum retained (0–1)");
                if (d.ApplyModifiedProperties()) PlayerTuningEditing.Save(profile);
            }
            var setting = profile.ForPlayer(playerSlot);
            Rect box = GUILayoutUtility.GetRect(200, 190, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(box, new Color(.1f, .11f, .13f));
            Vector2 center = new Vector2(box.center.x, box.y + 92);
            const float radius = 67;
            float half = setting.backwardConeDegrees * .5f * Mathf.Deg2Rad;
            Handles.BeginGUI();
            Handles.color = new Color(InputColor.r, InputColor.g, InputColor.b, .27f);
            var fan = new Vector3[35]; fan[0] = center;
            for (int i = 0; i <= 33; i++)
            {
                float angle = Mathf.PI * .5f - half + 2 * half * i / 33;
                fan[i + 1] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            Handles.DrawAAConvexPolygon(fan);
            Handles.color = Color.white;
            Handles.DrawWireDisc(center, Vector3.forward, radius);
            Handles.DrawAAPolyLine(2, center, center + Vector2.down * 69);
            Handles.DrawAAPolyLine(2, center + new Vector2(-5, -62), center + Vector2.down * 69, center + new Vector2(5, -62));
            Handles.EndGUI();
            GUI.Label(new Rect(center.x + 12, center.y - 79, 145, 22), "Current travel", EditorStyles.miniLabel);
            GUI.Label(new Rect(box.x + 10, box.yMax - 24, box.width - 20, 22), $"{setting.backwardConeDegrees:0}° behind travel · {setting.retainedMomentum:P0} old momentum retained", EditorStyles.centeredGreyMiniLabel);
            for (int side = -1; side <= 1; side += 2)
            {
                float angle = Mathf.PI * .5f + side * half;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Rect handle = new Rect(point.x - 6, point.y - 6, 12, 12);
                if (side == -1 && Event.current.type == EventType.Repaint)
                    ReversalHandlePoint = GUIUtility.GUIToScreenPoint(handle.center) - position.position;
                EditorGUI.DrawRect(handle, InputColor);
                int id = GUIUtility.GetControlID(901 + side, FocusType.Passive, handle);
                EditorGUIUtility.AddCursorRect(handle, MouseCursor.RotateArrow);
                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0 && handle.Contains(e.mousePosition))
                {
                    coneDrag = id; GUIUtility.hotControl = id;
                    Undo.RegisterCompleteObjectUndo(profile, "Adjust reversal cone"); e.Use();
                }
                if (coneDrag != id || GUIUtility.hotControl != id) continue;
                if (e.type == EventType.MouseDrag)
                {
                    Vector2 delta = e.mousePosition - center;
                    setting.backwardConeDegrees = Mathf.Clamp(Vector2.Angle(Vector2.up, delta) * 2, 0, 180);
                    EditorUtility.SetDirty(profile); e.Use(); Repaint();
                }
                if (e.type == EventType.MouseUp)
                { coneDrag = GUIUtility.hotControl = 0; PlayerTuningEditing.Save(profile); e.Use(); }
            }
        }

        private void DrawSpatialPreview()
        {
            if (!tuning.attackProfile) return;
            var stage = tuning.attackProfile.GetStage(selectedStage);
            if (stage == null) return;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Reach & motion — top-down diagram", EditorStyles.boldLabel);
            float size = PlayerGlobalModifiersEditing.GetOrCreate().ForPlayer(playerSlot).size;
            float body = previewPlayer ? PlayerScaleAdjuster.BodyRadiusOf(previewPlayer.GetComponent<PlayerControllerScript>()) : .5f * size;
            float reachScale = previewPlayer ? PlayerScaleAdjuster.ActionReachOf(previewPlayer) : size;
            var capsule = FindMeleeCapsule();
            float reach = capsule ? CapsuleReach(capsule) : 0;
            float travel = stage.StageType == AttackStageType.FinisherRepulsor ? 0 : stage.TravelDistance * reachScale;
            float radius = stage.GetRepulsorRadius(previewPlayer ? PlayerScaleAdjuster.SizeOf(previewPlayer) : size, body);
            float extent = Mathf.Max(1, body, stage.StageType == AttackStageType.FinisherRepulsor ? radius : travel + reach);
            Rect box = GUILayoutUtility.GetRect(200, 208, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(box, new Color(.1f, .11f, .13f));
            float scale = Mathf.Min((box.width - 110) / (extent * 2), 73 / extent);
            Vector2 origin = new Vector2(box.center.x - (stage.StageType == AttackStageType.FinisherRepulsor ? 0 : travel * scale * .5f), box.y + 93);
            float localTime = selectedOnly ? cursor : Mathf.Max(0, cursor - StageOffset(selectedStage));
            if (!selectedOnly && stage.EarlyComboHandoff && selectedStage + 1 < tuning.attackProfile.Stages.Count)
                localTime = Mathf.Min(localTime, Mathf.Max(stage.ComboHandoffSeconds, PlayerTuningEditing.Window(stage, tuning).x));
            float normalized = Mathf.Clamp01(localTime / stage.Duration);
            float progress = stage.DistanceCurve == null ? normalized : stage.DistanceCurve.Evaluate(normalized);
            Vector2 current = origin + Vector2.right * travel * progress * scale;
            Handles.BeginGUI();
            Handles.color = new Color(.3f, .33f, .38f);
            Handles.DrawAAPolyLine(1, new Vector3(box.x + 10, origin.y), new Vector3(box.xMax - 10, origin.y));
            Handles.DrawWireDisc(origin, Vector3.forward, body * scale);
            Handles.color = Color.white; Handles.DrawWireDisc(current, Vector3.forward, body * scale);
            Handles.color = HitColor;
            if (stage.StageType == AttackStageType.FinisherRepulsor)
            {
                var pulse = previewPlayer ? previewPlayer.GetComponentInChildren<PlayerRepulsorAOE>(true) : null;
                float start = pulse ? pulse.PreviewStartRadiusWorld(stage) : body;
                float a0 = pulse ? pulse.EffectiveActivationStart(stage) : stage.ActivationStartNormalized;
                float a1 = pulse ? pulse.EffectiveActivationEnd(stage) : stage.ActivationEndNormalized;
                if (normalized >= a0 && normalized <= a1)
                {
                    float activeT = Mathf.InverseLerp(a0, a1, normalized);
                    float r = Mathf.Lerp(start, radius, Mathf.Clamp01(stage.RepulsorRadiusCurve.Evaluate(activeT)));
                    PlayerRepulsorRangeRings.DrawEditorGuides(origin, Vector3.forward,
                        Mathf.Min(stage.GetRepulsorInnerRadius(body, radius), r) * scale, r * scale, 1.35f);
                }
            }
            else if (reach > 0)
            {
                float half = stage.StageType == AttackStageType.ComboSwipe ? stage.RotationArc * .5f : 0;
                var arc = new Vector3[49];
                for (int i = 0; i < arc.Length; i++)
                {
                    float angle = Mathf.Lerp(-half, half, i / 48f) * Mathf.Deg2Rad;
                    arc[i] = current + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach * scale;
                }
                Handles.DrawAAPolyLine(2, arc);
                Handles.DrawAAPolyLine(1, current, arc[0]); Handles.DrawAAPolyLine(1, current, arc[48]);
            }
            Handles.color = InputColor;
            Handles.DrawAAPolyLine(3, origin, origin + Vector2.right * travel * scale);
            Handles.EndGUI();
            GUI.Label(new Rect(box.x + 10, box.y + 6, box.width - 20, 22), $"Body radius {body:0.##} · Travel {travel:0.##} · " + (stage.StageType == AttackStageType.FinisherRepulsor ? $"Pulse radius {radius:0.##}" : capsule ? $"Weapon reach {reach:0.##}" : "Choose a scene player to inspect weapon reach"), EditorStyles.miniLabel);
            GUI.Label(new Rect(box.x + 10, box.yMax - 40, box.width - 20, 35), stage.StageType == AttackStageType.FinisherRepulsor ?
                $"White: body   Red: {stage.RepulsorInnerDamageMultiplier:0.##}x damage   Yellow: 1x damage\nA target's body touching the red zone takes the inner damage tier." :
                "White: body   Orange: damage reach   Teal: full authored travel\nDiagram assumes open space; collisions and aim assist can shorten travel.", EditorStyles.wordWrappedMiniLabel);
        }
        private float StageOffset(int index)
        {
            float t = 0;
            for (int i = 0; i < index; i++)
            {
                var s = tuning.attackProfile.GetStage(i);
                t += s.AllowComboCancel ? Mathf.Max(s.ComboHandoffSeconds, PlayerTuningEditing.Window(s, tuning).x) : s.Duration;
            }
            return t;
        }
        private CapsuleCollider FindMeleeCapsule()
        {
            if (!previewPlayer) return null;
            foreach (var melee in previewPlayer.GetComponentsInChildren<PlayerMelee>(true))
            { var capsule = melee.GetComponent<CapsuleCollider>(); if (capsule) return capsule; }
            return null;
        }
        private float CapsuleReach(CapsuleCollider capsule)
        {
            Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
            Vector3 outer = capsule.transform.TransformPoint(capsule.center + axis * capsule.height * .5f);
            Vector3 delta = outer - previewPlayer.transform.position; delta.y = 0;
            return delta.magnitude;
        }
        private void DrawSceneControls()
        {
            sceneOverlay = EditorGUILayout.Toggle("Scene view reach overlay", sceneOverlay);
            if (previewPlayer)
            {
                var player = previewPlayer.GetComponent<PlayerControllerScript>();
                var attack = previewPlayer.GetComponent<PlayerAttackController>();
                EditorGUILayout.HelpBox($"Selected scene player: {previewPlayer.name}\nMovement: {(player && player.UseSharedSettings && tuning.sharedEnabled ? "shared" : "local")} · Combat: {(attack && attack.UseSharedSettings && tuning.sharedEnabled ? "shared" : "local")} · Appearance: {(previewPlayer.UseSharedSettings && visuals.sharedEnabled ? "shared" : "local")}", MessageType.Info);
                if (GUILayout.Button("Select scene player / inspect local overrides")) Selection.activeGameObject = previewPlayer.gameObject;
            }
            SceneView.RepaintAll();
        }
        private void DrawSceneOverlay(SceneView view)
        {
            if (!sceneOverlay || !previewPlayer || !tuning || !tuning.attackProfile) return;
            var stage = tuning.attackProfile.GetStage(selectedStage); if (stage == null) return;
            Vector3 origin = previewPlayer.transform.position;
            Handles.color = Color.white;
            Handles.DrawWireDisc(origin, Vector3.up, PlayerScaleAdjuster.BodyRadiusOf(previewPlayer.GetComponent<PlayerControllerScript>()));
            Handles.color = HitColor;
            if (stage.StageType == AttackStageType.FinisherRepulsor)
            {
                var pulse = previewPlayer.GetComponentInChildren<PlayerRepulsorAOE>(true);
                float outer = pulse ? (pulse.IsPulseActive ? pulse.RadiusWorld : pulse.PreviewEndRadiusWorld(stage)) :
                    stage.GetRepulsorRadius(PlayerScaleAdjuster.SizeOf(previewPlayer));
                float inner = pulse ? (pulse.IsPulseActive ? pulse.ActiveInnerRadiusWorld : pulse.PreviewInnerRadiusWorld(stage)) :
                    stage.GetRepulsorInnerRadius(PlayerScaleAdjuster.BodyRadiusOf(previewPlayer.GetComponent<PlayerControllerScript>()), outer);
                if (Application.isPlaying && (!pulse || !pulse.IsPulseActive)) return;
                if (pulse && pulse.IsPulseActive) origin = pulse.OriginWorld;
                PlayerRepulsorRangeRings.DrawEditorGuides(origin, Vector3.up, inner, outer, HandleUtility.GetHandleSize(origin) * .012f);
            }
            else
            {
                // The live Swipe guide already draws the exact current capsule in
                // both Game and Scene views; a full arc here would imply a larger live hitbox.
                if (Application.isPlaying && stage.StageType == AttackStageType.ComboSwipe) return;
                var capsule = FindMeleeCapsule();
                if (capsule)
                {
                    float reach = CapsuleReach(capsule);
                    Vector3 dir = previewPlayer.transform.right;
                    float angle = stage.StageType == AttackStageType.ComboSwipe ? stage.RotationArc : 0;
                    Handles.DrawWireArc(origin, Vector3.up, Quaternion.AngleAxis(-angle * .5f, Vector3.up) * dir, angle, reach);
                    Handles.DrawLine(origin, origin + dir * reach);
                }
            }
        }
    }
}
