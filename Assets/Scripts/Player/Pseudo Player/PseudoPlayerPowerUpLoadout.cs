using UnityEngine;
using Massive.PowerUps;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerPowerUpController))]
public class PseudoPlayerPowerUpLoadout : MonoBehaviour
{
    [SerializeField] private PlayerPowerUpController powerUps;

    [Header("Starting Ability (Power-Up)")]
    [SerializeField] private PowerUpDefinition startingPowerUp;

    [Tooltip("< 0 = use definition's effectDurationSeconds. 0.. = override seconds.")]
    [SerializeField] private float durationOverrideSeconds = -1f;

    [Tooltip("If true, forces duration to infinite (never expires).")]
    [SerializeField] private bool infiniteDuration = false;

    [Header("Lifecycle")]
    [SerializeField] private bool equipOnEnable = true;

    [Tooltip("If true, unequips immediately when this GO disables (prevents lingering effects).")]
    [SerializeField] private bool clearOnDisable = true;

    private void Reset()
    {
        powerUps = GetComponent<PlayerPowerUpController>();
    }

    private void Awake()
    {
        if (!powerUps) powerUps = GetComponent<PlayerPowerUpController>();
    }

    private void OnEnable()
    {
        if (!equipOnEnable) return;
        Equip();
    }

    private void OnDisable()
    {
        if (!clearOnDisable) return;
        if (powerUps != null) powerUps.Clear();
    }

    [ContextMenu("Equip Now")]
    public void Equip()
    {
        if (powerUps == null) return;
        if (startingPowerUp == null) return;

        if (infiniteDuration)
        {
            powerUps.Equip(startingPowerUp, float.PositiveInfinity);
        }
        else if (durationOverrideSeconds >= 0f)
        {
            powerUps.Equip(startingPowerUp, durationOverrideSeconds);
        }
        else
        {
            // Default: uses def.effectDurationSeconds (normal gameplay behavior)
            powerUps.Equip(startingPowerUp);
        }
    }

    [ContextMenu("Clear Now")]
    public void Clear()
    {
        if (powerUps != null) powerUps.Clear();
    }
}
