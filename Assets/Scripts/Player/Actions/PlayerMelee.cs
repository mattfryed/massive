using System.Collections;
using UnityEngine;
using Massive.Player;     // for PlayerAttackController / AttackStage
using Massive.PowerUps;   // for PlayerPowerUpController (Decoherence intercept)

/// <summary>
/// Event-driven melee hitbox for the player's weapon.
/// - Gates the collider to be active only during the current AttackStage activation window.
/// - Handles Player vs Player, Sword vs Sword, and Sword vs Shield interactions.
/// - Routes gameplay effects directly to PlayerControllerScript.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class PlayerMelee : MonoBehaviour
{
    [Header("Owners & Controllers")]
    [SerializeField] private PlayerControllerScript owner;              // Assign (or auto-found)
    [SerializeField] private PlayerAttackController attackController;   // Assign (or auto-found)

    [Header("Hitbox")]
    [SerializeField] private Collider hitbox;                           // Must be a trigger collider
    [SerializeField] private bool gateHitboxToActivationWindow = true;  // If true, collider enabled only during stage window

    [Header("Tags")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string shieldTag = "Shield";
    [SerializeField] private string swordTag  = "Sword";

    [Header("VFX / SFX")]
    [SerializeField] private GameObject swordClashPrefab;               // optional

    private Coroutine gateRoutine;

    
private bool IsFriendly(PlayerControllerScript otherPlayer)
{
    if (owner == null || otherPlayer == null) return false;
    return otherPlayer.teamID == owner.teamID;
}

    private void Reset()
    {
        hitbox = GetComponent<Collider>();
        if (hitbox) hitbox.isTrigger = true;
    }

    private void Awake()
    {
        if (!owner) owner = GetComponentInParent<PlayerControllerScript>();
        if (!attackController) attackController = GetComponentInParent<PlayerAttackController>();
        if (!hitbox) hitbox = GetComponent<Collider>();

        if (hitbox) hitbox.isTrigger = true;
    }

    private void OnEnable()
    {
        if (gateHitboxToActivationWindow && hitbox) hitbox.enabled = false;

        if (attackController != null)
        {
            attackController.OnStageStarted.AddListener(OnStageStarted);
            attackController.OnStageCompleted.AddListener(OnStageCompleted);
        }
    }

    private void OnDisable()
    {
        if (attackController != null)
        {
            attackController.OnStageStarted.RemoveListener(OnStageStarted);
            attackController.OnStageCompleted.RemoveListener(OnStageCompleted);
        }

        if (gateRoutine != null)
        {
            StopCoroutine(gateRoutine);
            gateRoutine = null;
        }

        if (hitbox) hitbox.enabled = false;
    }

    // --- Stage gating ---

    private void OnStageStarted(AttackStage stage)
    {
        if (!gateHitboxToActivationWindow || hitbox == null) return;

        if (gateRoutine != null) StopCoroutine(gateRoutine);
        gateRoutine = StartCoroutine(GateHitboxRoutine(stage));
    }

    private void OnStageCompleted(AttackStage stage)
    {
        if (!gateHitboxToActivationWindow || hitbox == null) return;

        // Hard shut-off on stage end
        hitbox.enabled = false;

        if (gateRoutine != null)
        {
            StopCoroutine(gateRoutine);
            gateRoutine = null;
        }
    }

    private IEnumerator GateHitboxRoutine(AttackStage stage)
    {
        // Wait until activation window opens
        while (attackController != null &&
               attackController.CurrentStage == stage &&
               attackController.StageNormalizedTime < stage.ActivationStartNormalized)
        {
            yield return null;
        }

        if (attackController != null && attackController.CurrentStage == stage && hitbox)
            hitbox.enabled = true;

        // Keep enabled through the activation window
        while (attackController != null &&
               attackController.CurrentStage == stage &&
               attackController.StageNormalizedTime <= stage.ActivationEndNormalized)
        {
            yield return null;
        }

        if (hitbox) hitbox.enabled = false;
        gateRoutine = null;
    }

    // --- Collisions ---

    private void OnTriggerEnter(Collider other)
    {
        if (!owner) return;

        // Sword vs Sword (clash)
        // if (other.CompareTag(swordTag))
        // {
        //     if (swordClashPrefab)
        //     {
        //         var sc = Instantiate(swordClashPrefab);
        //         sc.transform.position = transform.position;
        //     }

        //     // Apply your existing clash behavior to BOTH owners
        //     owner.SwordClash();
        //     var otherOwner = other.GetComponentInParent<PlayerControllerScript>();
        //     if (otherOwner) otherOwner.SwordClash();
        //     return;
        // }

        // Sword vs Shield
        // Default: attacker gets stunned.
        // Power-up override: defender can "Decohere" while shielding, letting attacker phase through + get briefly stunned.
    if (other.CompareTag(shieldTag))
    {
        var defender = other.GetComponentInParent<PlayerControllerScript>();

        // NEW: ignore teammate shields (prevents grief-stunning your own team)
        if (defender != null && IsFriendly(defender))
            return;

        // Power-up override still gets first shot (existing behavior)
        var defenderPU = other.GetComponentInParent<PlayerPowerUpController>();
        if (defenderPU != null)
        {
            Vector3 dir = other.transform.position - owner.transform.position;
            dir.y = 0;
            if (dir.magnitude > 0.01f)
            {
                dir.Normalize();
                if (defenderPU.TryHandleShieldImpact(owner, other, dir))
                    return;
            }
        }

        var defenderShield = other.GetComponentInParent<Massive.Player.PlayerShieldAbility>();

        float strength = 1f;
        if (defenderShield != null && defenderShield.IsActive)
            strength = defenderShield.CurrentStrength01;

        owner.Stun(other.transform.position, strength);
        AudioSystem.I?.Play(AudioEventId.Player_Parry, transform.position);

        float leak01 = Mathf.Clamp01(1f - strength);

        // NEW: only leak-through damage if defender is an enemy
        if (defender != null && leak01 > 0.001f && !IsFriendly(defender))
        {
            owner.GrowScaled(leak01);
            defender.ShrinkScaled(owner.gameObject, leak01);
            defender.playSFX("struckSFX");
        }

        return;
    }

    // Sword vs Player
    if (other.CompareTag(playerTag))
    {
        // Slight robustness improvement: collider might be on a child
        var victim = other.GetComponentInParent<PlayerControllerScript>();
        if (!victim) return;

        // NEW: ignore self + teammates
        if (victim == owner) return;
        if (IsFriendly(victim)) return;

        if (!victim.shieldOn)
        {
            owner.Grow();
            victim.Shrink(owner.gameObject);
            AudioSystem.I?.Play(AudioEventId.Player_Hit, transform.position);
        }
    }



        // Sword vs Player (damage if victim not shielding)
        if (other.CompareTag(playerTag))
        {
            var victim = other.GetComponent<PlayerControllerScript>();
            if (!victim) return;

            if (!victim.shieldOn)
            {
                owner.Grow();                    // Attacker grows
                victim.Shrink(owner.gameObject); // Victim shrinks + blob eject to attacker
                // victim.playSFX("struckSFX");
                AudioSystem.I?.Play(AudioEventId.Player_Hit, transform.position);
            }
        }
    }

#if UNITY_EDITOR
    // Simple viz
    private void OnDrawGizmosSelected()
    {
        if (hitbox)
        {
            Gizmos.color = (hitbox.enabled ? Color.green : Color.red);
            Gizmos.DrawWireCube(hitbox.bounds.center, hitbox.bounds.size);
        }
    }
#endif
}