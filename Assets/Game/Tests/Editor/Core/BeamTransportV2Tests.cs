using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[TestFixture, Category("Core")]
public sealed class BeamTransportV2Tests
{
    private readonly List<Object> cleanup = new();
    private GameObject player, prop;
    private StarterAssetsInputs input;
    private BeamTransportController transport;
    private BeamHoistAbility ability;
    private BeamHoistSurface surface;
    private BeamTransportVFX effect;
    private SkillData skill;
    private Vector3 origin;
    private float timeScale;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    private T Track<T>(T value) where T : Object { cleanup.Add(value); return value; }
    private GameObject Cube(string name, Vector3 position, Vector3 size)
    {
        var go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        go.name = name; go.transform.position = position; go.transform.localScale = size;
        return go;
    }

    [SetUp]
    public void Setup()
    {
        timeScale = Time.timeScale; Time.timeScale = 1;
        origin = new Vector3(12000, 1000, 12000);
        player = Track(new GameObject("V2 test player")); player.transform.position = origin;
        input = player.AddComponent<StarterAssetsInputs>();
        player.AddComponent<PlayerSkillState>();
        var capsule = player.AddComponent<CharacterController>();
        capsule.height = 2; capsule.radius = .3f; capsule.center = Vector3.up;
        player.AddComponent<GameplaySuspensionController>();
        transport = player.AddComponent<BeamTransportController>();
        skill = Track(ScriptableObject.CreateInstance<SkillData>()); Set(skill, "id", Guid.NewGuid().ToString());
        ability = player.AddComponent<BeamHoistAbility>(); Set(ability, "requiredSkill", skill); Invoke(ability, "OnEnable");
        Cube("V2 floor", origin + Vector3.down * .5f, new Vector3(20, 1, 20));
        prop = Cube("V2 container", origin + new Vector3(2.5f, 1, 0), new Vector3(2, 2, 3));
        surface = prop.AddComponent<BeamHoistSurface>(); Invoke(surface, "OnEnable");
        BeamHoistSurfaceBaker.Bake(surface, true);
        effect = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<BeamTransportVFX>(BeamTransportSetup.VfxPath)));
        cleanup.Remove(effect); cleanup.Add(effect.gameObject);
        Set(transport, "reusableVfx", effect);
    }
    [TearDown]
    public void Teardown()
    {
        transport.CancelTransport(); Set(transport, "reusableVfx", null);
        Invoke(ability, "OnDisable"); Invoke(surface, "OnDisable");
        foreach (var item in cleanup) if (item != null) Object.DestroyImmediate(item);
        cleanup.Clear(); Time.timeScale = timeScale;
    }
    private void Unlock()
    {
        var book = Track(ScriptableObject.CreateInstance<KnowledgeBookItemData>());
        book.skill = skill; book.itemType = ItemType.KnowledgeBook;
        var inventory = player.AddComponent<PlayerInventory>(); Invoke(inventory, "Awake");
        Assert.That(inventory.TryAddItem(book)); inventory.UseItem(0);
        Assert.That(ability.IsUnlocked);
    }
    private bool Candidate(Vector3 root, out BeamHoistPath path)
    {
        for (int i = 0; i < surface.CandidateCount; i++)
            if (surface.TryGetCandidate(root, i, transport.FeetOffset, out var end, out float release))
            { path = BeamHoistPath.Create(root, end, release, 1.5f, .9f); return true; }
        path = default; return false;
    }

    [Test]
    public void HoistFade_LocksBeforeTravel_ReleasesAtLanding_AndCancellationClearsTail()
    {
        Unlock();
        Set(effect, "hoistFadeInDuration", .4f);
        Assert.That(ability.TryActivate(), Is.True);
        var actual = (BeamHoistPath)typeof(BeamTransportController).GetField("hoistPath", Private).GetValue(transport);
        Assert.That(effect.Visibility, Is.Zero);
        Assert.That(input.GameplayInputBlocked, Is.True);
        Assert.That(player.GetComponent<CharacterController>().enabled, Is.False);
        transport.Advance(.2f);
        Assert.That(effect.Visibility, Is.EqualTo(.5f).Within(.001f));
        Assert.That(player.transform.position, Is.EqualTo(actual.start));
        transport.Advance(.2f);
        Assert.That(effect.Visibility, Is.EqualTo(1f));
        Assert.That(player.transform.position, Is.EqualTo(actual.start));
        float duration = actual.liftDuration + actual.transferDuration;
        transport.Advance(duration * .25f);
        Assert.That(Vector3.Distance(player.transform.position, actual.Evaluate(.25f)), Is.LessThan(.002f));
        transport.Advance(duration);
        Assert.That(transport.IsTransporting, Is.False);
        Assert.That(input.GameplayInputBlocked, Is.False);
        Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
        Assert.That(Vector3.Distance(player.transform.position, actual.landing), Is.LessThan(.002f));
        Assert.That(effect.Visibility, Is.EqualTo(1f), "Release must precede independent fade-out");
        transport.CancelTransport();
        Assert.That(effect.Visibility, Is.Zero);
        Assert.That(effect.GetComponentInChildren<ParticleSystem>(true).particleCount, Is.Zero);
        player.transform.position = actual.start;
        Assert.That(transport.TryHoist(actual), Is.True);
        transport.Advance(.1f);
        transport.CancelTransport();
        Assert.That(effect.Visibility, Is.Zero);
        Assert.That(input.GameplayInputBlocked, Is.False);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void Bake_UprightRotatedScaledContainer_AllLowerSidesAndNoChildren(int side)
    {
        prop.transform.rotation = Quaternion.Euler(0, 37, 0);
        BeamHoistSurfaceBaker.Bake(surface, true);
        Assert.That(surface.IsBaked, Is.True, surface.BakeStatus);
        Assert.That(prop.transform.childCount, Is.Zero);
        var b = surface.Footprint;
        Vector3 local = new Vector3(b.center.x, b.min.y, b.center.z);
        if (side < 2) local.x = side == 0 ? b.min.x - .45f : b.max.x + .45f;
        else local.z = side == 2 ? b.min.z - .3f : b.max.z + .3f;
        Assert.That(Candidate(prop.transform.TransformPoint(local), out var path), Is.True);
        Assert.That(path.landing.y, Is.EqualTo(origin.y + 2.02f).Within(.002f));
        Assert.That(path.release.y, Is.GreaterThan(path.landing.y + .4f));
        Assert.That(Candidate(path.landing, out _), Is.False);
        Assert.That(Candidate(path.landing + Vector3.up, out _), Is.False);
        local.y = b.max.y;
        Assert.That(Candidate(prop.transform.TransformPoint(local), out _), Is.False);
    }

    [Test]
    public void Bake_RefreshesColliderChanges_AndRejectsTriggerOnlyProps()
    {
        string signature = surface.BakeSignature;
        prop.GetComponent<BoxCollider>().size = new Vector3(1, 1.5f, 1);
        BeamHoistSurfaceBaker.Bake(surface);
        Assert.That(surface.BakeSignature, Is.Not.EqualTo(signature));
        Assert.That(surface.Footprint.size.y, Is.EqualTo(1.5f));
        prop.GetComponent<BoxCollider>().isTrigger = true;
        BeamHoistSurfaceBaker.Bake(surface);
        Assert.That(surface.IsBaked, Is.False);
    }

    [Test]
    public void ContextualJump_LockedAndOutOfRangeFallBack_UnlockedHoists_TopReturnsToJump()
    {
        input.JumpInput(true); Assert.That(input.jump, Is.True); Assert.That(transport.IsTransporting, Is.False);
        input.JumpInput(false); Unlock();
        player.transform.position = origin + Vector3.back * 8;
        input.JumpInput(true); Assert.That(input.jump, Is.True); Assert.That(transport.IsTransporting, Is.False);
        input.JumpInput(false); player.transform.position = origin;
        Assert.That(Candidate(origin, out var path), Is.True);
        input.JumpInput(true);
        Assert.That(transport.IsTransporting, Is.True); Assert.That(input.jump, Is.False);
        Assert.That(effect.Direction, Is.EqualTo(BeamTransportDirection.Up));
        transport.Advance(1.5f);
        Assert.That(effect.transform.Find("BeamInVFX").gameObject.activeSelf, Is.False, "Beam must release before the curve.");
        Assert.That(player.transform.position.x, Is.EqualTo(origin.x));
        transport.Advance(.45f);
        Assert.That(player.transform.position.x, Is.GreaterThan(origin.x).And.LessThan(path.landing.x));
        Assert.That(player.transform.position.y, Is.GreaterThan(path.landing.y));
        Assert.That(effect.transform.position, Is.EqualTo(origin));
        transport.Advance(.45f); transport.Advance(.01f);
        Assert.That(Vector3.Distance(player.transform.position, path.landing), Is.LessThan(.002f));
        Assert.That(transport.IsTransporting, Is.False);
        Assert.That(input.GameplayInputBlocked, Is.False);
        // Deterministic EditMode stepping has no game frames. Preserve the existing one-frame resume gate.
        Set(input, "blockedThroughFrame", Time.frameCount - 1);
        input.JumpInput(false); input.JumpInput(true);
        Assert.That(input.jump, Is.True); Assert.That(transport.IsTransporting, Is.False);
    }

    [TestCase(.1f, 1f, 2f, false)]
    [TestCase(.6f, 1f, 2f, true)]
    [TestCase(2f, 6.1f, 2f, false)]
    [TestCase(6.1f, 6.5f, 2f, false)]
    [TestCase(2f, 2.5f, 4.1f, false)]
    [TestCase(2f, 2.5f, 4f, true)]
    [TestCase(-1f, 1f, 2f, false)]
    public void AbilityLimits_ApplyToDestinationAndFullLift(float gain, float lift, float lateral, bool allowed)
    {
        var path = BeamHoistPath.Create(Vector3.zero, new Vector3(lateral, gain, 0), lift, 1, 1);
        Assert.That(ability.IsWithinLimits(path), Is.EqualTo(allowed));
    }

    [TestCase("minimumVerticalGain", 3f)]
    [TestCase("maximumHoistHeight", 1f)]
    [TestCase("maximumLateralDistance", 1f)]
    public void ContextualJump_UnreachableTargetFallsBack(string limit, float value)
    {
        Unlock(); Set(ability, limit, value);
        input.JumpInput(true);
        Assert.That(input.jump, Is.True); Assert.That(transport.IsTransporting, Is.False);
    }

    [Test]
    public void CurvedPath_RejectsBlockedRoute_AndLongFrameSweepsWholeCurve()
    {
        Assert.That(Candidate(origin, out var path), Is.True);
        Assert.That(transport.CanHoist(path), Is.True);
        var blocker = Cube("V2 curve blocker", path.Evaluate(.48f) + Vector3.up, Vector3.one * .15f);
        Assert.That(transport.CanHoist(path), Is.False);
        blocker.SetActive(false);
        Assert.That(transport.TryHoist(path), Is.True);
        transport.Advance(path.liftDuration);
        Vector3 release = player.transform.position;
        blocker.SetActive(true);
        transport.Advance(10);
        Assert.That(transport.IsTransporting, Is.False);
        Assert.That(player.transform.position, Is.EqualTo(release));
        Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
    }

    [Test]
    public void ContextualJump_BlockedLiftAndUnsupportedLandingFallBack()
    {
        Unlock();
        var ceiling = Cube("V2 ceiling", origin + Vector3.up * 3, new Vector3(2, .1f, 2));
        input.JumpInput(true); Assert.That(input.jump, Is.True); Assert.That(transport.IsTransporting, Is.False);
        input.JumpInput(false); ceiling.SetActive(false);
        prop.GetComponent<Collider>().enabled = false;
        input.JumpInput(true); Assert.That(input.jump, Is.True); Assert.That(transport.IsTransporting, Is.False);
    }

    [Test]
    public void CanonicalPrefab_FullConeAndNestedLevelStartUseSameSource()
    {
        var canonical = AssetDatabase.LoadAssetAtPath<BeamTransportVFX>(BeamTransportSetup.VfxPath);
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(BeamTransportSetup.LevelStartPath);
        var nested = template.GetComponentInChildren<BeamTransportVFX>(true);
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(nested), Is.EqualTo(canonical));
        foreach (string name in new[] { "BeamOuterCone", "BeamMiddle", "BeamCore", "BeamSparks", "GroundRing" })
            Assert.That(canonical.GetComponentsInChildren<Transform>(true).Single(t => t.name == name).gameObject.activeSelf, Is.True);
        var cone = canonical.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "BeamOuterCone");
        Assert.That(cone.sharedMesh.vertexCount, Is.GreaterThan(20));
        Assert.That(cone.GetComponent<MeshRenderer>().sharedMaterials.All(m => m != null), Is.True);
        Assert.That(AssetDatabase.GetAssetPath(cone.sharedMesh), Is.EqualTo("Assets/Game/Generated/BeamOuterCone.asset"));
        Assert.That(canonical.GetComponentsInChildren<UnityEngine.ProBuilder.ProBuilderMesh>(true), Is.Empty);
    }

    [TestCase("Assets/Game/Scenes/ConstructionSite.unity")]
    [TestCase("Assets/Game/Scenes/GamePoc.unity")]
    public void Repair_PreservesAuthoredOverridesAndRepairsMissingReferences(string scenePath)
    {
        var scene = EditorSceneManager.OpenPreviewScene(scenePath);
        try
        {
            var character = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerCharacter>(true)).Single();
            BeamTransportSetup.ConfigureScene(character);
            var sequence = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerBeamInSequence>(true)).Single();
            var data = new SerializedObject(sequence);
            sequence.transform.position += new Vector3(1, 2, 3);
            sequence.transform.rotation = Quaternion.Euler(0, 23, 0);
            data.FindProperty("startHeight").floatValue = 7.125f;
            data.FindProperty("descentDuration").floatValue = 3.125f;
            data.FindProperty("transportVfx").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            var poses = sequence.GetComponentsInChildren<Transform>(true).Select(t => (t, t.localPosition, t.localRotation, t.localScale)).ToArray();
            var sparks = sequence.GetComponentInChildren<ParticleSystem>(true);
            string particles = EditorJsonUtility.ToJson(sparks);
            BeamTransportSetup.ConfigureScene(character); BeamTransportSetup.ConfigureScene(character);
            data.Update();
            Assert.That(data.FindProperty("startHeight").floatValue, Is.EqualTo(7.125f));
            Assert.That(data.FindProperty("descentDuration").floatValue, Is.EqualTo(3.125f));
            Assert.That(sequence.ArrivalTransform, Is.EqualTo(sequence.transform));
            Assert.That(data.FindProperty("transportVfx").objectReferenceValue, Is.Not.Null);
            Assert.That(EditorJsonUtility.ToJson(sparks), Is.EqualTo(particles));
            foreach (var (t, position, rotation, scale) in poses)
            { Assert.That(t.localPosition, Is.EqualTo(position)); Assert.That(t.localRotation, Is.EqualTo(rotation)); Assert.That(t.localScale, Is.EqualTo(scale)); }
            Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerBeamInSequence>(true)).Count(), Is.EqualTo(1));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
