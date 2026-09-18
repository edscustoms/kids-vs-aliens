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
        var background = GetComponent<Image>(); if (background != null) background.color = InterfaceFactory.Navy;
        icons = new Image[slotButtons.Length]; panels = new NeonPanel[slotButtons.Length]; quantities = new TMP_Text[slotButtons.Length];
        for (int i = 0; i < slotButtons.Length; i++)
        {
            var button = slotButtons[i]; if (button.image != null) button.image.enabled = false;
            var surface = InterfaceFactory.Panel(button.transform, "SlotSurface", Vector2.zero, Vector2.one); surface.SetAsFirstSibling();
            panels[i] = surface.GetComponent<NeonPanel>(); panels[i].radius = 12;
            button.targetGraphic = panels[i];
            icons[i] = InterfaceFactory.Rect(button.transform, "ItemIcon", new Vector2(.18f,.3f), new Vector2(.82f,.89f)).gameObject.AddComponent<Image>();
            icons[i].preserveAspect = true; icons[i].raycastTarget = false;
            var label = slotTexts[i]; label.fontSizeMin = 13; label.fontSizeMax = 18; label.enableAutoSizing = true; label.raycastTarget = false;
            label.color = Color.white; label.rectTransform.anchorMin = new Vector2(.035f,.03f); label.rectTransform.anchorMax = new Vector2(.965f,.35f); label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            quantities[i] = InterfaceFactory.Text(button.transform, "Quantity", "", new Vector2(.68f,.34f), new Vector2(.94f,.56f), 18, Color.white, TextAlignmentOptions.Right);
            InterfaceFactory.Text(button.transform, "Number", (i+1).ToString(), new Vector2(.07f,.76f), new Vector2(.3f,.98f), 19, InterfaceFactory.Cyan);
        }
        Refresh();
    }
    private void LateUpdate()
    {
        if (panels == null) return;
        for (int i = 0; i < panels.Length; i++)
        {
            bool selected = inventory.QuickSlotItem(i) != null && inventory.QuickSlotItem(i) == inventory.SelectedItem;
            var desired = selected ? InterfaceFactory.Cyan : InterfaceFactory.Violet;
            if (panels[i].accent != desired) panels[i].SetAccent(desired);
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
                var item = inventory.QuickSlotItem(i); icons[i].sprite = item?.icon; icons[i].enabled = icons[i].sprite != null;
                int count = 0; foreach (var owned in inventory.Items) if (owned == item) count++;
                quantities[i].text = item != null ? count.ToString() : "";
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