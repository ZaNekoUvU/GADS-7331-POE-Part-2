using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Main menu with combat-style UI Toolkit panels over the combat field background.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInMainMenuScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != MainMenuSceneBootstrap.MainMenuSceneName)
            return;

        if (FindAnyObjectByType<MainMenuController>() != null)
            return;

        var go = new GameObject("MainMenu");
        go.AddComponent<MainMenuController>();
    }

    [SerializeField] private string playSceneName = "Exploration Scene";
    [SerializeField] private Sprite backgroundSprite;

    private UIDocument _document;
    private VisualElement _commandsList;
    private readonly List<FfStyleMenuUi.MenuRow> _entries = new();
    private int _selectedIndex;

    private void Awake()
    {
        Time.timeScale = 1f;
        ResolveBackgroundSprite();
    }

    private void Start()
    {
        StartCoroutine(InitializeUiRoutine());
    }

    private void Update()
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

    private IEnumerator InitializeUiRoutine()
    {
        yield return null;

        _document = GetComponent<UIDocument>();
        if (_document == null)
            _document = gameObject.AddComponent<UIDocument>();

        FfStyleMenuUi.ConfigureDocument(_document, 100);
        FfStyleMenuUi.BuildScreen(
            _document.rootVisualElement,
            "Back To The Forge",
            "— Main Menu —",
            out _commandsList);

        BuildMenuEntries();
        RefreshCommands();
        GameAudioController.RefreshMusicForActiveScene();
    }

    private void ResolveBackgroundSprite()
    {
        if (backgroundSprite != null)
            return;

#if UNITY_EDITOR
        backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MAIN BACKGROUND.png");
#endif
    }

    private void BuildMenuEntries()
    {
        _entries.Clear();
        _entries.Add(new FfStyleMenuUi.MenuRow("New Game", StartGame));
        _entries.Add(new FfStyleMenuUi.MenuRow("Quit", QuitGame));
        _selectedIndex = 0;
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

    private void OnDestroy()
    {
        FfStyleMenuUi.ReleaseFocus(_document);
    }

    private void StartGame()
    {
        if (string.IsNullOrWhiteSpace(playSceneName))
        {
            Debug.LogError($"{nameof(MainMenuController)}: Play scene name is empty.", this);
            return;
        }

        FfStyleMenuUi.ReleaseFocus(_document);
        if (_document != null)
            _document.enabled = false;

        GameplaySessionReset.PrepareForGameplayScene();
        SceneManager.LoadScene(playSceneName.Trim());
    }

    private static void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
