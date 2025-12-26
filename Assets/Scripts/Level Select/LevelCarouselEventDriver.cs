using UnityEngine;
using UnityEngine.EventSystems;

public class LevelCarouselEventDriver : MonoBehaviour, IMoveHandler, ISubmitHandler, ICancelHandler
{
    [SerializeField] private LevelCarouselController carousel;

    private void Awake()
    {
        if (carousel == null)
            carousel = FindFirstObjectByType<LevelCarouselController>();
    }

    public void OnMove(AxisEventData eventData)
    {
        if (carousel == null) return;

        // We only care about left/right for your carousel.
        switch (eventData.moveDir)
        {
            case MoveDirection.Left:
                carousel.StepCounterClockwise();
                eventData.Use();
                break;

            case MoveDirection.Right:
                carousel.StepClockwise();
                eventData.Use();
                break;
        }
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (carousel == null) return;

        // Single-tap commit (your requirement)
        carousel.ConfirmSelection();
        eventData.Use();
    }

    public void OnCancel(BaseEventData eventData)
    {
        // You said no cancel needed; ignore.
        eventData.Use();
    }
}