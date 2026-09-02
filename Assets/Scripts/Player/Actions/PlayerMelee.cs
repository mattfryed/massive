using System.Collections;
using UnityEngine;
using Massive.Player;
using Massive.PowerUps;

/// <summary>
/// Event-driven melee hitbox for the player's weapon.
/// The victim resolves a hit transaction first; attacker mass/feedback is only
/// granted after the victim confirms that damage was accepted.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class PlayerMelee : MonoBehaviour
{
    [Header("Owners & Controllers")]
    [SerializeField] private PlayerControllerScript owner;
    [SerializeField] private PlayerAttackController attackController;

    [Header("Hitbox")]
    [SerializeField] private Collider hitbox;
    [SerializeField] private bool gateHitboxToActivationWindow = true;

    [Header("Tags")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string shieldTag = "Shield";
    [SerializeField] private string swordTag = "Sword";

    [Header("VFX / SFX")]
    [SerializeField] private GameObject swordClashPrefab;

    private Coroutine gateRoutine;

    public PlayerControllerScript Owner => owner;

    private bool IsFriendly(PlayerControllerScript otherPlayer)
    {
        return owner != null && otherPlayer != null && otherPlayer.teamID == owner.teamID;
    }

    private void Reset()
    {
        hitbox = GetComponent<Collider>();
        if (hitbox != null)
            hitbox.isTrigger = true;
    }

    private void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<PlayerControllerScript>();
        if (attackController == null)
            attackController = GetComponentInParent<PlayerAttackController>();
        if (hitbox == null)
            hitbox = GetComponent<Collider>();

        if (hitbox != null)
            hitbox.isTrigger = true;
    }

    private void OnEnable()
    {
        if (gateHitboxToActivationWindow && hitbox != null)
            hitbox.enabled = false;

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

        if (hitbox != null)
            hitbox.enabled = false;
    }

    private void OnStageStarted(AttackStage stage)
    {
        if (!gateHitboxToActivationWindow || hitbox == null)
            return;

        if (gateRoutine != null)
            StopCoroutine(gateRoutine);

        gateRoutine = StartCoroutine(GateHitboxRoutine(stage));
    }

    private void OnStageCompleted(AttackStage stage)
    {
        if (!gateHitboxToActivationWindow || hitbox == null)
            return;

        hitbox.enabled = false;

        if (gateRoutine != null)
        {
            StopCoroutine(gateRoutine);
            gateRoutine = null;
        }
    }

    private IEnumerator GateHitboxRoutine(AttackStage stage)
    {
        while (attackController != null &&
               attackController.CurrentStage == stage &&
               attackController.StageNormalizedTime < stage.ActivationStartNormalized)
        {
            yield return null;
        }

        if (attackController != null && attackController.CurrentStage == stage && hitbox != null)
            hitbox.enabled = true;

        while (attackController != null &&
               attackController.CurrentStage == stage &&
               attackController.StageNormalizedTime <= stage.ActivationEndNormalized)
        {
            yield return null;
        }

        if (hitbox != null)
            hitbox.enabled = false;

        gateRoutine = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner == null || other == null)
            return;

        // Sword-vs-sword remains disabled until the clash system is re-enabled.
        // Keeping the branch in one place prevents the former duplicate player-hit path.
        if (other.CompareTag(swordTag))
        {
            // Optional future clash implementation:
            // Spawn clash VFX, then call SwordClash on both owners.
            return;
        }

        if (other.CompareTag(shieldTag))
        {
            ResolveShieldImpact(other);
            return;
        }

        if (other.CompareTag(playerTag))
            ResolvePlayerImpact(other);
    }

    private void ResolveShieldImpact(Collider shieldCollider)
    {
        PlayerControllerScript defender = shieldCollider.GetComponentInParent<PlayerControllerScript>();
        if (defender != null && IsFriendly(defender))
            return;

        PlayerPowerUpController defenderPowerUps = shieldCollider.GetComponentInParent<PlayerPowerUpController>();
        if (defenderPowerUps != null)
        {
            Vector3 direction = shieldCollider.transform.position - owner.transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
                if (defenderPowerUps.TryHandleShieldImpact(owner, shieldCollider, direction))
                    return;
            }
        }

        Massive.Player.PlayerShieldAbility defenderShield =
            shieldCollider.GetComponentInParent<Massive.Player.PlayerShieldAbility>();

        float strength = 1f;
        if (defenderShield != null && defenderShield.IsActive)
            strength = defenderShield.CurrentStrength01;

        owner.Stun(shieldCollider.transform.position, strength);
        AudioSystem.I?.Play(AudioEventId.Player_Parry, transform.position);

        float leak01 = Mathf.Clamp01(1f - strength);
        if (defender == null || IsFriendly(defender) || leak01 <= 0.001f)
            return;

        PlayerHitResult hit = defender.TryApplyHit(owner.gameObject, leak01);
        if (!hit.accepted)
            return;

        owner.GrowScaled(hit.appliedScale01);
        defender.playSFX("struckSFX");
        AudioSystem.I?.Play(AudioEventId.Player_Hit, transform.position);
    }

    private void ResolvePlayerImpact(Collider playerCollider)
    {
        PlayerControllerScript victim = playerCollider.GetComponentInParent<PlayerControllerScript>();
        if (victim == null || victim == owner || IsFriendly(victim))
            return;

        if (victim.shieldOn)
            return;

        PlayerHitResult hit = victim.TryApplyHit(owner.gameObject, 1f);
        if (!hit.accepted)
            return;

        // Restore attacker mass only after the victim accepted the hit. If the
        // victim had less than a full hit of mass remaining, gain is proportional.
        owner.GrowScaled(hit.appliedScale01);
        AudioSystem.I?.Play(AudioEventId.Player_Hit, transform.position);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (hitbox == null) return;

        Gizmos.color = hitbox.enabled ? Color.green : Color.red;
        Gizmos.DrawWireCube(hitbox.bounds.center, hitbox.bounds.size);
    }
#endif
}
