#if UNITY_EDITOR
using System;
using Massive.Enemies;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class AmplifiedDysonSetup
{
    public const string Folder = "Assets/Enemy System/Enemy Types/Melee/AmplifiedDysonSphere";
    public const string PrefabPath = Folder + "/Enemy_AmplifiedDysonSphere.prefab";
    public const string DefinitionPath = Folder + "/ED_AmplifiedDysonSphere.asset";
    public const string TimelinePath = Folder + "/Amplified Dyson Preview.asset";
    public const string FormationPath = Folder + "/Amplified Dyson Flanks.asset";
    public static readonly string[] PickupNames = { "Mass Nugglet", "Amplifier Nugglet", "Mass Nugget", "Amplifier Nugget" };

    [MenuItem("MASSIVE/Enemies/Amplified Dyson/Ensure Prototype Assets")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Enemy System/Enemy Types/Melee", "AmplifiedDysonSphere");
        var source = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DysonRepulsorSetup.DefinitionPath);
        if (!source || !source.prefab) throw new InvalidOperationException("The canonical Dyson is missing.");
        var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath);
        if (!definition)
        {
            definition = Object.Instantiate(source); definition.name = "ED_AmplifiedDysonSphere";
            definition.id = "AMPLIFIED_DYSON_SPHERE"; definition.healthMassEq = source.healthMassEq * 2f;
            definition.defeatRewardKey = ScoreRewardKeys.AmplifiedDysonDefeat;
            AssetDatabase.CreateAsset(definition, DefinitionPath);
        }
        var pickups = new EnemyLootPickup[4];
        for (int i = 0; i < 4; i++) pickups[i] = EnsurePickup(i);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!prefab)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(source.prefab, scene);
                root.name = "AMPLIFIED DYSON SPHERE";
                var controller = root.GetComponent<DysonSphereRepulsorController>(); controller.definition = definition;
                controller.sceneDirector = null; controller.arenaBounds = null;
                var panels = controller.panels;
                panels.shimmerEnabled = true;
                panels.energyOutlines = true;
                panels.outlineEnergySource = AssetDatabase.LoadAssetAtPath<Material>("Assets/Power-ups/Amplifier Core/Amplifier Core Energy.mat");
                var panelData = new SerializedObject(panels);
                var oldCore = panelData.FindProperty("core").objectReferenceValue as Transform;
                if (oldCore) oldCore.gameObject.SetActive(false);
                foreach (var core in root.GetComponentsInChildren<DysonRepulsorCore>(true)) core.gameObject.SetActive(false);
                var coreRoot = new GameObject("Amplifier Nugget core"); coreRoot.transform.SetParent(panels.transform, false);
                var coreVisual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Power-ups/Amplifier Nuggets/Amplifier Nugget Visual.prefab"), scene);
                coreVisual.transform.SetParent(coreRoot.transform, false); coreVisual.transform.localPosition = Vector3.zero;
                coreVisual.SetActive(true);
                foreach (var ps in coreVisual.GetComponentsInChildren<ParticleSystem>(true))
                { var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy; main.simulationSpace = ParticleSystemSimulationSpace.Local; }
                var animation = coreRoot.AddComponent<AmplifiedDysonCore>(); animation.controller = controller; animation.visual = coreVisual.transform;
                coreVisual.transform.localScale = Vector3.one * animation.coreScale;
                panelData.FindProperty("core").objectReferenceValue = coreRoot.transform;
                panelData.FindProperty("generateInEditMode").boolValue = false;
                panelData.ApplyModifiedPropertiesWithoutUndo();
                // Preserve independently editable fill styling; generated geometry supplies reverse faces.
                if (panels.panelFillMaterial)
                {
                    string path = Folder + "/Amplified Dyson Panel Fill.mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!material)
                    {
                        material = new Material(panels.panelFillMaterial) { name = "Amplified Dyson Panel Fill" };
                        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
                        AssetDatabase.CreateAsset(material, path);
                    }
                    panels.panelFillMaterial = material;
                }
                var loot = root.AddComponent<AmplifiedDysonLoot>(); loot.amplifierNugglet = pickups[1];
                loot.deathDrops = new[] {
                    new AmplifiedDysonLoot.Drop { prefab = pickups[0], weight = 40 },
                    new AmplifiedDysonLoot.Drop { prefab = pickups[1], weight = 30 },
                    new AmplifiedDysonLoot.Drop { prefab = pickups[2], weight = 20 },
                    new AmplifiedDysonLoot.Drop { prefab = pickups[3], weight = 10 }
                };
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            definition.prefab = prefab; EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
        }
        var formation = AssetDatabase.LoadAssetAtPath<EnemyFormation>(FormationPath);
        if (!formation)
        {
            formation = Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyFormation>("Assets/Enemy System/Encounters/Enemy Lab/04 Dyson Flanks.asset"));
            formation.name = "Amplified Dyson Flanks";
            foreach (var slot in formation.slots) slot.enemy = definition;
            AssetDatabase.CreateAsset(formation, FormationPath);
        }
        if (!AssetDatabase.LoadAssetAtPath<EnemyEncounterTimeline>(TimelinePath))
        {
            var timeline = ScriptableObject.CreateInstance<EnemyEncounterTimeline>();
            timeline.name = "Amplified Dyson Preview"; timeline.duration = 30; timeline.fitRegulation = false;
            timeline.maxAliveTotal = 4; timeline.maxPressure = 12; timeline.twoVTwo.enabled = false;
            timeline.pressureCosts.Add(new EnemyEncounterTimeline.PressureCost { enemy = definition, cost = 6 });
            timeline.cues.Add(new EnemyEncounterTimeline.Cue { label = "Amplified Dyson Flanks", formation = formation, arrivalSeconds = 3 });
            AssetDatabase.CreateAsset(timeline, TimelinePath);
        }
    }
    private static EnemyLootPickup EnsurePickup(int index)
    {
        string path = Folder + "/" + PickupNames[index] + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing.GetComponent<EnemyLootPickup>();
        string[] sourceNames = { "Metabolic Matter Nugglet", "Metabolic Amplifier Nugglet", "Metabolic Mass Nugget", "Metabolic Amplifier Nugget" };
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/Anomalies/EMERGENCE/" + sourceNames[index] + ".prefab");
        if (!source) throw new InvalidOperationException("Missing shared pickup: " + sourceNames[index]);
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            root.name = PickupNames[index];
            var old = root.GetComponent<MatterNuggetScript>(); string json = EditorJsonUtility.ToJson(old);
            Object.DestroyImmediate(old);
            var pickup = root.AddComponent<EnemyLootPickup>(); EditorJsonUtility.FromJsonOverwrite(json, pickup);
            pickup.lifetimeSeconds = 12; root.SetActive(false);
            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<EnemyLootPickup>();
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    [MenuItem("MASSIVE/Enemies/Amplified Dyson/Select Enemy and Loot Settings")]
    public static void SelectEnemy() { Install(); Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); }
    [MenuItem("MASSIVE/Enemies/Amplified Dyson/Select Panel Wave Settings")]
    public static void SelectPanels() { Install(); Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponentInChildren<DysonSpherePanels>(true); }
    [MenuItem("MASSIVE/Enemies/Amplified Dyson/Select Rainbow Outline Settings")]
    public static void SelectEnergyOutlines() { SelectPanels(); }
    [MenuItem("MASSIVE/Enemies/Amplified Dyson/Open Encounter Preview")]
    public static void OpenPreview() { Install(); EnemyEncounterComposer.Show(AssetDatabase.LoadAssetAtPath<EnemyEncounterTimeline>(TimelinePath)); }
}
#endif
