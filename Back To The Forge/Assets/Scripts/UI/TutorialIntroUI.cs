using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Full-screen tutorial when exploration starts (after main menu). Matches pause / main menu UI.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialIntroUI : MonoBehaviour
{
    public static TutorialIntroUI Instance { get; private set; }

    public static bool IsOpen { get; private set; }

    private const int UiSortOrder = 6100;

    [SerializeField] private string explorationSceneName = "Exploration Scene";

    private UIDocument _document;
    private VisualElement _overlay;
    private VisualElement _menuPanel;
    private VisualElement _body;
    private VisualElement _commandsList;
    private readonly List<FfStyleMenuUi.MenuRow> _entries = new();
    private int _selectedIndex;
    private float _timeScaleBeforeOpen = 1f;
    private bool _pendingShow;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfMissing()
    {
        if (Instance != null || FindAnyObjectByType<TutorialIntroUI>() != null)
            return;

        var go = new GameObject($"[{nameof(TutorialIntroUI)}]");
        go.AddComponent<TutorialIntroUI>();
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
        BuildUi();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance != this)
            return;

        if (IsOpen)
            Time.timeScale = _timeScaleBeforeOpen;

        Instance = null;
        IsOpen = false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || !scene.IsValid())
            return;

        if (scene.name == PauseMenuController.DefaultMainMenuSceneName)
        {
            ForceClose();
            return;
        }

        if (!IsExplorationScene(scene.name))
            return;

        _pendingShow = true;
        StartCoroutine(ShowWhenReadyRoutine());
    }

    private IEnumerator ShowWhenReadyRoutine()
    {
        yield return null;
        yield return null;

        if (!_pendingShow)
            yield break;

        _pendingShow = false;

        if (!IsExplorationScene(SceneManager.GetActiveScene().name))
            yield break;

        Open();
    }

    private bool IsExplorationScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(explorationSceneName))
            return true;

        return sceneName == explorationSceneName.Trim();
    }

    public static void ForceClose()
    {
        if (Instance == null)
        {
            IsOpen = false;
            return;
        }

        Instance._pendingShow = false;
        Instance.Close();
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        HandleMenuInput();

        if (WasBeginPressedThisFrame())
            ActivateSelection();
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
    }

    private static bool WasBeginPressedThisFrame()
    {
        var kb = Keyboard.current;
        if (kb == null)
            return false;

        return kb.enterKey.wasPressedThisFrame
               || kb.numpadEnterKey.wasPressedThisFrame
               || kb.zKey.wasPressedThisFrame;
    }

    private void Open()
    {
        if (IsOpen)
            return;

        PauseMenuController.ForceCloseAndResetTime();

        IsOpen = true;
        _timeScaleBeforeOpen = Time.timeScale;
        Time.timeScale = 0f;
        _selectedIndex = 0;

        if (_body != null)
            FfStyleMenuUi.RefreshInfoParagraphs(_body, TutorialIntroReference.Paragraphs);

        RebuildEntries();
        RefreshCommands();
        SetOverlayVisible(true);
    }

    private void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        Time.timeScale = _timeScaleBeforeOpen > 0f ? _timeScaleBeforeOpen : 1f;
        FfStyleMenuUi.ReleaseFocus(_document);
        SetOverlayVisible(false);

        var player = PlayerMovement2D.Instance ?? FindAnyObjectByType<PlayerMovement2D>();
        player?.EnsureInputReady();
    }

    private void RebuildEntries()
    {
        _entries.Clear();
        _entries.Add(new FfStyleMenuUi.MenuRow(TutorialIntroReference.BeginLabel, Close));
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

        _selectedIndex = (_selectedIndex + delta + _entries.Count) % _entries.Count;
        RefreshCommands();
    }

    private void ActivateSelection()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _entries.Count)
            return;

        _entries[_selectedIndex].OnSelect?.Invoke();
    }

    private void BuildUi()
    {
        _document = GetComponent<UIDocument>();
        if (_document == null)
            _document = gameObject.AddComponent<UIDocument>();

        FfStyleMenuUi.ConfigureDocument(_document, UiSortOrder);
        _overlay = FfStyleMenuUi.BuildScreen(
            _document.rootVisualElement,
            "Back To The Forge",
            "— How to Play —",
            out _commandsList);

        _menuPanel = _overlay.Q<VisualElement>("menu-panel");

        if (_menuPanel != null)
        {
            _menuPanel.style.minWidth = 380;
            _menuPanel.style.maxWidth = 520;
        }

        _body = new VisualElement { name = "tutorial-body" };
        _body.style.marginBottom = 12;
        _body.pickingMode = PickingMode.Ignore;

        if (_menuPanel != null && _commandsList != null)
            _menuPanel.Insert(_menuPanel.IndexOf(_commandsList), _body);

        SetOverlayVisible(false);
    }

    private void SetOverlayVisible(bool visible)
    {
        if (_overlay != null)
            _overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
