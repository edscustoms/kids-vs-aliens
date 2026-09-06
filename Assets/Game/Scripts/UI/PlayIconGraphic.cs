using UnityEngine;
using UnityEngine.UI;

public sealed class PlayIconGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        Rect rect = GetPixelAdjustedRect();
        vertices.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
        vertices.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
        vertices.AddVert(new Vector3(rect.xMax, rect.center.y), color, Vector2.zero);
        vertices.AddTriangle(0, 1, 2);
    }
}
