using System;
using UnityEngine;

/// <summary>Accepted Editor bake. Runtime consumes these records without choosing locations or variants.</summary>
[CreateAssetMenu(menuName = "Environment/Alien Ground Aurora Schedule")]
public sealed class AlienGroundAuroraSchedule : ScriptableObject
{
    [Serializable]
    public struct Event
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector2 dimensions;
        public float startTime, lifetime, fadeIn, fadeOut;
        public float motionSpeed, phase, intensity;
        // Perimeter softness, irregularity, and two authored shape offsets. No runtime selection.
        public Vector4 footprint;
        public Color tint;
        public int poolSlot;
    }

    [Min(10)] public float duration = 96;
    [Range(1, 14)] public int poolSize = 14;
    public Event[] events = Array.Empty<Event>();
    [TextArea] public string bakeSummary;
}
