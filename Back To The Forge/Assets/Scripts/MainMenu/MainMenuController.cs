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

    private const string StyleSheetResource = "Combat/CombatBattleHud";
    private const string ThemeResource = "Combat/UnityDefaultRuntimeTheme";

    [SerializeField] private string playSceneName = "Exploration Scene";
    [SerializeField] private Sprite backgroundSprite;

    private UIDocument _document;
    private VisualElement _commandsList;
    private readonly List<MenuEntry> _entries = new();
    private int _selectedIndex;

    private struct MenuEntry
    {
        public string Label;
        public Action OnSelect;
    }

    private static readonly Color TextColor = new(1f, 1f, 1f, 1f);

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
        EnsurePanelSettings();
        BuildMenuEntries();
        BuildLayout();
        RefreshCommands();
    }

    private void ResolveBackgroundSprite()
    {
        if (backgroundSprite != null)
            return;

#if UNITY_EDITOR
        backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MAIN BACKGROUND.png");
#endif
    }

    private void EnsurePanelSettings()
    {
        _document = GetComponent<UIDocument>();
        if (_document == null)
            _document = gameObject.AddComponent<UIDocument>();

        if (_document.panelSettings == null)
        {
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "MainMenuPanelSettings";
            TryAssignDefaultTheme(panelSettings);
            _document.panelSettings = panelSettings;
        }

        var ps = _document.panelSettings;
        TryAssignDefaultTheme(ps);
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution = new Vector2Int(800, 600);
        ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        ps.match = 0.5f;
        ps.sortingOrder = 100;

        _document.visualTreeAsset = null;
        _document.sortingOrder = 100;
    }

    private static void TryAssignDefaultTheme(PanelSettings panelSettings)
    {
        if (panelSettings == null || panelSettings.themeStyleSheet != null)
            return;

        var theme = Resources.Load<ThemeStyleSheet>(ThemeResource);
#if UNITY_EDITOR
        if (theme == null)
        {
            theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Packages/com.unity.ui/PackageResources/StyleSheets/UnityThemes/UnityDefaultRuntimeTheme.tss");
        }
#endif
        if (theme != null)
            panelSettings.themeStyleSheet = theme;
    }

    private void BuildMenuEntries()
    {
        _entries.Clear();
        _entries.Add(new MenuEntry { Label = "New Game", OnSelect = StartGame });
        _entries.Add(new MenuEntry { Label = "Quit", OnSelect = QuitGame });
        _selectedIndex = 0;
    }

    private void BuildLayout()
    {
        var root = _document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1f;
        root.pickingMode = PickingMode.Ignore;

        var styleSheet = Resources.Load<StyleSheet>(StyleSheetResource);
        if (styleSheet != null)
            root.styleSheets.Add(styleSheet);

        var overlay = new VisualElement { name = "menu-overlay" };
        overlay.style.flexGrow = 1f;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.alignItems = Align.Center;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.2f);
        overlay.pickingMode = PickingMode.Position;
        root.Add(overlay);

        var panel = new VisualElement { name = "menu-panel" };
        panel.AddToClassList("hud-panel");
        panel.style.minWidth = 280;
        panel.style.maxWidth = 360;
        panel.style.paddingTop = 16;
        panel.style.paddingBottom = 16;
        panel.style.paddingLeft = 20;
        panel.style.paddingRight = 20;
        overlay.Add(panel);

        var title = new Label("Back To The Forge");
        title.name = "menu-title";
        ApplyLabelStyle(title, 22f, true);
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.style.marginBottom = 12;
        panel.Add(title);

        var subtitle = new Label("— Main Menu —");
        ApplyLabelStyle(subtitle, 13f, false);
        subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
        subtitle.style.marginBottom = 14;
        subtitle.style.color = new Color(0.85f, 0.85f, 0.95f, 1f);
        panel.Add(subtitle);

        _commandsList = new VisualElement { name = "commands-list" };
        _commandsList.AddToClassList("command-list");
        _commandsList.pickingMode = PickingMode.Position;
        panel.Add(_commandsList);
    }

    private void RefreshCommands()
    {
        if (_commandsList == null)
            return;

        _commandsList.Clear();

        if (_selectedIndex < 0 || _selectedIndex >= _entries.Count)
            _selectedIndex = 0;

        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            var index = i;

            var row = new VisualElement();
            row.AddToClassList("command-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.pickingMode = PickingMode.Position;
            row.focusable = true;

            if (i == _selectedIndex)
            {
                row.AddToClassList("command-row--selected");
                row.style.backgroundColor = new Color(1f, 1f, 1f, 0.25f);
            }

            var cursor = new Label("\u25ba");
            cursor.AddToClassList("command-cursor");
            ApplyLabelStyle(cursor, 14f, false, TextColor);
            cursor.style.width = 18;
            cursor.style.visibility = i == _selectedIndex ? Visibility.Visible : Visibility.Hidden;
            row.Add(cursor);

            var label = new Label(entry.Label);
            label.AddToClassList("command-label");
            ApplyLabelStyle(label, 17f, true, TextColor);
            row.Add(label);

            row.RegisterCallback<ClickEvent>(_ => SelectAndActivate(index));
            row.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button == 0)
                    SelectAndActivate(index);
            });

            _commandsList.Add(row);
        }
    }

    private static void ApplyLabelStyle(Label label, float fontSize, bool bold, Color? color = null)
    {
        label.style.color = color ?? TextColor;
        label.style.fontSize = fontSize;
        label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
    }

    private void MoveSelection(int delta)
    {
        if (_entries.Count == 0)
            return;

        _selectedIndex = (_selectedIndex + delta + _entries.Count) % _entries.Count;
        RefreshCommands();
    }

    private void SelectAndActivate(int index)
    {
        _selectedIndex = index;
        ActivateSelection();
    }

    private void ActivateSelection()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _entries.Count)
            return;

        _entries[_selectedIndex].OnSelect?.Invoke();
    }

    private void StartGame()
    {
        if (string.IsNullOrWhiteSpace(playSceneName))
        {
            Debug.LogError($"{nameof(MainMenuController)}: Play scene name is empty.", this);
            return;
        }

        PauseMenuController.ForceCloseAndResetTime();
        Time.timeScale = 1f;
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
