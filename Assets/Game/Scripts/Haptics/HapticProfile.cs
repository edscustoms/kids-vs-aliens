using UnityEngine;

public enum HapticImpactStyle { Light, Medium, Heavy }

[CreateAssetMenu(menuName = "Game/Haptics/Profile")]
public sealed class HapticProfile : ScriptableObject
{
    [Header("Android")]
    [Range(1, 100)] public int durationMilliseconds = 18;
    [Range(1, 255)] public int amplitude = 90;
    [Header("iOS")]
    public HapticImpactStyle impactStyle = HapticImpactStyle.Light;
    [Range(0f, 1f)] public float intensity = .6f;
}
