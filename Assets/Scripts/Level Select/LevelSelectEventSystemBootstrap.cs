using UnityEngine;
using UnityEngine.EventSystems;

public class LevelSelectEventSystemBootstrap : MonoBehaviour
{
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private GameObject targetToSelect; // the GameObject with LevelCarouselEventDriver

    private void Awake()
    {
        if (eventSystem == null) eventSystem = EventSystem.current;

        if (eventSystem != null && targetToSelect != null)
            eventSystem.SetSelectedGameObject(targetToSelect);
    }

    private void OnEnable()
    {
        // In case another UI element steals selection when enabling objects
        if (eventSystem == null) eventSystem = EventSystem.current;

        if (eventSystem != null && targetToSelect != null)
            eventSystem.SetSelectedGameObject(targetToSelect);
    }
}