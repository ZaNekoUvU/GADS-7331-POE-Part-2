using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// AI-backed quest blacksmith: commissions an invented mineral, spawns pickup via <see cref="QuestMineralSpawner"/>,
/// turn-ins pay gold while the same commission stays active until the player ends the day (new ore spawn) or
/// continuing. Ending the day only happens when the player chooses that option in dialogue here; it heals the player,
/// runs the blacksmith sell-all / day advance, clears hired companions, and starts the next commission.
/// Uses <see cref="ForgeQuestManager"/> for cross-scene state.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BlacksmithQuestGiver : MonoBehaviour
{
    [Header("Character (AI persona)")]
    [SerializeField] private NpcDialogueProfile profile;

    [Header("Quest")]
    [Tooltip("Commission item from pickups (Quest Mineral) — separate from iron mined from veins.")]
    [SerializeField] private ItemDefinition questMineralDefinition;

    [Tooltip("Iron (or other standard ore) the smith also takes when you turn in the commission; must match mine vein oreDefinition.")]
    [SerializeField] private ItemDefinition forgeIronTurnInDefinition;

    [Tooltip("Fallback gold/unit if no BlacksmithMaster is in the scene. Normally pay uses quest mineral BaseSellPrice × the blacksmith bonus.")]
    [SerializeField] private int goldRewardPerUnit = 22;

    [Header("Services")]
    [SerializeField] private BlacksmithMaster blacksmith;
    [SerializeField] private Inventory playerInventory;
    [SerializeField] private PlayerPersistentCombatHealth playerHealth;
    [FormerlySerializedAs("ollamaService")]
    [SerializeField] private AiDialogueService aiService;
    [SerializeField] private SimpleRpgDialogueUI dialogueUi;
    [SerializeField] private ForgeQuestChoiceUI choiceUi;

    [Header("Fallback wording")]
    [SerializeField] private string offerFallback =
        "Need a favor — bring me back any strange ore you find in the eastern tunnels. I'll make it worth your while.";

    [SerializeField] private string turnInThanksFallback = "Aye, this is what I needed. Here's your coin.";
    [SerializeField] private string turnInEmptyFallback =
        "You need the strange ore I asked for and your iron ingots before I can pay you.";

    [Header("Proximity")]
    [SerializeField] private InputActionReference interactAction;

    private readonly HashSet<Collider2D> _playerProximity = new();
    private bool _sessionBusy;

    private void Awake()
    {
        var c = GetComponent<Collider2D>();
        if (c != null && !c.isTrigger)
            Debug.LogWarning($"{nameof(BlacksmithQuestGiver)}: use a trigger collider for talk range.", this);
    }

    private void OnEnable()
    {
        if (interactAction != null)
            interactAction.action.Enable();

        ForgeQuestManager.GetOrCreate();
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.action.Disable();

        StopAllCoroutines();
        _sessionBusy = false;
        _playerProximity.Clear();
    }

    /// <summary>
    /// Uses <see cref="BlacksmithMaster"/> on this GameObject when present (combined forge NPC); otherwise falls back
    /// to the serialized reference or <see cref="BlacksmithMaster.ResolveEconomy"/>.
    /// </summary>
    private void EnsureBlacksmithResolved()
    {
        var onSelf = GetComponent<BlacksmithMaster>();
        if (onSelf != null)
        {
            blacksmith = onSelf;
            return;
        }

        if (blacksmith == null)
            blacksmith = BlacksmithMaster.ResolveEconomy();
    }

    private void Update()
    {
        if (SimpleRpgDialogueUI.IsDialogueOpen || ForgeQuestChoiceUI.IsBlockingGameplay || PauseMenuController.IsOpen)
            return;

        if (_playerProximity.Count <= 0 || _sessionBusy || profile == null || questMineralDefinition == null
            || forgeIronTurnInDefinition == null)
            return;

        if (!WasInteractPressedThisFrame())
            return;

        if (aiService == null)
            aiService = AiDialogueService.GetOrCreate();
        if (dialogueUi == null)
            dialogueUi = SimpleRpgDialogueUI.GetOrCreate();
        if (choiceUi == null)
            choiceUi = ForgeQuestChoiceUI.GetOrCreate();
        if (playerInventory == null)
            playerInventory = FindAnyObjectByType<Inventory>();
        EnsureBlacksmithResolved();
        if (playerHealth == null)
            playerHealth = FindAnyObjectByType<PlayerPersistentCombatHealth>();

        var q = ForgeQuestManager.Instance;
        if (q != null && q.QuestActive)
            StartCoroutine(SessionWhenQuestActiveRoutine());
        else
            StartCoroutine(SessionOfferNewQuestRoutine());
    }

    private IEnumerator SessionOfferNewQuestRoutine()
    {
        _sessionBusy = true;
        yield return StartCoroutine(OfferNewQuestContentRoutine());
        _sessionBusy = false;
    }

    /// <summary>Requests a new commission (after a day reset or first talk). Does not manage <see cref="_sessionBusy"/>.</summary>
    private IEnumerator OfferNewQuestContentRoutine()
    {
        if (aiService == null)
            aiService = AiDialogueService.GetOrCreate();

        if (aiService.IsBusy)
        {
            ForgeQuestManager.GetOrCreate().BeginQuest("Raw Emberglass", questMineralDefinition, forgeIronTurnInDefinition, CommissionGoldPerUnitHint());
            dialogueUi.Show(profile.CharacterName, offerFallback);
            yield break;
        }

        ForgeQuestOfferDto dto = null;
        string err = null;

        dialogueUi.ShowAwaitingLine(profile.CharacterName, "…");

        yield return StartCoroutine(aiService.RequestForgeQuestOfferCoroutine(
            profile.CharacterName,
            profile.PersonaDescription,
            d => dto = d,
            e => err = e));

        if (dto != null)
        {
            ForgeQuestManager.GetOrCreate().BeginQuest(dto.materialName, questMineralDefinition, forgeIronTurnInDefinition, CommissionGoldPerUnitHint());
            dialogueUi.SetDialogueLineAndAllowAdvance(dto.requestLine);
        }
        else
        {
            Debug.LogWarning($"[ForgeQuest] Offer failed: {err}. Using fallback.", this);
            ForgeQuestManager.GetOrCreate().BeginQuest("Raw Emberglass", questMineralDefinition, forgeIronTurnInDefinition, CommissionGoldPerUnitHint());
            dialogueUi.SetDialogueLineAndAllowAdvance(offerFallback);
        }
    }

    private Inventory ResolvePlayerInventory()
    {
        var pm = PlayerMovement2D.Instance;
        if (pm != null)
        {
            if (pm.TryGetComponent<Inventory>(out var onPlayer))
                return onPlayer;

            var onHierarchy = pm.GetComponentInChildren<Inventory>(true);
            if (onHierarchy == null)
                onHierarchy = pm.GetComponentInParent<Inventory>();
            if (onHierarchy != null)
                return onHierarchy;
        }

        if (playerInventory != null)
            return playerInventory;

        return FindAnyObjectByType<Inventory>();
    }

    private int CommissionGoldPerUnitHint()
    {
        if (questMineralDefinition == null)
            return Mathf.Max(1, goldRewardPerUnit);

        EnsureBlacksmithResolved();

        if (blacksmith == null)
            return Mathf.Max(1, goldRewardPerUnit);

        var p = blacksmith.GetUnitSellPrice(questMineralDefinition, quoteForgeCommissionOre: true);
        return p > 0 ? p : Mathf.Max(1, goldRewardPerUnit);
    }

    private IEnumerator SessionWhenQuestActiveRoutine()
    {
        _sessionBusy = true;

        yield return StartCoroutine(choiceUi.RunRoutine("Turn in materials", "Just chat", "End the day"));

        if (choiceUi.LastChoice == 0)
            yield return StartCoroutine(TurnInRoutine());
        else if (choiceUi.LastChoice == 1)
            yield return StartCoroutine(SmallTalkRoutine());
        else if (choiceUi.LastChoice == 2)
            yield return StartCoroutine(EndForgingDayRoutine());

        _sessionBusy = false;
    }

    /// <summary>Runs <see cref="BlacksmithMaster.SellAllAndEndDay"/> while forge state is still active (so commission ore is paid),
    /// then clears forge quest state. Called only when the player picks end day in dialogue.</summary>
    private IEnumerator EndForgingDayRoutine()
    {
        EnsureBlacksmithResolved();
        if (blacksmith != null)
            blacksmith.SellAllAndEndDay();
        else
        {
            Debug.LogWarning(
                $"{nameof(BlacksmithQuestGiver)}: No {nameof(BlacksmithMaster)} — end day cleared forge state only; economy day did not advance.",
                this);
            HiredCompanionManager.Instance?.ClearHiresForNewDay();
        }

        var q = ForgeQuestManager.Instance;
        if (q != null)
            q.ClearForNewDay(ResolvePlayerInventory());

        if (playerHealth == null)
            playerHealth = FindAnyObjectByType<PlayerPersistentCombatHealth>();
        if (playerHealth != null)
            playerHealth.ResetToFullHealth();

        yield return StartCoroutine(OfferNewQuestContentRoutine());
    }

    private IEnumerator TurnInRoutine()
    {
        if (aiService == null)
            aiService = AiDialogueService.GetOrCreate();

        var q = ForgeQuestManager.Instance;
        var inv = ResolvePlayerInventory();
        if (q == null || inv == null)
        {
            dialogueUi.Show(profile.CharacterName, turnInEmptyFallback);
            yield break;
        }

        var materialName = q.QuestMaterialName;

        dialogueUi.ShowAwaitingLine(profile.CharacterName, "…");

        EnsureBlacksmithResolved();

        var unitsQuest = q.TurnInAndPay(inv, blacksmith, out var goldPaid, out var ironUnits);
        var request = new BlacksmithRoleplayRequestDto
        {
            mode = BlacksmithRoleplayModes.TurnIn,
            blacksmithName = profile.CharacterName,
            personaDescription = profile.PersonaDescription,
            localKnowledge = profile.LocalKnowledge,
            questMaterialName = materialName,
            questMineralUnits = unitsQuest,
            ironUnits = ironUnits,
            goldPaid = goldPaid
        };

        string line = null;
        string err = null;
        yield return StartCoroutine(aiService.RequestBlacksmithRoleplayLineCoroutine(request, s => line = s, e => err = e));

        if (!string.IsNullOrWhiteSpace(err))
            Debug.LogWarning($"[ForgeQuest] Turn-in reply failed: {err}", this);

        if (!string.IsNullOrWhiteSpace(line))
            dialogueUi.Show(profile.CharacterName, line);
        else
            dialogueUi.Show(profile.CharacterName, unitsQuest > 0 ? turnInThanksFallback : turnInEmptyFallback);

        if (unitsQuest <= 0)
            yield break;

        yield return new WaitUntil(() => !SimpleRpgDialogueUI.IsDialogueOpen);

        yield return StartCoroutine(choiceUi.RunRoutine("End the day", "Keep gathering"));

        if (choiceUi.LastChoice == 0)
            yield return StartCoroutine(EndForgingDayRoutine());
    }

    private IEnumerator SmallTalkRoutine()
    {
        if (aiService == null)
            aiService = AiDialogueService.GetOrCreate();

        var q = ForgeQuestManager.Instance;
        if (q == null || !q.QuestActive)
        {
            dialogueUi.Show(profile.CharacterName, "Come back later.");
            yield break;
        }

        dialogueUi.ShowAwaitingLine(profile.CharacterName, "…");

        var request = new BlacksmithRoleplayRequestDto
        {
            mode = BlacksmithRoleplayModes.SmallTalk,
            blacksmithName = profile.CharacterName,
            personaDescription = profile.PersonaDescription,
            localKnowledge = profile.LocalKnowledge,
            questMaterialName = q.QuestMaterialName
        };

        string line = null;
        string err = null;
        yield return StartCoroutine(aiService.RequestBlacksmithRoleplayLineCoroutine(request, s => line = s, e => err = e));

        if (!string.IsNullOrWhiteSpace(err))
            Debug.LogWarning($"[ForgeQuest] Small-talk reply failed: {err}", this);

        if (!string.IsNullOrWhiteSpace(line))
            dialogueUi.Show(profile.CharacterName, line);
        else
            dialogueUi.Show(profile.CharacterName, "Mind the forge — and those tunnels.");
    }

    private bool WasInteractPressedThisFrame()
    {
        if (SimpleRpgDialogueUI.InteractConsumedByDialogueFrame == Time.frameCount)
            return false;

        if (interactAction != null && interactAction.action != null)
            return interactAction.action.WasPressedThisFrame();

        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!PlayerMovement2D.IsPlayerCharacterCollider(other))
            return;

        _playerProximity.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!PlayerMovement2D.IsPlayerCharacterCollider(other))
            return;

        _playerProximity.Remove(other);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (questMineralDefinition != null && forgeIronTurnInDefinition != null
            && questMineralDefinition == forgeIronTurnInDefinition)
            Debug.LogWarning($"{nameof(BlacksmithQuestGiver)}: Assign different assets for quest mineral vs forge iron.", this);
    }
#endif
}
