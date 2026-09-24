using System.Linq;
using Massive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Massive.EditorTools
{
    [CustomEditor(typeof(PlayerMovementReversal)), CanEditMultipleObjects]
    public sealed class PlayerMovementReversalEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Joystick movement only. The cone is centered directly behind current horizontal travel, not player aim. 0% drops the old drift; 100% preserves normal movement. Applied once on a fresh reversal. Normal acceleration and top speed are unchanged.", MessageType.Info);
            DrawSettings(serializedObject);
            EditorGUILayout.HelpBox("Attack, shield, charge, recoil, stun and known launch/current states protect their momentum. A brief recovery prevents the first released frame from cancelling an action. Play-mode edits are temporary.", MessageType.None);
            if (GUILayout.Button("Open Global Reversal Controls")) PlayerMovementReversalWindow.Open();
        }

        internal static void DrawSettings(SerializedObject data)
        {
            data.Update();
            EditorGUILayout.PropertyField(data.FindProperty("useGlobalModifiers"), new GUIContent("Use Global Joystick Settings"));
            data.ApplyModifiedProperties();
            if (data.targetObjects.Any(item => ((PlayerMovementReversal)item).UsesGlobalModifiers))
            {
                EditorGUILayout.HelpBox("Global values come from MASSIVE → Player → Joystick Reversal Controls. Disable Use Global Joystick Settings for a local exception.", MessageType.Info);
                foreach (PlayerMovementReversal c in data.targetObjects)
                    EditorGUILayout.LabelField(c.name, $"{(c.AssistEnabled ? "On" : "Off")} · {c.BackwardConeDegrees:0}° · keep {c.RetainedMomentum:P0}");
                return;
            }
            EditorGUILayout.PropertyField(data.FindProperty("assistEnabled"), new GUIContent("Enable Joystick Reversal"));
            EditorGUILayout.PropertyField(data.FindProperty("backwardConeDegrees"), new GUIContent("Backward Cone (degrees)", "Full angular width: 100 degrees means ±50 degrees around directly backward."));
            var momentum = data.FindProperty("retainedMomentum");
            EditorGUI.showMixedValue = momentum.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            float percent = EditorGUILayout.Slider(new GUIContent("Momentum Retained (%)", "0 = discard horizontal momentum; 100 = normal movement."), momentum.floatValue * 100f, 0f, 100f);
            if (EditorGUI.EndChangeCheck()) momentum.floatValue = percent / 100f;
            EditorGUI.showMixedValue = false;
            if (!data.ApplyModifiedProperties()) return;
            foreach (Object o in data.targetObjects)
            {
                var c = (PlayerMovementReversal)o;
                PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            }
            SceneView.RepaintAll();
        }

        private void OnSceneGUI()
        {
            var c = (PlayerMovementReversal)target;
            if (!c.AssistEnabled) return;
            Vector3 forward = c.transform.right;
            var body = c.GetComponent<Rigidbody>();
            if (Application.isPlaying && body && body.linearVelocity.sqrMagnitude > .01f) forward = body.linearVelocity;
            forward.y = 0f;
            if (forward.sqrMagnitude < .0001f) forward = Vector3.right;
            Vector3 back = -forward.normalized;
            float radius = 1.75f * PlayerScaleAdjuster.SizeOf(c);
            Vector3 origin = c.transform.position;
            Vector3 edge = Quaternion.AngleAxis(-c.BackwardConeDegrees * .5f, Vector3.up) * back;
            using (new Handles.DrawingScope(new Color(.2f, .9f, 1f, .12f)))
                Handles.DrawSolidArc(origin, Vector3.up, edge, c.BackwardConeDegrees, radius);
            using (new Handles.DrawingScope(new Color(.2f, .9f, 1f, .9f)))
            {
                Handles.DrawWireArc(origin, Vector3.up, edge, c.BackwardConeDegrees, radius);
                Handles.DrawLine(origin, origin + edge * radius);
                Handles.DrawLine(origin, origin + Quaternion.AngleAxis(c.BackwardConeDegrees, Vector3.up) * edge * radius);
                Handles.Label(origin + back * radius, $"Reverse cone: {c.BackwardConeDegrees:0}°\nKeep {c.RetainedMomentum:P0} momentum");
            }
        }
    }

    public sealed class PlayerMovementReversalWindow : EditorWindow
    {
        private Vector2 scroll;
        [MenuItem("MASSIVE/Player/Joystick Reversal Controls")]
        public static void Open()
        {
            var w = GetWindow<PlayerMovementReversalWindow>("Global Joystick Reversal");
            w.minSize = new Vector2(390f, 420f);
        }

        private void OnGUI()
        {
            using (var view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;
                PlayerGlobalModifiersEditing.Draw(false);
            }
        }
    }
}
