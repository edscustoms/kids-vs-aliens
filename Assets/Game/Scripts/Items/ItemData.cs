using UnityEngine;

public enum ItemType
{
    Weapon,
    Consumable,
    Armor,
    Key
}

public abstract class ItemData : ScriptableObject
{
    public string itemName;
    public ItemType itemType;
    public Sprite icon;

    [Header("World")]
    public GameObject worldPrefab;
}