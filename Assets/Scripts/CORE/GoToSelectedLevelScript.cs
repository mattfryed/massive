using UnityEngine;

public class GoToSelectedLevelScript : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds to wait before loading the selected gameplay scene.")]
    public float delayTime = 1.0f;

    private void Start()
    {
        // Ensure flow context exists (selected level should already be set by Level Select)
        GameFlowContext.EnsureExists();

        Invoke(nameof(GoToLevel), delayTime);
    }

    private void GoToLevel()
    {
        // Loads the gameplay scene from the selected LevelDefinition
        SceneFlow.GoToSelectedGameplay();
    }
}

