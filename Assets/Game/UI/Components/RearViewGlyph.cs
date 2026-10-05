using UnityEngine;
using UnityEngine.UI;

/// <summary>Small vector U-turn arrow for the bike's view-only look-back button.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RearViewGlyph : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); var rect = GetPixelAdjustedRect();
        Vector2 Map(Vector2 point) => rect.center + Vector2.Scale(point, rect.size);
        void Stroke(Vector2 a, Vector2 b)
        {
            a = Map(a); b = Map(b);
            Vector2 normal = new Vector2(a.y - b.y, b.x - a.x).normalized * Mathf.Min(rect.width, rect.height) * .045f;
            int n = mesh.currentVertCount;
            mesh.AddVert(a + normal, color, Vector2.zero); mesh.AddVert(b + normal, color, Vector2.zero);
            mesh.AddVert(b - normal, color, Vector2.zero); mesh.AddVert(a - normal, color, Vector2.zero);
            mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n, n + 2, n + 3);
        }
        Stroke(new Vector2(.27f, -.32f), new Vector2(.27f, .03f));
        Vector2 previous = new Vector2(.27f, .03f);
        for (int i = 1; i <= 10; i++)
        {
            float angle = i * Mathf.PI / 10;
            Vector2 next = new Vector2(Mathf.Cos(angle) * .27f, .03f + Mathf.Sin(angle) * .27f);
            Stroke(previous, next); previous = next;
        }
        Stroke(previous, new Vector2(-.27f, -.24f));
        Stroke(new Vector2(-.27f, -.24f), new Vector2(-.43f, -.08f));
        Stroke(new Vector2(-.27f, -.24f), new Vector2(-.11f, -.08f));
    }
}
