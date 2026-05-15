using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Clears pause/dialogue blockers and restores player input after leaving the main menu or loading a gameplay scene.
/// </summary>
public static class GameplaySessionReset
{
    private static GameplaySessionResetRunner _runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        if (!scene.IsValid() || scene.name == PauseMenuController.DefaultMainMenuSceneName)
            return;

        PrepareForGameplayScene();
        ScheduleDelayedPrepare();
    }

    /// <summary>Call before loading exploration from the main menu.</summary>
    public static void PrepareForGameplayScene()
    {
        PauseMenuController.ForceCloseAndResetTime();
        SimpleRpgDialogueUI.ForceCloseAll();
        ForgeQuestChoiceUI.ForceCloseAll();
        Time.timeScale = 1f;

        var player = Object.FindAnyObjectByType<PlayerMovement2D>();
        if (player != null)
            player.EnsureInputReady();
    }

    private static void ScheduleDelayedPrepare()
    {
        if (_runner == null)
        {
            var go = new GameObject($"[{nameof(GameplaySessionReset)}]");
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<GameplaySessionResetRunner>();
        }

        _runner.RunPrepareNextFrames();
    }

    private sealed class GameplaySessionResetRunner : MonoBehaviour
    {
        public void RunPrepareNextFrames()
        {
            StopAllCoroutines();
            StartCoroutine(PrepareRoutine());
        }

        private IEnumerator PrepareRoutine()
        {
            yield return null;
            PrepareForGameplayScene();

            yield return null;
            PrepareForGameplayScene();
        }
    }
}
