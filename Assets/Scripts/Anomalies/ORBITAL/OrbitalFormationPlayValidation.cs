#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Massive.Multiplier;
using Massive.Scoring;
using UnityEngine;

namespace Massive.Orbital
{
    /// <summary>Fresh-match, opt-in integration checks. Never saved to the scene or included in builds.</summary>
    public sealed class OrbitalFormationPlayValidation : MonoBehaviour
    {
        public static string LastReport = "Not run";
        private readonly List<string> results = new List<string>();
        private OrbitalProbabilityCloud h;
        private AmplifierResonanceSpawner spawner;
        private float interval, transition, rate, timeScale;
        private bool restored;
        private void Check(bool pass, string label) { results.Add((pass ? "PASS " : "FAIL ") + label); }
        private int CoreCount() => AmplifierCoreGameplay.ActiveCores.Count(c => c && !c.IsPresentationOnly && c.gameObject.activeInHierarchy);

        private IEnumerator Start()
        {
            LastReport = "Running";
            yield return null; yield return null;
            h = FindFirstObjectByType<OrbitalProbabilityCloud>();
            spawner = FindFirstObjectByType<AmplifierResonanceSpawner>();
            if (!h || !spawner) { LastReport = "FAIL ORBITAL cloud or amplifier coordinator missing"; Destroy(gameObject); yield break; }
            interval = h.cloud.formationIntervalSeconds; transition = h.cloud.formationTransitionSeconds;
            rate = h.peakStrikesPerSecond; timeScale = Time.timeScale;
            try
            {
                float deadline = Time.realtimeSinceStartup + 20f;
                while (!h.IsHazardActive && Time.realtimeSinceStartup < deadline) yield return null;
                Check(h.IsHazardActive, "Normal match flow opens the cloud and scoring");
                h.cloud.formationIntervalSeconds = 2f; h.cloud.formationTransitionSeconds = .6f;
                h.cloud.Rebuild();
                var seen = new HashSet<OrbitalFormation>();
                float began = Time.time; deadline = Time.realtimeSinceStartup + 20f;
                bool sawForming = false;
                while (seen.Count < 6 && Time.realtimeSinceStartup < deadline)
                {
                    if (spawner.Phase == AmplifierEncounterPhase.FormingPattern)
                    {
                        sawForming = true;
                        if (spawner.ActiveCore || spawner.ActivePattern.InteractionEnabled) Check(false, "Pattern must settle before core/collision activation");
                    }
                    var cloud = h.cloud;
                    if (Time.time - began > 1.4f && cloud.CurrentFormation == cloud.NextFormation && seen.Add(cloud.CurrentFormation))
                    {
                        Check(cloud.VisibleParticleCount == cloud.particleCount, "Formation " + cloud.CurrentFormation + " retains the full particle pool");
                        ScreenCapture.CaptureScreenshot("Assets/Screenshots/ORBITAL/ORBITAL-formation-" + (int)cloud.CurrentFormation + ".png");
                    }
                    yield return null;
                }
                Check(seen.Count == 6, "All six formations render in the requested cycle");
                Check(sawForming, "DYNAMO pattern formation runs alongside the cycling cloud");
                deadline = Time.realtimeSinceStartup + 25f;
                while ((spawner.Phase != AmplifierEncounterPhase.Active || !spawner.ActiveCore || spawner.ActiveCore.IsSpawning) && Time.realtimeSinceStartup < deadline) yield return null;
                Check(spawner.Phase == AmplifierEncounterPhase.Active && spawner.ActiveCore && CoreCount() == 1, "Coordinator creates exactly one gameplay core after safe placement");
                if (!spawner.ActiveCore) { LastReport = string.Join("\n", results) + "\n" + spawner.Status; yield break; }

                deadline = Time.realtimeSinceStartup + 4f;
                while (h.cloud.FormationBlend < .1f && Time.realtimeSinceStartup < deadline) yield return null;
                float blend = h.cloud.FormationBlend;
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(.2f);
                Check(h.cloud.FormationBlend == blend && !h.IsHazardActive, "Pause freezes formation interpolation and electron strikes");
                Time.timeScale = timeScale;
                h.scoreService.SetScoringState(true, false);
                blend = h.cloud.FormationBlend; int pausedTransitions = h.cloud.TransitionsStarted;
                yield return new WaitForSeconds(.15f);
                Check(h.cloud.FormationBlend == blend && h.cloud.TransitionsStarted == pausedTransitions, "Paused match clock freezes the cloud cycle");
                h.scoreService.OpenScoring();

                var core = spawner.ActiveCore;
                Vector3 contact = Vector3.zero; float densest = -1f;
                for (int x = -8; x <= 8; x++) for (int z = -8; z <= 8; z++)
                {
                    Vector3 p = new Vector3(x * .45f, 0f, z * .45f);
                    float density = h.DensityAt(p);
                    if (density > densest) { densest = density; contact = p; }
                }
                int strikes = h.CoreStrikes;
                core.Body.position = contact; core.Body.linearVelocity = Vector3.zero;
                Physics.SyncTransforms(); h.peakStrikesPerSecond = 1000f;
                for (int i = 0; i < 50 && h.CoreStrikes == strikes; i++) yield return new WaitForFixedUpdate();
                h.peakStrikesPerSecond = rate;
                Check(h.CoreStrikes > strikes && core.Body.linearVelocity.sqrMagnitude > .01f, "Managed amplifier core receives electron impulse during a formation transition");
                Check(spawner.ActivePattern && spawner.ActivePattern.InteractionEnabled && spawner.Phase == AmplifierEncounterPhase.Active, "Electron impact preserves the active resonance/core encounter");
                ScreenCapture.CaptureScreenshot("Assets/Screenshots/ORBITAL/ORBITAL-amplifier-overlap.png");

                var service = h.scoreService;
                var goal = FindObjectsByType<AmplifierGoalCapture>(FindObjectsSortMode.None).First(g => g.TeamID == 1);
                int previousTier = service.GetTeamAmplifierTierIndex(1);
                int previousCaptures = spawner.CapturesObserved;
                core.Body.position = goal.CapturePoint.position; core.Body.linearVelocity = Vector3.zero;
                Physics.SyncTransforms();
                deadline = Time.realtimeSinceStartup + 3f;
                while (!core.HasBeenCaptured && Time.realtimeSinceStartup < deadline) yield return new WaitForFixedUpdate();
                Check(core.HasBeenCaptured && service.GetTeamAmplifierTierIndex(1) == previousTier + 1, "Real goal deposit advances the team's amplifier tier exactly once");
                Check(spawner.CapturesObserved == previousCaptures + 1 && spawner.Phase == AmplifierEncounterPhase.Dissolving && !spawner.ActivePattern.InteractionEnabled, "Capture retires the paired pattern through the existing coordinator");
                Check(!core.TryCapture(goal) && service.GetTeamAmplifierTierIndex(1) == previousTier + 1, "Duplicate core capture cannot advance the amplifier twice");
                ScoreAwardResult award;
                bool accepted = service.TryAwardToTeam(ScoreRewardKeys.EnergyPickupSmall, 1, null, "orbital-overlap-" + GetInstanceID(), goal.transform.position, out award);
                Check(accepted && award.teamAmplifierMultiplier == service.GetTeamAmplifierMultiplier(1) && award.finalMilliElectronVolts == award.baseMilliElectronVolts * award.teamAmplifierMultiplier,
                    "Existing score awards use the captured team's amplifier multiplier");
                var presenter = FindObjectsByType<TeamAmplifierPresenter>(FindObjectsSortMode.None).First(p => p.TeamID == 1);
                var presenterData = new UnityEditor.SerializedObject(presenter);
                var text = presenterData.FindProperty("multiplierText").objectReferenceValue as TMPro.TMP_Text;
                Check(text && text.text == "x" + service.GetTeamAmplifierMultiplier(1), "Amplifier HUD receives the scoring event");

                int pairs = spawner.PairsSpawned, cycles = h.cloud.TransitionsStarted;
                bool sawDelay = false; deadline = Time.realtimeSinceStartup + 30f;
                while (spawner.PairsSpawned == pairs && Time.realtimeSinceStartup < deadline)
                { sawDelay |= spawner.Phase == AmplifierEncounterPhase.Delay; yield return null; }
                Check(sawDelay && spawner.LastRespawnDelay >= 6f && spawner.LastRespawnDelay <= 10f && spawner.PairsSpawned == pairs + 1,
                    "Capture completes absorption and the configured 6–10 second respawn delay");
                Check(CoreCount() == 1 && spawner.ActiveCore != core && h.cloud.TransitionsStarted > cycles,
                    "One replacement core spawns while the cloud continues cycling independently");
            }
            finally { Restore(); }
            LastReport = string.Join("\n", results); Debug.Log("[ORBITAL formation overlap]\n" + LastReport); Destroy(gameObject);
        }
        private void Restore()
        {
            if (restored || !h) return; restored = true;
            Time.timeScale = timeScale; h.peakStrikesPerSecond = rate;
            h.cloud.formationIntervalSeconds = interval; h.cloud.formationTransitionSeconds = transition;
            h.cloud.Rebuild();
            if (h.scoreService) h.scoreService.OpenScoring();
        }
        private void OnDestroy() { Restore(); }
    }
}
#endif
