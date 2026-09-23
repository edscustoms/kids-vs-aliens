using UnityEngine;
using UnityEngine.UI;

public static class NeonVisuals
{
    // Reserve equal breathing room for the imported glow, then fit the original
    // sprite aspect inside that area. No sprite remapping or per-frame layout.
    public static Image Icon(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var area = InterfaceFactory.Rect(parent, name + "Area", min, max);
        var image = InterfaceFactory.Rect(area, name, new(.08f,.08f), new(.92f,.92f)).gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }
    // The original Graphic remains the hit target; the procedural shell never
    // enlarges/reduces a joystick's container or changes its handle coordinates.
    public static NeonPanel Replace(Image original, NeonShape shape, UITheme theme)
    {
        var child = original.transform.Find("ProceduralSurface");
        var rect = child != null ? (RectTransform)child : InterfaceFactory.Rect(original.transform, "ProceduralSurface", Vector2.zero, Vector2.one);
        if (rect.GetSiblingIndex() != 0) rect.SetAsFirstSibling();
        var layout = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
        if (!layout.ignoreLayout) layout.ignoreLayout = true;
        var panel = rect.GetComponent<NeonPanel>() ?? rect.gameObject.AddComponent<NeonPanel>();
        panel.raycastTarget = false; panel.ApplyTheme(theme); panel.SetShape(shape);
        original.color = Color.clear;
        return panel;
    }
    public static void Feedback(GameObject owner, NeonPanel surface)
    {
        var feedback = owner.GetComponent<NeonControlFeedback>() ?? owner.AddComponent<NeonControlFeedback>();
        feedback.Configure(surface);
    }
    public static InterfaceGlyph Symbol(Transform parent, InterfaceSymbol symbol, Vector2 min, Vector2 max)
    {
        var glyph = InterfaceFactory.Rect(parent, "Symbol", min, max).gameObject.AddComponent<InterfaceGlyph>();
        glyph.symbol = symbol; glyph.raycastTarget = false; glyph.color = Color.white;
        return glyph;
    }
}
