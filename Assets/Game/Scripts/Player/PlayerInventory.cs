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

    private readonly List<ItemData> items = new();

    public IReadOnlyList<ItemData> Items => items;

    public event Action OnInventoryChanged;

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
                playerEquipment.EquipWeapon((WeaponItemData)item);
                break;

            case ItemType.Consumable:
                break;

            default:
                break;
        }
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
