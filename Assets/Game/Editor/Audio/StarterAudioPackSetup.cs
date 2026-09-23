using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Explicit one-pack import; moves with AssetDatabase and preserves clip GUIDs.</summary>
public static class StarterAudioPackSetup
{
    private const string Clips = AudioAssets.Root + "/Clips/";
    private readonly struct Entry
    {
        public readonly string file, folder, eventName, notes;
        public readonly SoundCategory category;
        public readonly bool active, inMemory;
        public Entry(string file, string folder, string eventName, SoundCategory category, bool active, string notes, bool inMemory = false)
        { this.file=file; this.folder=folder; this.eventName=eventName; this.category=category; this.active=active; this.notes=notes; this.inMemory=inMemory; }
    }
    private static readonly Entry[] Pack =
    {
        new("cool-interface-click-tone-2568.wav", "UI", "UI_Click", SoundCategory.UI, true, "LOCKED: all ordinary menu/button clicks use this subtle interface tone."),
        new("click-melodic-tone-1129.wav", "UI", "UI_PlayConfirm", SoundCategory.UI, true, "LOCKED: only accepted final gameplay launch/Continue/Resume. Never layer UI_Click on the same action."),
        new("short-laser-gun-shot-1670.wav", "Weapons", "Weapon_PlasmaPistol_Fire", SoundCategory.Weapons, true, "Temporary laser discharge; replace with final plasma pistol sound."),
        new("game-whip-shot-1512.wav", "Combat", "Combat_Melee_Impact", SoundCategory.Combat, true, "Temporary stylized whip snap for confirmed contact; needs a proper body impact replacement. Not a swing trigger."),
        new("mixkit-alien-technology-button-3118.wav", "Beam", "Beam_Start", SoundCategory.Beam, true, "Temporary short alien activation cue for materialization; not final transport design."),
        new("mixkit-sci-fi-tube-swoosh-912.wav", "Beam", "Beam_End", SoundCategory.Beam, true, "Temporary energy-release swoosh at arrival/landing."),
        new("mixkit-electricity-reactor-buzz-904.wav", "Machinery", "Machinery_Reactor_Buzz", SoundCategory.Machinery, false, "UNUSED candidate: sustained reactor texture. Not certified seamless; do not assign as Beam_Loop without audition/edit.", true),
        new("mixkit-flying-space-bike-1612.wav", "Aliens", "Alien_Vehicle_Flyby", SoundCategory.Aliens, false, "UNUSED candidate: future vehicle flyby, not a stationary loop."),
        new("mixkit-futuristic-door-opening-906.wav", "Environment", "Environment_TechDoor_Open", SoundCategory.Environment, false, "UNUSED candidate: future powered door opening."),
        new("mixkit-interface-hint-notification-911.wav", "UI", "UI_HintNotification", SoundCategory.UI, false, "UNUSED candidate: future hint notification; never an ordinary button click."),
        new("mixkit-sci-fi-click-900.wav", "Machinery", "Machinery_TechSwitch", SoundCategory.Machinery, false, "UNUSED candidate: in-world technology switch, not menu clicks."),
        new("mixkit-sci-fi-confirmation-914.wav", "UI", "UI_Notification_Success", SoundCategory.UI, false, "UNUSED candidate: future success notification, not gameplay launch or normal clicks."),
        new("mixkit-sci-fi-drill-alert-760.wav", "Machinery", "Machinery_Drill_Alert", SoundCategory.Machinery, false, "UNUSED candidate: future drill/machinery alert."),
        new("mixkit-sci-fi-interface-robot-click-901.wav", "Machinery", "Machinery_Robot_Interaction", SoundCategory.Machinery, false, "UNUSED candidate: robotic world interaction, not menu clicks."),
        new("mixkit-sci-fi-interface-zoom-890.wav", "UI", "UI_Scanner_Zoom", SoundCategory.UI, false, "UNUSED candidate: future scanner/zoom presentation; never standard navigation."),
        new("mixkit-sci-fi-plasma-gun-power-1680.wav", "Weapons", "Weapon_Plasma_Charge", SoundCategory.Weapons, false, "UNUSED candidate: long charging/power-up effect, not a repeated pistol shot.", true),
        new("mixkit-synth-mechanical-notification-or-alert-650.wav", "Machinery", "Machinery_Mechanical_Alert", SoundCategory.Machinery, false, "UNUSED candidate: future mechanical alert."),
        new("mixkit-tech-transitions-3176.wav", "UI", "UI_Tech_Transition", SoundCategory.UI, false, "UNUSED candidate: future special transition; not normal menu navigation or launch.")
    };

    [MenuItem("Tools/Audio/Organize and Assign Starter Pack")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before importing the pack.");
        AudioAssets.Ensure();
        var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioAssets.LibraryPath);
        var mixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(AudioAssets.MixerPath);
        // Preflight the whole pack before moving any file.
        foreach (var entry in Pack)
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(Clips + entry.file) == null && AssetDatabase.LoadAssetAtPath<AudioClip>(Clips + entry.folder + "/" + entry.file) == null)
                throw new InvalidOperationException("Missing starter clip: " + entry.file);
        foreach (var entry in Pack)
        {
            AudioAssets.Folder(Clips + entry.folder);
            string source = Clips + entry.file, destination = Clips + entry.folder + "/" + entry.file;
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(source) != null)
            {
                string before = AssetDatabase.AssetPathToGUID(source);
                string error = AssetDatabase.MoveAsset(source, destination);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                if (before != AssetDatabase.AssetPathToGUID(destination)) throw new InvalidOperationException("Clip GUID changed: " + entry.file);
            }
            var importer = (AudioImporter)AssetImporter.GetAtPath(destination);
            string presetName = entry.inMemory ? "SFX_Loop_3D" : "SFX_Short_Mono";
            var preset = AssetDatabase.LoadAssetAtPath<Preset>(AudioAssets.Root + "/Presets/" + presetName + ".preset");
            if (!preset.ApplyTo(importer)) throw new InvalidOperationException("Cannot apply audio preset: " + entry.file);
            importer.SaveAndReimport();
            string eventPath = AudioAssets.Root + "/Events/" + entry.category + "/" + entry.eventName + ".asset";
            var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(eventPath);
            if (sound == null)
            {
                sound = ScriptableObject.CreateInstance<SoundEvent>();
                sound.displayName = entry.eventName; sound.category = entry.category;
                sound.description = entry.notes;
                AssetDatabase.CreateAsset(sound, eventPath);
            }
            // Do not replace later human choices when the import command is rerun.
            if (sound.ClipCount == 0)
            {
                sound.variants = new[] { AssetDatabase.LoadAssetAtPath<AudioClip>(destination) };
                sound.status = entry.active ? SoundStatus.Placeholder : SoundStatus.Candidate;
                sound.notes = "STARTER PACK / TEMPORARY TEST AUDIO. " + entry.notes;
                bool ui = entry.category == SoundCategory.UI;
                sound.spatial = !ui; sound.playDuringPause = ui;
                sound.mixerGroup = mixer.FindMatchingGroups(ui ? "UI" : "SFX").Single(g => g.name == (ui ? "UI" : "SFX"));
                sound.volume = ui ? new Vector2(.55f,.55f) : new Vector2(.5f,.6f);
                sound.pitch = ui ? Vector2.one : new Vector2(.97f,1.03f);
                sound.cooldown = 0;
                if (entry.eventName == "Weapon_PlasmaPistol_Fire") sound.maxVoices = 12;
                if (entry.eventName == "UI_PlayConfirm") { sound.volume = new Vector2(.7f,.7f); sound.priority = 32; }
                EditorUtility.SetDirty(sound); AssetDatabase.SaveAssetIfDirty(sound);
            }
            if (entry.active && !library.events.Contains(sound)) { library.events.Add(sound); EditorUtility.SetDirty(library); }
        }
        AssetDatabase.SaveAssetIfDirty(library);
        AudioLibraryValidation.Validate();
        Debug.Log("Starter pack: 18 clips organized with GUIDs preserved; 6 live placeholders, 12 unused candidates. Beam_Loop intentionally Missing.");
    }

    // Explicit batch entry: only audio wiring, not unrelated gameplay/menu repair.
    public static void ApplyAndWireScenes()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use in an isolated batch session; scene changes are saved.");
        Apply();
        foreach (string name in new[] { "Menu", "GamePoc", "ConstructionSite" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/" + name + ".unity");
            AudioSceneSetup.ConfigureUiAudio(scene, name == "Menu");
            EditorSceneManager.SaveScene(scene);
        }
    }
}
