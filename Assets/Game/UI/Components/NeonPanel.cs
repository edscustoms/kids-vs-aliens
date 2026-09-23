using UnityEngine;
using UnityEngine.UI;

public enum NeonShape { Panel, Button, Circle, Joystick, Slot, Fill, Divider, Badge }
public enum NeonState { Normal, Selected, Empty, Disabled, Locked }

// One expanded quad, one shared material; vertex data preserves batching across styles.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class NeonPanel : MaskableGraphic
{
    public Color accent = new(0, .88f, 1);
    public Color secondary = new(.86f, .06f, 1);
    [Min(0)] public float radius = 22;
    [Min(0)] public float border = 1.6f;
    [Min(0)] public float glow = 12;
    public NeonShape shape;
    public NeonState state;
    [Tooltip("Subtle frame rails / slot corner marks. Disable for plain containers.")]
    public bool details = true;
    private float interaction;

    protected override void OnEnable() { base.OnEnable(); NeonUIRenderer.Prepare(this); }
    protected override void OnCanvasHierarchyChanged() { base.OnCanvasHierarchyChanged(); NeonUIRenderer.Prepare(this); }
    public void SetAccent(Color value) { if (accent == value) return; accent = value; SetVerticesDirty(); }
    public void SetState(NeonState value) { if (state == value) return; state = value; SetVerticesDirty(); }
    public void SetInteraction(float value) { if (interaction == value) return; interaction = value; SetVerticesDirty(); }
    public void SetShape(NeonShape value) { if (shape == value) return; shape = value; SetVerticesDirty(); }
    public void ApplyTheme(UITheme theme, bool danger = false)
    {
        color = theme != null ? theme.surface : new Color(.014f, .024f, .075f, .985f);
        accent = danger ? (theme != null ? theme.danger : new Color(1, .035f, .48f)) : (theme != null ? theme.primary : new Color(0, .87f, 1));
        secondary = danger ? new Color(.85f, .025f, .7f) : (theme != null ? theme.surfaceAccentEnd : new Color(.94f, .04f, 1));
        radius = theme != null ? theme.panelRadius : 22;
        border = theme != null ? theme.borderThickness : 1.6f;
        glow = theme != null ? theme.surfaceGlow : 12;
        SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        NeonUIRenderer.Quad(mesh, GetPixelAdjustedRect(), color, accent, secondary,
            new Vector4(radius, border, glow, (float)shape),
            new Vector4((float)state, interaction, details ? 1 : 0, 0));
    }
}

internal static class NeonUIRenderer
{
    private static Material shared;
    internal static void Prepare(Graphic graphic)
    {
        if (shared == null) shared = Resources.Load<Material>("NeonUI");
        if (shared != null && graphic.material != shared) graphic.material = shared;
        var canvas = graphic.canvas;
        if (canvas != null) canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1
            | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3;
    }
    internal static void Quad(VertexHelper mesh, Rect rect, Color color, Color accent, Color secondary, Vector4 style, Vector4 state)
    {
        mesh.Clear();
        if (rect.width <= 0 || rect.height <= 0) return;
        float padding = Mathf.Max(2, style.z * 2);
        Vector2 half = rect.size * .5f;
        for (int i = 0; i < 4; i++) {
            var point = new Vector2(i == 0 || i == 3 ? -half.x - padding : half.x + padding,
                i < 2 ? -half.y - padding : half.y + padding);
            var vertex = UIVertex.simpleVert;
            vertex.position = rect.center + point; vertex.color = color;
            // Keep RGB as separate floats: packed 24-bit integers lose low bits
            // during GPU interpolation on camera/world-space canvases.
            vertex.uv0 = new Vector4(point.x, point.y, half.x, half.y);
            vertex.uv1 = new Vector4(accent.r, accent.g, accent.b, style.x);
            vertex.uv2 = new Vector4(secondary.r, secondary.g, secondary.b, style.y);
            vertex.uv3 = new Vector4(style.z, style.w, state.x + (state.z > .5f ? 8 : 0), state.y);
            mesh.AddVert(vertex);
        }
        mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(2, 3, 0);
    }
}
