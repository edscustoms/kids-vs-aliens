using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class MountedPistolAimTests
{
    private PlayerAim aim;
    private PlayerBikeRider rider;
    private AlienBikeController bike;
    private Camera camera;
    private AimTarget target;
    private readonly Vector3 origin = new(4000, 0, 4000);
    private GameInputMode oldMode;

    [SetUp]
    public void Setup()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        oldMode = InputModeController.CurrentMode;
        typeof(InputModeController).GetProperty("CurrentMode").SetValue(null, GameInputMode.Desktop);
        var player = new GameObject("Mounted aim fixture");
        player.transform.position = origin;
        rider = player.AddComponent<PlayerBikeRider>();
        bike = new GameObject("Stable bike heading").AddComponent<AlienBikeController>();
        bike.transform.position = origin;
        typeof(PlayerBikeRider).GetProperty("Bike").SetValue(rider, bike);
        typeof(PlayerBikeRider).GetProperty("Phase").SetValue(rider, BikeRidePhase.Riding);
        aim = player.AddComponent<PlayerAim>();
        camera = new GameObject("Visibility camera").AddComponent<Camera>();
        camera.transform.position = origin + new Vector3(0, 30, -1);
        camera.transform.LookAt(origin);
        camera.orthographic = true; camera.orthographicSize = 20; camera.aspect = 1;
        Invoke(aim, "Awake");
        Set(aim, "mainCamera", camera);
        Set(aim, "autoAimRange", 25f);
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        target = body.AddComponent<AimTarget>();
        Invoke(target, "OnEnable"); // EditMode fixtures explicitly register their runtime-only lifecycle.
        PositionTarget(0);
    }

    [TearDown]
    public void Cleanup()
    {
        foreach (var item in Object.FindObjectsByType<AimTarget>(FindObjectsInactive.Include)) Invoke(item, "OnDisable");
        typeof(PlayerBikeRider).GetProperty("Phase").SetValue(rider, BikeRidePhase.OnFoot);
        typeof(InputModeController).GetProperty("CurrentMode").SetValue(null, oldMode);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [TestCase(0, true)]
    [TestCase(69, true)]
    [TestCase(-69, true)]
    [TestCase(71, false)]
    [TestCase(-71, false)]
    [TestCase(180, false)]
    public void CameraVisibleTargetsStillRequireTheBikeForwardCone(float angle, bool allowed)
    {
        PositionTarget(angle);
        var rotation = rider.transform.rotation;
        Invoke(aim, "Update");
        Assert.That(aim.CurrentTarget == target, Is.EqualTo(allowed));
        Assert.That(rider.transform.rotation, Is.EqualTo(rotation), "Aiming cannot steer Amy/bike");
    }

    [Test]
    public void StickyLockOutsideConeReleasesAndSelectsAnotherVisibleTarget()
    {
        PositionTarget(69); Invoke(aim, "Update");
        Assert.That(aim.CurrentTarget, Is.SameAs(target));
        PositionTarget(71);
        var second = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        second.transform.position = origin + new Vector3(0, 1, 10);
        var next = second.AddComponent<AimTarget>(); next.CacheBodyData(); Invoke(next, "OnEnable"); Physics.SyncTransforms();
        Invoke(aim, "Update");
        Assert.That(aim.CurrentTarget, Is.SameAs(next));
    }

    [Test]
    public void WorldWallBlocksLockButOccupiedHullDoesNot()
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = origin + new Vector3(0, 2, 5);
        wall.transform.localScale = new Vector3(4, 5, 1); Physics.SyncTransforms();
        Invoke(aim, "Update"); Assert.That(aim.CurrentTarget, Is.Null);
        wall.transform.SetParent(bike.transform, true);
        Invoke(aim, "Update"); Assert.That(aim.CurrentTarget, Is.SameAs(target));
        wall.transform.SetParent(null, true);
        Assert.That(aim.TryGetShotAimPoint(origin + Vector3.up, out var point), Is.True);
        Assert.That(aim.CurrentTarget, Is.Null, "Per-shot validation also rejects a newly blocked lock");
        Assert.That((point - origin - Vector3.up).normalized, Is.EqualTo(Vector3.forward));
    }

    [Test]
    public void NoLockUsesBikeHeadingAndRealMuzzleOriginOnDesktopAndMobile()
    {
        target.gameObject.SetActive(false);
        bike.transform.rotation = Quaternion.Euler(0, 47, 0);
        rider.transform.rotation = Quaternion.Euler(0, -90, 0);
        Vector3 muzzle = origin + new Vector3(.5f, 1.7f, .4f);
        foreach (var mode in new[] { GameInputMode.Desktop, GameInputMode.Mobile })
        {
            typeof(InputModeController).GetProperty("CurrentMode").SetValue(null, mode);
            Assert.That(aim.TryGetShotAimPoint(muzzle, out var point), Is.True);
            Assert.That(Vector3.Angle(point - muzzle, bike.transform.forward), Is.LessThan(.01f));
        }
    }

    [Test]
    public void MountedShotsReuseAuthoredAccuracySpread()
    {
        Set(aim, "mobileAimSettings", UnityEditor.AssetDatabase.LoadAssetAtPath<MobileAimSettings>(
            "Assets/Game/Settings/Combat/MobileAimSettings.asset"));
        Invoke(aim, "Update");
        var previousRandom = Random.state;
        try
        {
            Random.InitState(173);
            var origin = rider.MountedAimPoint;
            aim.TryGetShotAimPoint(origin, out var first);
            bool different = false;
            for (int i = 0; i < 30; i++)
            { aim.TryGetShotAimPoint(origin, out var point); different |= Vector3.Distance(first, point) > .05f; }
            Assert.That(different, Is.True, "Mounted fire does not bypass the existing accuracy zones");
        }
        finally { Random.state = previousRandom; }
    }

    private void PositionTarget(float angle)
    {
        target.transform.position = origin + Quaternion.Euler(0, angle, 0) * Vector3.forward * 10 + Vector3.up;
        target.CacheBodyData(); Physics.SyncTransforms();
    }
    private static void Set(object owner, string field, object value) => owner.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static void Invoke(object owner, string method) => owner.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
}
