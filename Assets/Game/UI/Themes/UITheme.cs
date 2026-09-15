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

    public ButtonStyle pill = new ButtonStyle();
    public ButtonStyle iconCircle = new ButtonStyle();

    public ButtonStyle GetButtonStyle(UIButtonVariant variant) =>
        variant == UIButtonVariant.IconCircle ? iconCircle : pill;
}
