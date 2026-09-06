using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

[TestFixture, Category("Core")]
public sealed class UnarmedCombatContentTests
{
    [TestCase("Amy")]
    [TestCase("SportyGranny")]
    public void SharedMappingsUseImportedMarkedActionsAndKeepLegsUnderLocomotion(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<CharacterVisual>($"Assets/Game/Prefabs/Player/Characters/{name}.prefab");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAnimationSetup.ControllerPath);
        Assert.That(prefab.Animator.runtimeAnimatorController, Is.SameAs(controller));
        Assert.That(prefab.AnimationActions, Is.SameAs(AssetDatabase.LoadAssetAtPath<CharacterAnimationActions>(CharacterAnimationSetup.ActionsPath)));
        Assert.That(prefab.Animator.GetComponent<CharacterAnimationEventRelay>(), Is.Not.Null);
        Assert.That(prefab.Animator.applyRootMotion, Is.False);
        var layer = controller.layers.Single(l => l.name == "UnarmedCombatActions");
        Assert.That(layer.stateMachine.defaultState.name, Is.EqualTo("Empty"));
        Assert.That(layer.stateMachine.defaultState.motion, Is.Null);
        Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root), Is.False);
        Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg), Is.False);
        Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg), Is.False);
        foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
            AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers }) Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(part), Is.True);
        Assert.That(System.Array.FindIndex(controller.layers, l => l.name == "GrenadeThrow"),
            Is.GreaterThan(System.Array.FindIndex(controller.layers, l => l.name == layer.name)));
        foreach (var action in new[] { CharacterActionId.MeleeLight1, CharacterActionId.MeleeLight2 })
        {
            Assert.That(prefab.AnimationActions.TryGetBinding(action, out var binding), Is.True);
            Assert.That(binding.layerName, Is.EqualTo(layer.name));
            Assert.That(layer.stateMachine.states.Single(s => layer.name + "." + s.state.name == binding.statePath).state.motion, Is.SameAs(binding.clip));
            Assert.That(binding.clip.humanMotion, Is.True);
            var markers = binding.clip.events.Where(e => e.functionName == nameof(CharacterAnimationEventRelay.OnCharacterAnimationEvent)
                && e.intParameter == (int)CharacterAnimationEventId.MeleeImpact).ToArray();
            Assert.That(markers.Length, Is.EqualTo(1));
            Assert.That(markers[0].time, Is.GreaterThan(0).And.LessThan(binding.clip.length * .8f));
            VerifyMovingPunch(prefab, action);
        }
    }

    private static void VerifyMovingPunch(CharacterVisual prefab, CharacterActionId action)
    {
        var actor = Object.Instantiate(prefab); var baseline = Object.Instantiate(prefab);
        try
        {
            foreach (var movement in new[] { Vector2.zero, new Vector2(0,.5f), Vector2.up, Vector2.left, Vector2.right,
                new Vector2(-1,1).normalized, new Vector2(1,1).normalized, Vector2.down })
            {
                foreach (var animator in new[] { actor.Animator, baseline.Animator })
                {
                    animator.fireEvents = false; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.Rebind(); animator.SetBool("CombatStance", true); animator.SetFloat("MoveX", movement.x); animator.SetFloat("MoveY", movement.y);
                    animator.Update(0); animator.Update(.5f);
                }
                var driver = new CharacterAnimatorDriver(actor.Animator, actor.AnimationActions);
                Assert.That(driver.TryPlayAction(action), Is.True);
                float biggestFootDifference = 0;
                for (int frame = 0; frame < 100; frame++)
                {
                    // Direction changes remain responsive in the middle of the upper-body action.
                    if (frame == 12 && movement != Vector2.zero)
                    {
                        foreach (var animator in new[] { actor.Animator, baseline.Animator })
                        { animator.SetFloat("MoveX", -movement.y); animator.SetFloat("MoveY", movement.x); }
                    }
                    actor.Animator.Update(1f/60); baseline.Animator.Update(1f/60);
                    foreach (var foot in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
                    {
                        Vector3 a = actor.transform.InverseTransformPoint(actor.Animator.GetBoneTransform(foot).position);
                        Vector3 b = baseline.transform.InverseTransformPoint(baseline.Animator.GetBoneTransform(foot).position);
                        biggestFootDifference = Mathf.Max(biggestFootDifference, Vector3.Distance(a,b));
                    }
                }
                Assert.That(biggestFootDifference, Is.LessThan(.035f), $"{prefab.name}: {action}, movement {movement} must retain base feet.");
            }
        }
        finally { Object.DestroyImmediate(actor.gameObject); Object.DestroyImmediate(baseline.gameObject); }
    }

    [Test]
    public void CombatLocomotionOnlyReplacesIdle_AndAnyStateConditionsAreExclusive()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAnimationSetup.ControllerPath);
        var machine = controller.layers[0].stateMachine;
        var normal = machine.states.Single(s => s.state.name == "UnarmedLocomotion").state;
        var combat = machine.states.Single(s => s.state.name == "UnarmedCombatLocomotion").state;
        var original = (BlendTree)normal.motion; var fighting = (BlendTree)combat.motion;
        Assert.That(fighting.blendType, Is.EqualTo(original.blendType));
        Assert.That(fighting.blendParameter, Is.EqualTo(original.blendParameter));
        Assert.That(fighting.blendParameterY, Is.EqualTo(original.blendParameterY));
        Assert.That(fighting.children.Length, Is.EqualTo(original.children.Length));
        for (int i = 0; i < original.children.Length; i++)
        {
            Assert.That(fighting.children[i].position, Is.EqualTo(original.children[i].position));
            Assert.That(fighting.children[i].timeScale, Is.EqualTo(original.children[i].timeScale));
            if (original.children[i].position != Vector2.zero) Assert.That(fighting.children[i].motion, Is.SameAs(original.children[i].motion));
            else
            {
                var idle = (AnimationClip)fighting.children[i].motion;
                Assert.That(idle.humanMotion, Is.True); Assert.That(idle.isLooping, Is.True);
            }
        }
        Assert.That(machine.anyStateTransitions.Single(t => t.destinationState == normal).conditions.Any(c => c.parameter == "CombatStance" && c.mode == AnimatorConditionMode.IfNot), Is.True);
        Assert.That(machine.anyStateTransitions.Single(t => t.destinationState == combat).conditions.Any(c => c.parameter == "CombatStance" && c.mode == AnimatorConditionMode.If), Is.True);
        foreach (string name in new[] { "PistolLocomotion", "RifleLocomotion" })
            Assert.That(machine.anyStateTransitions.Single(t => t.destinationState.name == name).conditions.Any(c => c.parameter == "CombatStance"), Is.False);
    }

    [Test]
    public void FightingBookAndTutorialUseTheSameGameplaySkillAndSemanticAction()
    {
        var item = AssetDatabase.LoadAssetAtPath<UnarmedCombatItemData>(UnarmedCombatSetup.ItemPath);
        var book = AssetDatabase.LoadAssetAtPath<KnowledgeBookItemData>(UnarmedCombatSetup.BookPath);
        Assert.That(book.grantedItem, Is.SameAs(item)); Assert.That(book.skill, Is.SameAs(item.requiredSkill));
        Assert.That(book.worldPrefab.GetComponent<PickupItem>(), Is.Not.Null);
        var pickup = new SerializedObject(book.worldPrefab.GetComponent<PickupItem>());
        Assert.That(pickup.FindProperty("item").objectReferenceValue, Is.SameAs(book));
        Assert.That(book.skill.TutorialData.action, Is.EqualTo(CharacterActionId.MeleeLight1));
        Assert.That(book.skill.TutorialData.combatStance, Is.True);
        Assert.That(book.skill.TutorialData.equipment, Is.Null);
    }
}
