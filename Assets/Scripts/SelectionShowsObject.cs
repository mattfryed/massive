using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionShowsObject : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [SerializeField] private GameObject showWhenSelected;
    [SerializeField] private bool hideOnAwake = true;

    private void Awake()
    {
        if (showWhenSelected != null && hideOnAwake)
            showWhenSelected.SetActive(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (showWhenSelected != null)
            showWhenSelected.SetActive(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (showWhenSelected != null)
            showWhenSelected.SetActive(false);
    }

    private void OnDisable()
    {
        // Safety: if the menu disables, don't leave the marker stuck on.
        if (showWhenSelected != null)
            showWhenSelected.SetActive(false);
    }
}
