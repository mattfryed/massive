using UnityEngine;

public class LevelIcon : MonoBehaviour
{
    [Header("Optional Visual Hooks")]
    [SerializeField] private Transform visualsRoot;
    [SerializeField] private float selectedScale = 1.15f;
    [SerializeField] private float scaleLerpSpeed = 10f;

    [Tooltip("Optional: expensive components to only enable when selected.")]
    [SerializeField] private Behaviour[] enableOnlyWhenSelected;

    private bool _isSelected;
    private Vector3 _baseScale;

    private void Awake()
    {
        if (visualsRoot == null) visualsRoot = transform;
        _baseScale = visualsRoot.localScale;
        ApplySelected(false, instant: true);
    }

    private void Update()
    {
        if (visualsRoot == null) return;
        Vector3 target = _baseScale * (_isSelected ? selectedScale : 1f);
        visualsRoot.localScale = Vector3.Lerp(visualsRoot.localScale, target, 1f - Mathf.Exp(-scaleLerpSpeed * Time.unscaledDeltaTime));
    }

    public void ApplySelected(bool selected, bool instant)
    {
        _isSelected = selected;

        if (enableOnlyWhenSelected != null)
        {
            for (int i = 0; i < enableOnlyWhenSelected.Length; i++)
            {
                if (enableOnlyWhenSelected[i] != null)
                    enableOnlyWhenSelected[i].enabled = selected;
            }
        }

        if (instant && visualsRoot != null)
        {
            visualsRoot.localScale = _baseScale * (selected ? selectedScale : 1f);
        }
    }
}