using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class FloatingAnimationSetup
{
    public const string ClipPath = "Assets/Game/Animations/Floating/Floating.fbx";
    public const string LayerName = CharacterAnimatorDriver.FloatingLayerName;

    [MenuItem("Tools/Setup/Repair Floating Animation Presentation")]
    public static void Ensure()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ClipPath);
        var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        bool changed = importer.animationType != ModelImporterAnimationType.Human || importer.clipAnimations.Length == 0;
        importer.animationType = ModelImporterAnimationType.Human;
        foreach (var clip in clips)
        {
            changed |= !clip.loopTime || !clip.loopPose || !clip.lockRootRotation || !clip.lockRootHeightY || !clip.lockRootPositionXZ;
            clip.loopTime = clip.loopPose = true;
            clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
        }
        if (changed) { importer.clipAnimations = clips; importer.SaveAndReimport(); }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAnimationSetup.ControllerPath);
        bool controllerChanged = false;
        if (!controller.parameters.Any(p => p.name == "Floating"))
        { controller.AddParameter("Floating", AnimatorControllerParameterType.Bool); controllerChanged = true; }
        if (!controller.layers.Any(l => l.name == LayerName))
        {
            var machine = new AnimatorStateMachine { name = LayerName, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(machine, controller);
            var mask = new AvatarMask { name = "Floating Body", hideFlags = HideFlags.HideInHierarchy };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, i != (int)AvatarMaskBodyPart.Root);
            AssetDatabase.AddObjectToAsset(mask, controller);
            var empty = machine.AddState("Empty"); empty.writeDefaultValues = false;
            var floating = machine.AddState("Floating"); floating.writeDefaultValues = false;
            floating.motion = AssetDatabase.LoadAllAssetsAtPath(ClipPath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            machine.defaultState = empty;
            var enter = empty.AddTransition(floating);
            enter.hasExitTime = false; enter.hasFixedDuration = true; enter.duration = .1f;
            enter.AddCondition(AnimatorConditionMode.If, 0, "Floating");
            var exit = floating.AddTransition(empty);
            exit.hasExitTime = false; exit.hasFixedDuration = true; exit.duration = .1f;
            exit.AddCondition(AnimatorConditionMode.IfNot, 0, "Floating");
            controller.AddLayer(new AnimatorControllerLayer { name = LayerName, defaultWeight = 0f,
                blendingMode = AnimatorLayerBlendingMode.Override, stateMachine = machine, avatarMask = mask });
            controllerChanged = true;
        }
        var layers = controller.layers;
        int floatingIndex = System.Array.FindIndex(layers, l => l.name == LayerName);
        if (layers[floatingIndex].defaultWeight != 0f)
        {
            layers[floatingIndex].defaultWeight = 0f;
            controller.layers = layers;
            controllerChanged = true;
        }
        if (controllerChanged) { EditorUtility.SetDirty(controller); AssetDatabase.SaveAssetIfDirty(controller); }
    }
}
