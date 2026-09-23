using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
internal sealed class AndroidHapticBackend : IHapticBackend, System.IDisposable
{
    private AndroidJavaObject vibrator;
    private AndroidJavaClass effects;
    private bool available, useEffects, amplitudeControl;

    public AndroidHapticBackend()
    {
        try
        {
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            int sdk = version.GetStatic<int>("SDK_INT");
            using var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unity.GetStatic<AndroidJavaObject>("currentActivity");
            using var context = activity.Call<AndroidJavaObject>("getApplicationContext");
            if (sdk >= 31)
            {
                using var manager = context.Call<AndroidJavaObject>("getSystemService", "vibrator_manager");
                vibrator = manager?.Call<AndroidJavaObject>("getDefaultVibrator");
            }
            if (vibrator == null) vibrator = context.Call<AndroidJavaObject>("getSystemService", "vibrator");
            available = vibrator != null && vibrator.Call<bool>("hasVibrator");
            if (available && sdk >= 26)
            {
                effects = new AndroidJavaClass("android.os.VibrationEffect");
                useEffects = true;
                amplitudeControl = vibrator.Call<bool>("hasAmplitudeControl");
            }
        }
        catch (AndroidJavaException) { available = false; }
    }

    public void Play(HapticProfile profile)
    {
        if (!available) return;
        long duration = Mathf.Clamp(profile.durationMilliseconds, 1, 100);
        if (useEffects)
        {
            try
            {
                int amplitude = amplitudeControl ? Mathf.Clamp(profile.amplitude, 1, 255) : -1;
                using var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", duration, amplitude);
                vibrator.Call("vibrate", effect);
                return;
            }
            catch (AndroidJavaException) { useEffects = false; }
        }
        // Legacy devices retain the short duration; never fall back to a long generic buzz.
        try { vibrator.Call("vibrate", duration); }
        catch (AndroidJavaException) { available = false; }
    }

    public void Dispose()
    {
        vibrator?.Dispose();
        effects?.Dispose();
    }
}
#endif

#if UNITY_IOS && !UNITY_EDITOR
internal sealed class IosHapticBackend : IHapticBackend
{
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void KVA_PlayImpact(int style, float intensity);
    private bool available = true;

    public void Play(HapticProfile profile)
    {
        if (!available) return;
        try { KVA_PlayImpact(Mathf.Clamp((int)profile.impactStyle, 0, 2), Mathf.Clamp01(profile.intensity)); }
        catch (System.DllNotFoundException) { available = false; }
        catch (System.EntryPointNotFoundException) { available = false; }
    }
}
#endif
