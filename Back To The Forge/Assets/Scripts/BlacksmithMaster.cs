using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Daily quest + end-of-day sell flow. Assign the player <see cref="Inventory"/> and a pool of
/// <see cref="ItemDefinition"/>s the blacksmith can request (for now only iron ore).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BlacksmithMaster : MonoBehaviour
{
    private const string LogPrefix = "[Blacksmith]";

    [Header("Core")]
    [SerializeField] private Inventory playerInventory;
    [SerializeField] private ItemDefinition[] dailyQuestPool;
    [Tooltip("Sell price multiplier for today's quested item only.")]
    [SerializeField] private float questItemSellMultiplier = 2f;

    [SerializeField] private int startingDay = 1;
    [SerializeField] private int startingGold;

    [Header("Proximity & interact")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private InputActionReference interactAction;
    [Tooltip("While the player is in this object's trigger and presses Interact (E), sell all and advance the day.")]
    [SerializeField] private bool endDayOnInteractWhileInRange = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private int _currentDay;
    private int _playerGold;
    private ItemDefinition _todaysQuestItem;
    private int _playerOverlapCount;
    private Collider2D _collider2D;

    public int CurrentDay => _currentDay;
    public int PlayerGold => _playerGold;
    public ItemDefinition TodaysQuestItem => _todaysQuestItem;
    public float QuestItemSellMultiplier => questItemSellMultiplier;
    public bool PlayerInRange => _playerOverlapCount > 0;

    public string GetQuestSummary()
    {
        if (_todaysQuestItem == null)
            return $"Day {_currentDay}: (no quest assigned — add items to {nameof(dailyQuestPool)})";

        return $"Day {_currentDay}: Find {_todaysQuestItem.DisplayName} (Item #{_todaysQuestItem.ItemId}) — sells for {questItemSellMultiplier:0.#}× today!";
    }

    public event Action OnEconomyChanged;
    public event Action<ItemDefinition> OnDailyQuestRolled;

    private void Awake()
    {
        if (playerInventory == null)
            playerInventory = FindAnyObjectByType<Inventory>();

        _collider2D = GetComponent<Collider2D>();
        if (_collider2D != null && !_collider2D.isTrigger && debugLogs)
            Debug.LogWarning($"{LogPrefix} Collider2D on '{name}' should be a trigger for range detection.", this);

        _currentDay = startingDay;
        _playerGold = startingGold;
    }

    private void OnEnable()
    {
        if (interactAction != null)
            interactAction.action.Enable();
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.action.Disable();
    }

    private void Start()
    {
        if (dailyQuestPool == null || dailyQuestPool.Length == 0)
        {
            Debug.LogWarning($"{nameof(BlacksmithMaster)}: No items in {nameof(dailyQuestPool)} — assign at least one ItemDefinition.", this);
            return;
        }

        if (_todaysQuestItem == null)
            RollDailyQuest();
    }

    private void Update()
    {
        if (_playerOverlapCount <= 0)
            return;

        if (!WasInteractPressedThisFrame())
            return;

        if (debugLogs)
            Debug.Log($"{LogPrefix} Interact pressed while in range of '{name}'.", this);

        if (endDayOnInteractWhileInRange)
            TryEndDayViaInteract();
    }

    private bool WasInteractPressedThisFrame()
    {
        if (interactAction != null && interactAction.action != null)
            return interactAction.action.WasPressedThisFrame();

        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }

    private void TryEndDayViaInteract()
    {
        var result = SellAllAndEndDay();

        if (!debugLogs)
            return;

        if (result.TotalGold == 0 && string.IsNullOrEmpty(result.BreakdownLines))
        {
            Debug.Log($"{LogPrefix} End of day — inventory was empty (Day is now {CurrentDay}, gold {_playerGold}).", this);
            return;
        }

        Debug.Log(
            $"{LogPrefix} End of day — sold for {result.TotalGold}g (quest items: {result.QuestItemGold}g, other: {result.OtherGold}g). " +
            $"Total gold now {_playerGold}. New day: {CurrentDay}.\n{result.BreakdownLines}",
            this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        if (++_playerOverlapCount == 1 && debugLogs)
            Debug.Log($"{LogPrefix} Player entered range of '{name}'.", this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        _playerOverlapCount = Mathf.Max(0, _playerOverlapCount - 1);

        if (_playerOverlapCount == 0 && debugLogs)
            Debug.Log($"{LogPrefix} Player left range of '{name}'.", this);
    }

    /// <summary>Picks a random item from the quest pool as today's commission.</summary>
    public void RollDailyQuest()
    {
        if (dailyQuestPool == null || dailyQuestPool.Length == 0)
            return;

        var valid = 0;
        for (var i = 0; i < dailyQuestPool.Length; i++)
        {
            if (dailyQuestPool[i] != null)
                valid++;
        }

        if (valid == 0)
            return;

        var pick = UnityEngine.Random.Range(0, dailyQuestPool.Length);
        for (var tries = 0; tries < dailyQuestPool.Length; tries++)
        {
            var idx = (pick + tries) % dailyQuestPool.Length;
            if (dailyQuestPool[idx] != null)
            {
                _todaysQuestItem = dailyQuestPool[idx];
                break;
            }
        }

        if (debugLogs && _todaysQuestItem != null)
        {
            Debug.Log(
                $"{LogPrefix} Daily quest set — Day {_currentDay}: '{_todaysQuestItem.DisplayName}' " +
                $"(itemId={_todaysQuestItem.ItemId}, base sell {_todaysQuestItem.BaseSellPrice}g/unit, " +
                $"quest multiplier ×{Mathf.Max(1f, questItemSellMultiplier):0.##}).",
                this);
        }

        OnDailyQuestRolled?.Invoke(_todaysQuestItem);
        OnEconomyChanged?.Invoke();
    }

    /// <summary>Sells every stack in the inventory, applies quest bonus to the quest item, clears bags, advances the day, rolls the next quest.</summary>
    public SellDayResult SellAllAndEndDay()
    {
        if (playerInventory == null)
        {
            Debug.LogError($"{nameof(BlacksmithMaster)}: No {nameof(Inventory)} assigned or found.", this);
            return default;
        }

        if (debugLogs)
            Debug.Log($"{LogPrefix} SellAllAndEndDay started (Day {_currentDay}, preview sell {PreviewSellTotal()}g).", this);

        var slots = playerInventory.GetSlots();
        var total = 0;
        var questGold = 0;
        var otherGold = 0;
        var sb = new StringBuilder(128);

        for (var i = 0; i < Inventory.MaxSlots; i++)
        {
            if (i >= slots.Length)
                break;

            var slot = slots[i];
            if (slot.IsEmpty)
                continue;

            var unitBase = Mathf.Max(0, slot.item.BaseSellPrice);
            var isQuest = _todaysQuestItem != null && slot.item == _todaysQuestItem;
            var mult = isQuest ? Mathf.Max(1f, questItemSellMultiplier) : 1f;
            var unitSold = Mathf.RoundToInt(unitBase * mult);
            var stackGold = unitSold * slot.count;
            total += stackGold;

            if (isQuest)
                questGold += stackGold;
            else
                otherGold += stackGold;

            sb.AppendLine($"{slot.item.DisplayName} x{slot.count} → {stackGold}g");
        }

        _playerGold += total;
        playerInventory.ClearAll();
        _currentDay++;

        RollDailyQuest();

        OnEconomyChanged?.Invoke();

        if (debugLogs)
            Debug.Log($"{LogPrefix} SellAllAndEndDay finished — advanced to Day {_currentDay}, player gold {_playerGold}g.", this);

        return new SellDayResult(total, questGold, otherGold, sb.ToString());
    }

    /// <summary>Gold you would get if you sold now (does not modify inventory).</summary>
    public int PreviewSellTotal()
    {
        if (playerInventory == null)
            return 0;

        var slots = playerInventory.GetSlots();
        var total = 0;

        for (var i = 0; i < Inventory.MaxSlots && i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot.IsEmpty)
                continue;

            var unitBase = Mathf.Max(0, slot.item.BaseSellPrice);
            var isQuest = _todaysQuestItem != null && slot.item == _todaysQuestItem;
            var mult = isQuest ? Mathf.Max(1f, questItemSellMultiplier) : 1f;
            total += Mathf.RoundToInt(unitBase * mult) * slot.count;
        }

        return total;
    }

    public readonly struct SellDayResult
    {
        public readonly int TotalGold;
        public readonly int QuestItemGold;
        public readonly int OtherGold;
        public readonly string BreakdownLines;

        public SellDayResult(int totalGold, int questItemGold, int otherGold, string breakdownLines)
        {
            TotalGold = totalGold;
            QuestItemGold = questItemGold;
            OtherGold = otherGold;
            BreakdownLines = breakdownLines;
        }
    }
}
