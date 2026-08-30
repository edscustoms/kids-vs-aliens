using UnityEngine;

public enum ItemType
{
    Weapon,
    Consumable,
    Armor,
    Key,
    KnowledgeBook
}

public abstract class ItemData : ScriptableObject
{
    public string itemName;
    public ItemType itemType;
    public Sprite icon;

    [Header("World")]
    public GameObject worldPrefab;
}