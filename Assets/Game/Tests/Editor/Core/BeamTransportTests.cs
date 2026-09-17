using System.Reflection;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[TestFixture, Category("Core")]
public sealed class BeamTransportTests
{
    private GameObject player, floor, ledge, targetObject, landingObject, vfxObject;
    private BeamTransportController transport;
    private BeamHoistTarget target;
    private GameplaySuspensionController suspension;
    private CharacterController capsule;
    private float scale;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);

    [SetUp]
    public void Setup()
    {
        scale = Time.timeScale;
        Time.timeScale = 1f;
        player = new GameObject("Beam test player");
        // Keep physics fixtures far from the open scene.
        player.transform.position = new Vector3(10000, 1000, 10000);
        player.AddComponent<StarterAssetsInputs>();
        player.AddComponent<PlayerSkillState>();
        capsule = player.AddComponent<CharacterController>();
        capsule.height = 2f; capsule.radius = 0.3f; capsule.center = Vector3.up;
        suspension = player.AddComponent<GameplaySuspensionController>();
        transport = player.AddComponent<BeamTransportController>();
        floor = Cube("Beam test floor", player.transform.position + Vector3.down * 0.5f, new Vector3(12, 1, 12));
        ledge = Cube("Beam test ledge", player.transform.position + new Vector3(3, 1, 0), new Vector3(2, 2, 2));
        targetObject = new GameObject("Beam test target");
        targetObject.transform.position = player.transform.position;
        target = targetObject.AddComponent<BeamHoistTarget>();
        landingObject = new GameObject("Beam test landing");
        landingObject.transform.position = player.transform.position + new Vector3(3, 2, 0);
        Set(target, "landing", landingObject.transform);
        Invoke(target, "OnEnable");
        vfxObject = new GameObject("Beam test VFX");
        var vfx = vfxObject.AddComponent<BeamTransportVFX>();
        Set(transport, "reusableVfx", vfx);
    }
    private static GameObject Cube(string name, Vector3 position, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.position = position; go.transform.localScale = size;
        return go;
    }
    [TearDown]
    public void Cleanup()
    {
        transport.CancelTransport();
        Set(transport, "reusableVfx", null);
        Invoke(target, "OnDisable");
        foreach (var go in new[] { player, floor, ledge, targetObject, landingObject, vfxObject }) Object.DestroyImmediate(go);
        Time.timeScale = scale;
    }

    [Test]
    public void Hoist_IsVerticalThenOneCurve_AndBeamStaysFixed()
    {
        Vector3 origin = player.transform.position;
        Assert.That(transport.TryHoist(target), Is.True);
        Assert.That(capsule.enabled, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        transport.Advance(target.LiftDuration / 2);
        Assert.That(player.transform.position.x, Is.EqualTo(origin.x));
        Assert.That(player.transform.position.z, Is.EqualTo(origin.z));
        transport.Advance(target.LiftDuration / 2);
        Assert.That(player.transform.position.y, Is.GreaterThan(target.Landing.position.y));
        transport.Advance(target.TransferDuration);
        Assert.That(player.transform.position.x, Is.GreaterThan(origin.x).And.LessThan(target.Landing.position.x));
        Assert.That(player.transform.position.y, Is.GreaterThan(target.Landing.position.y));
        transport.Advance(target.LandingDuration);
        transport.Advance(0.01f);
        Assert.That(player.transform.position, Is.EqualTo(target.Landing.position));
        Assert.That(vfxObject.transform.position, Is.EqualTo(origin));
        Assert.That(capsule.enabled, Is.True);
        Assert.That(suspension.IsSuspended, Is.False);
    }

    [Test]
    public void Arrival_OwnsPlayerSynchronously_RejectsDoubleStart()
    {
        landingObject.transform.position = player.transform.position;
        var vfx = vfxObject.GetComponent<BeamTransportVFX>();
        Assert.That(transport.TryArrival(landingObject.transform, vfx, 3.5f, 2f), Is.True);
        Assert.That(player.transform.position, Is.EqualTo(landingObject.transform.position + Vector3.up * 3.5f));
        Assert.That(player.GetComponent<StarterAssetsInputs>().GameplayInputBlocked, Is.True);
        Assert.That(transport.TryArrival(landingObject.transform, vfx, 3.5f, 2f), Is.False);
        transport.Advance(1f);
        Assert.That(player.transform.position.y, Is.LessThan(landingObject.transform.position.y + 3.5f));
        transport.Advance(1f);
        transport.Advance(0.01f);
        Assert.That(player.transform.position, Is.EqualTo(landingObject.transform.position));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NestedPauseAndTransport_ReleaseInEitherOrder(bool transportFirst)
    {
        var beam = suspension.Acquire(SuspensionReason.BeamTransport);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        var pause = suspension.Acquire(SuspensionReason.ManualPause);
        Assert.That(Time.timeScale, Is.Zero);
        if (transportFirst)
        {
            beam.Dispose();
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(player.GetComponent<StarterAssetsInputs>().GameplayInputBlocked, Is.True);
            pause.Dispose();
        }
        else
        {
            pause.Dispose();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(player.GetComponent<StarterAssetsInputs>().GameplayInputBlocked, Is.True);
            beam.Dispose();
        }
        Assert.That(suspension.IsSuspended, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    [Test]
    public void PauseFreezesTransport_AndDisableRestoresCapsule()
    {
        Assert.That(transport.TryHoist(target), Is.True);
        var pause = suspension.Acquire(SuspensionReason.ManualPause);
        Vector3 position = player.transform.position;
        transport.Advance(20f);
        Assert.That(player.transform.position, Is.EqualTo(position));
        Invoke(transport, "OnDisable");
        Assert.That(capsule.enabled, Is.True);
        Assert.That(suspension.OwnerCount, Is.EqualTo(1));
        pause.Dispose();
    }

    [Test]
    public void BlockedLandingOrCeiling_DoesNotAcquireControl()
    {
        var obstacle = Cube("Beam test ceiling", player.transform.position + Vector3.up * 3, new Vector3(2, 0.2f, 2));
        try
        {
            Assert.That(transport.TryHoist(target), Is.False);
            obstacle.transform.position = target.Landing.position + Vector3.up;
            Assert.That(transport.TryHoist(target), Is.False);
            Assert.That(suspension.IsSuspended, Is.False);
            Assert.That(capsule.enabled, Is.True);
        }
        finally { Object.DestroyImmediate(obstacle); }
    }

    [Test]
    public void BookUsesExistingInventoryUnlock_ThenHoistCanActivate()
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        Set(skill, "id", System.Guid.NewGuid().ToString());
        var book = ScriptableObject.CreateInstance<KnowledgeBookItemData>();
        book.skill = skill; book.itemType = ItemType.KnowledgeBook;
        var ability = player.AddComponent<BeamHoistAbility>();
        Set(ability, "requiredSkill", skill);
        Invoke(ability, "Awake");
        var inventory = player.AddComponent<PlayerInventory>();
        Invoke(inventory, "Awake");
        try
        {
            Assert.That(ability.TryActivate(), Is.False);
            Assert.That(inventory.TryAddItem(book), Is.True);
            inventory.UseItem(0);
            Assert.That(ability.IsUnlocked, Is.True);
            Assert.That(inventory.Items.Count, Is.Zero);
            Assert.That(ability.TryActivate(), Is.True);
        }
        finally { Object.DestroyImmediate(book); Object.DestroyImmediate(skill); }
    }

    [Test]
    public void ParticleDirection_IsIdempotentAndPreservesOtherMotionAndStyle()
    {
        var sparks = vfxObject.AddComponent<ParticleSystem>();
        var velocity = sparks.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = 0.1f; velocity.y = new ParticleSystem.MinMaxCurve(-3f, -1f); velocity.z = 0.6f;
        var main = sparks.main;
        main.startSize = 0.2f;
        var vfx = vfxObject.GetComponent<BeamTransportVFX>();
        Set(vfx, "beamSparks", sparks);
        vfx.SetDirection(BeamTransportDirection.Up);
        vfx.SetDirection(BeamTransportDirection.Up);
        Assert.That(velocity.y.constantMin, Is.EqualTo(1f));
        Assert.That(velocity.y.constantMax, Is.EqualTo(3f));
        vfx.SetDirection(BeamTransportDirection.Down);
        Assert.That(velocity.y.constantMin, Is.EqualTo(-3f));
        Assert.That(velocity.y.constantMax, Is.EqualTo(-1f));
        Assert.That(velocity.x.constant, Is.EqualTo(0.1f));
        Assert.That(velocity.z.constant, Is.EqualTo(0.6f));
        Assert.That(main.startSize.constant, Is.EqualTo(0.2f));
    }

    [TestCase("Assets/Game/Scenes/ConstructionSite.unity")]
    [TestCase("Assets/Game/Scenes/GamePoc.unity")]
    public void SceneRepair_IsIdempotent(string path)
    {
        var scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            PlayerCharacter character = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<PlayerCharacter>(true) is PlayerCharacter found) character = found;
            Assert.That(character, Is.Not.Null);
            BeamTransportSetup.ConfigureScene(character);
            int count = character.GetComponents<Component>().Length;
            BeamTransportSetup.ConfigureScene(character);
            Assert.That(character.GetComponents<Component>().Length, Is.EqualTo(count));
            Assert.That(character.GetComponents<BeamTransportController>().Length, Is.EqualTo(1));
            Assert.That(character.GetComponent<BeamHoistAbility>().RequiredSkill, Is.Not.Null);
            Assert.That(new SerializedObject(character.GetComponent<BeamTransportController>()).FindProperty("vfxPrefab").objectReferenceValue, Is.Not.Null);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void AuthoredPrefab_ParticlesActuallyMoveInRequestedWorldDirection(int sign)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<BeamTransportVFX>(BeamTransportSetup.VfxPath);
        Assert.That(prefab, Is.Not.Null);
        var effect = Object.Instantiate(prefab);
        try
        {
            effect.Show(Vector3.zero, sign > 0 ? BeamTransportDirection.Up : BeamTransportDirection.Down);
            var sparks = effect.GetComponentInChildren<ParticleSystem>();
            sparks.Simulate(1f, true, true);
            var particles = new ParticleSystem.Particle[256];
            int count = sparks.GetParticles(particles);
            Assert.That(count, Is.GreaterThan(0));
            float vertical = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = particles[i].totalVelocity;
                if (sparks.main.simulationSpace == ParticleSystemSimulationSpace.Local) velocity = sparks.transform.TransformDirection(velocity);
                vertical += velocity.y;
            }
            Assert.That(vertical * sign / count, Is.GreaterThan(0.1f));
        }
        finally { Object.DestroyImmediate(effect.gameObject); }
    }
}
