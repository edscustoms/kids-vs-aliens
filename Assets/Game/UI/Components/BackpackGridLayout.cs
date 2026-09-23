using UnityEngine;
using UnityEngine.UI;

// Normal uGUI layout/ScrollRect owns the content. Width changes only recalculate
// cell metrics during layout; there is no per-frame hierarchy reconstruction.
[AddComponentMenu("UI/Backpack Grid Layout")]
public sealed class BackpackGridLayout : GridLayoutGroup
{
    public override void CalculateLayoutInputHorizontal()
    {
        constraint = Constraint.FixedColumnCount;
        constraintCount = 5;
        startCorner = Corner.UpperLeft;
        startAxis = Axis.Horizontal;
        childAlignment = TextAnchor.UpperLeft;
        if (padding.left != 16 || padding.right != 16 || padding.top != 16 || padding.bottom != 16)
            padding = new RectOffset(16, 16, 16, 16);
        spacing = new Vector2(16, 16);
        float width = Mathf.Max(1, (rectTransform.rect.width - padding.horizontal - spacing.x * 4) / 5);
        cellSize = new Vector2(width, width * .85f);
        base.CalculateLayoutInputHorizontal();
    }
}
