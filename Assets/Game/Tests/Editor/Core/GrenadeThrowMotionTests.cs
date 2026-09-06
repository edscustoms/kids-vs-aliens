using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[TestFixture, Category("Core")]
public sealed class GrenadeThrowMotionTests
{
    [TestCase("Amy")]
    [TestCase("SportyGranny")]
    public void ThrowHasRaisedWindup_ForwardRelease_AndReturnsToMovingBasePose(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<CharacterVisual>(
            $"Assets/Game/Prefabs/Player/Characters/{name}.prefab");
        var actor = Object.Instantiate(prefab);
        var baseline = Object.Instantiate(prefab);
        try
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CharacterAnimationSetup.ThrowClipPath);
            float release = clip.events.Single(e => e.functionName == nameof(CharacterAnimationEventRelay.OnCharacterAnimationEvent)
                && e.intParameter == (int)CharacterAnimationEventId.GrenadeRelease).time;
            var directions = new[] { Vector2.zero, new Vector2(0, .5f), Vector2.up, Vector2.down,
                Vector2.left, Vector2.right, new Vector2(-1, 1).normalized, new Vector2(1, 1).normalized,
                new Vector2(-1, -1).normalized, new Vector2(1, -1).normalized };
            foreach (Vector2 direction in directions)
            {
                Prepare(actor.Animator, direction); Prepare(baseline.Animator, direction);
                float idleHeight = Bone(actor, HumanBodyBones.RightHand).y;
                float armLength = Vector3.Distance(Bone(actor, HumanBodyBones.RightUpperArm), Bone(actor, HumanBodyBones.RightLowerArm))
                    + Vector3.Distance(Bone(actor, HumanBodyBones.RightLowerArm), Bone(actor, HumanBodyBones.RightHand));
                actor.Animator.SetTrigger("ThrowGrenade"); actor.Animator.Update(0);
                float maxForward = float.MinValue, releaseForward = 0, raisedWindup = 0, elbowBack = 0;
                float maxFootDifference = 0;
                bool sampledRelease = false;
                for (float time = 0; time < 1.65f;)
                {
                    float dt = Mathf.Min(1f / 120, 1.65f - time);
                    if (!sampledRelease && time + dt >= release) dt = release - time;
                    actor.Animator.Update(dt); baseline.Animator.Update(dt); time += dt;
                    Vector3 hand = Bone(actor, HumanBodyBones.RightHand);
                    Vector3 shoulder = Bone(actor, HumanBodyBones.RightUpperArm);
                    if (time < release * .7f)
                    {
                        raisedWindup = Mathf.Max(raisedWindup, hand.y - idleHeight);
                        elbowBack = Mathf.Max(elbowBack, shoulder.z - Bone(actor, HumanBodyBones.RightLowerArm).z);
                    }
                    if (time < release + .25f) maxForward = Mathf.Max(maxForward, hand.z - shoulder.z);
                    if (!sampledRelease && time >= release - .00001f)
                    {
                        sampledRelease = true; releaseForward = hand.z - shoulder.z;
                    }
                    foreach (var foot in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
                        maxFootDifference = Mathf.Max(maxFootDifference, Vector3.Distance(Bone(actor, foot), Bone(baseline, foot)));
                }
                string context = $"{name}, Move {direction}";
                Assert.That(raisedWindup, Is.GreaterThan(armLength * .65f), "Visible preparation: " + context);
                Assert.That(elbowBack, Is.GreaterThan(armLength * .25f), "Elbow must cock behind shoulder: " + context);
                Assert.That(releaseForward, Is.GreaterThan(armLength * .85f), "Hand must reach forward: " + context);
                Assert.That(maxForward - releaseForward, Is.LessThan(armLength * .08f), "Marker must be near maximum forward reach: " + context);
                Assert.That(maxFootDifference, Is.LessThan(.025f), "Throw must retain underlying leg motion: " + context);
                Assert.That(Vector3.Distance(Bone(actor, HumanBodyBones.RightHand), Bone(baseline, HumanBodyBones.RightHand)),
                    Is.LessThan(.015f), "Recovery must return to the current locomotion pose: " + context);
            }
        }
        finally { Object.DestroyImmediate(actor.gameObject); Object.DestroyImmediate(baseline.gameObject); }
    }

    private static Vector3 Bone(CharacterVisual actor, HumanBodyBones bone) =>
        actor.transform.InverseTransformPoint(actor.Animator.GetBoneTransform(bone).position);

    private static void Prepare(Animator animator, Vector2 direction)
    {
        animator.fireEvents = false; animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind(); animator.SetInteger("WeaponStyle", 0);
        animator.SetFloat("MoveX", direction.x); animator.SetFloat("MoveY", direction.y);
        animator.Update(0);
        for (int i = 0; i < 30; i++) animator.Update(1f / 60);
    }
}
