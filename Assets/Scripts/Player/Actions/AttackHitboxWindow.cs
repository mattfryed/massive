using System.Collections;
using UnityEngine;
using Massive.Player; // namespace of your new scripts

[RequireComponent(typeof(Collider))]
public class AttackHitboxWindow : MonoBehaviour
{
    [SerializeField] PlayerAttackController attackController;
    [SerializeField] Collider hitbox; // your sword collider
    Coroutine gateRoutine;

    void Reset()
    {
        hitbox = GetComponent<Collider>();
        if (!attackController) attackController = GetComponentInParent<PlayerAttackController>();
    }

    void OnEnable()
    {
        if (!attackController) attackController = GetComponentInParent<PlayerAttackController>();
        if (attackController != null)
        {
            attackController.OnStageStarted.AddListener(OnStageStart);
            attackController.OnStageCompleted.AddListener(OnStageComplete);
        }
        if (hitbox) hitbox.enabled = false;
    }

    void OnDisable()
    {
        if (attackController != null)
        {
            attackController.OnStageStarted.RemoveListener(OnStageStart);
            attackController.OnStageCompleted.RemoveListener(OnStageComplete);
        }
    }

    void OnStageStart(AttackStage stage)
    {
        if (gateRoutine != null) StopCoroutine(gateRoutine);
        gateRoutine = StartCoroutine(GateHitbox(stage));
    }

    void OnStageComplete(AttackStage stage)
    {
        if (hitbox) hitbox.enabled = false;
    }

    IEnumerator GateHitbox(AttackStage stage)
    {
        if (!attackController || stage == null || hitbox == null)
            yield break;

        // Wait until we reach the start of the activation window
        while (attackController.CurrentStage == stage &&
               attackController.StageNormalizedTime < stage.ActivationStartNormalized)
            yield return null;

        if (attackController.CurrentStage == stage && hitbox) hitbox.enabled = true;

        // Keep enabled until end of activation window
        while (attackController.CurrentStage == stage &&
               attackController.StageNormalizedTime <= stage.ActivationEndNormalized)
            yield return null;

        if (hitbox) hitbox.enabled = false;
        gateRoutine = null;
    }
}
