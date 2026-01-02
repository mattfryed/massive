using UnityEngine;

/// <summary>
/// Marker + routing point for Symmetry Knot "end knot".
/// Also receives mass tick events (optional).
/// </summary>
public class SymmetryKnotGoalMouth : MonoBehaviour
{
    [Header("Identity")]
    public int teamID = 1;

    [Header("Scoring")]
    [SerializeField] private ScoreSphereScript scoreSphere;
    [SerializeField] private bool autoFindScoreSphere = true;



    private void Awake()
    {
        if (scoreSphere == null)
            scoreSphere = GetComponentInParent<ScoreSphereScript>(true);

        // Fallback: find by teamID (runs once, OK)
        if (scoreSphere == null)
        {
            var all = FindObjectsByType<ScoreSphereScript>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].teamID == teamID)
                {
                    scoreSphere = all[i];
                    break;
                }
            }
        }
    }

    public void OnSymmetryKnotMass(float amount01)
    {
        if (scoreSphere != null)
            scoreSphere.AddScore01(amount01);
    }
}