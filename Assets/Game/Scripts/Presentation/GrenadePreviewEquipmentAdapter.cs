using UnityEngine;

[CreateAssetMenu(menuName = "Kids VS Aliens/Presentation/Equipment/Grenade Adapter")]
public sealed class GrenadePreviewEquipmentAdapter : PreviewEquipmentAdapter
{
    public override bool Supports(ItemData item) => item is GrenadeItemData;

    public override bool TryAttach(
        ItemData item,
        CharacterVisual actor,
        Transform stagingRoot,
        out WeaponAnimationStyle style
    )
    {
        style = WeaponAnimationStyle.Unarmed;
        if (
            !(item is GrenadeItemData grenade)
            || !PreviewVisualSafety.IsVisualPrefab(grenade.heldPrefab)
            || actor == null
            || !actor.HasWeaponSocket
        )
            return false;
        HeldItemGrip template = grenade.heldPrefab.GetComponent<HeldItemGrip>();
        if (template == null || template.GripPoint == null)
            return false;
        GameObject instance = Instantiate(grenade.heldPrefab, stagingRoot);
        if (instance.GetComponent<HeldItemGrip>().AttachTo(actor.WeaponSocket))
            return true;
        if (Application.isPlaying)
            Destroy(instance);
        else
            DestroyImmediate(instance);
        return false;
    }
}
