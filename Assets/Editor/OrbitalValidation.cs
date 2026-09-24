#if UNITY_EDITOR
using System;
using System.Linq;
using Massive.Orbital;
using UnityEditor;
using UnityEngine;

public static class OrbitalValidation
{
    public static string LastReport { get; private set; } = "Not run";

    [MenuItem("MASSIVE/ORBITAL/Validate probability and scene")]
    public static void Run()
    {
        int checks = 0;
        Action<bool, string> check = (condition, label) =>
        { if (!condition) throw new InvalidOperationException("[ORBITAL] " + label); checks++; };
        check(OrbitalDensity.Evaluate(Vector3.zero) == 0f && OrbitalDensity.Evaluate(Vector3.one) == 0f, "Nucleus and outside radius have zero density.");
        check(Mathf.Abs(OrbitalDensity.Evaluate(Vector3.forward * .43f) - 1f) < .001f, "Dense polar lobe reaches normalized peak.");
        var random = new System.Random(44);
        int clockwise = 0, dense = 0;
        double sampleDensity = 0, uniformDensity = 0;
        var sampler = new OrbitalDensity.FaceSampler();
        for (int i = 0; i < 20000; i++)
        {
            var p = new Vector3((float)random.NextDouble() * 2f - 1f, 0f, (float)random.NextDouble() * 2f - 1f);
            float d = OrbitalDensity.Evaluate(p);
            check(!float.IsNaN(d) && d >= 0f && d <= 1f, "Finite bounded density");
            check(Mathf.Abs(d - OrbitalDensity.Evaluate(-p)) < .0001f, "Full cross section has inversion symmetry");
            uniformDensity += d;
            var sampled = sampler.Sample(random);
            sampleDensity += OrbitalDensity.Evaluate(sampled);
            if (OrbitalDensity.Evaluate(sampled) > .2f) dense++;
            var direction = OrbitalDensity.SampleDirection(Vector3.forward, random);
            check(Mathf.Abs(direction.y) < .0001f && Mathf.Abs(direction.magnitude - 1f) < .0001f, "Impulse remains planar and normalized");
            if (direction.x > 0f) clockwise++;
        }
        check(sampleDensity > uniformDensity * 2.5, "Visible particle samples concentrate in dense regions.");
        check(clockwise > 9600 && clockwise < 10400, "Independent orbit handedness is unbiased.");
        float once = OrbitalDensity.StrikeProbability(.4f, 2.4f, 1f);
        float split = 1f - Mathf.Pow(1f - OrbitalDensity.StrikeProbability(.4f, 2.4f, .02f), 50f);
        check(Mathf.Abs(once - split) < .00001f, "One-second encounter probability is timestep invariant.");
        check(OrbitalDensity.StrikeProbability(0f, 2.4f, 1f) == 0f && OrbitalDensity.StrikeProbability(1f, 0f, 1f) == 0f, "Empty density and disabled rate cannot strike.");
        check(Mathf.Abs(OrbitalDensity.Evaluate(Vector3.forward * .43f, .25f) - 1f) < .001f &&
            OrbitalDensity.Evaluate(Vector3.forward * .77f, .25f) == OrbitalDensity.Evaluate(Vector3.forward * .77f),
            "Extended tail preserves the peak and inner density.");
        check(OrbitalDensity.Evaluate(Vector3.forward * 1.1f, .25f) > 0f &&
            OrbitalDensity.Evaluate(Vector3.forward * 1.1f, .25f) < .1f &&
            OrbitalDensity.Evaluate(Vector3.forward * 1.25f, .25f) == 0f, "Sparse tail extends smoothly to its new boundary.");
        var extendedSampler = new OrbitalDensity.FaceSampler(false, .25f);
        int outsideOldEdge = 0;
        for (int i = 0; i < 5000; i++) if (extendedSampler.Sample(random).magnitude > 1f) outsideOldEdge++;
        check(outsideOldEdge > 50, "Visible samples reach the extended tail.");

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        check(scene.path == OrbitalLevelSetup.ScenePath, "Open ORBITAL for scene checks.");
        var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        check(components.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "No missing scripts in ORBITAL.");
        var hazard = UnityEngine.Object.FindFirstObjectByType<OrbitalProbabilityCloud>();
        check(hazard && hazard.cloud && hazard.impacts && hazard.nuggetPrefab && hazard.scoreService && hazard.anomalyManager, "Hazard references wired.");
        check(hazard.cloud.outerTailExtension == .25f && hazard.DensityAt(hazard.transform.position + Vector3.forward * (hazard.cloud.radius * 1.1f)) > 0f,
            "Gameplay hazard shares the extended visible probability field.");
        check(hazard.nuggetPrefab.name == "Mass Nugglet" && Mathf.Abs(hazard.nuggetPrefab.rewardMultiplier - .1f) < .0001f,
            "Electron strikes spawn nugglets with one tenth reward.");
        check((hazard.nuggetPrefab.transform.localScale - Vector3.one / 3f).sqrMagnitude < .00001f &&
            hazard.nuggetPrefab.GetComponentsInChildren<ParticleSystem>(true).All(p => p.main.scalingMode == ParticleSystemScalingMode.Hierarchy),
            "Nugglet particles inherit one third visual scale.");
        check(hazard.cloud.VisibleParticleCount == hazard.cloud.particleCount, "Particle pool renders in Edit Mode.");
        check(!ShaderUtil.ShaderHasError(hazard.cloud.particleMaterial.shader) && !ShaderUtil.ShaderHasError(hazard.impacts.lineMaterial.shader), "Both shaders compiled.");
        check(components.SelectMany(t => t.GetComponents<AudioListener>()).Count(l => l.enabled && l.gameObject.activeInHierarchy) == 1, "Exactly one scene audio listener.");
        var definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(OrbitalLevelSetup.DefinitionPath);
        check(definition && definition.SceneName == scene.name && definition.iconPrefab && definition.instructionsPanelPrefab, "Menu and instructions resolve ORBITAL.");
        check(AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Scripts/CORE/LevelCatalog.asset").Levels.Count(l => l == definition) == 1, "One catalog entry.");
        check(EditorBuildSettings.scenes.Count(s => s.path == scene.path && s.enabled) == 1, "One enabled build scene.");
        LastReport = checks + " assertions passed (20,000 density, sampler and direction trials; scene, shaders, menu, references).";
        Debug.Log("[ORBITAL] " + LastReport);
    }

    [MenuItem("MASSIVE/ORBITAL/Run live interaction checks")]
    public static void RunLive()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode in ORBITAL first.");
        if (UnityEngine.Object.FindFirstObjectByType<OrbitalPlayValidation>()) throw new InvalidOperationException("Checks already running.");
        new GameObject("ORBITAL interaction checks (temporary)").AddComponent<OrbitalPlayValidation>();
    }

    [MenuItem("MASSIVE/ORBITAL/Run live polish checks")]
    public static void RunPolish()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode in ORBITAL first.");
        if (UnityEngine.Object.FindFirstObjectByType<OrbitalPolishValidation>()) throw new InvalidOperationException("Checks already running.");
        new GameObject("ORBITAL polish checks (temporary)").AddComponent<OrbitalPolishValidation>();
    }
}
#endif
