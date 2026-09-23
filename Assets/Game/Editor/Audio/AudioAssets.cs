using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>Explicit, idempotent initial authoring. Never rewrites existing sound tuning/imports.</summary>
public static class AudioAssets
{
    public const string Root = "Assets/Game/Audio";
    public const string LibraryPath = Root + "/AudioLibrary.asset";
    public const string MixerPath = Root + "/Mixers/MainAudioMixer.mixer";
    public const string PistolPath = Root + "/Events/Weapons/Weapon_PlasmaPistol_Fire.asset";
    public const string MeleePath = Root + "/Events/Combat/Combat_Melee_Impact.asset";
    public const string BeamStartPath = Root + "/Events/Beam/Beam_Start.asset";
    public const string BeamLoopPath = Root + "/Events/Beam/Beam_Loop.asset";
    public const string BeamEndPath = Root + "/Events/Beam/Beam_End.asset";
    public const string UiClickPath = Root + "/Events/UI/UI_Click.asset";
    public const string UiPlayPath = Root + "/Events/UI/UI_PlayConfirm.asset";

    [MenuItem("Tools/Audio/Create or Repair Audio Assets")]
    public static void Ensure()
    {
        Folder(Root + "/Clips"); Folder(Root + "/Mixers"); Folder(Root + "/Presets");
        foreach (SoundCategory category in Enum.GetValues(typeof(SoundCategory))) Folder(Root + "/Events/" + category);
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (mixer == null) mixer = CreateMixer();
        EnsureMixerGroups(mixer);
        var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
        if (library == null) { library = ScriptableObject.CreateInstance<AudioLibrary>(); AssetDatabase.CreateAsset(library, LibraryPath); }
        var sfx = mixer.FindMatchingGroups("SFX").FirstOrDefault();
        EnsureEvent(library, PistolPath, SoundCategory.Weapons, "Plasma pistol discharge at the actual muzzle.", sfx, 8);
        EnsureEvent(library, MeleePath, SoundCategory.Combat, "Confirmed player melee contact; no air swing sound.", sfx, 4);
        EnsureEvent(library, BeamStartPath, SoundCategory.Beam, "Beam appears / begins materializing.", sfx, 2);
        EnsureEvent(library, BeamLoopPath, SoundCategory.Beam, "Sustained transport energy; loop begins on first travel frame.", sfx, 2);
        EnsureEvent(library, BeamEndPath, SoundCategory.Beam, "Destination reached; stop loop and release energy.", sfx, 2);
        var ui = mixer.FindMatchingGroups("UI").FirstOrDefault();
        EnsureEvent(library, UiClickPath, SoundCategory.UI, "Shared ordinary menu/button interaction.", ui, 8);
        EnsureEvent(library, UiPlayPath, SoundCategory.UI, "Accepted final gameplay launch, Continue or Resume; replaces the ordinary click.", ui, 4);
        CreatePresets();
        AssetDatabase.SaveAssetIfDirty(library);
    }

    public static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void EnsureEvent(AudioLibrary library, string path, SoundCategory category, string description, AudioMixerGroup group, int voices)
    {
        var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(path);
        if (sound == null)
        {
            sound = ScriptableObject.CreateInstance<SoundEvent>();
            sound.displayName = Path.GetFileNameWithoutExtension(path); sound.category = category;
            sound.description = description; sound.mixerGroup = group; sound.maxVoices = voices;
            sound.spatial = category != SoundCategory.UI;
            sound.playDuringPause = category == SoundCategory.UI;
            if (category == SoundCategory.UI) sound.pitch = Vector2.one;
            sound.notes = "MISSING CLIP: intentionally silent V1 placeholder. Assign approved production audio here.";
            AssetDatabase.CreateAsset(sound, path);
        }
        if (!library.events.Contains(sound)) { library.events.Add(sound); EditorUtility.SetDirty(library); }
    }

    private static AudioMixer CreateMixer()
    {
        // Unity has no public mixer creation API. This narrowly isolated Editor-only
        // reflection creates native assets; it is never used by playback or builds.
        Type type = typeof(Editor).Assembly.GetType("UnityEditor.Audio.AudioMixerController");
        var create = type?.GetMethod("CreateMixerControllerAtPath", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (create == null) throw new NotSupportedException("Create MainAudioMixer in Unity's Audio Mixer window; mixer authoring API changed.");
        return (AudioMixer)create.Invoke(null, new object[] { MixerPath });
    }

    private static void EnsureMixerGroups(AudioMixer mixer)
    {
        Type type = mixer.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var master = type.GetProperty("masterGroup", flags).GetValue(mixer);
        var createGroup = type.GetMethod("CreateNewGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (createGroup == null) throw new NotSupportedException("Unity mixer group authoring API changed.");
        var childrenProperty = master.GetType().GetProperty("children", flags);
        foreach (string name in new[] { "Music", "SFX", "UI", "Ambience", "Voice" })
        {
            if (mixer.FindMatchingGroups(name).Any(g => g.name == name)) continue;
            var group = createGroup.Invoke(mixer, new object[] { name, false });
            var children = (Array)childrenProperty.GetValue(master);
            var expanded = Array.CreateInstance(master.GetType(), children.Length + 1);
            Array.Copy(children, expanded, children.Length); expanded.SetValue(group, children.Length);
            childrenProperty.SetValue(master, expanded);
            EditorUtility.SetDirty((UnityEngine.Object)master); EditorUtility.SetDirty(mixer);
        }
        // Populate a native mixer view and stable settings parameters, without
        // replacing user-authored views or renaming existing exposed parameters.
        var groups = mixer.FindMatchingGroups("");
        var exposedProperty = type.GetProperty("exposedParameters", flags);
        var exposed = (Array)exposedProperty.GetValue(mixer);
        Type parameterType = exposed.GetType().GetElementType();
        foreach (var group in groups)
        {
            object volumeId = group.GetType().GetMethod("GetGUIDForVolume", flags).Invoke(group, null);
            if (exposed.Cast<object>().Any(p => parameterType.GetField("guid").GetValue(p).Equals(volumeId))) continue;
            object parameter = Activator.CreateInstance(parameterType);
            parameterType.GetField("guid").SetValue(parameter, volumeId);
            parameterType.GetField("name").SetValue(parameter, group.name + "Volume");
            var expanded = Array.CreateInstance(parameterType, exposed.Length + 1);
            Array.Copy(exposed, expanded, exposed.Length); expanded.SetValue(parameter, exposed.Length);
            exposed = expanded; exposedProperty.SetValue(mixer, exposed); EditorUtility.SetDirty(mixer);
        }
        var viewsProperty = type.GetProperty("views", flags);
        var views = (Array)viewsProperty.GetValue(mixer);
        if (views.Length == 0)
        {
            Type viewType = views.GetType().GetElementType();
            object view = Activator.CreateInstance(viewType);
            viewType.GetField("name").SetValue(view, "All Groups");
            var guids = Array.CreateInstance(viewType.GetField("guids").FieldType.GetElementType(), groups.Length);
            for (int i = 0; i < groups.Length; i++) guids.SetValue(groups[i].GetType().GetProperty("groupID", flags).GetValue(groups[i]), i);
            viewType.GetField("guids").SetValue(view, guids);
            views = Array.CreateInstance(viewType, 1); views.SetValue(view, 0); viewsProperty.SetValue(mixer, views);
            EditorUtility.SetDirty(mixer);
        }
        AssetDatabase.SaveAssetIfDirty(mixer);
    }

    private static void CreatePresets()
    {
        string[] names = { "SFX_Short_Mono", "SFX_Loop_3D", "Ambience_Stream", "Music_Stream", "Voice_Mono" };
        if (names.All(n => AssetDatabase.LoadAssetAtPath<Preset>(Root + "/Presets/" + n + ".preset") != null)) return;
        // A temporary silent importer provides Unity's native Preset serialization.
        // No existing clip is ever reimported by this setup command.
        string temp = AssetDatabase.GenerateUniqueAssetPath(Root + "/Clips/AudioPresetSeed.wav");
        try
        {
            using (var writer = new BinaryWriter(File.Create(temp)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + 882);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(22050); writer.Write(44100);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(882); writer.Write(new byte[882]);
            }
            AssetDatabase.ImportAsset(temp, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(temp);
            for (int i = 0; i < names.Length; i++)
            {
                string path = Root + "/Presets/" + names[i] + ".preset";
                if (AssetDatabase.LoadAssetAtPath<Preset>(path) != null) continue;
                bool streaming = i == 2 || i == 3;
                importer.forceToMono = !streaming;
                importer.loadInBackground = streaming;
                var settings = importer.defaultSampleSettings;
                settings.loadType = streaming ? AudioClipLoadType.Streaming : i == 0 ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = .7f;
                settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
                settings.preloadAudioData = !streaming;
                importer.defaultSampleSettings = settings;
                importer.SetOverrideSampleSettings("Android", settings);
                importer.SetOverrideSampleSettings("iOS", settings);
                AssetDatabase.CreateAsset(new Preset(importer), path);
            }
        }
        finally { AssetDatabase.DeleteAsset(temp); }
    }
}
