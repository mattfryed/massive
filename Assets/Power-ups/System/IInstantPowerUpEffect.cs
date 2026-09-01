using UnityEngine;

namespace Massive.PowerUps
{
    /// <summary>
    /// For pickups that should apply an immediate effect and not occupy the "equipped power-up" slot.
    /// </summary>
    public interface IInstantPowerUpEffect
    {
        void ApplyInstant(PlayerControllerScript player, PlayerPowerUpController powerUps, GameObject pickupGO);
    }
}
