using Massive.PowerUps;
using UnityEngine;

namespace Massive.Demonstrations
{
    public enum PlayerDemoKind { MovementAndCombo, Block, PowerUp, CombatPair, SoloCombo }
    public enum CombatDemoDefense { TapParry, HeldBlock, None, DecoherenceParry }

    [CreateAssetMenu(menuName = "MASSIVE/Demonstrations/Player Scenario")]
    public sealed class PlayerDemoScenario : ScriptableObject
    {
        public GameObject playerPrefab;
        public PlayerDemoKind kind;
        [Tooltip("Power-up demos claim the ordinary pickup with a real melee attack before using the effect.")]
        public PowerUpDefinition powerUp;
        [Min(.1f)] public float readablePause = 1.2f;
        [Min(1f)] public float actionTimeout = 8f;
        [Min(.1f)] public float movementDistance = 2f;
        [Range(0f, 1f)] public float acceleratorChargeFraction = .35f;
        [Min(1f)] public float acceleratorTargetDistance = 4f;
        [Min(.1f)] public float viewPadding = .2f;
        [Header("Combat pair")]
        public CombatDemoDefense defense;
        public DecoherencePowerUpDefinition defenderPowerUp;
        [Tooltip("Extra time beyond the opening parry window before starting a held-block attack.")]
        [Min(.05f)] public float heldBlockLead = .15f;
    }
}
