using UnityEngine;

public enum CapsuleKind { Plasma, Armor }

// Capsules are run resources in PlayerInventory, not backpack/quick-slot items.
[CreateAssetMenu(menuName = "Game/Items/Capsule")]
public sealed class CapsuleItemData : ItemData
{
    public CapsuleKind kind;
}
