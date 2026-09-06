using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[TestFixture, Category("Core")]
public sealed class CharacterAnimationSetupTests
{
    [TestCase("Amy")]
    [TestCase("SportyGranny")]
    public void StandardCharactersHaveSharedMappingAndRelayOnAnimator(string name)
    {
        var visual = AssetDatabase.LoadAssetAtPath<CharacterVisual>($"Assets/Game/Prefabs/Player/Characters/{name}.prefab");
        Assert.That(visual.Animator.GetComponent<CharacterAnimationEventRelay>(), Is.Not.Null);
        Assert.That(visual.AnimationActions.TryGetBinding(CharacterActionId.GrenadeThrow, out var binding), Is.True);
        var controller = (AnimatorController)visual.Animator.runtimeAnimatorController;
        var layer = controller.layers.Single(l => l.name == binding.layerName);
        var state = layer.stateMachine.states.Single(s => binding.statePath == layer.name + "." + s.state.name).state;
        Assert.That(state.motion, Is.SameAs(binding.clip));
        Assert.That(controller.parameters.Single(p => p.name == binding.triggerParameter).defaultBool, Is.False);
        Assert.That(controller.layers[0].stateMachine.states.SelectMany(s => s.state.transitions)
            .SelectMany(t => t.conditions).Any(c => c.parameter == binding.triggerParameter), Is.False,
            "The grenade trigger must not take over Base Layer locomotion.");
    }

    [Test]
    public void ReauthoringMarkerIsIdempotent_AndDoesNotChangeMotion()
    {
        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(CharacterAnimationSetup.ThrowClipPath);
        var clip = Object.Instantiate(source);
        try
        {
            var entry = nameof(CharacterAnimationEventRelay.OnCharacterAnimationEvent);
            var release = source.events.Single(e => e.functionName == entry);
            var curves = AnimationUtility.GetCurveBindings(clip);
            var keyValues = curves.Select(c => AnimationUtility.GetEditorCurve(clip, c).keys).ToArray();
            CharacterAnimationSetup.SetReleaseMarker(clip, release.time);
            CharacterAnimationSetup.SetReleaseMarker(clip, release.time);
            Assert.That(clip.events.Count(e => e.functionName == entry), Is.EqualTo(1));
            Assert.That(clip.events.Single(e => e.functionName == entry).intParameter,
                Is.EqualTo((int)CharacterAnimationEventId.GrenadeRelease));
            for (int i = 0; i < curves.Length; i++)
                Assert.That(AnimationUtility.GetEditorCurve(clip, curves[i]).keys, Is.EqualTo(keyValues[i]));
        }
        finally { Object.DestroyImmediate(clip); }
    }
}
