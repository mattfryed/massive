using UnityEditor;
using UnityEngine;

namespace Massive.Emergence.Editor
{
    [InitializeOnLoad]
    public static class EmergenceEditorPreview
    {
        public static EmergenceField Target { get; private set; }
        public static bool Running { get; private set; }
        public static string Error { get; private set; }
        static float speed = 1, accumulator;
        static double lastTime;
        static int configuration;
        static bool dirty;

        static EmergenceEditorPreview()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            AssemblyReloadEvents.beforeAssemblyReload += EmergenceGpuSimulation.DisposeAll;
            EditorApplication.quitting += Stop;
            EditorApplication.quitting += EmergenceGpuSimulation.DisposeAll;
            Undo.undoRedoPerformed += SettingsChanged;
        }

        public static bool CanPreview(EmergenceField field) => field && field.isActiveAndEnabled &&
            field.gameObject.scene.IsValid() && field.gameObject.scene.isLoaded && !EditorUtility.IsPersistent(field) &&
            !EditorApplication.isPlayingOrWillChangePlaymode;

        public static void Run(EmergenceField field)
        {
            if (!Attach(field)) return;
            Error = null;
            if (RefreshConfiguration()) Running = true;
            lastTime = EditorApplication.timeSinceStartup; accumulator = 0;
        }
        public static void Pause() { Running = false; accumulator = 0; }
        public static void Reset(EmergenceField field)
        {
            if (!Attach(field)) return;
            configuration = int.MinValue; dirty = true; RefreshConfiguration();
            accumulator = 0; lastTime = EditorApplication.timeSinceStartup; Repaint();
        }
        public static void Step(EmergenceField field)
        {
            if (!Attach(field)) return;
            Error = null;
            Pause();
            if (RefreshConfiguration()) field.AdvanceEditorPreview(1f / Mathf.Clamp(field.profile.simulationHz, 30, 120), 1);
            Repaint();
        }
        public static void Stop()
        {
            Pause();
            if (Target) Target.ClearEditorPreview();
            Target = null; Error = null; dirty = false;
        }
        static bool Attach(EmergenceField field)
        {
            if (!CanPreview(field)) return false;
            if (Target != field)
            {
                Stop(); Target = field; configuration = int.MinValue; dirty = true;
            }
            return true;
        }
        public static void SettingsChanged() => dirty = true;

        static bool RefreshConfiguration()
        {
            if (!Target || !Target.profile) { Error = "Assign a particle profile."; Pause(); return false; }
            var p = Target.profile;
            string invalid = p.ConfigurationError();
            if (invalid != null) { Error = invalid; dirty = true; Pause(); return false; }
            int key = ConfigurationKey(Target);
            if (Error != null && !dirty && key == configuration) return false;
            if (key != configuration)
            {
                Target.RebuildPreview(); configuration = key; accumulator = 0;
                Error = Target.Simulation == null ? "The preview could not start. Check field references and whether the population fits the arena." : null;
                dirty = true;
            }
            if (Target.Simulation == null) { Pause(); return false; }
            Error = null;
            if (dirty) { Target.RenderEditorPreview(); dirty = false; Repaint(); }
            return true;
        }
        static int ConfigurationKey(EmergenceField field)
        {
            var p = field.profile;
            unchecked
            {
                int key = p.GetInstanceID();
                void Add(int value) => key = key * 31 + value;
                Add(p.seed); Add(p.initialCoverage.GetHashCode()); Add(p.initialSpeed.GetHashCode());
                Add((int)p.backend);
                Add(p.particleGap.GetHashCode()); Add(p.coreRangeMultiplier.GetHashCode());
                Add(p.interactionRadius.GetHashCode()); Add(p.connectionRadius.GetHashCode()); Add(p.feedingRadius.GetHashCode());
                foreach (var s in p.species) { Add(s.count); Add(s.diameter.GetHashCode()); Add(s.sizeVariation.GetHashCode()); }
                Add(field.presentationOnly.GetHashCode()); Add(field.presentationHalfSize.GetHashCode());
                Add(field.presentationWarmup.GetHashCode()); Add(field.arena ? field.arena.GetInstanceID() : 0);
                return key;
            }
        }
        static void Tick()
        {
            if (!Target) { Running = false; return; }
            if (EditorApplication.isCompiling || !CanPreview(Target)) { Stop(); return; }
            double now = EditorApplication.timeSinceStartup;
            float elapsed = (float)(now - lastTime); lastTime = now;
            if (!RefreshConfiguration() || !Running) return;
            float dt = 1f / Mathf.Clamp(Target.profile.simulationHz, 30, 120);
            int maximum = Mathf.Clamp(Target.profile.maximumStepsPerFrame, 1, 3);
            accumulator = Mathf.Min(accumulator + Mathf.Max(0, elapsed) * speed, dt * maximum);
            int steps = Mathf.Min(maximum, Mathf.FloorToInt(accumulator / dt));
            if (steps == 0) return;
            try { Target.AdvanceEditorPreview(dt, steps); accumulator -= dt * steps; }
            catch (System.Exception exception) { Error = exception.Message; Pause(); }
            Repaint();
        }
        static void PlayModeChanged(PlayModeStateChange state)
        {
            // Clear the preview before play, including when domain/scene reloads are disabled.
            if (state == PlayModeStateChange.ExitingEditMode) Stop();
        }
        static void Repaint() { SceneView.RepaintAll(); EditorApplication.QueuePlayerLoopUpdate(); }

        public static EmergenceField FindField(EmergenceProfile profile)
        {
            if (Target && Target.profile == profile) return Target;
            foreach (var field in Object.FindObjectsByType<EmergenceField>(FindObjectsSortMode.None))
                if (field.profile == profile && CanPreview(field)) return field;
            return null;
        }
        public static void DrawControls(EmergenceField field)
        {
            EditorGUILayout.LabelField("Editor particle preview", EditorStyles.boldLabel);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Exit Play Mode to use the editor particle preview.", MessageType.None);
                return;
            }
            if (!field)
            {
                EditorGUILayout.HelpBox("Open the EMERGENCE scene to preview its particles here.", MessageType.Info);
                return;
            }
            EditorGUILayout.HelpBox("Run the ecology in Scene view without Play Mode. Colors and forces update live; counts, sizes and distance ranges automatically restart the preview. Settings edits persist in the shared profile. Nuggets are counted without creating pickups or awarding score.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!CanPreview(field)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool running = Target == field && Running;
                    if (GUILayout.Button(running ? "Pause preview" : "Run preview")) { if (running) Pause(); else Run(field); }
                    if (GUILayout.Button("Step")) Step(field);
                    if (GUILayout.Button("Reset")) Reset(field);
                }
                speed = EditorGUILayout.Slider("Preview speed", speed, .25f, 2);
            }
            if (Target == field)
            {
                if (Error != null) EditorGUILayout.HelpBox(Error, MessageType.Warning);
                if (field.Simulation != null)
                    EditorGUILayout.LabelField((Running ? "Running" : "Paused") + " · " + field.Simulation.Age.ToString("F1") + " s · " +
                        field.Simulation.Count + " particles · " + field.Simulation.Secreted + " nuggets produced · " + (field.UsesGpu ? "GPU" : "CPU reference"), EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.Space();
        }
    }
}
