using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
public sealed class AudioEmitter : MonoBehaviour
{
    [SerializeField] private SoundEvent sound;
    [SerializeField] private bool loop = true;
    [SerializeField] private bool playOnEnable;
    private AudioSource source;
    private AudioService owner;
    private bool started;
    private float basePitch = 1, baseVolume = 1;
    public float PlaybackDuration => source != null && source.clip != null ? source.clip.length / Mathf.Max(.01f, Mathf.Abs(source.pitch)) : 0;

    private void Awake() { source = GetComponent<AudioSource>(); source.playOnAwake = false; }
    // Defer the first activation until scene services have initialized. Later enables play directly.
    private void Start() { started = true; if (playOnEnable) Play(); }
    private void OnEnable() { if (started && playOnEnable) Play(); }
    public bool Play() => Play(sound, loop);
    public bool Play(SoundEvent value, bool looping = true)
    {
        if (!isActiveAndEnabled || source == null) return false;
        Stop(); owner = AudioService.Instance;
        bool played = owner != null && owner.PlayLocal(source, value, looping);
        if (played) { basePitch = source.pitch; baseVolume = source.volume; }
        return played;
    }
    // Continuous semantic intensity, relative to the event's randomized starting gain/pitch.
    public void SetResponse(float pitchMultiplier, float volumeMultiplier = 1)
    {
        if (source == null || owner == null) return;
        source.pitch = Mathf.Clamp(basePitch * pitchMultiplier, .01f, 3);
        source.volume = Mathf.Clamp01(baseVolume * volumeMultiplier);
    }
    public void Stop()
    {
        if (owner != null && source != null) owner.StopLocal(source);
        if (source != null) { source.Stop(); source.clip = null; }
        owner = null;
    }
    private void OnDisable() => Stop();
    private void Reset() { GetComponent<AudioSource>().playOnAwake = false; }
}
