using System.Reflection;
using NUnit.Framework;
using StarterAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class LevelStartAuthoringTests
{
    private Scene scene;
    private GameObject player, root;
    private BeamTransportController transport;
    private PlayerBeamInSequence sequence;
    private BeamTransportVFX effect;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [SetUp] public void Setup()
    {
        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        player = Make("Player");
        player.transform.position = new Vector3(10000, 1000, 10000);
        player.AddComponent<StarterAssetsInputs>();
        var capsule = player.AddComponent<CharacterController>();
        capsule.center = Vector3.up; capsule.height = 2; capsule.radius = .3f;
        player.AddComponent<GameplaySuspensionController>();
        transport = player.AddComponent<BeamTransportController>();
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        SceneManager.MoveGameObjectToScene(floor, scene);
        floor.transform.position = player.transform.position + Vector3.down * .5f;
        floor.transform.localScale = new Vector3(100, 1, 100);
        root = Make("LevelStart");
        root.transform.position = player.transform.position;
        sequence = root.AddComponent<PlayerBeamInSequence>();
        effect = Make("Arrival VFX").AddComponent<BeamTransportVFX>();
        Set("player", player.transform); Set("transportVfx", effect);
        // Even a stale/renamed/moved old child cannot override the root anymore.
        var old = Make("BeamInSpawn"); old.transform.SetParent(root.transform, false);
        old.transform.localPosition = new Vector3(12, 8, -9);
        old.transform.localRotation = Quaternion.Euler(0, 251, 0);
    }
    private GameObject Make(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
    private void Set(string field, object value) => typeof(PlayerBeamInSequence).GetField(field, Private).SetValue(sequence, value);
    private void Begin() => typeof(PlayerBeamInSequence).GetMethod("Awake", Private).Invoke(sequence, null);
    [TearDown] public void Cleanup() { if (transport != null) transport.CancelTransport(); if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true); }

    [TestCase(0, 0)] [TestCase(10, 97)] [TestCase(30, 271)]
    public void ArrivalUsesMovedRootAndYawIgnoringOldChild(float distance, float yaw)
    {
        root.transform.position += Vector3.right * distance;
        root.transform.rotation = Quaternion.Euler(12, yaw, 7);
        Begin();
        Assert.That(transport.IsTransporting, Is.True);
        Assert.That(effect.transform.position, Is.EqualTo(root.transform.position));
        transport.Advance(1); transport.Advance(3); transport.Advance(1); transport.Advance(.1f);
        Assert.That(transport.IsTransporting, Is.False);
        Assert.That(player.transform.position, Is.EqualTo(root.transform.position));
        Assert.That(Quaternion.Angle(player.transform.rotation, Quaternion.Euler(0, root.transform.eulerAngles.y, 0)), Is.LessThan(.001f));
        Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
        Assert.That(player.GetComponent<StarterAssetsInputs>().GameplayInputBlocked, Is.False);
    }

    [Test] public void DuplicateAuthoritiesRejectArrival()
    {
        Make("Duplicate LevelStart").AddComponent<PlayerBeamInSequence>();
        LogAssert.Expect(LogType.Error, "Multiple LevelStart arrival authorities. Remove the unintended duplicate before starting a fresh run.");
        Begin(); Assert.That(transport.IsTransporting, Is.False);
    }

    [Test] public void MissingAuthorityIsDiagnosed()
    {
        Object.DestroyImmediate(sequence);
        LogAssert.Expect(LogType.Error, $"Fresh start in '{scene.name}' requires exactly one active LevelStart with PlayerBeamInSequence; found 0. Run Tools > Setup > Setup or Repair Active Gameplay Scene. No fallback spawn is intended.");
        typeof(PlayerBeamInSequence).GetMethod("ValidateScene", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { scene, LoadSceneMode.Single });
    }
}
