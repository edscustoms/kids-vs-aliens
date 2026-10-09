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
        // Open passage: existing ground, matching outer side fences, no closing wall.
        for (float z = 49.5f; z < 59; z += 1)
        {
            var center = new Vector3(31.425f,1,z);
            Assert.That(Physics.Raycast(center, Vector3.down, 1.8f, ~0, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(Physics.Raycast(center, Vector3.left, 5.3f, ~0, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(Physics.Raycast(center, Vector3.right, 5.3f, ~0, QueryTriggerInteraction.Ignore), Is.True);
        }
        Assert.That(Physics.Raycast(new Vector3(31.425f,1,58), Vector3.forward, 3f, ~0, QueryTriggerInteraction.Ignore), Is.False);
        var fences = GameObject.Find("Exit transport passage").GetComponentsInChildren<BoxCollider>();
        foreach (var fence in fences)
        {
            Assert.That(fence.GetComponent<Renderer>(), Is.Not.Null, "No invisible passage barriers");
            Assert.That(fence.bounds.size.x, Is.LessThan(.6f), "Imported fence thickness must remain natural");
            for (int j = 0; j < fences.Length; j++)
            {
                var other = fences[j]; if (other == fence) continue;
                bool overlaps = Physics.ComputePenetration(fence, fence.transform.position, fence.transform.rotation,
                    other, other.transform.position, other.transform.rotation, out _, out float depth);
                Assert.That(overlaps ? depth : 0, Is.LessThan(.002f), "Fence colliders must meet without overlap");
            }
        }
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
        ProceduralUIReview.Capture("gate-exit", 1280, 720, true);
        var gate = SceneCrossover.Pending;
        Assert.That(gate.Destination.stagedRoots.All(r => !r.activeInHierarchy), Is.True, "Preload cannot overlap the source environment");
        var gateWatch = System.Diagnostics.Stopwatch.StartNew();
        var phases = new HashSet<CrossoverPhase>();
        bool capturedOut = false, capturedLoading = false, capturedIn = false, capturedPassage = false;
        double fullyVisibleAt = -1, measuredLoadingHold = 0;
        bool verifiedHold = false, completedBarHeld = false;
        yield return Until(() => !SceneManager.GetSceneByName("ConstructionSite").isLoaded, 20, "Beam transport and unload", () => {
            Assert.That(ActiveRunController.Instance, Is.SameAs(run));
            Assert.That(Camera.main, Is.SameAs(camera));
            Assert.That(Object.FindAnyObjectByType<EventSystem>(), Is.SameAs(events));
            Assert.That(GameObject.Find("Gate transition fade"), Is.Null, "No legacy black overlay");
            if (gate != null)
            {
                phases.Add(gate.Phase);
                if (gate.Phase == CrossoverPhase.Idle && player.transform.position.z > 53 && !capturedPassage)
                { ProceduralUIReview.Capture("open-exit-passage",1280,720,true); capturedPassage = true; }
                var presentation = LoadingSceneController.Presentation;
                if (gate.Phase == CrossoverPhase.Loading && presentation.IsFullyVisible)
                {
                    if (fullyVisibleAt < 0) fullyVisibleAt = EditorApplication.timeSinceStartup;
                    if (presentation.DisplayedProgress == 1 && !completedBarHeld)
                    { completedBarHeld = true; ProceduralUIReview.Capture("loading-completed-hold",1280,720,true); }
                }
                if (gate.Phase == CrossoverPhase.Arriving && !presentation.IsFullyVisible && !verifiedHold)
                {
                    measuredLoadingHold = EditorApplication.timeSinceStartup - fullyVisibleAt;
                    Assert.That(measuredLoadingHold, Is.GreaterThanOrEqualTo(2.5));
                    Assert.That(presentation.DisplayedProgress, Is.EqualTo(1));
                    verifiedHold = true;
                }
                if (gate.Phase == CrossoverPhase.Departing || gate.Phase == CrossoverPhase.Arriving)
                    Assert.That(player.GetComponent<GameplaySuspensionController>().BlocksControls, Is.True);
                if (gate.Phase == CrossoverPhase.Departing && !capturedOut)
                { ProceduralUIReview.Capture("beam-out",1280,720,true); capturedOut = true; }
                if (gate.Phase == CrossoverPhase.Loading && !capturedLoading)
                {
                    Assert.That(LoadingSceneController.Presentation.IsVisible, Is.True);
                    Assert.That(LoadingSceneController.Presentation.DisplayedProgress, Is.InRange(0f,1f));
                    Assert.That(gate.Destination.bikeChase.gameObject.activeInHierarchy, Is.False);
                    ProceduralUIReview.Capture("beam-loading",1280,720,true); capturedLoading = true;
                }
                if (gate.Phase == CrossoverPhase.Arriving && !LoadingSceneController.Presentation.IsVisible && !capturedIn)
                {
                    Assert.That(gate.Destination.bikeChase.PlayerBike.gameObject.activeInHierarchy, Is.True);
                    Assert.That(gate.Destination.bikeChase.gameObject.activeInHierarchy, Is.False);
                    ProceduralUIReview.Capture("beam-in",1280,720,true); capturedIn = true;
                }
            }
            if (gate != null && !gate.Transitioning)
            {
                var delta = new Vector3(31.425f, 0, 58.8f) - player.transform.position; delta.y = 0;
                var direction = Quaternion.Inverse(Quaternion.Euler(0, camera.transform.eulerAngles.y, 0)) * delta.normalized;
                input.MoveInput(new Vector2(direction.x, direction.z)); input.SprintInput(false);
            }
        });
        double transitionSeconds = gateWatch.Elapsed.TotalSeconds;
        Assert.That(verifiedHold && completedBarHeld, Is.True, "Completed real bar held before fading");
        Assert.That(phases, Does.Contain(CrossoverPhase.Departing));
        Assert.That(phases, Does.Contain(CrossoverPhase.Loading));
        Assert.That(phases, Does.Contain(CrossoverPhase.Arriving));
        Assert.That(DialoguePlayer.Instance.CurrentLine.text, Is.EqualTo("Okay, now I need to get to school. Thanks for the lift."));
        Assert.That(player.GetComponent<GameplaySuspensionController>().BlocksControls, Is.False);
        ProceduralUIReview.Capture("beam-arrival-dialogue", 1280, 720, true);
        input.MoveInput(Vector2.zero);
        Assert.That(run.gameObject.scene.name, Is.EqualTo("BikeRoute")); Owners();
        Assert.That(player.GetComponent<PlayerSkillState>(), Is.SameAs(skills));
        Assert.That(player.GetComponent<StarterAssetsInputs>(), Is.SameAs(input));
        Assert.That(Vector3.Distance(player.transform.position, new Vector3(195.4f,6.08f,154.94f)), Is.LessThan(.5f));
        float observeUntil = Time.time + 4;
        yield return Until(() => Time.time >= observeUntil, 6, "Arrival survives defense warning interval");
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(23)); Assert.That(inventory.ArmorCapsules, Is.EqualTo(7));
        Assert.That(inventory.Items.Select(i => RunContentCatalog.Instance.Id(i)), Is.EqualTo(itemIds));
        Assert.That(player.GetComponent<PlayerHealth>().CurrentHealth, Is.EqualTo(73));
        Assert.That(player.GetComponent<PlayerHealth>().CurrentArmor, Is.EqualTo(17));
        var chase = Object.FindAnyObjectByType<BikeRouteChaseDirector>();
        Assert.That(chase.Player, Is.SameAs(player)); Assert.That(chase.Wave, Is.Zero);
        Assert.That(chase.PlayerBike.maxSpeed, Is.EqualTo(40.6f).Within(.001));
        Assert.That(chase.PlayerBike.turboMaxSpeed, Is.EqualTo(70.1f).Within(.001));
        Assert.That(chase.GetComponent<BikeRouteContainment>().OutsideRoute, Is.False);
        Assert.That(chase.GetComponent<BikeRouteContainment>().OutsideSeconds, Is.Zero);
        Assert.That(chase.Player.NearbyBike, Is.SameAs(chase.PlayerBike), "No walking needed after arrival");
        ProceduralUIReview.Capture("gate-bike-arrival", 1280, 720, true);
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
        var mountedInput = chase.Player.GetComponent<StarterAssetsInputs>();
        Vector3 rideStart = chase.PlayerBike.transform.position;
        float rideEnd = Time.time + 2;
        yield return Until(() => Time.time >= rideEnd, 5, "Representative native ride", () => mountedInput.MoveInput(Vector2.up));
        mountedInput.MoveInput(Vector2.zero);
        Assert.That(Vector3.Distance(rideStart, chase.PlayerBike.transform.position), Is.GreaterThan(5));
        ProceduralUIReview.Capture("gate-mounted-ride", 1280, 720, true);
        var mountedRun = ActiveRunController.Instance;
        Assert.That(mountedRun.RestartFromBeginning(), Is.True);
        yield return EditorTestFrame.Next();
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance != mountedRun && ActiveRunController.Instance.IsReady,
            25, "Hard Restart destination");
        Assert.That(ActiveRunController.Instance.gameObject.scene.name, Is.EqualTo("BikeRoute"));
        Assert.That(Object.FindAnyObjectByType<BikeRouteChaseDirector>().Wave, Is.Zero);
        Owners();
        Directory.CreateDirectory("Logs/SceneCrossover");
        File.WriteAllText("Logs/SceneCrossover/gate-validation.txt", $"Beam Out/loading/Beam In/dialogue, same rig/camera/input, source/destination Continue paused, native mount/ride/wave 1, unload and destination Hard Restart passed.\nGate approach + transition: {transitionSeconds:F2} seconds. Arrival requires zero walking.\nLoading fully visible before fading: {measuredLoadingHold:F3} seconds.\nMaximum Editor preload interval: {frames.Max():F2} ms (not device profiling).\n");
        SessionState.SetBool(Key + "Complete", true);
    }
    [UnityTest, Timeout(180000)]
    public IEnumerator InterruptedGateRestoresSourceAndHardRestart()
    {
        Setup(); yield return new EnterPlayMode();
        yield return ExerciseInterruption();
    }
    static IEnumerator ExerciseInterruption()
    {
        Application.runInBackground = true;
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady, 20, "New Game source ready");
        var run = ActiveRunController.Instance;
        yield return Until(() => SceneCrossover.Pending != null && SceneCrossover.Pending.Ready, 40, "Preload complete");
        yield return Until(() => !run.GetComponent<BeamTransportController>().IsTransporting
            && !run.GetComponent<GameplaySuspensionController>().IsSuspended, 15, "Source arrival complete");
        var mission = Object.FindAnyObjectByType<ExcavatorRepairMission>();
        PlaceForExit(run, new Vector3(31.425f, .08f, 58.7f));
        yield return EditorTestFrame.Next();
        Assert.That(SceneCrossover.Pending.Transitioning, Is.False, "Unfinished finale cannot leave");
        PlaceForExit(run, new Vector3(31.425f, .08f, 56.8f));
        yield return EditorTestFrame.Next();
        mission.RestoreRunState("{\"state\":4,\"cluePlayed\":true}");
        run.GetComponent<PlayerInventory>().RestoreCapsules(19, 4);
        PlaceForExit(run, new Vector3(31.425f, .08f, 58.7f));
        yield return Until(() => SceneCrossover.Pending.Transitioning, 5, "Beam departure started");
        Vector3 recovery = SceneCrossover.Pending.transform.Find("Departure recovery").position;
        run.SendMessage("OnApplicationPause", true);
        Assert.That(RunSaveService.TryReadActive(out var suspended), Is.True);
        Assert.That(suspended.sceneName, Is.EqualTo("ConstructionSite"));
        Assert.That(suspended.player.plasmaCapsules, Is.EqualTo(19));
        Assert.That(Vector3.Distance(suspended.player.position, recovery), Is.LessThan(.01f), "Departure interruption saves the ground pad");
        Assert.That(RunSaveService.Continue(), Is.True);
        yield return EditorTestFrame.Next();
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance != run && ActiveRunController.Instance.IsReady,
            30, "Interrupted gate Continue", foreground: false);
        Owners(); run = ActiveRunController.Instance;
        Assert.That(run.gameObject.scene.name, Is.EqualTo("ConstructionSite"));
        Assert.That(run.GetComponent<PlayerInventory>().PlasmaCapsules, Is.EqualTo(19));
        Assert.That(Object.FindAnyObjectByType<ExcavatorRepairMission>().State, Is.EqualTo(ExcavatorRepairState.Completed));
        Assert.That(Object.FindAnyObjectByType<InGameMenuController>().IsOpen, Is.True);
        // Restart while the incoming scene may still be loading; no duplicate startup.
        Assert.That(run.RestartFromBeginning(), Is.True);
        yield return EditorTestFrame.Next();
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance != run && ActiveRunController.Instance.IsReady,
            30, "Source Hard Restart");
        Owners();
        Assert.That(ActiveRunController.Instance.gameObject.scene.name, Is.EqualTo("ConstructionSite"));
        Assert.That(Object.FindAnyObjectByType<ExcavatorRepairMission>().State, Is.Not.EqualTo(ExcavatorRepairState.Completed));
        SessionState.SetBool(Key + "Complete", true);
    }
    [UnityTest, Timeout(90000)]
    public IEnumerator UnavailableDestinationKeepsSourceSaveAndOwners()
    {
        Setup();
        var gate = Object.FindAnyObjectByType<SceneCrossover>();
        var serialized = new SerializedObject(gate);
        serialized.FindProperty("destinationScene").stringValue = "MissingGateDestination";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        yield return new EnterPlayMode();
        yield return ExerciseUnavailable();
    }
    static IEnumerator ExerciseUnavailable()
    {
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady, 20, "Source ready without destination");
        var gate = Object.FindAnyObjectByType<SceneCrossover>();
        yield return Until(() => gate.Failure != null, 5, "Unavailable destination reported");
        Assert.That(gate.Committed, Is.False); Assert.That(gate.Transitioning, Is.False);
        var run = ActiveRunController.Instance;
        Assert.That(run.Save(), Is.True);
        Assert.That(RunSaveService.TryReadActive(out var save), Is.True);
        Assert.That(save.sceneName, Is.EqualTo("ConstructionSite"));
        Assert.That(SceneManager.GetSceneByName("BikeRoute").isLoaded, Is.False);
        Owners(); SessionState.SetBool(Key + "Complete", true);
    }
    [UnityTest, Timeout(120000)]
    public IEnumerator InterruptedBeamInContinuesOnGroundBesideBike()
    {
        Setup(); yield return new EnterPlayMode();
        yield return ExerciseArrivalInterruption();
    }
    static IEnumerator ExerciseArrivalInterruption()
    {
        Application.runInBackground = true;
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady, 20, "Source ready");
        var run = ActiveRunController.Instance;
        yield return Until(() => SceneCrossover.Pending != null && SceneCrossover.Pending.Ready
            && !run.GetComponent<GameplaySuspensionController>().IsSuspended, 40, "Preload and original arrival");
        Object.FindAnyObjectByType<ExcavatorRepairMission>().RestoreRunState("{\"state\":4,\"cluePlayed\":true}");
        run.GetComponent<PlayerInventory>().RestoreCapsules(21, 6);
        PlaceForExit(run, new Vector3(31.425f,.08f,58.7f));
        yield return Until(() => SceneCrossover.Pending.Phase == CrossoverPhase.Arriving, 15, "Beam In starts");
        Assert.That(run.GetComponent<BeamTransportController>().IsTransporting, Is.True);
        run.SendMessage("OnApplicationPause", true);
        Assert.That(RunSaveService.TryReadActive(out var save), Is.True);
        Assert.That(save.sceneName, Is.EqualTo("BikeRoute"));
        Assert.That(Vector3.Distance(save.player.position, new Vector3(195.4f,6.08f,154.94f)), Is.LessThan(.01f));
        Assert.That(save.previousScenes.Single().sceneName, Is.EqualTo("ConstructionSite"));
        Assert.That(RunSaveService.Continue(), Is.True);
        yield return EditorTestFrame.Next();
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance != run && ActiveRunController.Instance.IsReady,
            25, "Continue interrupted Beam In", foreground: false);
        Assert.That(Object.FindAnyObjectByType<InGameMenuController>().IsOpen, Is.True);
        Foreground(); Owners();
        Assert.That(SceneManager.GetSceneByName("ConstructionSite").isLoaded, Is.False);
        Assert.That(SceneManager.GetSceneByName("Loading").isLoaded, Is.False);
        var chase = Object.FindAnyObjectByType<BikeRouteChaseDirector>();
        Assert.That(chase.Wave, Is.Zero);
        Assert.That(chase.Player.GetComponent<PlayerInventory>().PlasmaCapsules, Is.EqualTo(21));
        yield return Until(() => chase.Player.NearbyBike == chase.PlayerBike, 5, "Bike available after interrupted arrival");
        Assert.That(chase.GetComponent<BikeRouteContainment>().OutsideRoute, Is.False);
        Assert.That(chase.Player.GetComponent<GameplaySuspensionController>().BlocksControls, Is.False);
        SessionState.SetBool(Key + "Complete", true);
    }
    [UnityTest, Timeout(120000)]
    public IEnumerator InterruptedLoadingKeepsArtworkAndSourceRecovery()
    {
        Setup(); yield return new EnterPlayMode();
        yield return ExerciseLoadingInterruption();
    }
    static IEnumerator ExerciseLoadingInterruption()
    {
        Application.runInBackground = true;
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady, 20, "Source ready");
        var run = ActiveRunController.Instance;
        yield return Until(() => SceneCrossover.Pending != null && SceneCrossover.Pending.Ready
            && !run.GetComponent<GameplaySuspensionController>().IsSuspended, 40, "Preload and arrival");
        Object.FindAnyObjectByType<ExcavatorRepairMission>().RestoreRunState("{\"state\":4,\"cluePlayed\":true}");
        PlaceForExit(run, new Vector3(31.425f,.08f,58.7f));
        yield return Until(() => SceneCrossover.Pending.Phase == CrossoverPhase.Loading, 8, "Loading screen shown");
        Vector3 recovery = SceneCrossover.Pending.transform.Find("Departure recovery").position;
        run.SendMessage("OnApplicationPause", true);
        for (int i = 0; i < 3; i++) yield return EditorTestFrame.Next();
        Assert.That(LoadingSceneController.Presentation.IsVisible, Is.True, "Never uncover overlapping worlds on backgrounding");
        Assert.That(SceneCrossover.Pending.Committed, Is.False);
        Assert.That(RunSaveService.TryReadActive(out var save), Is.True);
        Assert.That(save.sceneName, Is.EqualTo("ConstructionSite"));
        Assert.That(Vector3.Distance(save.player.position, recovery), Is.LessThan(.01f));
        Assert.That(RunSaveService.Continue(), Is.True);
        yield return EditorTestFrame.Next();
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance != run && ActiveRunController.Instance.IsReady,
            25, "Continue interrupted loading", foreground: false);
        Owners();
        Assert.That(Object.FindAnyObjectByType<InGameMenuController>().IsOpen, Is.True);
        Assert.That(ActiveRunController.Instance.FindWorldObject("construction-exit-transport").GetComponent<GameplayTrigger>().HasFired, Is.False);
        SessionState.SetBool(Key + "Complete", true);
    }
    [UnityTest, Timeout(90000)]
    public IEnumerator OriginalGameStartLoadingStillOpensMenu()
    {
        Setup(); EditorSceneManager.OpenScene("Assets/Game/Scenes/Loading.unity");
        yield return new EnterPlayMode();
        yield return ExerciseOriginalLoading();
    }
    static IEnumerator ExerciseOriginalLoading()
    {
        Assert.That(LoadingSceneController.Presentation, Is.Null);
        var loader = Object.FindAnyObjectByType<LoadingSceneController>();
        Assert.That(loader, Is.Not.Null); Assert.That(loader.IsPresentationOnly, Is.False);
        yield return Until(() => SceneManager.GetActiveScene().name == "Menu", 20, "Original loading target", foreground: false);
        Assert.That(LoadingSceneController.Presentation, Is.Null);
        Assert.That(SceneManager.GetSceneByName("Loading").isLoaded, Is.False);
        Assert.That(Object.FindObjectsByType<EventSystem>().Length, Is.EqualTo(1));
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
        Assert.That(Object.FindObjectsByType<UnityEngine.InputSystem.PlayerInput>().Length, Is.EqualTo(1));
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
