using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    [SerializeField] private Transform weaponHolder;
    [SerializeField] private PlayerShooter playerShooter;

    // Temporary: only for testing dynamic equip
    [SerializeField] private WeaponItemData startingWeapon;

    private GameObject equippedWeaponObject;
    private WeaponItemData equippedWeapon;

    private void Start()
    {
        if (startingWeapon != null)
        {
            EquipWeapon(startingWeapon);
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

        UnequipWeapon();

        equippedWeapon = weapon;

        equippedWeaponObject = Instantiate(
            weapon.equippedPrefab,
            weaponHolder,
            false
        );

        Transform muzzle = FindChildByName(
            equippedWeaponObject.transform,
            "Muzzle"
        );

        if (muzzle == null)
        {
            Debug.LogError(
                $"Weapon {weapon.itemName} has no Muzzle!"
            );

            return;
        }

        playerShooter.EquipWeapon(
            weapon,
            muzzle
        );
    }

    public void UnequipWeapon()
    {
        playerShooter.UnequipWeapon();

        if (equippedWeaponObject != null)
        {
            Destroy(equippedWeaponObject);
        }

        equippedWeaponObject = null;
        equippedWeapon = null;
    }

    private Transform FindChildByName(
        Transform parent,
        string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform result =
                FindChildByName(child, childName);

            if (result != null)
                return result;
        }

        return null;
    }
}