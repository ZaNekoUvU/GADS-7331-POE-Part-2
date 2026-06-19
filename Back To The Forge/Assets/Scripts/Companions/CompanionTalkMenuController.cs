using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Press C in exploration to pick a hired mercenary and open <see cref="CompanionConversationUi"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class CompanionTalkMenuController : MonoBehaviour
{
    public static CompanionTalkMenuController Instance { get; private set; }

    /// <summary>True while picking a mercenary or in a companion conversation (C menu flow).</summary>
    public static bool IsCompanionTalkFlowActive => Instance != null && Instance._busy;

    [SerializeField] private InputActionReference companionMenuAction;

    private bool _busy;
    private CombatAdditiveCoordinator _combatCoordinator;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfMissing()
    {
        if (Instance != null || FindAnyObjectByType<CompanionTalkMenuController>() != null)
            return;

        var go = new GameObject($"[{nameof(CompanionTalkMenuController)}]");
        go.AddComponent<CompanionTalkMenuController>();
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
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        if (companionMenuAction != null)
            companionMenuAction.action.Enable();
    }

    private void OnDisable()
    {
        if (companionMenuAction != null)
            companionMenuAction.action.Disable();

        StopAllCoroutines();
        _busy = false;
    }

    private void Update()
    {
        if (_busy || !CanOpenMenu() || !WasCompanionMenuPressedThisFrame())
            return;

        StartCoroutine(OpenMercenaryPickerRoutine());
    }

    private IEnumerator OpenMercenaryPickerRoutine()
    {
        _busy = true;

        var mgr = HiredCompanionManager.Instance;
        if (mgr == null || mgr.CountHired() <= 0)
        {
            var dialogue = SimpleRpgDialogueUI.GetOrCreate();
            dialogue.Show(string.Empty, "You have no hired mercenaries to talk to.");
            yield return new WaitUntil(() => !SimpleRpgDialogueUI.IsDialogueOpen);
            _busy = false;
            yield break;
        }

        var unitIds = new List<int>(HiredCompanionManager.MaxCompanionSlots);
        var labels = new List<string>(HiredCompanionManager.MaxCompanionSlots);
        CollectHiredMercenaryOptions(mgr, unitIds, labels);

        if (unitIds.Count == 0)
        {
            _busy = false;
            yield break;
        }

        var choiceUi = ForgeQuestChoiceUI.GetOrCreate();
        yield return choiceUi.RunChoiceListRoutine(labels, "Talk to mercenary");

        var picked = choiceUi.LastChoice;
        if (picked < 0 || picked >= unitIds.Count)
        {
            _busy = false;
            yield break;
        }

        if (!MercenaryOfferLookup.TryGet(unitIds[picked], out var offer) || offer == null)
        {
            Debug.LogWarning($"{nameof(CompanionTalkMenuController)}: No offer data for unit id {unitIds[picked]}.", this);
            _busy = false;
            yield break;
        }

        var conversation = CompanionConversationUi.GetOrCreate();
        if (conversation == null)
        {
            _busy = false;
            yield break;
        }

        conversation.BeginConversation(offer, unitIds[picked], () => _busy = false);
        yield return new WaitUntil(() => !CompanionConversationUi.IsBlockingGameplay);

        if (_busy)
            _busy = false;
    }

    private static void CollectHiredMercenaryOptions(
        HiredCompanionManager mgr,
        List<int> unitIds,
        List<string> labels)
    {
        TryAddSlot(mgr.GetCompanionSlotUnitId(0), unitIds, labels);
        TryAddSlot(mgr.GetCompanionSlotUnitId(1), unitIds, labels);
        TryAddSlot(mgr.GetCompanionSlotUnitId(2), unitIds, labels);
    }

    private static void TryAddSlot(int unitId, List<int> unitIds, List<string> labels)
    {
        if (unitId <= 0)
            return;

        if (!MercenaryOfferLookup.TryGet(unitId, out var offer) || offer == null)
            return;

        unitIds.Add(unitId);
        labels.Add(offer.NpcDisplayName);
    }

    private bool CanOpenMenu()
    {
        if (PauseMenuController.IsOpen
            || SimpleRpgDialogueUI.IsDialogueOpen
            || CompanionConversationUi.IsBlockingGameplay
            || ForgeQuestChoiceUI.IsBlockingGameplay)
            return false;

        if (IsCombatBlocking())
            return false;

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name == PauseMenuController.DefaultMainMenuSceneName)
            return false;

        return PlayerMovement2D.Instance != null;
    }

    private bool IsCombatBlocking()
    {
        if (_combatCoordinator == null)
            _combatCoordinator = FindAnyObjectByType<CombatAdditiveCoordinator>();

        return _combatCoordinator != null && _combatCoordinator.IsCombatActiveOrLoading;
    }

    private bool WasCompanionMenuPressedThisFrame()
    {
        if (companionMenuAction != null && companionMenuAction.action != null)
            return companionMenuAction.action.WasPressedThisFrame();

        return Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
    }
}
