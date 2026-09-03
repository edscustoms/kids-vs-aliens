using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : MonoBehaviour
{
    [SerializeField]
    private PlayerEquipment playerEquipment;

    [SerializeField]
    private int maxSlots = 5;

    [SerializeField]
    private PlayerSkillState playerSkillState;

    private readonly List<ItemData> items = new();

    public IReadOnlyList<ItemData> Items => items;

    public int GrenadeCount
    {
        get
        {
            int count = 0;

            for (int i = 0;
                 i < items.Count;
                 i++)
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
        if (playerEquipment == null)
        {
            playerEquipment = GetComponent<PlayerEquipment>();
        }

        if (playerSkillState == null)
        {
            playerSkillState = GetComponent<PlayerSkillState>();
        }
    }

    public bool TryAddItem(ItemData item)
    {
        if (item == null)
            return false;

        if (items.Count >= maxSlots)
            return false;

        items.Add(item);

        OnInventoryChanged?.Invoke();

        return true;
    }

    // Kept for compatibility with any scene event or external POC script
    // that still calls the original void API. Pickups use TryAddItem so they
    // can remain in the world when capacity is full.
    public void AddItem(ItemData item)
    {
        TryAddItem(item);
    }

    public bool HasGrenade(
        GrenadeItemData grenade)
    {
        return grenade != null &&
               items.Contains(grenade);
    }

    public GrenadeItemData GetFirstGrenade()
    {
        for (int i = 0;
             i < items.Count;
             i++)
        {
            if (items[i] is GrenadeItemData grenade)
                return grenade;
        }

        return null;
    }

    public bool TryConsumeGrenade(
        GrenadeItemData grenade)
    {
        if (grenade == null)
            return false;

        int index =
            items.IndexOf(
                grenade);

        if (index < 0)
            return false;

        items.RemoveAt(index);

        OnInventoryChanged?.Invoke();

        return true;
    }

    public void UseItem(int index)
    {
        if (index < 0 || index >= items.Count)
            return;

        ItemData item = items[index];

        switch (item.itemType)
        {
            case ItemType.Weapon:
                TryEquipWeapon(item as WeaponItemData);
                break;

            case ItemType.KnowledgeBook:
                UseKnowledgeBook(index, item as KnowledgeBookItemData);
                break;

            case ItemType.Consumable:
                break;

            default:
                break;
        }
    }

    private void TryEquipWeapon(WeaponItemData weapon)
    {
        if (weapon == null)
            return;

        playerEquipment.EquipWeapon(weapon);
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
            Debug.Log($"Skill already acquired: {book.skill.DisplayName}", this);

            // Do not consume duplicate books yet.
            // We can later decide whether duplicates give XP.
            return;
        }

        if (!playerSkillState.UnlockSkill(book.skill))
            return;

        items.RemoveAt(index);

        OnInventoryChanged?.Invoke();

        // Temporary POC presentation.
        // Later this routes through the game's messaging/hologram UI.
        Debug.Log($"KNOWLEDGE ACQUIRED: {book.skill.DisplayName}", this);
    }

    public void DropItem(int index)
    {
        if (index < 0 || index >= items.Count)
            return;

        ItemData item = items[index];

        if (item.worldPrefab == null)
        {
            Debug.LogWarning($"{item.itemName} has no world prefab.");
            return;
        }

        // If we're dropping the item currently in our hand,
        // unequip it first.
        if (playerEquipment.IsEquipped(item))
        {
            playerEquipment.UnequipWeapon();
        }

        Vector3 dropPosition = transform.position + transform.forward * 2f + Vector3.up * 0.6f;

        Instantiate(item.worldPrefab, dropPosition, Quaternion.identity);

        items.RemoveAt(index);

        OnInventoryChanged?.Invoke();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            UseItem(0);

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            UseItem(1);

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            UseItem(2);

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
            UseItem(3);

        if (Keyboard.current.digit5Key.wasPressedThisFrame)
            UseItem(4);
    }
}
