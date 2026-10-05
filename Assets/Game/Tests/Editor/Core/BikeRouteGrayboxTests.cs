using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cinemachine;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BikeRouteGrayboxTests
{
    public const string ScenePath = "Assets/Game/Scenes/BikeRoute.unity";

    [Test]
    public void SceneRetainsRideFoundationAlongsideAuthoredChase()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        try
        {
            var components = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
            Assert.That(components.All(component => component != null), Is.True, "No missing scripts");
            Assert.That(components.OfType<PlayerCharacter>().Count(), Is.EqualTo(1));
            Assert.That(components.OfType<PlayerBikeRider>().Count(), Is.EqualTo(1));
            Assert.That(components.OfType<GameplayCameraController>().Count(), Is.EqualTo(1));
            Assert.That(components.OfType<GameplayInterface>().Count(), Is.EqualTo(1));
            Assert.That(components.OfType<Terrain>().Any(terrain => terrain.terrainData != null
                && terrain.GetComponent<TerrainCollider>()?.terrainData == terrain.terrainData), Is.True);
            Assert.That(components.OfType<BeamTransportController>(), Is.Empty);
            Assert.That(components.OfType<PlayerBeamInSequence>(), Is.Empty);
            Assert.That(components.OfType<MonoBehaviour>().Any(component => component.isActiveAndEnabled
                && (component is AuthoredEncounter
                    || component is GameplayTrigger || component is ExcavatorRepairMission)), Is.False,
                "No unrelated mission/encounter layer");
            Assert.That(components.OfType<ObjectiveController>().All(owner =>
                new SerializedObject(owner).FindProperty("openingObjective").objectReferenceValue == null), Is.True);
            var bike = components.OfType<BikeRouteChaseDirector>().Single().PlayerBike;
            Assert.That(bike.acceleration, Is.EqualTo(28));
            Assert.That(bike.maxSpeed, Is.EqualTo(40.6f));
            Assert.That(bike.reverseSpeed, Is.EqualTo(6));
            Assert.That(bike.turboAcceleration, Is.EqualTo(53));
            Assert.That(bike.turboMaxSpeed, Is.EqualTo(70.1f));
            Assert.That(bike.steeringStrength, Is.EqualTo(100));
            Assert.That(bike.steeringAtMaxSpeed, Is.EqualTo(.45f));
            Assert.That(bike.lateralGrip, Is.EqualTo(8));
            Assert.That(bike.hoverHeight, Is.EqualTo(.85f));
            Assert.That(bike.hoverSpring, Is.EqualTo(70));
            Assert.That(bike.hoverDamping, Is.EqualTo(14));
            Assert.That(bike.maxChargeTime, Is.EqualTo(1.4f));
            Assert.That(bike.minJump, Is.EqualTo(4));
            Assert.That(bike.maxJump, Is.EqualTo(9));
            Assert.That(bike.GetComponent<RunWorldObject>().Id, Is.Not.Null.And.Not.Empty);
            Assert.That(EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == ScenePath), Is.True,
                "Existing Continue and Hard Restart must be able to load the scene");
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}

// Startup smoke only. Route quality and complete-route acceptance require manual driving.
public sealed class BikeRouteGrayboxPlayTests
{
    private const string Key = "BikeRouteGrayboxPlayTests";
    private static PlayerBikeRider Rider => Object.FindAnyObjectByType<PlayerBikeRider>();
    private static AlienBikeController Bike => Object.FindAnyObjectByType<BikeRouteChaseDirector>().PlayerBike;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        SessionState.SetString(Key + "Saves", Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY") ?? "");
        SessionState.SetInt(Key + "Camera", (int)GameplayCameraSettings.Mode);
        SessionState.SetBool(Key + "Completed", false);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",
            Path.GetFullPath("Logs/BikeRoute/TestSaves-" + Guid.NewGuid().ToString("N")));
        EditorSceneManager.OpenScene(BikeRouteGrayboxTests.ScenePath);
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        Time.timeScale = 1;
        GameplayCameraSettings.Mode = GameplayCameraMode.Action;
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady, 10);
        Foreground();
        Object.FindAnyObjectByType<BikeRouteChaseDirector>().enabled = false; // Startup geometry smoke; chase has its own focused tests.
        yield return Until(() => Rider.NearbyBike == Bike, 4);
    }

    [UnityTest]
    public IEnumerator AuthoredStartMountsDrivesAndDismountsWithCurrentCamera()
    {
        Assert.That(Bike.IsWithinInteractionRange(Rider.transform.position), Is.True,
            "Use the authored start, without relocating the player or bike");
        ProceduralUIReview.Capture("bike-route-start", 1280, 720);
        Assert.That(Rider.TryMount(Bike), Is.True);
        yield return Until(() => Rider.IsDriving, 6);
        Assert.That(Object.FindAnyObjectByType<BikeHud>(), Is.Not.Null);
        var input = Rider.GetComponent<StarterAssetsInputs>();
        // Riding starts before the input owner's deliberate one-frame handoff block expires.
        yield return Until(() => input.CanProcessBikeControls, 4, "Mounted input handoff");
        ProceduralUIReview.Capture("bike-route-riding-start", 1280, 720);
        Foreground();
        yield return EditorTestFrame.Next();
        yield return Until(() => input.CanProcessBikeControls, 4, "Riding controls after capture/focus recovery");
        var start = Bike.transform.position;
        var heading = Bike.transform.forward;
        float peakSpeed = 0;
        input.MoveInput(Vector2.zero);
        input.MoveInput(Vector2.up);
        yield return Until(() =>
        {
            AssertCamera();
            peakSpeed = Mathf.Max(peakSpeed, Bike.Speed);
            return Vector3.Dot(Bike.transform.position - start, heading) >= 40;
        }, 8);
        Assert.That(peakSpeed, Is.GreaterThan(15), "Actual acceleration along the initial road");
        input.MoveInput(Vector2.zero);
        yield return Until(() => Bike.Speed < .8f && Bike.IsGrounded, 8);
        AssertCamera();
        Assert.That(Rider.TryDismount(), Is.True);
        yield return Until(() => !Rider.IsBusy, 4);
        yield return Until(() => Object.FindAnyObjectByType<GameplayCameraController>().BikeBlend == 0, 3);
        Assert.That(Rider.GetComponent<CharacterController>().enabled, Is.True);
        Assert.That(Rider.GetComponent<ThirdPersonController>().enabled, Is.True);
        Assert.That(GameplayCameraSettings.Mode, Is.EqualTo(GameplayCameraMode.Action));
        AssertCamera();
        SessionState.SetBool(Key + "Completed", true);
    }

    private static void AssertCamera()
    {
        var camera = Camera.main;
        Assert.That(camera, Is.Not.Null);
        var rig = Object.FindAnyObjectByType<GameplayCameraController>().GetComponent<CinemachineVirtualCamera>();
        var viewport = camera.WorldToViewportPoint(Rider.transform.position + Vector3.up * .7f);
        Assert.That(float.IsNaN(viewport.x) || float.IsInfinity(viewport.x)
            || float.IsNaN(viewport.y) || float.IsInfinity(viewport.y), Is.False);
        Assert.That(viewport.z, Is.GreaterThan(camera.nearClipPlane));
        Assert.That(viewport.x, Is.InRange(.02f, .98f));
        Assert.That(viewport.y, Is.InRange(.02f, .98f));
        Assert.That(rig.Follow, Is.Not.Null);
    }

    private static IEnumerator Until(Func<bool> condition, float seconds, string reason = "Startup condition")
    {
        double deadline = EditorApplication.timeSinceStartup + seconds * 3 + 10;
        float end = Time.unscaledTime + seconds;
        while (!condition() && Time.unscaledTime < end && EditorApplication.timeSinceStartup < deadline)
        {
            Foreground();
            yield return EditorTestFrame.Next();
        }
        var input = Rider != null ? Rider.GetComponent<StarterAssetsInputs>() : null;
        Assert.That(condition(), Is.True, reason + " timed out; phase=" + Rider?.Phase
            + " bikeControlsActive=" + input?.BikeControlsActive + " canProcessBikeControls=" + input?.CanProcessBikeControls
            + " blocksControls=" + Rider?.GetComponent<GameplaySuspensionController>().BlocksControls
            + " timeScale=" + Time.timeScale);
    }

    private static void Foreground()
    {
        ActiveRunController.Instance?.SendMessage("OnApplicationPause", false);
        ActiveRunController.Instance?.SendMessage("OnApplicationFocus", true);
        var menu = Object.FindAnyObjectByType<InGameMenuController>();
        if (menu != null && menu.IsOpen) menu.ResumeGame();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying)
        {
            ActiveRunController.Instance?.PrepareToLeave();
            yield return new ExitPlayMode();
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", SessionState.GetString(Key + "Saves", ""));
        GameplayCameraSettings.Mode = (GameplayCameraMode)SessionState.GetInt(Key + "Camera", 0);
        SessionState.EraseString(Key + "Saves");
        SessionState.EraseInt(Key + "Camera");
        Time.timeScale = 1;
        bool completed = SessionState.GetBool(Key + "Completed", false);
        SessionState.EraseBool(Key + "Completed");
        if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Failed)
            Assert.That(completed, Is.True, "The drive body must complete; aborted Play Mode is not a pass");
    }
}
