using UnityEngine;

public enum WeaponFireMode
{
    SemiAuto,
    Automatic,
}

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Game/Items/Weapon")]
public class WeaponItemData : ItemData
{
    [Header("Visual")]
    public GameObject equippedPrefab;

    [Header("Weapon Stats")]
    public float damage = 10f;
    public float range = 15f;
    public float fireRate = 4f;

    [Header("Ammo")]
    public int magazineSize = 8;
    public float reloadTime = 1.2f;

    [Header("Fire Mode")]
    public WeaponFireMode fireMode = WeaponFireMode.SemiAuto;

    [Header("Animation")]
    public WeaponAnimationStyle animationStyle;

    [Header("Knowledge Requirement")]
    [Tooltip(
        "Optional. When assigned, the player cannot equip/use this weapon "
            + "until this skill has been acquired."
    )]
    public SkillData requiredSkill;
}
