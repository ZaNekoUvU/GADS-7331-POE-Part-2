using UnityEngine;

/// <summary>
/// Registers the main menu scene name for the pause menu when gameplay scenes load.
/// </summary>
public static class MainMenuSceneBootstrap
{
    public const string MainMenuSceneName = PauseMenuController.DefaultMainMenuSceneName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterPauseMenuTarget()
    {
        PauseMenuController.SetMainMenuScene(MainMenuSceneName);
        PauseMenuController.ForceCloseAndResetTime();
    }
}
