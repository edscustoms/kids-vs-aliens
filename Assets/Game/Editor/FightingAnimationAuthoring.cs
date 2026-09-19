using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Non-destructive derivatives. The complete source pack stays intact.
public static class FightingAnimationAuthoring
{
    public const string Output = "Assets/Game/Animations/Combat_v2/Fighting";
    public readonly struct Selection
    {
        public readonly string source, name;
        public readonly float start, end, speed, impact, chain;
        public Selection(string source, string name, float start, float end, float speed, float impact, float chain)
        { this.source=source; this.name=name; this.start=start; this.end=end; this.speed=speed; this.impact=impact; this.chain=chain; }
        public float ChainNormalized => (chain-start)/(end-start);
    }
    public static readonly Selection[] Attacks = {
        new("jab_left", "Light1", .12f, .85f, 1.2f, .4f, .65f),
        new("cross_right", "Light2", .34f, 1.27f, 1.25f, .78f, 1.06f),
        // Split the authored combination at the shared linking pose. Separate FIRE
        // presses remain authoritative; both halves retain the same wide stance.
        new("combo_hook_uppercut", "Light3", 1.15f, 1.95f, 1.15f, 1.60f, 1.85f),
        new("combo_hook_uppercut", "Heavy", 1.85f, 2.85f, 1.2f, 2.15f, 2.65f),
        new("front_kick", "Kick", .55f, 1.90f, 1.2f, 1.20f, 1.65f),
        new("roundhouse_kick_right", "HeavyKick", 1.05f, 2.90f, 1.25f, 1.75f, 2.55f)
    };
    public static AnimationClip Source(string name) => AssetDatabase.LoadAllAssetsAtPath(CombatV2Audition.Folder + "/" + name + ".fbx")
        .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
    public static AnimationClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(Output + "/" + name + ".anim");
    public static void RebuildAndReview()
    {
        ApplyProduction();
        CombatV2Audition.FinalReview();
    }

    public static void BuildCandidates()
    {
        Directory.CreateDirectory(Output); AssetDatabase.Refresh();
        foreach (var selection in Attacks) Build(selection, false, true);
        Build(new Selection("jab_left", "Guard", 1.15f, 2.85f, 1, -1, -1), true, true);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Animation/Apply Reviewed Fighting V1")]
    public static void ApplyProduction()
    {
        BuildCandidates();
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAnimationSetup.ControllerPath);
        var layers=controller.layers.ToList();
        var combat=layers.Single(l=>l.name=="UnarmedCombatActions");
        var machine=combat.stateMachine;
        foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
        foreach (var state in machine.states) machine.RemoveState(state.state);
        var empty=machine.AddState("Empty"); empty.writeDefaultValues=true; machine.defaultState=empty;
        var upper=combat.avatarMask;
        upper.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head,true); EditorUtility.SetDirty(upper);
        var actions=new SerializedObject(AssetDatabase.LoadAssetAtPath<CharacterAnimationActions>(CharacterAnimationSetup.ActionsPath));
        var bindings=actions.FindProperty("bindings");
        var ids=new[] {CharacterActionId.MeleeLight1,CharacterActionId.MeleeLight2,CharacterActionId.MeleeLight3,CharacterActionId.MeleeHeavy,CharacterActionId.Kick,CharacterActionId.HeavyKick};
        for(int i=0;i<Attacks.Length;i++)
        {
            string trigger=ids[i].ToString();
            if(!controller.parameters.Any(p=>p.name==trigger)) controller.AddParameter(trigger,AnimatorControllerParameterType.Trigger);
            var state=machine.AddState(Attacks[i].name); state.motion=Clip(Attacks[i].name); state.writeDefaultValues=true;
            state.iKOnFeet=false; // Upper-body actions must not write foot IK goals over locomotion.
            var enter=machine.AddAnyStateTransition(state); enter.hasExitTime=false; enter.hasFixedDuration=true;
            enter.duration=.12f; enter.canTransitionToSelf=false;
            enter.AddCondition(AnimatorConditionMode.If,0,trigger); enter.AddCondition(AnimatorConditionMode.If,0,"CombatStance");
            var exit=state.AddTransition(empty); exit.hasExitTime=true; exit.exitTime=.92f;
            exit.hasFixedDuration=true; exit.duration=.16f;
            int index=-1;
            for(int b=0;b<bindings.arraySize;b++) if(bindings.GetArrayElementAtIndex(b).FindPropertyRelative("action").intValue==(int)ids[i]) index=b;
            if(index<0) index=bindings.arraySize++;
            var binding=bindings.GetArrayElementAtIndex(index);
            binding.FindPropertyRelative("action").intValue=(int)ids[i];
            binding.FindPropertyRelative("triggerParameter").stringValue=trigger;
            binding.FindPropertyRelative("layerName").stringValue=combat.name;
            binding.FindPropertyRelative("statePath").stringValue=combat.name+"."+state.name;
            binding.FindPropertyRelative("cancellationStatePath").stringValue=combat.name+".Empty";
            binding.FindPropertyRelative("clip").objectReferenceValue=state.motion;
            binding.FindPropertyRelative("chainStart").floatValue=Attacks[i].ChainNormalized;
            binding.FindPropertyRelative("requiresLegMotion").boolValue=i>=4;
        }
        actions.ApplyModifiedPropertiesWithoutUndo();
        var baseMachine=layers[0].stateMachine;
        var stance=baseMachine.states.Single(s=>s.state.name=="UnarmedCombatLocomotion").state;
        var tree=(BlendTree)stance.motion; var children=tree.children;
        for(int i=0;i<children.Length;i++) if(children[i].position==Vector2.zero) children[i].motion=Clip("Guard");
        tree.children=children;
        foreach(var t in baseMachine.anyStateTransitions)
            if(t.destinationState==stance || t.destinationState.name=="UnarmedLocomotion")
            { t.duration=.25f; t.hasFixedDuration=true; }
        var guard=layers.FirstOrDefault(l=>l.name=="CombatGuard");
        if(guard==null)
        {
            guard=new AnimatorControllerLayer {name="CombatGuard",defaultWeight=0,avatarMask=upper,stateMachine=new AnimatorStateMachine {name="CombatGuard"}};
            AssetDatabase.AddObjectToAsset(guard.stateMachine,controller); layers.Insert(layers.IndexOf(combat),guard);
        }
        var guardState=guard.stateMachine.states.FirstOrDefault().state ?? guard.stateMachine.AddState("Guard");
        guardState.motion=Clip("Guard"); guardState.writeDefaultValues=true; guard.stateMachine.defaultState=guardState;
        var feet=AssetDatabase.LoadAssetAtPath<AvatarMask>(Output+"/Footwork.mask");
        if(feet==null) { feet=new AvatarMask {name="FightingFootwork"}; AssetDatabase.CreateAsset(feet,Output+"/Footwork.mask"); }
        for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++) feet.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,true);
        var footwork=layers.FirstOrDefault(l=>l.name=="CombatFootwork");
        if(footwork==null)
        {
            footwork=new AnimatorControllerLayer {name="CombatFootwork",stateMachine=new AnimatorStateMachine {name="CombatFootwork"}};
            AssetDatabase.AddObjectToAsset(footwork.stateMachine,controller); layers.Insert(layers.IndexOf(combat)+1,footwork);
        }
        footwork.avatarMask=feet; footwork.defaultWeight=0; footwork.syncedLayerIndex=-1; footwork.syncedLayerAffectsTiming=false;
        var footMachine=footwork.stateMachine;
        foreach(var t in footMachine.anyStateTransitions) footMachine.RemoveAnyStateTransition(t);
        foreach(var s in footMachine.states) footMachine.RemoveState(s.state);
        var footEmpty=footMachine.AddState("Guard"); footEmpty.motion=Clip("Guard");
        footEmpty.writeDefaultValues=false; footEmpty.iKOnFeet=true; footMachine.defaultState=footEmpty;
        for(int i=0;i<Attacks.Length;i++)
        {
            var state=footMachine.AddState(Attacks[i].name); state.motion=Clip(Attacks[i].name); state.writeDefaultValues=false; state.iKOnFeet=true;
            var enter=footMachine.AddAnyStateTransition(state); enter.hasExitTime=false; enter.hasFixedDuration=true; enter.duration=.12f; enter.canTransitionToSelf=false;
            enter.AddCondition(AnimatorConditionMode.If,0,ids[i].ToString()); enter.AddCondition(AnimatorConditionMode.If,0,"CombatStance");
            var exit=state.AddTransition(footEmpty); exit.hasExitTime=true; exit.exitTime=.92f; exit.hasFixedDuration=true; exit.duration=.16f;
        }
        controller.layers=layers.ToArray();
        foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(CharacterAnimationSetup.ControllerPath)) EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(feet);
        EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<UnarmedCombatItemData>(UnarmedCombatSetup.ItemPath));
        AssetDatabase.SaveAssets();
    }

    private static void Build(Selection selection, bool loop, bool fists)
    {
        var source = Source(selection.source);
        var result = UnityEngine.Object.Instantiate(source);
        result.name = selection.name;
        float duration = (selection.end-selection.start)/selection.speed;
        // Resample only offline. Runtime plays normal, compact Humanoid clips.
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            var sourceCurve = AnimationUtility.GetEditorCurve(source, binding);
            int samples = Mathf.CeilToInt(duration * 60);
            var keys = new Keyframe[samples+1];
            bool finger = binding.propertyName.StartsWith("LeftHand.") || binding.propertyName.StartsWith("RightHand.");
            for (int i=0; i<=samples; i++)
            {
                float t = duration*i/samples;
                float value = sourceCurve.Evaluate(selection.start+t*selection.speed);
                if (fists && finger) value = binding.propertyName.EndsWith("Spread") ? 0 : binding.propertyName.Contains("Thumb") ? -.3f : -.8f;
                if (loop)
                {
                    float seam = Mathf.SmoothStep(0,1,Mathf.InverseLerp(duration-.22f,duration,t));
                    if (!finger) value = Mathf.Lerp(value,sourceCurve.Evaluate(selection.start),seam);
                }
                keys[i] = new Keyframe(t,value);
            }
            var curve = new AnimationCurve(keys);
            for (int i=0;i<keys.Length;i++) curve.SmoothTangents(i,0);
            AnimationUtility.SetEditorCurve(result,binding,curve);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(result);
        settings.startTime=0; settings.stopTime=duration; settings.loopTime=loop; settings.loopBlend=loop;
        settings.keepOriginalOrientation=true; settings.loopBlendOrientation=true;
        AnimationUtility.SetAnimationClipSettings(result,settings);
        result.EnsureQuaternionContinuity();
        AnimationUtility.SetAnimationEvents(result,selection.impact < 0 ? Array.Empty<AnimationEvent>() : new[] {
            new AnimationEvent { time=(selection.impact-selection.start)/selection.speed,
                functionName=nameof(CharacterAnimationEventRelay.OnCharacterAnimationEvent), intParameter=(int)CharacterAnimationEventId.MeleeImpact }
        });
        string path=Output+"/"+selection.name+".anim";
        var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null) AssetDatabase.CreateAsset(result,path);
        else { EditorUtility.CopySerialized(result,existing); UnityEngine.Object.DestroyImmediate(result); EditorUtility.SetDirty(existing); }
    }
}


