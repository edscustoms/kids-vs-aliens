using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cinemachine;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BikeCameraTests
{
    private const string Key = "BikeCameraTests";
    internal const string IsolateFlybys = Key + "IsolateFlybys";
    private static void Completed() => SessionState.SetBool(Key + "Completed", true);
    private static PlayerBikeRider Rider => Object.FindAnyObjectByType<PlayerBikeRider>();
    private static AlienBikeController Bike => Object.FindAnyObjectByType<AlienBikeController>();
    private static GameplayCameraController Owner => Object.FindAnyObjectByType<GameplayCameraController>();
    private static CinemachineVirtualCamera Rig => Owner.GetComponent<CinemachineVirtualCamera>();

    [UnitySetUp] public IEnumerator Setup()
    {
        SessionState.SetString(Key, Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY") ?? "");
        SessionState.SetInt(Key + "Mode", (int)GameplayCameraSettings.Mode);
        SessionState.SetBool(IsolateFlybys, true);
        SessionState.SetBool(Key + "Completed", false);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", Path.GetFullPath("Logs/BikeCamera/Saves-" + Guid.NewGuid().ToString("N")));
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        yield return new EnterPlayMode();
        Application.runInBackground = true; Time.timeScale = 1;
        GameplayCameraSettings.Mode = GameplayCameraMode.Action;
        yield return Seconds(3);
        Rider.GetComponent<BeamTransportController>().CancelTransport();
        ActiveRunController.Instance.SendMessage("OnApplicationFocus", true);
        Object.FindAnyObjectByType<InGameMenuController>().ResumeGame();
        yield return Until(() => !Rider.GetComponent<GameplaySuspensionController>().IsSuspended, 8);
        Teleport(Bike.mountApproaches[0].approachPoint.position + Vector3.left * 1.1f + Vector3.up * .1f);
        yield return Seconds(.5f);
    }

    [UnityTest] public IEnumerator ActionRoundTrip() { yield return RoundTrip(GameplayCameraMode.Action); }
    [UnityTest] public IEnumerator TacticalRoundTrip() { yield return RoundTrip(GameplayCameraMode.Tactical); }
    [UnityTest] public IEnumerator IsometricRoundTripWithoutProjectionCut() { yield return RoundTrip(GameplayCameraMode.Isometric); }

    // Reuse the existing bike acceptance flows within the same guarded, flyby-isolated fixture.
    [UnityTest] public IEnumerator ExistingRideJumpTurboAndDismount()
    {
        yield return new AlienBikePlayTests().MountDriveJumpTurboCollisionDismountAndRepeatedUse();
        Completed();
    }
    [UnityTest] public IEnumerator ExistingAirborneContinueAndPause()
    {
        yield return new AlienBikePlayTests().PauseCancelsChargeAndContinueRestoresGroundedBikeAndOnFootPlayer();
        Completed();
    }
    [UnityTest] public IEnumerator ExistingInterruptionAndFenceCollision()
    {
        yield return new AlienBikePlayTests().MountInterruptionReleasesControlsAndExistingFenceStopsBike();
        Completed();
    }

    private static IEnumerator RoundTrip(GameplayCameraMode mode)
    {
        GameplayCameraSettings.Mode = mode; yield return Seconds(.3f);
        var body = Rig.GetCinemachineComponent<CinemachineTransposer>();
        var follow = Rig.Follow; var lookAt = Rig.LookAt; var lens = Rig.m_Lens;
        var offset = body.m_FollowOffset; var rotation = Rig.transform.localRotation;
        var binding = body.m_BindingMode; bool ortho = Camera.main.orthographic;
        Assert.That(ortho, Is.EqualTo(mode == GameplayCameraMode.Isometric), "Selected preset reached the output camera");
        Assert.That(Owner.BikeBlend, Is.Zero, "Proximity never starts camera transition");
        Capture(mode + "-on-foot");
        Assert.That(Rider.TryMount(Bike), Is.True);
        Assert.That(Rider.Phase, Is.EqualTo(BikeRidePhase.Approaching));
        yield return Seconds(.18f);
        Assert.That(Owner.BikeBlend, Is.InRange(.001f, .99f));
        Capture(mode + "-mid-mount");
        if (ortho)
        {
            var projection = Camera.main.projectionMatrix;
            Assert.That(projection.m33, Is.InRange(.001f, .999f), "Continuous orthographic/perspective projection");
            Assert.That(projection.m32, Is.LessThan(0));
        }
        yield return Until(() => Rider.IsDriving, 6);
        Assert.That(Owner.BikeBlend, Is.EqualTo(1).Within(.001), "Settled when seated");
        yield return Seconds(.2f); Capture(mode + "-riding");
        Assert.That(Camera.main.orthographic, Is.False);
        Assert.That(Camera.main.fieldOfView, Is.EqualTo(70).Within(.01));
        Vector3 offsetFromBike = Camera.main.transform.position - Bike.transform.position;
        Assert.That(Vector3.Dot(offsetFromBike, Bike.transform.forward), Is.LessThan(-4));
        Assert.That(offsetFromBike.y, Is.InRange(1.5f, 2.8f));
        Assert.That(GameplayCameraSettings.Mode, Is.EqualTo(mode));
        Assert.That(Rig.Follow, Is.SameAs(follow)); Assert.That(Rig.LookAt, Is.SameAs(lookAt));
        Assert.That(Rider.TryDismount(), Is.True);
        yield return Seconds(.22f);
        Assert.That(Owner.BikeBlend, Is.InRange(.01f, .99f));
        Capture(mode + "-mid-dismount");
        yield return Until(() => !Rider.IsBusy, 3);
        Assert.That(Owner.BikeBlend, Is.Zero, "On-foot camera restored with control");
        yield return Seconds(.2f); Capture(mode + "-restored");
        Assert.That(GameplayCameraSettings.Mode, Is.EqualTo(mode));
        Assert.That(Camera.main.orthographic, Is.EqualTo(ortho));
        Assert.That(body.m_FollowOffset, Is.EqualTo(offset)); Assert.That(body.m_BindingMode, Is.EqualTo(binding));
        Assert.That(Rig.m_Lens.FieldOfView, Is.EqualTo(lens.FieldOfView));
        Assert.That(Rig.m_Lens.ModeOverride, Is.EqualTo(lens.ModeOverride));
        Assert.That(Quaternion.Angle(Rig.transform.localRotation, rotation), Is.LessThan(.001));
        Assert.That(Camera.main.projectionMatrix.m33, Is.EqualTo(ortho ? 1 : 0).Within(.001));
        Completed();
    }

    [UnityTest] public IEnumerator RejectedAndAbortedMountNeverRetainBikeFraming()
    {
        GameplayCameraSettings.Mode = GameplayCameraMode.Isometric;
        Assert.That(Rider.TryMount(null), Is.False); yield return Seconds(.1f);
        yield return Until(() => Camera.main.orthographic, 1);
        Assert.That(Owner.BikeBlend, Is.Zero);
        Assert.That(Rider.TryMount(Bike), Is.True); yield return Seconds(.2f);
        float partial = Owner.BikeBlend; Assert.That(partial, Is.GreaterThan(0));
        Rider.AbortRide(); yield return Seconds(.1f);
        Assert.That(Owner.BikeBlend, Is.LessThan(partial));
        yield return Seconds(.65f);
        Assert.That(Owner.BikeBlend, Is.Zero);
        Assert.That(Camera.main.orthographic, Is.True, "After abort: rig=" + Rig.m_Lens.ModeOverride + " state=" + Rig.State.Lens.ModeOverride);
        Assert.That(GameplayCameraSettings.Mode, Is.EqualTo(GameplayCameraMode.Isometric));
        Completed();
    }

    [UnityTest] public IEnumerator DisableLeaseLossDeathAndCameraReenableCleanUp()
    {
        for (int scenario = 0; scenario < 4; scenario++)
        {
            Teleport(Bike.mountApproaches[0].approachPoint.position + Vector3.left * .8f + Vector3.up * .1f);
            yield return Seconds(.3f); Assert.That(Rider.TryMount(Bike), Is.True);
            yield return Until(() => Rider.IsDriving, 6);
            switch (scenario)
            {
                case 0: Bike.enabled = false; break;
                case 1: Rider.GetComponent<GameplaySuspensionController>().ReleaseAll(); break;
                case 2: Rider.enabled = false; break;
                case 3: Rider.GetComponent<PlayerHealth>().TakeDamage(10000); break;
            }
            yield return Until(() => !Rider.IsBusy && Owner.BikeBlend == 0, 3, true);
            yield return Until(() => Mathf.Abs(Camera.main.fieldOfView - Rig.m_Lens.FieldOfView) < .01f, 1, true);
            Assert.That(Camera.main.orthographic, Is.False);
            Bike.enabled = true; Rider.enabled = true;
        }
        Owner.enabled = false; Owner.enabled = true;
        yield return EditorTestFrame.Next(); Assert.That(Owner.BikeBlend, Is.Zero);
        Completed();
    }

    [UnityTest] public IEnumerator BikeYawAndExistingLookAreFollowedButCosmeticLeanIsNot()
    {
        Assert.That(Rider.TryMount(Bike), Is.True); yield return Until(() => Rider.IsDriving, 6);
        Bike.Secure(); yield return Seconds(.3f);
        var feedback = Bike.GetComponent<AlienBikeVisualFeedback>(); feedback.enabled = false;
        var visual = feedback.bikeLeanPivot; var before = Rig.State.RawOrientation;
        visual.localRotation *= Quaternion.Euler(0, 0, 30);
        yield return Seconds(.3f);
        Assert.That(Quaternion.Angle(before, Rig.State.RawOrientation), Is.LessThan(.05));
        Bike.Body.rotation *= Quaternion.Euler(0, 55, 0); yield return Seconds(.8f);
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(Bike.transform.eulerAngles.y, Rig.State.RawOrientation.eulerAngles.y)), Is.LessThan(1),
            "root=" + Bike.transform.eulerAngles + " camera=" + Rig.State.RawOrientation.eulerAngles
            + " look=" + Rider.GetComponent<ThirdPersonController>().CameraLookAngles + " phase=" + Rider.Phase + " blend=" + Owner.BikeBlend);
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, Rig.State.RawOrientation.eulerAngles.z)), Is.LessThan(.05));
        var input = Rider.GetComponent<StarterAssetsInputs>();
        Object.FindAnyObjectByType<InputModeController>().SendMessage("ApplyMode", GameInputMode.Desktop);
        float heading = Rig.State.RawOrientation.eulerAngles.y;
        input.LookInput(new Vector2(3, 0)); yield return Seconds(.2f); input.LookInput(Vector2.zero); yield return Seconds(.4f);
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(heading, Rig.State.RawOrientation.eulerAngles.y)), Is.GreaterThan(.1));
        Completed();
    }

    [UnityTest] public IEnumerator ContinueFromBikeRestoresSelectedOnFootCameraPaused()
    {
        GameplayCameraSettings.Mode = GameplayCameraMode.Isometric;
        Assert.That(Rider.TryMount(Bike), Is.True); yield return Until(() => Rider.IsDriving, 6);
        var run = ActiveRunController.Instance;
        Assert.That(run.Save(), Is.True); run.PrepareToLeave();
        Assert.That(RunSaveService.Continue(), Is.True, RunSaveService.LastError);
        yield return EditorTestFrame.Next();
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady, 20, true);
        yield return EditorTestFrame.Next();
        Assert.That(Rider.IsBusy, Is.False); Assert.That(Owner.BikeBlend, Is.Zero);
        Assert.That(GameplayCameraSettings.Mode, Is.EqualTo(GameplayCameraMode.Isometric));
        Assert.That(Camera.main.orthographic, Is.True); Assert.That(Time.timeScale, Is.Zero);
        Completed();
    }

    [UnityTest] public IEnumerator MountedViewUsesExistingOcclusionNearSiteGeometry()
    {
        Assert.That(Rider.TryMount(Bike), Is.True); yield return Until(() => Rider.IsDriving, 6);
        Bike.Secure();
        var occlusion = CameraOcclusionController.Active;
        Assert.That(occlusion, Is.Not.Null);
        var colliders = Bike.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>()).ToArray();
        var renderers = occlusion.GetComponentsInChildren<Renderer>();
        foreach (string category in new[] { "fence", "exteriorwalls", "container", "office", "pipe" })
        {
            var obstacle = colliders.Where(c => !c.isTrigger && c.enabled && c.bounds.size.y > (category == "pipe" ? .05f : 1)
                && c.bounds.min.y < 3 && Hierarchy(c.transform).Contains(category))
                .OrderBy(c => (c.bounds.center - Bike.transform.position).sqrMagnitude).FirstOrDefault();
            Assert.That(obstacle, Is.Not.Null, category + " authored obstacle");
            var group = obstacle.transform;
            while (group.parent != null && !group.name.ToLowerInvariant().Contains(category)) group = group.parent;
            var groupRenderers = group.GetComponentsInChildren<Renderer>();
            var bounds = obstacle.bounds;
            foreach (var renderer in groupRenderers) bounds.Encapsulate(renderer.bounds);
            Vector3 direction = bounds.size.x < bounds.size.z ? Vector3.right : Vector3.forward;
            direction *= Mathf.Sign(Vector3.Dot(Bike.transform.position - bounds.center, direction));
            Vector3 point = bounds.center + direction * (Mathf.Abs(Vector3.Dot(bounds.extents, direction)) + 2.2f);
            point.y = Bike.transform.position.y;
            Bike.transform.SetPositionAndRotation(point, Quaternion.LookRotation(direction));
            Bike.Body.position = point; Bike.Body.rotation = Bike.transform.rotation;
            Physics.SyncTransforms(); yield return Seconds(1.2f);
            int faded = renderers.Count(r => occlusion.IsOccluded(r));
            Debug.Log("Bike camera near " + category + ": " + Hierarchy(obstacle.transform)
                + " bike=" + point + " camera=" + Camera.main.transform.position + " faded=" + faded);
            Capture("near-" + category);
            Assert.That(obstacle.enabled, Is.True, "Occlusion must preserve collision");
            Assert.That(Owner.BikeBlend, Is.EqualTo(1));
            if (category == "pipe" || category == "container")
                Assert.That(groupRenderers.Any(r => occlusion.IsOccluded(r)), Is.True,
                    "The tested foreground obstruction must fade enough to reveal the bike hull");
            if (category == "pipe")
            {
                Assert.That(obstacle.GetComponentInParent<CameraOcclusionGroup>(), Is.Not.Null);
                Assert.That(obstacle.transform.IsChildOf(occlusion.transform), Is.True, "Static pipe cache membership");
            }
            var samples = (Vector3[])typeof(CameraOcclusionController).GetField("amySamplePositions",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(occlusion);
            Assert.That(samples.All(p => Vector3.Distance(p, Rider.transform.position) < 2), Is.True,
                "Disabled riding capsule must not move visibility samples to world origin");
            if (Physics.Linecast(Camera.main.transform.position, Rider.transform.position + Vector3.up * .6f,
                out var hit, ~0, QueryTriggerInteraction.Ignore) && hit.transform.IsChildOf(group))
                Assert.That(groupRenderers.Any(r => occlusion.IsOccluded(r)), Is.True, "Existing fade responds behind " + category);
        }
        Completed();
    }

    private static string Hierarchy(Transform item)
    {
        string path = item.name;
        while ((item = item.parent) != null) path = item.name + "/" + path;
        return path.ToLowerInvariant();
    }

    private static void Teleport(Vector3 p)
    {
        var cc = Rider.GetComponent<CharacterController>(); cc.enabled = false; Rider.transform.position = p; cc.enabled = true;
        Rider.GetComponent<ThirdPersonController>().ResetMotion(); Physics.SyncTransforms();
    }
    private static void Capture(string name)
    {
        // This RenderTexture helper reprojects overlay UI with a standard lens.
        // Capture the full sequence in Action/Tactical; assert the hybrid lens directly.
        if (GameplayCameraSettings.Mode == GameplayCameraMode.Isometric && Owner.BikeBlend > 0 && Owner.BikeBlend < 1) return;
        ProceduralUIReview.Capture("bike-camera-" + name, 1280, 720);
    }
    private static IEnumerator Seconds(float duration)
    {
        yield return UntilTime(Time.time + duration);
    }
    private static IEnumerator UntilTime(float end)
    {
        double deadline = EditorApplication.timeSinceStartup + 30;
        while (Time.time < end && EditorApplication.timeSinceStartup < deadline)
        { Foreground(); yield return EditorTestFrame.Next(); }
        Assert.That(Time.time, Is.GreaterThanOrEqualTo(end));
    }
    private static IEnumerator Until(Func<bool> condition, float seconds, bool unscaled = false)
    {
        double deadline = EditorApplication.timeSinceStartup + seconds * 3 + 10;
        float end = (unscaled ? Time.unscaledTime : Time.time) + seconds;
        while (!condition() && (unscaled ? Time.unscaledTime : Time.time) < end && EditorApplication.timeSinceStartup < deadline)
        { if (!unscaled) Foreground(); yield return EditorTestFrame.Next(); }
        Assert.That(condition(), Is.True, "Camera condition timed out; phase=" + Rider?.Phase + " blend=" + Owner?.BikeBlend);
    }
    private static void Foreground()
    {
        if (Time.timeScale != 0) return;
        ActiveRunController.Instance?.SendMessage("OnApplicationPause", false);
        ActiveRunController.Instance?.SendMessage("OnApplicationFocus", true);
        var menu = Object.FindAnyObjectByType<InGameMenuController>(); if (menu != null && menu.IsOpen) menu.ResumeGame();
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        SessionState.EraseBool(IsolateFlybys);
        if (Application.isPlaying) { ActiveRunController.Instance?.PrepareToLeave(); yield return new ExitPlayMode(); }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", SessionState.GetString(Key, "")); SessionState.EraseString(Key);
        GameplayCameraSettings.Mode = (GameplayCameraMode)SessionState.GetInt(Key + "Mode", 0);
        SessionState.EraseInt(Key + "Mode"); Time.timeScale = 1;
        bool completed = SessionState.GetBool(Key + "Completed", false);
        SessionState.EraseBool(Key + "Completed");
        Assert.That(completed, Is.True, "Camera test body must finish; aborted Play Mode setup is not a pass");
    }
}

// Isolate unrelated atmospheric routes only in this fixture's transient processed scenes,
// including Continue reloads. No production scene/prefab bytes or clearance rules change.
// ConstructionSite currently has a Path_07/fence clearance failure before Play Mode starts.
public sealed class BikeCameraTestSceneProcessor : IProcessSceneWithReport
{
    public int callbackOrder => -1;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report != null || !SessionState.GetBool(BikeCameraTests.IsolateFlybys, false)) return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var flybys in root.GetComponentsInChildren<AlienFlybyController>(true))
                Object.DestroyImmediate(flybys.gameObject);
    }
}
