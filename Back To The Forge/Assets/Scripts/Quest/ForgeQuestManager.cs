using System;
using UnityEngine;

/// <summary>
/// Holds active forge quest state: commissioned material name, the unique quest item (e.g. Quest Mineral),
/// and the standard ore (e.g. Iron) the blacksmith also takes on turn-in.
/// </summary>
public sealed class ForgeQuestManager : MonoBehaviour
{
    public static ForgeQuestManager Instance { get; private set; }

    public bool QuestActive { get; private set; }
    public string QuestMaterialName { get; private set; }
    /// <summary>Unique commission item (pickups / narrative ore).</summary>
    public ItemDefinition QuestItemAsset { get; private set; }
    /// <summary>Standard ore (e.g. Iron) removed together on forge turn-in.</summary>
    public ItemDefinition ForgeIronTurnInItem { get; private set; }
    public bool OrePickedUp { get; private set; }
    /// <summary>True after a successful forge turn-in until the player picks up commission ore again or the day resets.</summary>
    public bool CommissionDelivered { get; private set; }
    /// <summary>Cached per-unit rate from last <see cref="BeginQuest"/> (for UI). Turn-in pay uses <see cref="BlacksmithMaster.GetUnitSellPrice"/>.</summary>
    public int GoldRewardPerUnit { get; private set; }

    public event Action OnForgeQuestChanged;

    /// <summary>True when <paramref name="def"/> is the active forge commission item (by id or reference).</summary>
    public bool IsCommissionItem(ItemDefinition def)
    {
        if (!QuestActive || def == null || QuestItemAsset == null)
            return false;

        if (QuestItemAsset.ItemId > 0 && def.ItemId > 0)
            return def.ItemId == QuestItemAsset.ItemId;

        return def == QuestItemAsset;
    }

    /// <summary>Inventory label; commission ore uses the AI-invented <see cref="QuestMaterialName"/>.</summary>
    public string GetInventoryDisplayName(ItemDefinition def)
    {
        if (def == null)
            return "?";

        return IsCommissionItem(def) ? QuestMaterialName : def.DisplayName;
    }

    /// <summary>When false, <paramref name="message"/> explains what is still missing.</summary>
    public bool CanTurnIn(Inventory inv, out string message)
    {
        message = null;
        if (!QuestActive || QuestItemAsset == null)
        {
            message = "No active forge commission.";
            return false;
        }

        if (inv == null)
        {
            message = "Can't read your inventory.";
            return false;
        }

        var cQuest = CountOf(inv, QuestItemAsset);
        var needIron = ForgeIronTurnInItem != null;
        var cIron = needIron ? CountOf(inv, ForgeIronTurnInItem) : 0;

        if (cQuest > 0 && (!needIron || cIron > 0))
            return true;

        if (cQuest <= 0 && needIron && cIron <= 0)
        {
            message =
                $"You need {QuestMaterialName} (pick up the commission ore in the world — not today's daily special) " +
                $"and {ForgeIronTurnInItem.DisplayName} from the mines.";
            return false;
        }

        if (cQuest <= 0)
        {
            message =
                $"You need {QuestMaterialName}. Find the commission ore spawn — gathering today's daily special won't count.";
            return false;
        }

        message = $"You have {QuestMaterialName}, but I still need {ForgeIronTurnInItem.DisplayName} from the mines.";
        return false;
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

    public static ForgeQuestManager GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        var existing = FindAnyObjectByType<ForgeQuestManager>();
        if (existing != null)
            return existing;

        var go = new GameObject($"[{nameof(ForgeQuestManager)}]");
        return go.AddComponent<ForgeQuestManager>();
    }

    /// <param name="questInventoryItem">Unique quest material item (e.g. Quest Mineral).</param>
    /// <param name="ironTurnInItem">Standard ore also due on turn-in (e.g. Iron). If null, only the quest item is required.</param>
    public void BeginQuest(
        string inventedMaterialDisplayName,
        ItemDefinition questInventoryItem,
        ItemDefinition ironTurnInItem,
        int goldPerUnit)
    {
        if (QuestActive)
            return;

        if (string.IsNullOrWhiteSpace(inventedMaterialDisplayName) || questInventoryItem == null || goldPerUnit < 1)
            return;

        QuestActive = true;
        QuestMaterialName = inventedMaterialDisplayName.Trim();
        QuestItemAsset = questInventoryItem;
        ForgeIronTurnInItem = ironTurnInItem;
        OrePickedUp = false;
        CommissionDelivered = false;
        GoldRewardPerUnit = goldPerUnit;
        OnForgeQuestChanged?.Invoke();
    }

    public void MarkOrePickedUp()
    {
        if (!QuestActive)
            return;

        OrePickedUp = true;
        CommissionDelivered = false;
        OnForgeQuestChanged?.Invoke();
    }

    /// <summary>
    /// Removes <b>all</b> stacks of the quest item and, when configured, <b>all</b> iron due for the commission; pays per item using
    /// <see cref="BlacksmithMaster.GetUnitSellPrice"/>. Requires at least one quest item and one iron when iron is configured.
    /// </summary>
    /// <returns>Units of quest item removed (0 if turn-in did not run).</returns>
    public int TurnInAndPay(Inventory inv, BlacksmithMaster payTo, out int goldPaid, out int ironUnitsRemoved)
    {
        goldPaid = 0;
        ironUnitsRemoved = 0;
        if (!QuestActive || QuestItemAsset == null || inv == null)
            return 0;

        var cQuest = CountOf(inv, QuestItemAsset);
        var cIron = ForgeIronTurnInItem != null ? CountOf(inv, ForgeIronTurnInItem) : 0;

        if (ForgeIronTurnInItem != null)
        {
            if (cQuest <= 0 || cIron <= 0)
                return 0;
        }
        else if (cQuest <= 0)
        {
            return 0;
        }

        var pay = payTo != null ? payTo : BlacksmithMaster.ResolveEconomy();
        if (pay == null)
        {
            Debug.LogError($"{nameof(ForgeQuestManager)}: No {nameof(BlacksmithMaster)} — cannot pay for forge turn-in.", this);
            return 0;
        }

        var priceQuest = Mathf.Max(1, pay.GetUnitSellPrice(QuestItemAsset, quoteForgeCommissionOre: false));
        var priceIron = ForgeIronTurnInItem != null
            ? Mathf.Max(1, pay.GetUnitSellPrice(ForgeIronTurnInItem, quoteForgeCommissionOre: false))
            : 0;

        var rm = RemoveAllOf(inv, QuestItemAsset);
        var ri = ForgeIronTurnInItem != null ? RemoveAllOf(inv, ForgeIronTurnInItem) : 0;
        ironUnitsRemoved = ri;

        if (rm <= 0 || (ForgeIronTurnInItem != null && ri <= 0))
            return 0;

        goldPaid = rm * priceQuest;
        if (ForgeIronTurnInItem != null && ri > 0)
            goldPaid += ri * priceIron;

        if (goldPaid > 0)
            pay.AddGold(goldPaid);

        OrePickedUp = false;
        CommissionDelivered = rm > 0;
        OnForgeQuestChanged?.Invoke();
        return rm;
    }

    /// <summary>
    /// Clears forge quest state. If inventory still contains quest ore or bundled iron, removes it (cleanup).
    /// When ending the day, call <see cref="BlacksmithMaster.SellAllAndEndDay"/> first so items are sold instead of discarded.
    /// </summary>
    public void ClearForNewDay(Inventory inv)
    {
        if (QuestActive && inv != null)
        {
            if (QuestItemAsset != null)
            {
                var n = CountOf(inv, QuestItemAsset);
                if (n > 0)
                    RemoveAllOf(inv, QuestItemAsset);
            }

            if (ForgeIronTurnInItem != null)
            {
                var n = CountOf(inv, ForgeIronTurnInItem);
                if (n > 0)
                    RemoveAllOf(inv, ForgeIronTurnInItem);
            }
        }

        QuestActive = false;
        OrePickedUp = false;
        CommissionDelivered = false;
        QuestMaterialName = null;
        QuestItemAsset = null;
        ForgeIronTurnInItem = null;
        GoldRewardPerUnit = 0;
        OnForgeQuestChanged?.Invoke();
    }

    /// <summary>Counts commission ore in the player inventory (0 if none).</summary>
    public int CountCommissionOreInInventory(Inventory inv)
    {
        if (!QuestActive || QuestItemAsset == null || inv == null)
            return 0;

        return CountOf(inv, QuestItemAsset);
    }

    /// <summary>True when the player is carrying at least one unit of the commissioned ore.</summary>
    public bool HasCommissionOreInInventory(Inventory inv)
    {
        return CountCommissionOreInInventory(inv) > 0;
    }

    /// <summary>Counts supplementary turn-in ore (e.g. Iron) in the player inventory.</summary>
    public int CountSupplementaryTurnInInInventory(Inventory inv)
    {
        if (!QuestActive || ForgeIronTurnInItem == null || inv == null)
            return 0;

        return CountOf(inv, ForgeIronTurnInItem);
    }

    /// <summary>True when the quest expects supplementary ore on turn-in and the player has none.</summary>
    public bool IsMissingSupplementaryTurnIn(Inventory inv)
    {
        return QuestActive && ForgeIronTurnInItem != null && CountSupplementaryTurnInInInventory(inv) <= 0;
    }

    private static int CountOf(Inventory inv, ItemDefinition def)
    {
        if (def == null || inv == null)
            return 0;

        return def.ItemId > 0 ? inv.CountItemWithId(def.ItemId) : inv.CountItem(def);
    }

    private static int RemoveAllOf(Inventory inv, ItemDefinition def)
    {
        var c = CountOf(inv, def);
        if (c <= 0)
            return 0;

        return def.ItemId > 0 ? inv.TryRemoveItemWithId(def.ItemId, c) : inv.TryRemove(def, c);
    }
}
