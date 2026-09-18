using System.Linq;
using System.Reflection;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[TestFixture, Category("Core")]
public sealed class PlayerFloatingPresentationTests
{
    private GameObject player, floor, ledge, marker, effectObject;
    private PlayerAnimation presentation;
    private ThirdPersonController movement;
    private BeamTransportController transport;
    private Vector3 origin;
    private float timeScale;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    private void Sample() { Physics.SyncTransforms(); Invoke(presentation, "UpdateFloating"); }

    [SetUp]
    public void Setup()
    {
        timeScale = Time.timeScale; Time.timeScale = 1;
        origin = new Vector3(15000, 1000, 15000);
        player = new GameObject("Floating presentation test"); player.transform.position = origin;
        player.AddComponent<StarterAssetsInputs>();
        var capsule = player.AddComponent<CharacterController>();
        capsule.height = 2; capsule.radius = .3f; capsule.center = Vector3.up;
        movement = player.AddComponent<ThirdPersonController>();
        transport = player.AddComponent<BeamTransportController>();
        presentation = player.AddComponent<PlayerAnimation>();
        Invoke(presentation, "Awake"); Invoke(presentation, "OnEnable");
        floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = origin + Vector3.down * .5f; floor.transform.localScale = new Vector3(30, 1, 30);
        ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ledge.transform.position = origin + new Vector3(4, 1, 0); ledge.transform.localScale = new Vector3(2, 2, 2);
        marker = new GameObject("Floating landing marker"); marker.transform.position = origin;
        effectObject = new GameObject("Floating test effect");
        Set(transport, "reusableVfx", effectObject.AddComponent<BeamTransportVFX>());
    }
    [TearDown]
    public void Cleanup()
    {
        transport.CancelTransport(); Set(transport, "reusableVfx", null);
        Invoke(presentation, "OnDisable");
        foreach (var go in new[] { player, floor, ledge, marker, effectObject }) Object.DestroyImmediate(go);
        Time.timeScale = timeScale;
    }

    [TestCase(false)] [TestCase(true)]
    public void StraightTransport_UsesEndpointBuffersInBothDirections(bool upward)
    {
        bool accepted = upward ? transport.TryDeparture(5, 2, null)
            : transport.TryArrival(marker.transform, effectObject.GetComponent<BeamTransportVFX>(), 5, 2);
        Assert.That(accepted, Is.True);
        Sample(); Assert.That(presentation.IsFloating, Is.False);
        Vector3 start = transport.PresentationStart, end = transport.PresentationDestination;
        bool sawFloating = false;
        for (int i = 0; i < 100; i++)
        {
            transport.Advance(.02f);
            Vector3 unchanged = player.transform.position; Sample();
            Assert.That(player.transform.position, Is.EqualTo(unchanged));
            float distance = Vector3.Distance(start, unchanged), remaining = Vector3.Distance(end, unchanged);
            Assert.That(presentation.IsFloating, Is.EqualTo(distance > .5f && remaining > .5f));
            sawFloating |= presentation.IsFloating;
        }
        Assert.That(sawFloating, Is.True);
        transport.CancelTransport(); Assert.That(presentation.IsFloating, Is.False);
    }

    [TestCase(.5f, .5f)] [TestCase(1f, .25f)]
    public void CurrentBezier_UnchangedPositions_FloatsThroughApexAndClearsOnDescent(float startBuffer, float endBuffer)
    {
        Set(presentation, "beamFloatStartClearance", startBuffer);
        Set(presentation, "beamFloatLandingClearance", endBuffer);
        var path = BeamHoistPath.Create(origin, origin + new Vector3(4, 2, 0), origin.y + 5, 1.5f, .9f);
        Assert.That(transport.TryHoist(path), Is.True);
        Sample(); Assert.That(presentation.IsFloating, Is.False);
        bool middle = false, ending = false;
        float elapsed = 0f, previousY = origin.y;
        for (int i = 0; i < 120; i++)
        {
            transport.Advance(.02f); elapsed += .02f;
            Vector3 expected = path.Evaluate(Mathf.Clamp01(elapsed / path.Duration));
            Assert.That(Vector3.Distance(player.transform.position, expected), Is.LessThan(.002f));
            Sample();
            if (Vector3.Distance(expected, origin) <= startBuffer) Assert.That(presentation.IsFloating, Is.False);
            if (expected.y > path.landing.y + endBuffer && Vector3.Distance(expected, origin) > startBuffer)
            { Assert.That(presentation.IsFloating, Is.True); middle = true; }
            if (elapsed / path.Duration > .5f && expected.y < previousY && expected.y <= path.landing.y + endBuffer)
            { Assert.That(presentation.IsFloating, Is.False); ending = true; }
            previousY = expected.y;
        }
        transport.Advance(.02f);
        Assert.That(presentation.IsFloating, Is.False);
        Assert.That(middle && ending, Is.True);
    }

    [TestCase("CancelTransport")] [TestCase("OnDisable")] [TestCase("OnDestroy")]
    public void TransportInterruption_ClearsSynchronously(string interruption)
    {
        Assert.That(transport.TryDeparture(5, 2, null), Is.True);
        transport.Advance(.7f); Sample(); Assert.That(presentation.IsFloating, Is.True);
        if (interruption == "OnDestroy") Set(transport, "reusableVfx", null);
        if (interruption == "CancelTransport") transport.CancelTransport(); else Invoke(transport, interruption);
        Assert.That(presentation.IsFloating, Is.False, "No later animation Update should be needed.");
    }

    [TestCase(0f, true, false)] [TestCase(1f, true, false)] [TestCase(3f, true, true)]
    [TestCase(1f, false, false)] [TestCase(3f, false, true)]
    public void Falling_UsesLastSupportedHeight_NotJumpApex(float drop, bool jumped, bool expected)
    {
        player.transform.position = origin + Vector3.up * drop;
        movement.Grounded = true; Sample();
        movement.Grounded = false;
        if (jumped)
        {
            player.transform.position += Vector3.up; Sample();
            Assert.That(presentation.IsFloating, Is.False, "Ascending jump must keep its normal presentation.");
        }
        player.transform.position += Vector3.down * .1f; Sample();
        Assert.That(presentation.IsFloating, Is.EqualTo(expected));
        Assert.That(movement.Grounded, Is.False);
        player.transform.position = origin; movement.Grounded = true; Sample();
        Assert.That(presentation.IsFloating, Is.False);
    }

    [Test]
    public void FallThreshold_IsConfigurable_AndDisableClearsPresentation()
    {
        Set(presentation, "minimumFallHeightForFloating", 4f);
        player.transform.position = origin + Vector3.up * 3;
        movement.Grounded = true; Sample(); movement.Grounded = false;
        player.transform.position += Vector3.down * .1f; Sample(); Assert.That(presentation.IsFloating, Is.False);
        Set(presentation, "minimumFallHeightForFloating", 2f);
        player.transform.position += Vector3.down * .1f; Sample(); Assert.That(presentation.IsFloating, Is.True);
        Invoke(presentation, "OnDisable"); Assert.That(presentation.IsFloating, Is.False);
    }

    [Test]
    public void InactiveFloatingLayer_PreservesExistingGrenadePoses()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<CharacterVisual>("Assets/Game/Prefabs/Player/Characters/Amy.prefab");
        var actor = Object.Instantiate(prefab);
        var baseline = Object.Instantiate(prefab);
        var baselineController = Object.Instantiate((AnimatorController)baseline.Animator.runtimeAnimatorController);
        baselineController.layers = baselineController.layers.Where(l => l.name != FloatingAnimationSetup.LayerName).ToArray();
        baseline.Animator.runtimeAnimatorController = baselineController;
        try
        {
            foreach (var visual in new[] { actor, baseline })
            {
                visual.Animator.fireEvents = false;
                visual.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                visual.Animator.Rebind(); visual.Animator.Update(0);
                visual.Animator.SetTrigger("ThrowGrenade");
            }
            for (int i = 0; i < 180; i++)
            {
                actor.Animator.Update(1f / 120); baseline.Animator.Update(1f / 120);
                foreach (var bone in new[] { HumanBodyBones.RightHand, HumanBodyBones.LeftHand, HumanBodyBones.LeftFoot })
                    Assert.That(Vector3.Distance(actor.Animator.GetBoneTransform(bone).position,
                        baseline.Animator.GetBoneTransform(bone).position), Is.LessThan(.001f), $"Floating off changed {bone} at sample {i}.");
            }
            var driver = new CharacterAnimatorDriver(actor.Animator, actor.AnimationActions);
            driver.SetFloating(true);
            actor.Animator.Update(.2f); baseline.Animator.Update(.2f);
            driver.SetFloating(false);
            actor.Animator.Update(.2f); baseline.Animator.Update(.2f);
            Assert.That(Vector3.Distance(actor.Animator.GetBoneTransform(HumanBodyBones.RightHand).position,
                baseline.Animator.GetBoneTransform(HumanBodyBones.RightHand).position), Is.LessThan(.001f), "Floating must not retain its pose after clearing.");
        }
        finally { Object.DestroyImmediate(actor.gameObject); Object.DestroyImmediate(baseline.gameObject); Object.DestroyImmediate(baselineController); }
    }

    [Test]
    public void ActualAmyAnimator_FloatingIsSwappableHumanoidLoop_AndReturnsToExistingLayers()
    {
        var visual = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterVisual>("Assets/Game/Prefabs/Player/Characters/Amy.prefab"));
        try
        {
            var animator = visual.Animator;
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            var layer = controller.layers.Single(l => l.name == FloatingAnimationSetup.LayerName);
            var clip = (AnimationClip)layer.stateMachine.states.Single(s => s.state.name == "Floating").state.motion;
            Assert.That(clip.humanMotion && clip.isLooping, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root), Is.False);
            animator.Rebind(); animator.Update(0);
            var driver = new CharacterAnimatorDriver(animator, visual.AnimationActions);
            driver.SetFloating(true); animator.Update(.01f); animator.Update(.2f);
            int index = animator.GetLayerIndex(FloatingAnimationSetup.LayerName);
            Assert.That(animator.GetCurrentAnimatorStateInfo(index).IsName(FloatingAnimationSetup.LayerName + ".Floating"), Is.True);
            driver.SetFloating(false); animator.Update(.01f); animator.Update(.2f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(index).IsName(FloatingAnimationSetup.LayerName + ".Empty"), Is.True);
        }
        finally { Object.DestroyImmediate(visual.gameObject); }
    }
}
