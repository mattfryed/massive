using System;
using System.Collections.Generic;
using Massive.Settings;
using Massive.Player;
using Massive.Multiplier;
using Massive.Resonance;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Massive.EditorTools
{
    [InitializeOnLoad]
    public static class SharedSettingsEditing
    {
        public const string Folder = "Assets/Scripts/Settings/Resources";
        static SharedSettingsEditing() { Undo.undoRedoPerformed += Refresh; }

        private static bool profilesReady;
        public static void EnsureProfiles()
        {
            if (profilesReady) return;
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Scripts/Settings", "Resources");
            Create<MeleeVisualProfile>(p =>
            {
                var player = SceneSource<PlayerMeleePlasma>();
                foreach (var item in Object.FindObjectsByType<PlayerMeleePlasma>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (item.transform.parent != null && item.transform.parent.name == "Players" && item.GetComponent<PlayerControllerScript>() != null
                        && item.GetComponent<PlayerControllerScript>().playerID == 0) { player = item; break; }
                Copy(player, p);
                if (player) { Copy(player.GetComponent<AttackTrailGPU>(), p.legacyParticles); Copy(player.GetComponent<PlayerRepulsorFeedback>(), p.body); Copy(player.GetComponent<PlayerRepulsorGridPulse>(), p.grid); }
            });
            Create<AmplifierSharedProfile>(p =>
            {
                var treatment = SceneSource<AmplifierGoalTreatments>();
                if (treatment) p.treatment = JsonUtility.FromJson<AmplifierTreatmentSettings>(JsonUtility.ToJson(treatment.Settings));
                var cycle = SceneSource<AmplifierResonanceSpawner>();
                var core = cycle && cycle.corePrefab ? cycle.corePrefab : SceneSource<AmplifierCoreGameplay>();
                if (core) { Copy(core, p.core); Copy(core.GetComponent<AmplifierCoreVisual>(), p.coreSurface); }
                Copy(SceneSource<AmplifierGoalCapture>(), p.goal);
                Copy(SceneSource<TeamAmplifierToastPresenter>(), p.toast);
            });
            Create<ResonanceSharedProfile>(p =>
            {
                var cycle = SceneSource<AmplifierResonanceSpawner>();
                Copy(cycle, p.cycle);
                var formation = cycle && cycle.patternAnchor ? cycle.patternAnchor.GetComponent<ResonanceManifestation>() : null;
                if (!formation) formation = SceneSource<ResonanceManifestation>();
                if (!formation && cycle && cycle.patternOrder.Count > 0 && cycle.patternOrder[0].patternPrefab)
                    formation = cycle.patternOrder[0].patternPrefab.GetComponent<ResonanceManifestation>();
                Copy(formation, p.formation);
            });
            Create<TextAnimationSharedProfile>(p =>
            {
                var board = SceneSource<ScoreboardManagerScript>();
                if (board) Copy(board, p);
                var tiers = SceneSource<EnergyTierVisualController>();
                p.energyTierProfile = tiers ? tiers.Profile : AssetDatabase.LoadAssetAtPath<EnergyTierVisualProfile>("Assets/Scripts/Scoring/EnergyTierVisualProfile.asset");
            });
            SharedSettingsRuntime.Reload();
            profilesReady = true;
        }

        private static T SceneSource<T>() where T : Component
        {
            T fallback = null;
            foreach (T source in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
            {
                if (!source.gameObject.scene.IsValid() || UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(source.gameObject)) continue;
                if (source.gameObject.activeInHierarchy) return source;
                if (!fallback) fallback = source;
            }
            return fallback;
        }
        private static void Copy(Object source, object destination)
        {
            if (source != null) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), destination);
        }
        private static void Create<T>(Action<T> seed) where T : SharedSettingsProfile
        {
            string path = Folder + "/" + typeof(T).Name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<T>(path)) return;
            T asset = ScriptableObject.CreateInstance<T>();
            seed(asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        public static bool IsShared(ISharedSettingsConsumer consumer) =>
            consumer != null && consumer.UseSharedSettings && consumer.SharedSettingsAsset && consumer.SharedSettingsAsset.sharedEnabled;

        public static Object Owner(ISharedSettingsConsumer consumer) =>
            IsShared(consumer) ? (Object)consumer.SharedSettingsAsset : (Object)consumer;

        public static void Banner(SerializedObject local)
        {
            var consumer = local.targetObject as ISharedSettingsConsumer;
            if (consumer == null) return;
            EnsureProfiles();
            local.Update();
            var toggle = local.FindProperty("useSharedSettings");
            EditorGUILayout.PropertyField(toggle, new GUIContent("Use Shared Project Settings"));
            if (local.ApplyModifiedProperties()) Refresh();
            EditorGUILayout.HelpBox(IsShared(consumer)
                ? "PROJECT SETTINGS — changes apply across scenes and are saved, including in Play Mode. Preview playback and scene references stay local."
                : "LOCAL SETTINGS — this component uses its saved scene / prefab tuning.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("Shared profile", consumer.SharedSettingsAsset, typeof(SharedSettingsProfile), false);
                if (GUILayout.Button("Open", GUILayout.Width(48))) SharedSettingsWindow.Open(consumer.SharedSettingsAsset);
            }
        }

        public static SerializedObject Tuning(ISharedSettingsConsumer consumer) => new SerializedObject(Owner(consumer));
        public static void Save(Object owner)
        {
            EditorUtility.SetDirty(owner);
            if (EditorUtility.IsPersistent(owner)) AssetDatabase.SaveAssetIfDirty(owner);
            Refresh();
        }
        public static void Apply(SerializedObject data)
        {
            if (data.ApplyModifiedProperties()) Save(data.targetObject);
        }

        public static void DrawGroup(SerializedObject data, string group)
        {
            data.Update();
            if (string.IsNullOrEmpty(group))
            {
                var iterator = data.GetIterator();
                bool enter = true;
                while (iterator.NextVisible(enter))
                {
                    enter = false;
                    if (data.targetObject is MeleeVisualProfile)
                    {
                        if (OriginalAttackStageEditing.IsPrefabField(iterator.name)) continue;
                        if (iterator.name == "legacyParticles")
                        {
                            OriginalAttackStageEditing.Draw(data);
                            iterator.isExpanded = EditorGUILayout.Foldout(iterator.isExpanded, "Legacy fallback (actors without stage prefabs)", true);
                            if (iterator.isExpanded) EditorGUILayout.PropertyField(iterator, true);
                            continue;
                        }
                    }
                    if (iterator.name != "m_Script") EditorGUILayout.PropertyField(iterator, true);
                }
            }
            else
            {
                var field = data.FindProperty(group);
                var end = field.GetEndProperty();
                field.NextVisible(true);
                while (!SerializedProperty.EqualContents(field, end))
                {
                    EditorGUILayout.PropertyField(field, true);
                    if (!field.NextVisible(false)) break;
                }
            }
            Apply(data);
        }

        // Shared values are drawn once. Bindings, bounds and local fallback values are never copied over.
        public static void DrawComponent(SerializedObject local, params string[] additionallyHidden)
        {
            Banner(local);
            var consumer = local.targetObject as ISharedSettingsConsumer;
            if (consumer == null || !IsShared(consumer))
            {
                var hidden = new List<string>(additionallyHidden) { "m_Script", "useSharedSettings" };
                DrawLocal(local, hidden);
                return;
            }
            using (var shared = Tuning(consumer))
            {
                DrawGroup(shared, consumer.SharedSettingsGroup);
                var hidden = new List<string>(additionallyHidden) { "m_Script", "useSharedSettings" };
                var root = string.IsNullOrEmpty(consumer.SharedSettingsGroup) ? shared.GetIterator() : shared.FindProperty(consumer.SharedSettingsGroup);
                int depth = root.depth;
                if (root.NextVisible(true)) do
                {
                    if (!string.IsNullOrEmpty(consumer.SharedSettingsGroup) && root.depth <= depth) break;
                    hidden.Add(root.name);
                } while (root.NextVisible(false));
                EditorGUILayout.LabelField("Local scene / prefab setup", EditorStyles.boldLabel);
                DrawLocal(local, hidden);
            }
        }
        private static void DrawLocal(SerializedObject data, List<string> hidden)
        {
            data.Update();
            var iterator = data.GetIterator();
            bool enter = true;
            while (iterator.NextVisible(enter))
            {
                enter = false;
                if (!hidden.Contains(iterator.name)) EditorGUILayout.PropertyField(iterator, true);
            }
            Apply(data);
        }
        public static void Refresh()
        {
            OriginalAttackStageVfx.InvalidateContent();
            foreach (var core in Object.FindObjectsByType<AmplifierCoreGameplay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(core.gameObject)) core.ApplyTuning();
            foreach (var effect in Object.FindObjectsByType<AmplifierGoalTreatments>(FindObjectsInactive.Include, FindObjectsSortMode.None)) effect.RenderTreatment();
            foreach (var formation in Object.FindObjectsByType<ResonanceManifestation>(FindObjectsInactive.Include, FindObjectsSortMode.None)) formation.RefreshPreview();
            foreach (var plasma in Object.FindObjectsByType<PlayerMeleePlasma>(FindObjectsInactive.Include, FindObjectsSortMode.None)) plasma.RefreshSharedSettings();
            foreach (var tiers in Object.FindObjectsByType<EnergyTierVisualController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (Application.isPlaying && tiers.isActiveAndEnabled && tiers.HasActiveUnit) tiers.ApplyTier(tiers.ActiveUnit, true);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }

    public sealed class SharedSettingsWindow : EditorWindow
    {
        [SerializeField] private int tab;
        private Vector2 scroll;
        private UnityEditor.Editor nestedEditor;
        [MenuItem("MASSIVE/Shared Settings")]
        public static void Open() { Open(null); }
        [MenuItem("MASSIVE/Player/Shared Melee Settings")]
        private static void Melee() { SharedSettingsEditing.EnsureProfiles(); Open(SharedSettingsRuntime.Load<MeleeVisualProfile>()); }
        [MenuItem("MASSIVE/Resonance/Shared Formation & Timing")]
        private static void Resonance() { SharedSettingsEditing.EnsureProfiles(); Open(SharedSettingsRuntime.Load<ResonanceSharedProfile>()); }
        [MenuItem("MASSIVE/Text Animation/Shared Settings")]
        private static void Text() { SharedSettingsEditing.EnsureProfiles(); Open(SharedSettingsRuntime.Load<TextAnimationSharedProfile>()); }
        public static void Open(SharedSettingsProfile profile)
        {
            if (profile is PlayerTuningProfile) { PlayerTuningWindow.Open(); return; }
            var window = GetWindow<SharedSettingsWindow>("Shared Settings");
            if (profile is MeleeVisualProfile) window.tab = 0;
            if (profile is AmplifierSharedProfile) window.tab = 1;
            if (profile is ResonanceSharedProfile) window.tab = 2;
            if (profile is TextAnimationSharedProfile) window.tab = 3;
            window.minSize = new Vector2(480, 500); window.Show();
        }
        private void OnDisable() { if (nestedEditor) DestroyImmediate(nestedEditor); }
        private void OnGUI()
        {
            SharedSettingsEditing.EnsureProfiles();
            if (GUILayout.Button("Open Player Tuning — attack timeline, movement, size & melee")) PlayerTuningWindow.Open();
            if (GUILayout.Button("Open Power-up Settings — visuals, life cycle, spawning & toasts")) PowerUpSettingsWindow.Open();
            tab = GUILayout.Toolbar(tab, new[] { "Melee", "Amplifier", "Resonance", "Text" });
            SharedSettingsProfile[] profiles = { SharedSettingsRuntime.Load<MeleeVisualProfile>(), SharedSettingsRuntime.Load<AmplifierSharedProfile>(), SharedSettingsRuntime.Load<ResonanceSharedProfile>(), SharedSettingsRuntime.Load<TextAnimationSharedProfile>() };
            var profile = profiles[Mathf.Clamp(tab, 0, 3)];
            EditorGUILayout.HelpBox("Shared by every participating scene and included in builds. Changes save immediately, including in Play Mode. Components can opt out for local variations.", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (var data = new SerializedObject(profile)) SharedSettingsEditing.DrawGroup(data, "");
            if (profile is TextAnimationSharedProfile)
            {
                var text = (TextAnimationSharedProfile)profile;
                if (text.scoreDigitMorphPreset)
                {
                    EditorGUILayout.LabelField("Score animation preset — shared asset", EditorStyles.boldLabel);
                    UnityEditor.Editor.CreateCachedEditor(text.scoreDigitMorphPreset, null, ref nestedEditor);
                    EditorGUI.BeginChangeCheck();
                    nestedEditor.OnInspectorGUI();
                    if (EditorGUI.EndChangeCheck()) SharedSettingsEditing.Save(text.scoreDigitMorphPreset);
                }
                if (text.energyTierProfile && GUILayout.Button("Edit Shared Tier Styles / Promotion / Loop Presets")) Selection.activeObject = text.energyTierProfile;
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Select Shared Asset")) Selection.activeObject = profile;
        }
    }

    public abstract class SharedComponentEditor : UnityEditor.Editor
    { public override void OnInspectorGUI() { SharedSettingsEditing.DrawComponent(serializedObject); } }
    [CustomEditor(typeof(SharedSettingsProfile), true)]
    public sealed class SharedProfileEditor : UnityEditor.Editor
    { public override void OnInspectorGUI() { SharedSettingsEditing.DrawGroup(serializedObject, ""); } }
    [CustomEditor(typeof(AttackTrailGPU))]
    public sealed class SharedAttackTrailEditor : SharedComponentEditor
    {
        public override void OnInspectorGUI()
        {
            var gpu = (AttackTrailGPU)target;
            if (!gpu.IsStagePrefabEmitter) { base.OnInspectorGUI(); return; }
            EditorGUILayout.HelpBox("Stage prefab particle settings. These values belong to this prefab, independently of the other attack stages.", MessageType.Info);
            OriginalAttackStageEditing.DrawEmitter(serializedObject);
            var stage = gpu.GetComponentInParent<OriginalAttackStageVfx>();
            if (stage) OriginalAttackStageVfxEditor.DrawPreview(stage);
        }
    }
    [CustomEditor(typeof(PlayerRepulsorFeedback))] public sealed class SharedRepulsorBodyEditor : SharedComponentEditor {}
    [CustomEditor(typeof(PlayerRepulsorGridPulse))] public sealed class SharedRepulsorGridEditor : SharedComponentEditor {}
    [CustomEditor(typeof(AmplifierCoreVisual))] public sealed class SharedCoreSurfaceEditor : SharedComponentEditor {}
    [CustomEditor(typeof(AmplifierGoalCapture))] public sealed class SharedGoalForceEditor : SharedComponentEditor {}
    [CustomEditor(typeof(TeamAmplifierToastPresenter))] public sealed class SharedToastEditor : SharedComponentEditor {}
    [CustomEditor(typeof(ScoreboardManagerScript))] public sealed class SharedScoreboardEditor : SharedComponentEditor {}
    [CustomEditor(typeof(EnergyTierVisualController))] public sealed class SharedEnergyTierEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var consumer = (EnergyTierVisualController)target;
            SharedSettingsEditing.DrawComponent(serializedObject, SharedSettingsEditing.IsShared(consumer) ? "profile" : "m_Script");
        }
    }
}
