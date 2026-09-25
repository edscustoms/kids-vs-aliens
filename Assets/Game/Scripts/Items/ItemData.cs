using UnityEngine;

public enum ItemType
{
    Weapon,
    Consumable,
    Armor,
    Key,
    KnowledgeBook,
    Grenade,
    UnarmedCombat
}

public abstract class ItemData : ScriptableObject
{
    public string itemName;
    public ItemType itemType;
    public Sprite icon;

    [SerializeField, Tooltip("Identical item data shares one inventory slot and a quantity.")]
    private bool stackable;
    public virtual bool IsStackable => stackable;
    public virtual bool IsUnique => false;

    [Header("World")]
    public GameObject worldPrefab;
}
