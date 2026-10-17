using System.Collections;
using Massive.Enemies;
using Massive.Scoring;
using UnityEngine;

namespace Massive.Demonstrations
{
    public sealed partial class PlayerDemoDirector
    {
        public int ConfirmedBodyPickups { get; private set; }
        public string LastCollectedPickup { get; private set; }
        public int ConfirmedPickupToasts { get; private set; }
        public string LastPickupToast { get; private set; }
        private bool IsPickupCircle => scenario && scenario.kind == PlayerDemoKind.PowerUp &&
            scenario.powerUp is Massive.PowerUps.TimeDilationPowerUpDefinition &&
            scenario.circlePickups != null && scenario.circlePickups.Length > 0;

        private Vector3 PickupCirclePoint(float angle) => transform.position +
            new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * scenario.pickupCircleRadius;

        private IEnumerator PickupCircleDemo()
        {
            yield return MoveTo(primaryHome);
            if (failed) yield break;
            var body = primary.GetComponent<Rigidbody>();
            for (int index = 0; index < scenario.circlePickups.Length; index++)
            {
                var prefab = scenario.circlePickups[index];
                if (!prefab) { Fail("Pickup circle contains an unassigned prefab."); yield break; }
                float start = Mathf.PI - index * 2f * Mathf.PI / scenario.circlePickups.Length;
                float end = Mathf.PI - (index + 1) * 2f * Mathf.PI / scenario.circlePickups.Length;
                Vector3 destination = PickupCirclePoint(end);
                var pickup = Instantiate(prefab, destination, prefab.transform.rotation, session.transform);
                pickup.ConfigureDemonstration(primary);
                foreach (var interactor in pickup.GetComponentsInChildren<GridInteractor>(true)) interactor.grid = demoGrid;
                bool collected = false;
                pickup.Collected += actor =>
                {
                    if (actor != primary) return;
                    collected = true; ConfirmedBodyPickups++; LastCollectedPickup = prefab.name;
                    var toast = EnemyScoreToast.ShowPickup(pickup.rewardToastPrefab, PickupDemonstrationAward(pickup),
                        pickup.LastMassRestored, actor.massScoreMax - actor.massScoreMin, pickup.transform.position,
                        gameObject.scene, pickup.LastMultiplierGained, presentationCamera, session.transform);
                    if (toast) { ConfirmedPickupToasts++; LastPickupToast = toast.label.text; }
                };
                // Enemy loot prefabs are stored inactive for their normal spawn/pool path.
                pickup.gameObject.SetActive(true);
                Phase = "Collecting " + prefab.name;
                // Keep the previous movement input during this short lead-in. The native
                // spawn/collection grace period completes while the player approaches.
                if (scenario.pickupSpawnPause > 0f) yield return new WaitForSeconds(scenario.pickupSpawnPause);
                float began = Time.time;
                while (Time.time - began < scenario.pickupCircleLegSeconds)
                {
                    float t = Mathf.Clamp01((Time.time - began + .15f) / scenario.pickupCircleLegSeconds);
                    Vector3 target = PickupCirclePoint(Mathf.Lerp(start, end, t));
                    Vector3 delta = target - body.position;
                    SetFrame(primary, Vector2.ClampMagnitude(new Vector2(delta.x, delta.z) * 2f, 1f), delta);
                    yield return null;
                }
                // Flow straight into the next arc after confirmed contact. Only brake at
                // the loop boundary, or steer closer if gameplay tuning changes the path.
                if (!collected || index == scenario.circlePickups.Length - 1) yield return MoveTo(destination);
                if (failed) yield break;
                if (!collected) { Fail("Player did not collect " + prefab.name + "."); yield break; }
            }
        }

        private ScoreAwardResult PickupDemonstrationAward(EnemyLootPickup pickup)
        {
            // A preview receipt uses the live economy and score math without touching
            // match totals/telemetry. This demonstration's team amplifier is x1.
            if (!scenario.scoreEconomy.TryGetReward(pickup.scoreRewardKey, out var rule) ||
                !rule.enabled || rule.bonusOnly || rule.BaseMilliElectronVolts <= 0) return default;
            var chain = primary.GetComponent<PlayerScoreChain>();
            double multiplier = rule.multiplierEligible && !rule.ignoreAllMultipliers && chain
                ? System.Math.Max(1d, chain.CurrentMultiplier) : 1d;
            var award = new ScoreAwardResult
            {
                accepted = true, rewardKey = rule.key, quantity = 1, rewardScale = 1,
                teamID = primary.teamID, playerID = primary.playerID, worldPosition = pickup.transform.position,
                baseMilliElectronVolts = rule.BaseMilliElectronVolts, multiplier = multiplier,
                teamAmplifierMultiplier = 1, combinedMultiplier = multiplier,
                finalMilliElectronVolts = EnergyScoreMath.SaturatingScale(rule.BaseMilliElectronVolts, multiplier)
            };
            chain?.ApplyAward(rule.chainEffect, Mathf.Max(0f, rule.chainCharge));
            return award;
        }
    }
}
