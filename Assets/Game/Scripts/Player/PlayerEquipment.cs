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

    public WeaponItemData EquippedWeapon =>
        equippedWeapon;

    public event Action<WeaponItemData>
        EquippedWeaponChanged;

    private void Awake()
    {
        if (playerCharacter == null)
        {
            playerCharacter =
                GetComponent<PlayerCharacter>();
        }
    }

    private void Start()
    {
        // If a menu loadout exists, SelectedWeapon may intentionally be null
        // because the player selected NONE.
        WeaponItemData weaponToEquip =
            PlayerLoadoutState.IsInitialized
                ? PlayerLoadoutState.SelectedWeapon
                : startingWeapon;

        if (weaponToEquip != null)
        {
            EquipWeapon(
                weaponToEquip
            );
        }
        else
        {
            UnequipWeapon();
        }
    }

    public bool IsEquipped(
        ItemData item
    )
    {
        return equippedWeapon == item;
    }

    public void EquipWeapon(
        WeaponItemData weapon
    )
    {
        if (weapon == null)
            return;

        if (
            playerCharacter == null
            || playerCharacter.ActiveVisual == null
            || !playerCharacter.ActiveVisual.HasWeaponSocket
        )
        {
            Debug.LogError(
                "Active character has no WeaponSocket!"
            );
            return;
        }

        ClearEquippedWeapon();

        WeaponInstance newInstance =
            WeaponInstance.SpawnAttached(
                weapon,
                playerCharacter.ActiveVisual
            );

        if (newInstance == null)
        {
            EquippedWeaponChanged?.Invoke(
                null
            );
            return;
        }

        if (newInstance.Muzzle == null)
        {
            Debug.LogError(
                $"Weapon {weapon.itemName} has no Muzzle assigned on WeaponInstance!"
            );

            Destroy(
                newInstance.gameObject
            );

            EquippedWeaponChanged?.Invoke(
                null
            );
            return;
        }

        equippedWeapon =
            weapon;

        equippedWeaponInstance =
            newInstance;

        playerShooter.EquipWeapon(
            weapon,
            equippedWeaponInstance.Muzzle
        );

        EquippedWeaponChanged?.Invoke(
            equippedWeapon
        );
    }

    public void UnequipWeapon()
    {
        ClearEquippedWeapon();

        EquippedWeaponChanged?.Invoke(
            null
        );
    }

    private void ClearEquippedWeapon()
    {
        playerShooter.UnequipWeapon();

        if (equippedWeaponInstance != null)
        {
            Destroy(
                equippedWeaponInstance.gameObject
            );
        }

        equippedWeaponInstance = null;
        equippedWeapon = null;
    }
}
