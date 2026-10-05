using UnityEngine;
using UnityEngine.UI;

/// <summary>Beam-inspired clean segmented rails. One mesh, existing shared neon material.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BikeLaserFrame : MaskableGraphic
{
    private float progress;
    private bool locked;
    protected override void OnEnable() { base.OnEnable(); NeonUIRenderer.Prepare(this); }
    protected override void OnCanvasHierarchyChanged() { base.OnCanvasHierarchyChanged(); NeonUIRenderer.Prepare(this); }
    public void Present(float amount, bool full) { progress = amount; locked = full; SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Vector2 half = rectTransform.rect.size * .5f;
        float pulse = locked ? .8f + .2f * Mathf.Sin(Time.time * 35) : progress;
        Color danger = new Color(1, .025f, .24f, .8f + .2f * progress);
        Color hot = Color.Lerp(new Color(1, .22f, .62f), new Color(1, .92f, .97f), .5f + pulse * .5f);
        float glow = 7 + pulse * 7;
        // Four clean notched corners and hot tips.
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 sign = new Vector2(corner % 2 == 0 ? -1 : 1, corner < 2 ? 1 : -1);
            Vector2 Map(float x, float y) => Vector2.Scale(sign, half - new Vector2(x, y));
            void Line(float x, float y, float xx, float yy, float width, Color tint, float light)
                => Stroke(mesh, Map(x, y), Map(xx, yy), width, tint, light);
            Line(0, 28, 0, 9, 3.5f, hot, glow);
            Line(0, 9, 9, 0, 3.5f, hot, glow);
            Line(9, 0, 31, 0, 3.5f, hot, glow);
            Line(38, 0, 48, 0, 2, danger, glow);
            Line(0, 36, 0, 46, 2, danger, glow);
        }
        // Broken border rails, never a scaled square. Keep a central label notch.
        for (int side = -1; side <= 1; side += 2)
        {
            for (float x = -half.x + 55; x < half.x - 55; x += 23)
            {
                if (Mathf.Abs(x) < 24) continue;
                float end = Mathf.Min(x + 15, half.x - 55);
                Stroke(mesh, new Vector2(x, side * half.y), new Vector2(end, side * half.y), 1.8f, danger, glow * .7f);
                Stroke(mesh, new Vector2(x, side * (half.y - 7)), new Vector2(end, side * (half.y - 7)), .6f, danger * .45f, 2);
            }
            for (float y = -half.y + 54; y < half.y - 54; y += 27)
                Stroke(mesh, new Vector2(side * half.x, y), new Vector2(side * half.x, Mathf.Min(y + 16, half.y - 54)), 1.5f, danger, glow * .7f);
        }
    }
    private static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint, float glow)
    {
        Vector2 tangent = (b - a).normalized, normal = new Vector2(-tangent.y, tangent.x);
        Vector2 half = new Vector2(Vector2.Distance(a, b) * .5f, width * .5f), center = (a + b) * .5f;
        float padding = Mathf.Max(2, glow * 2);
        int first = mesh.currentVertCount;
        for (int i = 0; i < 4; i++)
        {
            Vector2 local = new Vector2(i == 0 || i == 3 ? -half.x - padding : half.x + padding,
                i < 2 ? -half.y - padding : half.y + padding);
            var v = UIVertex.simpleVert;
            v.position = center + tangent * local.x + normal * local.y; v.color = new Color(1, 1, 1, tint.a);
            v.uv0 = new Vector4(local.x, local.y, half.x, half.y);
            v.uv1 = new Vector4(tint.r, tint.g, tint.b, 1);
            v.uv2 = new Vector4(tint.r, tint.g, tint.b, 0);
            v.uv3 = new Vector4(glow, (float)NeonShape.Fill, 0, 0);
            mesh.AddVert(v);
        }
        mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first + 2, first + 3, first);
    }
}
