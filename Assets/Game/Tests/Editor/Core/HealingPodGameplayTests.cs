using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class HealingPodGameplayTests
{
    private const string Key = "HealingPodGameplayTests";
    private const string PrefabPath = "Assets/Game/Prefabs/Environment/PF_HealingPod.prefab";
    // Rebind runtime references after EnterPlayMode/Continue. Coroutine closures must
    // not retain the EditMode fixture's pre-domain-reload Unity object references.
    private static HealingPodController pod;
    private static PlayerCharacter player;
    private static PlayerHealth health;
    private static CharacterController capsule;
    private static GameplaySuspensionController suspension;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        SessionState.SetString(Key, Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY") ?? "");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", System.IO.Path.GetFullPath("Logs/HealingPodV1-" + Guid.NewGuid().ToString("N")));
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return Seconds(4);
        var arrival = Object.FindAnyObjectByType<AuthoredBeamArrival>();
        Assert.That(arrival.TryBegin(Object.FindAnyObjectByType<PlayerCharacter>()), Is.True);
        yield return Seconds(2f);
        Assert.That(arrival.HasArrived, Is.True);
        Bind();
        Assert.That(ActiveRunController.Instance.IsReady, Is.True);
        Assert.That(player.GetComponent<BeamTransportController>().IsTransporting, Is.False);
        health.RestoreRunHealth(40, 17);
    }

    private void Bind()
    {
        pod = Object.FindObjectsByType<HealingPodController>(FindObjectsInactive.Exclude).Single();
        player = Object.FindAnyObjectByType<PlayerCharacter>();
        health = player.GetComponent<PlayerHealth>();
        capsule = player.GetComponent<CharacterController>();
        suspension = player.GetComponent<GameplaySuspensionController>();
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
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", SessionState.GetString(Key, ""));
        SessionState.EraseString(Key);
        Time.timeScale = 1;
    }

    [UnityTest]
    public IEnumerator FullHealthShowsExistingToastWithoutClaimOrConsumptionThenDifferentMaxHealthCanHeal()
    {
        health.HealToFull();
        int reports = 0;
        player.GetComponent<PlayerFeedback>().Reported += feedback =>
        { if (feedback.Code == FeedbackCode.HealthAlreadyFull) reports++; };
        Teleport(Marker("PlayerExitPoint").position);
        yield return Until(() => pod.Openness == 1);
        Teleport(Marker("PlayerAlignPoint").position);
        yield return Seconds(.4f);
        Assert.That(pod.IsBusy, Is.False);
        Assert.That(suspension.IsSuspended, Is.False);
        Assert.That(pod.RemainingCapacity, Is.EqualTo(1));
        Assert.That(reports, Is.EqualTo(1));
        var toast = new SerializedObject(Object.FindAnyObjectByType<GameplayFeedbackPresenter>());
        Assert.That(((TMPro.TMP_Text)toast.FindProperty("message").objectReferenceValue).text, Is.EqualTo("HEALTH ALREADY FULL"));
        Assert.That(((CanvasGroup)toast.FindProperty("view").objectReferenceValue).alpha, Is.GreaterThan(.9));
        yield return Seconds(.7f);
        Assert.That(reports, Is.EqualTo(1), "Staying in the chamber does not spam feedback");
        var data = new SerializedObject(health);
        data.FindProperty("maxHealth").floatValue = 250;
        data.ApplyModifiedPropertiesWithoutUndo();
        health.RestoreRunHealth(100, 17);
        yield return Until(() => pod.IsBusy);
        yield return Until(() => !pod.IsBusy);
        Assert.That(health.CurrentHealth, Is.EqualTo(250));
        Assert.That(health.CurrentArmor, Is.EqualTo(17));
        Assert.That(pod.RemainingCapacity, Is.EqualTo(.4f).Within(.0001));
        Teleport(Marker("PlayerAlignPoint").position);
        yield return Seconds(.3f);
        Assert.That(pod.IsBusy, Is.False);
        Assert.That(reports, Is.EqualTo(2), "Returning full after walk-out is a fresh denied attempt");
        Assert.That(pod.RemainingCapacity, Is.EqualTo(.4f).Within(.0001));
    }

    [UnityTest]
    public IEnumerator PresentationDisableAndDeathRestoreOriginalAnimatorSpeedAndControl()
    {
        var animator = player.ActiveVisual.Animator;
        animator.speed = .8f;
        yield return ApproachAndEnter(false);
        yield return Until(() => pod.Phase == HealingPodPhase.Healing);
        Assert.That(animator.speed, Is.EqualTo(.8f * .45f).Within(.001));
        var animation = player.GetComponent<PlayerAnimation>();
        animation.enabled = false;
        Assert.That(animator.speed, Is.EqualTo(.8f));
        yield return Until(() => !pod.IsBusy);
        Assert.That(suspension.IsSuspended, Is.False);
        Assert.That(capsule.enabled, Is.True);
        animation.enabled = true;
        yield return ApproachAndEnter(false);
        yield return Until(() => pod.Phase == HealingPodPhase.Healing);
        health.TakeDamage(1000);
        Assert.That(pod.IsBusy, Is.False);
        Assert.That(animator.speed, Is.EqualTo(.8f));
        Assert.That(animation.IsFloating, Is.False);
        Assert.That(pod.RemainingCapacity, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator WalkInUsesCapacityFloatsWalksOutAndRejectsDepletedChamberOncePerVisit()
    {
        Assert.That(pod.Openness, Is.Zero);
        yield return ApproachAndEnter(true);
        Assert.That(suspension.OwnerCount, Is.EqualTo(1));
        Assert.That(player.GetComponent<StarterAssetsInputs>().GameplayInputBlocked, Is.True);
        Assert.That(player.GetComponent<ThirdPersonController>().enabled, Is.False);
        Assert.That(capsule.enabled, Is.False);
        Assert.That(pod.TryBeginHealing(player), Is.False, "Concurrent entry must not claim a second lease");
        int healingGrants = 0;
        health.OnHealthChanged += () => healingGrants++;
        float start = Time.time;
        var seen = new System.Collections.Generic.HashSet<HealingPodPhase>();
        bool capturedHealing = false;
        var data = new SerializedObject(pod);
        float expectedDuration = SequenceDuration(data);
        var animator = player.ActiveVisual.Animator;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        bool sawWalk = false;
        var vfx = (ParticleSystem)data.FindProperty("healingVfx").objectReferenceValue;
        // Batch EditMode pumps Play Mode without a continuously rendered Game view.
        // Keep this test's particles simulating until the explicit URP capture below.
        var particles = vfx.main;
        particles.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emitter = (AudioEmitter)data.FindProperty("healingEmitter").objectReferenceValue;
        while (pod.IsBusy && Time.time - start < expectedDuration + 2)
        {
            seen.Add(pod.Phase);
            if (pod.Phase == HealingPodPhase.Closing)
            {
                Assert.That(Vector3.Distance(player.transform.position, Marker("PlayerAlignPoint").position), Is.LessThan(.005));
                Assert.That(Quaternion.Angle(player.transform.rotation, Marker("PlayerAlignPoint").rotation), Is.LessThan(.1));
            }
            if (pod.Phase == HealingPodPhase.Healing)
            {
                if (!capturedHealing) { CaptureHealingView(); capturedHealing = true; }
                Assert.That(pod.Openness, Is.Zero);
                Assert.That(Vector3.Distance(player.transform.position, Marker("PlayerFloatPoint").position), Is.LessThan(.005));
                Assert.That(vfx.isPlaying, Is.True);
                Assert.That(vfx.particleCount, Is.GreaterThan(0));
                Assert.That(emitter.GetComponent<AudioSource>().isPlaying, Is.True);
                Assert.That(player.GetComponent<PlayerAnimation>().IsFloating, Is.True);
                Assert.That(animator.speed, Is.EqualTo(data.FindProperty("suspendedAnimationSpeed").floatValue).Within(.001));
                Assert.That(animator.GetCurrentAnimatorStateInfo(animator.GetLayerIndex(CharacterAnimatorDriver.FloatingLayerName))
                    .IsName("FloatingPresentation.Floating"), Is.True);
                Assert.That(pod.GetComponentInChildren<BeamEnergyField>().isActiveAndEnabled, Is.True);
            }
            if (pod.Phase == HealingPodPhase.Exiting)
            {
                Assert.That(pod.Openness, Is.EqualTo(1));
                Assert.That(capsule.enabled, Is.True);
                Assert.That(suspension.IsSuspended, Is.True);
                Assert.That(animator.speed, Is.EqualTo(1));
                Assert.That(player.GetComponent<PlayerAnimation>().IsFloating, Is.False);
                if (!sawWalk && animator.GetFloat("MoveY") > .5f && new Vector2(capsule.velocity.x, capsule.velocity.z).magnitude > .2f)
                { CaptureHealingView("walking"); sawWalk = true; }
            }
            yield return EditorTestFrame.Next();
        }
        Assert.That(pod.IsBusy, Is.False);
        Assert.That(Time.time - start, Is.LessThan(expectedDuration + 1f));
        Assert.That(sawWalk, Is.True, "Exit must physically walk and drive the existing locomotion blend");
        foreach (var phase in new[] {HealingPodPhase.Closing, HealingPodPhase.Rising, HealingPodPhase.Healing,
            HealingPodPhase.Lowering, HealingPodPhase.Opening, HealingPodPhase.Exiting}) Assert.That(seen, Does.Contain(phase));
        Assert.That(healingGrants, Is.EqualTo(1));
        Assert.That(health.HealthNormalized, Is.EqualTo(1));
        Assert.That(health.CurrentArmor, Is.EqualTo(17));
        Assert.That(pod.RemainingCapacity, Is.EqualTo(.4f).Within(.0001f));
        Assert.That(pod.IsDepleted, Is.False);
        Assert.That(suspension.IsSuspended, Is.False);
        Assert.That(capsule.enabled, Is.True);
        Assert.That(player.GetComponent<ThirdPersonController>().enabled, Is.True);
        Assert.That(player.GetComponent<StarterAssetsInputs>().GameplayInputBlocked, Is.False);
        Assert.That(Vector2.Distance(XZ(player.transform.position), XZ(Marker("PlayerExitPoint").position)), Is.LessThan(.02));
        Assert.That(vfx.isPlaying, Is.False);
        Assert.That(emitter.GetComponent<AudioSource>().isPlaying, Is.False);
        Assert.That(pod.TryBeginHealing(player), Is.False);
        Assert.That(animator.speed, Is.EqualTo(1));
        Assert.That(animator.GetLayerWeight(animator.GetLayerIndex(CharacterAnimatorDriver.FloatingLayerName)), Is.Zero);
        Assert.That(pod.GetComponentInChildren<BeamEnergyField>(true).gameObject.activeSelf, Is.False);

        health.RestoreRunHealth(20, 17);
        yield return ApproachAndEnter(false);
        yield return Until(() => !pod.IsBusy);
        Assert.That(health.CurrentHealth, Is.EqualTo(60).Within(.001));
        Assert.That(health.CurrentArmor, Is.EqualTo(17));
        Assert.That(pod.RemainingCapacity, Is.Zero);
        Assert.That(pod.IsDepleted, Is.True);
        Assert.That(pod.GetComponentsInChildren<Renderer>().Single(r => r.name == "HealPod_Chamber").enabled, Is.False);

        var denied = (SoundEvent)new SerializedObject(pod).FindProperty("deniedSound").objectReferenceValue;
        Teleport(pod.transform.position + pod.transform.forward * 5);
        yield return Seconds(data.FindProperty("openCloseDuration").floatValue + .2f);
        Assert.That(pod.Openness, Is.Zero, "An empty pod still closes");
        double before = SoundTime(denied);
        Teleport(Marker("PlayerExitPoint").position);
        yield return Seconds(.3f);
        Assert.That(SoundTime(denied), Is.EqualTo(before), "Proximity is not a denied chamber attempt");
        yield return Until(() => pod.Openness == 1);
        Teleport(Marker("PlayerAlignPoint").position);
        yield return Seconds(.3f);
        double first = SoundTime(denied);
        Assert.That(first, Is.GreaterThan(before));
        yield return Seconds(.7f);
        Assert.That(SoundTime(denied), Is.EqualTo(first), "Denied must not repeat during Stay");
        Teleport(Marker("PlayerAlignPoint").position);
        yield return Seconds(.3f);
        Assert.That(SoundTime(denied), Is.EqualTo(first), "Chamber entry during the same visit must not spam");
        Assert.That(pod.IsBusy, Is.False);
        Assert.That(healingGrants, Is.EqualTo(3), "Two grants plus the fixture's health reset");
        Teleport(pod.transform.position + pod.transform.forward * 5);
        yield return Seconds(.2f);
        Teleport(Marker("PlayerExitPoint").position);
        yield return Until(() => pod.Openness == 1);
        Teleport(Marker("PlayerAlignPoint").position);
        yield return Seconds(.3f);
        Assert.That(SoundTime(denied), Is.GreaterThan(first));
        Assert.That(ActiveRunController.Instance.Save(), Is.True);
        Assert.That(RunSaveService.ActiveStore.Read<ActiveRunSave>().world.Single(w => w.id == pod.GetComponent<RunWorldObject>().Id)
            .parts.Single(p => p.key == pod.RunStateKey).json, Does.Contain("\"remainingCapacity\":0"));
    }

    [UnityTest]
    public IEnumerator DisableBeforeAndAfterCommitReleasesOnlyItsLeaseAndKeepsTheCorrectUseState()
    {
        yield return ApproachAndEnter(false);
        yield return Until(() => pod.Phase == HealingPodPhase.Healing);
        var animator = player.ActiveVisual.Animator;
        Assert.That(animator.speed, Is.LessThan(1));
        var modal = suspension.Acquire(SuspensionReason.Modal);
        pod.enabled = false;
        Assert.That(pod.IsBusy, Is.False);
        Assert.That(pod.IsDepleted, Is.False);
        Assert.That(health.CurrentHealth, Is.EqualTo(40));
        Assert.That(pod.RemainingCapacity, Is.EqualTo(1));
        Assert.That(animator.speed, Is.EqualTo(1));
        Assert.That(player.GetComponent<PlayerAnimation>().IsFloating, Is.False);
        Assert.That(capsule.enabled, Is.True);
        Assert.That(suspension.OwnerCount, Is.EqualTo(1));
        Assert.That(Time.timeScale, Is.Zero);
        modal.Dispose();
        Assert.That(suspension.IsSuspended, Is.False);
        Assert.That(player.GetComponent<StarterAssetsInputs>().GameplayInputBlocked, Is.False);
        pod.enabled = true;
        yield return ApproachAndEnter(false);
        yield return Until(() => pod.Phase == HealingPodPhase.Lowering);
        pod.gameObject.SetActive(false);
        Assert.That(pod.IsBusy, Is.False);
        Assert.That(pod.RemainingCapacity, Is.EqualTo(.4f).Within(.0001));
        Assert.That(animator.speed, Is.EqualTo(1));
        Assert.That(suspension.IsSuspended, Is.False);
        Assert.That(capsule.enabled, Is.True);
        Assert.That(health.HealthNormalized, Is.EqualTo(1));
        Assert.That(health.CurrentArmor, Is.EqualTo(17));
        pod.gameObject.SetActive(true);
        yield return Seconds(.1f);
        Assert.That(pod.RemainingCapacity, Is.EqualTo(.4f).Within(.0001));
    }

    [UnityTest]
    public IEnumerator ContinueBeforeAndAfterCommitUsesSafeExitAndRestoresDepletionWithoutRewards()
    {
        yield return ApproachAndEnter(false);
        yield return Until(() => pod.Phase == HealingPodPhase.Closing);
        yield return SaveContinue(1, 40);
        yield return ApproachAndEnter(false);
        yield return Until(() => pod.Phase == HealingPodPhase.Lowering);
        // Register a second, initially inactive late spawn through the existing world/catalog contract.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var later = Object.Instantiate(prefab, pod.transform.position + Vector3.right * 20, Quaternion.identity);
        string spawnedId = RunWorldObject.TrackSpawn(later, prefab).Id;
        later.SetActive(false);
        yield return SaveContinue(.4f, 100);
        var restoredSpawn = ActiveRunController.Instance.FindWorldObject(spawnedId);
        Assert.That(restoredSpawn, Is.Not.Null);
        Assert.That(restoredSpawn.gameObject.activeSelf, Is.False);
        restoredSpawn.gameObject.SetActive(true);
        yield return Seconds(.1f);
        Assert.That(restoredSpawn.GetComponent<HealingPodController>().IsDepleted, Is.False);
        string json = pod.CaptureRunState();
        health.RestoreRunHealth(31, 17);
        pod.RestoreRunState(json); pod.RestoreRunState(json);
        Assert.That(health.CurrentHealth, Is.EqualTo(31), "Restore must never replay healing");
        Assert.That(pod.RemainingCapacity, Is.EqualTo(.4f).Within(.0001));
        Assert.That(pod.IsBusy, Is.False);
        health.RestoreRunHealth(60, 17);
        yield return ApproachAndEnter(false);
        yield return Until(() => pod.Phase == HealingPodPhase.Lowering);
        restoredSpawn.gameObject.SetActive(false);
        yield return SaveContinue(0, 100);
    }

    private IEnumerator SaveContinue(float capacity, float hp)
    {
        var run = ActiveRunController.Instance;
        var exit = Marker("PlayerExitPoint");
        var position = exit.position;
        var rotation = exit.rotation;
        var livePosition = player.transform.position;
        Assert.That(run.Save(), Is.True);
        Assert.That(player.transform.position, Is.EqualTo(livePosition), "Saving must not move the live player");
        run.SendMessage("OnApplicationPause", true);
        Assert.That(suspension.IsWorldPaused, Is.True, "Backgrounding saves then acquires its own pause lease");
        var save = RunSaveService.ActiveStore.Read<ActiveRunSave>();
        Assert.That(Vector3.Distance(save.player.position, position), Is.LessThan(.001));
        Assert.That(Quaternion.Angle(save.player.rotation, rotation), Is.LessThan(.1));
        Assert.That(save.player.verticalVelocity, Is.Zero);
        Assert.That(save.player.health, Is.EqualTo(hp));
        run.PrepareToLeave();
        Assert.That(RunSaveService.Continue(), Is.True, RunSaveService.LastError);
        yield return EditorTestFrame.Next();
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady, false);
        // Peer-dependent arrival handoff runs after both world restore passes.
        yield return EditorTestFrame.Next();
        Bind();
        Assert.That(suspension.IsWorldPaused, Is.True);
        Assert.That(pod.RemainingCapacity, Is.EqualTo(capacity).Within(.0001));
        Assert.That(pod.IsDepleted, Is.EqualTo(capacity == 0));
        Assert.That(pod.IsBusy, Is.False);
        Assert.That(Vector3.Distance(player.transform.position, position), Is.LessThan(.01));
        Assert.That(health.CurrentHealth, Is.EqualTo(hp));
        Assert.That(health.CurrentArmor, Is.EqualTo(17));
        Assert.That(capsule.enabled, Is.True);
        Assert.That(player.ActiveVisual.Animator.speed, Is.EqualTo(1));
        Assert.That(player.GetComponent<PlayerAnimation>().IsFloating, Is.False);
        var data = new SerializedObject(pod);
        Assert.That(SoundTime((SoundEvent)data.FindProperty("healingSound").objectReferenceValue), Is.Zero);
        Foreground();
    }

    private IEnumerator ApproachAndEnter(bool walk)
    {
        var approach = pod.transform.position + pod.transform.forward * 2.5f + Vector3.up * .36f;
        if (walk) yield return WalkTo(approach, false);
        else Teleport(approach);
        yield return Until(() => pod.Openness == 1);
        Assert.That(pod.IsBusy, Is.False, "Proximity alone must not heal");
        if (walk) yield return WalkTo(Marker("PlayerAlignPoint").position, true);
        else Teleport(Marker("PlayerAlignPoint").position + pod.transform.forward * .15f);
        yield return Until(() => pod.IsBusy);
    }

    private IEnumerator WalkTo(Vector3 destination, bool stopOnClaim)
    {
        double deadline = EditorApplication.timeSinceStartup + 10;
        while (EditorApplication.timeSinceStartup < deadline)
        {
            if (stopOnClaim && pod.IsBusy) yield break;
            Vector3 delta = destination - player.transform.position; delta.y = 0;
            if (delta.magnitude < .05f) yield break;
            capsule.Move(Vector3.ClampMagnitude(delta, 2 * Time.deltaTime));
            yield return EditorTestFrame.Next();
        }
        Assert.Fail("Physical approach was blocked before chamber entry.");
    }

    private void Teleport(Vector3 position)
    {
        capsule.enabled = false; player.transform.position = position; capsule.enabled = true;
        player.GetComponent<ThirdPersonController>().ResetMotion();
        Physics.SyncTransforms();
    }

    private Transform Marker(string name) => pod.transform.Find("PlayerMarkers/" + name);
    private static float SequenceDuration(SerializedObject data) =>
        new[] {"alignDuration", "riseDuration", "healDuration", "lowerDuration", "exitDuration"}
            .Sum(field => data.FindProperty(field).floatValue) + data.FindProperty("openCloseDuration").floatValue * 2;
    private static void CaptureHealingView(string label = "healing")
    {
        var target = new RenderTexture(960, 640, 24);
        var previous = RenderTexture.active;
        var image = new Texture2D(960, 640, TextureFormat.RGB24, false);
        try
        {
            target.Create();
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 960, 640), 0, 0); image.Apply();
            System.IO.Directory.CreateDirectory("Logs/HealingPodV1");
            System.IO.File.WriteAllBytes("Logs/HealingPodV1/" + label + ".png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target);
        }
    }
    private static Vector2 XZ(Vector3 value) => new(value.x, value.z);
    private static double SoundTime(SoundEvent sound)
    {
        Assert.That(sound, Is.Not.Null, "The serialized semantic sound must remain assigned");
        var states = (Array)typeof(AudioService).GetField("events", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(AudioService.Instance);
        foreach (var state in states)
            if ((SoundEvent)state.GetType().GetField("sound").GetValue(state) == sound)
                return (double)state.GetType().GetField("nextTime").GetValue(state);
        Assert.Fail("Semantic event is missing from AudioLibrary: " + sound.name);
        return 0;
    }

    private static IEnumerator Until(Func<bool> condition, bool resume = true, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        double deadline = EditorApplication.timeSinceStartup + 15;
        while (!condition() && EditorApplication.timeSinceStartup < deadline)
        { if (resume) Foreground(); yield return EditorTestFrame.Next(); }
        Assert.That(condition(), Is.True, $"Timed out waiting for pod/run phase at line {line}");
    }
    private static IEnumerator Seconds(float seconds)
    {
        float elapsed = 0;
        double deadline = EditorApplication.timeSinceStartup + seconds + 15;
        while (elapsed < seconds && EditorApplication.timeSinceStartup < deadline)
        { Foreground(); yield return EditorTestFrame.Next(); elapsed += Time.deltaTime; }
        Assert.That(elapsed, Is.GreaterThanOrEqualTo(seconds));
    }
    private static void Foreground()
    {
        if (Time.timeScale != 0) return;
        ActiveRunController.Instance?.SendMessage("OnApplicationPause", false);
        ActiveRunController.Instance?.SendMessage("OnApplicationFocus", true);
        var menu = Object.FindAnyObjectByType<InGameMenuController>();
        if (menu != null && menu.IsOpen) menu.ResumeGame();
    }
}
