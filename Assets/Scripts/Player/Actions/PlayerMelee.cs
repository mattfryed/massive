using System.Collections;
using UnityEngine;
using Massive.Player; // for PlayerAttackController / AttackStage

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

    private void Reset()
    {
        hitbox = GetComponent<Collider>();
        if (hitbox) hitbox.isTrigger = true;
        // Try to resolve common references
        if (!owner) owner = GetComponentInParent<PlayerControllerScript>();
        if (!attackController) attackController = GetComponentInParent<PlayerAttackController>();
    }

    private void Awake()
    {
        if (!hitbox) hitbox = GetComponent<Collider>();
        if (!owner) owner = GetComponentInParent<PlayerControllerScript>();
        if (!attackController) attackController = GetComponentInParent<PlayerAttackController>();

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
        if (!gateHitboxToActivationWindow || hitbox == null || attackController == null || stage == null)
            return;

        if (gateRoutine != null) StopCoroutine(gateRoutine);
        gateRoutine = StartCoroutine(GateHitbox(stage));
    }

    private void OnStageCompleted(AttackStage stage)
    {
        if (hitbox) hitbox.enabled = false;
        if (gateRoutine != null)
        {
            StopCoroutine(gateRoutine);
            gateRoutine = null;
        }
    }

    private IEnumerator GateHitbox(AttackStage stage)
    {
        // Wait until we reach the stage's activation start
        while (attackController.CurrentStage == stage &&
               attackController.StageNormalizedTime < stage.ActivationStartNormalized)
        {
            yield return null;
        }

        if (attackController.CurrentStage == stage && hitbox) hitbox.enabled = true;

        // Keep enabled through the activation window
        while (attackController.CurrentStage == stage &&
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
        if (other.CompareTag(swordTag))
        {
            if (swordClashPrefab)
            {
                var sc = Instantiate(swordClashPrefab);
                sc.transform.position = transform.position;
            }

            // Apply your existing clash behavior to BOTH owners
            owner.SwordClash();
            var otherOwner = other.GetComponentInParent<PlayerControllerScript>();
            if (otherOwner) otherOwner.SwordClash();
            return;
        }

        // Sword vs Shield (stun attacker)
        if (other.CompareTag(shieldTag))
        {
            owner.Stun(other.transform.position);
            return;
        }

        // Sword vs Player (damage if victim not shielding)
        if (other.CompareTag(playerTag))
        {
            var victim = other.GetComponent<PlayerControllerScript>();
            if (!victim) return;

            if (!victim.shieldOn)
            {
                owner.Grow();                           // Attacker grows
                victim.Shrink(owner.gameObject);        // Victim shrinks + blob eject to attacker
                victim.playSFX("struckSFX");
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
