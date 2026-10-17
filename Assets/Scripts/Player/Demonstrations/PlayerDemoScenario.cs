using Massive.PowerUps;
using UnityEngine;

namespace Massive.Demonstrations
{
    public enum PlayerDemoKind { MovementAndCombo, Block, PowerUp, CombatPair, SoloCombo, AmplifierGoal, DroneThrust, DroneCombo, DroneChargedThrust, DefenseSequence }
    public enum CombatDemoDefense { TapParry, HeldBlock, None, DecoherenceParry }

    [CreateAssetMenu(menuName = "MASSIVE/Demonstrations/Player Scenario")]
    public sealed class PlayerDemoScenario : ScriptableObject
    {
        public GameObject playerPrefab;
        public PlayerDemoKind kind;
        [Min(.1f)] public float actorScale = 1f;
        [Tooltip("Power-up demos claim the ordinary pickup with a real melee attack before using the effect.")]
        public PowerUpDefinition powerUp;
        [Min(.1f)] public float readablePause = 1.2f;
        [Min(1f)] public float actionTimeout = 8f;
        [Min(.1f)] public float movementDistance = 2f;
        [Range(0f, 1f)] public float acceleratorChargeFraction = .35f;
        [Min(1f)] public float acceleratorTargetDistance = 4f;
        [Min(.1f)] public float viewPadding = .2f;
        [Header("Drone combat demonstration")]
        public Massive.Enemies.EnemyDefinition droneDefinition;
        [Header("Decoherence follow-up wave")]
        [Tooltip("Optional second enemy type, spawned after the Drones are defeated.")]
        public Massive.Enemies.EnemyDefinition decoherenceSecondEnemy;
        [Header("Time Dilation pickup circle")]
        [Tooltip("Optional canonical body pickups, collected in order around a circle. Empty retains the movement-only demo.")]
        public Massive.Enemies.EnemyLootPickup[] circlePickups;
        [Min(.5f)] public float pickupCircleRadius = 1.45f;
        [Min(.5f)] public float pickupCircleLegSeconds = 1.1f;
        [Min(0f)] public float pickupSpawnPause = .4f;
        [Header("Amplifier demonstration")]
        public Massive.Multiplier.AmplifierCoreGameplay amplifierCorePrefab;
        public GameObject teamGoalPrefab;
        [Tooltip("Show both goals followed by a real two-player possession contest.")]
        public bool demonstrateAmplifierContest;
        public GameObject opposingTeamGoalPrefab;
        public Vector2 amplifierArenaSize = new Vector2(16.4f, 6f);
        [Tooltip("Demo-only wait after capture; normal Core spawn and capture effects are retained.")]
        [Min(0f)] public float amplifierRespawnDelay = 5f;
        public Massive.Scoring.ScoreEconomyProfile scoreEconomy;
        public TMPro.TMP_FontAsset labelFont;
        [Header("Combat pair")]
        public CombatDemoDefense defense;
        public DecoherencePowerUpDefinition defenderPowerUp;
        [Tooltip("Extra time beyond the opening parry window before starting a held-block attack.")]
        [Min(.05f)] public float heldBlockLead = .15f;
        [Tooltip("DEFEND demo pause after a confirmed parry. Limited to the remaining gameplay counter window.")]
        [Min(0f)] public float counterAttackPause = .35f;
    }
}
