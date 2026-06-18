using UnityEngine;

/// <summary>
/// Legacy hook — gold is shown in the inventory panel footer (see <see cref="InventoryPanelToggle"/>).
/// </summary>
public class GoldDisplayUI : MonoBehaviour
{
    private void Awake()
    {
        enabled = false;
    }
}
