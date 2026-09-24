#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Massive.Enemies;
using Massive.Multiplier;
using UnityEngine;

namespace Massive.Orbital
{
    /// <summary>Opt-in live checks. Never saved to the scene or included in a player build.</summary>
    public sealed class OrbitalPlayValidation : MonoBehaviour
    {
        public static string LastReport = "Not run";
        private readonly List<string> results = new List<string>();
        private OrbitalProbabilityCloud hazard;
        private PlayerControllerScript player;
        private AmplifierCoreGameplay core;
        private Rigidbody playerBody, coreBody;
        private Vector3 playerPosition, corePosition, playerVelocity, coreVelocity;
        private float originalRate, originalMass, originalTimeScale;
        private bool originalScoring, originalClock, restored;
        private GameObject drone, fixtureCore;

        private void Check(bool condition, string label) { results.Add((condition ? "PASS " : "FAIL ") + label); }

        private IEnumerator Start()
        {
            LastReport = "Running";
            hazard = FindFirstObjectByType<OrbitalProbabilityCloud>();
            if (!hazard) { LastReport = "FAIL No ORBITAL hazard"; Destroy(gameObject); yield break; }
            float timeout = Time.realtimeSinceStartup + 12f;
            while (!hazard.IsHazardActive && Time.realtimeSinceStartup < timeout) yield return null;
            if (!hazard.IsHazardActive) { LastReport = "FAIL Match never opened"; Destroy(gameObject); yield break; }
            player = FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None).First(p => !p.IsPseudoPlayer);
            core = AmplifierCoreGameplay.ActiveCores.FirstOrDefault(c => !c.IsPresentationOnly && !c.IsCaptured);
            if (!core)
            {
                // Scene authors may remove the standalone core while tuning. Exercise the actual prefab temporarily.
                fixtureCore = Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Power-ups/Amplifier Core/Amplifier Core.prefab"),
                    new Vector3(-10f, 0f, 3f), Quaternion.identity);
                core = fixtureCore.GetComponent<AmplifierCoreGameplay>();
                timeout = Time.realtimeSinceStartup + 6f;
                while (core.IsCaptured && Time.realtimeSinceStartup < timeout) yield return null;
            }
            playerBody = player.GetComponent<Rigidbody>(); coreBody = core.Body;
            playerPosition = playerBody.position; corePosition = coreBody.position;
            playerVelocity = playerBody.linearVelocity; coreVelocity = coreBody.linearVelocity;
            originalMass = player.massScore; originalRate = hazard.peakStrikesPerSecond;
            originalScoring = hazard.scoreService.IsScoringOpen; originalClock = hazard.scoreService.IsChainClockRunning;
            originalTimeScale = Time.timeScale;
            try
            {
                Check(hazard.TotalStrikes == hazard.NuggetsReleased && hazard.TotalStrikes == hazard.impacts.FlashesShown,
                    "Every natural strike releases one nugget and one orbital flash");
                hazard.peakStrikesPerSecond = 1000f;
                int count = hazard.TotalStrikes;
                hazard.scoreService.CloseScoring();
                yield return new WaitForSeconds(.12f);
                Check(!hazard.IsHazardActive && hazard.TotalStrikes == count, "Closed scoring stops strikes");
                hazard.scoreService.OpenScoring();
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(.12f);
                Check(!hazard.IsHazardActive && hazard.TotalStrikes == count, "Time pause stops strikes");
                Time.timeScale = originalTimeScale;

                coreBody.position = new Vector3(-10f, 0f, 3f); coreBody.linearVelocity = Vector3.zero;
                playerBody.position = new Vector3(0f, 0f, 2.1f); playerBody.linearVelocity = Vector3.zero;
                player.lastActivityTime = Time.time;
                Physics.SyncTransforms();
                int before = hazard.PlayerStrikes;
                Vector3 start = playerBody.position;
                for (int i = 0; i < 30 && hazard.PlayerStrikes == before; i++) yield return new WaitForFixedUpdate();
                Check(hazard.PlayerStrikes > before, "Player receives an electron strike");
                hazard.peakStrikesPerSecond = 0f;
                yield return new WaitForFixedUpdate();
                Check((playerBody.position - start).sqrMagnitude > .0001f, "Player moves after the impulse");
                playerBody.position = new Vector3(10f, 0f, -3f); playerBody.linearVelocity = Vector3.zero;

                coreBody.position = new Vector3(0f, 0f, 2.1f); coreBody.linearVelocity = Vector3.zero;
                Physics.SyncTransforms();
                before = hazard.CoreStrikes; start = coreBody.position; hazard.peakStrikesPerSecond = 1000f;
                for (int i = 0; i < 30 && hazard.CoreStrikes == before; i++) yield return new WaitForFixedUpdate();
                Check(hazard.CoreStrikes > before, "Amplifier core receives an electron strike");
                hazard.peakStrikesPerSecond = 0f;
                yield return new WaitForFixedUpdate();
                Check((coreBody.position - start).sqrMagnitude > .0001f, "Amplifier core moves after the impulse");
                coreBody.position = new Vector3(-10f, 0f, 3f); coreBody.linearVelocity = Vector3.zero;

                var director = FindFirstObjectByType<EnemyDirector>(FindObjectsInactive.Include);
                var definition = director.spawnProfile.batches.First(b => b.enemy && b.enemy.prefab.GetComponent<DroneController>()).enemy;
                drone = Instantiate(definition.prefab, new Vector3(0f, 0f, 2.1f), Quaternion.identity);
                drone.name = "ORBITAL validation drone";
                var enemy = drone.GetComponent<EnemyBase>(); enemy.Init(definition, null);
                yield return new WaitForSeconds(.65f);
                var droneBody = drone.GetComponent<Rigidbody>();
                droneBody.position = new Vector3(0f, 0f, 2.1f); droneBody.linearVelocity = Vector3.zero;
                start = droneBody.position; before = hazard.EnemyStrikes; hazard.peakStrikesPerSecond = 1000f;
                Physics.SyncTransforms();
                for (int i = 0; i < 30 && hazard.EnemyStrikes == before; i++) yield return new WaitForFixedUpdate();
                Check(hazard.EnemyStrikes > before, "Real drone prefab receives an electron strike");
                hazard.peakStrikesPerSecond = 0f;
                yield return new WaitForFixedUpdate();
                Check((droneBody.position - start).sqrMagnitude > .0001f && droneBody.linearVelocity.magnitude > 1f, "Drone recoils while its steering is active");
                Destroy(drone); drone = null;

                Check(hazard.TotalStrikes == hazard.NuggetsReleased && hazard.TotalStrikes == hazard.impacts.FlashesShown,
                    "Player, core and enemy strikes each release a nugget and flash");
                var nugget = hazard.GetComponentsInChildren<OrbitalMassNugget>().First();
                Check(nugget.GetComponent<Rigidbody>().linearVelocity.sqrMagnitude > .01f, "Mass nugget floats away from contact");
                foreach (var other in hazard.GetComponentsInChildren<OrbitalMassNugget>())
                    if (other != nugget) other.gameObject.SetActive(false);
                player.ApplyExternalMassDelta(.25f - player.massScore, false);
                float mass = player.massScore;
                player.ExternalStun(.8f);
                nugget.Eject(playerBody.position, Vector3.zero, 2f);
                Physics.SyncTransforms();
                yield return new WaitForSeconds(.5f);
                float smallReward = player.massScore - mass;
                Check(!nugget.gameObject.activeSelf && smallReward > 0f,
                    "Ejected nugglet is collectible and grants mass once");
                var standardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/Anomalies/ORBITAL/Orbital Mass Nugget.prefab");
                var standard = Instantiate(standardPrefab).GetComponent<OrbitalMassNugget>();
                mass = player.massScore;
                player.ExternalStun(.8f);
                standard.Eject(playerBody.position, Vector3.zero, 2f);
                Physics.SyncTransforms();
                yield return new WaitForSeconds(.5f);
                Check(!standard.gameObject.activeSelf && Mathf.Abs(smallReward * 10f - (player.massScore - mass)) < .00001f,
                    "Standard nugget still grants ten times the nugglet reward");
                Destroy(standard.gameObject);
                nugget.Eject(new Vector3(11f, 0f, 4f), Vector3.zero, .1f);
                yield return new WaitForSeconds(.16f);
                Check(!nugget.gameObject.activeSelf, "Nugget expires for reuse");

                hazard.peakStrikesPerSecond = 1000f;
                coreBody.position = new Vector3(11f, 0f, 0f);
                before = hazard.CoreStrikes;
                yield return new WaitForSeconds(.12f);
                Check(hazard.CoreStrikes == before, "No strikes outside the probability cloud");
                hazard.peakStrikesPerSecond = 0f;
                hazard.cloud.enabled = false;
                yield return null;
                Check(!hazard.IsHazardActive, "Disabling the density display also disables the hazard");
                hazard.cloud.enabled = true;
                yield return null;
                Check(hazard.cloud.VisibleParticleCount == hazard.cloud.particleCount, "Visual pool rebuilds after disable and re-enable");
                Check(hazard.GetComponentsInChildren<OrbitalMassNugget>(true).Length <= hazard.nuggetCapacity, "Nugget population stays bounded");
                yield return new WaitForSeconds(.4f);
                Check(hazard.impacts.GetComponentsInChildren<LineRenderer>().Length == 0, "Impact lines disappear after the flash");
            }
            finally { Restore(); }
            LastReport = string.Join("\n", results);
            Debug.Log("[ORBITAL live checks]\n" + LastReport);
            Destroy(gameObject);
        }

        private void Restore()
        {
            if (restored) return;
            restored = true;
            if (fixtureCore) Destroy(fixtureCore);
            if (!playerBody) return;
            Time.timeScale = originalTimeScale;
            if (hazard) { hazard.peakStrikesPerSecond = originalRate; hazard.scoreService.SetScoringState(originalScoring, originalClock); }
            if (player) { player.ApplyExternalMassDelta(originalMass - player.massScore, false); player.lastActivityTime = Time.time; }
            playerBody.position = playerPosition; playerBody.linearVelocity = playerVelocity;
            if (coreBody) { coreBody.position = corePosition; coreBody.linearVelocity = coreVelocity; }
            if (drone) Destroy(drone);
        }
        private void OnDestroy() { Restore(); }
    }
}
#endif
