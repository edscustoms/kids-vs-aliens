using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MenuController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField]
    private MenuPreviewCatalog catalog;

    [Header("Preview")]
    [SerializeField]
    private MenuPreviewStage previewStage;

    [Header("Type Carousel")]
    [SerializeField]
    private Button previousTypeButton;

    [SerializeField]
    private Button nextTypeButton;

    [SerializeField]
    private TMP_Text typeNameText;

    [Header("Item Carousel")]
    [SerializeField]
    private Button previousItemButton;

    [SerializeField]
    private Button nextItemButton;

    [SerializeField]
    private TMP_Text itemNameText;

    [Header("Selection")]
    [SerializeField]
    private Button selectButton;

    [SerializeField]
    private TMP_Text selectButtonText;

    [Header("Loadout Preview")]
    [SerializeField]
    private Button previewButton;

    [Header("Exit / Back")]
    [Tooltip("Text inside the existing Exit button. Changes to BACK while previewing the loadout.")]
    [SerializeField]
    private TMP_Text exitBackButtonText;

    [Header("Game")]
    [SerializeField]
    private string gameSceneName = "GamePoc";

    private readonly List<MenuPreviewItem> currentItems = new();

    private MenuPreviewType currentType;
    private int currentItemIndex;

    private MenuPreviewItem selectedCharacterItem;
    private MenuPreviewItem selectedWeaponItem;

    private bool isLoadoutPreviewMode;

    private void Awake()
    {
        previousTypeButton.onClick.AddListener(PreviousType);
        nextTypeButton.onClick.AddListener(NextType);

        previousItemButton.onClick.AddListener(PreviousItem);
        nextItemButton.onClick.AddListener(NextItem);

        if (selectButton != null)
            selectButton.onClick.AddListener(SelectCurrentItem);

        if (previewButton != null)
            previewButton.onClick.AddListener(EnterLoadoutPreview);
    }

    private void Start()
    {
        currentType = MenuPreviewType.Character;
        currentItemIndex = 0;

        ResolveInitialSelections();

        RefreshType();
        RefreshModeUI();
    }

    private void OnDestroy()
    {
        previousTypeButton.onClick.RemoveListener(PreviousType);
        nextTypeButton.onClick.RemoveListener(NextType);

        previousItemButton.onClick.RemoveListener(PreviousItem);
        nextItemButton.onClick.RemoveListener(NextItem);

        if (selectButton != null)
            selectButton.onClick.RemoveListener(SelectCurrentItem);

        if (previewButton != null)
            previewButton.onClick.RemoveListener(EnterLoadoutPreview);
    }

    // =====================================================
    // TYPE CAROUSEL
    // =====================================================

    private void PreviousType()
    {
        ChangeType(-1);
    }

    private void NextType()
    {
        ChangeType(1);
    }

    private void ChangeType(int direction)
    {
        int typeCount = Enum.GetValues(typeof(MenuPreviewType)).Length;

        int newIndex = (int)currentType + direction;

        if (newIndex < 0)
            newIndex = typeCount - 1;

        if (newIndex >= typeCount)
            newIndex = 0;

        currentType = (MenuPreviewType)newIndex;

        currentItemIndex = 0;

        RefreshType();
    }

    // =====================================================
    // ITEM CAROUSEL
    // =====================================================

    private void PreviousItem()
    {
        ChangeItem(-1);
    }

    private void NextItem()
    {
        ChangeItem(1);
    }

    private void ChangeItem(int direction)
    {
        if (currentItems.Count == 0)
            return;

        currentItemIndex += direction;

        if (currentItemIndex < 0)
            currentItemIndex = currentItems.Count - 1;

        if (currentItemIndex >= currentItems.Count)
            currentItemIndex = 0;

        RefreshItem();
    }

    private void RefreshType()
    {
        typeNameText.text = currentType.ToString().ToUpperInvariant();

        catalog.GetItems(currentType, currentItems);

        currentItemIndex = 0;

        RefreshItem();
    }

    private void RefreshItem()
    {
        if (currentItems.Count == 0)
        {
            itemNameText.text = "NONE";

            previousItemButton.interactable = false;
            nextItemButton.interactable = false;

            RefreshSelectButton();

            // In loadout preview mode we KEEP showing the selected loadout,
            // even if the currently browsed category has no items.
            if (!isLoadoutPreviewMode)
                previewStage.Clear();

            return;
        }

        previousItemButton.interactable = currentItems.Count > 1;

        nextItemButton.interactable = currentItems.Count > 1;

        MenuPreviewItem item = CurrentItem;

        itemNameText.text = item.displayName.ToUpperInvariant();

        RefreshSelectButton();

        if (isLoadoutPreviewMode)
        {
            ShowSelectedLoadout();
        }
        else
        {
            previewStage.Show(item.previewPrefab);
        }
    }

    // =====================================================
    // SELECTION
    // =====================================================

    public void SelectCurrentItem()
    {
        MenuPreviewItem item = CurrentItem;

        if (item == null)
            return;

        switch (item.type)
        {
            case MenuPreviewType.Character:
            {
                if (item.characterPrefab == null)
                {
                    Debug.LogWarning($"{item.name}: Character menu item has no characterPrefab.");
                    return;
                }

                selectedCharacterItem = item;

                PlayerLoadoutState.SelectCharacter(item.characterPrefab);

                break;
            }

            case MenuPreviewType.Weapon:
            {
                if (!CanSelectItem(item))
                {
                    Debug.LogWarning(
                        $"{item.name}: Weapon menu item is not configured. "
                            + "Assign Weapon Item Data, or enable Clears Slot for a NONE entry."
                    );
                    return;
                }

                selectedWeaponItem = item;

                PlayerLoadoutState.SelectWeapon(item.clearsSlot ? null : item.weaponItemData);

                break;
            }

            case MenuPreviewType.Grenade:
            {
                // Future loadout slot.
                Debug.Log("Grenade selection is not wired yet.");
                return;
            }
        }

        RefreshSelectButton();

        // This is the nice UX part:
        // while PREVIEW is active, selecting a different item immediately
        // refreshes the combined character + weapon presentation.
        if (isLoadoutPreviewMode)
        {
            ShowSelectedLoadout();
        }
    }

    private void RefreshSelectButton()
    {
        if (selectButton == null)
            return;

        MenuPreviewItem item = CurrentItem;

        bool canSelect = CanSelectItem(item);

        bool selected = IsSelectedItem(item);

        selectButton.interactable = canSelect && !selected;

        if (selectButtonText != null)
        {
            selectButtonText.text = selected ? "SELECTED" : "SELECT";
        }
    }

    private bool CanSelectItem(MenuPreviewItem item)
    {
        if (item == null)
            return false;

        return item.type switch
        {
            MenuPreviewType.Character => !item.clearsSlot && item.characterPrefab != null,

            MenuPreviewType.Weapon => item.clearsSlot || item.weaponItemData != null,

            _ => false,
        };
    }

    private bool IsSelectedItem(MenuPreviewItem item)
    {
        if (item == null)
            return false;

        return item.type switch
        {
            MenuPreviewType.Character => item == selectedCharacterItem,

            MenuPreviewType.Weapon => item == selectedWeaponItem,

            _ => false,
        };
    }

    // =====================================================
    // LOADOUT PREVIEW MODE
    // =====================================================

    public void EnterLoadoutPreview()
    {
        if (selectedCharacterItem == null)
        {
            Debug.LogWarning("Cannot preview loadout: no selected character.");
            return;
        }

        isLoadoutPreviewMode = true;

        RefreshModeUI();
        ShowSelectedLoadout();
    }

    private void ExitLoadoutPreview()
    {
        isLoadoutPreviewMode = false;

        RefreshModeUI();

        // Return to the exact category/item the player was browsing.
        RefreshItem();
    }

    private void ShowSelectedLoadout()
    {
        if (selectedCharacterItem == null || selectedCharacterItem.previewPrefab == null)
        {
            previewStage.Clear();
            return;
        }

        previewStage.ShowLoadout(
            selectedCharacterItem.previewPrefab,
            selectedWeaponItem != null ? selectedWeaponItem.weaponItemData : null
        );
    }

    private void RefreshModeUI()
    {
        if (previewButton != null)
        {
            // We deliberately KEEP carousel + SELECT active in preview mode.
            // Only PREVIEW itself becomes unavailable.
            previewButton.interactable = !isLoadoutPreviewMode && selectedCharacterItem != null;
        }

        if (exitBackButtonText != null)
        {
            exitBackButtonText.text = isLoadoutPreviewMode ? "BACK" : "EXIT";
        }

        RefreshSelectButton();
    }

    // =====================================================
    // DEFAULT / EXISTING SELECTIONS
    // =====================================================

    private void ResolveInitialSelections()
    {
        if (PlayerLoadoutState.HasCharacterSelection)
        {
            selectedCharacterItem = FindCharacterItem(PlayerLoadoutState.SelectedCharacter);
        }

        if (PlayerLoadoutState.HasWeaponSelection)
        {
            selectedWeaponItem = FindWeaponItem(PlayerLoadoutState.SelectedWeapon);
        }

        if (selectedCharacterItem == null)
        {
            selectedCharacterItem = FindDefaultItem(MenuPreviewType.Character);
        }

        if (selectedWeaponItem == null)
        {
            selectedWeaponItem = FindDefaultItem(MenuPreviewType.Weapon);
        }

        SyncLoadoutState();
    }

    private MenuPreviewItem FindCharacterItem(CharacterVisual character)
    {
        if (character == null)
            return null;

        foreach (MenuPreviewItem item in catalog.Items)
        {
            if (
                item != null
                && item.type == MenuPreviewType.Character
                && item.characterPrefab == character
            )
            {
                return item;
            }
        }

        return null;
    }

    private MenuPreviewItem FindWeaponItem(WeaponItemData weapon)
    {
        foreach (MenuPreviewItem item in catalog.Items)
        {
            if (item == null || item.type != MenuPreviewType.Weapon)
            {
                continue;
            }

            if (weapon == null && item.clearsSlot)
            {
                return item;
            }

            if (weapon != null && !item.clearsSlot && item.weaponItemData == weapon)
            {
                return item;
            }
        }

        return null;
    }

    private MenuPreviewItem FindDefaultItem(MenuPreviewType type)
    {
        // Prefer a real item as the initial default.
        // This preserves the current behaviour where the player starts
        // with the first configured weapon instead of automatically NONE.
        foreach (MenuPreviewItem item in catalog.Items)
        {
            if (item == null || item.type != type || item.clearsSlot)
            {
                continue;
            }

            if (CanSelectItem(item))
                return item;
        }

        // If a category only contains NONE, that is still a valid default.
        foreach (MenuPreviewItem item in catalog.Items)
        {
            if (item == null || item.type != type)
            {
                continue;
            }

            if (CanSelectItem(item))
                return item;
        }

        return null;
    }

    private void SyncLoadoutState()
    {
        if (selectedCharacterItem != null && selectedCharacterItem.characterPrefab != null)
        {
            PlayerLoadoutState.SelectCharacter(selectedCharacterItem.characterPrefab);
        }

        if (selectedWeaponItem != null)
        {
            PlayerLoadoutState.SelectWeapon(
                selectedWeaponItem.clearsSlot ? null : selectedWeaponItem.weaponItemData
            );
        }
    }

    // =====================================================
    // GAME / EXIT
    // =====================================================

    public void PlayGame()
    {
        SyncLoadoutState();

        SceneManager.LoadScene(gameSceneName);
    }

    public void ExitGame()
    {
        if (isLoadoutPreviewMode)
        {
            ExitLoadoutPreview();
            return;
        }

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private MenuPreviewItem CurrentItem
    {
        get
        {
            if (currentItemIndex < 0 || currentItemIndex >= currentItems.Count)
            {
                return null;
            }

            return currentItems[currentItemIndex];
        }
    }
}
