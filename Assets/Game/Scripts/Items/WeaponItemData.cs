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
        "Optional. The weapon can be carried and equipped without this skill, "
            + "but its primary action cannot be used until the skill is acquired."
    )]
    public SkillData requiredSkill;
}
