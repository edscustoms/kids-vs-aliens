using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Generates a short Humanoid grenade throw from the project's existing idle pose.
/// No manual keyframing required.
///
/// The generated clip intentionally animates only torso + both arms.
/// Legs/root continue to come from the Base Layer through AM_GrenadeThrow.
/// </summary>
public static class GenerateGrenadeThrowAnimation
{
    private const string OutputFolder = "Assets/Game/Animations/Grenade";
    private const string OutputClipPath = OutputFolder + "/GrenadeThrow_Unarmed.anim";

    private const string PreferredIdlePath = "Assets/Game/Animations/Unarmed/idle.fbx";
    private const string PreferredControllerPath = "Assets/Game/Animations/Player/HumanoidShooter.controller";
    private const string PreferredMaskPath = "Assets/Game/Animations/Player/AM_GrenadeThrow.mask";

    private const string GrenadeLayerName = "GrenadeThrow";
    private const string TossStateName = "TossGrenade";
    private const string ThrowTriggerName = "ThrowGrenade";

    private const float FrameRate = 30f;
    private const float Duration = 1.35f;

    // Preparation leads with the raised elbow; extension follows the shoulder drive.
    // The release key is shared by authoring/wiring, never a gameplay timer.
    public const float ReleaseTime = 0.54f;
    private static readonly float[] Times =
        { 0f, .18f, .32f, .43f, ReleaseTime, .66f, .95f, Duration };

    [MenuItem("Tools/Kids VS Aliens/Helpers/Generate Grenade Throw Animation")]
    public static void Generate()
    {
        AnimationClip idle = FindIdleClip();
        if (idle == null)
        {
            Debug.LogError(
                "Grenade Throw Generator: could not find the project's Humanoid idle clip. " +
                "Expected something like Assets/Game/Animations/Unarmed/idle.fbx.");
            return;
        }

        EnsureFolder(OutputFolder);

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputClipPath);
        if (clip == null)
        {
            clip = new AnimationClip
            {
                name = "GrenadeThrow_Unarmed",
                frameRate = FrameRate,
                wrapMode = WrapMode.Once,
            };

            AssetDatabase.CreateAsset(clip, OutputClipPath);
        }
        else
        {
            clip.ClearCurves();
            clip.frameRate = FrameRate;
            clip.wrapMode = WrapMode.Once;
        }

        Dictionary<string, float> idleValues = ReadHumanoidPoseAtStart(idle);

        // Offsets from the imported unarmed idle, in Unity Humanoid muscle space.
        // Cock the torso/right elbow, drive the shoulder, extend the forearm, then unwind.
        SetMuscle(clip, idleValues, "Spine Front-Back",
            0, .025f, .06f, .01f, -.08f, -.10f, -.035f, 0);
        SetMuscle(clip, idleValues, "Spine Twist Left-Right",
            0, .06f, .12f, .02f, -.12f, -.14f, -.045f, 0);
        SetMuscle(clip, idleValues, "Chest Front-Back",
            0, .04f, .08f, .015f, -.10f, -.12f, -.04f, 0);
        SetMuscle(clip, idleValues, "Chest Twist Left-Right",
            0, .10f, .20f, .035f, -.20f, -.23f, -.07f, 0);
        SetMuscleIfPresent(clip, idleValues, "UpperChest Front-Back",
            0, .02f, .04f, 0, -.055f, -.06f, -.02f, 0);
        SetMuscleIfPresent(clip, idleValues, "UpperChest Twist Left-Right",
            0, .06f, .12f, .02f, -.12f, -.14f, -.04f, 0);

        SetMuscle(clip, idleValues, "Right Shoulder Front-Back",
            0, .18f, .35f, -.10f, -.35f, -.30f, -.10f, 0);
        SetMuscle(clip, idleValues, "Right Shoulder Down-Up",
            0, .20f, .38f, .35f, .25f, .15f, .04f, 0);
        SetMuscle(clip, idleValues, "Right Arm Front-Back",
            0, .25f, .50f, -.35f, -.63f, -.68f, -.30f, 0);
        SetMuscle(clip, idleValues, "Right Arm Down-Up",
            0, .50f, .95f, .98f, .91f, .72f, .20f, 0);
        SetMuscle(clip, idleValues, "Right Arm Twist In-Out",
            0, .20f, .45f, .25f, .08f, .10f, .04f, 0);
        SetMuscle(clip, idleValues, "Right Forearm Stretch",
            0, -.70f, -1.35f, -.85f, .16f, .10f, -.10f, 0);
        SetMuscle(clip, idleValues, "Right Forearm Twist In-Out",
            0, -.10f, -.20f, -.10f, -.05f, -.05f, 0, 0);
        SetMuscleIfPresent(clip, idleValues, "Right Hand Down-Up",
            0, .10f, .18f, .10f, -.08f, -.15f, -.04f, 0);
        SetMuscleIfPresent(clip, idleValues, "Right Hand In-Out",
            0, -.06f, -.12f, -.06f, -.04f, -.08f, -.02f, 0);

        // A smaller opposing arm action leaves a distinct one-handed silhouette.
        SetMuscle(clip, idleValues, "Left Shoulder Front-Back",
            0, -.05f, -.10f, .05f, .18f, .20f, .07f, 0);
        SetMuscle(clip, idleValues, "Left Shoulder Down-Up",
            0, .08f, .15f, .12f, .05f, .02f, 0, 0);
        SetMuscle(clip, idleValues, "Left Arm Front-Back",
            0, -.18f, -.35f, -.10f, .18f, .25f, .08f, 0);
        SetMuscle(clip, idleValues, "Left Arm Down-Up",
            0, .15f, .25f, .20f, .12f, .08f, .02f, 0);
        SetMuscle(clip, idleValues, "Left Arm Twist In-Out",
            0, -.04f, -.08f, -.04f, .02f, .04f, .01f, 0);
        SetMuscle(clip, idleValues, "Left Forearm Stretch",
            0, -.20f, -.35f, -.25f, -.10f, -.08f, -.02f, 0);
        SetMuscle(clip, idleValues, "Left Forearm Twist In-Out",
            0, .02f, .04f, .02f, 0, 0, 0, 0);
        SetMuscleIfPresent(clip, idleValues, "Left Hand Down-Up",
            0, -.02f, -.04f, -.02f, 0, 0, 0, 0);
        SetMuscleIfPresent(clip, idleValues, "Left Hand In-Out",
            0, .02f, .04f, .02f, 0, 0, 0, 0);

        CharacterAnimationSetup.SetReleaseMarker(clip, ReleaseTime);
        EditorUtility.SetDirty(clip);

        AvatarMask mask = CreateOrUpdateMask();
        AnimatorController controller = FindController();

        if (controller != null)
        {
            EnsureTrigger(controller);
            AssignToGrenadeLayer(controller, mask, clip);
            RemoveConflictingBaseThrowTransitions(controller);
            EditorUtility.SetDirty(controller);
        }
        else
        {
            Debug.LogWarning(
                "Grenade Throw Generator: generated the clip successfully, " +
                "but HumanoidShooter.controller was not found automatically. " +
                "Assign GrenadeThrow_Unarmed to TossGrenade manually.");
        }

        CharacterAnimationSetup.ConfigureExistingCharacters();
        AssetDatabase.SaveAssetIfDirty(clip);
        AssetDatabase.SaveAssetIfDirty(mask);
        if (controller != null) AssetDatabase.SaveAssetIfDirty(controller);
        AssetDatabase.Refresh();

        Selection.activeObject = clip;
        EditorGUIUtility.PingObject(clip);

        Debug.Log(
            "Grenade Throw Generator: GrenadeThrow_Unarmed generated and wired. " +
            "Duration 1.35s; raised backward wind-up, shoulder-led extension, release at 0.54s, eased recovery. " +
            "Use Capture Grenade Throw Motion to review the generated action over locomotion.");
    }

    [MenuItem("Tools/Kids VS Aliens/Helpers/Wire Grenade Throw Animation")]
    public static void WireExistingAnimation()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputClipPath);
        var controller = FindController();
        if (clip == null || controller == null)
            throw new InvalidOperationException("Generate the grenade clip/controller before wiring it.");
        // Wire the current clip without regenerating its authored pose/curves.
        CharacterAnimationSetup.SetReleaseMarker(clip, ReleaseTime);
        EnsureTrigger(controller);
        RemoveConflictingBaseThrowTransitions(controller);
        CharacterAnimationSetup.ConfigureExistingCharacters();
        AssetDatabase.SaveAssetIfDirty(clip);
        AssetDatabase.SaveAssetIfDirty(controller);
        Debug.Log("Grenade animation wired: authored release marker, shared action mapping and compatible character relays. No gameplay scene was saved.");
    }

    private static void RemoveConflictingBaseThrowTransitions(AnimatorController controller)
    {
        foreach (var child in controller.layers[0].stateMachine.states)
            foreach (var transition in child.state.transitions)
                if (transition.destinationState != null && transition.destinationState.name == TossStateName
                    && transition.conditions.Any(condition => condition.parameter == ThrowTriggerName))
                {
                    child.state.RemoveTransition(transition);
                    EditorUtility.SetDirty(child.state);
                }
        // The old experimental Base Layer node stays as authored content;
        // only its conflicting trigger path is removed. Locomotion keeps running.
        EditorUtility.SetDirty(controller);
    }

    private static AnimationClip FindIdleClip()
    {
        AnimationClip direct = FindClipAtPath(PreferredIdlePath, "idle");
        if (direct != null)
            return direct;

        string[] guids = AssetDatabase.FindAssets("idle t:Model");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.IndexOf("/Animations/Unarmed/", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            AnimationClip clip = FindClipAtPath(path, "idle");
            if (clip != null)
                return clip;
        }

        return null;
    }

    private static AnimationClip FindClipAtPath(string path, string preferredName)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

        AnimationClip fallback = null;

        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is not AnimationClip clip)
                continue;

            if (clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                continue;

            fallback ??= clip;

            if (string.Equals(clip.name, preferredName, StringComparison.OrdinalIgnoreCase))
                return clip;
        }

        return fallback;
    }

    private static Dictionary<string, float> ReadHumanoidPoseAtStart(AnimationClip source)
    {
        var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
        {
            if (binding.type != typeof(Animator))
                continue;

            AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
            if (curve == null)
                continue;

            result[binding.propertyName] = curve.Evaluate(0f);
        }

        return result;
    }

    private static void SetMuscle(
        AnimationClip clip,
        Dictionary<string, float> idleValues,
        string requestedMuscle,
        params float[] offsets)
    {
        string actual = FindMuscleName(requestedMuscle);
        if (actual == null)
        {
            Debug.LogWarning($"Grenade Throw Generator: Humanoid muscle '{requestedMuscle}' was not found; skipping it.");
            return;
        }

        SetMuscleCurve(clip, idleValues, actual, offsets);
    }

    private static void SetMuscleIfPresent(
        AnimationClip clip,
        Dictionary<string, float> idleValues,
        string requestedMuscle,
        params float[] offsets)
    {
        string actual = FindMuscleName(requestedMuscle);
        if (actual == null)
            return;

        SetMuscleCurve(clip, idleValues, actual, offsets);
    }

    private static void SetMuscleCurve(
        AnimationClip clip,
        Dictionary<string, float> idleValues,
        string muscleName,
        float[] offsets)
    {
        if (offsets == null || offsets.Length != Times.Length)
            throw new ArgumentException("Every grenade throw muscle curve must provide one value per authored key time.");

        float baseValue = idleValues.TryGetValue(muscleName, out float value)
            ? value
            : 0f;

        Keyframe[] keys = new Keyframe[Times.Length];

        for (int i = 0; i < Times.Length; i++)
        {
            float muscleValue = Mathf.Clamp(baseValue + offsets[i], -1f, 1f);
            keys[i] = new Keyframe(Times[i], muscleValue);
        }

        AnimationCurve curve = new AnimationCurve(keys);

        // Monotone cubic interpolation eases preparation/recovery without overshoot.
        for (int i = 0; i < keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }

        EditorCurveBinding binding = EditorCurveBinding.FloatCurve(
            string.Empty,
            typeof(Animator),
            muscleName);

        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    private static string FindMuscleName(string requested)
    {
        string normalizedRequested = Normalize(requested);

        foreach (string muscle in HumanTrait.MuscleName)
        {
            if (Normalize(muscle) == normalizedRequested)
                return muscle;
        }

        // Mild fallback for Unity naming differences between versions.
        string[] requestedTokens = normalizedRequested
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string muscle in HumanTrait.MuscleName)
        {
            string normalizedMuscle = Normalize(muscle);
            bool allTokensMatch = requestedTokens.All(normalizedMuscle.Contains);

            if (allTokensMatch)
                return muscle;
        }

        return null;
    }

    private static string Normalize(string value)
    {
        return string.Join(
            " ",
            value
                .Replace("-", " ")
                .Replace("_", " ")
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            .Trim()
            .ToLowerInvariant();
    }

    private static AvatarMask CreateOrUpdateMask()
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(PreferredMaskPath);

        if (mask == null)
        {
            string[] guids = AssetDatabase.FindAssets("AM_GrenadeThrow t:AvatarMask");
            if (guids.Length > 0)
            {
                mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        if (mask == null)
        {
            EnsureFolder(Path.GetDirectoryName(PreferredMaskPath)?.Replace('\\', '/'));
            mask = new AvatarMask { name = "AM_GrenadeThrow" };
            AssetDatabase.CreateAsset(mask, PreferredMaskPath);
        }

        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        }

        // Custom clip intentionally owns torso + both arms only.
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);

        // Finger curls can remain driven by the base locomotion for V1.
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, false);

        // Remove old per-transform experiments so the Humanoid mask is the
        // single source of truth for this generated clip.
        mask.transformCount = 0;

        EditorUtility.SetDirty(mask);
        return mask;
    }

    private static AnimatorController FindController()
    {
        AnimatorController direct = AssetDatabase.LoadAssetAtPath<AnimatorController>(PreferredControllerPath);
        if (direct != null)
            return direct;

        string[] guids = AssetDatabase.FindAssets("HumanoidShooter t:AnimatorController");
        if (guids.Length == 0)
            return null;

        return AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static void EnsureTrigger(AnimatorController controller)
    {
        var parameters = controller.parameters;
        var existing = parameters.FirstOrDefault(p => p.name == ThrowTriggerName);
        if (existing != null)
        {
            existing.defaultBool = false; // No unsolicited throw on Animator startup.
            controller.parameters = parameters;
            return;
        }

        controller.AddParameter(ThrowTriggerName, AnimatorControllerParameterType.Trigger);
    }

    private static void AssignToGrenadeLayer(
        AnimatorController controller,
        AvatarMask mask,
        AnimationClip clip)
    {
        AnimatorControllerLayer[] layers = controller.layers;
        int layerIndex = Array.FindIndex(layers, l => l.name == GrenadeLayerName);

        if (layerIndex < 0)
        {
            Debug.LogWarning(
                "Grenade Throw Generator: GrenadeThrow Animator layer was not found. " +
                "The animation clip and mask were generated, but layer wiring was skipped.");
            return;
        }

        layers[layerIndex].avatarMask = mask;
        layers[layerIndex].defaultWeight = 1f;
        layers[layerIndex].blendingMode = AnimatorLayerBlendingMode.Override;

        AnimatorState tossState = layers[layerIndex]
            .stateMachine
            .states
            .Select(s => s.state)
            .FirstOrDefault(s => s.name == TossStateName);

        if (tossState == null)
        {
            Debug.LogWarning(
                "Grenade Throw Generator: TossGrenade state was not found on GrenadeThrow layer. " +
                "Assign GrenadeThrow_Unarmed manually.");
        }
        else
        {
            tossState.motion = clip;
            // Release the upper body gradually to the currently running locomotion,
            // rather than holding an idle arm pose until a short final snap.
            foreach (var transition in tossState.transitions)
                if (transition.destinationState != null && transition.destinationState.name == "Idle")
                {
                    transition.hasExitTime = true;
                    transition.hasFixedDuration = true;
                    transition.exitTime = (Duration - .25f) / Duration;
                    transition.duration = .25f;
                    EditorUtility.SetDirty(transition);
                }
            EditorUtility.SetDirty(tossState);
        }

        controller.layers = layers;
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            return;

        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
