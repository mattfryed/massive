using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class LevelSelectEventSystemBootstrap : MonoBehaviour
{
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private GameObject targetToSelect;
    [SerializeField] private bool keepSelected = true;

    private IEnumerator Start()
    {
        if (eventSystem == null) eventSystem = EventSystem.current;

        // Wait 1 frame so EventSystem applies First Selected, then override it.
        yield return null;

        ForceSelect();
    }

    private void Update()
    {
        if (!keepSelected) return;
        if (eventSystem == null || targetToSelect == null) return;

        if (eventSystem.currentSelectedGameObject != targetToSelect)
            eventSystem.SetSelectedGameObject(targetToSelect);
    }

    private void ForceSelect()
    {
        if (eventSystem != null && targetToSelect != null)
            eventSystem.SetSelectedGameObject(targetToSelect);
    }
}
