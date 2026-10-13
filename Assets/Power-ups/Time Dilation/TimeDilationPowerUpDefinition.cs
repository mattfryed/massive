using UnityEngine;

namespace Massive.PowerUps
{
    [CreateAssetMenu(fileName = "PU_TimeDilation", menuName = "MASSIVE/PowerUps/Time Dilation")]
    public class TimeDilationPowerUpDefinition : PowerUpDefinition
    {
        [Header("Tuning")]
        public float moveSpeedMultiplier = 1.45f;

        [Header("Equipped appearance — motion echoes")]
        public bool showMotionEchoes = true;
        [Range(1, 3)] public int echoCount = 3;
        [Tooltip("Time between the visible silhouettes. Each follows a recorded position and facing along the actual path.")]
        [Range(.025f, .15f)] public float echoSpacingSeconds = .075f;
        [Range(0f, 1f)] public float echoOpacity = .5f;
        [Range(.2f, 2f)] public float echoWidthScale = .85f;
        [Tooltip("Planar speed at which the echoes fully replace the idle sweep, relative to player size.")]
        [Min(.1f)] public float echoFullSpeed = 3f;
        [Header("Equipped appearance — perimeter marks")]
        public bool showPerimeterMarks = true;
        [Range(0f, 1f)] public float perimeterOpacity = .8f;
        [Range(1.02f, 1.5f)] public float perimeterScale = 1.16f;
        [Range(5f, 80f)] public float perimeterArcDegrees = 28f;
        [Min(.2f)] public float perimeterCycleSeconds = 1.25f;

        public override PowerUpType Type => PowerUpType.TimeDilation;
    }
}
