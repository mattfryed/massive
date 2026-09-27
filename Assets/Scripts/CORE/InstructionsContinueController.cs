using UnityEngine;

public class InstructionsContinueController : MonoBehaviour
{
    [Tooltip("Seconds to wait before continuing to gameplay.")]
    public float delayTime = 1.0f;

    private void Start()
    {
        Invoke(nameof(GoToSelectedLevel), delayTime);
    }

    private void GoToSelectedLevel()
    {
        SceneFlow.GoToSelectedGameplay();
    }
}
