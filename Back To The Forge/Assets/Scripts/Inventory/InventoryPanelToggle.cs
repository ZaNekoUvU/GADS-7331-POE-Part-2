using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryPanelToggle : MonoBehaviour
{
    [SerializeField] private InputActionReference inventoryAction;
    [SerializeField] private Inventory inventory;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text inventoryText;

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
        if (inventoryAction == null || panelRoot == null)
            return;

        var held = inventoryAction.action.IsPressed();
        panelRoot.SetActive(held);

        if (held)
            RefreshText();
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
                line = $"{i + 1}. —";
            else
                line = $"{i + 1}. {slots[i].item.DisplayName} x{slots[i].count}";

            inventoryText.text += line + "\n";
        }
    }
}
