using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GrenadeSlotUI : MonoBehaviour
{
    [SerializeField]
    private PlayerInventory inventory;

    [SerializeField]
    private PlayerGrenadeController grenadeController;

    [SerializeField]
    private Button selectButton;

    [SerializeField]
    private Image icon;

    [SerializeField]
    private TMP_Text countText;

    [Tooltip("Optional filled Image used to present the current throw charge.")]
    [SerializeField]
    private Image chargeFill;

    [SerializeField]
    private GameObject selectedIndicator;

    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged +=
                Refresh;
        }

        if (grenadeController != null)
        {
            grenadeController.GrenadeSelectionChanged +=
                HandleSelectionChanged;

            grenadeController.ChargeChanged +=
                HandleChargeChanged;
        }

        if (selectButton != null)
        {
            selectButton.onClick.AddListener(
                HandleButtonClicked);
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -=
                Refresh;
        }

        if (grenadeController != null)
        {
            grenadeController.GrenadeSelectionChanged -=
                HandleSelectionChanged;

            grenadeController.ChargeChanged -=
                HandleChargeChanged;
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(
                HandleButtonClicked);
        }
    }

    private void HandleButtonClicked()
    {
        if (grenadeController == null)
            return;

        if (grenadeController.IsGrenadeSelected)
        {
            grenadeController.CancelThrow();
        }
        else
        {
            grenadeController.SelectFirstAvailableGrenade();
        }
    }

    private void HandleSelectionChanged(
        bool selected)
    {
        Refresh();
    }

    private void HandleChargeChanged(
        float charge)
    {
        if (chargeFill != null)
        {
            chargeFill.fillAmount =
                Mathf.Clamp01(charge);
        }
    }

    private void Refresh()
    {
        int count =
            inventory != null
                ? inventory.GrenadeCount
                : 0;

        GrenadeItemData grenade =
            grenadeController != null &&
            grenadeController.SelectedGrenade != null
                ? grenadeController.SelectedGrenade
                : inventory != null
                    ? inventory.GetFirstGrenade()
                    : null;

        if (selectButton != null)
        {
            selectButton.interactable =
                grenade != null;
        }

        if (icon != null)
        {
            icon.sprite =
                grenade != null
                    ? grenade.icon
                    : null;

            icon.enabled =
                icon.sprite != null;
        }

        if (countText != null)
        {
            countText.text =
                count.ToString();
        }

        bool selected =
            grenadeController != null &&
            grenadeController.IsGrenadeSelected;

        if (selectedIndicator != null)
        {
            selectedIndicator.SetActive(
                selected);
        }

        HandleChargeChanged(
            selected && grenadeController != null
                ? grenadeController.Charge01
                : 0f);
    }
}
