using UnityEngine;

public class LevelSelectUIBinder : MonoBehaviour
{
    [SerializeField] private LevelCarouselController carousel;
    [SerializeField] private LevelSelectUIController ui;

    private void Awake()
    {
        if (carousel == null) carousel = FindFirstObjectByType<LevelCarouselController>();
        if (ui == null) ui = FindFirstObjectByType<LevelSelectUIController>();

        if (ui != null && carousel != null)
            ui.Bind(carousel);
    }
}