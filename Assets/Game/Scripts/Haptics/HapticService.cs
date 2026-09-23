using UnityEngine;

/// <summary>Main-thread, local-device feedback. Call only after a gameplay action succeeds.</summary>
public static class HapticService
{
    private static IHapticBackend backend;
    private static HapticProfile damageProfile;

    public static void Play(HapticProfile profile)
    {
        if (profile == null || !HapticSettings.Enabled) return;
        if (backend == null) backend = CreateBackend();
        backend.Play(profile);
    }

    public static void PlayerDamaged()
    {
        if (!HapticSettings.Enabled) return;
        if (damageProfile == null) damageProfile = Resources.Load<HapticProfile>("Haptics/PlayerDamage");
        Play(damageProfile);
    }

    private static IHapticBackend CreateBackend()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return new AndroidHapticBackend();
#elif UNITY_IOS && !UNITY_EDITOR
        return new IosHapticBackend();
#else
        return new SilentHapticBackend();
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        (backend as System.IDisposable)?.Dispose();
        backend = null;
        damageProfile = null;
    }
}

public interface IHapticBackend
{
    void Play(HapticProfile profile);
}

internal sealed class SilentHapticBackend : IHapticBackend
{
    public void Play(HapticProfile profile) { }
}
