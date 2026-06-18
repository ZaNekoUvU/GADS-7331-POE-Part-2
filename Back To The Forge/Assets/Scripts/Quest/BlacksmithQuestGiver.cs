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

        var inv = ResolvePlayerInventory();
        ActiveDialogue.ShowAwaitingLine(profile.CharacterName, "…");

        var sys = BuildSmallTalkSystemPrompt(q, inv);
        var user = "Say your line only.";

        string line = null;
        string err = null;
        yield return StartCoroutine(ollamaService.RequestRoleplayLineCoroutine(sys, user, s => line = s, e => err = e));

        if (!string.IsNullOrWhiteSpace(line) && SmallTalkContradictsInventory(line, q, inv))
        {
            Debug.LogWarning($"[ForgeQuest] Small talk contradicted inventory — using fallback. Raw: {line}", this);
            line = null;
        }

        if (!string.IsNullOrWhiteSpace(line))
            ActiveDialogue.Show(profile.CharacterName, line);
        else
            ActiveDialogue.Show(profile.CharacterName, BuildSmallTalkFallback(q, inv));
    }

    private string BuildSmallTalkSystemPrompt(ForgeQuestManager q, Inventory inv)
    {
        var sb = new StringBuilder(640);
        sb.AppendLine(BuildPersonaHeader());
        sb.AppendLine("Facts (must follow exactly — do not contradict or invent inventory):");
        AppendCommissionInventoryFacts(sb, q, inv);
        sb.AppendLine("- The traveler chose casual small talk, not a turn-in.");
        sb.AppendLine("- One or two short casual sentences only.");
        sb.AppendLine("- Do not repeat the full commission speech.");
        sb.AppendLine(
            "- NEVER say they already brought, delivered, handed over, found, or finished gathering the commission ore " +
            "unless the pack count above is greater than zero.");
        sb.AppendLine("- NEVER thank them for commission ore unless that count is greater than zero.");
        sb.AppendLine("- If they do not have it yet, you may encourage them or mention the forge — nothing is delivered yet.");
        sb.AppendLine("- No meta, no 'the user', no JSON.");
        return sb.ToString();
    }

    private static void AppendCommissionInventoryFacts(StringBuilder sb, ForgeQuestManager q, Inventory inv)
    {
        var material = q.QuestMaterialName ?? "the commission ore";
        var commissionCount = q.CountCommissionOreInInventory(inv);
        sb.AppendLine($"- Active commission material: {material}");
        sb.AppendLine($"- Commission ore in traveler's pack RIGHT NOW: {commissionCount} (game truth).");

        if (q.ForgeIronTurnInItem != null)
        {
            var ironName = q.ForgeIronTurnInItem.DisplayName;
            var ironCount = q.CountSupplementaryTurnInInInventory(inv);
            sb.AppendLine($"- {ironName} in traveler's pack RIGHT NOW: {ironCount} (game truth).");
        }

        if (commissionCount <= 0)
            sb.AppendLine($"- They do NOT have {material} yet. Do not speak as if they do.");
        else if (q.ForgeIronTurnInItem != null && q.CountSupplementaryTurnInInInventory(inv) <= 0)
            sb.AppendLine(
                $"- They have some {material}, but still need {q.ForgeIronTurnInItem.DisplayName} from the mines before turn-in.");
        else
            sb.AppendLine("- They may have enough to turn in later, but this is only small talk — do not pay or close the quest.");
    }

    private static bool SmallTalkContradictsInventory(string line, ForgeQuestManager q, Inventory inv)
    {
        if (string.IsNullOrWhiteSpace(line) || q == null || q.CountCommissionOreInInventory(inv) > 0)
            return false;

        var lower = line.ToLowerInvariant();
        var material = q.QuestMaterialName?.Trim().ToLowerInvariant();

        if (ContainsAny(lower,
                "you brought", "you've brought", "you have brought", "you delivered", "you've delivered",
                "you handed", "you've handed", "good haul", "nice haul", "well done finding",
                "already found", "already got", "already have", "got it already", "have it already"))
            return true;

        if (string.IsNullOrEmpty(material))
            return false;

        if (lower.Contains(material) && ContainsAny(lower,
                "you have", "you've got", "you got", "in your pack", "in your bag", "brought the", "found the"))
            return true;

        return false;
    }

    private static bool ContainsAny(string text, params string[] phrases)
    {
        for (var i = 0; i < phrases.Length; i++)
        {
            if (text.Contains(phrases[i]))
                return true;
        }

        return false;
    }

    private static string BuildSmallTalkFallback(ForgeQuestManager q, Inventory inv)
    {
        var material = q.QuestMaterialName ?? "that ore";
        if (q.CountCommissionOreInInventory(inv) <= 0)
            return $"Still after {material}? Check the tunnels — I'll be here.";

        if (q.ForgeIronTurnInItem != null && q.CountSupplementaryTurnInInInventory(inv) <= 0)
            return $"You've got {material}. Don't forget {q.ForgeIronTurnInItem.DisplayName} from the mines.";

        return "Mind the forge — and those tunnels.";
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
