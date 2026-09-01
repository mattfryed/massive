using UnityEngine;
using UnityEngine.EventSystems;

public class LevelCarouselEventDriver : MonoBehaviour, ISubmitHandler, ICancelHandler
{
    [SerializeField] private LevelCarouselController carousel;

    private void Awake()
    {
        if (carousel == null)
            carousel = FindFirstObjectByType<LevelCarouselController>();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (carousel == null) return;
        carousel.ConfirmSelection();
        eventData.Use();
    }

    public void OnCancel(BaseEventData eventData)
    {
        eventData.Use();
    }
}
