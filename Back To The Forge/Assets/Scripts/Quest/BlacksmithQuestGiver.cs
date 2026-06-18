using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Ollama quest blacksmith: commissions a invented mineral, spawns pickup via <see cref="QuestMineralSpawner"/>,
/// turn-ins pay gold while the same commission stays active until the player ends the day (new ore spawn) or
/// continuing. Ending the day only happens when the player chooses that option in dialogue here; it heals the player,
/// runs the blacksmith sell-all / day advance, clears hired companions, and starts the next commission.
/// Uses <see cref="ForgeQuestManager"/> for cross-scene state.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BlacksmithQuestGiver : MonoBehaviour
{
    [Header("Character (Ollama persona)")]
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
    [SerializeField] private OllamaDialogueService ollamaService;
    [SerializeField] private ForgeQuestChoiceUI choiceUi;

    private SimpleRpgDialogueUI ActiveDialogue => SimpleRpgDialogueUI.GetOrCreate();

    [Header("Fallback wording")]
    [SerializeField] private string offerFallback =
        "Need a favor — bring me back any strange ore you find in the eastern tunnels. I'll make it worth your while.";

    [SerializeField] private string turnInThanksFallback = "Aye, this is what I needed. Here's your coin.";
    [SerializeField] private string turnInEmptyFallback =
        "You're still short on what I asked for — check your pack and the mines.";

    [Header("Proximity")]
    [SerializeField] private InputActionReference interactAction;

    private readonly HashSet<Collider2D> _playerProximity = new();
    private bool _sessionBusy;

    private void Awake()
    {
        Collider2DTriggerUtil.WarnIfNoTalkTrigger(gameObject, nameof(BlacksmithQuestGiver));
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
        if (SimpleRpgDialogueUI.IsDialogueOpen || CompanionConversationUi.IsBlockingGameplay
            || ForgeQuestChoiceUI.IsBlockingGameplay || PauseMenuController.IsOpen)
            return;

        if (_playerProximity.Count <= 0 || _sessionBusy || profile == null || questMineralDefinition == null
            || forgeIronTurnInDefinition == null)
            return;

        if (!WasInteractPressedThisFrame())
            return;

        if (ollamaService == null)
            ollamaService = OllamaDialogueService.GetOrCreate();
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
        if (ollamaService == null)
            ollamaService = OllamaDialogueService.GetOrCreate();

        if (ollamaService.IsBusy)
        {
            ForgeQuestManager.GetOrCreate().BeginQuest("Raw Emberglass", questMineralDefinition, forgeIronTurnInDefinition, CommissionGoldPerUnitHint());
            ActiveDialogue.Show(profile.CharacterName, offerFallback);
            yield break;
        }

        ForgeQuestOfferDto dto = null;
        string err = null;

        ActiveDialogue.ShowAwaitingLine(profile.CharacterName, "…");

        yield return StartCoroutine(ollamaService.RequestForgeQuestOfferCoroutine(
            profile.CharacterName,
            profile.PersonaDescription,
            d => dto = d,
            e => err = e));

        if (dto != null)
        {
            ForgeQuestManager.GetOrCreate().BeginQuest(dto.materialName, questMineralDefinition, forgeIronTurnInDefinition, CommissionGoldPerUnitHint());
            ActiveDialogue.SetDialogueLineAndAllowAdvance(dto.requestLine);
        }
        else
        {
            Debug.LogWarning($"[ForgeQuest] Offer failed: {err}. Using fallback.", this);
            ForgeQuestManager.GetOrCreate().BeginQuest("Raw Emberglass", questMineralDefinition, forgeIronTurnInDefinition, CommissionGoldPerUnitHint());
            ActiveDialogue.SetDialogueLineAndAllowAdvance(offerFallback);
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
        var q = ForgeQuestManager.Instance;
        var inv = ResolvePlayerInventory();
        if (q == null || inv == null)
        {
            ActiveDialogue.Show(profile.CharacterName, turnInEmptyFallback);
            yield break;
        }

        if (!q.CanTurnIn(inv, out var blockerMessage))
        {
            ActiveDialogue.Show(profile.CharacterName, blockerMessage);
            yield break;
        }

        var materialName = q.QuestMaterialName;

        ActiveDialogue.ShowAwaitingLine(profile.CharacterName, "…");

        EnsureBlacksmithResolved();

        var unitsQuest = q.TurnInAndPay(inv, blacksmith, out var goldPaid, out var ironUnits);
        var sys = BuildTurnInSystemPrompt(materialName, unitsQuest, ironUnits, goldPaid);
        var user = "Speak your line to the traveler now (their reply ends the conversation).";

        string line = null;
        string err = null;
        yield return StartCoroutine(ollamaService.RequestRoleplayLineCoroutine(sys, user, s => line = s, e => err = e));

        if (!string.IsNullOrWhiteSpace(line))
            ActiveDialogue.Show(profile.CharacterName, line);
        else
            ActiveDialogue.Show(profile.CharacterName, unitsQuest > 0 ? turnInThanksFallback : turnInEmptyFallback);

        if (unitsQuest <= 0)
            yield break;

        yield return new WaitUntil(() => !SimpleRpgDialogueUI.IsDialogueOpen);

        yield return StartCoroutine(choiceUi.RunRoutine("End the day", "Keep gathering"));

        if (choiceUi.LastChoice == 0)
            yield return StartCoroutine(EndForgingDayRoutine());
    }

    private IEnumerator SmallTalkRoutine()
    {
        var q = ForgeQuestManager.Instance;
        if (q == null || !q.QuestActive)
        {
            ActiveDialogue.Show(profile.CharacterName, "Come back later.");
            yield break;
        }

        ActiveDialogue.ShowAwaitingLine(profile.CharacterName, "…");

        var sb = new StringBuilder(384);
        sb.AppendLine(BuildPersonaHeader());
        sb.AppendLine(
            $"The traveler is here for small talk. You already asked them to fetch \"{q.QuestMaterialName}\" — do not repeat the full commission speech. " +
            "One or two short casual sentences.");
        var sys = sb.ToString();
        var user = "Say your line only.";

        string line = null;
        string err = null;
        yield return StartCoroutine(ollamaService.RequestRoleplayLineCoroutine(sys, user, s => line = s, e => err = e));

        if (!string.IsNullOrWhiteSpace(line))
            ActiveDialogue.Show(profile.CharacterName, line);
        else
            ActiveDialogue.Show(profile.CharacterName, "Mind the forge — and those tunnels.");
    }

    private string BuildTurnInSystemPrompt(string materialName, int questMineralUnits, int ironUnits, int goldPaid)
    {
        var sb = new StringBuilder(512);
        sb.AppendLine(BuildPersonaHeader());
        sb.AppendLine("Facts (must follow):");
        sb.AppendLine($"- You asked for a special material called: {materialName}");
        sb.AppendLine($"- The traveler hands over {questMineralUnits} unit(s) of that strange ore and {ironUnits} unit(s) of standard iron.");
        sb.AppendLine($"- You pay them {goldPaid} gold total for this handoff (already settled in the till).");
        sb.AppendLine(
            "Reply with one short in-character line only: grateful and warm if they brought materials, disappointed but fair if not. " +
            "No meta, no 'the user', no JSON.");
        return sb.ToString();
    }

    private string BuildPersonaHeader()
    {
        return
            $"You are {profile.CharacterName}, a blacksmith in the fantasy game Back to the Forge.\n" +
            $"{profile.PersonaDescription.Trim()}";
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
