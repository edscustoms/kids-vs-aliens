using UnityEngine;

public enum MenuPreviewType
{
    Character,
    Weapon,
    Grenade,
}

[CreateAssetMenu(fileName = "MenuPreviewItem", menuName = "Kids VS Aliens/Menu/Preview Item")]
public class MenuPreviewItem : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;
    public MenuPreviewType type;

    [Header("Preview")]
    [Tooltip("Menu-only presentation prefab. May be a wrapper with custom rotation/scale.")]
    public GameObject previewPrefab;

    [Header("Selection")]
    [Tooltip(
        "Selecting this item intentionally clears this loadout slot. Use this for NONE entries."
    )]
    public bool clearsSlot;

    [Header("Gameplay")]
    [Tooltip("Actual CharacterVisual prefab spawned by PlayerCharacter for Character entries.")]
    public CharacterVisual characterPrefab;

    [Tooltip("Actual WeaponItemData equipped by PlayerEquipment for Weapon entries.")]
    public WeaponItemData weaponItemData;
}
