using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Free-text conversation with a hired mercenary. Blocks movement while open.
/// Uses the same FF dialogue shell as <see cref="SimpleRpgDialogueUI"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class CompanionConversationUi : MonoBehaviour
{
    public static CompanionConversationUi Instance { get; private set; }

    public static bool IsBlockingGameplay { get; private set; }

    private const int UiSortOrder = 5050;

    private UIDocument _document;
    private VisualElement _overlay;
    private Label _speakerLabel;
    private Label _lineLabel;
    private Label _statusLabel;
    private TextField _inputField;

    private HireableCompanionOffer _activeOffer;
    private int _activeUnitId;
    private readonly StringBuilder _history = new(512);
    private bool _waitingForOllama;
    private Action _onClosed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfMissing()
    {
        if (Instance != null || FindAnyObjectByType<CompanionConversationUi>() != null)
            return;

        var go = new GameObject($"[{nameof(CompanionConversationUi)}]");
        go.AddComponent<CompanionConversationUi>();
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
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            IsBlockingGameplay = false;
        }
    }

    public static CompanionConversationUi GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        var existing = FindAnyObjectByType<CompanionConversationUi>();
        if (existing != null)
            return existing;

        var player = PlayerMovement2D.Instance ?? FindAnyObjectByType<PlayerMovement2D>();
        if (player != null)
        {
            var onPlayer = player.GetComponent<CompanionConversationUi>();
            if (onPlayer != null)
                Destroy(onPlayer);
        }

        var go = new GameObject($"[{nameof(CompanionConversationUi)}]");
        return go.AddComponent<CompanionConversationUi>();
    }

    public void BeginConversation(HireableCompanionOffer offer, int unitId, Action onClosed = null)
    {
        if (offer == null || unitId <= 0 || IsBlockingGameplay)
            return;

        EnsureUiBuilt();

        StopAllCoroutines();
        _activeOffer = offer;
        _activeUnitId = unitId;
        _onClosed = onClosed;
        _history.Clear();
        _waitingForOllama = false;

        SetDisplayedDialogue(offer.NpcDisplayName,
            "Your words can unlock their battle skill—or sour their spirit.");

        if (_statusLabel != null)
            _statusLabel.text = DescribeCurrentMorale(unitId, offer);

        if (_inputField != null)
        {
            _inputField.SetValueWithoutNotify(string.Empty);
            _inputField.SetEnabled(true);
        }

        IsBlockingGameplay = true;
        if (PauseMenuController.IsOpen)
            PauseMenuController.ForceCloseAndResetTime();

        SetVisible(true);
        StartCoroutine(FocusInputNextFrame());
        StartCoroutine(RequestOpeningLineRoutine());
    }

    private IEnumerator RequestOpeningLineRoutine()
    {
        if (_activeOffer == null)
            yield break;

        _waitingForOllama = true;
        SetInputEnabled(false);
        SetDisplayedDialogue(_activeOffer.NpcDisplayName, "…");

        var service = OllamaDialogueService.GetOrCreate();
        string line = null;
        string err = null;

        var system =
            $"You are {_activeOffer.NpcDisplayName}, a hired mercenary walking with the traveler.\n" +
            $"Persona:\n{_activeOffer.PersonaForLlm}\n\n" +
            $"{DialogueSpeakerNameUtil.IdentityRules(_activeOffer.NpcDisplayName)}\n\n" +
            "The traveler stops to talk. Speak first in 1-2 short sentences — direct speech only, in character.";

        yield return service.RequestRoleplayLineCoroutine(
            system,
            "The traveler turns to you and wants to chat. Greet them briefly.",
            s => line = s,
            e => err = e,
            _activeOffer.NpcDisplayName);

        if (string.IsNullOrWhiteSpace(line))
        {
            line = !string.IsNullOrWhiteSpace(err)
                ? "…What's on your mind?"
                : "You wanted to talk?";
        }

        AppendHistory(_activeOffer.NpcDisplayName, line);
        SetDisplayedDialogue(_activeOffer.NpcDisplayName, DialogueSpeakerNameUtil.Enforce(line, _activeOffer.NpcDisplayName));

        _waitingForOllama = false;
        SetInputEnabled(true);
        yield return FocusInputNextFrame();
    }

    public void ForceClose()
    {
        StopAllCoroutines();
        _waitingForOllama = false;
        _activeOffer = null;
        _activeUnitId = 0;
        IsBlockingGameplay = false;
        SetVisible(false);
        _onClosed?.Invoke();
        _onClosed = null;
    }

    private IEnumerator FocusInputNextFrame()
    {
        yield return null;
        _inputField?.Focus();
    }

    private void Update()
    {
        if (!IsBlockingGameplay)
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ForceClose();
            return;
        }

        if (_waitingForOllama)
            return;

        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            TrySendCurrentInput();
    }

    private void TrySendCurrentInput()
    {
        if (_activeOffer == null || _waitingForOllama || _inputField == null)
            return;

        var text = _inputField.value?.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        StartCoroutine(SendPlayerLineRoutine(text));
    }

    private IEnumerator SendPlayerLineRoutine(string playerLine)
    {
        _waitingForOllama = true;
        SetInputEnabled(false);

        AppendHistory("You", playerLine);
        SetDisplayedDialogue("You", playerLine);

        var service = OllamaDialogueService.GetOrCreate();
        CompanionDialogueDto dto = null;
        string err = null;

        SetDisplayedDialogue(_activeOffer.NpcDisplayName, "…");

        yield return service.RequestCompanionDialogueCoroutine(
            _activeOffer,
            playerLine,
            _history.ToString(),
            result => dto = result,
            message => err = message);

        if (dto == null)
            dto = BuildFallbackDto(playerLine, _activeOffer, err);

        HiredCompanionManager.GetOrCreate().ApplyCompanionDialogueResult(_activeUnitId, dto);

        AppendHistory(_activeOffer.NpcDisplayName, dto.replyLine);
        SetDisplayedDialogue(_activeOffer.NpcDisplayName, dto.replyLine);

        if (_statusLabel != null)
            _statusLabel.text = BuildStatusAfterResult(dto);

        if (_inputField != null)
            _inputField.SetValueWithoutNotify(string.Empty);

        _waitingForOllama = false;
        SetInputEnabled(true);
        yield return FocusInputNextFrame();
    }

    private static CompanionDialogueDto BuildFallbackDto(string playerLine, HireableCompanionOffer offer, string err)
    {
        if (!string.IsNullOrWhiteSpace(err))
            Debug.LogWarning($"[CompanionDialogue] Using keyword fallback ({err}).", null);

        var lower = playerLine.ToLowerInvariant();
        var positive = lower.Contains("great") || lower.Contains("proud") || lower.Contains("believe")
            || lower.Contains("thank") || lower.Contains("good job") || lower.Contains("you can");
        var negative = lower.Contains("useless") || lower.Contains("weak") || lower.Contains("stupid")
            || lower.Contains("shut up") || lower.Contains("pathetic") || lower.Contains("hate");

        if (positive)
        {
            var skill = offer.PositiveMoraleSkill;
            return new CompanionDialogueDto
            {
                replyLine = "…Thanks. I'll remember that when blades are drawn.",
                sentiment = "positive",
                combatEffect = "positive_skill",
                effectLabel = skill.skillName
            };
        }

        if (negative)
        {
            var skill = offer.NegativeMoraleSkill;
            return new CompanionDialogueDto
            {
                replyLine = "Fine. See if I put my back into the next fight.",
                sentiment = "negative",
                combatEffect = "negative_skill",
                effectLabel = skill.skillName
            };
        }

        return new CompanionDialogueDto
        {
            replyLine = "Mm. Anything else?",
            sentiment = "neutral",
            combatEffect = "none",
            effectLabel = string.Empty
        };
    }

    private static string DescribeCurrentMorale(int unitId, HireableCompanionOffer offer)
    {
        var mgr = HiredCompanionManager.Instance;
        if (mgr == null || !mgr.TryGetMoraleState(unitId, out var state))
            return $"Skills: {offer.PositiveMoraleSkill.skillName} / {offer.NegativeMoraleSkill.skillName}";

        if (!state.HasActiveSkill)
            return $"Ready — encourage them for {offer.PositiveMoraleSkill.skillName}, or risk {offer.NegativeMoraleSkill.skillName}.";

        return state.DescribeActiveSkillForUi();
    }

    private string BuildStatusAfterResult(CompanionDialogueDto dto)
    {
        var effect = DescribeCurrentMorale(_activeUnitId, _activeOffer);
        if (dto == null)
            return effect;

        var sentiment = dto.sentiment ?? "neutral";
        var label = string.IsNullOrWhiteSpace(dto.effectLabel) ? string.Empty : $" — {dto.effectLabel}";

        return sentiment switch
        {
            var s when s.Contains("positive", StringComparison.OrdinalIgnoreCase) =>
                $"Encouraged{label}. {effect}",
            var s when s.Contains("negative", StringComparison.OrdinalIgnoreCase) =>
                $"Upset{label}. {effect}",
            _ => effect
        };
    }

    private void AppendHistory(string speaker, string line)
    {
        if (_history.Length > 0)
            _history.AppendLine();

        _history.Append(speaker);
        _history.Append(": ");
        _history.Append(line?.Trim() ?? string.Empty);
    }

    private void SetDisplayedDialogue(string speaker, string line)
    {
        if (_speakerLabel != null)
        {
            var hasSpeaker = !string.IsNullOrEmpty(speaker);
            _speakerLabel.style.display = hasSpeaker ? DisplayStyle.Flex : DisplayStyle.None;
            _speakerLabel.text = speaker ?? string.Empty;
        }

        if (_lineLabel != null)
            _lineLabel.text = line ?? string.Empty;
    }

    private void SetInputEnabled(bool enabled)
    {
        if (_inputField != null)
            _inputField.SetEnabled(enabled);
    }

    private void BuildUi()
    {
        _document = GetComponent<UIDocument>();
        if (_document == null)
            _document = gameObject.AddComponent<UIDocument>();

        FfStyleMenuUi.ConfigureDocument(_document, UiSortOrder);

        _overlay = FfStyleMenuUi.BuildCompanionConversationPanel(
            _document.rootVisualElement,
            out _speakerLabel,
            out _lineLabel,
            out _statusLabel,
            out _inputField);
    }

    private void EnsureUiBuilt()
    {
        if (_document == null || _overlay == null || _speakerLabel == null || _inputField == null)
            BuildUi();
    }

    private void SetVisible(bool visible)
    {
        if (_overlay != null)
            _overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
