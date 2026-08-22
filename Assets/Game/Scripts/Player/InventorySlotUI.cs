using UnityEngine;
using UnityEngine.EventSystems;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
    private PlayerInventory inventory;
    private int slotIndex;

    public void Setup(PlayerInventory playerInventory, int index)
    {
        inventory = playerInventory;
        slotIndex = index;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (inventory == null)
            return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            inventory.UseItem(slotIndex);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            inventory.DropItem(slotIndex);
        }
    }
}