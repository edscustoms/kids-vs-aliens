using System;
using UnityEngine;

public enum InterfaceIcon { Fighting, PlasmaPistol, Rifle, ElectricGrenade, Medkit, BeamHoistKnowledge, FightingKnowledge, PistolKnowledge, RifleKnowledge, GrenadeKnowledge }

// Presentation overrides live here, never in gameplay or save data.
[CreateAssetMenu(menuName = "UI/Interface Icon Catalog")]
public sealed class InterfaceIconCatalog : ScriptableObject
{
    [Serializable] public struct Entry { public InterfaceIcon role; public Sprite sprite; public ItemData item; public SkillData skill; }
    public Entry[] entries = Array.Empty<Entry>();
    private static InterfaceIconCatalog cached;
    public static InterfaceIconCatalog Current => cached != null ? cached : cached = Resources.Load<InterfaceIconCatalog>("InterfaceIcons");
    public static Sprite ForItem(ItemData item)
    {
        if (item == null) return null;
        if (Current != null) {
            foreach (var entry in Current.entries) if (entry.item == item) return entry.sprite;
            if (item is KnowledgeBookItemData book) return ForSkill(book.skill) ?? item.icon;
        }
        return item.icon;
    }
    public static Sprite ForSkill(SkillData skill)
    {
        if (skill != null && Current != null)
            foreach (var entry in Current.entries) if (entry.skill == skill) return entry.sprite;
        return null;
    }
    public Sprite ForRole(InterfaceIcon role)
    {
        foreach (var entry in entries) if (entry.role == role) return entry.sprite;
        return null;
    }
}
