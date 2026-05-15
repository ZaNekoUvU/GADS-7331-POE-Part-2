using System.Collections;
using UnityEngine;

/// <summary>
/// Optional helper: sets <see cref="CombatSession"/> data and calls <see cref="CombatAdditiveCoordinator.BeginCombat"/>.
/// Add to any GameObject and wire the coordinator (or leave empty to find one in the scene).
/// </summary>
public class CombatStarter : MonoBehaviour
{
    [SerializeField] private CombatAdditiveCoordinator coordinator;
    [SerializeField] private int defaultEncounterId;

    [Header("Random encounter intro (RiskyGround → LLM)")]
    [Tooltip("When used via StartRandomEncounterWithLlmIntro, shows one narrator line from Ollama before combat.")]
    [SerializeField] private bool useLlmBanditEncounterIntro = true;

    [SerializeField] private OllamaDialogueService ollamaService;

    private static readonly string[] EncounterMotifHints =
    {
        "wrong silence",
        "too-quiet birds",
        "fresh hoofprints",
        "broken cage-straps",
        "cold ash",
        "a snapped branch",
        "dust kicking up",
        "eyes from the ditch",
        "steel catching sun",
        "someone counted steps wrong"
    };

    private static readonly string[] FallbackBanditEncounterLines =
    {
        "You've walked into bandits.",
        "Bandits rise — no preamble.",
        "Ambush. They were waiting.",
        "Steel answers before words do.",
        "They're already closing."
    };

    private Coroutine _randomEncounterIntroRoutine;

    /// <summary>True while a risky-ground LLM intro is showing or loading — blocks duplicate rolls.</summary>
    public bool IsRandomEncounterIntroPlaying => _randomEncounterIntroRoutine != null;

    /// <summary>Use from UnityEvent (e.g. UI Button) with no argument.</summary>
    public void StartFight()
    {
        StartFightWithId(defaultEncounterId);
    }

    /// <summary>Use from code or UnityEvent with int payload if you use a custom caller.</summary>
    public void StartFightWithId(int encounterId)
    {
        CombatSession.EncounterId = encounterId;

        var party = FindAnyObjectByType<ExplorationCombatParty>();
        if (party != null)
            party.ApplyToCombatSession();
        else
            CombatSession.ResetAllyPartyDefaults();

        if (coordinator == null)
            coordinator = FindAnyObjectByType<CombatAdditiveCoordinator>();

        if (coordinator == null)
        {
            Debug.LogError($"{nameof(CombatStarter)}: No {nameof(CombatAdditiveCoordinator)} in scene.", this);
            return;
        }

        coordinator.BeginCombat();
    }

    /// <summary>
    /// Called by <see cref="RiskyGroundEncounter2D"/> instead of <see cref="StartFightWithId"/> so the player sees one LLM line (bandit-themed) first.
    /// </summary>
    public void StartRandomEncounterWithLlmIntro(int encounterId)
    {
        if (!useLlmBanditEncounterIntro)
        {
            StartFightWithId(encounterId);
            return;
        }

        if (_randomEncounterIntroRoutine != null)
            return;

        _randomEncounterIntroRoutine = StartCoroutine(RandomEncounterIntroThenFight(encounterId));
    }

    private IEnumerator RandomEncounterIntroThenFight(int encounterId)
    {
        var ui = SimpleRpgDialogueUI.GetOrCreate();
        var service = ollamaService != null ? ollamaService : OllamaDialogueService.GetOrCreate();

        ui.ShowAwaitingLine(string.Empty, "…");

        var systemPrompt =
            "You are the narrator in Baldur's Gate 3: bone-dry, clipped, present tense. Output ONE short line only — aim under ~14 words, often starting with You / Your / They're / Something. " +
            "The threat is bandits; name them bandits once (or imply them clearly). Hint one sharp sensory beat at most — no lore, no staging directions, no metaphors piled up. " +
            "No quotes, no markdown, no second sentence.";

        var motif = EncounterMotifHints[Random.Range(0, EncounterMotifHints.Length)];
        var userPrompt =
            $"Bandit encounter. One narrator line. Optionally nod at: {motif}. Don't explain much.";

        string line = null;
        string err = null;

        if (!service.IsBusy)
            yield return StartCoroutine(service.RequestRoleplayLineCoroutine(systemPrompt, userPrompt, s => line = s, e => err = e));
        else
            err = "busy";

        if (!string.IsNullOrWhiteSpace(line))
            ui.SetDialogueLineAndAllowAdvance(line);
        else
        {
            if (!string.IsNullOrEmpty(err))
                Debug.LogWarning($"{nameof(CombatStarter)}: Encounter intro LLM failed ({err}). Using fallback.", this);

            ui.SetDialogueLineAndAllowAdvance(FallbackBanditEncounterLines[Random.Range(0, FallbackBanditEncounterLines.Length)]);
        }

        yield return new WaitUntil(() => !SimpleRpgDialogueUI.IsDialogueOpen);

        _randomEncounterIntroRoutine = null;
        StartFightWithId(encounterId);
    }
}
