using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;

[TestFixture, Category("Core")]
public sealed class AudioSystemTests
{
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    [Test]
    public void AudioVariation_DoesNotAdvanceGameplayRandom()
    {
        var saved = Random.state;
        var sound = ScriptableObject.CreateInstance<SoundEvent>();
        var first = AudioClip.Create("first", 32, 1, 22050, false);
        var second = AudioClip.Create("second", 32, 1, 22050, false);
        var root = new GameObject("Random isolation source");
        var source = root.AddComponent<AudioSource>();
        try
        {
            sound.variants = new[] { first, second };
            sound.volume = new Vector2(.2f, .8f); sound.pitch = new Vector2(.8f, 1.2f);
            Random.InitState(1843);
            var expected = Enumerable.Range(0, 16).Select(_ => Random.value).ToArray();
            Random.InitState(1843);
            var volumes = new System.Collections.Generic.HashSet<float>();
            var pitches = new System.Collections.Generic.HashSet<float>();
            AudioClip previous = null;
            for (int i = 0; i < 64; i++)
            {
                var clip = sound.SelectClip(previous);
                Assert.That(clip, Is.Not.SameAs(previous)); previous = clip;
                sound.ConfigureSource(source);
                Assert.That(source.volume, Is.InRange(.2f, .8f)); Assert.That(source.pitch, Is.InRange(.8f, 1.2f));
                volumes.Add(source.volume); pitches.Add(source.pitch);
            }
            Assert.That(volumes.Count, Is.GreaterThan(1)); Assert.That(pitches.Count, Is.GreaterThan(1));
            CollectionAssert.AreEqual(expected, Enumerable.Range(0, 16).Select(_ => Random.value).ToArray());
        }
        finally { Random.state = saved; Object.DestroyImmediate(root); Object.DestroyImmediate(sound); Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
    }

    [Test]
    public void Manifest_ContainsRequiredEvents_ExcludesUnusedCandidates_EditorStillListsThem()
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioAssets.LibraryPath);
        Assert.That(library.events.Distinct().Count(), Is.EqualTo(library.events.Count));
        foreach (string path in new[] { AudioAssets.PistolPath, AudioAssets.MeleePath, AudioAssets.BeamStartPath,
            AudioAssets.BeamLoopPath, AudioAssets.BeamEndPath, AudioAssets.UiClickPath, AudioAssets.UiPlayPath })
            Assert.That(library.events, Does.Contain(AssetDatabase.LoadAssetAtPath<SoundEvent>(path)));
        Assert.That(library.events.Any(s => s.status == SoundStatus.Candidate), Is.False);
        var candidates = AssetDatabase.FindAssets("t:SoundEvent").Select(g => AssetDatabase.LoadAssetAtPath<SoundEvent>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(s => s.status == SoundStatus.Candidate).ToArray();
        Assert.That(candidates, Is.Not.Empty);
        var dependencies = AssetDatabase.GetDependencies(AudioAssets.LibraryPath, true);
        foreach (var candidate in candidates)
        {
            Assert.That(dependencies, Does.Not.Contain(AssetDatabase.GetAssetPath(candidate)));
            foreach (var clip in candidate.variants) Assert.That(dependencies, Does.Not.Contain(AssetDatabase.GetAssetPath(clip)));
        }
        var window = ScriptableObject.CreateInstance<AudioLibraryWindow>();
        try
        {
            var visible = (SoundEvent[])typeof(AudioLibraryWindow).GetField("visibleEvents", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
            CollectionAssert.IsSubsetOf(candidates, visible);
            CollectionAssert.IsSubsetOf(library.events, visible);
        }
        finally { Object.DestroyImmediate(window); }
    }

    [UnityTest]
    public IEnumerator EmitterStartsOnce_AndDisabledServiceCanBeReplaced()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        {
        var clip = AudioClip.Create("Loop", 441000, 1, 44100, false);
        var sound = ScriptableObject.CreateInstance<SoundEvent>(); sound.variants = new[] { clip }; sound.cooldown = 60;
        var library = ScriptableObject.CreateInstance<AudioLibrary>(); library.events.Add(sound);
        var local = new GameObject("Auto emitter"); local.SetActive(false);
        var emitter = local.AddComponent<AudioEmitter>(); Set(emitter, "sound", sound); Set(emitter, "playOnEnable", true);
        var listener = new GameObject("Listener").AddComponent<AudioListener>();
        AudioService original = null, replacement = null;
        try
        {
            original = CreateService(library, false);
            Assert.That(AudioService.Instance, Is.Null, "An initially disabled service must not claim the singleton in Awake.");
            original.enabled = true;
            local.SetActive(true);
            yield return null; yield return null;
            var source = local.GetComponent<AudioSource>();
            Assert.That(source.isPlaying, Is.True, "A duplicate initial request would stop the loop, then fail its cooldown.");
            Assert.That(source.clip, Is.SameAs(clip));
            emitter.enabled = false;
            Assert.That(source.clip, Is.Null);
            original.enabled = false;
            Assert.That(AudioService.Instance, Is.Null);
            replacement = CreateService(library);
            Assert.That(AudioService.Instance, Is.SameAs(replacement));
            emitter.enabled = true;
            Assert.That(source.isPlaying, Is.True, "Subsequent enable plays immediately.");
            yield return null; yield return null;
            Assert.That(source.isPlaying, Is.True, "Start must not replay on subsequent enables either.");
            LogAssert.Expect(LogType.Error, "Only one active AudioService is supported. Duplicate disabled.");
            original.enabled = true;
            Assert.That(original.enabled, Is.False);
            Assert.That(AudioService.Instance, Is.SameAs(replacement), "Rejected re-enable cannot steal replacement ownership.");
            replacement.gameObject.SetActive(false);
            Assert.That(AudioService.Instance, Is.Null);
            original.enabled = true;
            Assert.That(AudioService.Instance, Is.SameAs(original));
            Assert.That(original.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(original.PoolSize), "Re-enable reuses the initialized pool.");
            Object.Destroy(replacement.gameObject); replacement = null;
            yield return null;
            Assert.That(AudioService.Instance, Is.SameAs(original), "Destroying an old disabled service cannot clear the active owner.");
        }
        finally
        {
            Object.Destroy(local); Object.Destroy(listener.gameObject);
            if (original != null) Object.Destroy(original.gameObject);
            if (replacement != null) Object.Destroy(replacement.gameObject);
            Object.Destroy(library); Object.Destroy(sound); Object.Destroy(clip);
        }
        }
        yield return new ExitPlayMode();
    }

    private static AudioService CreateService(AudioLibrary library, bool enabled = true)
    {
        var root = new GameObject("Audio lifecycle test"); root.SetActive(false);
        var service = root.AddComponent<AudioService>(); Set(service, "library", library); Set(service, "oneShotCapacity", 2);
        service.enabled = enabled; root.SetActive(true); return service;
    }

    [UnityTearDown]
    public IEnumerator ExitAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
    [Test]
    public void Selection_IgnoresNullAndDuplicatePreviousVariants()
    {
        var sound = ScriptableObject.CreateInstance<SoundEvent>();
        var first = AudioClip.Create("first", 32, 1, 22050, false);
        var second = AudioClip.Create("second", 32, 1, 22050, false);
        try
        {
            sound.variants = new[] { first, null, first, second };
            for (int i = 0; i < 20; i++) Assert.That(sound.SelectClip(first), Is.SameAs(second));
            sound.variants = new[] { first, null, first };
            Assert.That(sound.SelectClip(first), Is.SameAs(first));
            sound.variants = new AudioClip[] { null };
            Assert.That(sound.SelectClip(first), Is.Null);
        }
        finally { Object.DestroyImmediate(sound); Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
    }

    [Test]
    public void NativeAssets_AreIndexedAndRouted()
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioAssets.LibraryPath);
        Assert.That(library, Is.Not.Null);
        foreach (string path in new[] { AudioAssets.PistolPath, AudioAssets.MeleePath, AudioAssets.BeamStartPath, AudioAssets.BeamLoopPath, AudioAssets.BeamEndPath })
        {
            var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(path);
            Assert.That(sound, Is.Not.Null); Assert.That(library.events.Count(s => s == sound), Is.EqualTo(1));
            Assert.That(sound.mixerGroup, Is.Not.Null); Assert.That(sound.mixerGroup.name, Is.EqualTo("SFX"));
        }
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioAssets.MixerPath);
        foreach (string group in new[] { "Master", "Music", "SFX", "UI", "Ambience", "Voice" })
        {
            Assert.That(mixer.FindMatchingGroups(group).Count(g => g.name == group), Is.EqualTo(1));
            Assert.That(mixer.GetFloat(group + "Volume", out _), Is.True);
        }
        foreach (string preset in new[] { "SFX_Short_Mono", "SFX_Loop_3D", "Ambience_Stream", "Music_Stream", "Voice_Mono" })
            Assert.That(AssetDatabase.LoadAssetAtPath<Preset>(AudioAssets.Root + "/Presets/" + preset + ".preset"), Is.Not.Null);
    }

    [Test]
    public void MissingClipValidation_IsExplicitAndNonDestructive()
    {
        var sound = ScriptableObject.CreateInstance<SoundEvent>();
        try
        {
            var issues = AudioLibraryValidation.Issues(sound);
            Assert.That(issues.Any(i => i.Contains("MISSING CLIPS")));
            Assert.That(issues.Any(i => i.Contains("mixer group")));
            Assert.That(sound.ClipCount, Is.Zero);
        }
        finally { Object.DestroyImmediate(sound); }
    }
}
