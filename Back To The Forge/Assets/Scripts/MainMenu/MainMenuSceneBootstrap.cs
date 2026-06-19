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

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.name == MainMenuSceneName)
        {
            PauseMenuController.ForceCloseAndResetTime();
            GameAudioController.RefreshMusicForActiveScene();
        }
        else
            GameplaySessionReset.PrepareForGameplayScene();
    }
}
