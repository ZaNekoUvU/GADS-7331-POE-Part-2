using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// FF-style dialogue overlay (speaker + line). UI Toolkit panel matches pause / main menu.
/// Advance with Interact (same binding as NPCs / blacksmith).
/// </summary>
[DisallowMultipleComponent]
public class SimpleRpgDialogueUI : MonoBehaviour
{
    public static SimpleRpgDialogueUI Instance { get; private set; }

    public static bool IsDialogueOpen { get; private set; }

    public static int InteractConsumedByDialogueFrame { get; private set; } = -1;

    [SerializeField] private InputActionReference advanceAction;

    private UIDocument _document;
    private VisualElement _dialogueHost;
    private Label _speakerLabel;
    private Label _lineLabel;
    private bool _advanceAllowed = true;

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
        SetVisible(false);
    }

    private void OnEnable()
    {
        if (advanceAction != null)
            advanceAction.action.Enable();
    }

    private void OnDisable()
    {
        if (advanceAction != null)
            advanceAction.action.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            IsDialogueOpen = false;
            InteractConsumedByDialogueFrame = -1;
        }
    }

    public static SimpleRpgDialogueUI GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        var existing = FindAnyObjectByType<SimpleRpgDialogueUI>();
        if (existing != null)
            return existing;

        var go = new GameObject($"[{nameof(SimpleRpgDialogueUI)}]");
        return go.AddComponent<SimpleRpgDialogueUI>();
    }

    public void Show(string speaker, string line)
    {
        ShowInternal(speaker, line, advanceInitiallyAllowed: true);
    }

    public void ShowAwaitingLine(string speaker, string placeholderLine)
    {
        ShowInternal(speaker, placeholderLine, advanceInitiallyAllowed: false);
    }

    public void SetDialogueLineAndAllowAdvance(string line)
    {
        if (_lineLabel != null)
            _lineLabel.text = line ?? string.Empty;
        _advanceAllowed = true;
    }

    public void AbortDialogue()
    {
        StopAllCoroutines();
        SetVisible(false);
        IsDialogueOpen = false;
        _advanceAllowed = true;
    }

    public static void ForceCloseAll()
    {
        if (Instance != null)
            Instance.AbortDialogue();
        else
            IsDialogueOpen = false;
    }

    private void BuildUi()
    {
        _document = GetComponent<UIDocument>();
        if (_document == null)
            _document = gameObject.AddComponent<UIDocument>();

        FfStyleMenuUi.ConfigureDocument(_document, 5000);
        _dialogueHost = FfStyleMenuUi.BuildDialoguePanel(
            _document.rootVisualElement,
            out _speakerLabel,
            out _lineLabel);
    }

    private void ShowInternal(string speaker, string line, bool advanceInitiallyAllowed)
    {
        if (_document == null || _speakerLabel == null || _lineLabel == null)
            BuildUi();

        StopAllCoroutines();
        IsDialogueOpen = true;
        _advanceAllowed = advanceInitiallyAllowed;
        StartCoroutine(ShowRoutine(speaker, line));
    }

    private IEnumerator ShowRoutine(string speaker, string line)
    {
        if (_speakerLabel != null)
        {
            var hasSpeaker = !string.IsNullOrEmpty(speaker);
            _speakerLabel.style.display = hasSpeaker ? DisplayStyle.Flex : DisplayStyle.None;
            _speakerLabel.text = speaker ?? string.Empty;
        }

        if (_lineLabel != null)
            _lineLabel.text = line ?? string.Empty;

        SetVisible(true);
        yield return null;

        while (true)
        {
            yield return null;
            if (_advanceAllowed && WasAdvancePressedThisFrame())
                break;
        }

        InteractConsumedByDialogueFrame = Time.frameCount;
        SetVisible(false);
        IsDialogueOpen = false;
        _advanceAllowed = true;
    }

    private void SetVisible(bool visible)
    {
        if (_dialogueHost != null)
            _dialogueHost.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private bool WasAdvancePressedThisFrame()
    {
        if (advanceAction != null && advanceAction.action != null)
            return advanceAction.action.WasPressedThisFrame();

        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }
}
