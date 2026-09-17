using System.Linq;
using Massive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Massive.EditorTools
{
    [CustomEditor(typeof(PlayerScaleAdjuster)), CanEditMultipleObjects]
    public class PlayerScaleAdjusterEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Change Player Size here. The root transform, body, hitboxes and connected effects follow it. Leave their local dimensions at the original 100% values.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            var size = serializedObject.FindProperty("size");
            EditorGUI.showMixedValue = size.hasMultipleDifferentValues;
            float percent = EditorGUILayout.Slider(new GUIContent("Player Size (%)"), size.floatValue * 100f, 10f, 300f);
            if (EditorGUI.EndChangeCheck()) size.floatValue = percent / 100f;
            EditorGUI.showMixedValue = false;
            using (new EditorGUILayout.HorizontalScope())
                foreach (float p in new[] { 0.5f, 0.75f, 1f, 1.25f, 1.5f })
                    if (GUILayout.Button($"{p * 100f:0}%")) size.floatValue = p;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Optional gameplay relationships", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("scaleActionReach"), new GUIContent("Scale Attack Travel & Targeting", "Physical melee hitboxes always follow player size. This separately scales each attack stage's travel and the lunge lock-on search distance."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("scaleMovement"), new GUIContent("Scale Movement Speed", "Scales traversal acceleration, speed limits and player recoil. Off preserves world-space speed. Environmental forces stay unchanged."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("scaleProjectileRange"), new GUIContent("Scale Projectile Range", "Scales Particle Accelerator range. Beam thickness always follows size; projectile speed stays unchanged."));
            if (serializedObject.ApplyModifiedProperties())
                foreach (PlayerScaleAdjuster s in targets) ApplyWithUndo(s);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Damage, attack timing, cooldowns, rigidbody mass, impact force, HUD text size and the arena stay unchanged. Already emitted world-space effects finish at their original size. Play-mode edits are temporary.", MessageType.None);
            foreach (PlayerScaleAdjuster s in targets)
            {
                if (!s.HasUniformPositiveScale) EditorGUILayout.HelpBox("Use uniform positive scales on the player and its parents. Non-uniform or mirrored roots cannot keep sphere colliders and all effects consistent.", MessageType.Warning);
                var p = s.GetComponent<PlayerControllerScript>();
                if (p) EditorGUILayout.LabelField(s.name + " body diameter", (2f * PlayerScaleAdjuster.BodyRadiusOf(p)).ToString("0.###") + " world units");
            }
            if (GUILayout.Button("Open All Players Size Controls")) PlayerScaleWindow.Open();
        }

        internal static void ApplyWithUndo(PlayerScaleAdjuster s)
        {
            Undo.RecordObject(s.transform, "Resize Player");
            foreach (var ps in s.GetComponentsInChildren<ParticleSystem>(true)) Undo.RecordObject(ps, "Scale Player Particles");
            s.ApplyScale();
            PrefabUtility.RecordPrefabInstancePropertyModifications(s);
            PrefabUtility.RecordPrefabInstancePropertyModifications(s.transform);
            foreach (var ps in s.GetComponentsInChildren<ParticleSystem>(true))
                if (PrefabUtility.IsPartOfPrefabInstance(ps)) PrefabUtility.RecordPrefabInstancePropertyModifications(ps);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(s.gameObject.scene);
            SceneView.RepaintAll();
        }
    }

    public class PlayerScaleWindow : EditorWindow
    {
        private Vector2 scroll;
        [MenuItem("MASSIVE/Player/Player Size Controls")]
        public static void Open() => GetWindow<PlayerScaleWindow>("Player Size");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Player Size", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Size changes apply in Edit mode and Play mode. Select a player for the optional movement, attack travel and projectile settings. Save the scene to retain Edit-mode changes.", MessageType.Info);
            using (var view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;
                var players = FindObjectsByType<PlayerControllerScript>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(p => p.gameObject.scene.IsValid() && p.transform.parent && p.transform.parent.name == "Players")
                    .OrderBy(p => p.playerID).ToArray();
                if (players.Length == 0) EditorGUILayout.HelpBox("No Players roster found in the open scene. You can also add Player Scale Adjuster directly to any player root.", MessageType.Info);
                foreach (var p in players)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        if (GUILayout.Button(p.name + (p.gameObject.activeInHierarchy ? "" : " (inactive)"))) Selection.activeGameObject = p.gameObject;
                        var s = p.GetComponent<PlayerScaleAdjuster>();
                        if (!s)
                        {
                            if (GUILayout.Button("Add Size Control")) Undo.AddComponent<PlayerScaleAdjuster>(p.gameObject);
                            continue;
                        }
                        EditorGUI.BeginChangeCheck();
                        float percent = EditorGUILayout.Slider("Size (%)", s.Size * 100f, 10f, 300f);
                        if (EditorGUI.EndChangeCheck()) SetSize(s, percent / 100f);
                        using (new EditorGUILayout.HorizontalScope())
                            foreach (float v in new[] { .5f, .75f, 1f, 1.25f, 1.5f })
                                if (GUILayout.Button($"{v * 100f:0}%")) SetSize(s, v);
                        EditorGUILayout.LabelField("Body diameter", (2f * PlayerScaleAdjuster.BodyRadiusOf(p)).ToString("0.###") + " world units");
                    }
                }
            }
        }

        private static void SetSize(PlayerScaleAdjuster s, float value)
        {
            Undo.RecordObject(s, "Change Player Size");
            Undo.RecordObject(s.transform, "Change Player Size");
            foreach (var ps in s.GetComponentsInChildren<ParticleSystem>(true)) Undo.RecordObject(ps, "Scale Player Particles");
            s.Size = value;
            PlayerScaleAdjusterEditor.ApplyWithUndo(s);
        }
    }
}
