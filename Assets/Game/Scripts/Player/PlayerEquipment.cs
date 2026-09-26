using System;
using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    [SerializeField]
    private PlayerCharacter playerCharacter;

    [SerializeField]
    private PlayerShooter playerShooter;

    [Tooltip("Fallback used when GamePoc is launched directly without a menu loadout.")]
    [SerializeField]
    private WeaponItemData startingWeapon;

    private WeaponInstance equippedWeaponInstance;
    private WeaponItemData equippedWeapon;

    public WeaponItemData EquippedWeapon => equippedWeapon;

    public WeaponInstance EquippedWeaponInstance => equippedWeaponInstance;

    public bool IsEquippedWeaponVisible =>
        equippedWeaponInstance != null && equippedWeaponInstance.gameObject.activeSelf;

    /// <summary>Actual selection changes only; null means unequipped.</summary>
    public event Action<WeaponItemData> EquippedWeaponChanged;
    /// <summary>Animation/style presentation only; null means visually unarmed.</summary>
    public event Action<WeaponItemData> WeaponPresentationChanged;
    public WeaponItemData PresentedWeapon => IsEquippedWeaponVisible ? equippedWeapon : null;

    private void Awake()
    {
        if (playerShooter == null) playerShooter = GetComponent<PlayerShooter>();
        if (playerCharacter == null)
        {
            playerCharacter = GetComponent<PlayerCharacter>();
        }
    }

    private void Start()
    {
        if (RunSaveService.IsRestoringScene(gameObject.scene.name)) return;
        // If a menu loadout exists, SelectedWeapon may intentionally be null
        // because the player selected NONE.
        WeaponItemData weaponToEquip = PlayerLoadoutState.IsInitialized
            ? PlayerLoadoutState.SelectedWeapon
            : startingWeapon;

        if (weaponToEquip != null)
        {
            var inventory = GetComponent<PlayerInventory>();
            if (inventory != null && inventory.EnsureOwnedWeapon(weaponToEquip)) EquipWeapon(weaponToEquip);
            else Debug.LogError("Starting weapon could not be added to the player's inventory.", this);
        }
        else
        {
            UnequipWeapon();
        }
    }

    public bool IsEquipped(ItemData item)
    {
        return equippedWeapon == item;
    }

    public void EquipWeapon(WeaponItemData weapon)
    {
        if (weapon == null)
            return;

        if (
            playerCharacter == null
            || playerCharacter.ActiveVisual == null
            || !playerCharacter.ActiveVisual.HasWeaponSocket
        )
        {
            Debug.LogError("Active character has no WeaponSocket!");
            return;
        }

        var inventory = GetComponent<PlayerInventory>();
        if (inventory == null || !inventory.EnsureOwnedWeapon(weapon)) return;
        if (equippedWeapon == weapon && equippedWeaponInstance != null)
        {
            // Rebind after an explicit inventory restore without recreating the
            // mounted representation or changing an existing owned record.
            playerShooter.EquipWeapon(weapon, equippedWeaponInstance.Muzzle);
            if (!IsEquippedWeaponVisible) SetEquippedWeaponPresentationVisible(true);
            return;
        }
        ClearEquippedWeapon();

        WeaponInstance newInstance = WeaponInstance.SpawnAttached(
            weapon,
            playerCharacter.ActiveVisual
        );

        if (newInstance == null)
        {
            PublishEquipmentChange();
            return;
        }

        if (newInstance.Muzzle == null)
        {
            Debug.LogError($"Weapon {weapon.itemName} has no Muzzle assigned on WeaponInstance!");

            Destroy(newInstance.gameObject);

            PublishEquipmentChange();
            return;
        }

        equippedWeapon = weapon;

        equippedWeaponInstance = newInstance;

        playerShooter.EquipWeapon(weapon, equippedWeaponInstance.Muzzle);

        PublishEquipmentChange();
    }

    public void UnequipWeapon()
    {
        if (equippedWeapon == null && equippedWeaponInstance == null) return;
        ClearEquippedWeapon();

        PublishEquipmentChange();
    }

    public void SetEquippedWeaponVisible(bool visible)
    {
        if (equippedWeaponInstance == null)
            return;

        equippedWeaponInstance.gameObject.SetActive(visible);
    }

    /// <summary>Changes appearance only, preserving selected owned state and its timers.</summary>
    public void SetEquippedWeaponPresentationVisible(bool visible)
    {
        SetEquippedWeaponVisible(visible);
        WeaponPresentationChanged?.Invoke(PresentedWeapon);
    }

    private void PublishEquipmentChange()
    {
        EquippedWeaponChanged?.Invoke(equippedWeapon);
        WeaponPresentationChanged?.Invoke(PresentedWeapon);
    }

    private void ClearEquippedWeapon()
    {
        playerShooter?.UnequipWeapon();

        if (equippedWeaponInstance != null)
        {
            Destroy(equippedWeaponInstance.gameObject);
        }

        equippedWeaponInstance = null;
        equippedWeapon = null;
    }
}
