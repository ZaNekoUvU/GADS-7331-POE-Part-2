using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Full-screen pause overlay (Esc). Pauses scaled time; UI matches the main menu (FF-style blue panel).
/// </summary>
[DisallowMultipleComponent]
public class PauseMenuController : MonoBehaviour
{
    public const string DefaultMainMenuSceneName = "Main Menu";

    public static PauseMenuController Instance { get; private set; }

    /// <summary>True while the pause overlay is visible and scaled time is frozen.</summary>
    public static bool IsOpen { get; private set; }

    private static string _pendingMainMenuSceneName = DefaultMainMenuSceneName;

    [Tooltip("Scene name exactly as in File > Build Settings (e.g. Main Menu).")]
    [SerializeField] private string mainMenuSceneName = DefaultMainMenuSceneName;

    private UIDocument _document;
    private VisualElement _overlay;
    private VisualElement _menuPanel;
    private Label _subtitleLabel;
    private VisualElement _controlsBody;
    private VisualElement _commandsList;
    private readonly List<FfStyleMenuUi.MenuRow> _entries = new();
    private int _selectedIndex;
    private float _timeScaleBeforePause;
    private bool _showingControls;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfMissing()
    {
        if (Instance != null || FindAnyObjectByType<PauseMenuController>() != null)
            return;

        var go = new GameObject($"[{nameof(PauseMenuController)}]");
        go.AddComponent<PauseMenuController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!string.IsNullOrWhiteSpace(_pendingMainMenuSceneName))
            mainMenuSceneName = _pendingMainMenuSceneName.Trim();

        BuildUi();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance != this)
            return;

        if (IsOpen)
            Time.timeScale = _timeScaleBeforePause;

        Instance = null;
        IsOpen = false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        ForceCloseAndResetTime();
    }

    private void Update()
    {
        if (IsNonPausableScene() || TutorialIntroUI.IsOpen)
            return;

        if (IsOpen)
            HandleMenuInput();

        if (!WasTogglePausePressedThisFrame())
            return;

        if (IsOpen)
        {
            if (_showingControls)
                ShowMainView();
            else
                Resume();
        }
        else
            Open();
    }

    private void HandleMenuInput()
    {
        if (_entries.Count == 0)
            return;

        var kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
            MoveSelection(-1);
        else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)
            MoveSelection(1);
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame)
            ActivateSelection();
    }

    private static bool IsNonPausableScene()
    {
        var scene = SceneManager.GetActiveScene();
        return scene.IsValid() && scene.name == DefaultMainMenuSceneName;
    }

    private static bool WasTogglePausePressedThisFrame()
    {
        var k = Keyboard.current;
        return k != null && k.escapeKey.wasPressedThisFrame;
    }

    private void Open()
    {
        if (IsOpen)
            return;

        IsOpen = true;
        _showingControls = false;
        _timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        _selectedIndex = 0;
        ShowMainView();
        SetOverlayVisible(true);
    }

    private void Resume()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        _showingControls = false;
        Time.timeScale = _timeScaleBeforePause > 0.01f ? _timeScaleBeforePause : 1f;
        FfStyleMenuUi.ReleaseFocus(_document);
        SetOverlayVisible(false);
    }

    private void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        IsOpen = false;
        FfStyleMenuUi.ReleaseFocus(_document);
        SetOverlayVisible(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void LoadMainMenu()
    {
        var target = string.IsNullOrWhiteSpace(mainMenuSceneName)
            ? DefaultMainMenuSceneName
            : mainMenuSceneName.Trim();

        if (!Application.CanStreamedLevelBeLoaded(target))
        {
            Debug.LogWarning($"{nameof(PauseMenuController)}: Main menu scene '{target}' is not in Build Settings.", this);
            return;
        }

        ForceCloseAndResetTime();
        SceneManager.LoadScene(target);
    }

    private static void QuitApplication()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Closes pause UI and restores gameplay time (call before loading another scene).</summary>
    public static void ForceCloseAndResetTime()
    {
        IsOpen = false;
        Time.timeScale = 1f;

        if (Instance == null)
            return;

        Instance._timeScaleBeforePause = 1f;
        FfStyleMenuUi.ReleaseFocus(Instance._document);
        Instance.SetOverlayVisible(false);
    }

    /// <summary>Assign main-menu scene at runtime when using the auto-created pause object (no inspector).</summary>
    public static void SetMainMenuScene(string sceneName)
    {
        _pendingMainMenuSceneName = string.IsNullOrWhiteSpace(sceneName)
            ? DefaultMainMenuSceneName
            : sceneName.Trim();

        if (Instance == null)
            return;

        Instance.mainMenuSceneName = _pendingMainMenuSceneName;
        if (IsOpen && !Instance._showingControls)
            Instance.ShowMainView();
    }

    private void ShowMainView()
    {
        _showingControls = false;

        if (_subtitleLabel != null)
            _subtitleLabel.text = "— Paused —";

        if (_controlsBody != null)
            _controlsBody.style.display = DisplayStyle.None;

        if (_menuPanel != null)
        {
            _menuPanel.style.maxWidth = 360;
            _menuPanel.style.minWidth = 280;
        }

        RebuildEntries();
        _selectedIndex = 0;
        RefreshCommands();
    }

    private void ShowControlsView()
    {
        _showingControls = true;

        if (_subtitleLabel != null)
            _subtitleLabel.text = "— Controls —";

        if (_controlsBody != null)
        {
            _controlsBody.style.display = DisplayStyle.Flex;
            FfStyleMenuUi.RefreshControlReferenceRows(_controlsBody, GameControlsReference.Entries);
        }

        if (_menuPanel != null)
        {
            _menuPanel.style.maxWidth = 480;
            _menuPanel.style.minWidth = 360;
        }

        _entries.Clear();
        _entries.Add(new FfStyleMenuUi.MenuRow("Back", ShowMainView));
        _selectedIndex = 0;
        RefreshCommands();
    }

    private void RebuildEntries()
    {
        _entries.Clear();

        var mainMenuTarget = string.IsNullOrWhiteSpace(mainMenuSceneName)
            ? DefaultMainMenuSceneName
            : mainMenuSceneName.Trim();
        var mainMenuAvailable = Application.CanStreamedLevelBeLoaded(mainMenuTarget);

        _entries.Add(new FfStyleMenuUi.MenuRow("Continue", Resume));
        _entries.Add(new FfStyleMenuUi.MenuRow("Controls", ShowControlsView));
        _entries.Add(new FfStyleMenuUi.MenuRow("Restart", RestartCurrentScene));
        _entries.Add(new FfStyleMenuUi.MenuRow(
            mainMenuAvailable ? "Main menu" : "Main menu (add to build)",
            LoadMainMenu,
            mainMenuAvailable));
        _entries.Add(new FfStyleMenuUi.MenuRow("Quit game", QuitApplication));
    }

    private void BuildUi()
    {
        _document = GetComponent<UIDocument>();
        if (_document == null)
            _document = gameObject.AddComponent<UIDocument>();

        FfStyleMenuUi.ConfigureDocument(_document, 6000);
        _overlay = FfStyleMenuUi.BuildScreen(
            _document.rootVisualElement,
            "Back To The Forge",
            "— Paused —",
            out _commandsList);

        _menuPanel = _overlay.Q<VisualElement>("menu-panel");
        _subtitleLabel = _overlay.Q<Label>("menu-subtitle");

        _controlsBody = new VisualElement { name = "controls-body" };
        _controlsBody.style.marginBottom = 12;
        _controlsBody.style.display = DisplayStyle.None;
        _controlsBody.pickingMode = PickingMode.Ignore;

        if (_menuPanel != null && _commandsList != null)
            _menuPanel.Insert(_menuPanel.IndexOf(_commandsList), _controlsBody);

        ShowMainView();
        SetOverlayVisible(false);
    }

    private void RefreshCommands()
    {
        FfStyleMenuUi.RefreshCommandRows(
            _commandsList,
            _entries,
            _selectedIndex,
            index => _selectedIndex = index,
            _ => ActivateSelection());
    }

    private void MoveSelection(int delta)
    {
        if (_entries.Count == 0)
            return;

        var next = _selectedIndex;
        for (var i = 0; i < _entries.Count; i++)
        {
            next = (next + delta + _entries.Count) % _entries.Count;
            if (_entries[next].Enabled)
                break;
        }

        _selectedIndex = next;
        RefreshCommands();
    }

    private void ActivateSelection()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _entries.Count)
            return;

        var entry = _entries[_selectedIndex];
        if (!entry.Enabled)
            return;

        entry.OnSelect?.Invoke();
    }

    private void SetOverlayVisible(bool visible)
    {
        if (_overlay == null)
            return;

        _overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
