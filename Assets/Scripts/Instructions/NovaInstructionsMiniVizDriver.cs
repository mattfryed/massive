using UnityEngine;
using UnityEngine.UI;

public class NovaInstructionsMiniVizDriver : MonoBehaviour
{
    [SerializeField] private NovaCoreMinigame uiMinigame;
    [SerializeField] private NovaCoreGPU coreGPU; // optional

    [Header("Optional Core Reveal")]
    [Range(0f, 1f)]
    [SerializeField] private float coreReveal01 = 1f;
    

    private void Start()
    {
        // Helps when this prefab lives under layout groups:
        Canvas.ForceUpdateCanvases();

        if (coreGPU != null)
            coreGPU.SetReveal01(coreReveal01);

        if (uiMinigame != null)
            uiMinigame.BeginInstructionsPreview();
    }
}
