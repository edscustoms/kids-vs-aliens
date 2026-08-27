using UnityEngine;

public enum MenuPreviewType
{
    Character,
    Weapon,
    Grenade,
}

[CreateAssetMenu(fileName = "MenuPreviewItem", menuName = "Kids VS Aliens/Menu/Preview Item")]
public class MenuPreviewItem : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;
    public MenuPreviewType type;

    [Header("Preview")]
    public GameObject previewPrefab;
}
