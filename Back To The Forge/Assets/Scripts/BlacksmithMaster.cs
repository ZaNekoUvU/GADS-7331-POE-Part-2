using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Daily quest + sell-all end-of-day (<see cref="SellAllAndEndDay"/>). Day advances only from
/// <see cref="BlacksmithQuestGiver"/> dialogue — not from standing in range and pressing Interact here.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BlacksmithMaster : MonoBehaviour
{
    private const string LogPrefix = "[Blacksmith]";

    private static bool _warnedMultipleEconomyActors;

    /// <summary>
    /// The blacksmith that should receive gold and UI updates. When a <see cref="BlacksmithQuestGiver"/> exists (combined
    /// forge NPC prefab), returns the <see cref="BlacksmithMaster"/> on that same object so payouts match the HUD.
    /// Otherwise returns any instance (e.g. exploration scene with a standalone smith only).
    /// </summary>
    public static BlacksmithMaster ResolveEconomy()
    {
        var giver = FindAnyObjectByType<BlacksmithQuestGiver>();
        if (giver != null)
        {
            var paired = giver.GetComponent<BlacksmithMaster>();
            if (paired != null)
            {
                WarnIfMultiple(paired);
                return paired;
            }
        }

        var fallback = FindAnyObjectByType<BlacksmithMaster>();
        WarnIfMultiple(fallback);
        return fallback;
    }

    private static void WarnIfMultiple(BlacksmithMaster chosen)
    {
        if (chosen == null || _warnedMultipleEconomyActors)
            return;

        var all = FindObjectsByType<BlacksmithMaster>(FindObjectsInactive.Exclude);
        if (all.Length <= 1)
            return;

        _warnedMultipleEconomyActors = true;
        Debug.LogWarning(
            $"{LogPrefix} {all.Length} {nameof(BlacksmithMaster)} components are loaded — using economy on '{chosen.name}'. " +
            $"If gold stays at 0 in the UI, remove duplicate standalone smith objects or leave only one smith per scene.",
            chosen);
    }

    [Header("Core")]
    [SerializeField] private Inventory playerInventory;
    [SerializeField] private ItemDefinition[] dailyQuestPool;
    [Tooltip("Sell price multiplier for today's daily special and for forge-commission ore (turn-in + end-of-day).")]
    [SerializeField] private float questItemSellMultiplier = 2f;

    [SerializeField] private int startingDay = 1;
    [SerializeField] private int startingGold = 70;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private int _currentDay;
    private int _playerGold;
    private ItemDefinition _todaysQuestItem;
    private readonly HashSet<Collider2D> _playerProximity = new();
    private Collider2D _collider2D;

    public int CurrentDay => _currentDay;
    public int PlayerGold => _playerGold;
    public ItemDefinition TodaysQuestItem => _todaysQuestItem;
    public float QuestItemSellMultiplier => questItemSellMultiplier;
    public bool PlayerInRange => _playerProximity.Count > 0;

    public void AddGold(int amount)
    {
        if (amount <= 0)
            return;

        _playerGold += amount;
        OnGoldAdded?.Invoke(amount);
        OnEconomyChanged?.Invoke();
    }

    /// <summary>Spends player gold if available. Does not go negative.</summary>
    public bool TrySpendGold(int amount)
    {
        if (amount <= 0)
            return true;

        if (_playerGold < amount)
            return false;

        _playerGold -= amount;
        OnEconomyChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Gold per unit for one stack of <paramref name="item"/> using the same rules as end-of-day selling:
    /// base price, or base × <see cref="questItemSellMultiplier"/> if it is today's daily special or forge commission ore.
    /// Set <paramref name="quoteForgeCommissionOre"/> when starting a forge quest to quote the rate that will apply at turn-in.
    /// </summary>
    public int GetUnitSellPrice(ItemDefinition item, bool quoteForgeCommissionOre = false)
    {
        if (item == null)
            return 0;

        var market = ResourceMarketPricing.Instance;
        var basePrice = market != null
            ? market.GetTodayPrice(item)
            : Mathf.Max(0, item.BaseSellPrice);
        var forge = ForgeQuestManager.Instance;
        ItemDefinition forgeAsset = null;
        if (forge != null && forge.QuestActive)
            forgeAsset = forge.QuestItemAsset;
        var matchesLiveForge =
            forgeAsset != null && item != null
            && (item == forgeAsset
                || (forgeAsset.ItemId > 0 && item.ItemId > 0 && forgeAsset.ItemId == item.ItemId));
        var treatAsForgeOre = quoteForgeCommissionOre || matchesLiveForge;
        var isDailySpecial =
            _todaysQuestItem != null && item != null
            && (item == _todaysQuestItem
                || (_todaysQuestItem.ItemId > 0 && item.ItemId > 0 && _todaysQuestItem.ItemId == item.ItemId));
        if (!isDailySpecial && !treatAsForgeOre)
            return Mathf.Max(1, basePrice);

        return Mathf.Max(1, Mathf.RoundToInt(basePrice * Mathf.Max(1f, questItemSellMultiplier)));
    }

    public string GetQuestSummary()
    {
        var forge = ForgeQuestManager.Instance;
        if (forge != null && forge.QuestActive)
        {
            var ironName = forge.ForgeIronTurnInItem != null
                ? forge.ForgeIronTurnInItem.DisplayName
                : "iron";
            return
                $"Day {_currentDay}: Forge commission — bring {forge.QuestMaterialName} (commission pickup) " +
                $"and {ironName} to the smith. Daily special: {(_todaysQuestItem != null ? _todaysQuestItem.DisplayName : "none")}.";
        }

        if (_todaysQuestItem == null)
            return $"Day {_currentDay}: (no quest assigned — add items to {nameof(dailyQuestPool)})";

        return $"Day {_currentDay}: Find {_todaysQuestItem.DisplayName} (Item #{_todaysQuestItem.ItemId}) — sells for {questItemSellMultiplier:0.#}× today!";
    }

    public event Action OnEconomyChanged;
    public event Action<int> OnGoldAdded;
    public event Action<ItemDefinition> OnDailyQuestRolled;

    private void Awake()
    {
        EnsurePlayerInventory();

        _collider2D = GetComponent<Collider2D>();
        if (debugLogs)
            Collider2DTriggerUtil.WarnIfNoTalkTrigger(gameObject, LogPrefix);

        _currentDay = startingDay;
        _playerGold = startingGold;
    }

    /// <summary>Resolves the player's bag (may run before the player exists in Awake; call again before selling).</summary>
    public Inventory EnsurePlayerInventory()
    {
        var pm = PlayerMovement2D.Instance;
        if (pm != null)
        {
            if (pm.TryGetComponent<Inventory>(out var onPlayer))
            {
                playerInventory = onPlayer;
                return playerInventory;
            }

            var onHierarchy = pm.GetComponentInChildren<Inventory>(true);
            if (onHierarchy == null)
                onHierarchy = pm.GetComponentInParent<Inventory>();

            if (onHierarchy != null)
            {
                playerInventory = onHierarchy;
                return playerInventory;
            }
        }

        if (playerInventory != null)
            return playerInventory;

        playerInventory = FindAnyObjectByType<Inventory>();
        return playerInventory;
    }

    private void OnDisable()
    {
        _playerProximity.Clear();
    }

    private void Start()
    {
        EnsurePlayerInventory();

        if (dailyQuestPool == null || dailyQuestPool.Length == 0)
        {
            Debug.LogWarning($"{nameof(BlacksmithMaster)}: No items in {nameof(dailyQuestPool)} — assign at least one ItemDefinition.", this);
            return;
        }

        ResourceMarketPricing.GetOrCreate();

        if (_todaysQuestItem == null)
            RollDailyQuest();
        else
            RollMarketPrices();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!PlayerMovement2D.IsPlayerCharacterCollider(other))
            return;

        if (_playerProximity.Add(other) && _playerProximity.Count == 1 && debugLogs)
            Debug.Log($"{LogPrefix} Player entered range of '{name}'.", this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!PlayerMovement2D.IsPlayerCharacterCollider(other))
            return;

        if (_playerProximity.Remove(other) && _playerProximity.Count == 0 && debugLogs)
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

        RollMarketPrices();

        if (debugLogs && _todaysQuestItem != null)
        {
            var market = ResourceMarketPricing.Instance;
            var todayPrice = market != null
                ? market.GetTodayPrice(_todaysQuestItem)
                : _todaysQuestItem.BaseSellPrice;
            Debug.Log(
                $"{LogPrefix} Daily quest set — Day {_currentDay}: '{_todaysQuestItem.DisplayName}' " +
                $"(itemId={_todaysQuestItem.ItemId}, market {todayPrice}g/unit today, " +
                $"quest multiplier ×{Mathf.Max(1f, questItemSellMultiplier):0.##}).",
                this);
        }

        OnDailyQuestRolled?.Invoke(_todaysQuestItem);
        OnEconomyChanged?.Invoke();
    }

    private void RollMarketPrices()
    {
        var market = ResourceMarketPricing.GetOrCreate();
        market.RollPricesForDay(_currentDay, dailyQuestPool);

        if (!debugLogs || dailyQuestPool == null)
            return;

        var sb = new StringBuilder(256);
        sb.Append($"{LogPrefix} Day {_currentDay} market prices — ");
        var first = true;
        foreach (var item in dailyQuestPool)
        {
            if (item == null || item.ItemId <= 0)
                continue;

            if (!first)
                sb.Append(", ");
            first = false;
            sb.Append(item.DisplayName);
            sb.Append(' ');
            sb.Append(market.GetTodayPrice(item));
            sb.Append('g');
        }

        if (!first)
            Debug.Log(sb.ToString(), this);
    }

    /// <summary>
    /// Player death penalty: discard inventory without selling, advance the day, restore nodes and mercenary roster.
    /// Gold is unchanged.
    /// </summary>
    public void ApplyDeathDayAdvance()
    {
        EnsurePlayerInventory();
        if (playerInventory != null)
            playerInventory.ClearAll();

        _currentDay++;
        HiredCompanionManager.Instance?.ClearHiresForNewDay();
        IronVein.RestoreAllForNewDay();
        RollDailyQuest();
        PlayerPersistentCombatHealth.GetOrCreate()?.ResetToFullHealth();
        OnEconomyChanged?.Invoke();

        if (debugLogs)
            Debug.Log($"{LogPrefix} Death day advance — now Day {_currentDay}, gold kept at {_playerGold}g.", this);
    }

    /// <summary>Sells every stack in the inventory using <see cref="GetUnitSellPrice"/> (daily special + active forge ore share the same bonus), clears bags, advances the day, rolls the next quest.</summary>
    public SellDayResult SellAllAndEndDay()
    {
        EnsurePlayerInventory();
        if (playerInventory == null)
        {
            Debug.LogError($"{nameof(BlacksmithMaster)}: No {nameof(Inventory)} assigned or found — assign the player's inventory or ensure the player is in the scene before selling.", this);
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

            var unitSold = GetUnitSellPrice(slot.item, quoteForgeCommissionOre: false);
            var stackGold = unitSold * slot.count;
            total += stackGold;

            var isQuestTagged =
                (_todaysQuestItem != null
                    && (slot.item == _todaysQuestItem
                        || (_todaysQuestItem.ItemId > 0 && slot.item != null && slot.item.ItemId == _todaysQuestItem.ItemId)))
                || (ForgeQuestManager.Instance != null
                    && ForgeQuestManager.Instance.QuestActive
                    && ForgeQuestManager.Instance.QuestItemAsset != null
                    && slot.item != null
                    && (slot.item == ForgeQuestManager.Instance.QuestItemAsset
                        || (ForgeQuestManager.Instance.QuestItemAsset.ItemId > 0
                            && slot.item.ItemId == ForgeQuestManager.Instance.QuestItemAsset.ItemId)));

            if (isQuestTagged)
                questGold += stackGold;
            else
                otherGold += stackGold;

            sb.AppendLine($"{slot.item.DisplayName} x{slot.count} → {stackGold}g");
        }

        _playerGold += total;
        playerInventory.ClearAll();
        _currentDay++;

        HiredCompanionManager.Instance?.ClearHiresForNewDay();
        IronVein.RestoreAllForNewDay();

        RollDailyQuest();
        PlayerPersistentCombatHealth.GetOrCreate()?.ResetToFullHealth();

        if (total > 0)
            OnGoldAdded?.Invoke(total);
        OnEconomyChanged?.Invoke();

        if (debugLogs)
            Debug.Log($"{LogPrefix} SellAllAndEndDay finished — advanced to Day {_currentDay}, player gold {_playerGold}g.", this);

        return new SellDayResult(total, questGold, otherGold, sb.ToString());
    }

    /// <summary>Gold you would get if you sold now (does not modify inventory).</summary>
    public int PreviewSellTotal()
    {
        EnsurePlayerInventory();
        if (playerInventory == null)
            return 0;

        var slots = playerInventory.GetSlots();
        var total = 0;

        for (var i = 0; i < Inventory.MaxSlots && i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot.IsEmpty)
                continue;

            total += GetUnitSellPrice(slot.item, quoteForgeCommissionOre: false) * slot.count;
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
