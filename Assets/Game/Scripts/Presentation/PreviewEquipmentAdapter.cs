using UnityEngine;

// One adapter per actual equipment family; the Knowledge presenter never
// branches on ItemType. Adapters only accept visual, never world/thrown, prefabs.
public abstract class PreviewEquipmentAdapter : ScriptableObject
{
    public abstract bool Supports(ItemData item);
    public abstract bool TryAttach(
        ItemData item,
        CharacterVisual actor,
        Transform stagingRoot,
        out WeaponAnimationStyle style
    );
}
