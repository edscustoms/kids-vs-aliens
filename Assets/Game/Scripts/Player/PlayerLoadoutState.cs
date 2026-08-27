using UnityEngine;

public static class PlayerLoadoutState
{
    public static CharacterVisual SelectedCharacter { get; private set; }
    public static WeaponItemData SelectedWeapon { get; private set; }

    public static bool HasCharacter => SelectedCharacter != null;
    public static bool HasWeapon => SelectedWeapon != null;

    public static void SelectCharacter(CharacterVisual characterPrefab)
    {
        if (characterPrefab == null)
            return;

        SelectedCharacter = characterPrefab;
    }

    public static void SelectWeapon(WeaponItemData weaponItemData)
    {
        if (weaponItemData == null)
            return;

        SelectedWeapon = weaponItemData;
    }

    public static void SetLoadout(CharacterVisual characterPrefab, WeaponItemData weaponItemData)
    {
        if (characterPrefab != null)
            SelectedCharacter = characterPrefab;

        if (weaponItemData != null)
            SelectedWeapon = weaponItemData;
    }

    public static void Clear()
    {
        SelectedCharacter = null;
        SelectedWeapon = null;
    }
}
