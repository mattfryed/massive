using System;
using System.Collections.Generic;
using System.Linq;
using Massive.PowerUps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.EditorTools
{
    [InitializeOnLoad]
    public static class PowerUpSettingsEditing
    {
        public const string Path = "Assets/Power-ups/Resources/PowerUpSettings.asset";
        static PowerUpSettingsEditing() { Undo.undoRedoPerformed += Refresh; }
        public static PowerUpSettings GetOrCreate()
        {
            var asset = AssetDatabase.LoadAssetAtPath<PowerUpSettings>(Path);
            if (asset) return asset;
            if (!AssetDatabase.IsValidFolder("Assets/Power-ups/Resources"))
                AssetDatabase.CreateFolder("Assets/Power-ups", "Resources");
            asset = ScriptableObject.CreateInstance<PowerUpSettings>();
            string[] paths = { "Time Dilation/PU_TimeDilation", "Particle Accelerator/PU_ParticleAccelerator", "Decoherence/PU_Decoherence", "Mass Node/PU_MassNode", "Amplifier Node/PU_AmplifierNode" };
            asset.definitions = paths.Select(p=>AssetDatabase.LoadAssetAtPath<PowerUpDefinition>("Assets/Power-ups/"+p+".asset")).ToList();
            asset.toasts.prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Power-ups/PU_PickupToast.prefab").GetComponent<PowerUpPickupToast>();
            AssetDatabase.CreateAsset(asset, Path); AssetDatabase.SaveAssetIfDirty(asset);
            PowerUpSettings.Reload();
            return asset;
        }
        public static void Save(Object asset)
        {
            if (asset is PowerUpSettings settings) settings.NotifyChanged();
            EditorUtility.SetDirty(asset);
            if (EditorUtility.IsPersistent(asset)) AssetDatabase.SaveAssetIfDirty(asset);
            Refresh();
        }
        public static void Refresh()
        {
            var settings = PowerUpSettings.Current;
            if (settings) settings.NotifyChanged();
            if (Application.isPlaying)
                foreach (var animator in Object.FindObjectsByType<PowerUpIconManifestAnimator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (!EditorSceneManager.IsPreviewSceneObject(animator.gameObject)) animator.RefreshSharedSettings();
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
        }
        public static void SetPercentage(PowerUpSettings settings, PowerUpDefinition selected, float percent)
        {
            var defs = settings.definitions.Where(d=>d).Distinct().ToArray();
            if (!defs.Contains(selected)) return;
            Undo.RecordObjects(defs, "Adjust power-up spawn mix");
            percent = Mathf.Clamp(percent, 0, 100);
            var others = defs.Where(d=>d!=selected).ToArray();
            float total = others.Sum(d=>Mathf.Max(0,d.spawnWeight));
            selected.spawnWeight = others.Length == 0 ? 100 : percent;
            foreach (var def in others)
                def.spawnWeight = (100-percent) * (total > 0 ? Mathf.Max(0,def.spawnWeight)/total : 1f/others.Length);
            foreach (var def in defs) { EditorUtility.SetDirty(def); if (EditorUtility.IsPersistent(def)) AssetDatabase.SaveAssetIfDirty(def); }
            Refresh();
        }
        public static T[] InScene<T>(Scene scene) where T:Component =>
            scene.IsValid() && scene.isLoaded ? scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray() : Array.Empty<T>();

        public static PowerUpSpawner EnsureScene(Scene scene, VectorGridGPU grid, Camera camera)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
                throw new InvalidOperationException("Install into a loaded scene in Edit Mode.");
            GetOrCreate();
            var grids = InScene<VectorGridGPU>(scene);
            if (!grid) grid = grids.Length == 1 ? grids[0] : null;
            if (!grid || grid.gameObject.scene != scene) throw new InvalidOperationException("Choose this scene's playfield grid first.");
            if (camera && camera.gameObject.scene != scene) throw new InvalidOperationException("Choose a camera from this scene.");
            var spawners = InScene<PowerUpSpawner>(scene);
            if (spawners.Length > 1) throw new InvalidOperationException("Multiple spawners found. Resolve the duplicate components before installing.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Install / repair power-up system");
            PowerUpSpawner spawner = spawners.FirstOrDefault();
            if (!spawner)
            {
                var root = new GameObject("Power-up System");
                SceneManager.MoveGameObjectToScene(root, scene); Undo.RegisterCreatedObjectUndo(root, "Create power-up system");
                spawner = Undo.AddComponent<PowerUpSpawner>(root);
            }
            if (!spawner.vectorGrid)
            {
                Undo.RecordObject(spawner,"Bind power-up playfield"); spawner.vectorGrid=grid;
                PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
            }
            var toasts = InScene<PowerUpPickupToastSystem>(scene);
            var toast = toasts.FirstOrDefault();
            if (!toast) toast = Undo.AddComponent<PowerUpPickupToastSystem>(spawner.gameObject);
            using (var data = new SerializedObject(toast))
            {
                data.Update();
                if (!data.FindProperty("toastPrefab").objectReferenceValue)
                    data.FindProperty("toastPrefab").objectReferenceValue = GetOrCreate().toasts.prefab;
                if (!data.FindProperty("cameraOverride").objectReferenceValue && camera)
                    data.FindProperty("cameraOverride").objectReferenceValue=camera;
                data.ApplyModifiedProperties();
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            return spawner;
        }
        public static List<string> Validate(PowerUpSettings settings)
        {
            var issues = new List<string>();
            if (!settings) { issues.Add("The global settings asset is missing."); return issues; }
            if (!settings.toasts.prefab) issues.Add("Assign the shared pickup toast prefab.");
            if (settings.definitions.Count == 0) issues.Add("The power-up catalogue is empty.");
            if (settings.definitions.Any(d=>!d)) issues.Add("The catalogue contains missing definitions.");
            var definitions = settings.definitions.Where(d=>d).ToArray();
            if (definitions.Distinct().Count()!=definitions.Length) issues.Add("The catalogue contains duplicate definitions.");
            if (definitions.Select(d=>d.Type).Distinct().Count()!=definitions.Length) issues.Add("The catalogue contains duplicate power-up types.");
            foreach (var def in definitions)
            {
                if (def is AmplifierNodePowerUpDefinition amplifier && !amplifier.massNode)
                    issues.Add(def.name + ": assign the shared Mass Node grant.");
                if (!def.pickupPrefab) { issues.Add(def.name+": missing pickup prefab."); continue; }
                var pickup = def.pickupPrefab.GetComponent<PowerUpPickup>();
                if (!pickup || pickup.definition != def) issues.Add(def.name+": pickup prefab must reference this definition.");
                if (!def.pickupPrefab.GetComponent<PowerUpIconManifestAnimator>()) issues.Add(def.name+": missing shared icon animator.");
                if (!def.pickupPrefab.GetComponentsInChildren<Collider>(true).Any(c=>c.isTrigger)) issues.Add(def.name+": missing trigger collider.");
            }
            return issues;
        }
    }

    public sealed class PowerUpSettingsWindow : EditorWindow
    {
        [SerializeField] private int tab, globalTab;
        private Vector2 scroll;
        private PowerUpSettings settings;
        private VectorGridGPU sceneGrid;
        private Camera sceneCamera;
        private string result;
        private static readonly string[] Globals = { "Visuals", "Life cycle", "Spawning", "Toasts", "Scene setup" };

        [MenuItem("MASSIVE/Power-up Settings %#&F1", false, 6)]
        [MenuItem("MASSIVE/Power-ups/Global Settings", false, 0)]
        public static void Open()
        {
            var window = GetWindow<PowerUpSettingsWindow>("Power-up Settings");
            window.minSize = new Vector2(720,620); window.Show();
        }
        private void OnEnable()
        {
            settings=PowerUpSettingsEditing.GetOrCreate();
            Undo.undoRedoPerformed+=Repaint;
            EditorApplication.playModeStateChanged+=PlayModeChanged;
        }
        private void OnDisable()
        {
            Undo.undoRedoPerformed-=Repaint;
            EditorApplication.playModeStateChanged-=PlayModeChanged;
        }
        private void PlayModeChanged(PlayModeStateChange _) { sceneGrid=null; sceneCamera=null; Repaint(); }
        private void OnInspectorUpdate() => Repaint();
        private void OnGUI()
        {
            if (!settings) settings=PowerUpSettingsEditing.GetOrCreate();
            EditorGUIUtility.labelWidth=Mathf.Min(270,position.width*.4f);
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("POWER-UPS",EditorStyles.boldLabel); GUILayout.FlexibleSpace();
                GUILayout.Label("GLOBAL • saved across all scenes",EditorStyles.miniLabel);
                if(GUILayout.Button("Undo",EditorStyles.toolbarButton)) Undo.PerformUndo();
                if(GUILayout.Button("Redo",EditorStyles.toolbarButton)) Undo.PerformRedo();
                if(GUILayout.Button("Select asset",EditorStyles.toolbarButton)) Selection.activeObject=settings;
            }
            EditorGUILayout.HelpBox("One shared profile controls every scene. Edits save immediately, including in Play Mode. Scene/prefab tuning cannot override these global controls.",MessageType.Info);
            var definitions=settings.definitions.Where(d=>d).Distinct().ToArray();
            string[] tabs=new[]{"Global"}.Concat(definitions.Select(d=>d.displayName)).ToArray();
            tab=Mathf.Clamp(tab,0,tabs.Length-1);
            tab=GUILayout.Toolbar(tab,tabs,GUILayout.Height(27));
            if(tab==0) globalTab=GUILayout.Toolbar(globalTab,Globals,GUILayout.Height(24));
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(8);
            if(tab>0) DrawDefinition(definitions[tab-1]);
            else if(globalTab==0)
            {
                EditorGUILayout.HelpBox("Natural despawn reverses spawn; inner acquire starts on the hit. Shell Scale also fits the pickup trigger and solid collider to the shell. Inner Scale affects only the representative icon.",MessageType.None);
                Group("visuals");
            }
            else if(globalTab==1)
            {
                Group("lifecycle");
                EditorGUILayout.HelpBox("Lifetime is measured from spawn until the natural outro begins. Zero means no timeout. Life cycle edits affect the next spawn/equip; running timers finish normally. Visual edits update live pickups.",MessageType.None);
                foreach(var d in definitions)
                    EditorGUILayout.LabelField(d.displayName,d is IInstantPowerUpEffect ? "Instant effect • world "+Lifetime(d) : "World "+Lifetime(d)+" • equipped "+d.EffectDurationSeconds.ToString("0.##")+" s");
            }
            else if(globalTab==2) { Group("spawning"); DrawMixer(definitions); }
            else if(globalTab==3)
            {
                Group("toasts");
                if(settings.toasts.prefab) PrefabLink(settings.toasts.prefab.gameObject,"Typography / panel prefab");
                EditorGUILayout.HelpBox("The next toast uses these values. Font, label and border styling are authored in the one shared toast prefab.",MessageType.None);
            }
            else DrawSceneSetup();
            EditorGUILayout.EndScrollView();
        }
        private static string Lifetime(PowerUpDefinition d)=>d.WorldLifetimeSeconds<=0 ? "unlimited" : d.WorldLifetimeSeconds.ToString("0.##")+" s";
        private void Group(string name)
        {
            using(var data=new SerializedObject(settings))
            {
                data.Update();
                var root=data.FindProperty(name); var end=root.GetEndProperty();
                root.NextVisible(true);
                while(!SerializedProperty.EqualContents(root,end))
                {
                    EditorGUILayout.PropertyField(root,true);
                    if(!root.NextVisible(false)) break;
                }
                if(data.ApplyModifiedProperties()) PowerUpSettingsEditing.Save(settings);
            }
        }
        private void DrawMixer(PowerUpDefinition[] defs)
        {
            EditorGUILayout.Space(10); EditorGUILayout.LabelField("Spawn chance mixer",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Changing one percentage redistributes the rest proportionally. Zero excludes a power-up. Turn off Spawning or set Max Concurrent to zero to suspend standard spawns.",MessageType.None);
            float sum=defs.Sum(d=>Mathf.Max(0,d.spawnWeight));
            foreach(var def in defs)
            {
                float percent=sum>0 ? Mathf.Max(0,def.spawnWeight)/sum*100 : 0;
                EditorGUI.BeginChangeCheck();
                float next=EditorGUILayout.Slider(def.displayName,percent,0,100);
                if(EditorGUI.EndChangeCheck()) { PowerUpSettingsEditing.SetPercentage(settings,def,next); GUIUtility.ExitGUI(); }
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Total",sum>0 ? "100%" : "0% — no power-ups can spawn");
                if(GUILayout.Button("Equal mix",GUILayout.Width(110)))
                {
                    Undo.RecordObjects(defs,"Equal power-up spawn mix");
                    foreach(var def in defs) { def.spawnWeight=1; PowerUpSettingsEditing.Save(def); }
                }
            }
            if(settings.TotalWeight()<=0) EditorGUILayout.HelpBox("No eligible pickup has a positive spawn chance.",MessageType.Warning);
        }
        private void DrawDefinition(PowerUpDefinition definition)
        {
            using(new EditorGUILayout.HorizontalScope())
            {
                using(new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("Shared definition",definition,typeof(PowerUpDefinition),false);
                if(GUILayout.Button("Select",GUILayout.Width(65))) Selection.activeObject=definition;
            }
            EditorGUILayout.HelpBox("These are the actual shared gameplay definitions. Changes apply anywhere this power-up is used. Spawn chance is managed in Global > Spawning.",MessageType.Info);
            if (definition is AmplifierNodePowerUpDefinition amplifierNode)
            {
                EditorGUILayout.HelpBox("Instant: Mass Node's mass gain plus the next personal multiplier milestone (for example, ×1.5 → ×2, or ×2 → ×4). At the cap it still grants mass. It leaves your equipped ability and team amplification unchanged.", MessageType.Info);
                if (amplifierNode.massNode && GUILayout.Button("Edit shared Mass Node grant"))
                {
                    var definitions = settings.definitions.Where(d => d).Distinct().ToArray();
                    int index = Array.IndexOf(definitions, amplifierNode.massNode);
                    if (index >= 0) { tab = index + 1; scroll = Vector2.zero; }
                    else Selection.activeObject = amplifierNode.massNode;
                }
            }
            if (definition is ParticleAcceleratorPowerUpDefinition)
                EditorGUILayout.HelpBox("Acquisition fills the energy ring. Sword fires instantly; hold to drain, release to refill after the minimum burst. Low energy caps that burst. After emptying the ring, press again to fire any recovered energy. Turn Propagation Speed controls how quickly aim travels along the beam; Max Turn Delay = 0 gives instant aim.", MessageType.None);
            if (definition is ParticleAcceleratorPowerUpDefinition energySettings)
                EditorGUILayout.LabelField("Minimum burst / full meter", $"{Mathf.Min(1, energySettings.minimumBurstSeconds / Mathf.Max(.01f, energySettings.fullMeterFireSeconds)):P0}");
            using(var data=new SerializedObject(definition))
            {
                data.Update(); var p=data.GetIterator(); bool enter=true;
                while(p.NextVisible(enter))
                {
                    enter=false;
                    if(p.name=="m_Script" || p.name=="spawnWeight") continue;
                    bool globalLife=p.name=="worldLifetimeSeconds" && !settings.lifecycle.useDefinitionLifetimes;
                    using(new EditorGUI.DisabledScope(globalLife))
                    {
                        if (definition is ParticleAcceleratorPowerUpDefinition && (p.name == "movementWhileFiring" || p.name == "turningWhileFiring"))
                            EditorGUILayout.PropertyField(p, new GUIContent(p.name == "movementWhileFiring" ?
                                "Movement Speed Scale While Firing" : "Turning Speed Scale While Firing", p.tooltip), true);
                        else EditorGUILayout.PropertyField(p,true);
                    }
                    if(globalLife) EditorGUILayout.LabelField("Effective world lifetime",Lifetime(definition),EditorStyles.miniLabel);
                    if(p.propertyType==SerializedPropertyType.ObjectReference && p.objectReferenceValue is GameObject prefab)
                        PrefabLink(prefab,"Edit "+p.displayName);
                    if(p.propertyType==SerializedPropertyType.ObjectReference && p.objectReferenceValue is ParticleAcceleratorBeam beam)
                        PrefabLink(beam.gameObject,"Edit sustained beam prefab");
                }
                if(data.ApplyModifiedProperties()) PowerUpSettingsEditing.Save(definition);
            }
            if (definition is ParticleAcceleratorPowerUpDefinition accelerator && accelerator.sustainedBeamPrefab)
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("Validate sustained beam in PLAYER ACTIONS")) ParticleAcceleratorBeamValidation.Run();
                beamAppearance = EditorGUILayout.Foldout(beamAppearance, "Beam, emitter and impact appearance", true);
                if (beamAppearance)
                {
                    DrawBeamComponent(accelerator.sustainedBeamPrefab.beamVisual, "Plasma strands");
                    DrawBeamComponent(accelerator.sustainedBeamPrefab.corePlasma, "Emitter plasma");
                    DrawBeamComponent(accelerator.sustainedBeamPrefab.contactPlasma, "Impact plasma");
                }
                ringAppearance = EditorGUILayout.Foldout(ringAppearance, "Energy ring appearance", true);
                if (ringAppearance && accelerator.vfxModulePrefab)
                {
                    var ring = accelerator.vfxModulePrefab.GetComponent<ParticleAcceleratorVFXModule>();
                    if (ring) using (var data = new SerializedObject(ring))
                    {
                        data.Update(); var property = data.GetIterator();
                        while (property.NextVisible(true))
                            if (property.depth == 0 && (property.name.StartsWith("ring") || property.name == "metaballMaterial" || property.name == "vfxPlaneYOffsetWorld"))
                                EditorGUILayout.PropertyField(property, true);
                        if (data.ApplyModifiedProperties()) { EditorUtility.SetDirty(ring); AssetDatabase.SaveAssetIfDirty(ring); }
                    }
                }
            }
        }
        private bool beamAppearance;
        private bool ringAppearance;
        private static void DrawBeamComponent(UnityEngine.Object component, string label)
        {
            if (!component) return;
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            using (var data = new SerializedObject(component))
            {
                data.Update(); var p = data.GetIterator(); bool enter = true;
                while (p.NextVisible(enter))
                { enter = false; if (p.name != "m_Script") EditorGUILayout.PropertyField(p, true); }
                if (data.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(component);
            }
        }
        private static void PrefabLink(GameObject prefab,string label)
        {
            using(new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(18);
                if(GUILayout.Button(label)) AssetDatabase.OpenAsset(prefab);
                if(GUILayout.Button("Ping",GUILayout.Width(55))) EditorGUIUtility.PingObject(prefab);
            }
        }
        private void DrawSceneSetup()
        {
            var scene=SceneManager.GetActiveScene();
            EditorGUILayout.LabelField("Active scene",scene.name,EditorStyles.boldLabel);
            var spawners=PowerUpSettingsEditing.InScene<PowerUpSpawner>(scene);
            var toasts=PowerUpSettingsEditing.InScene<PowerUpPickupToastSystem>(scene);
            var grids=PowerUpSettingsEditing.InScene<VectorGridGPU>(scene);
            var cameras=PowerUpSettingsEditing.InScene<Camera>(scene);
            if(!sceneGrid || sceneGrid.gameObject.scene!=scene)
                sceneGrid=spawners.FirstOrDefault(s=>s.vectorGrid)?.vectorGrid ?? (grids.Length==1 ? grids[0] : null);
            if(!sceneCamera || sceneCamera.gameObject.scene!=scene)
                sceneCamera=cameras.FirstOrDefault(c=>c.CompareTag("MainCamera")) ?? cameras.FirstOrDefault();
            EditorGUILayout.LabelField("Spawner / toast systems",spawners.Length+" / "+toasts.Length);
            foreach(var spawner in spawners)
            {
                EditorGUILayout.ObjectField("Spawner",spawner,typeof(PowerUpSpawner),true);
                if(Application.isPlaying && spawner.isActiveAndEnabled)
                    EditorGUILayout.LabelField("Live status",spawner.ActivePickupCount+" pickups • next attempt in "+Mathf.Max(0,spawner.NextSpawnTime-Time.time).ToString("0.0")+" s");
                if(!spawner.isActiveAndEnabled) EditorGUILayout.HelpBox("This spawner is inactive. Existing scene/demo enable states are preserved by repair.",MessageType.None);
            }
            sceneGrid=(VectorGridGPU)EditorGUILayout.ObjectField("Playfield grid",sceneGrid,typeof(VectorGridGPU),true);
            sceneCamera=(Camera)EditorGUILayout.ObjectField("Toast camera",sceneCamera,typeof(Camera),true);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || !sceneGrid))
                if(GUILayout.Button("Install / Repair Scene System",GUILayout.Height(30)))
                {
                    try { var installed=PowerUpSettingsEditing.EnsureScene(scene,sceneGrid,sceneCamera); result="Scene wired. Save the scene to keep its bindings."; EditorGUIUtility.PingObject(installed); }
                    catch(Exception e) { result=e.Message; }
                }
            EditorGUILayout.HelpBox("Reuses the scene's existing spawner and toast system. Adds missing components and missing grid/camera bindings with Undo. Existing active states are preserved. Each scene keeps its own arena references; all tuning comes from the global profile.",MessageType.None);
            if(spawners.Length>1 || toasts.Length>1) EditorGUILayout.HelpBox("Duplicate systems found. Keep one of each per scene. Runtime spawners share the limit and elect one scheduler.",MessageType.Warning);
            if(GUILayout.Button("Validate settings & scene"))
            {
                var issues=PowerUpSettingsEditing.Validate(settings);
                if(spawners.Length!=1) issues.Add("Expected one spawner in this scene.");
                if(toasts.Length!=1) issues.Add("Expected one toast system in this scene.");
                if(spawners.Any(s=>!s.vectorGrid)) issues.Add("A spawner has no playfield grid.");
                result=issues.Count==0 ? "All settings and scene bindings are valid." : string.Join("\n",issues);
            }
            if(!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result,MessageType.Info);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!=PlayerActionTestArenaSetup.ScenePath))
                if(GUILayout.Button("Run full power-up validation (PLAYER ACTIONS scene)")) PowerUpIconDemoValidation.Run();
            EditorGUILayout.Space(12);
            using(var data=new SerializedObject(settings))
            {
                data.Update();
                EditorGUILayout.PropertyField(data.FindProperty("definitions"),new GUIContent("Shared power-up catalogue"),true);
                if(data.ApplyModifiedProperties()) PowerUpSettingsEditing.Save(settings);
            }
        }
    }

    [CustomEditor(typeof(PowerUpSettings))]
    public sealed class PowerUpSettingsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Project-wide power-up settings. All scenes load this asset automatically.",MessageType.Info);
            if(GUILayout.Button("Open Power-up Settings")) PowerUpSettingsWindow.Open();
            DrawDefaultInspector();
            if(GUI.changed) PowerUpSettingsEditing.Save(target);
        }
    }
    [CustomEditor(typeof(PowerUpSpawner))]
    public sealed class PowerUpSpawnerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Timing, limits, placement rules and spawn mix are controlled globally. This component only binds the scene's playfield.",MessageType.Info);
            if(GUILayout.Button("Open Global Power-up Settings")) PowerUpSettingsWindow.Open();
            serializedObject.Update(); EditorGUILayout.PropertyField(serializedObject.FindProperty("vectorGrid"));
            serializedObject.ApplyModifiedProperties();
        }
    }
    [CustomEditor(typeof(PowerUpIconManifestAnimator))]
    public sealed class PowerUpIconAnimatorInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Animation, shell size and inner-icon size are controlled globally. These fields only configure this prefab's bindings and lifecycle behavior.",MessageType.Info);
            if(GUILayout.Button("Open Global Power-up Settings")) PowerUpSettingsWindow.Open();
            serializedObject.Update();
            foreach(string field in new[]{"controller","despawnRoot","wire","metaballs","playIntroOnEnable","startHiddenOnAwake","disableCollidersOnDespawn","destroyOnComplete"})
                EditorGUILayout.PropertyField(serializedObject.FindProperty(field),true);
            serializedObject.ApplyModifiedProperties();
        }
    }
    [CustomEditor(typeof(PowerUpPickupToastSystem))]
    public sealed class PowerUpToastSystemInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Toast appearance and timing come from Global Power-up Settings. Camera and parent remain scene bindings.",MessageType.Info);
            if(GUILayout.Button("Open Global Power-up Settings")) PowerUpSettingsWindow.Open();
            serializedObject.Update();
            foreach(string field in new[]{"cameraOverride","parent","prewarmOnStart"}) EditorGUILayout.PropertyField(serializedObject.FindProperty(field));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
