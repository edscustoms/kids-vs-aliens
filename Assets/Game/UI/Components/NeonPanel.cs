using UnityEngine;
using UnityEngine.UI;

// Vector UI: crisp at phone/tablet resolutions without large decorative textures.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class NeonPanel : MaskableGraphic
{
    public Color accent = new Color(0, .88f, 1);
    public Color secondary = new Color(.8f, .12f, 1);
    public float radius = 22;
    public float border = 1.5f;
    public float glow = 5;
    public void SetAccent(Color value) { accent = value; SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); var rect = GetPixelAdjustedRect();
        Ring(mesh, rect, 0, color, color, true);
        Ring(mesh, rect, -border, accent, secondary, false);
        if (glow > 0) {
            var a = accent; var b = secondary; a.a *= .09f; b.a *= .09f;
            Ring(mesh, rect, glow, a, b, false);
        }
    }
    private void Ring(VertexHelper mesh, Rect rect, float thickness, Color left, Color right, bool fill)
    {
        const int steps = 12, count = (steps + 1) * 4;
        int first = mesh.currentVertCount;
        float r = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * .5f);
        if (fill) mesh.AddVert(rect.center, color, Vector2.zero);
        for (int i = 0; i <= count; i++)
        {
            int corner = (i % count) / (steps + 1);
            float angle = (corner * 90 + (i % (steps + 1)) * 90f / steps) * Mathf.Deg2Rad;
            Vector2 center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - r : rect.xMin + r,
                corner < 2 ? rect.yMax - r : rect.yMin + r);
            Vector2 outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 p = center + outward * r;
            Color c = Color.Lerp(left, right, Mathf.InverseLerp(rect.xMin, rect.xMax, p.x));
            mesh.AddVert(p, c, Vector2.zero);
            if (fill) { if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1); }
            else {
                mesh.AddVert(center + outward * Mathf.Max(0, r + thickness), c, Vector2.zero);
                if (i > 0) {
                    int v = first + i * 2;
                    mesh.AddTriangle(v - 2, v - 1, v); mesh.AddTriangle(v, v - 1, v + 1);
                }
            }
        }
    }
}
