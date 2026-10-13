using System;
using System.Linq;
using System.Reflection;
using Massive.Enemies;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.Emergence.Editor
{
    public static class EmergencePickupValidation
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Arm(MatterNuggetScript pickup, Vector3 position)
        {
            pickup.Eject(position, Vector3.zero, 30);
            typeof(MatterNuggetScript).GetField("age", Hidden).SetValue(pickup, 1f);
            Physics.SyncTransforms();
        }
        static void Collect(MatterNuggetScript pickup, Collider body) =>
            typeof(MatterNuggetScript).GetMethod("TryCollect", Hidden).Invoke(pickup, new object[] { body });

        // Reuses the shared pickup suite's disposable scene, real player and local physics world.
        public static void Run(MatchScoreService scores, PlayerControllerScript player, GameObject scope, Scene scene, Action<bool, string> check)
        {
            var weights = new EmergenceDropWeights();
            int[] counts = new int[4];
            for (int i = 0; i < 10000; i++) counts[(int)weights.Select((i + .5) / 10000)]++;
            check(counts.SequenceEqual(new[] { 4000, 4000, 1000, 1000 }), "EMERGENCE defaults divide probability into 40 / 40 / 10 / 10 percent");
            weights.matterNugglet = weights.amplifierNugglet = 4; weights.matterNugget = weights.amplifierNugget = 1;
            counts = new int[4];
            for (int i = 0; i < 10000; i++) counts[(int)weights.Select((i + .5) / 10000)]++;
            check(counts.SequenceEqual(new[] { 4000, 4000, 1000, 1000 }), "Weights normalize without requiring a total of 100");
            for (int kind = 0; kind < 4; kind++)
            {
                weights = new EmergenceDropWeights { matterNugglet = kind == 0 ? 1 : 0, amplifierNugglet = kind == 1 ? 1 : 0,
                    matterNugget = kind == 2 ? 1 : 0, amplifierNugget = kind == 3 ? 1 : 0 };
                check((int)weights.Select(0) == kind && (int)weights.Select(.9999999999999999) == kind, "Zero weights exclude other types: " + (EmergenceDropType)kind);
            }
            weights.amplifierNugget = 0;
            check(!weights.IsValid, "All-zero drop weights are rejected");
            weights.matterNugglet = float.PositiveInfinity;
            check(!weights.IsValid, "Non-finite drop weights are rejected");
            weights.matterNugglet = -1;
            check(!weights.IsValid, "Negative drop weights are rejected");

            var body = player.GetComponent<SphereCollider>();
            var chain = player.GetComponent<PlayerScoreChain>();
            scores.Profile.TryGetReward(ScoreRewardKeys.MassNugget, out var matterRule);
            matterRule.ignoreAllMultipliers = false; matterRule.chainCharge = 0;
            string[] names = { "Metabolic Matter Nugglet", "Metabolic Amplifier Nugglet", "Metabolic Mass Nugget", "Metabolic Amplifier Nugget" };
            var prefabs = names.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(EmergenceLevelSetup.Folder + "/" + n + ".prefab").GetComponent<EmergenceMassNugget>()).ToArray();
            float smallMass = 0;
            for (int kind = 0; kind < 4; kind++)
            {
                bool small = kind < 2, amplifier = kind == 1 || kind == 3;
                var pickup = Object.Instantiate(prefabs[kind], scope.transform);
                try
                {
                    scores.ResetForMatch(true); player.massScore = .25f;
                    Vector3 originalScale = pickup.transform.localScale;
                    pickup.BeginAttachedPresentation(body.bounds.center); pickup.UpdateAttachedPresentation(body.bounds.center, .5f);
                    Physics.SyncTransforms(); scene.GetPhysicsScene().Simulate(.02f); Collect(pickup, body);
                    check(!pickup.IsDespawning && player.massScore == .25f && chain.CurrentMultiplier == 1 && scores.GetTeamScore(player.teamID) == 0 &&
                        pickup.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), names[kind] + " cannot collide, collect or reward while budding");
                    check(pickup.GetComponent<Rigidbody>().isKinematic && pickup.transform.localScale == originalScale * .5f &&
                        pickup.GetComponentsInChildren<ParticleSystemRenderer>(true).All(r => r.sharedMaterial.shader.name == "MASSIVE/Emergence/Opaque Reward Bud"), names[kind] + " bud scales its whole silhouette with opaque unlit materials");
                    pickup.ReleaseAttachedPresentation(body.bounds.center, Vector3.zero, 30);
                    check(!pickup.IsAttachedPresentation && pickup.VisualScale == 1 && pickup.transform.localScale == originalScale && !pickup.GetComponent<Rigidbody>().isKinematic,
                        names[kind] + " detaches at full size without restarting the spawn animation");
                    Physics.SyncTransforms(); scene.GetPhysicsScene().Simulate(.02f);
                    double gain = amplifier ? small ? .1 : 1 : 0;
                    long energy = amplifier ? 0 : small ? 10 : 100;
                    check(pickup.IsDespawning && Math.Abs(pickup.LastMultiplierGained - gain) < .00001 &&
                        scores.GetTeamScore(player.teamID) == energy, names[kind] + " grants exactly its intended reward through a real body contact");
                    check(pickup.LastMassRestored > 0, names[kind] + " restores mass");
                    if (kind == 0) smallMass = pickup.LastMassRestored;
                    check(Mathf.Abs(pickup.LastMassRestored - smallMass * (small ? 1 : 10)) < .00001f, names[kind] + " retains the 10:1 mass ratio");
                    check(Mathf.Abs(pickup.transform.localScale.x - (small ? 1f / 3 : 1)) < .00001f &&
                        (!small || pickup.GetComponentsInChildren<ParticleSystem>(true).All(ps => ps.main.scalingMode == ParticleSystemScalingMode.Hierarchy)), names[kind] + " has the intended size and scalable visuals");
                    Collect(pickup, body);
                    check(Math.Abs(chain.CurrentMultiplier - (1 + gain)) < .00001 && scores.GetTeamScore(player.teamID) == energy,
                        names[kind] + " rejects duplicate collection");
                    if (amplifier)
                    {
                        check(Object.FindObjectsByType<EnemyScoreToast>(FindObjectsSortMode.None).Any(t => t.label.text.Contains("× MULTIPLIER") && t.MassRestored > 0), "Amplifier receipt includes actual multiplier gain and restored mass");
                        player.massScore = player.massScoreMax;
                        Arm(pickup, Vector3.forward * 20); Collect(pickup, body);
                        check(pickup.LastMassRestored == 0 && Math.Abs(pickup.LastMultiplierGained - gain) < .00001,
                            "Full mass still accepts amplifier growth; pooled reuse grants a fresh reward");
                        scores.SetScoringState(true, false); Arm(pickup, Vector3.forward * 20); Collect(pickup, body);
                        check(pickup.LastMultiplierGained == 0, "Paused match clock blocks amplifier gain");
                        scores.SetScoringState(true, true); scores.SetBonusScoringOnly(true); Arm(pickup, Vector3.forward * 20); Collect(pickup, body);
                        check(pickup.LastMultiplierGained == 0, "Bonus phase blocks ordinary amplifier gain");
                        scores.SetBonusScoringOnly(false); scores.CloseScoring(); Arm(pickup, Vector3.forward * 20); Collect(pickup, body);
                        check(pickup.LastMultiplierGained == 0, "Closed match blocks amplifier gain");
                        scores.OpenScoring(); chain.AddMultiplier(1000);
                        Arm(pickup, Vector3.forward * 20); player.massScore = .25f; Collect(pickup, body);
                        check(pickup.LastMultiplierGained == 0 && pickup.LastMassRestored > 0 && chain.CurrentMultiplier == chain.MaximumMultiplier,
                            "At the multiplier cap amplifier pickups still restore mass without claiming a false gain");
                    }
                }
                finally { Object.DestroyImmediate(pickup.gameObject); }
            }
            scores.ResetForMatch(true);
            chain.AddMultiplier(.9); double received = chain.AddMultiplier(1);
            check(Math.Abs(received - 1) < .00001 && Math.Abs(chain.CurrentMultiplier - 2.9) < .00001, "A literal +1 multiplier crosses charge intervals without changing its value");
            chain.AddMultiplier(12.05); received = chain.AddMultiplier(1);
            check(Math.Abs(chain.CurrentMultiplier - 15.95) < .00001 && Math.Abs(received - 1) < .00001, "A literal +1 remains +1 near the cap");
            received = chain.AddMultiplier(.1);
            check(Math.Abs(received - .05) < .00001 && chain.CurrentMultiplier == 16, "The final partial gain clamps to the configured cap");
            chain.ResetChain(); chain.AddMultiplier(.1); chain.ApplyAward(ScoreChainAwardMode.AdvanceAndRefresh, .25f);
            check(Math.Abs(chain.CurrentMultiplier - 1.1625) < .00001, "Ordinary score charge continues correctly after direct multiplier growth");
            check(chain.AddMultiplier(double.NaN) == 0 && chain.AddMultiplier(double.PositiveInfinity) == 0 && chain.AddMultiplier(-1) == 0,
                "Invalid direct multiplier gains are ignored");
            var settings = ScoreChainSettings.RecommendedDefaults(); settings.multiplierSteps = new[] { 1, 3, 5 }; settings.chargeRequiredPerTier = new[] { 7f, 2f };
            chain.Configure(settings); chain.AddMultiplier(2.5);
            check(Math.Abs(chain.CurrentMultiplier - 3.5) < .00001 && Math.Abs(chain.Charge - .5) < .00001, "Direct growth honors a custom multiplier curve");
            chain.AddMultiplier(5); check(chain.CurrentMultiplier == 5, "Direct growth honors a custom cap");
            scores.ResetForMatch(true);

            var root = new GameObject("Weighted pickup pool fixture"); root.SetActive(false); root.transform.SetParent(scope.transform);
            try
            {
                var field = root.AddComponent<EmergenceField>();
                field.matterNuggletPrefab = prefabs[0]; field.amplifierNuggletPrefab = prefabs[1]; field.nuggetPrefab = prefabs[2]; field.amplifierNuggetPrefab = prefabs[3];
                var live = new EmergenceMassNugget[4];
                typeof(EmergenceField).GetField("nuggets", Hidden).SetValue(field, live);
                typeof(EmergenceField).GetField("pickupPool", Hidden).SetValue(field, new EmergenceMassNugget[4, 4]);
                var rent = typeof(EmergenceField).GetMethod("RentPickup", Hidden);
                for (int cycle = 0; cycle < 40; cycle++)
                {
                    foreach (var p in live) if (p) p.gameObject.SetActive(false);
                    for (int kind = 0; kind < 4; kind++)
                    {
                        // Rotate slots to catch stale aliases when a cached object moves between live slots.
                        int slot = (kind + cycle) % 4;
                        var pickup = (EmergenceMassNugget)rent.Invoke(field, new object[] { (EmergenceDropType)kind });
                        live[slot] = pickup; pickup.gameObject.SetActive(true);
                    }
                    if (field.ActiveNuggets != 4 || live.Distinct().Count() != 4) throw new Exception("Pool duplicated a live slot.");
                }
                check(root.GetComponentsInChildren<EmergenceMassNugget>(true).Length == 4 && field.ActiveNuggets == 4,
                    "All four drop types recycle across changing slots without duplicate live counts or repeated allocation");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
