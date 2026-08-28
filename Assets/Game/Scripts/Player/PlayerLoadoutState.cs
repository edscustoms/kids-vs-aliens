public static class PlayerLoadoutState
{
    public static bool IsInitialized { get; private set; }

    public static CharacterVisual SelectedCharacter { get; private set; }
    public static WeaponItemData SelectedWeapon { get; private set; }

    public static void Initialize(CharacterVisual characterPrefab, WeaponItemData weaponItemData)
    {
        SelectedCharacter = characterPrefab;
        SelectedWeapon = weaponItemData;
        IsInitialized = true;
    }

    public static void SelectCharacter(CharacterVisual characterPrefab)
    {
        if (characterPrefab == null)
            return;

        SelectedCharacter = characterPrefab;
        IsInitialized = true;
    }

    public static void SelectWeapon(WeaponItemData weaponItemData)
    {
        // Null is a valid explicit selection:
        // it means the player selected NONE.
        SelectedWeapon = weaponItemData;
        IsInitialized = true;
    }

    public static void Clear()
    {
        SelectedCharacter = null;
        SelectedWeapon = null;
        IsInitialized = false;
    }
}
