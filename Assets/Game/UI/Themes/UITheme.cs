using System;
using UnityEngine;
using UnityEngine.UI;

public enum UIButtonVariant { Pill, IconCircle }

[CreateAssetMenu(menuName = "UI/Theme")]
public sealed class UITheme : ScriptableObject
{
    [Serializable]
    public sealed class ButtonStyle
    {
        public Sprite sprite;
        public ColorBlock colors = ColorBlock.defaultColorBlock;
        public Color selectedColor = new Color(0.2f, 0.9f, 1f, 1f);
    }

    [Header("Shared interface palette")]
    public Color surface = new Color(.022f, .03f, .095f, .97f);
    public Color primary = new Color(0, .88f, 1);
    public Color secondary = new Color(.49f, .3f, .96f);
    public Color danger = new Color(1, .1f, .64f);
    public Color success = new Color(.2f, 1, .52f);
    public Color mutedText = new Color(.62f, .67f, .85f);
    public TMPro.TMP_FontAsset font;
    [Min(0)] public float panelRadius = 22;
    [Min(0)] public float borderGlow = 5;
    public ButtonStyle pill = new ButtonStyle();
    public ButtonStyle iconCircle = new ButtonStyle();

    public ButtonStyle GetButtonStyle(UIButtonVariant variant) =>
        variant == UIButtonVariant.IconCircle ? iconCircle : pill;
}
