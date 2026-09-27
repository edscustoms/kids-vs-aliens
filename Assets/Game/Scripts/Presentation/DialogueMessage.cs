using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Gameplay/Dialogue Message")]
public sealed class DialogueMessage : ScriptableObject
{
    [Serializable] public sealed class Line
    {
        [TextArea(2, 5)] public string text;
        [Tooltip("Optional Voice-category SoundEvent registered in AudioLibrary. None is valid.")]
        public SoundEvent voice;
        [Tooltip("0 uses the playing voice duration + padding, or reading time for silent text.")]
        [Min(0)] public float displayDuration;
        [Min(0)] public float delayAfter;
    }
    [Serializable] public sealed class Variant { public Line[] lines = Array.Empty<Line>(); }
    [Serializable] public sealed class SpecialOverride
    {
        public string specialCharacterId;
        public Variant content = new();
    }
    public Variant girl = new();
    public Variant boy = new();
    public SpecialOverride[] specialOverrides = Array.Empty<SpecialOverride>();

    public Variant Resolve(DialogueSpeaker speaker)
    {
        if (speaker != null && !string.IsNullOrWhiteSpace(speaker.specialCharacterId))
            foreach (var entry in specialOverrides)
                if (entry != null && string.Equals(entry.specialCharacterId, speaker.specialCharacterId, StringComparison.Ordinal))
                    return entry.content;
        return speaker != null && speaker.voiceType == DialogueVoiceType.Boy ? boy : girl;
    }
}
