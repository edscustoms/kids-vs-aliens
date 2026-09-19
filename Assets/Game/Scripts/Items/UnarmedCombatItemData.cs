using UnityEngine;

[CreateAssetMenu(menuName = "Items/Unarmed Combat")]
public sealed class UnarmedCombatItemData : ItemData
{
    public SkillData requiredSkill;
    [Tooltip("One deliberate FIRE press per step. Animation mappings own contact and recovery timing.")]
    public CharacterActionId[] attackChain = { CharacterActionId.MeleeLight1, CharacterActionId.MeleeLight2,
        CharacterActionId.MeleeLight3, CharacterActionId.MeleeHeavy, CharacterActionId.Kick };
    [Tooltip("Attacks that plant the supporting foot until their authored recovery window. Held movement resumes afterwards.")]
    public CharacterActionId[] plantedAttacks = { CharacterActionId.Kick, CharacterActionId.HeavyKick };
    public bool RequiresPlantedFeet(CharacterActionId action)
    {
        if (plantedAttacks != null) foreach (var candidate in plantedAttacks) if (candidate == action) return true;
        return false;
    }
    private void OnValidate() => itemType = ItemType.UnarmedCombat;
}
