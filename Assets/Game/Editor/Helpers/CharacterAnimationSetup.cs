using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Shared by character creation and the grenade clip generator. No scene wiring.
public static class CharacterAnimationSetup
{
    public const string ActionsPath = "Assets/Game/Animations/Player/HumanoidAnimationActions.asset";
    public const string ControllerPath = "Assets/Game/Animations/Player/HumanoidShooter.controller";
    public const string ThrowClipPath = "Assets/Game/Animations/Grenade/GrenadeThrow_Unarmed.anim";

    public static bool ConfigureVisual(CharacterVisual visual)
    {
        if (visual == null || visual.Animator == null) return false;
        Animator animator = visual.Animator;
        bool changed = false;
        if (animator.GetComponent<CharacterAnimationEventRelay>() == null)
        {
            animator.gameObject.AddComponent<CharacterAnimationEventRelay>();
            changed = true;
        }
        if (visual.AnimationActions == null && animator.runtimeAnimatorController ==
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath))
        {
            var serialized = new SerializedObject(visual);
            serialized.FindProperty("animationActions").objectReferenceValue = EnsureHumanoidActions();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }
        return changed;
    }

    public static CharacterAnimationActions EnsureHumanoidActions()
    {
        var actions = AssetDatabase.LoadAssetAtPath<CharacterAnimationActions>(ActionsPath);
        if (actions == null)
        {
            actions = ScriptableObject.CreateInstance<CharacterAnimationActions>();
            AssetDatabase.CreateAsset(actions, ActionsPath);
        }
        var serialized = new SerializedObject(actions);
        var bindings = serialized.FindProperty("bindings");
        for (int i = 0; i < bindings.arraySize; i++)
            if (bindings.GetArrayElementAtIndex(i).FindPropertyRelative("action").intValue == (int)CharacterActionId.GrenadeThrow)
                return actions; // Authored mappings belong to the character/content author.
        int index = bindings.arraySize++;
        var binding = bindings.GetArrayElementAtIndex(index);
        binding.FindPropertyRelative("action").intValue = (int)CharacterActionId.GrenadeThrow;
        binding.FindPropertyRelative("triggerParameter").stringValue = "ThrowGrenade";
        binding.FindPropertyRelative("layerName").stringValue = "GrenadeThrow";
        binding.FindPropertyRelative("statePath").stringValue = "GrenadeThrow.TossGrenade";
        binding.FindPropertyRelative("clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AnimationClip>(ThrowClipPath);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(actions);
        return actions;
    }

    public static void ConfigureExistingCharacters()
    {
        EnsureHumanoidActions();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Prefabs/Player/Characters" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (ConfigureVisual(root.GetComponent<CharacterVisual>()))
                    PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    public static void SetReleaseMarker(AnimationClip clip, float authoredTime)
    {
        if (clip == null) throw new ArgumentNullException(nameof(clip));
        string entry = nameof(CharacterAnimationEventRelay.OnCharacterAnimationEvent);
        var markers = AnimationUtility.GetAnimationEvents(clip)
            .Where(marker => marker.functionName != entry || marker.intParameter != (int)CharacterAnimationEventId.GrenadeRelease)
            .ToList();
        markers.Add(new AnimationEvent { time = authoredTime, functionName = entry,
            intParameter = (int)CharacterAnimationEventId.GrenadeRelease });
        AnimationUtility.SetAnimationEvents(clip, markers.OrderBy(marker => marker.time).ToArray());
        EditorUtility.SetDirty(clip);
    }
}
