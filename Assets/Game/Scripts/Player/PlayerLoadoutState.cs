public static class PlayerLoadoutState
{
    public static CharacterVisual SelectedCharacter { get; private set; }
    public static WeaponItemData SelectedWeapon { get; private set; }

    // Important:
    // null + HasWeaponSelection == false = no menu choice was made, use gameplay fallback.
    // null + HasWeaponSelection == true  = player explicitly selected NONE.
    public static bool HasCharacterSelection { get; private set; }
    public static bool HasWeaponSelection { get; private set; }

    public static void SelectCharacter(CharacterVisual characterPrefab)
    {
        if (characterPrefab == null)
            return;

        SelectedCharacter = characterPrefab;
        HasCharacterSelection = true;
    }

    public static void SelectWeapon(WeaponItemData weaponItemData)
    {
        // Null is VALID here: it means the player deliberately selected NONE.
        SelectedWeapon = weaponItemData;
        HasWeaponSelection = true;
    }

    public static void ClearCharacterSelection()
    {
        SelectedCharacter = null;
        HasCharacterSelection = false;
    }

    public static void ClearWeaponSelection()
    {
        SelectedWeapon = null;
        HasWeaponSelection = false;
    }

    public static void Clear()
    {
        ClearCharacterSelection();
        ClearWeaponSelection();
    }
}
