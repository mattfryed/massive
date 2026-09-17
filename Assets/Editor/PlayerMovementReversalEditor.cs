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
            if (GUILayout.Button("Open All Players Reversal Controls")) PlayerMovementReversalWindow.Open();
        }

        internal static void DrawSettings(SerializedObject data)
        {
            data.Update();
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
            if (!c.assistEnabled) return;
            Vector3 forward = c.transform.right;
            var body = c.GetComponent<Rigidbody>();
            if (Application.isPlaying && body && body.linearVelocity.sqrMagnitude > .01f) forward = body.linearVelocity;
            forward.y = 0f;
            if (forward.sqrMagnitude < .0001f) forward = Vector3.right;
            Vector3 back = -forward.normalized;
            float radius = 1.75f * PlayerScaleAdjuster.SizeOf(c);
            Vector3 origin = c.transform.position;
            Vector3 edge = Quaternion.AngleAxis(-c.backwardConeDegrees * .5f, Vector3.up) * back;
            using (new Handles.DrawingScope(new Color(.2f, .9f, 1f, .12f)))
                Handles.DrawSolidArc(origin, Vector3.up, edge, c.backwardConeDegrees, radius);
            using (new Handles.DrawingScope(new Color(.2f, .9f, 1f, .9f)))
            {
                Handles.DrawWireArc(origin, Vector3.up, edge, c.backwardConeDegrees, radius);
                Handles.DrawLine(origin, origin + edge * radius);
                Handles.DrawLine(origin, origin + Quaternion.AngleAxis(c.backwardConeDegrees, Vector3.up) * edge * radius);
                Handles.Label(origin + back * radius, $"Reverse cone: {c.backwardConeDegrees:0}°\nKeep {c.retainedMomentum:P0} momentum");
            }
        }
    }

    public sealed class PlayerMovementReversalWindow : EditorWindow
    {
        private Vector2 scroll;
        [MenuItem("MASSIVE/Player/Joystick Reversal Controls")]
        public static void Open()
        {
            var w = GetWindow<PlayerMovementReversalWindow>("Joystick Reversal");
            w.minSize = new Vector2(390f, 420f);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Full backward cone around travel direction. Momentum retained: 0% = drop old drift; 100% = normal. Select a player to see its cone in Scene view.", MessageType.Info);
            using (var view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;
                var players = FindObjectsByType<PlayerControllerScript>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(p => p.transform.parent && p.transform.parent.name == "Players").OrderBy(p => p.playerID);
                foreach (var p in players)
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        var c = p.GetComponent<PlayerMovementReversal>();
                        if (GUILayout.Button(p.name + (p.gameObject.activeInHierarchy ? "" : " (inactive)"))) Selection.activeObject = c ? (Object)c : p;
                        if (!c)
                        {
                            if (GUILayout.Button("Add Reversal Assist")) Undo.AddComponent<PlayerMovementReversal>(p.gameObject);
                            continue;
                        }
                        using (var data = new SerializedObject(c)) PlayerMovementReversalEditor.DrawSettings(data);
                    }
            }
        }
    }
}
