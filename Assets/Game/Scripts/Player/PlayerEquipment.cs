using System;
using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    [SerializeField]
    private PlayerCharacter playerCharacter;

    [SerializeField]
    private PlayerShooter playerShooter;

    [Tooltip(
        "Fallback used when GamePoc is launched directly or no menu weapon has been selected."
    )]
    [SerializeField]
    private WeaponItemData startingWeapon;

    private GameObject equippedWeaponObject;
    private WeaponItemData equippedWeapon;

    public WeaponItemData EquippedWeapon => equippedWeapon;

    public event Action<WeaponItemData> EquippedWeaponChanged;

    private void Awake()
    {
        if (playerCharacter == null)
        {
            playerCharacter = GetComponent<PlayerCharacter>();
        }
    }

    private void Start()
    {
        WeaponItemData weaponToEquip =
            PlayerLoadoutState.SelectedWeapon != null
                ? PlayerLoadoutState.SelectedWeapon
                : startingWeapon;

        if (weaponToEquip != null)
        {
            EquipWeapon(weaponToEquip);
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

        ClearEquippedWeapon();

        equippedWeapon = weapon;

        equippedWeaponObject = Instantiate(weapon.equippedPrefab);

        PlasmaCoreSetup plasmaSetup =
            equippedWeaponObject.GetComponentInChildren<PlasmaCoreSetup>();

        if (plasmaSetup != null)
        {
            plasmaSetup.Configure(playerCharacter.ActiveVisual.AuraColor);
        }

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
        {
            Destroy(equippedWeaponObject);
        }

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
