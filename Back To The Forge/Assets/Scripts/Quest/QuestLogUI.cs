using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Top-right objective panel — visible only while inventory (Tab) is open.
/// </summary>
[DisallowMultipleComponent]
public class QuestLogUI : MonoBehaviour
{
    public static QuestLogUI Instance { get; private set; }

    private const string ObjectiveHeader = "Objective";

    [Header("Objective text")]
    [SerializeField] private string visitBlacksmithObjective = "Visit the blacksmith for work.";
    [SerializeField] private string findResourceFormat = "Find {0} resource.";
    [SerializeField] private string returnMaterialFormat = "Return {0} to the blacksmith.";
    [SerializeField] private string continueExploringObjective = "Continue exploring.";
    [SerializeField] private string collectAdditionalResourceFormat = "Collect {0} from the mines.";

    [SerializeField] private string explorationSceneName = "Exploration Scene";

    private UIDocument _document;
    private VisualElement _panel;
    private Label _headerLabel;
    private Label _bodyLabel;
    private Inventory _inventory;
    private CombatAdditiveCoordinator _combatCoordinator;
    private bool _subscribedToForgeQuest;
    private bool _lastCombatActive;
    private bool _lastInventoryOpen;

    private readonly List<string> _objectiveLines = new();
    private readonly StringBuilder _objectiveText = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfMissing()
    {
        if (Instance != null || FindAnyObjectByType<QuestLogUI>() != null)
            return;

        var go = new GameObject($"[{nameof(QuestLogUI)}]");
        go.AddComponent<QuestLogUI>();
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
        Refresh();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureForgeQuestSubscription();
        RebindInventorySubscription();
        _lastCombatActive = IsCombatActive();
        Refresh();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeForgeQuest();
        UnsubscribeInventory();
    }

    private void LateUpdate()
    {
        var combatActive = IsCombatActive();
        var inventoryOpen = InventoryPanelToggle.IsInventoryOpen;
        if (combatActive == _lastCombatActive && inventoryOpen == _lastInventoryOpen)
            return;

        _lastCombatActive = combatActive;
        _lastInventoryOpen = inventoryOpen;
        RefreshVisibility();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _inventory = null;
        _combatCoordinator = null;
        RebindInventorySubscription();
        _lastCombatActive = IsCombatActive();
        Refresh();
    }

    private void OnForgeQuestChanged()
    {
        Refresh();
    }

    private void OnInventoryChanged()
    {
        Refresh();
    }

    private void BuildUi()
    {
        _document = gameObject.AddComponent<UIDocument>();
        FfStyleMenuUi.ConfigureDocument(_document, 4450);

        var root = _document.rootVisualElement;
        _panel = FfStyleMenuUi.BuildQuestObjectivePanel(root, ObjectiveHeader, out _headerLabel, out _bodyLabel);
        _panel.style.display = DisplayStyle.None;
    }

    private void EnsureForgeQuestSubscription()
    {
        if (_subscribedToForgeQuest)
            return;

        var q = ForgeQuestManager.GetOrCreate();
        if (q == null)
            return;

        q.OnForgeQuestChanged += OnForgeQuestChanged;
        _subscribedToForgeQuest = true;
    }

    private void UnsubscribeForgeQuest()
    {
        if (!_subscribedToForgeQuest)
            return;

        var q = ForgeQuestManager.Instance;
        if (q != null)
            q.OnForgeQuestChanged -= OnForgeQuestChanged;

        _subscribedToForgeQuest = false;
    }

    private void RebindInventorySubscription()
    {
        var target = FindAnyObjectByType<Inventory>();
        if (_inventory == target)
            return;

        UnsubscribeInventory();
        _inventory = target;

        if (_inventory != null)
            _inventory.OnChanged += OnInventoryChanged;
    }

    private void UnsubscribeInventory()
    {
        if (_inventory != null)
            _inventory.OnChanged -= OnInventoryChanged;
        _inventory = null;
    }

    public void RefreshVisibility() => Refresh();

    private void Refresh()
    {
        if (_panel == null || _bodyLabel == null)
            return;

        EnsureForgeQuestSubscription();
        RebindInventorySubscription();

        if (!ShouldShow())
        {
            _panel.style.display = DisplayStyle.None;
            return;
        }

        _panel.style.display = DisplayStyle.Flex;
        if (_headerLabel != null)
            _headerLabel.text = ObjectiveHeader;
        _bodyLabel.text = BuildObjectiveText();
    }

    private string BuildObjectiveText()
    {
        _objectiveLines.Clear();
        AppendPrimaryObjective(_objectiveLines);
        AppendSupplementaryObjectives(_objectiveLines);

        _objectiveText.Clear();
        for (var i = 0; i < _objectiveLines.Count; i++)
        {
            if (i > 0)
                _objectiveText.Append('\n');
            _objectiveText.Append(_objectiveLines[i]);
        }

        return _objectiveText.ToString();
    }

    private void AppendPrimaryObjective(List<string> lines)
    {
        var q = ForgeQuestManager.Instance;
        if (q == null || !q.QuestActive || string.IsNullOrWhiteSpace(q.QuestMaterialName))
        {
            lines.Add(visitBlacksmithObjective);
            return;
        }

        var materialName = q.QuestMaterialName;
        var carryingOre = q.HasCommissionOreInInventory(_inventory);

        if (carryingOre || q.OrePickedUp)
            lines.Add(string.Format(returnMaterialFormat, materialName));
        else if (!q.CommissionDelivered || QuestMineralSpawner.TryGetActiveSpawnPosition(out _))
            lines.Add(string.Format(findResourceFormat, materialName));
        else
            lines.Add(continueExploringObjective);
    }

    private void AppendSupplementaryObjectives(List<string> lines)
    {
        var q = ForgeQuestManager.Instance;
        if (q == null || !q.QuestActive || !q.IsMissingSupplementaryTurnIn(_inventory))
            return;

        var item = q.ForgeIronTurnInItem;
        if (item == null)
            return;

        lines.Add(string.Format(collectAdditionalResourceFormat, item.DisplayName));
    }

    private bool IsCombatActive()
    {
        var coordinator = _combatCoordinator != null ? _combatCoordinator : FindAnyObjectByType<CombatAdditiveCoordinator>();
        if (coordinator != null)
            _combatCoordinator = coordinator;

        return coordinator != null && coordinator.IsCombatActiveOrLoading;
    }

    private bool ShouldShow()
    {
        if (!InventoryPanelToggle.IsInventoryOpen)
            return false;

        if (IsCombatActive())
            return false;

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return false;

        if (scene.name == PauseMenuController.DefaultMainMenuSceneName)
            return false;

        if (!string.IsNullOrEmpty(explorationSceneName))
        {
            if (scene.name == explorationSceneName)
                return true;

            var exploration = SceneManager.GetSceneByName(explorationSceneName);
            if (exploration.IsValid() && exploration.isLoaded)
                return true;

            return false;
        }

        return true;
    }
}
