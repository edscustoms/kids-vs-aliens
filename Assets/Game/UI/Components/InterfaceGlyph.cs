using UnityEngine;
using UnityEngine.UI;

public enum InterfaceSymbol { Health, Armor, Fire, Jump, Sprint, Move, Pause, Book, Lock, Close }

// Utility symbols share the surface SDF renderer; no font fallbacks or pixel rings.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class InterfaceGlyph : MaskableGraphic
{
    public InterfaceSymbol symbol;
    protected override void OnEnable() { base.OnEnable(); NeonUIRenderer.Prepare(this); }
    protected override void OnCanvasHierarchyChanged() { base.OnCanvasHierarchyChanged(); NeonUIRenderer.Prepare(this); }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        NeonUIRenderer.Quad(mesh, GetPixelAdjustedRect(), color, color, color,
            new Vector4(0, 0, 3, 16 + (float)symbol), Vector4.zero);
    }
}
