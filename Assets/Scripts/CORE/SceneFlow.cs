using UnityEngine.SceneManagement;

public static class SceneFlow
{
    // Keep these centralized so you never “magic string” them across scripts.
    public const string InstructionsScene = "S-0_INSTRUCTIONS";
    public const string HowToPlayScene = "S-0_HOW-TO-PLAY";
    public const string LevelSelectScene = "S-0_LEVEL-SELECT NEW"; // change if needed
    public const string ChooseModeScene  = "S-0_ATTRACT";      // change if needed

    public static void GoToChooseMode() => SceneManager.LoadScene(ChooseModeScene);
    public static void GoToHowToPlay() => SceneManager.LoadScene(HowToPlayScene);
    public static void GoToLevelSelect() => SceneManager.LoadScene(LevelSelectScene);
    public static void GoToInstructions() => SceneManager.LoadScene(InstructionsScene);

    public const string PostGameScene = "S-0_POSTGAME"; // your post-game scene name

    public static void GoToPostGame() => UnityEngine.SceneManagement.SceneManager.LoadScene(PostGameScene);


    public static void GoToSelectedGameplay()
    {
        GameFlowContext.EnsureExists();
        string scene = GameFlowContext.Instance.SelectedLevel != null
    ? GameFlowContext.Instance.SelectedLevel.SceneName
    : null;


        if (string.IsNullOrEmpty(scene))
        {
            // No selection = flow error; decide your fallback policy here.
            // I’m choosing: go back to level select.
            SceneManager.LoadScene(HowToPlayScene);
            return;
        }

        SceneManager.LoadScene(scene);
    }
}
