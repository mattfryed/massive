using TMPro;
using UnityEngine;

public class NovaCoreTeamResultView : MonoBehaviour
{
    [Header("TMP Labels")]
    [SerializeField] private TMP_Text teamNameLabel;
    [SerializeField] private TMP_Text particlesLabel;
    [SerializeField] private TMP_Text scoreAwardedLabel;

    [Header("Formatting")]
    [Tooltip("Displayed as: (scoreAwarded01 * scoreDisplayMultiplier). Example: 0.12 * 100 = 12.")]
    [SerializeField] private float scoreDisplayMultiplier = 100f;

    [SerializeField] private string particlesFormat = "{0}";
    [SerializeField] private string scoreFormat = "+{0:0}";

    public void Set(string teamName, int particlesScored, float scoreAwarded01)
    {
        if (teamNameLabel != null) teamNameLabel.text = teamName;

        if (particlesLabel != null)
            particlesLabel.text = string.Format(particlesFormat, particlesScored);

        if (scoreAwardedLabel != null)
        {
            float displayScore = scoreAwarded01 * scoreDisplayMultiplier;
            scoreAwardedLabel.text = string.Format(scoreFormat, displayScore);
        }
    }
}
