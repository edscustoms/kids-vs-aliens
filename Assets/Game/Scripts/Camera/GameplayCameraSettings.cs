using System;
using UnityEngine;

public enum GameplayCameraMode
{
    Action,
    Tactical,
    Isometric,
}

/// <summary>Only the player preference is persisted; camera tuning belongs to camera data.</summary>
public static class GameplayCameraSettings
{
    public const string PreferenceKey = "settings.gameplayCameraMode";
    public static event Action<GameplayCameraMode> Changed;
    public static GameplayCameraMode Mode
    {
        get
        {
            int value = PlayerPrefs.GetInt(PreferenceKey, 0);
            return IsValid(value) ? (GameplayCameraMode)value : GameplayCameraMode.Action;
        }
        set
        {
            if (!IsValid((int)value))
                value = GameplayCameraMode.Action;
            PlayerPrefs.SetInt(PreferenceKey, (int)value);
            PlayerPrefs.Save();
            Changed?.Invoke(value);
        }
    }

    private static bool IsValid(int value) =>
        value >= 0 && value <= (int)GameplayCameraMode.Isometric;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvents() => Changed = null;
}
