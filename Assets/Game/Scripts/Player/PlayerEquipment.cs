using System;
using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    [SerializeField]
    private PlayerCharacter playerCharacter;

    [SerializeField]
    private PlayerShooter playerShooter;

    // Temporary: only for testing dynamic equip
    [SerializeField]
    private WeaponItemData startingWeapon;

    private GameObject equippedWeaponObject;
    private WeaponItemData equippedWeapon;

    public WeaponItemData EquippedWeapon => equippedWeapon;

    public event Action<WeaponItemData> EquippedWeaponChanged;

    private void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();
    }

    private void Start()
    {
        if (startingWeapon != null)
            EquipWeapon(startingWeapon);
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

        ClearEquippedWeapon();

        equippedWeapon = weapon;

        equippedWeaponObject = Instantiate(weapon.equippedPrefab);

        Transform gripPoint = FindChildByName(equippedWeaponObject.transform, "GripPoint");

        if (gripPoint == null)
        {
            Debug.LogError($"Weapon {weapon.itemName} has no GripPoint!");

            ClearEquippedWeapon();
            EquippedWeaponChanged?.Invoke(null);
            return;
        }

        Transform weaponSocket = playerCharacter.ActiveVisual.WeaponSocket;

        AlignGripToSocket(equippedWeaponObject.transform, gripPoint, weaponSocket);

        Transform muzzle = FindChildByName(equippedWeaponObject.transform, "Muzzle");

        if (muzzle == null)
        {
            Debug.LogError($"Weapon {weapon.itemName} has no Muzzle!");

            ClearEquippedWeapon();
            EquippedWeaponChanged?.Invoke(null);
            return;
        }

        playerShooter.EquipWeapon(weapon, muzzle);

        EquippedWeaponChanged?.Invoke(equippedWeapon);
    }

    public void UnequipWeapon()
    {
        ClearEquippedWeapon();

        EquippedWeaponChanged?.Invoke(null);
    }

    private void ClearEquippedWeapon()
    {
        playerShooter.UnequipWeapon();

        if (equippedWeaponObject != null)
            Destroy(equippedWeaponObject);

        equippedWeaponObject = null;
        equippedWeapon = null;
    }

    private void AlignGripToSocket(Transform weapon, Transform gripPoint, Transform weaponSocket)
    {
        Quaternion rotationDelta = weaponSocket.rotation * Quaternion.Inverse(gripPoint.rotation);

        weapon.rotation = rotationDelta * weapon.rotation;

        Vector3 positionDelta = weaponSocket.position - gripPoint.position;

        weapon.position += positionDelta;

        weapon.SetParent(weaponSocket, true);
    }

    private Transform FindChildByName(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform result = FindChildByName(child, childName);

            if (result != null)
                return result;
        }

        return null;
    }
}
