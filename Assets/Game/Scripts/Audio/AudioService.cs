using UnityEngine;

/// <summary>Scene-owned, fixed-capacity audio. Saturation drops the new request.</summary>
[DefaultExecutionOrder(-500), DisallowMultipleComponent]
public sealed class AudioService : MonoBehaviour
{
    [SerializeField] private AudioLibrary library;
    [SerializeField, Min(1)] private int oneShotCapacity = 32;
    [SerializeField, Min(1)] private int emitterCapacity = 32;
    public static AudioService Instance { get; private set; }
    private struct EventState { public SoundEvent sound; public AudioClip previous; public double nextTime; }
    private struct Voice { public AudioSource source; public SoundEvent sound; public bool paused; }
    private EventState[] events;
    private Voice[] voices;
    private bool backgrounded;
    private int poolSize;
    private bool retiring;
    private double retireDeadline;
    public int PoolSize => poolSize;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        int count = library != null ? library.events.Count : 0;
        events = new EventState[count];
        for (int i = 0; i < count; i++) events[i].sound = library.events[i];
        poolSize = Mathf.Max(1, oneShotCapacity);
        voices = new Voice[poolSize + Mathf.Max(1, emitterCapacity)];
        for (int i = 0; i < poolSize; i++)
        {
            var child = new GameObject("One Shot " + i);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            voices[i].source = source;
        }
    }

    private void OnEnable()
    {
        if (retiring) return; // A retained UI tail never reclaims ownership from the next scene.
        if (Instance != null && Instance != this && Instance.isActiveAndEnabled)
        {
            Debug.LogError("Only one active AudioService is supported. Duplicate disabled.", this);
            enabled = false; return;
        }
        Instance = this;
    }

    public static bool Play(SoundEvent sound, Vector3 position) => Instance != null && Instance.PlayOneShot(sound, position, false);
    public static bool Play2D(SoundEvent sound) => Instance != null && Instance.PlayOneShot(sound, Vector3.zero, true);

    private bool PlayOneShot(SoundEvent sound, Vector3 position, bool force2D)
    {
        if (!isActiveAndEnabled || voices == null) return false;
        RefreshVoices();
        for (int i = 0; i < poolSize; i++)
        {
            if (voices[i].sound != null) continue;
            voices[i].source.transform.position = position;
            return StartVoice(i, sound, false, force2D);
        }
        return false;
    }

    internal bool PlayLocal(AudioSource source, SoundEvent sound, bool loop)
    {
        if (!isActiveAndEnabled || source == null || !source.isActiveAndEnabled || voices == null) return false;
        RefreshVoices();
        int slot = -1;
        for (int i = poolSize; i < voices.Length; i++)
        {
            if (voices[i].source == source) { StopVoice(i); slot = i; break; }
            if (slot < 0 && voices[i].source == null) slot = i;
        }
        if (slot < 0) return false;
        voices[slot].source = source;
        bool started = StartVoice(slot, sound, loop, false);
        if (!started) voices[slot] = default;
        return started;
    }

    private bool StartVoice(int slot, SoundEvent sound, bool loop, bool force2D)
    {
        if (sound == null || backgrounded || (Time.timeScale <= 0 && !sound.playDuringPause)) return false;
        int index = -1;
        for (int i = 0; i < events.Length; i++) if (events[i].sound == sound) { index = i; break; }
        if (index < 0) return false; // The library is the explicit build/preload manifest.
        double now = Time.unscaledTimeAsDouble;
        if (now < events[index].nextTime) return false;
        int active = 0;
        for (int i = 0; i < voices.Length; i++) if (voices[i].sound == sound) active++;
        if (active >= Mathf.Max(1, sound.maxVoices)) return false;
        AudioClip clip = sound.SelectClip(events[index].previous);
        // Never request disk loading in response to a shot. Import validation catches this.
        if (clip == null || (clip.loadType != AudioClipLoadType.Streaming && clip.loadState != AudioDataLoadState.Loaded)) return false;
        AudioSource source = voices[slot].source;
        sound.ConfigureSource(source, force2D);
        source.clip = clip;
        source.loop = loop;
        voices[slot].sound = sound;
        voices[slot].paused = false;
        events[index].previous = clip;
        events[index].nextTime = now + Mathf.Max(0, sound.cooldown);
        source.Play();
        return true;
    }

    internal void StopLocal(AudioSource source)
    {
        if (voices == null) return;
        for (int i = poolSize; i < voices.Length; i++)
            if (voices[i].source == source) { StopVoice(i); return; }
    }

    private void StopVoice(int index)
    {
        var source = voices[index].source;
        if (source != null) { source.Stop(); source.clip = null; }
        voices[index].sound = null; voices[index].paused = false;
        if (index >= poolSize) voices[index].source = null;
    }

    // Keep already-playing UI one-shots alive through a synchronous scene switch.
    // The next scene still owns its own service; old gameplay/loops never carry over.
    public void PreserveUiTailForSceneChange()
    {
        if (retiring || voices == null) return;
        retiring = true;
        retireDeadline = Time.realtimeSinceStartupAsDouble + 5;
        if (Instance == this) Instance = null;
        for (int i = 0; i < voices.Length; i++)
            if (i >= poolSize || voices[i].sound == null || !voices[i].sound.playDuringPause) StopVoice(i);
        DontDestroyOnLoad(gameObject);
    }
    private void Update()
    {
        RefreshVoices();
        if (!retiring) return;
        bool playing = false;
        for (int i = 0; i < poolSize; i++) if (voices[i].sound != null) { playing = true; break; }
        if (!playing || Time.realtimeSinceStartupAsDouble >= retireDeadline) Destroy(gameObject);
    }
    private void RefreshVoices()
    {
        if (voices == null) return;
        for (int i = 0; i < voices.Length; i++)
        {
            if (voices[i].sound == null) continue;
            var source = voices[i].source;
            if (source == null || !source.isActiveAndEnabled) { StopVoice(i); continue; }
            bool pause = backgrounded || (!voices[i].sound.playDuringPause && Time.timeScale <= 0);
            if (pause != voices[i].paused)
            {
                if (pause) source.Pause(); else source.UnPause();
                voices[i].paused = pause;
            }
            if (!pause && !source.isPlaying) StopVoice(i);
        }
    }
    private void OnApplicationPause(bool value) { backgrounded = value; RefreshVoices(); }
    private void OnDisable()
    {
        if (Instance == this) Instance = null;
        if (voices != null) for (int i = 0; i < voices.Length; i++) StopVoice(i);
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
