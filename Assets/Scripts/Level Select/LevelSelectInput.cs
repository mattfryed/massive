using UnityEngine;

public class LevelSelectInput : MonoBehaviour
{
    [SerializeField] private LevelCarouselController carousel;

    [Header("Axis Input")]
    [SerializeField] private float axisThreshold = 0.6f;
    [SerializeField] private float repeatCooldownSeconds = 0.18f;

    [Header("Fallback (Keyboard)")]
    [SerializeField] private KeyCode leftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode rightKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode confirmKey = KeyCode.Return;

#if REWIRED
    [Header("Rewired")]
    [SerializeField] private int rewiredPlayerId = 0;
    [SerializeField] private string horizontalAxisAction = "MoveH";
    [SerializeField] private string confirmAction = "Sword";
    private Rewired.Player _p;
#endif

    private float _cooldown;
    private bool _axisArmed = true;

    private void Awake()
    {
        if (carousel == null) carousel = FindFirstObjectByType<LevelCarouselController>();

#if REWIRED
        _p = Rewired.ReInput.players.GetPlayer(rewiredPlayerId);
#endif
    }

    private void Update()
    {
        if (carousel == null) return;

        _cooldown -= Time.unscaledDeltaTime;

        float h = 0f;

#if REWIRED
        if (_p != null) h = _p.GetAxisRaw(horizontalAxisAction);
#else
        h = Input.GetAxisRaw("Horizontal");
#endif

        // Keyboard fallback
        if (Input.GetKeyDown(leftKey)) h = -1f;
        if (Input.GetKeyDown(rightKey)) h = 1f;

        // One-step-per-flick behavior
        if (_cooldown <= 0f)
        {
            if (_axisArmed && h <= -axisThreshold)
            {
                carousel.SelectPrev();
                _cooldown = repeatCooldownSeconds;
                _axisArmed = false;
            }
            else if (_axisArmed && h >= axisThreshold)
            {
                carousel.SelectNext();
                _cooldown = repeatCooldownSeconds;
                _axisArmed = false;
            }
        }

        if (Mathf.Abs(h) < 0.2f)
            _axisArmed = true;

        bool confirm = false;

#if REWIRED
        if (_p != null) confirm |= _p.GetButtonDown(confirmAction);
#endif
        confirm |= Input.GetKeyDown(confirmKey);

        if (confirm)
            carousel.ConfirmSelection();
    }
}