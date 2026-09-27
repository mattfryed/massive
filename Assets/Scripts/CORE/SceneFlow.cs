using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneFlow
{
    public const string InstructionsScene = "S-0_INSTRUCTIONS";
    public const string HowToPlayScene = "S-0_HOW-TO-PLAY";
    public const string LevelSelectScene = "S-0_LEVEL-SELECT NEW";
    public const string ChooseModeScene = "S-0_ATTRACT";
    public const string PostGameScene = "S-0_POSTGAME";

    // Session-owned, so destroying the instructions UI cannot lose the operation.
    private static AsyncOperation gameplayPreload;
    private static string preloadedScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        gameplayPreload = null;
        preloadedScene = null;
    }

    public static bool IsSelectedGameplayReady =>
        gameplayPreload != null && preloadedScene == SelectedGameplayScene() &&
        gameplayPreload.progress >= 0.9f;

    public static bool PreloadSelectedGameplay()
    {
        string scene = SelectedGameplayScene();
        if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogWarning($"[SceneFlow] Cannot preload selected gameplay scene '{scene}'. Check the level definition and Build Settings.");
            return false;
        }

        // A scene operation cannot be cancelled. Never enqueue a second one
        // behind an operation whose activation is being held.
        if (gameplayPreload != null)
            return preloadedScene == scene;

        try
        {
            var operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            if (operation == null) return false;
            operation.allowSceneActivation = false;
            gameplayPreload = operation;
            preloadedScene = scene;
            operation.completed += _ =>
            {
                if (gameplayPreload == operation) ResetStatics();
            };
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SceneFlow] Could not preload '{scene}': {exception.Message}");
            ResetStatics();
            return false;
        }
    }

    public static void GoToChooseMode() => LoadScene(ChooseModeScene);
    public static void GoToHowToPlay() => LoadScene(HowToPlayScene);
    public static void GoToLevelSelect() => LoadScene(LevelSelectScene);
    public static void GoToInstructions() => LoadScene(InstructionsScene);
    public static void GoToPostGame() => LoadScene(PostGameScene);

    public static void GoToSelectedGameplay()
    {
        string scene = SelectedGameplayScene();
        if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene))
        {
            GoToLevelSelect();
            return;
        }

        if (gameplayPreload != null && preloadedScene == scene)
        {
            // Also accepts an early confirmation: Unity activates when ready.
            gameplayPreload.allowSceneActivation = true;
            return;
        }

        LoadScene(scene);
    }

    private static string SelectedGameplayScene()
    {
        GameFlowContext.EnsureExists();
        var level = GameFlowContext.Instance.SelectedLevel;
        return level != null ? level.SceneName : null;
    }

    private static void LoadScene(string scene)
    {
        if (gameplayPreload != null)
        {
            // Exceptional navigation away from instructions. Unity has no scene
            // cancellation API; synchronous LoadScene drains pending operations
            // and replaces them with this destination instead of deadlocking an
            // async load behind allowSceneActivation=false.
            gameplayPreload.allowSceneActivation = true;
            ResetStatics();
        }
        SceneManager.LoadScene(scene);
    }
}
