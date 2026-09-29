#if UNITY_EDITOR
using Massive.Player;
using Massive.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Massive.EditorTools
{
    [CustomEditor(typeof(OriginalAttackStageVfx))]
    public sealed class OriginalAttackStageVfxEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var effect = (OriginalAttackStageVfx)target;
            EditorGUILayout.HelpBox("This prefab owns the stage's visuals. Edit its GPU emitter, add Particle System children, meshes, lights or other effect components. +Z is the attack direction. Attack timing and damage remain in Player Tuning.", MessageType.Info);
            if (effect.stage == AttackStageType.FinisherRepulsor)
                EditorGUILayout.HelpBox("Original Repulsor already has player-body and grid feedback. Its optional particle layer starts disabled to preserve that look; enable it to preview and tune it.", MessageType.None);
            DrawPreview(effect);
        }
        public static void DrawPreview(OriginalAttackStageVfx effect)
        {
            if (EditorUtility.IsPersistent(effect))
            {
                if (GUILayout.Button("Open stage prefab")) AssetDatabase.OpenAsset(effect.gameObject);
                return;
            }
            if (Application.IsPlaying(effect.gameObject)) return;
            if (GUILayout.Button(OriginalStagePreview.Target == effect ? "Stop stage preview" : "Loop stage preview in Scene view"))
            {
                if (OriginalStagePreview.Target == effect) OriginalStagePreview.Stop();
                else OriginalStagePreview.Play(effect);
            }
        }
    }

    public static class OriginalAttackStageEditing
    {
        static readonly string[] Fields = { "originalThrustPrefab", "originalSweepPrefab", "originalRepulsorPrefab" };
        static readonly string[] Labels = { "Thrust", "Sweep", "Repulsor" };
        static readonly string[] GPUFields = { "particleCount", "trailLength", "baseWidth", "tipWidth", "forwardSpeed", "trailDrag", "minLifetime", "maxLifetime", "sizeStart", "sizeEnd", "emissionRate", "sim", "trailMat", "quadMesh" };
        public static bool HasPrefabs(SerializedObject data) => data.FindProperty(Fields[0]) != null;
        public static bool IsPrefabField(string name) => name == Fields[0] || name == Fields[1] || name == Fields[2];
        public static void Draw(SerializedObject data, int selectedStage = -1)
        {
            EditorGUILayout.HelpBox("Original Particles uses one editable prefab per stage. These controls edit the prefab itself and save during Play Mode. Open a prefab to add effects; saved changes refresh the running actors.", MessageType.None);
            for (int i = 0; i < 3; i++)
            {
                if (selectedStage >= 0 && i != selectedStage) continue;
                var field = data.FindProperty(Fields[i]); if (field == null) continue;
                EditorGUILayout.PropertyField(field, new GUIContent(Labels[i] + " prefab"));
                var prefab = field.objectReferenceValue as OriginalAttackStageVfx;
                if (!prefab) continue;
                if (GUILayout.Button("Open " + Labels[i] + " prefab")) AssetDatabase.OpenAsset(prefab.gameObject);
                if (i == 2)
                    EditorGUILayout.HelpBox("Body and grid feedback remain under Impact & recovery. Enable and edit the prefab's optional particle layer to add particles.", MessageType.None);
                else
                {
                    var gpu = prefab.GetComponentInChildren<AttackTrailGPU>(true);
                    if (gpu)
                    {
                        field.isExpanded = EditorGUILayout.Foldout(field.isExpanded, Labels[i] + " GPU particles", true);
                        if (field.isExpanded) { using var emitter = new SerializedObject(gpu); DrawEmitter(emitter); }
                    }
                }
                EditorGUILayout.Space(5);
            }
        }
        public static void DrawEmitter(SerializedObject data)
        {
            data.Update();
            foreach (string name in GPUFields)
            {
                var field = data.FindProperty(name);
                if (field != null) EditorGUILayout.PropertyField(field, true);
            }
            if (data.ApplyModifiedProperties())
            {
                var gpu = (AttackTrailGPU)data.targetObject;
                EditorUtility.SetDirty(gpu);
                if (EditorUtility.IsPersistent(gpu)) PrefabUtility.SavePrefabAsset(gpu.transform.root.gameObject);
                OriginalAttackStageVfx.InvalidateContent();
            }
        }
    }

    [InitializeOnLoad]
    public static class OriginalStagePreview
    {
        public static OriginalAttackStageVfx Target { get; private set; }
        static double lastTime;
        static float elapsed;
        static bool ended;
        static OriginalStagePreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.playModeStateChanged += _ => Stop();
            PrefabStage.prefabStageClosing += _ => Stop();
        }
        public static void Play(OriginalAttackStageVfx target)
        {
            Stop(); Target = target; elapsed = 0; ended = false;
            Target.Begin(); lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }
        public static void Stop()
        {
            EditorApplication.update -= Tick;
            if (Target) Target.StopImmediately();
            Target = null;
            SceneView.RepaintAll();
        }
        static void Tick()
        {
            if (!Target || Application.IsPlaying(Target.gameObject)) { Stop(); return; }
            var tuning = SharedSettingsRuntime.Load<PlayerTuningProfile>();
            var stage = tuning && tuning.attackProfile ? tuning.attackProfile.GetStage((int)Target.stage) : null;
            if (stage == null) { Stop(); return; }
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min(.05f, (float)(now - lastTime)); lastTime = now;
            elapsed += dt;
            if (elapsed < stage.Duration)
                Target.Sample(stage, elapsed / stage.Duration, stage.ActivationStartNormalized, Target.transform.forward, dt);
            else
            {
                if (!ended) { ended = true; Target.EndEmission(); }
                Target.TickTail(dt);
                if (elapsed >= stage.Duration + Target.tailSeconds + .4f)
                { Target.Begin(); elapsed = 0; ended = false; }
            }
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
        }
    }
    public sealed class OriginalStagePrefabImports : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
                if (path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                { OriginalAttackStageVfx.InvalidateContent(); break; }
        }
    }
}
#endif
