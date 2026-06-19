using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Handles player death in combat: death screen, respawn at session start, inventory wipe, day advance (keeps gold).
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerDeathController : MonoBehaviour
{
    public const float DeathScreenDurationSeconds = 5f;

    public static PlayerDeathController Instance { get; private set; }

    public static bool IsDeathSequenceActive { get; private set; }

    private UIDocument _document;
    private VisualElement _overlay;
    private Label _bodyLabel;
    private bool _sequenceRunning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        IsDeathSequenceActive = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfMissing()
    {
        if (Instance != null || FindAnyObjectByType<PlayerDeathController>() != null)
            return;

        var go = new GameObject($"[{nameof(PlayerDeathController)}]");
        go.AddComponent<PlayerDeathController>();
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
        SetOverlayVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Instance = null;
        IsDeathSequenceActive = false;
    }

    private void OnEnable()
    {
        CombatUnit.OnAllyDefeated += OnAllyDefeated;
    }

    private void OnDisable()
    {
        CombatUnit.OnAllyDefeated -= OnAllyDefeated;
    }

    private void OnAllyDefeated(CombatUnit unit)
    {
        if (_sequenceRunning || unit == null || !unit.IsPlayerCharacter)
            return;

        StartCoroutine(DeathSequenceRoutine());
    }

    private IEnumerator DeathSequenceRoutine()
    {
        if (_sequenceRunning)
            yield break;

        _sequenceRunning = true;
        IsDeathSequenceActive = true;

        PauseMenuController.ForceCloseAndResetTime();
        SimpleRpgDialogueUI.ForceCloseAll();
        ForgeQuestChoiceUI.ForceCloseAll();
        CompanionConversationUi.Instance?.ForceClose();

        SetOverlayVisible(true);

        yield return new WaitForSecondsRealtime(DeathScreenDurationSeconds);

        yield return EndCombatIfNeeded();

        ApplyDeathConsequences();

        SetOverlayVisible(false);
        IsDeathSequenceActive = false;
        _sequenceRunning = false;
    }

    private static IEnumerator EndCombatIfNeeded()
    {
        var coordinator = Object.FindAnyObjectByType<CombatAdditiveCoordinator>();
        if (coordinator == null || !coordinator.IsCombatActiveOrLoading)
            yield break;

        CombatSession.ClearVictoryLootPending();

        var controller = Object.FindAnyObjectByType<CombatSceneController>();
        if (controller != null)
            controller.EndCombat();

        while (coordinator.IsCombatActiveOrLoading)
            yield return null;
    }

    private static void ApplyDeathConsequences()
    {
        Time.timeScale = 1f;

        var player = PlayerMovement2D.Instance ?? Object.FindAnyObjectByType<PlayerMovement2D>();
        var inventory = ResolvePlayerInventory(player);
        var blacksmith = BlacksmithMaster.ResolveEconomy();

        if (blacksmith != null)
            blacksmith.ApplyDeathDayAdvance();
        else
            inventory?.ClearAll();

        ForgeQuestManager.Instance?.ClearForNewDay(inventory);

        RespawnPlayer(player);

        Debug.Log("[PlayerDeath] Respawned at session start — inventory cleared, forge quest reset, day advanced, gold kept.");
    }

    private static Inventory ResolvePlayerInventory(PlayerMovement2D player)
    {
        if (player != null)
        {
            if (player.TryGetComponent<Inventory>(out var onPlayer))
                return onPlayer;

            var onHierarchy = player.GetComponentInChildren<Inventory>(true);
            if (onHierarchy == null)
                onHierarchy = player.GetComponentInParent<Inventory>();
            if (onHierarchy != null)
                return onHierarchy;
        }

        return Object.FindAnyObjectByType<Inventory>();
    }

    private static void RespawnPlayer(PlayerMovement2D player)
    {
        if (player == null)
            return;

        PlayerSessionStartRecorder.ResetToRecordedStart(player.transform, player.GetComponent<Rigidbody2D>());
        player.EnsureInputReady();
    }

    private void BuildUi()
    {
        _document = GetComponent<UIDocument>();
        if (_document == null)
            _document = gameObject.AddComponent<UIDocument>();

        FfStyleMenuUi.ConfigureDocument(_document, 6200);

        var root = _document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1f;
        root.pickingMode = PickingMode.Position;

        var styleSheet = Resources.Load<StyleSheet>(FfStyleMenuUi.StyleSheetResource);
        if (styleSheet != null)
            root.styleSheets.Add(styleSheet);

        _overlay = new VisualElement { name = "death-overlay" };
        _overlay.style.flexGrow = 1f;
        _overlay.style.justifyContent = Justify.Center;
        _overlay.style.alignItems = Align.Center;
        _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.72f);
        _overlay.pickingMode = PickingMode.Position;
        root.Add(_overlay);

        var panel = new VisualElement { name = "death-panel" };
        panel.AddToClassList("hud-panel");
        panel.style.minWidth = 320;
        panel.style.maxWidth = 440;
        panel.style.paddingTop = 20;
        panel.style.paddingBottom = 20;
        panel.style.paddingLeft = 24;
        panel.style.paddingRight = 24;
        _overlay.Add(panel);

        var title = new Label("You fell in battle") { name = "death-title" };
        FfStyleMenuUi.ApplyLabelStyle(title, 22f, true);
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.style.marginBottom = 12;
        panel.Add(title);

        _bodyLabel = new Label("Your pack is lost. A new day begins — visit the blacksmith for a new commission.") { name = "death-body" };
        FfStyleMenuUi.ApplyLabelStyle(_bodyLabel, 14f, false);
        _bodyLabel.style.color = FfStyleMenuUi.SubtitleColor;
        _bodyLabel.style.whiteSpace = WhiteSpace.Normal;
        _bodyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(_bodyLabel);
    }

    private void SetOverlayVisible(bool visible)
    {
        if (_overlay != null)
            _overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
