using UnityEngine;

public class ChooseModeScript : MonoBehaviour
{
    // Hook this up to your UI buttons.
    // If true = 2v2, else 1v1.
public void MoveToNextScene(bool is2v2)
{
    GameFlowContext.EnsureExists();
    GameFlowContext.Instance.SetMode(is2v2 ? GameMode.TwoVTwo : GameMode.OneVOne);

    // optional: clear previous level selection when starting fresh
    GameFlowContext.Instance.ClearSelection();

    SceneFlow.GoToHowToPlay(); 
}
}
