using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[Category("Core")]
public sealed class MobileAimPlacementTests
{
    private readonly List<GameObject> objects = new();
    private PlayerAim aim;
    private Camera camera;
    private AimTarget target;
    private GameInputMode previousMode;
    private Random.State previousRandom;
    private static readonly Vector3 Offset = new(1500, 0, 1500);

    [SetUp]
    public void Setup()
    {
        previousMode = InputModeController.CurrentMode;
        previousRandom = Random.state;
        typeof(InputModeController).GetProperty("CurrentMode").SetValue(null, GameInputMode.Mobile);
        var player = Own(new GameObject("Aim fixture"));
        player.transform.position = Offset;
        aim = player.AddComponent<PlayerAim>();
        aim.enabled = false;
        camera = Own(new GameObject("Aim camera")).AddComponent<Camera>();
        camera.enabled = false;
        camera.aspect = 1;
        camera.transform.position = Offset + new Vector3(0, 2, -4);
        var body = Own(GameObject.CreatePrimitive(PrimitiveType.Capsule));
        body.transform.position = Offset + new Vector3(0, 1, 10);
        target = body.AddComponent<AimTarget>();
        target.CacheBodyData();
        // Frame the lower sample exactly in the screen center: this must rank the
        // enemy favorably without dragging the shot's spread center down its body.
        camera.transform.LookAt(target.BodyCenter - Vector3.up * target.BodyRadius * .35f);
        Set(aim, "mainCamera", camera);
        typeof(PlayerAim).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(aim, null);
        typeof(PlayerAim).GetProperty("CurrentTarget").SetValue(aim, target);
        Physics.SyncTransforms();
    }

    [TearDown]
    public void Cleanup()
    {
        for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
        objects.Clear();
        typeof(InputModeController).GetProperty("CurrentMode").SetValue(null, previousMode);
        Random.state = previousRandom;
    }

    [Test]
    public void LowerBodyNearScreenCenter_DoesNotPullAimBelowVisibleTorso()
    {
        Assert.That(Visible(out var point, out var score), Is.True);
        Assert.That(Vector3.Distance(point, target.BodyCenter + Vector3.up * target.BodyRadius * .6f), Is.LessThan(.001f));
        Assert.That(score, Is.LessThan(.00001f), "Target ranking retains its best visible screen score.");
    }

    [Test]
    public void LowCover_UsesExposedUpperBody()
    {
        Cover(new Vector3(0, .65f, 8), new Vector3(3, 1.3f, .2f));
        Assert.That(Visible(out var point, out _), Is.True);
        Assert.That(point.y, Is.GreaterThan(target.BodyCenter.y));
    }

    [TestCase("PF_Enemy_Melee_POC_V1")]
    public void AuthoredAlienBodyCache_SelectsVisibleBodyRatherThanFeet(string prefabName)
    {
        target.gameObject.SetActive(false);
        var alien = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Game/Prefabs/Enemies/" + prefabName + ".prefab")));
        alien.transform.position = Offset + Vector3.forward * 10;
        var animator = alien.GetComponentInChildren<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind(); animator.Update(0);
        target = alien.GetComponent<AimTarget>();
        target.CacheBodyData();
        camera.transform.LookAt(target.BodyCenter - Vector3.up * target.BodyRadius * .35f);
        Physics.SyncTransforms();
        var renderer = alien.GetComponentInChildren<SkinnedMeshRenderer>();
        Assert.That(Vector3.Distance(target.BodyCenter, renderer.bounds.center), Is.LessThan(.01f),
            "The cached body center must agree with the actual rendered body's bounds.");
        Assert.That(target.BodyCenter.y - alien.transform.position.y, Is.GreaterThan(.5f),
            "The authored renderer cache itself must not put body center at the feet.");
        Assert.That(Visible(out var point, out _), Is.True);
        Assert.That(point.y, Is.GreaterThanOrEqualTo(target.BodyCenter.y));
        TestContext.WriteLine($"{prefabName}: body center height={target.BodyCenter.y - alien.transform.position.y:F3}, radius={target.BodyRadius:F3}");
    }

    [Test]
    public void UpperBodyCovered_LowerVisibleSampleRemainsAvailable()
    {
        Cover(new Vector3(0, 2.05f, 8), new Vector3(3, 2, .2f));
        Assert.That(Visible(out var point, out _), Is.True);
        Assert.That(point.y, Is.LessThan(target.BodyCenter.y));
    }

    [TestCase(8f)]
    [TestCase(-2f)]
    public void FullCoverFromPlayerOrCamera_RejectsTarget(float z)
    {
        Cover(new Vector3(0, 2, z), new Vector3(3, 4, .2f));
        Assert.That(Visible(out _, out _), Is.False);
    }

    [Test]
    public void AuthoredSpread_RemainsVariableAndCanPhysicallyMiss()
    {
        Set(aim, "mobileAimSettings", AssetDatabase.LoadAssetAtPath<MobileAimSettings>(
            "Assets/Game/Settings/Combat/MobileAimSettings.asset"));
        Random.InitState(7321);
        var origin = Offset + Vector3.up * 1.4f;
        Assert.That(Visible(out var spreadCenter, out _), Is.True);
        Vector3 sum = Vector3.zero;
        int hits = 0, misses = 0;
        for (int i = 0; i < 512; i++)
        {
            Assert.That(aim.TryGetShotAimPoint(origin, out var point), Is.True);
            sum += point - spreadCenter;
            var direction = point - origin;
            if (Physics.Raycast(origin, direction.normalized, out var hit, 20)
                && target.OwnsCollider(hit.collider)) hits++;
            else misses++;
        }
        Assert.That(hits, Is.GreaterThan(0));
        Assert.That(misses, Is.GreaterThan(0), "Assistance must not guarantee all hits.");
        Assert.That(Mathf.Abs(sum.y / 512), Is.LessThan(.08f), "Spread stays centered on the visible upper body.");
    }

    private bool Visible(out Vector3 point, out float score)
    {
        object[] args = { target, Vector3.zero, 0f };
        bool found = (bool)typeof(PlayerAim).GetMethod("TryGetCameraAndPlayerVisiblePoint",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(aim, args);
        point = (Vector3)args[1]; score = (float)args[2];
        return found;
    }

    private void Cover(Vector3 position, Vector3 size)
    {
        var cover = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
        cover.transform.position = Offset + position;
        cover.transform.localScale = size;
        Physics.SyncTransforms();
    }

    private GameObject Own(GameObject value) { objects.Add(value); return value; }
    private static void Set(Object owner, string name, object value) => owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
}
