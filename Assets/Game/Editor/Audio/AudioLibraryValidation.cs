using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AudioLibraryValidation
{
    [MenuItem("Tools/Audio/Validate Library")]
    public static void Validate()
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioAssets.LibraryPath);
        if (library == null) { Debug.LogWarning("AudioLibrary is missing. Use Tools > Audio > Create or Repair Audio Assets."); return; }
        int warnings = 0;
        var seen = new HashSet<SoundEvent>(); var names = new HashSet<string>();
        foreach (var sound in library.events)
        {
            if (sound == null) { Warn("Null SoundEvent entry in AudioLibrary.", library, ref warnings); continue; }
            if (!seen.Add(sound)) Warn("Duplicate library entry: " + sound.name, sound, ref warnings);
            if (!names.Add(sound.DisplayName)) Warn("Duplicate event display name: " + sound.DisplayName, sound, ref warnings);
            foreach (string issue in Issues(sound)) Warn(sound.name + ": " + issue, sound, ref warnings);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:SoundEvent"))
        {
            var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(AssetDatabase.GUIDToAssetPath(guid));
            // Unused assets intentionally stay Editor-only; still validate their authoring.
            if (sound != null && !seen.Contains(sound))
                foreach (string issue in Issues(sound)) Warn(sound.name + ": " + issue, sound, ref warnings);
        }
        Debug.Log("Audio Library validation complete: " + warnings + " warning(s). Missing V1 clips are intentional until assigned.", library);
    }

    public static List<string> Issues(SoundEvent sound)
    {
        var issues = new List<string>();
        if (sound.ClipCount == 0) issues.Add("MISSING CLIPS (silent).");
        if (sound.mixerGroup == null) issues.Add("Missing mixer group.");
        if (sound.maxVoices < 1 || sound.cooldown < 0 || sound.volume.x < 0 || sound.volume.y > 1 || sound.volume.x > sound.volume.y
            || sound.pitch.x <= 0 || sound.pitch.y > 3 || sound.pitch.x > sound.pitch.y || sound.minDistance <= 0 || sound.maxDistance < sound.minDistance)
            issues.Add("Invalid volume/pitch/distance/voice-limit configuration.");
        if (sound.status == SoundStatus.Missing && sound.ClipCount > 0) issues.Add("Clips assigned but status is still Missing.");
        if (sound.variants == null) return issues;
        var seen = new HashSet<AudioClip>();
        foreach (var clip in sound.variants)
        {
            if (clip == null) { issues.Add("Empty variant slot."); continue; }
            if (!seen.Add(clip)) issues.Add("Duplicate variant: " + clip.name);
            string path = AssetDatabase.GetAssetPath(clip);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;
            bool gameplay = sound.category != SoundCategory.Music && sound.category != SoundCategory.Ambience;
            if (sound.spatial && clip.channels > 1) issues.Add(clip.name + ": stereo 3D sound; consider mono.");
            if (gameplay && clip.length <= 2 && clip.frequency > 48000) issues.Add(clip.name + ": tiny SFX above 48 kHz.");
            if (gameplay && File.Exists(path) && new FileInfo(path).Length > 5 * 1024 * 1024) issues.Add(clip.name + ": source file exceeds 5 MB; review duration/import memory.");
            CheckSettings(importer.defaultSampleSettings, "Default");
            foreach (string platform in new[] { "Android", "iOS" })
                if (importer.ContainsSampleSettingsOverride(platform)) CheckSettings(importer.GetOverrideSampleSettings(platform), platform);
            void CheckSettings(AudioImporterSampleSettings settings, string platform)
            {
                if (gameplay && (settings.loadType == AudioClipLoadType.Streaming || !settings.preloadAudioData || importer.loadInBackground))
                    issues.Add(clip.name + " (" + platform + "): gameplay audio must preload synchronously and must not stream.");
                if (gameplay && clip.length > 5 && (settings.compressionFormat == AudioCompressionFormat.PCM || settings.loadType == AudioClipLoadType.DecompressOnLoad))
                    issues.Add(clip.name + " (" + platform + "): long/uncompressed gameplay audio; review memory cost.");
            }
        }
        return issues;
    }
    private static void Warn(string message, Object context, ref int count) { count++; Debug.LogWarning(message, context); }
}
