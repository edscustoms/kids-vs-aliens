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

    public void AddItem(ItemData item)
    {
        if (item == null)
            return;

        if (items.Count >= maxSlots)
        {
            return;
        }

        items.Add(item);

        OnInventoryChanged?.Invoke();
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

        SkillData requiredSkill = weapon.requiredSkill;

        if (requiredSkill != null)
        {
            bool hasRequiredSkill =
                playerSkillState != null && playerSkillState.HasSkill(requiredSkill);

            if (!hasRequiredSkill)
            {
                // POC message only.
                // Later route this through the generic player messaging UI.
                Debug.Log($"KNOWLEDGE REQUIRED: {requiredSkill.DisplayName}", this);

                return;
            }
        }

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
