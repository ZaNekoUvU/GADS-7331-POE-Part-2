using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Ollama quest blacksmith: commissions a invented mineral, spawns pickup via <see cref="QuestMineralSpawner"/>,
/// then lets the player turn in for gold or chat. Uses <see cref="ForgeQuestManager"/> for cross-scene state.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BlacksmithQuestGiver : MonoBehaviour
{
    [Header("Character (Ollama persona)")]
    [SerializeField] private NpcDialogueProfile profile;

    [Header("Quest")]
    [Tooltip("Inventory item granted by QuestMineralPickup — display name for HUD is this asset; spoken name comes from the LLM.")]
    [SerializeField] private ItemDefinition questMineralDefinition;

    [SerializeField] private int goldRewardPerUnit = 22;

    [Header("Services")]
    [SerializeField] private BlacksmithMaster blacksmith;
    [SerializeField] private Inventory playerInventory;
    [SerializeField] private OllamaDialogueService ollamaService;
    [SerializeField] private SimpleRpgDialogueUI dialogueUi;
    [SerializeField] private ForgeQuestChoiceUI choiceUi;

    [Header("Fallback wording")]
    [SerializeField] private string offerFallback =
        "Need a favor — bring me back any strange ore you find in the eastern tunnels. I'll make it worth your while.";

    [SerializeField] private string turnInThanksFallback = "Aye, this is what I needed. Here's your coin.";
    [SerializeField] private string turnInEmptyFallback = "You brought nothing I asked for. Come back when you have the ore.";

    [Header("Proximity")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private InputActionReference interactAction;

    private int _overlap;
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
    }

    private void Update()
    {
        if (SimpleRpgDialogueUI.IsDialogueOpen || ForgeQuestChoiceUI.IsBlockingGameplay)
            return;

        if (_overlap <= 0 || _sessionBusy || profile == null || questMineralDefinition == null)
            return;

        if (!WasInteractPressedThisFrame())
            return;

        if (ollamaService == null)
            ollamaService = OllamaDialogueService.GetOrCreate();
        if (dialogueUi == null)
            dialogueUi = SimpleRpgDialogueUI.GetOrCreate();
        if (choiceUi == null)
            choiceUi = ForgeQuestChoiceUI.GetOrCreate();
        if (playerInventory == null)
            playerInventory = FindAnyObjectByType<Inventory>();
        if (blacksmith == null)
            blacksmith = FindAnyObjectByType<BlacksmithMaster>();

        var q = ForgeQuestManager.Instance;
        if (q != null && q.QuestActive)
            StartCoroutine(SessionWhenQuestActiveRoutine());
        else
            StartCoroutine(SessionOfferNewQuestRoutine());
    }

    private IEnumerator SessionOfferNewQuestRoutine()
    {
        _sessionBusy = true;

        if (ollamaService.IsBusy)
        {
            dialogueUi.Show(profile.CharacterName, offerFallback);
            _sessionBusy = false;
            yield break;
        }

        ForgeQuestOfferDto dto = null;
        string err = null;

        dialogueUi.ShowAwaitingLine(profile.CharacterName, "…");

        yield return StartCoroutine(ollamaService.RequestForgeQuestOfferCoroutine(
            profile.CharacterName,
            profile.PersonaDescription,
            d => dto = d,
            e => err = e));

        if (dto != null)
        {
            ForgeQuestManager.Instance.BeginQuest(dto.materialName, questMineralDefinition, goldRewardPerUnit);
            dialogueUi.SetDialogueLineAndAllowAdvance(dto.requestLine);
        }
        else
        {
            Debug.LogWarning($"[ForgeQuest] Offer failed: {err}. Using fallback.", this);
            ForgeQuestManager.Instance.BeginQuest("Raw Emberglass", questMineralDefinition, goldRewardPerUnit);
            dialogueUi.SetDialogueLineAndAllowAdvance(offerFallback);
        }

        _sessionBusy = false;
    }

    private IEnumerator SessionWhenQuestActiveRoutine()
    {
        _sessionBusy = true;

        yield return StartCoroutine(choiceUi.RunRoutine("Turn in materials", "Just chat"));

        if (choiceUi.LastChoice == 0)
            yield return StartCoroutine(TurnInRoutine());
        else
            yield return StartCoroutine(SmallTalkRoutine());

        _sessionBusy = false;
    }

    private IEnumerator TurnInRoutine()
    {
        var q = ForgeQuestManager.Instance;
        var inv = playerInventory;
        if (q == null || inv == null)
        {
            dialogueUi.Show(profile.CharacterName, turnInEmptyFallback);
            yield break;
        }

        var materialName = q.QuestMaterialName;

        dialogueUi.ShowAwaitingLine(profile.CharacterName, "…");

        var unitsRemoved = q.TurnInAndPay(inv, blacksmith, out var goldPaid);
        var sys = BuildTurnInSystemPrompt(materialName, unitsRemoved, goldPaid);
        var user = "Speak your line to the traveler now (their reply ends the conversation).";

        string line = null;
        string err = null;
        yield return StartCoroutine(ollamaService.RequestRoleplayLineCoroutine(sys, user, s => line = s, e => err = e));

        if (!string.IsNullOrWhiteSpace(line))
            dialogueUi.Show(profile.CharacterName, line);
        else
            dialogueUi.Show(profile.CharacterName, unitsRemoved > 0 ? turnInThanksFallback : turnInEmptyFallback);
    }

    private IEnumerator SmallTalkRoutine()
    {
        var q = ForgeQuestManager.Instance;
        if (q == null || !q.QuestActive)
        {
            dialogueUi.Show(profile.CharacterName, "Come back later.");
            yield break;
        }

        dialogueUi.ShowAwaitingLine(profile.CharacterName, "…");

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
            dialogueUi.Show(profile.CharacterName, line);
        else
            dialogueUi.Show(profile.CharacterName, "Mind the forge — and those tunnels.");
    }

    private string BuildTurnInSystemPrompt(string materialName, int unitsRemoved, int goldPaid)
    {
        var sb = new StringBuilder(512);
        sb.AppendLine(BuildPersonaHeader());
        sb.AppendLine("Facts (must follow):");
        sb.AppendLine($"- You asked for material called: {materialName}");
        sb.AppendLine($"- The traveler brought {unitsRemoved} unit(s) of that ore.");
        sb.AppendLine($"- You pay them {goldPaid} gold total for this handoff (already settled in the till).");
        sb.AppendLine(
            "Reply with one short in-character line only: grateful and warm if units > 0, disappointed but fair if 0. " +
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
        if (!other.CompareTag(playerTag))
            return;

        _overlap++;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        _overlap = Mathf.Max(0, _overlap - 1);
    }
}
