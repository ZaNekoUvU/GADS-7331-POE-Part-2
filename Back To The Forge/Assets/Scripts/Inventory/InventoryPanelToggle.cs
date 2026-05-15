using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryPanelToggle : MonoBehaviour
{
    [SerializeField] private InputActionReference inventoryAction;
    [SerializeField] private Inventory inventory;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text inventoryText;

    private void Awake()
    {
        if (inventory == null)
            inventory = FindAnyObjectByType<Inventory>();
    }

    private void OnEnable()
    {
        if (inventoryAction != null)
            inventoryAction.action.Enable();

        if (inventory != null)
            inventory.OnChanged += RefreshText;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        RefreshText();
    }

    private void OnDisable()
    {
        if (inventoryAction != null)
            inventoryAction.action.Disable();

        if (inventory != null)
            inventory.OnChanged -= RefreshText;
    }

    private void Update()
    {
        if (panelRoot == null || PauseMenuController.IsOpen)
            return;

        var held = IsInventoryHeld();
        panelRoot.SetActive(held);

        if (held)
            RefreshText();
    }

    private bool IsInventoryHeld()
    {
        if (inventoryAction != null && inventoryAction.action != null)
            return inventoryAction.action.IsPressed();

        return Keyboard.current != null && Keyboard.current.tabKey.isPressed;
    }

    private void RefreshText()
    {
        if (inventoryText == null || inventory == null)
            return;

        var slots = inventory.GetSlots();
        inventoryText.text = string.Empty;

        for (var i = 0; i < Inventory.MaxSlots; i++)
        {
            string line;
            if (i >= slots.Length || slots[i].IsEmpty)
            {
                line = $"{i + 1}. —";
            }
            else
            {
                var unitPrice = GetTodaySellPrice(slots[i].item);
                line = $"{i + 1}. {GetSlotDisplayName(slots[i].item)} x{slots[i].count} ({unitPrice}g)";
            }

            inventoryText.text += line + "\n";
        }
    }

    private static int GetTodaySellPrice(ItemDefinition item)
    {
        if (item == null)
            return 0;

        var market = ResourceMarketPricing.Instance;
        return market != null ? market.GetTodayPrice(item) : Mathf.Max(1, item.BaseSellPrice);
    }

    private static string GetSlotDisplayName(ItemDefinition item)
    {
        if (item == null)
            return "?";

        var forge = ForgeQuestManager.Instance;
        if (forge != null)
            return forge.GetInventoryDisplayName(item);

        return item.DisplayName;
    }
}
