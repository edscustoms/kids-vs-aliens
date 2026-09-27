using UnityEngine;

// Ordinary backpack content; collection, quantities and saving stay with the existing owners.
[CreateAssetMenu(menuName = "Game/Items/Generic Item")]
public sealed class GenericItemData : ItemData
{
    private void OnValidate() => itemType = ItemType.Generic;
}
