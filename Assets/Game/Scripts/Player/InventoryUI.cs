using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;

    [SerializeField] private Button[] slotButtons;
    [SerializeField] private TMP_Text[] slotTexts;

    private void Start()
    {
        inventory.OnInventoryChanged += Refresh;

        for (int i = 0; i < slotButtons.Length; i++)
        {
            InventorySlotUI slot =
                slotButtons[i].gameObject.GetComponent<InventorySlotUI>();

            if (slot == null)
            {
                slot =
                    slotButtons[i].gameObject.AddComponent<InventorySlotUI>();
            }

            slot.Setup(inventory, i);
        }

        Refresh();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= Refresh;
    }

    private void Refresh()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            if (i < inventory.Items.Count)
            {
                slotTexts[i].text = inventory.Items[i].itemName;
                slotButtons[i].interactable = true;
            }
            else
            {
                slotTexts[i].text = "Empty";
                slotButtons[i].interactable = false;
            }
        }
    }
}