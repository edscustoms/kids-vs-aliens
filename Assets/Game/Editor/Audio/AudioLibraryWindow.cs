using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public sealed class AudioLibraryWindow : EditorWindow
{
    private AudioLibrary library;
    private SoundEvent selected;
    private Editor inspector;
    private Vector2 scroll, editScroll;
    private string search = "", newName = "New_Sound";
    private SoundCategory category;
    private AudioClip previous;
    private SoundEvent[] visibleEvents = Array.Empty<SoundEvent>();
    private static readonly Type AudioUtil = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");
    private static readonly MethodInfo PlayPreview = AudioUtil?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
    private static readonly MethodInfo StopPreview = AudioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

    [MenuItem("Tools/Audio/Audio Library")]
    public static void Open() => GetWindow<AudioLibraryWindow>("Audio Library");

    private void OnEnable() { EditorApplication.playModeStateChanged += ModeChanged; OnProjectChange(); }
    private void OnProjectChange()
    {
        visibleEvents = AssetDatabase.FindAssets("t:SoundEvent").Select(g => AssetDatabase.LoadAssetAtPath<SoundEvent>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(s => s != null).OrderBy(s => s.category).ThenBy(s => s.DisplayName).ToArray();
        Repaint();
    }
    private void OnDisable() { EditorApplication.playModeStateChanged -= ModeChanged; ClosePreview(); if (inspector != null) DestroyImmediate(inspector); }
    private void ModeChanged(PlayModeStateChange state) => ClosePreview();
    private void OnGUI()
    {
        if (library == null) library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioAssets.LibraryPath);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Create / Repair Assets", EditorStyles.toolbarButton)) { AudioAssets.Ensure(); library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioAssets.LibraryPath); }
            if (GUILayout.Button("Validate", EditorStyles.toolbarButton)) AudioLibraryValidation.Validate();
            if (GUILayout.Button("Stop Preview", EditorStyles.toolbarButton)) ClosePreview();
            search = GUILayout.TextField(search, EditorStyles.toolbarSearchField);
        }
        if (library == null) { EditorGUILayout.HelpBox("Create audio assets to start the library.", MessageType.Info); return; }
        using (new EditorGUILayout.HorizontalScope())
        {
            category = (SoundCategory)EditorGUILayout.EnumPopup(category, GUILayout.Width(120));
            newName = EditorGUILayout.TextField(newName);
            if (GUILayout.Button("Create Event", GUILayout.Width(110))) CreateEvent();
            if (GUILayout.Button("Runtime Manifest", GUILayout.Width(115))) Selection.activeObject = library;
        }
        EditorGUILayout.LabelField("Category / Event / Status / Clips / Description", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(140), GUILayout.MaxHeight(position.height * .48f));
        // The Editor catalog includes candidates; the runtime manifest contains only wired events.
        foreach (var sound in visibleEvents)
        {
            if (sound == null) continue;
            if (!string.IsNullOrEmpty(search) && (sound.DisplayName + sound.category + sound.description + sound.status).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(sound.category.ToString(), GUILayout.Width(85));
                    GUILayout.Label(sound.DisplayName, GUILayout.MinWidth(165));
                    GUILayout.Label(sound.status.ToString(), GUILayout.Width(85));
                    GUILayout.Label(sound.ClipCount + " clips", GUILayout.Width(48));
                    using (new EditorGUI.DisabledScope(sound.ClipCount == 0 || EditorApplication.isPlayingOrWillChangePlaymode))
                        if (GUILayout.Button("Preview", GUILayout.Width(65))) Preview(sound);
                    if (GUILayout.Button("Edit / Select", GUILayout.Width(90))) Select(sound);
                }
                GUILayout.Label(sound.description, EditorStyles.wordWrappedMiniLabel);
                if (sound.ClipCount == 0) EditorGUILayout.HelpBox("MISSING CLIPS — silent placeholder", MessageType.Warning);
                EditorGUILayout.LabelField(library.events.Contains(sound) ? "Runtime manifest" : "Editor only (not in runtime manifest)", EditorStyles.miniLabel);
            }
        }
        EditorGUILayout.EndScrollView();
        if (selected != null)
        {
            EditorGUILayout.LabelField("Edit " + selected.DisplayName, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Expand Variants to drag in replacement clips. Changes use normal Unity references and Undo.", MessageType.None);
            editScroll = EditorGUILayout.BeginScrollView(editScroll);
            if (inspector == null || inspector.target != selected) Editor.CreateCachedEditor(selected, null, ref inspector);
            inspector.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }
    }
    private void Select(SoundEvent sound) { selected = sound; Selection.activeObject = sound; EditorGUIUtility.PingObject(sound); }
    private void CreateEvent()
    {
        string safe = new string(newName.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
        if (string.IsNullOrWhiteSpace(safe)) return;
        string folder = AudioAssets.Root + "/Events/" + category; AudioAssets.Folder(folder);
        var sound = CreateInstance<SoundEvent>(); sound.displayName = safe; sound.category = category;
        sound.spatial = category != SoundCategory.UI && category != SoundCategory.Music && category != SoundCategory.Ambience;
        sound.playDuringPause = category == SoundCategory.UI;
        var mixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(AudioAssets.MixerPath);
        string group = category == SoundCategory.UI || category == SoundCategory.Music || category == SoundCategory.Ambience || category == SoundCategory.Voice ? category.ToString() : "SFX";
        if (mixer != null) sound.mixerGroup = mixer.FindMatchingGroups(group).FirstOrDefault();
        AssetDatabase.CreateAsset(sound, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safe + ".asset"));
        Undo.RegisterCreatedObjectUndo(sound, "Create Sound Event"); Select(sound);
    }
    private void Preview(SoundEvent sound)
    {
        ClosePreview();
        if (PlayPreview == null) { Debug.LogWarning("Unity clip preview API changed; preview the clip in its Inspector."); return; }
        previous = sound.SelectClip(previous);
        // Native Editor clip audition works without touching the scene/listener.
        // Mixer, randomized gain/pitch and 3D attenuation are verified in Play Mode.
        if (previous != null) PlayPreview.Invoke(null, new object[] { previous, 0, false });
    }
    private void ClosePreview()
    {
        StopPreview?.Invoke(null, null);
    }
}
