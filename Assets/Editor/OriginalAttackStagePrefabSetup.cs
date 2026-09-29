#if UNITY_EDITOR
using System.IO;
using Massive.Player;
using Massive.Settings;
using UnityEditor;
using UnityEngine;

namespace Massive.EditorTools
{
    public static class OriginalAttackStagePrefabSetup
    {
        public const string Folder = "Assets/Prefabs/Player/Attack Stages/Original Particles";
        public const string ProfilePath = "Assets/Scripts/Settings/Resources/MeleeVisualProfile.asset";
        static readonly string[] Names = { "Thrust", "Sweep", "Repulsor" };
        [MenuItem("MASSIVE/Player/Create Missing Original Particle Stage Prefabs")]
        public static void Install()
        {
            EnsureFolder(Folder);
            var profile = AssetDatabase.LoadAssetAtPath<MeleeVisualProfile>(ProfilePath);
            var actor = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab");
            var source = actor.GetComponent<AttackTrailGPU>();
            var prefabs = new OriginalAttackStageVfx[3];
            for (int i = 0; i < 3; i++)
            {
                string path = Folder + "/" + Names[i] + ".prefab";
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing) { prefabs[i] = existing.GetComponent<OriginalAttackStageVfx>(); continue; }
                var root = new GameObject(Names[i]); root.SetActive(false);
                try
                {
                    var effect = root.AddComponent<OriginalAttackStageVfx>();
                    effect.stage = (AttackStageType)i; effect.startAtActivation = i == 2;
                    if (i != 2)
                    {
                        var emitter = new GameObject("GPU Particle Trail"); emitter.transform.SetParent(root.transform, false);
                        var gpu = emitter.AddComponent<AttackTrailGPU>();
                        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(profile.legacyParticles), gpu);
                        gpu.sim = source.sim; gpu.trailMat = source.trailMat; gpu.quadMesh = source.quadMesh;
                        gpu.ConfigureAsStageEmitter();
                    }
                    var extra = new GameObject(i == 2 ? "Optional Repulsor Particles" : "Additional Particle Layer");
                    extra.transform.SetParent(root.transform, false); extra.SetActive(false);
                    var ps = extra.AddComponent<ParticleSystem>();
                    var main = ps.main; main.loop = false; main.playOnAwake = false;
                    main.duration = .4f; main.startLifetime = .25f; main.startSpeed = i == 2 ? 3f : 1f;
                    main.startSize = .06f; main.startColor = Color.white; main.maxParticles = 128;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    var emission = ps.emission; emission.rateOverTime = 0;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)32) });
                    var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = i == 2 ? .45f : .12f; shape.radiusThickness = .05f;
                    shape.rotation = new Vector3(90, 0, 0);
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
                    root.SetActive(true);
                    var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabs[i] = saved.GetComponent<OriginalAttackStageVfx>();
                }
                finally { Object.DestroyImmediate(root); }
            }
            Undo.RecordObject(profile, "Assign Original Particles stage prefabs");
            if (!profile.originalThrustPrefab) profile.originalThrustPrefab = prefabs[0];
            if (!profile.originalSweepPrefab) profile.originalSweepPrefab = prefabs[1];
            if (!profile.originalRepulsorPrefab) profile.originalRepulsorPrefab = prefabs[2];
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            OriginalAttackStageVfx.InvalidateContent();
            Debug.Log("Original Particles stage prefabs installed: " + Folder);
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
