using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Isolated batch smoke review: no production scenes or clips are changed.
[InitializeOnLoad]
public static class AudioRuntimeReview
{
    private const string Key = "AudioRuntimeReview";
    static AudioRuntimeReview()
    {
        EditorApplication.playModeStateChanged += Mode;
        EditorApplication.update += Tick;
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this review in an isolated batch Editor.");
        AudioAssets.Ensure();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false) || state != PlayModeStateChange.EnteredPlayMode) return;
        SessionState.SetBool(Key + "Ready", true);
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key + "Ready", false) || !EditorApplication.isPlaying) return;
        SessionState.SetBool(Key + "Ready", false); Review();
    }
    private static void Check(bool valid, string message)
    {
        if (!valid) throw new Exception("AUDIO FAIL: " + message);
        Debug.Log("AUDIO PASS: " + message);
    }
    private static void Set(UnityEngine.Object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    private static void Review()
    {
        int code = 0;
        try
        {
            new GameObject("Listener").AddComponent<AudioListener>();
            var a = AudioClip.Create("A", 441000, 1, 44100, false);
            var b = AudioClip.Create("B", 441000, 1, 44100, false);
            var sound = ScriptableObject.CreateInstance<SoundEvent>(); sound.variants = new[] { a, b }; sound.maxVoices = 2;
            sound.mixerGroup = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(AudioAssets.MixerPath).FindMatchingGroups("SFX").Single();
            var library = ScriptableObject.CreateInstance<AudioLibrary>(); library.events.Add(sound);
            var go = new GameObject("Audio Service"); go.SetActive(false);
            var service = go.AddComponent<AudioService>(); Set(service, "library", library); Set(service, "oneShotCapacity", 2); go.SetActive(true);
            var sources = go.GetComponentsInChildren<AudioSource>();
            Check(sources.Length == 2 && service.PoolSize == 2, "Sources prewarmed in Awake");
            Check(AudioService.Play(sound, Vector3.one) && AudioService.Play(sound, Vector3.right), "Immediate one-shot playback");
            Check(!AudioService.Play(sound, Vector3.zero), "Voice limit and pool saturation drop new request");
            Check(sources[0].clip != sources[1].clip, "Variants avoid immediate repeat");
            Check(sources[0].outputAudioMixerGroup == sound.mixerGroup && sources[0].spatialBlend == 1, "Mixer and 3D routing");
            sources[0].Stop(); sources[1].Stop();
            sound.variants = new[] { b };
            Check(AudioService.Play2D(sound) && sources[0].clip == b && sources[0].spatialBlend == 0, "Replacement clip and 2D playback reuse existing source");
            sources[0].Stop();
            var local = new GameObject("Emitter"); var emitter = local.AddComponent<AudioEmitter>();
            Check(emitter.Play(sound) && local.GetComponent<AudioSource>().isPlaying && local.GetComponent<AudioSource>().loop, "Local emitter starts immediately");
            sound.maxVoices = 1;
            Check(!AudioService.Play(sound, Vector3.zero), "Emitter and one-shot share event voice cap");
            Time.timeScale = 0; Invoke(service, "Update");
            Check(!local.GetComponent<AudioSource>().isPlaying && !AudioService.Play(sound, Vector3.zero), "World pause pauses loop and rejects gameplay requests");
            Time.timeScale = 1; Invoke(service, "Update");
            Check(local.GetComponent<AudioSource>().isPlaying, "Loop resumes after world pause");
            local.SetActive(false);
            Check(!local.GetComponent<AudioSource>().isPlaying && AudioService.Play(sound, Vector3.zero), "Disable stops emitter and frees voice");
            sources[0].Stop(); sound.cooldown = 10;
            Check(AudioService.Play(sound, Vector3.zero), "Cooldown accepts initial request"); sources[0].Stop();
            Check(!AudioService.Play(sound, Vector3.zero), "Cooldown rejects repeated request");
            sound.cooldown = 0;
            // Warm new state before measuring only the one-shot hot path.
            service.enabled = false; UnityEngine.Object.DestroyImmediate(go);
            go = new GameObject("Measured Service"); go.SetActive(false); service = go.AddComponent<AudioService>();
            Set(service, "library", library); Set(service, "oneShotCapacity", 2); go.SetActive(true);
            sources = go.GetComponentsInChildren<AudioSource>();
            for (int i = 0; i < 16; i++) { AudioService.Play(sound, Vector3.zero); sources[0].Stop(); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) { AudioService.Play(sound, Vector3.zero); sources[0].Stop(); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocated == 0, "100 warmed one-shots allocate zero managed bytes (" + allocated + ")");
            Check(go.GetComponentsInChildren<AudioSource>().SequenceEqual(sources), "Source identities unchanged after repeated playback");
            service.enabled = false; Check(!AudioService.Play(sound, Vector3.zero), "Disabled service stops/rejects playback");
            Debug.Log("AUDIO REVIEW COMPLETE");
        }
        catch (Exception e) { code = 1; Debug.LogException(e); }
        finally { Time.timeScale = 1; SessionState.SetBool(Key, false); EditorApplication.Exit(code); }
    }
}
