using UnityEngine;

[DisallowMultipleComponent]
public class PlayerExternalEffects : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerControllerScript player;

    [Header("Storm")]
    [SerializeField] private bool stormDrainIgnoresInvuln = false;


    void Awake()
    {
        if (!player) player = GetComponent<PlayerControllerScript>();
    }

    // Called by DynamoStormController via SendMessage OR direct reference
    public void ApplyStormDamage(float amount01)
    {
        if (!player) return;
        if (player.temporarilyEliminated) return;

        if (!stormDrainIgnoresInvuln && player.IsInvulnerable)
            return;

        float a = Mathf.Abs(amount01);
        if (a <= 0f) return;

        player.ApplyExternalMassDelta(-a, allowDeath: true);
    }

    // Examples for future expansion:
    public void ApplyExternalStun(float seconds)
    {
        if (!player) return;
        player.ExternalStun(seconds);
    }

    public void ApplyScaledHit(GameObject hitSource, float scale01)
    {
        if (!player) return;
        player.ShrinkScaled(hitSource, scale01);
    }
}
