using UnityEngine;

public class InstructionsContinueController : MonoBehaviour
{
    [Tooltip("Seconds to wait before continuing to gameplay.")]
    public float delayTime = 1.0f;

    [Header("Optional Music Stop")]
    [SerializeField] private string titleMusicManagerTag = "TitleMusicManager";

    private void Start()
    {
        // Optional: stop title music (preserves your previous behavior)
        var tmm = GameObject.FindWithTag(titleMusicManagerTag);
        if (tmm != null)
        {
            var mm = tmm.GetComponent<MusicManagerScript>();
            if (mm != null) mm.StopMusic();
        }

        Invoke(nameof(GoToSelectedLevel), delayTime);
    }

    private void GoToSelectedLevel()
    {
        SceneFlow.GoToSelectedGameplay();
    }
}
