using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation and configuration around Unity's existing input/event handling.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class UIButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image icon;
    [SerializeField] private UITheme theme;
    [SerializeField] private UIButtonVariant variant;
    [SerializeField] private bool selected;
    private ColorBlock unthemedColors;
    private bool capturedColors;

    public Button Button => button != null ? button : button = GetComponent<Button>();
    public Button.ButtonClickedEvent OnClick => Button.onClick;
    public TMP_Text Label => label;
    public bool Selected => selected;
    public bool Interactable { get => Button.interactable; set => Button.interactable = value; }
    public string Text { get => label != null ? label.text : string.Empty; set { if (label != null) label.text = value; } }
    public Sprite Icon { get => icon != null ? icon.sprite : null; set { if (icon != null) icon.sprite = value; } }

    public void Configure(UITheme style, UIButtonVariant presentation, TMP_Text text = null, Image image = null)
    {
        theme = style;
        variant = presentation;
        label = text;
        icon = image;
        ApplyStyle();
    }

    public void SetSelected(bool value)
    {
        selected = value;
        ApplyStyle();
    }

    private void OnEnable() => ApplyStyle();
    private void OnValidate() => ApplyStyle();

    public void ApplyStyle()
    {
        if (!capturedColors) { unthemedColors = Button.colors; capturedColors = true; }
        var style = theme != null ? theme.GetButtonStyle(variant) : null;
        ColorBlock colors = style != null ? style.colors : unthemedColors;
        if (style != null && Button.targetGraphic is Image background && style.sprite != null)
            background.sprite = style.sprite;
        if (selected)
        {
            Color tint = style != null ? style.selectedColor : colors.selectedColor;
            colors.normalColor = tint;
            colors.highlightedColor = tint;
            colors.selectedColor = tint;
        }
        // Disabled and pressed states still belong to Selectable. Layout, text,
        // font/material, image tint and dimensions remain authored per instance.
        Button.colors = colors;
    }
}
