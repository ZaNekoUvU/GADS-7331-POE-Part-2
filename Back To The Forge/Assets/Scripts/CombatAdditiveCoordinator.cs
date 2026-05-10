using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Place in the exploration scene. Loads the combat scene additively and disables
/// the exploration camera and audio listener while combat is active.
/// </summary>
public class CombatAdditiveCoordinator : MonoBehaviour
{
    [SerializeField] private string combatSceneName = "Combat Scene";
    [SerializeField] private Camera explorationCamera;
    [SerializeField] private AudioListener explorationAudioListener;

    [Tooltip("If true, sets Time.timeScale to 0 while combat is shown. Combat logic must use unscaled time or set scale back to 1.")]
    [SerializeField] private bool pauseExplorationWithTimeScale;

    private AsyncOperation _loadOp;
    private float _savedTimeScale = 1f;

    /// <summary>True while the combat scene is loading or already loaded.</summary>
    public bool IsCombatActiveOrLoading
    {
        get
        {
            if (_loadOp != null)
                return true;

            if (string.IsNullOrEmpty(combatSceneName))
                return false;

            var scene = SceneManager.GetSceneByName(combatSceneName);
            return scene.IsValid() && scene.isLoaded;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    /// <summary>Call from encounter zones, UI, etc.</summary>
    public void BeginCombat()
    {
        if (_loadOp != null)
            return;

        if (string.IsNullOrEmpty(combatSceneName))
        {
            Debug.LogError($"{nameof(CombatAdditiveCoordinator)}: combat scene name is empty.", this);
            return;
        }

        _loadOp = SceneManager.LoadSceneAsync(combatSceneName, LoadSceneMode.Additive);
        if (_loadOp == null)
        {
            Debug.LogError(
                $"{nameof(CombatAdditiveCoordinator)}: Could not load '{combatSceneName}'. Add the scene to Build Settings and use the exact scene name (see the .unity file name).",
                this);
            return;
        }

        _loadOp.completed += _ =>
        {
            _loadOp = null;
            var loaded = SceneManager.GetSceneByName(combatSceneName);
            if (!loaded.IsValid() || !loaded.isLoaded)
            {
                Debug.LogError(
                    $"{nameof(CombatAdditiveCoordinator)}: Scene '{combatSceneName}' did not load. Check spelling — it must match the name in File > Build Settings.",
                    this);
            }
        };
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != combatSceneName)
            return;

        if (explorationCamera == null)
            explorationCamera = Camera.main;

        if (explorationAudioListener == null && explorationCamera != null)
            explorationAudioListener = explorationCamera.GetComponent<AudioListener>();

        SetExplorationOutputEnabled(false);

        if (pauseExplorationWithTimeScale)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (scene.name != combatSceneName)
            return;

        SetExplorationOutputEnabled(true);

        if (pauseExplorationWithTimeScale)
            Time.timeScale = _savedTimeScale;

        CombatSession.RaiseCombatEnded();
    }

    private void SetExplorationOutputEnabled(bool enabled)
    {
        if (explorationCamera != null)
            explorationCamera.enabled = enabled;

        if (explorationAudioListener != null)
            explorationAudioListener.enabled = enabled;
    }
}
