#if UNITY_EDITOR
using System;
using System.Linq;
using Massive.Multiplier;
using Massive.Orbital;
using UnityEditor;
using UnityEngine;

public static class OrbitalFormationValidation
{
    public static string LastReport { get; private set; } = "Not run";
    [MenuItem("MASSIVE/ORBITAL/Validate formation cycle and amplifier wiring")]
    public static void Run()
    {
        int checks = 0;
        Action<bool, string> check = (pass, label) => { if (!pass) throw new InvalidOperationException(label); checks++; };
        var cycle = new OrbitalFormationCycle(); cycle.Reset(OrbitalFormation.Original);
        cycle.Advance(14.5f, 15f, 2f);
        check(cycle.From == OrbitalFormation.Original && cycle.To == cycle.From, "First formation holds before 15 seconds");
        cycle.Advance(.5f, 15f, 2f);
        check(cycle.From == OrbitalFormation.Original && cycle.To == OrbitalFormation.Shells300 && cycle.Blend == 0f, "First transition starts at 15 seconds");
        cycle.Advance(1f, 15f, 2f);
        check(Mathf.Abs(cycle.Blend - .5f) < .00001f, "Halfway transition has equal density weights");
        cycle.Advance(0f, 15f, 2f);
        check(Mathf.Abs(cycle.Blend - .5f) < .00001f, "Paused clock retains formation blend");
        cycle.Advance(1f, 15f, 2f);
        check(cycle.From == OrbitalFormation.Shells300 && cycle.To == cycle.From, "Transition settles on target");
        for (int i = 2; i <= 6; i++)
        {
            cycle.Advance(15f, 15f, 2f);
            check((int)cycle.From == i % 6 && cycle.From == cycle.To && cycle.TransitionsStarted == i, "Six-state sequence and wrap " + i);
        }
        cycle.Reset(OrbitalFormation.Lobes420); cycle.Advance(5.25f, 5f, 1f);
        check(cycle.From == OrbitalFormation.Lobes420 && cycle.To == OrbitalFormation.Lobes421 && cycle.Blend > 0f, "Inspector timing and starting formation honored");
        cycle.Reset(OrbitalFormation.Original); cycle.Advance(92f, 15f, 2f);
        check(cycle.From == OrbitalFormation.Original && cycle.To == cycle.From && cycle.TransitionsStarted == 6, "Large timestep preserves full-cycle order");

        var random = new System.Random(414);
        for (int state = 0; state < OrbitalFormationCycle.Count; state++)
        {
            var formation = (OrbitalFormation)state;
            var sampler = new OrbitalDensity.FaceSampler(false, .25f, formation);
            double sampled = 0, uniform = 0;
            for (int i = 0; i < 1000; i++)
            {
                Vector3 p = new Vector3((float)random.NextDouble() * 2.5f - 1.25f, 0f, (float)random.NextDouble() * 2.5f - 1.25f);
                float d = OrbitalDensity.Evaluate(p, .25f, formation);
                check(!float.IsNaN(d) && d >= 0f && d <= 1f && Mathf.Abs(d - OrbitalDensity.Evaluate(-p, .25f, formation)) < .00001f, "Bounded symmetric field " + formation);
                uniform += d; sampled += OrbitalDensity.Evaluate(sampler.Sample(random), .25f, formation);
            }
            check(sampled > uniform * 1.8 && sampler.TotalWeight > 0f, "Samples concentrate in visible lobes " + formation);
            check(OrbitalDensity.Evaluate(Vector3.forward * 1.25f, .25f, formation) == 0f, "Finite cloud support " + formation);
        }
        check(Mathf.Abs(OrbitalDensity.Evaluate(Vector3.right * .62f, 0, OrbitalFormation.Shells300) - OrbitalDensity.Evaluate(Vector3.forward * .62f, 0, OrbitalFormation.Shells300)) < .00001f, "300 shell is circular");
        check(OrbitalDensity.Evaluate(Vector3.forward * .65f, 0, OrbitalFormation.Lobes322) == 0f && OrbitalDensity.Evaluate(Vector3.right * .65f, 0, OrbitalFormation.Lobes322) > .9f, "322 has horizontal lobes and polar node");
        check(OrbitalDensity.Evaluate(Vector3.right * ((5f - Mathf.Sqrt(5f)) / 18f), 0, OrbitalFormation.Lobes411) < .001f && OrbitalDensity.Evaluate(Vector3.right * ((5f + Mathf.Sqrt(5f)) / 18f), 0, OrbitalFormation.Lobes411) < .001f, "411 retains both radial nodes");
        check(OrbitalDensity.Evaluate(Vector3.forward * .4f, 0, OrbitalFormation.Lobes420) < .001f, "420 retains inner/outer radial separation");
        check(OrbitalDensity.Evaluate(Vector3.right * .7f, 0, OrbitalFormation.Lobes421) == 0f && OrbitalDensity.Evaluate(new Vector3(1, 0, 1).normalized * .707f, 0, OrbitalFormation.Lobes421) > .95f, "421 has diagonal lobes and equatorial node");

        var h = UnityEngine.Object.FindFirstObjectByType<OrbitalProbabilityCloud>();
        var s = UnityEngine.Object.FindFirstObjectByType<AmplifierResonanceSpawner>();
        check(h && h.cloud.cycleFormations && h.cloud.formationIntervalSeconds == 15f, "Gameplay cloud cycles every 15 seconds");
        check(s && s.scoreService == h.scoreService && s.spawnRegion && s.spawnRegion.arenaBounds && s.patternAnchor && s.corePrefab && s.patternOrder.Count > 0, "Amplifier cycle has ORBITAL scene references and source assets");
        check(s.corePrefab.name == "Amplifier Core - Spawn Cycle" && !s.corePrefab.IsPresentationOnly && s.initialSpawnDelay == 6f && s.respawnDelay == 8f && s.respawnDelayVariation == new Vector2(-2, 2), "DYNAMO spawn configuration retained");
        check(UnityEngine.Object.FindObjectsByType<AmplifierGoalCapture>(FindObjectsSortMode.None).Select(g => g.TeamID).OrderBy(t => t).SequenceEqual(new[] { 1, 2 }), "Both team scoring goals present");
        check(UnityEngine.Object.FindObjectsByType<AmplifierCoreGameplay>(FindObjectsSortMode.None).All(c => c.IsPresentationOnly), "No competing standalone gameplay core");
        LastReport = checks + " formation/timing/sampling/wiring assertions passed";
        Debug.Log("[ORBITAL formations] " + LastReport);
    }

    [MenuItem("MASSIVE/ORBITAL/Run formation and amplifier overlap checks")]
    public static void RunLive()
    {
        if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != OrbitalLevelSetup.ScenePath)
            throw new InvalidOperationException("Start a fresh ORBITAL Play session first.");
        if (UnityEngine.Object.FindFirstObjectByType<OrbitalFormationPlayValidation>()) throw new InvalidOperationException("Checks already running");
        new GameObject("ORBITAL formation + amplifier checks (temporary)").AddComponent<OrbitalFormationPlayValidation>();
    }
}
#endif
