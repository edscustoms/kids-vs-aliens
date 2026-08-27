using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MenuPreviewCatalog", menuName = "Kids VS Aliens/Menu/Preview Catalog")]
public class MenuPreviewCatalog : ScriptableObject
{
    [SerializeField]
    private List<MenuPreviewItem> items = new();

    public IReadOnlyList<MenuPreviewItem> Items => items;

    public void GetItems(MenuPreviewType type, List<MenuPreviewItem> results)
    {
        results.Clear();

        foreach (MenuPreviewItem item in items)
        {
            if (item == null)
                continue;

            if (item.type != type)
                continue;

            results.Add(item);
        }
    }
}
