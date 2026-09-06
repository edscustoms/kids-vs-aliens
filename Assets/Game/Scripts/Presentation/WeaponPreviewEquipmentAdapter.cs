using UnityEngine;

[CreateAssetMenu(menuName = "Kids VS Aliens/Presentation/Equipment/Weapon Adapter")]
public sealed class WeaponPreviewEquipmentAdapter : PreviewEquipmentAdapter
{
    public override bool Supports(ItemData item) => item is WeaponItemData;

    public override bool TryAttach(
        ItemData item,
        CharacterVisual actor,
        Transform stagingRoot,
        out WeaponAnimationStyle style
    )
    {
        style = WeaponAnimationStyle.Unarmed;
        if (
            !(item is WeaponItemData weapon)
            || !PreviewVisualSafety.IsVisualPrefab(weapon.equippedPrefab)
            || actor == null
            || !actor.HasWeaponSocket
        )
            return false;
        WeaponInstance template = weapon.equippedPrefab.GetComponent<WeaponInstance>();
        if (template == null || template.GripPoint == null)
            return false;
        WeaponInstance instance = WeaponInstance.SpawnAttached(weapon, actor, stagingRoot);
        if (instance == null)
            return false;
        style = weapon.animationStyle;
        return true;
    }
}
