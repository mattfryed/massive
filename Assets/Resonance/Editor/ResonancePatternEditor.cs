using UnityEditor;
using UnityEngine;

namespace Massive.Resonance.Editor
{
    [CustomEditor(typeof(ResonancePatternController))]
    public sealed class ResonancePatternControllerEditor : UnityEditor.Editor
    {
        private int previewArc;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var controller = (ResonancePatternController)target;
            if (controller.rendering==ResonanceRendering.OptionBParticles)
            {
                EditorGUILayout.LabelField("Particle pool (all layers + ghost)",controller.ParticleCount+" / "+controller.particles.particleBudget);
                EditorGUILayout.HelpBox("Option B uses real Unity Particle Systems to sample the same energy field. The original layer widths/colors/tapers and contact controls still apply. Particle settings add density, dot size, lifetime, presence and wandering. Generated particle modules are transient: tune this controller. The full-pattern grains are hidden until a Core reveal.",MessageType.Info);
                if (controller.particleMaterial==null) EditorGUILayout.HelpBox("Assign the Resonance Particles material.",MessageType.Warning);
            }
            EditorGUILayout.HelpBox("Global profile Enabled toggles supersede the definition without modifying it. Positive Taper Length overrides Taper Fraction. Player Response and Core Response are independent; Pattern Default restores each arc's original behavior. Explicit Sludge/Magnetic modes use Field Affected Layers, regardless of Fields Affect Only Amplifier Cores (that legacy filter belongs to arc lenses/membranes).", MessageType.Info);
            if (controller.rendering==ResonanceRendering.OptionAContinuous && controller.segmentMaterial == null) EditorGUILayout.HelpBox("Assign a Resonance Ribbon material to generate visible geometry and contacts.", MessageType.Warning);
            if (GUILayout.Button("Select Pattern Definition")) Selection.activeObject = controller.definition;
            if (GUILayout.Button("Fit to Arena Bounds"))
            {
                Undo.RecordObjects(new Object[] { controller, controller.transform }, "Fit Resonance");
                controller.FitToArena(); controller.Rebuild(); EditorUtility.SetDirty(controller);
            }
            if (GUILayout.Button("Rebuild")) controller.Rebuild();
            if (GUILayout.Button("Preview Core Impact — Full Pattern Flash")) controller.PreviewImpact();
            previewArc=EditorGUILayout.IntSlider("Local preview arc",previewArc,0,Mathf.Max(0,controller.InteractiveSegments.Count-1));
            if (previewArc<controller.InteractiveSegments.Count) EditorGUILayout.LabelField("Preview target",controller.InteractiveSegments[previewArc].Settings.name);
            if (GUILayout.Button("Preview Local Core — Compression / Flare / Pulses")) controller.PreviewLocalContact(true,previewArc);
            if (GUILayout.Button("Preview Player — Passage / Wake / Recovery")) controller.PreviewLocalContact(false,previewArc);
            EditorGUILayout.HelpBox("Localized Contact Effects are presentation only. Core feedback builds during magnetic braking and releases at turnaround (or a hard Core impact). Player wakes remain disturbed while crossing/sludging, then recover. Reach / Pulse Width / Speed use local pattern units along the arc. These controls do not alter collisions, actor movement, or grid attraction. Previews work in Edit or Play Mode without spawning actors.",MessageType.Info);
            EditorGUILayout.LabelField("Grid attraction samples", controller.GridSampleCount + " / " + controller.gridSampleBudget);
            EditorGUILayout.HelpBox("Override Grid Falloff affects only this pattern's grid attraction. Outer Feather is the fraction of the attraction radius that fades to zero at the outer edge; Inner Softening reduces the pull near each sample center to avoid pinching. These are not visual taper or collision controls. Turn the override off to compare the previous shared VectorGridGPU falloff.", MessageType.Info);
            EditorGUILayout.HelpBox("Filament, Ribbon and Diffuse are three parts of the resting visual. Diffuse never expands collision. Energy Flow defaults to From Pattern Center. The full-pattern impact ghost remains separate and never attracts the grid or collides. Grid attraction and actor responses run in Play Mode.", MessageType.Info);
            if (controller.rendering==ResonanceRendering.OptionAContinuous)
                EditorGUILayout.HelpBox("Option A uses a planar curve-distance field, not camera-facing billboards or overlapping strip slices. Diffuse is procedural shader haze. Each layer has an HDR Color picker. Plasma Displacement / Noise Scale / Noise Speed / Fine Detail / Tip Boost control live electrical motion. Arc Color Influence optionally mixes definition colors back in.", MessageType.Info);
            EditorGUILayout.HelpBox("Magnetic Path is GLOBAL: Curved Glide keeps tangential momentum through the turn; Stop And Reflect is the previous V-shaped full stop. Magnetic Reach, Brake/Release Seconds, Tangential Carry, Surface Follow and Speed Retention apply to every arc. Per-arc Deflection Degrees is only for legacy Lens Deflector, not magnetic Cores.", MessageType.Info);
            if (controller.blending == ResonanceBlend.Multiply) EditorGUILayout.HelpBox("Multiply darkens existing pixels; on a black background it may be invisible. Use Alpha, Additive or Screen for luminous energy.", MessageType.Info);
        }
    }

    [CustomEditor(typeof(ResonancePatternDefinition))]
    public sealed class ResonancePatternDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Angles run from +X toward +Z. For the 345 Hz outer curve, concave sections are centered at 0/90/180/270°. Enable Independent Collision Profile for separate contact start/end (fractions of this arc), taper length and falloff. Resting thickness/taper remain independent. Contacts are capped to the visible footprint. Full-pattern impact ghosts never collide.", MessageType.Info);
            DrawDefaultInspector();
            var definition = (ResonancePatternDefinition)target;
            if (definition.curves.Count == 0 && GUILayout.Button("Populate 345 Hz Defaults"))
            {
                Undo.RecordObject(definition, "Populate Resonance Pattern");
                definition.Set345HzDefaults(); EditorUtility.SetDirty(definition);
            }
        }
    }

    public static class ResonancePrototypeSetup
    {
        public const string Folder = "Assets/Resonance";
        [MenuItem("MASSIVE/Resonance/Create 345 Hz Prototype")]
        public static void CreatePrototype()
        {
            string assetPath = Folder + "/345 Hz Pattern.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ResonancePatternDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ResonancePatternDefinition>();
                definition.Set345HzDefaults(); AssetDatabase.CreateAsset(definition, assetPath);
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Resonance Ribbon.mat");
            if (material == null)
            {
                Shader shader = Shader.Find("MASSIVE/Resonance/Ribbon");
                if (shader == null) { Debug.LogError("Resonance Ribbon shader has not imported."); return; }
                material = new Material(shader); AssetDatabase.CreateAsset(material, Folder + "/Resonance Ribbon.mat");
            }
            var wall = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Folder + "/Resonance Wall.physicMaterial");
            if (wall == null)
            {
                wall = new PhysicsMaterial("Resonance Wall") { dynamicFriction = 0f, staticFriction = 0f, bounciness = 0.65f, bounceCombine = PhysicsMaterialCombine.Maximum };
                AssetDatabase.CreateAsset(wall, Folder + "/Resonance Wall.physicMaterial");
            }
            GameObject go = new GameObject("Resonance 345 Hz Prototype");
            Undo.RegisterCreatedObjectUndo(go, "Create Resonance Prototype");
            var controller = go.AddComponent<ResonancePatternController>();
            controller.definition = definition; controller.segmentMaterial = material; controller.wallMaterial = wall;
            // Prefab remains portable. Arena reference belongs only to the scene instance.
            string prefabPath = Folder + "/Resonance 345 Hz.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.AutomatedAction);
            controller.arenaBounds = Object.FindFirstObjectByType<ArenaBoundsFromVectorGrid>();
            controller.FitToArena(); controller.Rebuild();
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = go;
        }
    }
}
