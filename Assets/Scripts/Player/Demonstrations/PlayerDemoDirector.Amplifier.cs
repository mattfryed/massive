using System.Collections;
using Massive.Multiplier;
using Massive.Player;
using Massive.Scoring;
using System.Linq;
using UnityEngine;

namespace Massive.Demonstrations
{
    public sealed partial class PlayerDemoDirector
    {
        public AmplifierCoreGameplay AmplifierCore => amplifierCore;
        public int ConfirmedGoals { get; private set; }
        public int DemonstrationTeamMultiplier { get; private set; } = 1;
        private AmplifierCoreGameplay amplifierCore;
        private AmplifierGoalCapture amplifierGoal;
        private AmplifierGoalTreatments amplifierTreatments;
        private TeamAmplifierPresenter amplifierPresenter;
        public AmplifierGoalCapture AmplifierGoal => amplifierGoal;
        public AmplifierGoalTreatments AmplifierTreatments => amplifierTreatments;
        public TeamAmplifierPresenter AmplifierPresenter => amplifierPresenter;

        private void PrepareAmplifier()
        {
            if (!scenario.amplifierCorePrefab || !scenario.scoreEconomy || !scenario.teamGoalPrefab)
            { Fail("Amplifier demo needs the canonical Core, team goal and score economy."); return; }
            var chain = primary.GetComponent<PlayerScoreChain>();
            if (!chain) chain = primary.gameObject.AddComponent<PlayerScoreChain>();
            chain.Configure(scenario.scoreEconomy.ChainSettings);
            while (chain.CurrentMultiplier < 16d && chain.AdvanceToNextStep()) { }
            if (System.Math.Abs(chain.CurrentMultiplier - 16d) > .001d)
            { Fail("The score economy cannot demonstrate the requested 16x multiplier."); return; }

            amplifierCore = Instantiate(scenario.amplifierCorePrefab, session.transform);
            amplifierCore.transform.localPosition = Vector3.left * 3.3f;
            amplifierCore.ConfigureDemonstration(session.transform, scenario.amplifierRespawnDelay);
            foreach (var interactor in amplifierCore.GetComponentsInChildren<GridInteractor>(true)) interactor.grid = demoGrid;

            var goal = Instantiate(scenario.teamGoalPrefab, session.transform);
            goal.name = "Demonstration team goal";
            amplifierGoal = goal.GetComponentInChildren<AmplifierGoalCapture>(true);
            goal.transform.position += transform.position + Vector3.left * 11.5f - amplifierGoal.CapturePoint.position;
            foreach (var interactor in goal.GetComponentsInChildren<GridInteractor>(true)) interactor.grid = demoGrid;
            foreach (var legacy in goal.GetComponentsInChildren<ScoreSphereScript>(true)) legacy.enabled = false;
            var surface = goal.GetComponentInChildren<ScoreVoidMetaballsVisual>(true);
            surface.ConfigureDemonstration(amplifierGoal.TeamID, .5f);
            amplifierGoal.ConfigureDemonstration(session.transform, () =>
            {
                ConfirmedGoals++;
                DemonstrationTeamMultiplier = scenario.scoreEconomy.TeamAmplifierSettings.GetMultiplier(1);
                Phase = "Amplifier goal";
                return true;
            });
            amplifierTreatments = demoGrid.GetComponent<AmplifierGoalTreatments>();
            if (!amplifierTreatments) amplifierTreatments = demoGrid.gameObject.AddComponent<AmplifierGoalTreatments>();
            amplifierTreatments.Configure(demoGrid, new[] { amplifierGoal }, null, null);
            amplifierTreatments.ConfigureDemonstration(team => team == amplifierGoal.TeamID ?
                Mathf.RoundToInt(Mathf.Log(DemonstrationTeamMultiplier, 2)) : 0);
            amplifierTreatments.ShowControls = false; amplifierTreatments.enabled = true;
            var number = new GameObject("Team Amplifier number"); number.transform.SetParent(goal.transform, false);
            amplifierPresenter = number.AddComponent<TeamAmplifierPresenter>();
            // The normal HUD supplies this mesh/material through its authored Core child.
            // A standalone demo presenter uses the same canonical prefab as its visual source.
            var badgeSource = scenario.amplifierCorePrefab.GetComponentsInChildren<MeshRenderer>(true)
                .FirstOrDefault(r => r.sharedMaterial && r.sharedMaterial.shader.name == "MASSIVE/Amplifier Core/Energy");
            if (!badgeSource) { Fail("Canonical Amplifier Core is missing its energy badge visual."); return; }
            amplifierPresenter.ConfigureDemonstration(amplifierGoal.TeamID, surface, () => DemonstrationTeamMultiplier, badgeSource);
        }

        private void FrameAmplifierGrid()
        {
            if (!presentationCamera || !demoGrid || !amplifierGoal) return;
            float left = amplifierGoal.CapturePoint.position.x;
            float right = presentationCamera.transform.position.x + presentationCamera.orthographicSize * presentationCamera.aspect;
            var position = demoGrid.transform.position; position.x = (left + right) * .5f;
            demoGrid.transform.position = position;
            demoGrid.size = new Vector2(right - left, demoGrid.size.y);
        }

        private IEnumerator AmplifierDemo()
        {
            if (!amplifierCore) { Fail("Amplifier Core failed to spawn."); yield break; }
            yield return Until(() => !amplifierCore.IsCaptured && !amplifierCore.IsOccupied && !amplifierCore.IsLaunched,
                "Amplifier did not complete its normal spawn/respawn.", 15f);
            if (failed) yield break;
            DemonstrationTeamMultiplier = 1;
            Phase = "Entering Amplifier";
            yield return Ready(primary); if (failed) yield break;
            float entryReach = primary.attackController.Profile.GetStage(0).TravelDistance * PlayerScaleAdjuster.ActionReachOf(primary);
            float bodyRadius = PlayerScaleAdjuster.BodyRadiusOf(primary);
            if (Vector3.Distance(primary.transform.position, amplifierCore.transform.position) > entryReach + bodyRadius)
            {
                // Approach with ordinary movement when shown at native gameplay size.
                yield return MoveActorTo(primary, amplifierCore.transform.position + Vector3.right * (entryReach * .8f + bodyRadius), Vector3.left);
                if (failed) yield break;
            }
            yield return Attack(primary, Vector3.left);
            yield return Until(() => primary.IsInsideAmplifier, "Thrust did not enter the Amplifier Core.");
            if (failed) yield break;
            yield return Until(() => !primary.IsAmplifierTransitioning, "Amplifier entry did not finish.");
            if (failed) yield break;
            SetFrame(primary, Vector2.zero, Vector3.left);
            yield return new WaitForSeconds(.45f);
            int before = ConfirmedGoals;
            Phase = "Charging Amplifier";
            yield return Attack(primary, Vector3.left, amplifierCore.Possession.fullChargeSeconds * .65f);
            yield return Until(() => amplifierCore.IsLaunched || ConfirmedGoals > before, "Amplifier shot was not released.");
            if (failed) yield break;
            yield return Until(() => ConfirmedGoals > before, "Amplifier shot did not score in its goal.");
            if (failed) yield break;
            yield return Until(() => !primary.IsInsideAmplifier, "Player did not finish leaving the Amplifier.");
            if (failed) yield break;
            yield return new WaitForSeconds(scenario.readablePause);
            yield return Until(() => !amplifierCore.IsCaptured, "Captured Amplifier did not respawn.", 15f);
        }
    }
}
