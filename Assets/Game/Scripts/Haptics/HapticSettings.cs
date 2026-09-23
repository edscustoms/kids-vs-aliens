using System;
using UnityEngine;

public static class HapticSettings
{
    public const string PreferenceKey = "settings.hapticsEnabled";
    public static event Action<bool> Changed;
    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PreferenceKey, 1) != 0;
        set
        {
            PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke(value);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvents() => Changed = null;
}
