using UnityEngine;

[CreateAssetMenu(menuName = "Kids VS Aliens/Items/Unarmed Combat")]
public sealed class UnarmedCombatItemData : ItemData
{
    public SkillData requiredSkill;
    private void OnValidate() => itemType = ItemType.UnarmedCombat;
}
