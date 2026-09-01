using UnityEngine;

namespace Massive.PowerUps
{
    [CreateAssetMenu(fileName = "PU_MassNode", menuName = "MASSIVE/PowerUps/Mass Node")]
    public class MassNodePowerUpDefinition : PowerUpDefinition, IInstantPowerUpEffect
    {
        [Header("Mass Gain")]
        [Tooltip("Old MatterNuggetScript behavior: call Grow() N times.")]
        public int growCalls = 3;

        [Tooltip("Optional extra massScore delta in 0..1 space (applied via ApplyExternalMassDelta).")]
        public float extraMass01 = 0f;

        [Header("SFX")]
        public string pickupSfx = "massNuggetSFX";

        [Header("VFX - SmallMassBlob")]
        [Tooltip("Spawn the SmallMassBlob VFX from the pickup location.")]
        public bool spawnSmallMassBlobs = true;

        [Min(0)] public int blobsToPlayer = 4;
        [Min(0)] public int blobsToGoal   = 4;

        [Tooltip("If set, use this prefab instead of the player's massBlobPrefab.")]
        public GameObject overrideSmallMassBlobPrefab;

        public override PowerUpType Type => PowerUpType.MassNode;

        public void ApplyInstant(PlayerControllerScript player, PlayerPowerUpController powerUps, GameObject pickupGO)
        {
            if (player == null) return;

            // SFX
            if (!string.IsNullOrEmpty(pickupSfx))
                player.playSFX(pickupSfx);

            // Mass gain (same semantics as melee gain, including overflow-to-score if enabled)
            int n = Mathf.Max(0, growCalls);
            for (int i = 0; i < n; i++)
                player.Grow();

            if (extraMass01 > 0.0001f)
                player.ApplyExternalMassDelta(extraMass01, allowDeath: true);

            // VFX
            if (spawnSmallMassBlobs)
            {
                Vector3 spawnPos = pickupGO ? pickupGO.transform.position : player.transform.position;
                SpawnMassBlobs(player, spawnPos);
            }
        }

        private void SpawnMassBlobs(PlayerControllerScript player, Vector3 spawnPos)
        {
            // PlayerControllerScript already has a "massBlobPrefab" intended for SmallMassBlobScript VFX. :contentReference[oaicite:7]{index=7}
            GameObject blobPrefab = overrideSmallMassBlobPrefab != null ? overrideSmallMassBlobPrefab : player.massBlobPrefab;
            if (blobPrefab == null) return;

            // Player target (so you see “mass pulled into the player”)
            for (int i = 0; i < blobsToPlayer; i++)
            {
                var go = Object.Instantiate(blobPrefab, spawnPos, Quaternion.identity);
                var smb = go.GetComponent<SmallMassBlobScript>();
                if (smb != null) smb.target = player.gameObject;
            }

            // Goal target (so you also see “mass pulled toward team goal/score”)
            GameObject goalTarget = null;
            if (player.goalZone != null)
            {
                var t = player.goalZone.transform.Find("Score Sphere");
                if (t != null) goalTarget = t.gameObject;
            }

            if (goalTarget != null)
            {
                for (int i = 0; i < blobsToGoal; i++)
                {
                    var go = Object.Instantiate(blobPrefab, spawnPos, Quaternion.identity);
                    var smb = go.GetComponent<SmallMassBlobScript>();
                    if (smb != null) smb.target = goalTarget;
                }
            }
        }
    }
}
