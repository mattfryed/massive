using UnityEngine;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    public class PowerUpDebugCheats : MonoBehaviour
    {
        [Header("References")]
        public PlayerPowerUpController powerUps;

        [Header("Cheat")]
        public PowerUpDefinition equipThis;

        public KeyCode equipKey = KeyCode.P;
        public KeyCode clearKey = KeyCode.O;

        private void Awake()
        {
            if (!powerUps) powerUps = GetComponent<PlayerPowerUpController>();
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (Input.GetKeyDown(equipKey) && powerUps && equipThis)
                powerUps.Equip(equipThis);

            if (Input.GetKeyDown(clearKey) && powerUps)
                powerUps.ForceExpire();
#endif
        }
    }
}
