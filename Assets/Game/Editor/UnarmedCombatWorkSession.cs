using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.TestTools.TestRunner.Api;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class UnarmedCombatWorkSession
{
    private const string Boxing = "Assets/Game/Animations/Combat/Boxing_mixamo.fbx";
    private const string Idle = "Assets/Game/Animations/Combat/FightingIdle_mixamo.fbx";
    static UnarmedCombatWorkSession() => EditorApplication.update += Tick;
    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists("Temp/Melee.refresh")) { File.Delete("Temp/Melee.refresh"); AssetDatabase.Refresh(); return; }
        if (!File.Exists("Temp/Melee.request")) return;
        string request = File.ReadAllText("Temp/Melee.request").Trim(); File.Delete("Temp/Melee.request");
        try
        {
            if (request == "setup") Setup();
            if (request == "sample") Sample();
            if (request == "markers") Markers();
            if (request == "focused" || request == "core") { RunTests(request); return; }
            if (request == "capture") { MeleeMotionReview.Capture(CharacterActionId.MeleeLight1); MeleeMotionReview.Capture(CharacterActionId.MeleeLight2); }
            File.WriteAllText("Temp/Melee.result", "OK " + request);
        }
        catch (Exception error) { File.WriteAllText("Temp/Melee.result", error.ToString()); Debug.LogException(error); }
    }

    private static AnimationClip Clip(string path, string name = null) => AssetDatabase.LoadAllAssetsAtPath(path)
        .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__") && (name == null || c.name == name));

    private static TestRunnerApi api;
    private static void RunTests(string kind)
    {
        api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Results(kind));
        var filter = new Filter { testMode = TestMode.EditMode, categoryNames = new[] { "Core" } };
        if (kind == "focused") filter.testNames = new[] { "PlayerMeleeTests", "UnarmedCombatContentTests", "GrenadeAnimationTests", "GrenadeThrowMotionTests", "CharacterAnimationSetupTests" };
        Time.timeScale = 1;
        api.Execute(new ExecutionSettings(filter));
    }
    private sealed class Results : ICallbacks
    {
        private readonly string kind;
        private readonly float scale = Time.timeScale;
        private readonly Dictionary<string, byte[]> saved = new Dictionary<string, byte[]>();
        public Results(string kind)
        {
            this.kind = kind;
            foreach (string path in new[] { "ProjectSettings/TimeManager.asset", "Assets/Resources/PerformanceTestRunInfo.json", "Assets/Resources/PerformanceTestRunInfo.json.meta",
                "Assets/Resources/PerformanceTestRunSettings.json", "Assets/Resources/PerformanceTestRunSettings.json.meta" })
                if (File.Exists(path)) saved[path] = File.ReadAllBytes(path);
        }
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, "Logs/Melee-" + kind + "-tests.xml");
            Time.timeScale = scale;
            foreach (var pair in saved) File.WriteAllBytes(pair.Key, pair.Value);
            File.WriteAllText("Temp/Melee.result", $"{kind}: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped");
        }
    }

    private static void Markers()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(Boxing);
        var clips = importer.clipAnimations;
        foreach (var clip in clips)
        {
            if (clip.name != "Punch_Left" && clip.name != "Punch_Right") continue;
            var events = clip.events.Where(e => e.functionName != nameof(CharacterAnimationEventRelay.OnCharacterAnimationEvent)
                || e.intParameter != (int)CharacterAnimationEventId.MeleeImpact).ToList();
            // Contact chosen from the actual retargeted fist samples and visual review.
            // ModelImporter stores event times normalized to the split clip.
            events.Add(new AnimationEvent { time = .25f / Clip(Boxing, clip.name).length,
                functionName = nameof(CharacterAnimationEventRelay.OnCharacterAnimationEvent), intParameter = (int)CharacterAnimationEventId.MeleeImpact });
            clip.events = events.OrderBy(e => e.time).ToArray();
        }
        importer.clipAnimations = clips; importer.SaveAndReimport();
        File.WriteAllText("Logs/Melee/authored-markers.txt", string.Join("\n", new[] { "Punch_Left", "Punch_Right" }
            .Select(n => n + " length=" + Clip(Boxing,n).length + " marker=" + Clip(Boxing,n).events.Single(e => e.intParameter == 1).time)));
    }

    private static void Setup()
    {
        foreach (string path in new[] { Idle, Boxing })
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = path == Idle;
                clip.loopPose = path == Idle;
                clip.lockRootRotation = false; clip.lockRootHeightY = true; clip.lockRootPositionXZ = false;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAnimationSetup.ControllerPath);
        var layers = controller.layers.ToList();
        var combat = layers.Single(l => l.name == "UnarmedCombatActions");
        layers.Remove(combat); layers.Insert(layers.FindIndex(l => l.name == "GrenadeThrow"), combat);
        var mask = new AvatarMask { name = "AM_UnarmedCombat" };
        const string maskPath = "Assets/Game/Animations/Player/AM_UnarmedCombat.mask";
        var existing = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if (existing != null) { Object.DestroyImmediate(mask); mask = existing; }
        else AssetDatabase.CreateAsset(mask, maskPath);
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
            AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers }) mask.SetHumanoidBodyPartActive(part, true);
        mask.transformCount = 0;
        combat.avatarMask = mask; combat.defaultWeight = 1; combat.blendingMode = AnimatorLayerBlendingMode.Override;
        controller.layers = layers.ToArray();
        foreach (string trigger in new[] { "MeleeLight1", "MeleeLight2" })
            if (!controller.parameters.Any(p => p.name == trigger)) controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
        if (!controller.parameters.Any(p => p.name == "CombatStance")) controller.AddParameter("CombatStance", AnimatorControllerParameterType.Bool);
        var baseMachine = layers[0].stateMachine;
        var normal = baseMachine.states.Single(s => s.state.name == "UnarmedLocomotion").state;
        var stance = baseMachine.states.FirstOrDefault(s => s.state.name == "UnarmedCombatLocomotion").state;
        if (stance == null)
        {
            stance = baseMachine.AddState("UnarmedCombatLocomotion", new Vector3(-400, 200));
            var tree = Object.Instantiate((BlendTree)normal.motion);
            tree.name = "UnarmedCombatLocomotion";
            var children = tree.children;
            for (int i = 0; i < children.Length; i++) if (children[i].position == Vector2.zero) children[i].motion = Clip(Idle);
            tree.children = children;
            AssetDatabase.AddObjectToAsset(tree, controller);
            stance.motion = tree; stance.writeDefaultValues = normal.writeDefaultValues;
        }
        var toNormal = baseMachine.anyStateTransitions.Single(t => t.destinationState == normal);
        if (!toNormal.conditions.Any(c => c.parameter == "CombatStance")) toNormal.AddCondition(AnimatorConditionMode.IfNot, 0, "CombatStance");
        if (!baseMachine.anyStateTransitions.Any(t => t.destinationState == stance))
        {
            var toStance = baseMachine.AddAnyStateTransition(stance);
            toStance.hasExitTime = false; toStance.duration = .15f; toStance.hasFixedDuration = true; toStance.canTransitionToSelf = false;
            toStance.AddCondition(AnimatorConditionMode.Equals, 0, "WeaponStyle");
            toStance.AddCondition(AnimatorConditionMode.If, 0, "CombatStance");
        }
        var empty = combat.stateMachine.states.Single(s => s.state.name == "Empty").state;
        empty.motion = null; combat.stateMachine.defaultState = empty;
        var actions = new SerializedObject(AssetDatabase.LoadAssetAtPath<CharacterAnimationActions>(CharacterAnimationSetup.ActionsPath));
        var bindings = actions.FindProperty("bindings");
        string[] clipNames = { "Punch_Left", "Punch_Right" };
        for (int i = 0; i < clipNames.Length; i++)
        {
            var action = i == 0 ? CharacterActionId.MeleeLight1 : CharacterActionId.MeleeLight2;
            var state = combat.stateMachine.states.Single(s => s.state.name == clipNames[i]).state;
            state.motion = Clip(Boxing, clipNames[i]); state.writeDefaultValues = true;
            if (!combat.stateMachine.anyStateTransitions.Any(t => t.destinationState == state))
            {
                var enter = combat.stateMachine.AddAnyStateTransition(state);
                enter.hasExitTime = false; enter.hasFixedDuration = true; enter.duration = .04f; enter.canTransitionToSelf = false;
                enter.AddCondition(AnimatorConditionMode.If, 0, action.ToString());
                enter.AddCondition(AnimatorConditionMode.If, 0, "CombatStance");
            }
            if (!state.transitions.Any(t => t.destinationState == empty))
            {
                var exit = state.AddTransition(empty); exit.hasExitTime = true; exit.exitTime = .85f;
                exit.hasFixedDuration = true; exit.duration = .09f;
            }
            int index = -1;
            for (int b = 0; b < bindings.arraySize; b++) if (bindings.GetArrayElementAtIndex(b).FindPropertyRelative("action").intValue == (int)action) index = b;
            if (index < 0) index = bindings.arraySize++;
            var binding = bindings.GetArrayElementAtIndex(index);
            binding.FindPropertyRelative("action").intValue = (int)action;
            binding.FindPropertyRelative("triggerParameter").stringValue = action.ToString();
            binding.FindPropertyRelative("layerName").stringValue = combat.name;
            binding.FindPropertyRelative("statePath").stringValue = combat.name + "." + state.name;
            binding.FindPropertyRelative("clip").objectReferenceValue = state.motion;
            binding.FindPropertyRelative("cancellationStatePath").stringValue = combat.name + ".Empty";
        }
        actions.ApplyModifiedPropertiesWithoutUndo();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(CharacterAnimationSetup.ControllerPath)) EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(mask);
        AssetDatabase.SaveAssetIfDirty(mask); AssetDatabase.SaveAssetIfDirty(controller); AssetDatabase.SaveAssetIfDirty(actions.targetObject);
        CharacterAnimationSetup.ConfigureExistingCharacters();
        Sample();
    }

    private static void Sample()
    {
        Directory.CreateDirectory("Logs/Melee");
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterVisual>("Assets/Game/Prefabs/Player/Characters/Amy.prefab"));
        var csv = new StringBuilder("clip,time,leftZ,rightZ,leftY,rightY,leftReach,rightReach,leftX,rightX,hipsZ\n");
        try
        {
            var animator = actor.Animator; animator.fireEvents = false; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (string name in new[] { "Punch_Left", "Punch_Right" })
            {
                var clip = Clip(Boxing, name);
                animator.Rebind(); animator.SetBool("CombatStance", true); animator.Update(0); animator.Update(.2f);
                animator.Play("UnarmedCombatActions." + name, animator.GetLayerIndex("UnarmedCombatActions"), 0); animator.Update(0);
                for (float time = 0; time < clip.length; time += 1f / 120)
                {
                    var left = actor.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftHand).position);
                    var right = actor.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightHand).position);
                    var leftShoulder = actor.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position);
                    var rightShoulder = actor.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position);
                    var hips = actor.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Hips).position);
                    csv.AppendLine(FormattableString.Invariant($"{name},{time:F5},{left.z:F5},{right.z:F5},{left.y:F5},{right.y:F5},{Vector3.Distance(left,leftShoulder):F5},{Vector3.Distance(right,rightShoulder):F5},{left.x:F5},{right.x:F5},{hips.z:F5}"));
                    animator.Update(1f / 120);
                }
            }
            File.WriteAllText("Logs/Melee/impact-samples.csv", csv.ToString());
        }
        finally { Object.DestroyImmediate(actor.gameObject); }
    }
}
