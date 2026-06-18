using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Forge / NPC choice overlay using the same FF-style command list as pause and main menu.
/// </summary>
public class ForgeQuestChoiceUI : MonoBehaviour
{
    public static ForgeQuestChoiceUI Instance { get; private set; }

    public int LastChoice { get; private set; } = -1;

    public static bool IsBlockingGameplay { get; private set; }

    private UIDocument _document;
    private VisualElement _overlay;
    private VisualElement _commandsList;
    private readonly List<FfStyleMenuUi.MenuRow> _rows = new();
    private int _selectedIndex;
    private int? _picked;

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
        SetOverlayVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static ForgeQuestChoiceUI GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        var existing = FindAnyObjectByType<ForgeQuestChoiceUI>();
        if (existing != null)
            return existing;

        var go = new GameObject($"[{nameof(ForgeQuestChoiceUI)}]");
        return go.AddComponent<ForgeQuestChoiceUI>();
    }

    public static void ForceCloseAll()
    {
        IsBlockingGameplay = false;

        if (Instance == null)
            return;

        Instance._picked = null;
        Instance.SetOverlayVisible(false);
    }

    public IEnumerator RunRoutine(string buttonAText, string buttonBText, string buttonCText = null)
    {
        var labels = new List<string>(3) { buttonAText, buttonBText };
        if (!string.IsNullOrEmpty(buttonCText))
            labels.Add(buttonCText);

        yield return RunChoiceListRoutine(labels, "— Choose —", includeCancel: false);
    }

    /// <summary>Shows a FF-style list; <see cref="LastChoice"/> is the picked index, or -1 if cancelled.</summary>
    public IEnumerator RunChoiceListRoutine(
        IReadOnlyList<string> optionLabels,
        string subtitle = "— Choose —",
        bool includeCancel = true)
    {
        BuildUi(subtitle);
        LastChoice = -1;
        _picked = null;
        _selectedIndex = 0;

        _rows.Clear();
        if (optionLabels != null)
        {
            for (var i = 0; i < optionLabels.Count; i++)
            {
                var index = i;
                _rows.Add(new FfStyleMenuUi.MenuRow(optionLabels[i], () => _picked = index));
            }
        }

        if (includeCancel)
            _rows.Add(new FfStyleMenuUi.MenuRow("Cancel", () => _picked = -1));

        if (_rows.Count == 0)
            yield break;

        RefreshCommands();
        IsBlockingGameplay = true;

        try
        {
            SetOverlayVisible(true);
            yield return new WaitUntil(() => _picked.HasValue);

            LastChoice = _picked.Value;
            _picked = null;
            SetOverlayVisible(false);
        }
        finally
        {
            IsBlockingGameplay = false;
        }
    }

    private void Update()
    {
        if (!IsBlockingGameplay || _overlay == null || _overlay.style.display == DisplayStyle.None)
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
        else if (kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame)
            _picked = -1;
    }

    private void BuildUi(string subtitle = "— Choose —")
    {
        _document = GetComponent<UIDocument>();
        if (_document == null)
            _document = gameObject.AddComponent<UIDocument>();

        FfStyleMenuUi.ConfigureDocument(_document, 5500);
        _overlay = FfStyleMenuUi.BuildChoiceOverlay(
            _document.rootVisualElement,
            subtitle,
            out _commandsList);
    }

    private void RefreshCommands()
    {
        FfStyleMenuUi.RefreshCommandRows(
            _commandsList,
            _rows,
            _selectedIndex,
            index => _selectedIndex = index,
            _ => ActivateSelection());
    }

    private void MoveSelection(int delta)
    {
        if (_rows.Count == 0)
            return;

        var next = _selectedIndex;
        for (var i = 0; i < _rows.Count; i++)
        {
            next = (next + delta + _rows.Count) % _rows.Count;
            if (_rows[next].Enabled)
                break;
        }

        _selectedIndex = next;
        RefreshCommands();
    }

    private void ActivateSelection()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
            return;

        var row = _rows[_selectedIndex];
        if (!row.Enabled)
            return;

        row.OnSelect?.Invoke();
    }

    private void SetOverlayVisible(bool visible)
    {
        if (_overlay != null)
            _overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
