using System;
using UnityEngine;

public enum GraphicsQuality { Low, Medium, High }

/// <summary>Device preferences only. Never participates in Active Run.</summary>
public static class GraphicsPreferences
{
    public const string QualityKey = "settings.graphics.quality";
    public const string ShadowsKey = "settings.graphics.shadows";
    public static event Action Changed;
    public static GraphicsQuality Quality
    {
        get => (GraphicsQuality)Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, 2), 0, 2);
        set { PlayerPrefs.SetInt(QualityKey, Mathf.Clamp((int)value, 0, 2)); Save(); }
    }
    public static bool Shadows
    {
        get => PlayerPrefs.GetInt(ShadowsKey, 1) != 0;
        set { PlayerPrefs.SetInt(ShadowsKey, value ? 1 : 0); Save(); }
    }
    private static void Save() { PlayerPrefs.Save(); Changed?.Invoke(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Changed = null;
}
