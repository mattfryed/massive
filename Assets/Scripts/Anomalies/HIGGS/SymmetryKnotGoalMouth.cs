using UnityEngine;

/// <summary>
/// Marker + routing point for Symmetry Knot "end knot".
/// Also receives mass tick events (optional).
/// </summary>
public class SymmetryKnotGoalMouth : MonoBehaviour
{
    [Header("Identity")]
    public int teamID = 1;

    [Header("Flow Receive (optional)")]
    [Tooltip("If true, this component will log received mass ticks. Replace with real scoring later.")]
    public bool debugLogMass = false;

    // Called by SymmetryKnotController via SendMessage by default.
    public void OnSymmetryKnotMass(float amount)
    {
        if (debugLogMass)
            Debug.Log($"[SymmetryKnotGoalMouth] team={teamID} +{amount:0.000} mass", this);

        // TODO later: route into your actual team score / goal scripts.
    }
}
