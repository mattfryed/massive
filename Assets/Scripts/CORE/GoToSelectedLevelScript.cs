using UnityEngine;

public class GoToSelectedLevelScript : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds to wait before loading the selected gameplay scene.")]
    public float delayTime = 1.0f;

    [Header("Optional Music Stop")]
    [SerializeField] private string titleMusicManagerTag = "TitleMusicManager";

    private void Start()
    {
        // Stop title music (mirrors old behavior, but null-safe)
        var tmm = GameObject.FindWithTag(titleMusicManagerTag);
        if (tmm != null)
        {
            var mm = tmm.GetComponent<MusicManagerScript>();
            if (mm != null) mm.StopMusic();
        }

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

