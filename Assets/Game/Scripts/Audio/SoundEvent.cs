using UnityEngine;
using UnityEngine.Audio;

public enum SoundStatus { Missing, Placeholder, Candidate, Approved, Final }
public enum SoundCategory { UI, Player, Weapons, Combat, Beam, Aliens, Machinery, Environment, Ambience, Music, Voice }

/// <summary>Semantic sound definition. Only the audio layer reads clips.</summary>
[CreateAssetMenu(menuName = "Audio/Sound Event")]
public sealed class SoundEvent : ScriptableObject
{
    // Audio variation must never advance the gameplay random sequence (including Editor previews).
    private static readonly System.Random audioRandom = new System.Random();
    public string displayName;
    public SoundCategory category;
    [TextArea] public string description;
    public SoundStatus status;
    public AudioClip[] variants = System.Array.Empty<AudioClip>();
    public Vector2 volume = new Vector2(.8f, 1f);
    public Vector2 pitch = new Vector2(.95f, 1.05f);
    public bool spatial = true;
    [Min(.01f)] public float minDistance = 2f;
    [Min(.01f)] public float maxDistance = 35f;
    public AudioMixerGroup mixerGroup;
    [Range(0, 256)] public int priority = 128;
    [Min(1)] public int maxVoices = 4;
    [Min(0)] public float cooldown;
    [Tooltip("For UI sounds that should continue while gameplay is paused.")]
    public bool playDuringPause;
    [TextArea] public string notes;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public int ClipCount
    {
        get { int count = 0; if (variants != null) foreach (var clip in variants) if (clip != null) count++; return count; }
    }

    // Skip nulls and the previous clip (including duplicate array entries).
    public AudioClip SelectClip(AudioClip previous)
    {
        if (variants == null) return null;
        int count = 0;
        foreach (var clip in variants) if (clip != null && clip != previous) count++;
        bool excludePrevious = count > 0;
        if (!excludePrevious) count = ClipCount;
        if (count == 0) return null;
        int chosen = audioRandom.Next(count);
        foreach (var clip in variants)
            if (clip != null && (!excludePrevious || clip != previous) && chosen-- == 0) return clip;
        return null;
    }

    public void ConfigureSource(AudioSource source, bool force2D = false)
    {
        source.playOnAwake = false;
        source.outputAudioMixerGroup = mixerGroup;
        source.volume = Mathf.Lerp(Mathf.Clamp01(volume.x), Mathf.Clamp(volume.y, Mathf.Clamp01(volume.x), 1), (float)audioRandom.NextDouble());
        source.pitch = Mathf.Lerp(Mathf.Clamp(pitch.x, .01f, 3), Mathf.Clamp(pitch.y, Mathf.Clamp(pitch.x, .01f, 3), 3), (float)audioRandom.NextDouble());
        source.spatialBlend = spatial && !force2D ? 1 : 0;
        source.minDistance = Mathf.Max(.01f, minDistance);
        source.maxDistance = Mathf.Max(source.minDistance, maxDistance);
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.dopplerLevel = 0;
        source.priority = Mathf.Clamp(priority, 0, 256);
        source.ignoreListenerPause = playDuringPause;
    }

    private void OnValidate()
    {
        volume.x = Mathf.Clamp01(volume.x); volume.y = Mathf.Clamp(volume.y, volume.x, 1);
        pitch.x = Mathf.Clamp(pitch.x, .01f, 3); pitch.y = Mathf.Clamp(pitch.y, pitch.x, 3);
        minDistance = Mathf.Max(.01f, minDistance); maxDistance = Mathf.Max(minDistance, maxDistance);
        maxVoices = Mathf.Max(1, maxVoices); cooldown = Mathf.Max(0, cooldown);
    }
}
