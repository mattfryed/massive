using UnityEditor;
using UnityEngine;

namespace Massive.Emergence.Editor
{
    [CustomEditor(typeof(EmergenceProfile))]
    public sealed class EmergenceProfileEditor : UnityEditor.Editor
    {
        public EmergenceField PreviewField { get; set; }
        int requestedPopulation;
        bool scaleWithPopulation = true;
        static readonly string[] Roles = { "Structure", "Motor", "Catalyst", "Nutrient", "Inert" };
        public override bool RequiresConstantRepaint() => EmergenceEditorPreview.Running;
        public override void OnInspectorGUI()
        {
            EmergenceEditorPreview.DrawControls(PreviewField ? PreviewField : EmergenceEditorPreview.FindField((EmergenceProfile)target));
            DrawPopulationTools((EmergenceProfile)target);
            serializedObject.Update();
            EditorGUILayout.HelpBox("Development level: hidden from level selection. Colors and force strengths update live. Structural changes rebuild an active editor preview automatically; during Play Mode they require restarting play. Particles use solid unlit colors; alpha is always one.", MessageType.Info);
            var species = serializedObject.FindProperty("species");
            EditorGUILayout.LabelField("Species and appearance", EditorStyles.boldLabel);
            for (int i = 0; i < Mathf.Min(EmergenceProfile.SpeciesCount, species.arraySize); i++)
                EditorGUILayout.PropertyField(species.GetArrayElementAtIndex(i), new GUIContent(Roles[i]), true);
            EditorGUILayout.HelpBox("Inert particles have no biological role: they cannot feed, be eaten, join organisms or produce nuggets. Their matrix row controls their response; their column controls how other species respond to them. Zero disables that species attraction only; soft repulsion, agitation and player pushes still apply.", MessageType.None);
            DrawPropertiesExcluding(serializedObject, "m_Script", "species", "attraction");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Attraction / repulsion matrix", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Each row responds to each column. Positive = attract; negative = repel; diagonal = same species. The two directions can differ. Size-based core repulsion acts independently of this matrix. Spacing is soft: dense clusters and impacts can briefly compress or overlap particles.", MessageType.None);
            var matrix = serializedObject.FindProperty("attraction");
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(76);
                foreach (string role in Roles) GUILayout.Label(role, EditorStyles.miniLabel, GUILayout.MinWidth(34));
            }
            for (int row = 0; row < EmergenceProfile.SpeciesCount && matrix.arraySize == EmergenceProfile.SpeciesCount * EmergenceProfile.SpeciesCount; row++)
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(Roles[row], GUILayout.Width(72));
                    for (int column = 0; column < EmergenceProfile.SpeciesCount; column++)
                    {
                        var value = matrix.GetArrayElementAtIndex(row * EmergenceProfile.SpeciesCount + column);
                        value.floatValue = EditorGUILayout.FloatField(value.floatValue, GUILayout.MinWidth(34));
                    }
                }
            if (serializedObject.ApplyModifiedProperties()) EmergenceEditorPreview.SettingsChanged();
            string error = ((EmergenceProfile)target).ConfigurationError();
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                if (GUILayout.Button("Refresh matching scene previews"))
                    foreach (var field in Object.FindObjectsByType<EmergenceField>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (field.profile == target) field.RebuildPreview();
        }
        void DrawPopulationTools(EmergenceProfile profile)
        {
            if (profile.species == null || profile.species.Length != EmergenceProfile.SpeciesCount) return;
            foreach (var s in profile.species) if (s == null) return;
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUILayout.LabelField("Population scaling", EditorStyles.boldLabel);
                if (requestedPopulation <= 0) requestedPopulation = profile.ParticleCount;
                requestedPopulation = EditorGUILayout.IntField("Target particle count", requestedPopulation);
                scaleWithPopulation = EditorGUILayout.Toggle("Scale sizes and distances", scaleWithPopulation);
                EditorGUILayout.HelpBox("Preserves species proportions. Scaling also adjusts particle sizes, interaction distances and motion to keep similar local density as the population changes. Leaving it off retains your current sizes and ranges, which can greatly increase the work per particle.", MessageType.None);
                if (GUILayout.Button("Apply population"))
                {
                    Undo.RecordObject(profile, "Scale EMERGENCE population");
                    ScalePopulation(profile, requestedPopulation, scaleWithPopulation);
                    requestedPopulation = profile.ParticleCount;
                    EditorUtility.SetDirty(profile); EmergenceEditorPreview.SettingsChanged();
                }
            }
            EditorGUILayout.Space();
        }
        public static void ScalePopulation(EmergenceProfile profile, int targetCount, bool scale)
        {
            targetCount = Mathf.Clamp(targetCount, 1, profile.backend == EmergenceBackend.GPU ? 100000 : 4000);
            int previous = profile.ParticleCount, assigned = 0, cumulative = 0;
            if (previous <= 0) { profile.species[0].count = targetCount; return; }
            for (int i = 0; i < EmergenceProfile.SpeciesCount; i++)
            {
                cumulative += profile.species[i].count;
                int boundary = i == EmergenceProfile.SpeciesCount - 1 ? targetCount : Mathf.RoundToInt((float)cumulative / previous * targetCount);
                profile.species[i].count = boundary - assigned; assigned = boundary;
            }
            if (!scale) return;
            float ratio = Mathf.Sqrt((float)previous / targetCount);
            foreach (var s in profile.species) s.diameter = Mathf.Max(.005f, s.diameter * ratio);
            profile.particleGap *= ratio;
            profile.interactionRadius *= ratio; profile.separation *= ratio; profile.connectionRadius *= ratio; profile.feedingRadius *= ratio;
            profile.initialSpeed *= ratio; profile.speedLimit *= ratio; profile.force *= ratio; profile.repulsion *= ratio;
            profile.coreRepulsion *= ratio; profile.agitation *= ratio; profile.maximumForce *= ratio;
        }
    }

    [CustomEditor(typeof(EmergenceField))]
    public sealed class EmergenceFieldEditor : UnityEditor.Editor
    {
        UnityEditor.Editor profileEditor;
        bool showSettings = true;
        public override bool RequiresConstantRepaint() => EmergenceEditorPreview.Running;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var field = (EmergenceField)target;
            if (!field.profile) return;
            showSettings = EditorGUILayout.Foldout(showSettings, "Particle settings (shared profile)", true);
            if (showSettings)
            {
                CreateCachedEditor(field.profile, typeof(EmergenceProfileEditor), ref profileEditor);
                ((EmergenceProfileEditor)profileEditor).PreviewField = field;
                profileEditor.OnInspectorGUI();
            }
        }
        void OnDisable() { if (profileEditor) DestroyImmediate(profileEditor); }
    }
}
