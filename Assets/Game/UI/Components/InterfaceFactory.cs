using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class InterfaceFactory
{
    private static UITheme theme;
    public static void UseTheme(UITheme value) => theme = value;
    public static Color Navy => theme != null ? theme.surface : new Color(.022f, .03f, .095f, .97f);
    public static Color Cyan => theme != null ? theme.primary : new Color(0, .88f, 1);
    public static Color Violet => theme != null ? theme.secondary : new Color(.49f, .3f, .96f);
    public static Color Magenta => theme != null ? theme.danger : new Color(1, .1f, .64f);
    public static Color Green => theme != null ? theme.success : new Color(.2f, 1, .52f);
    public static Color Muted => theme != null ? theme.mutedText : new Color(.62f, .67f, .85f);
    public static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
        return r;
    }
    public static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, bool danger = false)
    {
        var rect = Rect(parent, name, min, max);
        var panel = rect.gameObject.AddComponent<NeonPanel>();
        panel.ApplyTheme(theme, danger);
        return rect;
    }
    public static TMP_Text Text(Transform parent, string name, string value, Vector2 min, Vector2 max,
        float size = 28, Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
    {
        var text = Rect(parent, name, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        if (theme != null && theme.font != null) text.font = theme.font;
        text.text = value; text.fontSize = size; text.color = color ?? Color.white;
        text.alignment = align; text.raycastTarget = false; text.enableAutoSizing = true;
        text.fontSizeMax = size; text.fontSizeMin = Mathf.Min(size, 18);
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
    public static Button Button(Transform parent, string name, string label, Vector2 min, Vector2 max, UnityAction action, bool danger = false)
    {
        var rect = Panel(parent, name, min, max, danger);
        rect.GetComponent<NeonPanel>().SetShape(NeonShape.Button);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<NeonPanel>();
        if (Application.isPlaying) UIAudioButton.Ensure(button);
        NeonVisuals.Feedback(rect.gameObject, rect.GetComponent<NeonPanel>());
        var colors = ColorBlock.defaultColorBlock; colors.highlightedColor = new Color(.6f, .85f, 1); colors.pressedColor = new Color(.34f, .48f, .75f);
        colors.selectedColor = Color.white; colors.disabledColor = new Color(.4f, .42f, .55f, .5f); button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var caption=Text(rect, "Label", label, new Vector2(.05f,.1f), new Vector2(.95f,.9f), 27, null, TextAlignmentOptions.Center);
        caption.fontStyle=FontStyles.Bold;caption.characterSpacing=1.2f;
        if (action != null) button.onClick.AddListener(action);
        return button;
    }
    public static RectTransform Overlay(Transform parent, string name)
    {
        var root = Rect(parent, name, Vector2.zero, Vector2.one);
        var shade = root.gameObject.AddComponent<Image>(); shade.color = new Color(.003f,.005f,.025f,.68f);
        return root;
    }
    public static RectTransform Scroll(Transform parent, Vector2 min, Vector2 max, out ScrollRect scroll)
    {
        var root = Rect(parent, "Scroll", min,max);
        scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
        var viewport = Rect(root, "Viewport", Vector2.zero, Vector2.one); viewport.gameObject.AddComponent<RectMask2D>();
        var hit = viewport.gameObject.AddComponent<Image>(); hit.color = Color.clear;
        var content = Rect(viewport, "Content", new Vector2(0,1), Vector2.one);
        content.pivot = new Vector2(.5f,1); scroll.viewport = viewport; scroll.content = content;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return content;
    }
    public static string LevelName(string name) => name == "ConstructionSite" ? "Construction Site" : name == "GamePoc" ? "Training Grounds" : name;
}
