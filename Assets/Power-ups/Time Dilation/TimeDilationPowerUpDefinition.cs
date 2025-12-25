using UnityEngine;

namespace Massive.PowerUps
{
    [CreateAssetMenu(fileName = "PU_TimeDilation", menuName = "MASSIVE/PowerUps/Time Dilation")]
    public class TimeDilationPowerUpDefinition : PowerUpDefinition
    {
        [Header("Tuning")]
        public float moveSpeedMultiplier = 1.45f;
    }
}