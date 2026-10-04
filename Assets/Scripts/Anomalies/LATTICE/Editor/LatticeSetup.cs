#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Lattice.EditorTools
{
    public static class LatticeSetup
    {
        public const string ScenePath = "Assets/Scenes/S-1_LATTICE.unity";
        [MenuItem("MASSIVE/LATTICE/Install in current scene")]
        public static void Install()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open S-1_LATTICE in Edit Mode before installing.");
            var grid = InScene<VectorGridGPU>(scene).FirstOrDefault();
            if (!grid) throw new InvalidOperationException("LATTICE needs its existing VectorGridGPU.");
            Undo.SetCurrentGroupName("Install LATTICE disruption");
            var field = grid.GetComponent<LatticeDisruptionField>();
            if (!field) field = Undo.AddComponent<LatticeDisruptionField>(grid.gameObject);
            Undo.RecordObject(field, "Assign LATTICE shader");
            field.strandShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Anomalies/LATTICE/LatticeStrands.shader");
            foreach (var player in InScene<PlayerControllerScript>(scene))
            {
                var motor = player.GetComponent<LatticePlayerMotor>();
                if (!motor) motor = Undo.AddComponent<LatticePlayerMotor>(player.gameObject);
                Undo.RecordObject(motor, "Assign LATTICE field"); motor.field = field;
                PrefabUtility.RecordPrefabInstancePropertyModifications(motor);
            }
            EditorUtility.SetDirty(field); PrefabUtility.RecordPrefabInstancePropertyModifications(field);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = grid.gameObject;
            Debug.Log("LATTICE installed. Save the scene to retain the field and player adapters.");
        }
        public static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }

    [CustomEditor(typeof(LatticeDisruptionField))]
    public sealed class LatticeDisruptionFieldEditor : UnityEditor.Editor
    {
        double nextFrame;
        void OnEnable() { EditorApplication.update += Preview; }
        void OnDisable() { EditorApplication.update -= Preview; }
        void Preview()
        {
            var field = target as LatticeDisruptionField;
            if (!field || Application.isPlaying || !field.animateInEditor || !field.isActiveAndEnabled || EditorApplication.timeSinceStartup < nextFrame) return;
            nextFrame = EditorApplication.timeSinceStartup + 1.0/30;
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); Repaint();
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                using (new EditorGUI.DisabledScope(property.name == "m_Script"))
                {
                    if (property.name == "keepStrandsAtNoiseBoundary")
                        EditorGUILayout.PropertyField(property, new GUIContent("Hold Tips At Boundary", property.tooltip), true);
                    else if (property.name == "flutterAmplitude")
                        EditorGUILayout.PropertyField(property, new GUIContent("Thread Looseness", property.tooltip));
                    else if (property.name == "flutterFrequency")
                        EditorGUILayout.PropertyField(property, new GUIContent("Breeze Speed", property.tooltip));
                    else EditorGUILayout.PropertyField(property, true);
                }
                if (property.name == "strandMotionSeed" && GUILayout.Button("Randomize Strand Motion"))
                    property.intValue = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0) & int.MaxValue;
            }
            serializedObject.ApplyModifiedProperties();
            var field = (LatticeDisruptionField)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Connections", $"{field.DisconnectedEdgeCount} disconnected / {field.TransitioningEdgeCount} changing / {field.EdgeCount} total");
            EditorGUILayout.HelpBox("Drifting Noise is the level treatment. Connected / Disconnected are useful for comparing movement. Ordinary movement hops between dots; attacks and knockback keep their own motion. Preview progress is never saved.", MessageType.Info);
        }
    }
}
#endif
