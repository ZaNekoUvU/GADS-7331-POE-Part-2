using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Hold Tab to show inventory in an FF-style panel (same look as pause / main menu).
/// </summary>
public class InventoryPanelToggle : MonoBehaviour
{
    [SerializeField] private InputActionReference inventoryAction;
    [SerializeField] private Inventory inventory;

    private UIDocument _document;
    private VisualElement _overlay;
    private VisualElement _slotList;
    private readonly List<string> _slotLineBuffer = new();

    private void Awake()
    {
        DisableLegacyCanvasChildren();

        if (inventory == null)
            inventory = FindAnyObjectByType<Inventory>();

        BuildUi();
        SetOverlayVisible(false);
    }

    private void OnEnable()
    {
        if (inventoryAction != null)
            inventoryAction.action.Enable();

        if (inventory != null)
            inventory.OnChanged += OnInventoryChanged;
    }

    private void OnDisable()
    {
        if (inventoryAction != null)
            inventoryAction.action.Disable();

        if (inventory != null)
            inventory.OnChanged -= OnInventoryChanged;

        SetOverlayVisible(false);
    }

    private void Update()
    {
        if (ShouldForceHide())
        {
            SetOverlayVisible(false);
            return;
        }

        var held = IsInventoryHeld();
        SetOverlayVisible(held);

        if (held)
            RefreshSlotRows();
    }

    private bool ShouldForceHide()
    {
        return PauseMenuController.IsOpen
               || SimpleRpgDialogueUI.IsDialogueOpen
               || ForgeQuestChoiceUI.IsBlockingGameplay;
    }

    /// <summary>Hides old uGUI panel from <c>InventoryHUD</c> prefab so it is not stuck on screen.</summary>
    private void DisableLegacyCanvasChildren()
    {
        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child != null)
                child.gameObject.SetActive(false);
        }

        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            canvas.enabled = false;

        var raycaster = GetComponent<UnityEngine.UI.GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;
    }

    private void BuildUi()
    {
        _document = GetComponent<UIDocument>();
        if (_document == null)
            _document = gameObject.AddComponent<UIDocument>();

        FfStyleMenuUi.ConfigureDocument(_document, 4500);

        _overlay = FfStyleMenuUi.BuildInventoryOverlay(
            _document.rootVisualElement,
            "Inventory",
            "Hold Tab",
            out _slotList);
    }

    private void OnInventoryChanged()
    {
        if (_overlay != null && _overlay.style.display == DisplayStyle.Flex)
            RefreshSlotRows();
    }

    private bool IsInventoryHeld()
    {
        if (inventoryAction != null && inventoryAction.action != null)
            return inventoryAction.action.IsPressed();

        return Keyboard.current != null && Keyboard.current.tabKey.isPressed;
    }

    private void SetOverlayVisible(bool visible)
    {
        if (_overlay != null)
            _overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void RefreshSlotRows()
    {
        if (_slotList == null || inventory == null)
            return;

        _slotLineBuffer.Clear();
        var slots = inventory.GetSlots();

        for (var i = 0; i < Inventory.MaxSlots; i++)
        {
            if (i >= slots.Length || slots[i].IsEmpty)
            {
                _slotLineBuffer.Add($"{i + 1}. —");
                continue;
            }

            var unitPrice = GetTodaySellPrice(slots[i].item);
            var name = GetSlotDisplayName(slots[i].item);
            _slotLineBuffer.Add($"{i + 1}. {name}  x{slots[i].count}  ({unitPrice}g)");
        }

        FfStyleMenuUi.RefreshInventorySlotRows(_slotList, _slotLineBuffer);
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
