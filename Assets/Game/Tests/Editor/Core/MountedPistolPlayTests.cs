using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class MountedPistolHitProbe : MonoBehaviour, IDamageable
{
    public int Hits { get; private set; }
    public void ReceiveDamage(HitInfo hit) => Hits++;
}

public sealed class MountedPistolPlayTests
{
    private const string Key = "MountedPistolPlayTests";
    private static PlayerBikeRider Rider => Object.FindAnyObjectByType<PlayerBikeRider>();
    private static AlienBikeController Bike => Object.FindAnyObjectByType<BikeRouteChaseDirector>().PlayerBike;
    private static StarterAssetsInputs Input => Rider.GetComponent<StarterAssetsInputs>();
    private static PlayerEquipment Equipment => Rider.GetComponent<PlayerEquipment>();
    private static PlayerShooter Shooter => Rider.GetComponent<PlayerShooter>();
    private static PlayerAnimation Animation => Rider.GetComponent<PlayerAnimation>();
    private static PlayerInventory Inventory => Rider.GetComponent<PlayerInventory>();
    private static WeaponItemData Pistol => AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/PlasmaPistolItem.asset");

    private static void Setup()
    {
        SessionState.SetString(Key, Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY") ?? "");
        SessionState.SetInt(Key + "Camera", (int)GameplayCameraSettings.Mode);
        SessionState.SetBool(Key + "Complete", false);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", Path.GetFullPath("Logs/MountedPistol/Saves-" + Guid.NewGuid().ToString("N")));
        EditorSceneManager.OpenScene(BikeRouteGrayboxTests.ScenePath);
    }

    private static IEnumerator Initialize()
    {
        Application.runInBackground = true; Time.timeScale = 1;
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady, 12);
        yield return Seconds(.4f);
        Object.FindAnyObjectByType<BikeRouteChaseDirector>().enabled = false; // Isolate mounted weapon tests from chase combat.
        if (Rider.GetComponent<PlayerSkillState>().UnlockSkill(Pistol.requiredSkill))
        {
            // Learning legitimately opens the existing modal; acknowledge it before testing riding.
            var knowledge = Object.FindAnyObjectByType<KnowledgeAcquiredPresenter>();
            yield return Until(() => knowledge.CurrentSkill == Pistol.requiredSkill, 3);
            knowledge.Close();
            yield return Seconds(.2f);
        }
        Inventory.EnsureOwnedWeapon(Pistol); Equipment.EquipWeapon(Pistol);
        Inventory.RestoreCapsules(12, 0);
        yield return Until(() => Rider.NearbyBike == Bike, 4);
        yield return Seconds(.2f);
        Assert.That(Equipment.EquippedWeapon, Is.SameAs(Pistol));
        Assert.That(Shooter.CurrentAmmo, Is.EqualTo(Pistol.magazineSize), "Fixture equips after scene initialization");
    }

    [UnityTest]
    public IEnumerator ForwardAndLockedShotsUseRealMuzzleAndSmoothRightArmOnly()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return VerifyForwardAndLockedShotsUseRealMuzzleAndSmoothRightArmOnly(); }

    private static IEnumerator VerifyForwardAndLockedShotsUseRealMuzzleAndSmoothRightArmOnly()
    {
        var state = Shooter.ActiveWeaponState;
        var instance = Equipment.EquippedWeaponInstance;
        yield return Mount();
        var visual = Rider.GetComponent<PlayerCharacter>().ActiveVisual;
        var left = visual.Animator.GetBoneTransform(HumanBodyBones.LeftHand);
        var right = visual.Animator.GetBoneTransform(HumanBodyBones.RightHand);
        Vector3 leftBefore = visual.transform.InverseTransformPoint(left.position);
        Vector3 rightBefore = visual.transform.InverseTransformPoint(right.position);
        Assert.That(Equipment.IsEquippedWeaponVisible, Is.False);
        var body = Target();
        Input.ShootInput(true);
        yield return Seconds(.06f);
        Assert.That(Animation.MountedAimWeight, Is.InRange(.001f, .999f), "Arm raises over time");
        yield return Until(() => body.Hits == 1, 2);
        Assert.That(Shooter.CurrentAmmo, Is.EqualTo(Pistol.magazineSize - 1));
        Assert.That(Shooter.ActiveWeaponState, Is.SameAs(state));
        Assert.That(Equipment.EquippedWeaponInstance, Is.SameAs(instance));
        Assert.That(Vector3.Distance(leftBefore, visual.transform.InverseTransformPoint(left.position)), Is.LessThan(.035f));
        Assert.That(Vector3.Distance(rightBefore, visual.transform.InverseTransformPoint(right.position)), Is.GreaterThan(.1f), "Right hand visibly leaves the steering grip");
        Assert.That(Vector3.Angle(instance.Muzzle.forward, Bike.transform.forward), Is.LessThan(8));
        ProceduralUIReview.Capture("mounted-pistol-forward", 1280, 720);
        Input.ShootInput(false);
        yield return Until(() => Animation.MountedAimWeight == 0, 2);
        Assert.That(Equipment.IsEquippedWeaponVisible, Is.False);
        Assert.That(Vector3.Distance(rightBefore, visual.transform.InverseTransformPoint(right.position)), Is.LessThan(.04f));
        // The same physical target can now participate in the existing registry/LOS lock.
        body.gameObject.AddComponent<AimTarget>().CacheBodyData();
        yield return Until(() => Rider.GetComponent<PlayerAim>().CurrentTarget != null, 2);
        Assert.That(Rider.GetComponent<PlayerAim>().CurrentTarget, Is.SameAs(body.GetComponent<AimTarget>()));
        yield return Tap();
        yield return Until(() => body.Hits == 2, 2);
        yield return Seconds(.5f);
        int before = Shooter.CurrentAmmo;
        Input.ShootInput(true); yield return Seconds(1);
        Assert.That(Shooter.CurrentAmmo, Is.EqualTo(before - 1), "Holding SemiAuto must not repeat");
        Input.ShootInput(false); yield return Seconds(.05f);
        for (int i = 0; i < 3; i++) yield return Tap();
        Assert.That(Shooter.CurrentAmmo, Is.EqualTo(before - 4), "Distinct rapid taps retain the same fire-rate gate");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = Bike.transform.position + Bike.transform.forward * 3 + Vector3.up;
        wall.transform.localScale = new Vector3(5, 5, .4f); Physics.SyncTransforms();
        yield return Until(() => Rider.GetComponent<PlayerAim>().CurrentTarget == null, 2);
        int hits = body.Hits;
        yield return Tap(); yield return Seconds(.5f);
        Assert.That(body.Hits, Is.EqualTo(hits), "Real wall stops a no-lock mounted shot");
        Object.Destroy(wall); Object.Destroy(body.gameObject);
        SessionState.SetBool(Key + "Complete", true);
    }

    [UnityTest]
    public IEnumerator MountPauseDismountAndUnsupportedWeaponsNeverFire()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return VerifyMountPauseDismountAndUnsupportedWeaponsNeverFire(); }

    private static IEnumerator VerifyMountPauseDismountAndUnsupportedWeaponsNeverFire()
    {
        var culling = Rider.GetComponent<PlayerCharacter>().ActiveVisual.Animator.cullingMode;
        int rounds = Shooter.CurrentAmmo;
        Assert.That(Rider.TryMount(Bike), Is.True);
        Input.ShootInput(true); yield return Seconds(.15f); Input.ShootInput(false);
        Assert.That(Shooter.CurrentAmmo, Is.EqualTo(rounds));
        yield return Until(() => Rider.IsDriving && Input.CanProcessBikeControls, 6);
        Input.ShootInput(true); yield return Seconds(.04f);
        var pause = Rider.GetComponent<GameplaySuspensionController>().Acquire(SuspensionReason.ManualPause);
        yield return EditorTestFrame.Next();
        Assert.That(Input.shoot, Is.False);
        Assert.That(Shooter.enabled, Is.False);
        pause.Dispose(); yield return Seconds(.5f);
        Assert.That(Shooter.CurrentAmmo, Is.EqualTo(rounds), "Canceled pre-fire raise cannot shoot on resume");
        Input.ShootInput(false);
        var rifle = AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/PlasmaRifleItem.asset");
        Inventory.EnsureOwnedWeapon(rifle); Equipment.EquipWeapon(rifle);
        int rifleRounds = Shooter.CurrentAmmo;
        yield return Tap();
        Assert.That(Shooter.CurrentAmmo, Is.EqualTo(rifleRounds));
        Assert.That(Equipment.IsEquippedWeaponVisible, Is.False);
        Equipment.EquipWeapon(Pistol); yield return Seconds(.2f);
        Assert.That(Rider.TryDismount(), Is.True);
        Input.ShootInput(true); yield return Seconds(.1f); Input.ShootInput(false);
        yield return Until(() => !Rider.IsBusy && Input.CanProcessGameplayInput, 4);
        Assert.That(Shooter.CurrentAmmo, Is.EqualTo(rounds));
        Assert.That(Equipment.IsEquippedWeaponVisible, Is.True, "Original on-foot presentation restores");
        Assert.That(Rider.GetComponent<PlayerCharacter>().ActiveVisual.Animator.cullingMode, Is.EqualTo(culling));
        SessionState.SetBool(Key + "Complete", true);
    }

    [UnityTest]
    public IEnumerator SharedReloadAndSaveContinueRestoreOnFootWithoutPendingShot()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return VerifySharedReloadAndSaveContinueRestoreOnFootWithoutPendingShot(); }

    private static IEnumerator VerifySharedReloadAndSaveContinueRestoreOnFootWithoutPendingShot()
    {
        var state = Shooter.ActiveWeaponState;
        state.Restore(2, Time.time);
        yield return Mount();
        yield return Tap(); yield return Tap();
        yield return Until(() => Shooter.IsReloading, 2);
        Assert.That(Inventory.PlasmaCapsules, Is.EqualTo(9), "Existing reload owner pays exactly once");
        Assert.That(Shooter.ActiveWeaponState, Is.SameAs(state));
        var run = ActiveRunController.Instance;
        Assert.That(run.Save(), Is.True);
        run.PrepareToLeave(); Assert.That(RunSaveService.Continue(), Is.True, RunSaveService.LastError);
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady && !Rider.IsBusy, 20, false);
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(Inventory.PlasmaCapsules, Is.EqualTo(9));
        Assert.That(Shooter.IsReloading, Is.True);
        Assert.That(Input.shoot, Is.False);
        Assert.That(Animation.MountedAimWeight, Is.Zero);
        Object.FindAnyObjectByType<InGameMenuController>().ResumeGame();
        yield return Until(() => !Shooter.IsReloading, 3);
        Assert.That(Shooter.CurrentAmmo, Is.EqualTo(Pistol.magazineSize));
        Assert.That(Inventory.PlasmaCapsules, Is.EqualTo(9));
        yield return Seconds(.5f);
        Assert.That(Shooter.CurrentAmmo, Is.EqualTo(Pistol.magazineSize), "No delayed trigger replay after Continue");
        SessionState.SetBool(Key + "Complete", true);
    }

    [UnityTest]
    public IEnumerator SteeringTurboAndChargedJumpContinueWhilePistolIsRaised()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return VerifySteeringTurboAndChargedJumpContinueWhilePistolIsRaised(); }

    private static IEnumerator VerifySteeringTurboAndChargedJumpContinueWhilePistolIsRaised()
    {
        yield return Mount();
        Input.ShootInput(true); Input.MoveInput(new Vector2(.08f, 1)); Input.SprintInput(true);
        yield return Seconds(.45f);
        Assert.That(Bike.Speed, Is.GreaterThan(2)); Assert.That(Bike.IsTurbo, Is.True);
        Assert.That(Animation.MountedAimWeight, Is.GreaterThan(.9f));
        Input.SprintInput(false); Input.MoveInput(Vector2.zero); Input.JumpInput(true);
        yield return Seconds(.8f); Input.JumpInput(false);
        yield return Seconds(.08f); Assert.That(Bike.Body.linearVelocity.y, Is.GreaterThan(2));
        Assert.That(Animation.MountedAimWeight, Is.GreaterThan(.9f));
        Assert.That(Shooter.CurrentAmmo, Is.EqualTo(Pistol.magazineSize - 1));
        Input.ShootInput(false);
        yield return Until(() => Bike.IsGrounded && Bike.Speed < .8f, 8);
        Assert.That(Rider.TryDismount(), Is.True); yield return Until(() => !Rider.IsBusy, 4);
        SessionState.SetBool(Key + "Complete", true);
    }

    [UnityTest]
    public IEnumerator SideTargetsKeepLeftHandDrivingAndMuzzleSafetyStopsClippedBarrel()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return VerifySideTargetsAndMuzzleSafety(); }

    private static IEnumerator VerifySideTargetsAndMuzzleSafety()
    {
        yield return Mount();
        var visual = Rider.GetComponent<PlayerCharacter>().ActiveVisual;
        var left = visual.Animator.GetBoneTransform(HumanBodyBones.LeftHand);
        var leftBefore = visual.transform.InverseTransformPoint(left.position);
        var heading = Bike.transform.rotation;
        var body = Target();
        body.transform.localScale = new Vector3(.8f, 1.6f, .8f);
        var target = body.gameObject.AddComponent<AimTarget>();
        var aim = Rider.GetComponent<PlayerAim>();
        foreach (float angle in new[] { -65f, 65f })
        {
            body.transform.position = Bike.transform.position + Quaternion.Euler(0, angle, 0) * Bike.transform.forward * 4 + Vector3.up * 1.2f;
            target.CacheBodyData(); Physics.SyncTransforms();
            yield return Until(() => aim.CurrentTarget == target, 2);
            Input.ShootInput(true); yield return Seconds(.3f);
            Assert.That(Animation.MountedAimWeight, Is.GreaterThan(.99f));
            var muzzle = Equipment.EquippedWeaponInstance.Muzzle;
            Assert.That(Vector3.Angle(muzzle.forward, aim.AimPoint - muzzle.position), Is.LessThan(12), "Actual pistol follows the side lock");
            Assert.That(Vector3.Distance(leftBefore, visual.transform.InverseTransformPoint(left.position)), Is.LessThan(.035f));
            Assert.That(Quaternion.Angle(heading, Bike.transform.rotation), Is.LessThan(.1f));
            CapturePose(angle < 0 ? "left" : "right");
            Input.ShootInput(false); yield return Seconds(.6f);
        }
        Object.Destroy(target);
        body.transform.position = Bike.transform.position + Bike.transform.forward * 8 + Vector3.up;
        body.transform.localScale = new Vector3(3, 4, .5f); Physics.SyncTransforms();
        yield return Seconds(.1f);
        Input.ShootInput(true); yield return Seconds(.65f);
        Input.ShootInput(false); yield return EditorTestFrame.Next();
        var pistolMuzzle = Equipment.EquippedWeaponInstance.Muzzle;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall between rider chest and raised muzzle";
        wall.transform.position = Vector3.Lerp(Rider.MountedAimPoint, pistolMuzzle.position, .5f);
        wall.transform.localScale = Vector3.one * .2f;
        foreach (var hull in Bike.GetComponentsInChildren<Collider>())
            Physics.IgnoreCollision(wall.GetComponent<Collider>(), hull); // Keep the stationary pose; ray obstruction remains authoritative.
        var wallHit = wall.AddComponent<MountedPistolHitProbe>(); Physics.SyncTransforms();
        Assert.That(wall.GetComponent<Collider>().Raycast(new Ray(pistolMuzzle.position, Rider.MountedForward), out _, 15), Is.False,
            "Wall is behind the muzzle; only the existing body-to-muzzle safety ray can stop this shot");
        int hits = body.Hits;
        Input.ShootInput(true);
        yield return Until(() => wallHit.Hits == 1, 2);
        Assert.That(body.Hits, Is.EqualTo(hits), "Clipped barrel cannot bypass the wall");
        Input.ShootInput(false);
        Object.Destroy(wall); Object.Destroy(body.gameObject);
        SessionState.SetBool(Key + "Complete", true);
    }

    private static void CapturePose(string name)
    {
        var camera = Camera.main;
        var position = camera.transform.position; var rotation = camera.transform.rotation;
        try
        {
            camera.transform.position = Bike.transform.position + Bike.transform.right * 3 - Bike.transform.forward * 2 + Vector3.up * 2;
            camera.transform.LookAt(Rider.MountedAimPoint);
            ProceduralUIReview.Capture("mounted-pistol-" + name, 1280, 720);
        }
        finally { camera.transform.SetPositionAndRotation(position, rotation); }
    }

    private static MountedPistolHitProbe Target()
    {
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Mounted shot physical target";
        body.transform.position = Bike.transform.position + Bike.transform.forward * 8 + Vector3.up;
        body.transform.localScale = new Vector3(3, 4, .5f); Physics.SyncTransforms();
        return body.AddComponent<MountedPistolHitProbe>();
    }
    private static IEnumerator Mount()
    {
        Assert.That(Rider.TryMount(Bike), Is.True);
        yield return Until(() => Rider.IsDriving && Input.CanProcessBikeControls, 6);
        yield return Seconds(.25f);
        Assert.That(Rider.GetComponent<ThirdPersonController>().enabled, Is.False);
        Assert.That(Inventory.enabled, Is.False);
    }
    private static IEnumerator Tap()
    { Input.ShootInput(true); yield return Seconds(.05f); Input.ShootInput(false); yield return Seconds(.55f); }
    private static IEnumerator Seconds(float seconds)
    { float end = Time.time + seconds; yield return Until(() => Time.time >= end, seconds + 2); }
    private static IEnumerator Until(Func<bool> condition, float seconds, bool foreground = true)
    {
        if (foreground) Foreground();
        double end = EditorApplication.timeSinceStartup + seconds * 3 + 10;
        float time = Time.unscaledTime + seconds;
        while (!condition() && Time.unscaledTime < time && EditorApplication.timeSinceStartup < end)
        {
            if (foreground) Foreground();
            yield return EditorTestFrame.Next();
        }
        Assert.That(condition(), Is.True, "Mounted pistol condition timed out; phase=" + Rider?.Phase
            + " timeScale=" + Time.timeScale + " owners=" + Rider?.GetComponent<GameplaySuspensionController>().OwnerCount
            + " player=" + Rider?.transform.position + " bike=" + Bike?.transform.position
            + " nearby=" + Rider?.NearbyBike + " ammo=" + Shooter?.CurrentAmmo);
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
        { ActiveRunController.Instance?.PrepareToLeave(); yield return new ExitPlayMode(); }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", SessionState.GetString(Key, ""));
        GameplayCameraSettings.Mode = (GameplayCameraMode)SessionState.GetInt(Key + "Camera", 0);
        Time.timeScale = 1;
        if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Failed)
            Assert.That(SessionState.GetBool(Key + "Complete", false), Is.True, "Test body must finish");
        SessionState.EraseString(Key); SessionState.EraseInt(Key + "Camera"); SessionState.EraseBool(Key + "Complete");
    }
}
