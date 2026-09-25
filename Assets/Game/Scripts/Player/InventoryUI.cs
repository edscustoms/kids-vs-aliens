using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;

    [SerializeField] private Button[] slotButtons;
    [SerializeField] private TMP_Text[] slotTexts;

    private Image[] icons;
    private NeonPanel[] panels;
    private TMP_Text[] quantities;
    public void ApplyPresentation(UITheme theme)
    {
        var root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = new Vector2(.5f, 0); root.pivot = new Vector2(.5f, 0);
        root.anchoredPosition = new Vector2(0, 24); root.sizeDelta = new Vector2(540, 106);
        var layout = GetComponent<HorizontalLayoutGroup>();
        if (layout != null) { layout.spacing = 9; layout.padding = new RectOffset(8,8,6,6); }
        var background = GetComponent<Image>(); if (background != null) NeonVisuals.Replace(background, NeonShape.Panel, theme);
        icons = new Image[slotButtons.Length]; panels = new NeonPanel[slotButtons.Length]; quantities = new TMP_Text[slotButtons.Length];
        for (int i = 0; i < slotButtons.Length; i++)
        {
            var button = slotButtons[i]; if (button.image != null) button.image.enabled = false;
            var surface = InterfaceFactory.Panel(button.transform, "SlotSurface", Vector2.zero, Vector2.one); surface.SetAsFirstSibling();
            panels[i] = surface.GetComponent<NeonPanel>(); panels[i].radius = 12;
            panels[i].SetShape(NeonShape.Slot);
            NeonVisuals.Feedback(button.gameObject, panels[i]);
            button.targetGraphic = panels[i];
            icons[i] = NeonVisuals.Icon(button.transform, "ItemIcon", new(.11f,.22f), new(.89f,.79f));
            // Keep the serialized label reference for existing wiring, but never
            // render item names in the compact gameplay bar.
            slotTexts[i].enabled = false;
            slotTexts[i].raycastTarget = false;
            quantities[i] = InterfaceFactory.Text(button.transform, "Quantity", "", new Vector2(.64f,.015f), new Vector2(.88f,.29f), 18, Color.white, TextAlignmentOptions.Right);
            InterfaceFactory.Text(button.transform, "Number", (i+1).ToString(), new Vector2(.35f,.75f), new Vector2(.65f,1f), 19, InterfaceFactory.Cyan, TextAlignmentOptions.Center);
        }
        Refresh();
    }
    private void LateUpdate()
    {
        if (panels == null) return;
        for (int i = 0; i < panels.Length; i++)
        {
            bool selected = inventory.QuickSlotItem(i) != null && inventory.QuickSlotItem(i) == inventory.SelectedItem;
            panels[i].SetState(selected ? NeonState.Selected : inventory.QuickSlotItem(i) == null ? NeonState.Empty : NeonState.Normal);
        }
    }

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
            if (icons != null) {
                var item = inventory.QuickSlotItem(i); icons[i].sprite = InterfaceIconCatalog.ForItem(item); icons[i].enabled = icons[i].sprite != null;
                int count = inventory.CountAt(inventory.QuickSlotIndex(i));
                quantities[i].text = item != null && !(item is UnarmedCombatItemData) ? count.ToString() : "";
            }
            if (inventory.QuickSlotItem(i) != null)
            {
                slotTexts[i].text = inventory.QuickSlotItem(i).itemName;
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
