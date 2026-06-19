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
    [Tooltip("When used via StartRandomEncounterWithLlmIntro, rolls wild enemies and shows one narrator line from Ollama before combat.")]
    [SerializeField] private bool useLlmWildEncounterIntro = true;

    [SerializeField] private WildEnemyCatalog wildEnemyCatalog;
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
    /// Called by <see cref="RiskyGroundEncounter2D"/> — rolls enemies, Ollama intro, then combat.
    /// </summary>
    public void StartRandomEncounterWithLlmIntro(int encounterId)
    {
        if (!useLlmWildEncounterIntro)
        {
            RollAndStoreWildEncounter();
            if (coordinator == null)
                coordinator = FindAnyObjectByType<CombatAdditiveCoordinator>();
            coordinator?.PauseExplorationForCombat();
            StartFightWithId(encounterId);
            return;
        }

        if (_randomEncounterIntroRoutine != null)
            return;

        _randomEncounterIntroRoutine = StartCoroutine(RandomEncounterIntroThenFight(encounterId));
    }

    private void RollAndStoreWildEncounter()
    {
        CombatSession.ClearRolledWildEncounter();

        var catalog = wildEnemyCatalog;
        if (catalog == null)
        {
            Debug.LogWarning($"{nameof(CombatStarter)}: No {nameof(WildEnemyCatalog)} assigned — combat will use encounter table enemies.", this);
            return;
        }

        var rolled = catalog.RollEncounter();
        if (rolled != null)
            CombatSession.SetRolledWildEncounter(rolled);
    }

    private IEnumerator RandomEncounterIntroThenFight(int encounterId)
    {
        if (coordinator == null)
            coordinator = FindAnyObjectByType<CombatAdditiveCoordinator>();

        coordinator?.PauseExplorationForCombat();

        RollAndStoreWildEncounter();

        var rolled = CombatSession.ActiveWildEncounter;
        if (rolled == null)
        {
            _randomEncounterIntroRoutine = null;
            StartFightWithId(encounterId);
            yield break;
        }

        var ui = SimpleRpgDialogueUI.GetOrCreate();
        var service = ollamaService != null ? ollamaService : OllamaDialogueService.GetOrCreate();

        ui.ShowAwaitingLine(string.Empty, "…");

        var threatSummary = rolled.BuildGroupSummary();
        var flavor = rolled.BuildFlavorContext();
        var motif = EncounterMotifHints[Random.Range(0, EncounterMotifHints.Length)];

        var systemPrompt =
            "You are the narrator in Baldur's Gate 3: bone-dry, clipped, present tense. Output ONE short line only — aim under ~16 words, often starting with You / Your / They're / Something. " +
            "Name the threat clearly using the enemy types given. Hint one sharp sensory beat at most — no lore dumps, no staging directions, no metaphors piled up. " +
            "No quotes, no markdown, no second sentence.";

        var userPrompt =
            $"The party is ambushed. Enemies: {threatSummary}. " +
            (string.IsNullOrWhiteSpace(flavor) ? string.Empty : $"Context: {flavor} ") +
            $"Optional sensory nod: {motif}. One narrator line now.";

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

            ui.SetDialogueLineAndAllowAdvance(BuildFallbackIntroLine(rolled));
        }

        yield return new WaitUntil(() => !SimpleRpgDialogueUI.IsDialogueOpen);

        _randomEncounterIntroRoutine = null;
        StartFightWithId(encounterId);
    }

    private static string BuildFallbackIntroLine(RolledWildEncounter rolled)
    {
        var summary = rolled.BuildGroupSummary();
        return rolled.Count == 1
            ? $"A {rolled.PickPrimaryDisplayName().ToLowerInvariant()} blocks the path."
            : $"{summary} rise from the brush.";
    }
}
