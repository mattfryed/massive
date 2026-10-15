using System.Collections;
using Massive.Enemies;
using Massive.Player;
using UnityEngine;

namespace Massive.Demonstrations
{
    public sealed partial class PlayerDemoDirector
    {
        private bool IsDroneCombat => scenario && (scenario.kind == PlayerDemoKind.DroneThrust ||
            scenario.kind == PlayerDemoKind.DroneCombo || scenario.kind == PlayerDemoKind.DroneChargedThrust);

        private float ThrustTravel => primary.attackController.Profile.GetStage(0).TravelDistance *
            PlayerScaleAdjuster.ActionReachOf(primary);

        private void FrameDroneCombat()
        {
            float travel = ThrustTravel;
            float radius = PlayerScaleAdjuster.BodyRadiusOf(primary);
            float height = radius * 4f + 1f;
            if (scenario.kind == PlayerDemoKind.DroneChargedThrust)
                travel *= primary.attackController.Effective_thrustChargeMaxDistanceMultiplier;
            else if (scenario.kind == PlayerDemoKind.DroneCombo)
            {
                var profile = primary.attackController.Profile;
                travel += profile.GetStage(1).TravelDistance * PlayerScaleAdjuster.ActionReachOf(primary);
                float repulsor = profile.GetStage(2).GetRepulsorRadius(PlayerScaleAdjuster.SizeOf(primary), radius);
                height = repulsor * 2f + 2f;
                travel += repulsor;
            }
            FrameView(Vector3.right * (travel * .5f), travel + radius * 4f + 2f, height);
        }

        private IEnumerator DroneCombatDemo()
        {
            if (!scenario.droneDefinition || !scenario.droneDefinition.prefab)
            { Fail("Missing canonical Drone definition."); yield break; }
            yield return Ready(primary);
            if (failed) yield break;
            int before = ConfirmedKills;
            int expected = scenario.kind == PlayerDemoKind.DroneThrust ? 1 : 3;
            float thrust = ThrustTravel;
            Phase = "Preparing targets";
            if (scenario.kind == PlayerDemoKind.DroneChargedThrust)
            {
                float reach = thrust * primary.attackController.Effective_thrustChargeMaxDistanceMultiplier;
                for (int i = 0; i < 3; i++) SpawnCombatDrone(Vector3.right * reach * (.3f + i * .22f));
            }
            else
            {
                SpawnCombatDrone(Vector3.right * thrust * .7f);
                if (scenario.kind == PlayerDemoKind.DroneCombo)
                {
                    var profile = primary.attackController.Profile;
                    float sweep = profile.GetStage(1).TravelDistance * PlayerScaleAdjuster.ActionReachOf(primary);
                    float radius = profile.GetStage(2).GetRepulsorRadius(PlayerScaleAdjuster.SizeOf(primary), PlayerScaleAdjuster.BodyRadiusOf(primary));
                    SpawnCombatDrone(new Vector3(thrust + sweep * .6f, 0f, PlayerScaleAdjuster.BodyRadiusOf(primary) * 1.5f));
                    SpawnCombatDrone(new Vector3(thrust + sweep, 0f, radius * .72f));
                }
            }
            // Let ordinary Drone formation finish before showing the input.
            yield return new WaitForSeconds(.9f);
            if (scenario.kind == PlayerDemoKind.DroneCombo)
                yield return ComboDemo();
            else
            {
                bool charged = scenario.kind == PlayerDemoKind.DroneChargedThrust;
                Phase = charged ? "Hold Attack to charge" : "Tap Attack";
                // Release just before the ordinary full-charge auto-fire threshold.
                yield return Attack(primary, Vector3.right, charged ? primary.attackController.Effective_thrustChargeMaxSeconds * .98f : 0f);
                yield return Until(() => !primary.attackController.IsAttacking && !primary.attackController.IsCharging,
                    "Drone thrust did not finish.");
            }
            if (failed) yield break;
            yield return Until(() => ConfirmedKills - before == expected, "Drone attack did not defeat every target.");
            if (!failed) { Phase = "Targets defeated"; LastOutcome = expected + " drone" + (expected == 1 ? "" : "s") + " defeated"; }
        }

        private void SpawnCombatDrone(Vector3 offset) => SpawnCombatEnemy(scenario.droneDefinition, offset);

        private EnemyBase SpawnCombatEnemy(EnemyDefinition definition, Vector3 offset)
        {
            var staging = new GameObject("Enemy staging");
            staging.transform.SetParent(session.transform, false);
            staging.SetActive(false);
            var go = Instantiate(definition.prefab, primaryHome + offset,
                Quaternion.LookRotation(Vector3.left), staging.transform);
            var enemy = go.GetComponent<EnemyBase>();
            enemy.ConfigureDemonstration(session.transform, primary);
            enemy.Init(definition, null);
            enemy.AttacksEnabled = false;
            enemy.HoldPosition = true;
            // Stationary targets use ordinary hurtboxes, health and death presentation.
            // Disable steering so live-arena bounds cannot pull this remote stage to the origin.
            var steering = go.GetComponent<DroneController>();
            if (steering) steering.enabled = false;
            var body = go.GetComponent<Rigidbody>();
            if (body) body.isKinematic = true;
            enemy.Defeated += defeat => { if (defeat.creditedPlayer == primary) ConfirmedKills++; };
            go.transform.SetParent(session.transform, true);
            Destroy(staging);
            return enemy;
        }
    }
}
