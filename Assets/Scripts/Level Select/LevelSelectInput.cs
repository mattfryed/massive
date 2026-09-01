using UnityEngine;
using Rewired;

public class LevelSelectInput : MonoBehaviour
{
    [SerializeField] private LevelCarouselController carousel;

    [Header("Rewired Actions")]
    [SerializeField] private string horizontalAxisAction = "MoveH";
    [SerializeField] private string confirmAction = "Sword";

    [Header("Feel")]
    [SerializeField] private float axisThreshold = 0.6f;
    [SerializeField] private float repeatCooldownSeconds = 0.18f;
    [SerializeField] private float neutralResetThreshold = 0.2f;

    private float _cooldown;
    private bool _axisArmed = true;

    private void Awake()
    {
        if (carousel == null)
            carousel = FindFirstObjectByType<LevelCarouselController>();
    }

    private void Update()
    {
        if (carousel == null) return;
        if (!ReInput.isReady) return;

        _cooldown -= Time.unscaledDeltaTime;

        float h = 0f;
        bool confirm = false;

        // Any player can steer/confirm:
        var players = ReInput.players.Players;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            float ph = p.GetAxisRaw(horizontalAxisAction);
            if (Mathf.Abs(ph) > Mathf.Abs(h))
                h = ph;

            if (p.GetButtonDown(confirmAction))
                confirm = true;
        }

        // One-step-per-flick
if (_cooldown <= 0f)
{
    if (_axisArmed && h <= -axisThreshold)
    {
        // LEFT = previous stage
        carousel.StepPrevStage();
        _cooldown = repeatCooldownSeconds;
        _axisArmed = false;
    }
    else if (_axisArmed && h >= axisThreshold)
    {
        // RIGHT = next stage
        carousel.StepNextStage();
        _cooldown = repeatCooldownSeconds;
        _axisArmed = false;
    }
}


        if (Mathf.Abs(h) < neutralResetThreshold)
            _axisArmed = true;

        if (confirm)
            carousel.ConfirmSelection();
    }
}
