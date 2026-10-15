using System.Collections;
using System.Linq;
using Massive.Multiplier;
using Massive.Player;
using Massive.Scoring;
using TMPro;
using UnityEngine;

namespace Massive.Demonstrations
{
    public sealed partial class PlayerDemoDirector
    {
        [Header("Amplifier arena presentation")]
        [SerializeField] private Transform partnerJoystick;
        [SerializeField] private GameObject partnerAttackButton, partnerShieldButton;
        [SerializeField] private TMP_Text amplifierSequenceLabel;
        private Vector3 partnerJoystickHome;
        private float partnerAttackCueUntil, partnerShieldCueUntil;
        private AmplifierGoalCapture opposingAmplifierGoal;
        public int ConfirmedContests { get; private set; }
        public int ConfirmedContestWins { get; private set; }
        public int DarkTeamMultiplier { get; private set; } = 1;
        public AmplifierGoalCapture OpposingAmplifierGoal => opposingAmplifierGoal;
        private bool IsAmplifierArena => scenario && scenario.kind == PlayerDemoKind.AmplifierGoal && scenario.demonstrateAmplifierContest;

        private void CacheAmplifierControls()
        { if (partnerJoystick) partnerJoystickHome = partnerJoystick.localPosition; }

        private void ResetAmplifierControls()
        {
            partnerAttackCueUntil = partnerShieldCueUntil = 0f;
            if (partnerAttackButton) partnerAttackButton.SetActive(false);
            if (partnerShieldButton) partnerShieldButton.SetActive(false);
            if (partnerJoystick) partnerJoystick.localPosition = partnerJoystickHome;
        }

        private void OnAmplifierPartnerInput(PlayerInputFrame input)
        {
            if (input.attackDown || input.attackHeld) partnerAttackCueUntil = Time.time + .12f;
            if (input.shieldDown || input.shieldHeld) partnerShieldCueUntil = Time.time + .12f;
        }

        private void UpdateAmplifierControls()
        {
            if (!IsAmplifierArena) return;
            if (partnerAttackButton) partnerAttackButton.SetActive(Time.time < partnerAttackCueUntil);
            if (partnerShieldButton) partnerShieldButton.SetActive(Time.time < partnerShieldCueUntil);
            if (partnerJoystick && partner)
            {
                var direction = partner.IsInsideAmplifier ? partner.AimDirectionWS :
                    new Vector3(partner.moveHorizontal, 0f, partner.moveVertical);
                partnerJoystick.localPosition = partnerJoystickHome + direction * .25f;
            }
        }

        private void AmplifierCaption(string text)
        { if (amplifierSequenceLabel) amplifierSequenceLabel.text = text; }

        private void PrepareAmplifierArena()
        {
            if (!scenario.amplifierCorePrefab || !scenario.teamGoalPrefab || !scenario.opposingTeamGoalPrefab ||
                !scenario.scoreEconomy || !demoGrid)
            { Fail("Amplifier arena needs both canonical goals, Core, economy and grid."); return; }

            primaryHome = transform.position + Vector3.right * 2.7f;
            primary.transform.position = primaryHome;
            partnerHome = transform.position + new Vector3(-2.7f, 0f, -1.65f);
            partner = CreateActor(2, 2, partnerHome - transform.position);
            foreach (var actor in new[] { primary, partner })
            {
                var chain = actor.GetComponent<PlayerScoreChain>();
                if (!chain) chain = actor.gameObject.AddComponent<PlayerScoreChain>();
                chain.Configure(scenario.scoreEconomy.ChainSettings);
                while (chain.CurrentMultiplier < 16d && chain.AdvanceToNextStep()) { }
            }

            amplifierCore = Instantiate(scenario.amplifierCorePrefab, session.transform);
            amplifierCore.transform.localPosition = Vector3.zero;
            amplifierCore.ConfigureDemonstration(session.transform, scenario.amplifierRespawnDelay);
            foreach (var interactor in amplifierCore.GetComponentsInChildren<GridInteractor>(true)) interactor.grid = demoGrid;
            var badge = scenario.amplifierCorePrefab.GetComponentsInChildren<MeshRenderer>(true)
                .FirstOrDefault(r => r.sharedMaterial && r.sharedMaterial.shader.name == "MASSIVE/Amplifier Core/Energy");
            if (!badge) { Fail("Amplifier Core is missing its energy badge visual."); return; }
            amplifierGoal = CreateArenaGoal(scenario.teamGoalPrefab, 1, badge);
            opposingAmplifierGoal = CreateArenaGoal(scenario.opposingTeamGoalPrefab, 2, badge);
            if (failed) return;
            amplifierTreatments = demoGrid.GetComponent<AmplifierGoalTreatments>();
            if (!amplifierTreatments) amplifierTreatments = demoGrid.gameObject.AddComponent<AmplifierGoalTreatments>();
            amplifierTreatments.Configure(demoGrid, new[] { amplifierGoal, opposingAmplifierGoal }, null, null);
            amplifierTreatments.ConfigureDemonstration(team => Mathf.RoundToInt(Mathf.Log(
                team == 1 ? DemonstrationTeamMultiplier : DarkTeamMultiplier, 2)));
            amplifierTreatments.ShowControls = false;
            amplifierTreatments.enabled = true;

            CreateAmplifierArenaWalls();
            AmplifierCaption("UNCONTESTED / CLAIM, CHARGE, SCORE");
        }

        private AmplifierGoalCapture CreateArenaGoal(GameObject prefab, int team, Renderer badge)
        {
            var goal = Instantiate(prefab, session.transform);
            goal.name = team == 1 ? "Light team goal" : "Dark team goal";
            var capture = goal.GetComponentInChildren<AmplifierGoalCapture>(true);
            if (!capture || capture.TeamID != team)
            { Fail("Amplifier goal prefab has the wrong team."); return null; }
            goal.transform.position += transform.position + Vector3.right *
                (scenario.amplifierArenaSize.x * .5f * (team == 1 ? -1f : 1f)) - capture.CapturePoint.position;
            foreach (var ring in goal.GetComponentsInChildren<Shapes.Disc>(true))
                ring.Radius = scenario.amplifierArenaSize.y * .48f;
            foreach (var interactor in goal.GetComponentsInChildren<GridInteractor>(true)) interactor.grid = demoGrid;
            foreach (var legacy in goal.GetComponentsInChildren<ScoreSphereScript>(true)) legacy.enabled = false;
            var surface = goal.GetComponentInChildren<ScoreVoidMetaballsVisual>(true);
            surface.ConfigureDemonstration(team, .5f);
            capture.ConfigureDemonstration(session.transform, () =>
            {
                ConfirmedGoals++;
                int multiplier = scenario.scoreEconomy.TeamAmplifierSettings.GetMultiplier(1);
                if (team == 1) DemonstrationTeamMultiplier = multiplier; else DarkTeamMultiplier = multiplier;
                Phase = team == 1 ? "Light goal scored" : "Dark goal scored";
                AmplifierCaption(team == 1 ? "LIGHT GOAL / TEAM MULTIPLIER INCREASED" : "DARK GOAL / TEAM MULTIPLIER INCREASED");
                return true;
            });
            var number = new GameObject("Team Amplifier number"); number.transform.SetParent(goal.transform, false);
            var presenter = number.AddComponent<TeamAmplifierPresenter>();
            presenter.ConfigureDemonstration(team, surface, () => team == 1 ? DemonstrationTeamMultiplier : DarkTeamMultiplier, badge, 2f);
            if (team == 1) amplifierPresenter = presenter;
            return capture;
        }

        private void FrameAmplifierArena()
        {
            FrameView(Vector3.zero, scenario.amplifierArenaSize.x + 2f, scenario.amplifierArenaSize.y + .5f);
            if (demoGrid) demoGrid.size = scenario.amplifierArenaSize;
        }

        private void CreateAmplifierArenaWalls()
        {
            var half = scenario.amplifierArenaSize * .5f;
            for (int edge = 0; edge < 4; edge++)
            {
                bool vertical = edge < 2;
                float sign = edge % 2 == 0 ? -1f : 1f;
                var wall = new GameObject("Arena boundary " + edge);
                wall.transform.SetParent(session.transform, false);
                wall.transform.localPosition = vertical ? Vector3.right * sign * (half.x + .08f) : Vector3.forward * sign * (half.y + .08f);
                wall.AddComponent<BoxCollider>().size = vertical ? new Vector3(.16f, 3f, half.y * 2f) : new Vector3(half.x * 2f, 3f, .16f);
            }
        }

        private IEnumerator AmplifierArenaDemo()
        {
            DemonstrationTeamMultiplier = DarkTeamMultiplier = 1;
            yield return AmplifierAvailable();
            if (failed) yield break;
            AmplifierCaption("UNCONTESTED / CLAIM, CHARGE, SCORE");
            Phase = "Uncontested entry";
            yield return Ready(primary);
            yield return MoveActorTo(primary, transform.position + Vector3.right * AmplifierEntryGap(primary), Vector3.left);
            if (failed) yield break;
            yield return Attack(primary, Vector3.left);
            yield return Until(() => amplifierCore.Occupant == primary && !primary.IsAmplifierTransitioning,
                "Light player did not enter the Amplifier.");
            if (failed) yield break;
            yield return new WaitForSeconds(.45f);
            yield return ShootAmplifier(primary, amplifierGoal);
            if (failed) yield break;

            // Approach from opposite vertical sides. The losing player exits clear of the goal lane.
            AmplifierCaption("BOTH PLAYERS / THRUST INTO THE CORE");
            Phase = "Preparing simultaneous entry";
            yield return Ready(primary); yield return Ready(partner);
            float gap = AmplifierEntryGap(primary);
            yield return MoveActorTo(primary, transform.position + new Vector3(2.7f, 0f, -gap), Vector3.forward);
            yield return MoveActorTo(primary, transform.position + Vector3.back * gap, Vector3.forward);
            yield return MoveActorTo(partner, transform.position + new Vector3(-2.7f, 0f, gap), Vector3.back);
            yield return MoveActorTo(partner, transform.position + Vector3.forward * gap, Vector3.back);
            if (failed) yield break;
            yield return AmplifierAvailable();
            if (failed) yield break;
            yield return new WaitForSeconds(.4f);
            Phase = "Simultaneous entry";
            SetFrame(primary, Vector2.zero, Vector3.forward, down: true, held: true);
            SetFrame(partner, Vector2.zero, Vector3.back, down: true, held: true);
            yield return null;
            SetFrame(primary, Vector2.zero, Vector3.forward, up: true);
            SetFrame(partner, Vector2.zero, Vector3.back, up: true);
            yield return null;
            SetFrame(primary, Vector2.zero, Vector3.forward);
            SetFrame(partner, Vector2.zero, Vector3.back);
            yield return Until(() => amplifierCore.ControlState == AmplifierControlState.Contested &&
                !primary.IsAmplifierTransitioning && !partner.IsAmplifierTransitioning, "Both players did not establish a Core contest.");
            if (failed) yield break;
            ConfirmedContests++;
            Phase = "Contested / alternating buttons";
            AmplifierCaption("CONTESTED / ALTERNATE ATTACK AND SHIELD");
            yield return new WaitForSeconds(.3f);
            float fast = Mathf.Max(.24f, 1f / amplifierCore.Possession.pressesPerSecond + .05f);
            float lightNext = Time.time, darkNext = Time.time;
            bool lightAttack = true, darkAttack = true;
            float deadline = Time.time + amplifierCore.Possession.contestSeconds + 1f;
            while (amplifierCore.ControlState == AmplifierControlState.Contested && Time.time < deadline)
            {
                bool lightPress = Time.time >= lightNext, darkPress = Time.time >= darkNext;
                SetFrame(primary, Vector2.zero, Vector3.left, down: lightPress && lightAttack, held: lightPress && lightAttack,
                    shield: lightPress && !lightAttack);
                SetFrame(partner, Vector2.zero, Vector3.right, down: darkPress && darkAttack, held: darkPress && darkAttack,
                    shield: darkPress && !darkAttack);
                if (lightPress) { lightNext = Time.time + fast * 1.75f; lightAttack = !lightAttack; }
                if (darkPress) { darkNext = Time.time + fast; darkAttack = !darkAttack; }
                yield return null;
            }
            SetFrame(primary, Vector2.zero, Vector3.left);
            SetFrame(partner, Vector2.zero, Vector3.right);
            if (!amplifierCore.IsContestWinnerAiming || amplifierCore.Occupant != partner)
            { Fail("The alternating-input contest did not resolve to the dark player."); yield break; }
            ConfirmedContestWins++;
            Phase = "Dark wins / loser ejected";
            AmplifierCaption("DARK WINS / AIM, CHARGE, SCORE");
            yield return Until(() => !amplifierCore.PostContestInputProtected, "Contest winner input protection did not clear.");
            if (failed) yield break;
            yield return ShootAmplifier(partner, opposingAmplifierGoal);
            if (failed) yield break;
            yield return AmplifierAvailable();
        }

        private float AmplifierEntryGap(PlayerControllerScript actor)
        {
            float radius = amplifierCore.GetComponent<Collider>().bounds.extents.x;
            float travel = actor.attackController.Profile.GetStage(0).TravelDistance * PlayerScaleAdjuster.ActionReachOf(actor);
            return radius + PlayerScaleAdjuster.BodyRadiusOf(actor) + travel * .4f;
        }

        private IEnumerator AmplifierAvailable()
        {
            yield return Until(() => amplifierCore && !amplifierCore.IsCaptured && !amplifierCore.IsOccupied && !amplifierCore.IsLaunched,
                "Amplifier did not complete its normal spawn/respawn.", 15f);
        }

        private IEnumerator ShootAmplifier(PlayerControllerScript actor, AmplifierGoalCapture goal)
        {
            Vector3 direction = goal.CapturePoint.position - amplifierCore.transform.position;
            direction.y = 0f; direction.Normalize();
            int before = ConfirmedGoals;
            Phase = actor == primary ? "Light charging" : "Dark charging";
            yield return Attack(actor, direction, amplifierCore.Possession.fullChargeSeconds * .65f);
            yield return Until(() => amplifierCore.IsLaunched || ConfirmedGoals > before, "Amplifier shot was not released.");
            if (failed) yield break;
            yield return Until(() => ConfirmedGoals > before, "Amplifier shot did not score in its goal.");
            if (failed) yield break;
            yield return Until(() => !actor.IsInsideAmplifier, "Player did not finish leaving the Amplifier.");
            if (failed) yield break;
            yield return new WaitForSeconds(scenario.readablePause);
        }
    }
}
