using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AudioSceneSetup
{
    public static void ConfigureScene(PlayerCharacter player)
    {
        Scene scene = player.gameObject.scene;
        ConfigureUiAudio(scene);
        ConfigureBeam(player);
    }

    public static void ConfigureUiAudio(Scene scene, bool ensureMenuListener = false)
    {
        AudioAssets.Ensure();
        var services = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AudioService>(true)).ToArray();
        if (services.Length > 1) throw new InvalidOperationException("Multiple AudioServices in this scene. Resolve duplicates before repair.");
        AudioService service = services.FirstOrDefault();
        if (service == null)
        {
            var root = new GameObject("AudioService"); SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Add Audio Service"); service = Undo.AddComponent<AudioService>(root);
        }
        Missing(service, "library", AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioAssets.LibraryPath));
        var feedback = service.GetComponent<UIAudioFeedback>() ?? Undo.AddComponent<UIAudioFeedback>(service.gameObject);
        Missing(feedback, "clickSound", AssetDatabase.LoadAssetAtPath<SoundEvent>(AudioAssets.UiClickPath));
        Missing(feedback, "playConfirmSound", AssetDatabase.LoadAssetAtPath<SoundEvent>(AudioAssets.UiPlayPath));
        if (ensureMenuListener && !scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AudioListener>(true)).Any(l => l.enabled && l.gameObject.activeInHierarchy))
        {
            var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.enabled && c.targetTexture == null && c.gameObject.activeInHierarchy);
            if (camera == null) throw new InvalidOperationException("Menu audio needs an active display camera for its listener.");
            var listener = camera.GetComponent<AudioListener>();
            if (listener == null) listener = Undo.AddComponent<AudioListener>(camera.gameObject);
            Undo.RecordObject(listener, "Enable menu audio listener"); listener.enabled = true;
        }
    }

    private static void ConfigureBeam(PlayerCharacter player)
    {
        var presentation = player.GetComponent<BeamAudioPresentation>() ?? Undo.AddComponent<BeamAudioPresentation>(player.gameObject);
        var data = new SerializedObject(presentation);
        var emitter = data.FindProperty("emitter").objectReferenceValue as AudioEmitter;
        if (emitter == null)
        {
            Transform child = player.transform.Find("BeamAudio");
            if (child == null)
            {
                var go = new GameObject("BeamAudio"); Undo.RegisterCreatedObjectUndo(go, "Add Beam Audio");
                go.transform.SetParent(player.transform, false); child = go.transform;
            }
            emitter = child.GetComponent<AudioEmitter>() ?? Undo.AddComponent<AudioEmitter>(child.gameObject);
            var source = emitter.GetComponent<AudioSource>();
            Undo.RecordObject(source, "Prepare Beam Audio Source"); source.playOnAwake = false;
            Missing(presentation, "emitter", emitter);
        }
        Missing(presentation, "startSound", AssetDatabase.LoadAssetAtPath<SoundEvent>(AudioAssets.BeamStartPath));
        Missing(presentation, "loopSound", AssetDatabase.LoadAssetAtPath<SoundEvent>(AudioAssets.BeamLoopPath));
        Missing(presentation, "endSound", AssetDatabase.LoadAssetAtPath<SoundEvent>(AudioAssets.BeamEndPath));
        Missing(AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/PlasmaPistolItem.asset"), "fireSound", AssetDatabase.LoadAssetAtPath<SoundEvent>(AudioAssets.PistolPath));
        Missing(AssetDatabase.LoadAssetAtPath<UnarmedCombatItemData>(UnarmedCombatSetup.ItemPath), "impactSound", AssetDatabase.LoadAssetAtPath<SoundEvent>(AudioAssets.MeleePath));
    }
    private static void Missing(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        if (target == null) return;
        var data = new SerializedObject(target); var property = data.FindProperty(field);
        if (property.objectReferenceValue != null) return;
        property.objectReferenceValue = value; data.ApplyModifiedProperties();
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        if (AssetDatabase.Contains(target)) AssetDatabase.SaveAssetIfDirty(target);
    }
}
