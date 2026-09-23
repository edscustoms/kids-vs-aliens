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
            UIAudioFeedback.Click();
            inventory.UseQuickSlot(slotIndex);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            UIAudioFeedback.Click();
            inventory.DropQuickSlot(slotIndex);
        }
    }
}
