using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum InventoryAddFailure
{
    None,
    InvalidItem,
    Full,
    AlreadyLearned,
}

public class PlayerInventory : MonoBehaviour
{
    [SerializeField]
    private PlayerEquipment playerEquipment;

    [SerializeField]
    private int maxSlots = 25;

    [SerializeField]
    private PlayerSkillState playerSkillState;

    [SerializeField]
    private PlayerGrenadeController playerGrenadeController;

    [SerializeField] private PlayerMeleeController playerMeleeController;

    private readonly List<ItemData> items = new();
    private StarterAssets.StarterAssetsInputs input;

    public IReadOnlyList<ItemData> Items => items;
    // Persisted assignment tokens: nonnegative = owned item, -1 = empty,
    // -2 = the player's authored unarmed capability (ownership remains Knowledge).
    public const int CombatEntry = -2;
    public UnarmedCombatItemData LearnedCombat => playerMeleeController != null
        && playerSkillState != null && playerMeleeController.DefaultCombatItem != null
        && playerSkillState.HasSkill(playerMeleeController.DefaultCombatItem.requiredSkill)
        ? playerMeleeController.DefaultCombatItem : null;
    public ItemData EntryItem(int index) => index == CombatEntry ? LearnedCombat
        : index >= 0 && index < items.Count ? items[index] : null;
    private int[] quickSlots = { -1, -1, -1, -1, -1 };
    public int Capacity => maxSlots;
    public int QuickSlotCount => quickSlots.Length;
    public ItemData SelectedItem => playerGrenadeController != null && playerGrenadeController.IsGrenadeSelected
        ? playerGrenadeController.SelectedGrenade : playerMeleeController != null && playerMeleeController.SelectedItem != null
        ? playerMeleeController.SelectedItem : playerEquipment != null ? playerEquipment.EquippedWeapon : null;
    public int QuickSlotIndex(int slot) => slot >= 0 && slot < quickSlots.Length ? quickSlots[slot] : -1;
    public ItemData QuickSlotItem(int slot) => EntryItem(QuickSlotIndex(slot));
    public void UseQuickSlot(int slot) => UseItem(QuickSlotIndex(slot));
    public void DropQuickSlot(int slot) => DropItem(QuickSlotIndex(slot));
    public int[] CaptureQuickSlots() => (int[])quickSlots.Clone();
    public bool AssignQuickSlot(int slot, int itemIndex)
    {
        if (slot < 0 || slot >= quickSlots.Length || (itemIndex != -1 && EntryItem(itemIndex) == null)) return false;
        for (int i = 0; i < quickSlots.Length; i++) if (itemIndex != -1 && quickSlots[i] == itemIndex) quickSlots[i] = -1;
        quickSlots[slot] = itemIndex; OnInventoryChanged?.Invoke(); return true;
    }
    public bool SwapItems(int from, int to)
    {
        if (from < 0 || to < 0 || from >= items.Count || to >= items.Count) return false;
        (items[from], items[to]) = (items[to], items[from]);
        for (int i = 0; i < quickSlots.Length; i++)
            if (quickSlots[i] == from) quickSlots[i] = to; else if (quickSlots[i] == to) quickSlots[i] = from;
        OnInventoryChanged?.Invoke(); return true;
    }
    public void RestoreSavedItems(IReadOnlyList<ItemData> restored, int[] assignments)
    {
        if (restored.Count > maxSlots || assignments == null || assignments.Length != 5) throw new ArgumentException("Saved inventory does not fit this player.");
        foreach (int index in assignments) if (index < CombatEntry || index >= restored.Count) throw new ArgumentException("Invalid saved quick slot.");
        items.Clear(); foreach (var item in restored) { if (item == null) throw new ArgumentException("Missing saved item."); items.Add(item); }
        quickSlots = (int[])assignments.Clone();
        // Migrate earlier saves that stored the learned capability as a physical item.
        for (int i = items.Count - 1; i >= 0; i--)
            if (items[i] is UnarmedCombatItemData)
            {
                bool learned = items[i] == LearnedCombat;
                for (int slot = 0; slot < quickSlots.Length; slot++)
                    if (quickSlots[slot] == i) quickSlots[slot] = learned ? CombatEntry : -1;
                RemoveItem(i);
            }
        for (int slot = 0; slot < quickSlots.Length; slot++)
            if (quickSlots[slot] == CombatEntry && LearnedCombat == null) quickSlots[slot] = -1;
        RemoveLearnedBooks();
        OnInventoryChanged?.Invoke();
    }
    private void RemoveItem(int index)
    {
        items.RemoveAt(index);
        for (int i = 0; i < quickSlots.Length; i++) if (quickSlots[i] == index) quickSlots[i] = -1; else if (quickSlots[i] > index) quickSlots[i]--;
    }

    public int GrenadeCount
    {
        get
        {
            int count = 0;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is GrenadeItemData)
                    count++;
            }

            return count;
        }
    }

    public event Action OnInventoryChanged;

    private void Awake()
    {
        input = GetComponent<StarterAssets.StarterAssetsInputs>();
        if (playerEquipment == null)
            playerEquipment = GetComponent<PlayerEquipment>();

        if (playerSkillState == null)
            playerSkillState = GetComponent<PlayerSkillState>();

        if (playerGrenadeController == null)
            playerGrenadeController = GetComponent<PlayerGrenadeController>();
        if (playerMeleeController == null)
            playerMeleeController = GetComponent<PlayerMeleeController>();
    }

    private void OnEnable() { if (playerSkillState != null) playerSkillState.SkillUnlocked += RefreshCapabilities; }
    private void OnDisable() { if (playerSkillState != null) playerSkillState.SkillUnlocked -= RefreshCapabilities; }
    private void RefreshCapabilities(SkillData _) => OnInventoryChanged?.Invoke();

    public bool EnsureOwnedWeapon(WeaponItemData weapon)
    {
        if (weapon == null) return true;
        return items.Contains(weapon) || TryAddItem(weapon);
    }

    public bool TryAddItem(ItemData item) => TryAddItem(item, out _);

    public bool TryAddItem(ItemData item, out InventoryAddFailure failure)
    {
        if (!CanAcceptItem(item, out failure)) return false;
        items.Add(item);
        for (int i = 0; i < quickSlots.Length; i++) if (quickSlots[i] == -1) { quickSlots[i] = items.Count - 1; break; }
        OnInventoryChanged?.Invoke();
        return true;
    }

    // Shared eligibility for world pickup and every inventory insertion caller.
    public bool CanAcceptItem(ItemData item, out InventoryAddFailure failure)
    {
        failure = InventoryAddFailure.None;
        if (item == null || item is UnarmedCombatItemData)
        {
            failure = InventoryAddFailure.InvalidItem;
            return false;
        }

        if (item is KnowledgeBookItemData book && book.skill != null)
        {
            if (playerSkillState == null) playerSkillState = GetComponent<PlayerSkillState>();
            if (playerSkillState != null && playerSkillState.HasSkill(book.skill))
            {
                failure = InventoryAddFailure.AlreadyLearned;
                return false;
            }
        }

        if (items.Count >= maxSlots)
        {
            failure = InventoryAddFailure.Full;
            return false;
        }

        return true;
    }

    // Old snapshots and copies collected before a skill was learned can contain
    // dead books. Remove only those entries, repairing assignment indices silently.
    private void RemoveLearnedBooks()
    {
        if (playerSkillState == null) playerSkillState = GetComponent<PlayerSkillState>();
        if (playerSkillState == null) return;
        for (int i = items.Count - 1; i >= 0; i--)
            if (items[i] is KnowledgeBookItemData book && book.skill != null && playerSkillState.HasSkill(book.skill))
                RemoveItem(i);
    }

    public void AddItem(ItemData item)
    {
        TryAddItem(item);
    }

    public bool HasGrenade(GrenadeItemData grenade)
    {
        return grenade != null && items.Contains(grenade);
    }

    public GrenadeItemData GetFirstGrenade()
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is GrenadeItemData grenade)
                return grenade;
        }

        return null;
    }

    public bool TryConsumeGrenade(GrenadeItemData grenade)
    {
        if (grenade == null)
            return false;

        int index = items.IndexOf(grenade);

        if (index < 0)
            return false;

        RemoveItem(index);

        OnInventoryChanged?.Invoke();

        return true;
    }

    public void UseItem(int index)
    {
        if (input != null && !input.CanProcessGameplayInput)
            return;
        ItemData item = EntryItem(index);
        if (item == null) return;

        switch (item.itemType)
        {
            case ItemType.Weapon:
                TryEquipWeapon(item as WeaponItemData);
                break;

            case ItemType.Grenade:
                TrySelectGrenade(item as GrenadeItemData);
                break;

            case ItemType.KnowledgeBook:
                UseKnowledgeBook(index, item as KnowledgeBookItemData);
                break;

            case ItemType.UnarmedCombat:
                playerMeleeController?.SelectCombatItem(item as UnarmedCombatItemData);
                break;

            case ItemType.Consumable:
                break;
        }
    }

    private void TryEquipWeapon(WeaponItemData weapon)
    {
        if (weapon == null)
            return;

        if (playerGrenadeController != null && playerGrenadeController.IsGrenadeSelected)
        {
            playerGrenadeController.CancelThrow();
        }

        playerEquipment.EquipWeapon(weapon);
    }

    private void TrySelectGrenade(GrenadeItemData grenade)
    {
        if (grenade == null || playerGrenadeController == null)
        {
            return;
        }

        playerGrenadeController.SelectGrenade(grenade);
    }

    private void UseKnowledgeBook(int index, KnowledgeBookItemData book)
    {
        if (book == null || book.skill == null)
        {
            Debug.LogWarning("Knowledge book has no SkillData assigned.", this);

            return;
        }

        if (playerSkillState == null)
        {
            Debug.LogWarning("PlayerSkillState is missing on the player.", this);

            return;
        }

        if (playerSkillState.HasSkill(book.skill))
        {
            GetComponent<PlayerFeedback>()
                ?.Report(
                    new GameplayFeedbackEvent(
                        FeedbackCode.KnowledgeAlreadyKnown,
                        book.skill,
                        book,
                        FeedbackAction.Learn
                    )
                );

            return;
        }

        if (!playerSkillState.UnlockSkill(book.skill))
            return;

        // Replace in place: learning can grant a capability even with a full bag.
        // Repeated books must not duplicate an existing granted option.
        if (book.grantedItem is UnarmedCombatItemData)
        {
            for (int slot = 0; slot < quickSlots.Length; slot++)
                if (quickSlots[slot] == index) quickSlots[slot] = book.grantedItem == LearnedCombat ? CombatEntry : -1;
            RemoveItem(index);
        }
        else if (book.grantedItem != null && !items.Contains(book.grantedItem))
            items[index] = book.grantedItem;
        else
            RemoveItem(index);

        RemoveLearnedBooks();
        OnInventoryChanged?.Invoke();
    }

    public void DropItem(int index)
    {
        if (input != null && !input.CanProcessGameplayInput)
            return;
        if (index < 0 || index >= items.Count)
            return;

        ItemData item = items[index];

        if (item.worldPrefab == null)
        {
            Debug.LogWarning($"{item.itemName} has no world prefab.");

            return;
        }

        if (playerEquipment.IsEquipped(item))
        {
            playerEquipment.UnequipWeapon();
        }

        Vector3 dropPosition = transform.position + transform.forward * 2f + Vector3.up * 0.6f;

        var dropped = Instantiate(item.worldPrefab, dropPosition, Quaternion.identity);
        RunWorldObject.TrackSpawn(dropped, item.worldPrefab);

        RemoveItem(index);

        OnInventoryChanged?.Invoke();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            UseQuickSlot(0);

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            UseQuickSlot(1);

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            UseQuickSlot(2);

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
            UseQuickSlot(3);

        if (Keyboard.current.digit5Key.wasPressedThisFrame)
            UseQuickSlot(4);
    }
}
