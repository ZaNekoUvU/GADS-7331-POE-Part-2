using System;
using UnityEngine;

/// <summary>
/// Holds active forge quest state across scenes (Ollama-invented material name + which item counts for turn-in).
/// </summary>
public sealed class ForgeQuestManager : MonoBehaviour
{
    public static ForgeQuestManager Instance { get; private set; }

    public bool QuestActive { get; private set; }
    public string QuestMaterialName { get; private set; }
    public ItemDefinition QuestItemAsset { get; private set; }
    public bool OrePickedUp { get; private set; }
    public int GoldRewardPerUnit { get; private set; }

    public event Action OnForgeQuestChanged;

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

    public void BeginQuest(string inventedMaterialDisplayName, ItemDefinition inventoryItem, int goldPerUnit)
    {
        if (QuestActive)
            return;

        if (string.IsNullOrWhiteSpace(inventedMaterialDisplayName) || inventoryItem == null || goldPerUnit <= 0)
            return;

        QuestActive = true;
        QuestMaterialName = inventedMaterialDisplayName.Trim();
        QuestItemAsset = inventoryItem;
        OrePickedUp = false;
        GoldRewardPerUnit = goldPerUnit;
        OnForgeQuestChanged?.Invoke();
    }

    public void MarkOrePickedUp()
    {
        if (!QuestActive)
            return;

        OrePickedUp = true;
        OnForgeQuestChanged?.Invoke();
    }

    /// <summary>
    /// If the player has at least one quest item, removes all of that item, pays gold, keeps the same
    /// commission active, and resets ore pickup so a new vein can spawn. Returns 0 if nothing to turn in.
    /// </summary>
    public int TurnInAndPay(Inventory inv, BlacksmithMaster payTo, out int goldPaid)
    {
        goldPaid = 0;
        if (!QuestActive || QuestItemAsset == null || inv == null)
            return 0;

        var c = inv.CountItem(QuestItemAsset);
        if (c <= 0)
            return 0;

        inv.TryRemove(QuestItemAsset, c);
        goldPaid = c * GoldRewardPerUnit;
        if (payTo != null && goldPaid > 0)
            payTo.AddGold(goldPaid);

        OrePickedUp = false;
        OnForgeQuestChanged?.Invoke();
        return c;
    }

    /// <summary>
    /// Ends the forging day: removes any leftover commissioned ore, clears quest state, notifies listeners.
    /// Call before starting a new Ollama commission for the next day.
    /// </summary>
    public void ClearForNewDay(Inventory inv)
    {
        if (QuestActive && QuestItemAsset != null && inv != null)
            inv.TryRemove(QuestItemAsset, inv.CountItem(QuestItemAsset));

        QuestActive = false;
        OrePickedUp = false;
        QuestMaterialName = null;
        QuestItemAsset = null;
        GoldRewardPerUnit = 0;
        OnForgeQuestChanged?.Invoke();
    }
}
