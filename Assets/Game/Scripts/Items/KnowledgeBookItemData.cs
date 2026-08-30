using UnityEngine;

[CreateAssetMenu(
    fileName = "NewKnowledgeBook",
    menuName = "Kids VS Aliens/Items/Knowledge Book"
)]
public sealed class KnowledgeBookItemData : ItemData
{
    [Header("Knowledge")]
    public SkillData skill;

    [Header("Rarity")]
    public RarityTier rarity = RarityTier.Common;

    private void OnValidate()
    {
        itemType = ItemType.KnowledgeBook;
    }
}
