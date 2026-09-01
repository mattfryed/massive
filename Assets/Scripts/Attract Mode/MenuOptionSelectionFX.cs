using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class MenuOptionSelectionFX : MonoBehaviour,
    ISelectHandler, IDeselectHandler,
    IMoveHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [Header("Pseudo players associated with THIS option")]
    [SerializeField] private MenuLifeFxToggle[] pseudoPlayers;

    [SerializeField] private bool hideOnDeselect = true;
    [SerializeField] private bool replayRespawnEverySelect = true;

    [Header("Text animation (optional)")]
    [SerializeField] private TMPTextTransition labelTransition;
    [SerializeField] private bool playLabelOutOnDeselect = true;

    [Header("Robustness")]
    [SerializeField] private bool hoverAlsoTriggers = false;
    [SerializeField] private bool pollSelectionFallback = true;

    [Header("Debug")]
    [SerializeField] private bool logMoves = true;

    private bool _hover;
    private bool _active;
    private bool _syncedFirstFrame;

    private bool IsSelected()
    {
        var es = EventSystem.current;
        return es != null && es.currentSelectedGameObject == gameObject;
    }

    private void Update()
    {
        // Prevent the startup “IN then OUT” spam:
        // On the very first frame, just sync internal state without playing animations.
        if (!_syncedFirstFrame)
        {
            _active = IsSelected();
            _syncedFirstFrame = true;
            return;
        }

        if (!pollSelectionFallback) return;

        bool shouldBeActive = IsSelected() || (hoverAlsoTriggers && _hover);
        if (shouldBeActive != _active)
            Apply(shouldBeActive, forceReplay: shouldBeActive && replayRespawnEverySelect);
    }

    public void OnSelect(BaseEventData eventData)
    {
        Debug.Log($"{name} OnSelect");
        Apply(true, replayRespawnEverySelect);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        Debug.Log($"{name} OnDeselect");
        Apply(false, false);
    }

    public void OnMove(AxisEventData eventData)
    {
        if (logMoves)
            Debug.Log($"{name} OnMove: {eventData.moveDir}");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hoverAlsoTriggers) return;
        _hover = true;
        Apply(true, false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!hoverAlsoTriggers) return;
        _hover = false;
        if (!IsSelected())
            Apply(false, false);
    }

    private void Apply(bool active, bool forceReplay)
    {
        if (_active == active && !forceReplay) return;
        _active = active;

        if (labelTransition)
        {
            if (active) labelTransition.PlayIn();
            else if (playLabelOutOnDeselect) labelTransition.PlayOut();
        }

        if (pseudoPlayers != null)
        {
            for (int i = 0; i < pseudoPlayers.Length; i++)
            {
                var p = pseudoPlayers[i];
                if (!p) continue;

                if (active) p.Show(replayEvenIfVisible: forceReplay);
                else if (hideOnDeselect) p.Hide();
            }
        }
    }
}
