using UnityEngine;

[CreateAssetMenu(menuName = "Items/Unarmed Combat")]
public sealed class UnarmedCombatItemData : ItemData
{
    public SkillData requiredSkill;
    private void OnValidate() => itemType = ItemType.UnarmedCombat;
}
