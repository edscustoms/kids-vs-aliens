using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class SceneCrossoverPlayTests
{
    const string Key = "SceneCrossoverPlayTests";
    static void Setup()
    {
        SessionState.SetString(Key, Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY") ?? "");
        SessionState.SetInt(Key + "Camera", (int)GameplayCameraSettings.Mode);
        SessionState.SetBool(Key + "Complete", false);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", Path.GetFullPath("Logs/SceneCrossover/Saves-" + Guid.NewGuid().ToString("N")));
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
    }
    [UnityTest, Timeout(240000)]
    public IEnumerator WalkHandoffContinueMountAndRestart()
    {
        Setup(); yield return new EnterPlayMode();
        yield return ExerciseHandoff();
    }
    static IEnumerator ExerciseHandoff()
    {
        Application.runInBackground = true; GameplayCameraSettings.Mode = GameplayCameraMode.Action;
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady, 20, "Source ready");
        var sourceRun = ActiveRunController.Instance;
        var frames = new List<double>(); var watch = System.Diagnostics.Stopwatch.StartNew(); double last = 0;
        yield return Until(() => SceneCrossover.Pending != null && SceneCrossover.Pending.Ready, 45, "Destination prepared", () => {
            double now = watch.Elapsed.TotalMilliseconds; frames.Add(now - last); last = now;
            Assert.That(ActiveRunController.Instance, Is.SameAs(sourceRun)); Owners();
        });
        Assert.That(SceneCrossover.Pending.Destination.bikeChase.gameObject.activeInHierarchy, Is.False);
        yield return Until(() => !sourceRun.GetComponent<BeamTransportController>().IsTransporting
            && !sourceRun.GetComponent<GameplaySuspensionController>().IsSuspended, 15, "Opening arrival complete");
        var mission = Object.FindAnyObjectByType<ExcavatorRepairMission>();
        mission.RestoreRunState("{\"state\":4,\"cluePlayed\":true}");
        yield return EditorTestFrame.Next();
        PlaceForExit(sourceRun, new Vector3(31.425f, .08f, 46));
        sourceRun.GetComponent<PlayerHealth>().RestoreRunHealth(73, 17);
        sourceRun.GetComponent<PlayerInventory>().RestoreCapsules(23, 7);
        Assert.That(sourceRun.Save(), Is.True);
        Assert.That(RunSaveService.TryReadActive(out var before), Is.True);
        Assert.That(before.sceneName, Is.EqualTo("ConstructionSite"));
        Assert.That(before.world.Any(s => s.id == "bike-route-player-bike"), Is.False, "Preload cannot contaminate the source save");
        Assert.That(RunSaveService.Continue(), Is.True);
        yield return EditorTestFrame.Next();
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance != sourceRun && ActiveRunController.Instance.IsReady,
            30, "Continue source", foreground: false);
        Assert.That(Object.FindAnyObjectByType<InGameMenuController>().IsOpen, Is.True);
        Assert.That(ActiveRunController.Instance.gameObject.scene.name, Is.EqualTo("ConstructionSite"));
        Foreground();
        yield return Until(() => SceneCrossover.Pending != null && SceneCrossover.Pending.Ready, 45, "Continue preload");
        var run = ActiveRunController.Instance;
        var player = run.GetComponent<PlayerBikeRider>(); var camera = Camera.main;
        var input = run.GetComponent<StarterAssetsInputs>(); var skills = run.GetComponent<PlayerSkillState>();
        var events = Object.FindAnyObjectByType<EventSystem>();
        var inventory = run.GetComponent<PlayerInventory>();
        var itemIds = inventory.Items.Select(i => RunContentCatalog.Instance.Id(i)).ToArray();
        ProceduralUIReview.Capture("crossover-exit", 1280, 720, true);
        var points = SceneCrossoverAuthoring.SamplePath();
        points.Add(new Vector3(198, 6, 124)); points.Add(new Vector3(195.4f, 6, 154.9f));
        int waypoint = 0; bool crossed = false; double handoffPeak = 0, previousTime = 0;
        var driveWatch = System.Diagnostics.Stopwatch.StartNew();
        Vector3 previousPlayer = player.transform.position, previousCamera = camera.transform.position;
        yield return Until(() => waypoint >= points.Count, 110, "Walk authored corridor", () => {
            double now = driveWatch.Elapsed.TotalMilliseconds;
            if (player.transform.position.x > 170) handoffPeak = Math.Max(handoffPeak, now - previousTime);
            previousTime = now;
            Assert.That(ActiveRunController.Instance, Is.SameAs(run));
            Assert.That(Camera.main, Is.SameAs(camera));
            Assert.That(Object.FindAnyObjectByType<EventSystem>(), Is.SameAs(events));
            if (!crossed && run.gameObject.scene.name == "BikeRoute")
            {
                crossed = true;
                Assert.That(Vector3.Distance(previousPlayer, player.transform.position), Is.LessThan(1), "No player teleport");
                Assert.That(Vector3.Distance(previousCamera, camera.transform.position), Is.LessThan(2), "No camera snap");
                Assert.That(player.GetComponent<PlayerSkillState>(), Is.SameAs(skills));
                ProceduralUIReview.Capture("crossover-handoff", 1280, 720, true);
            }
            previousPlayer = player.transform.position; previousCamera = camera.transform.position;
            if (waypoint >= points.Count) return;
            var delta = Vector3.ProjectOnPlane(points[waypoint] - player.transform.position, Vector3.up);
            if (delta.magnitude < 1.25f) { waypoint++; return; }
            var direction = Quaternion.Inverse(Quaternion.Euler(0, camera.transform.eulerAngles.y, 0)) * delta.normalized;
            input.MoveInput(new Vector2(direction.x, direction.z)); input.SprintInput(true);
            Assert.That(player.transform.position.y, Is.GreaterThan(-1), "Supported corridor floor");
        });
        input.MoveInput(Vector2.zero); input.SprintInput(false);
        Assert.That(crossed, Is.True); Owners();
        yield return Until(() => !SceneManager.GetSceneByName("ConstructionSite").isLoaded, 10, "Source unloaded");
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(23)); Assert.That(inventory.ArmorCapsules, Is.EqualTo(7));
        Assert.That(inventory.Items.Select(i => RunContentCatalog.Instance.Id(i)), Is.EqualTo(itemIds));
        Assert.That(player.GetComponent<PlayerHealth>().CurrentHealth, Is.EqualTo(73));
        Assert.That(player.GetComponent<PlayerHealth>().CurrentArmor, Is.EqualTo(17));
        var chase = Object.FindAnyObjectByType<BikeRouteChaseDirector>();
        Assert.That(chase.Player, Is.SameAs(player)); Assert.That(chase.Wave, Is.Zero);
        Assert.That(chase.PlayerBike.maxSpeed, Is.EqualTo(40.6f).Within(.001));
        Assert.That(chase.PlayerBike.turboMaxSpeed, Is.EqualTo(70.1f).Within(.001));
        ProceduralUIReview.Capture("crossover-bike-arrival", 1280, 720, true);
        Assert.That(run.Save(), Is.True);
        Assert.That(RunSaveService.TryReadActive(out var after), Is.True);
        Assert.That(after.sceneName, Is.EqualTo("BikeRoute"));
        Assert.That(after.previousScenes.Single().sceneName, Is.EqualTo("ConstructionSite"));
        Assert.That(after.previousScenes.Single().world.Any(s => s.parts.Any(p => p.key == "excavator-repair")), Is.True);
        Assert.That(RunSaveService.Continue(), Is.True);
        yield return EditorTestFrame.Next();
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance != run && ActiveRunController.Instance.IsReady,
            25, "Continue destination", foreground: false);
        Assert.That(Object.FindAnyObjectByType<InGameMenuController>().IsOpen, Is.True);
        Foreground(); Owners();
        chase = Object.FindAnyObjectByType<BikeRouteChaseDirector>();
        Assert.That(chase.Wave, Is.Zero);
        Assert.That(chase.Player.GetComponent<PlayerInventory>().PlasmaCapsules, Is.EqualTo(23));
        yield return Until(() => chase.Player.NearbyBike == chase.PlayerBike, 5, "Native bike interaction");
        Assert.That(chase.Player.TryMount(chase.PlayerBike), Is.True);
        yield return Until(() => chase.Player.IsDriving && chase.Wave == 1, 7, "Existing mounting and wave startup");
        var mountedRun = ActiveRunController.Instance;
        Assert.That(mountedRun.RestartFromBeginning(), Is.True);
        yield return EditorTestFrame.Next();
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance != mountedRun && ActiveRunController.Instance.IsReady,
            25, "Hard Restart destination");
        Assert.That(ActiveRunController.Instance.gameObject.scene.name, Is.EqualTo("BikeRoute"));
        Assert.That(Object.FindAnyObjectByType<BikeRouteChaseDirector>().Wave, Is.Zero);
        Owners();
        Directory.CreateDirectory("Logs/SceneCrossover");
        File.WriteAllText("Logs/SceneCrossover/handoff-validation.txt", $"Walk, same rig/camera/input, source/destination Continue paused, native mount/wave 1, unload and destination Hard Restart passed.\nMaximum Editor preload/preparation interval: {frames.Max():F2} ms\nMaximum post-bend Editor interval (includes capture/unload): {handoffPeak:F2} ms\nNot device/GPU profiling.\n");
        SessionState.SetBool(Key + "Complete", true);
    }
    static void PlaceForExit(ActiveRunController run, Vector3 position)
    {
        var capsule = run.GetComponent<CharacterController>(); capsule.enabled = false;
        run.transform.position = position; capsule.enabled = true;
        run.GetComponent<ThirdPersonController>().RestoreRunVerticalVelocity(0); Physics.SyncTransforms();
    }
    static void Owners()
    {
        Assert.That(Object.FindObjectsByType<ActiveRunController>().Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<PlayerBikeRider>().Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<EventSystem>().Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<Camera>().Count(c => c.CompareTag("MainCamera")), Is.EqualTo(1));
    }
    static void Foreground()
    {
        ActiveRunController.Instance?.SendMessage("OnApplicationPause", false);
        ActiveRunController.Instance?.SendMessage("OnApplicationFocus", true);
        var menu = Object.FindAnyObjectByType<InGameMenuController>(); if (menu != null && menu.IsOpen) menu.ResumeGame();
    }
    static IEnumerator Until(Func<bool> predicate, float seconds, string reason, Action tick = null, bool foreground = true)
    {
        double end = EditorApplication.timeSinceStartup + seconds * 2;
        while (!predicate() && EditorApplication.timeSinceStartup < end)
        { if (foreground) Foreground(); tick?.Invoke(); yield return EditorTestFrame.Next(); }
        Assert.That(predicate(), Is.True, reason + " player=" + ActiveRunController.Instance?.transform.position + " error=" + ActiveRunController.Instance?.RestoreError);
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) { ActiveRunController.Instance?.PrepareToLeave(); yield return new ExitPlayMode(); }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", SessionState.GetString(Key, ""));
        GameplayCameraSettings.Mode = (GameplayCameraMode)SessionState.GetInt(Key + "Camera", 0); Time.timeScale = 1;
        if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Failed)
            Assert.That(SessionState.GetBool(Key + "Complete", false), Is.True);
    }
}
