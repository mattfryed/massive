#if UNITY_EDITOR
using Massive.Enemies;
using Massive.PowerUps;
using UnityEditor;
using UnityEngine;

/// <summary>One-time migration. Copies only the turret's authored effect children.</summary>
[InitializeOnLoad]
public static class ParticleAcceleratorBeamSetup
{
    public const string Folder = "Assets/Power-ups/Particle Accelerator";
    public const string PrefabPath = Folder + "/ParticleAcceleratorSustainedBeam.prefab";
    public const string DefinitionPath = Folder + "/PU_ParticleAccelerator.asset";
    static ParticleAcceleratorBeamSetup() { EditorApplication.delayCall += EnsureInstalled; }

    [MenuItem("MASSIVE/Power-ups/Install Sustained Particle Accelerator")]
    public static void EnsureInstalled()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var definition = AssetDatabase.LoadAssetAtPath<ParticleAcceleratorPowerUpDefinition>(DefinitionPath);
        if (!definition || definition.sustainedBeamPrefab) return;
        var prefab = AssetDatabase.LoadAssetAtPath<ParticleAcceleratorBeam>(PrefabPath);
        if (!prefab)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ParticleBeamTurretSetup.PrefabPath).GetComponent<ParticleBeamTurretController>();
            var root = new GameObject("ParticleAcceleratorSustainedBeam");
            try
            {
                var rig = root.AddComponent<ParticleAcceleratorBeam>();
                rig.beamVisual = Copy(source.beamVisual, root.transform, "Plasma beam");
                rig.corePlasma = Copy(source.corePlasma, root.transform, "Emitter plasma");
                rig.contactPlasma = Copy(source.contactPlasma, root.transform, "Impact plasma");
                rig.corePlasmaForwardOffset = source.corePlasmaForwardOffset;
                rig.beamVisual.gameObject.SetActive(false);
                rig.corePlasma.gameObject.SetActive(true); rig.contactPlasma.gameObject.SetActive(true);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath).GetComponent<ParticleAcceleratorBeam>();
            }
            finally { Object.DestroyImmediate(root); }
        }
        definition.sustainedBeamPrefab = prefab;
        EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
        Debug.Log("Particle Accelerator now uses the turret plasma beam. Tune it in MASSIVE > Power-up Settings > Particle Accelerator.");
    }

    static T Copy<T>(T source, Transform parent, string name) where T : Component
    {
        var go = Object.Instantiate(source.gameObject, parent); go.name = name;
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        return go.GetComponent<T>();
    }
}
#endif
