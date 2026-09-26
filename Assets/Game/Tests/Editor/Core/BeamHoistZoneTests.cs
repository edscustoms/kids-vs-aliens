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
public sealed class BeamHoistZoneTests
{
    private readonly List<Object> owned = new();
    private GameObject player, prop;
    private BeamHoistSurface surface;
    private BeamHoistZonePresentation presentation;
    private BeamHoistAbility ability;
    private BeamTransportController transport;
    private SkillData skill;
    private Vector3 origin;
    private float previousTimeScale;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    private GameObject Cube(Vector3 position, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); owned.Add(go);
        go.transform.position = position; go.transform.localScale = scale; return go;
    }
    [SetUp]
    public void Setup()
    {
        previousTimeScale = Time.timeScale; Time.timeScale = 1;
        origin = new Vector3(17000, 1000, 17000);
        player = new GameObject("Zone test player"); owned.Add(player); player.transform.position = origin;
        player.AddComponent<StarterAssetsInputs>(); player.AddComponent<PlayerSkillState>();
        var capsule = player.AddComponent<CharacterController>(); capsule.height = 2; capsule.center = Vector3.up; capsule.radius = .3f;
        transport = player.AddComponent<BeamTransportController>();
        ability = player.AddComponent<BeamHoistAbility>();
        skill = ScriptableObject.CreateInstance<SkillData>(); owned.Add(skill); Set(skill, "id", System.Guid.NewGuid().ToString());
        Set(ability, "requiredSkill", skill); Invoke(ability, "OnEnable");
        Cube(origin + Vector3.down * .5f, new Vector3(40, 1, 40));
        prop = Cube(origin + new Vector3(3, 1, 0), new Vector3(2, 2, 3));
        surface = prop.AddComponent<BeamHoistSurface>(); Invoke(surface, "OnEnable"); BeamHoistSurfaceBaker.Bake(surface, true);
        presentation = player.AddComponent<BeamHoistZonePresentation>();
        Set(presentation, "zonePrefab", AssetDatabase.LoadAssetAtPath<BeamHoistZoneVFX>(BeamHoistZoneSetup.PrefabPath));
        Invoke(presentation, "Awake");
    }
    private void Unlock() => player.GetComponent<PlayerSkillState>().UnlockSkill(skill);
    private void Tick(int count = 40, float dt = .05f) { Physics.SyncTransforms(); for (int i = 0; i < count; i++) presentation.TickPresentation(dt); }
    private BeamHoistZoneVFX Effect => prop.GetComponentInChildren<BeamHoistZoneVFX>(true);
    [TearDown]
    public void Cleanup()
    {
        transport.CancelTransport(); Invoke(surface, "OnDisable"); Invoke(ability, "OnDisable");
        foreach (var item in owned) if (item != null) Object.DestroyImmediate(item);
        owned.Clear(); Time.timeScale = previousTimeScale;
    }

    [Test]
    public void Discovery_KnowledgeDistanceHysteresisAndActiveCells()
    {
        Tick(); Assert.That(Effect, Is.Null, "Locked Knowledge must not instantiate visible pads.");
        Vector3 edge = origin;
        edge.x = Enumerable.Range(0, surface.CandidateCount).Min(i => surface.transform.TransformPoint(surface.GetBakedApproach(i).region.min).x);
        Unlock(); player.transform.position = edge + Vector3.left * 9; Tick(); Assert.That(Effect, Is.Null);
        player.transform.position = edge + Vector3.left * 5.5f; Tick();
        Assert.That(Effect, Is.Not.Null); Assert.That(Effect.State, Is.EqualTo(BeamHoistZoneState.Available));
        Assert.That(Effect.Opacity, Is.EqualTo(1));
        var instance = Effect;
        player.transform.position = edge + Vector3.left * 6.3f; Tick();
        Assert.That(Effect.State, Is.EqualTo(BeamHoistZoneState.Available), "Between 6 and 7m must stay revealed.");
        player.transform.position = edge + Vector3.left * 8; Tick(1);
        Assert.That(Effect.Opacity, Is.GreaterThan(0).And.LessThan(1), "Distance hide should fade.");
        Tick(); Assert.That(Effect.Opacity, Is.Zero);
        player.transform.position = edge + Vector3.left * 6.3f; Tick(); Assert.That(Effect.State, Is.EqualTo(BeamHoistZoneState.Hidden));
        player.transform.position = origin + Vector3.right * .8f; Tick(); Assert.That(Effect.State, Is.EqualTo(BeamHoistZoneState.Active));
        player.transform.position = edge + Vector3.left * .5f; Tick(); Assert.That(Effect.State, Is.EqualTo(BeamHoistZoneState.Available));
        Assert.That(Effect, Is.SameAs(instance), "Reuse the same visual while walking around.");
    }

    [Test]
    public void JumpAndHoist_Unchanged_AndTransportHidesZoneImmediately()
    {
        var effectObject = new GameObject("Existing transport effect"); owned.Add(effectObject);
        Set(transport, "reusableVfx", effectObject.AddComponent<BeamTransportVFX>());
        try
        {
            Unlock(); var input = player.GetComponent<StarterAssetsInputs>();
            player.transform.position = origin + Vector3.left * 3; Tick();
            input.JumpInput(true); Assert.That(input.jump, Is.True); Assert.That(transport.IsTransporting, Is.False); input.JumpInput(false);
            player.transform.position = origin + Vector3.right * .8f; Tick();
            int cell = Enumerable.Range(0, surface.CandidateCount).First(i => ability.CanPreviewSurfaceHoist(surface, i, player.transform.position));
            Assert.That(ability.TryBuildSurfaceHoist(surface, cell, player.transform.position, out var path), Is.True);
            Vector3 position = player.transform.position;
            Tick(); Assert.That(player.transform.position, Is.EqualTo(position));
            input.JumpInput(true); Assert.That(transport.IsTransporting, Is.True); Assert.That(input.jump, Is.False);
            Tick(1); Assert.That(Effect.Opacity, Is.Zero);
            // The ability chooses the best candidate, not necessarily the first
            // preview-valid cell. The stationary VFX prelude is not curve time.
            path = (BeamHoistPath)typeof(BeamTransportController).GetField("hoistPath", Private).GetValue(transport);
            transport.Advance(.5f);
            Assert.That(player.transform.position, Is.EqualTo(position));
            float elapsed = 0;
            for (int i = 1; i <= 24; i++)
            {
                transport.Advance(.1f); elapsed += .1f; Tick(1, 0);
                Assert.That(Vector3.Distance(player.transform.position, path.Evaluate(elapsed / path.Duration)), Is.LessThan(.003f));
            }
        }
        finally { Set(transport, "reusableVfx", null); }
    }

    [Test]
    public void RouteValidation_ChangesActiveStateOnly_AndTopIsHidden()
    {
        Unlock(); string baked = EditorJsonUtility.ToJson(surface);
        player.transform.position = origin + new Vector3(3, 2.02f, 0); Tick(); Assert.That(Effect, Is.Null);
        player.transform.position = origin + Vector3.right * .8f; Tick();
        Assert.That(Effect.State, Is.EqualTo(BeamHoistZoneState.Active));
        var mesh = Effect.GetComponentInChildren<MeshFilter>().sharedMesh;
        var vertices = mesh.vertices;
        Set(ability, "maximumHoistHeight", 1f); Tick();
        Assert.That(Effect.State, Is.EqualTo(BeamHoistZoneState.Available));
        Assert.That(mesh.vertices, Is.EqualTo(vertices));
        Set(ability, "maximumHoistHeight", 6f); Tick();
        Assert.That(Effect.State, Is.EqualTo(BeamHoistZoneState.Active));
        Cube(origin + new Vector3(1, 2.8f, 0), new Vector3(5, .2f, 8)); Tick(100);
        Assert.That(Effect.State, Is.EqualTo(BeamHoistZoneState.Available));
        Assert.That(mesh.vertices, Is.EqualTo(vertices));
        Assert.That(EditorJsonUtility.ToJson(surface), Is.EqualTo(baked));
    }

    [Test]
    public void EntireBakedFootprint_IsFixedThroughMovementFadeReentryAndDisable()
    {
        Unlock(); Tick();
        var effect = Effect;
        var filter = effect.GetComponentInChildren<MeshFilter>();
        var mesh = filter.sharedMesh;
        var vertices = mesh.vertices;
        var triangles = mesh.triangles;
        var matrix = filter.transform.localToWorldMatrix;
        // Every baked cell appears, including cells on the far side; no player-relative subset.
        float bakedArea = Enumerable.Range(0, surface.CandidateCount).Sum(i =>
        {
            var bounds = surface.GetBakedApproach(i).region;
            return bounds.size.x * bounds.size.z;
        });
        float meshArea = 0;
        for (int i = 0; i < triangles.Length; i += 3)
            meshArea += Mathf.Abs(Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                vertices[triangles[i + 2]] - vertices[triangles[i]]).y) * .5f;
        Assert.That(meshArea, Is.EqualTo(bakedArea).Within(.001f));
        foreach (var offset in new[] { new Vector3(.8f, 0, 0), new Vector3(3, 0, 3),
            new Vector3(6, 0, 0), new Vector3(3, 0, -3), new Vector3(-20, 0, 0), Vector3.zero })
        {
            player.transform.position = origin + offset; Tick();
            Assert.That(Effect, Is.SameAs(effect));
            Assert.That(filter.sharedMesh, Is.SameAs(mesh));
            Assert.That(mesh.vertices, Is.EqualTo(vertices));
            Assert.That(mesh.triangles, Is.EqualTo(triangles));
            Assert.That(filter.transform.localToWorldMatrix, Is.EqualTo(matrix));
        }
        presentation.enabled = false; Invoke(presentation, "OnDisable");
        Assert.That(effect.Opacity, Is.Zero);
        presentation.enabled = true; Tick();
        Assert.That(effect.Opacity, Is.EqualTo(1));
        Assert.That(mesh.vertices, Is.EqualTo(vertices));
    }

    [Test]
    public void Bake_UsesActualAbilityLimits_AndRejectsBlockedOrTooHighStarts()
    {
        float Area() => Enumerable.Range(0, surface.CandidateCount).Sum(i =>
        {
            var b = surface.GetBakedApproach(i).region; return b.size.x * b.size.z;
        });
        float initial = Area(); string signature = surface.BakeSignature;
        Assert.That(initial, Is.GreaterThan(0));
        Set(ability, "maximumLateralDistance", 2f); BeamHoistSurfaceBaker.Bake(surface);
        Assert.That(surface.BakeSignature, Is.Not.EqualTo(signature));
        Assert.That(Area(), Is.GreaterThan(0).And.LessThan(initial));
        for (int i = 0; i < surface.CandidateCount; i++)
        {
            var cell = surface.GetBakedApproach(i);
            foreach (float x in new[] { cell.region.min.x, cell.region.center.x, cell.region.max.x })
                foreach (float z in new[] { cell.region.min.z, cell.region.center.z, cell.region.max.z })
                {
                    Vector3 start = surface.transform.TransformPoint(new Vector3(x, 0, z));
                    start.y = origin.y + .02f;
                    Assert.That(ability.TryBuildSurfaceHoist(surface, i, start, out var path), Is.True);
                    Assert.That(ability.IsWithinLimits(path), Is.True);
                    Assert.That(transport.IsLandingSafe(start) && transport.IsHoistRouteClear(path), Is.True);
                }
        }
        Set(ability, "maximumHoistHeight", 1f); BeamHoistSurfaceBaker.Bake(surface);
        Assert.That(surface.CandidateCount, Is.Zero, "A surface above the actual ability height has no baked start area.");
        Set(ability, "maximumHoistHeight", 6f); Set(ability, "maximumLateralDistance", 4f);
        BeamHoistSurfaceBaker.Bake(surface); Assert.That(Area(), Is.EqualTo(initial).Within(.001f));
        Cube(origin + new Vector3(1, 2.8f, 0), new Vector3(5, .2f, 8));
        BeamHoistSurfaceBaker.Bake(surface, true);
        Assert.That(Area(), Is.LessThan(initial), "Static blocked routes must be excluded at bake time.");
    }

    [Test]
    public void Footprint_DoesNotFillGapsOrCreatePhysicsObjects_OrAdvanceGameplayRandom()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<BeamHoistZoneVFX>(BeamHoistZoneSetup.PrefabPath);
        var effect = Object.Instantiate(prefab); owned.Add(effect.gameObject);
        var patches = new List<BeamHoistZoneVFX.Patch>();
        foreach (float x in new[] { 0f, 1f, 3f })
            patches.Add(new BeamHoistZoneVFX.Patch { bounds = new Bounds(new Vector3(x + .5f, 0, .5f), new Vector3(1, 1, 1)),
                a = new Vector3(x, x * .1f, 0), b = new Vector3(x + 1, (x + 1) * .1f, 0),
                c = new Vector3(x + 1, (x + 1) * .1f, 1), d = new Vector3(x, x * .1f, 1) });
        effect.SetFootprint(patches);
        Assert.That(effect.PatchCount, Is.EqualTo(2), "Adjacent cells merge; the missing cell at x=2 must stay dark.");
        var mesh = effect.GetComponentInChildren<MeshFilter>().sharedMesh;
        Assert.That(mesh.vertices[5].y, Is.EqualTo(.2f), "Keep the ground shape while sharing glyphs across adjacent cells.");
        Assert.That(mesh.uv[1].x, Is.EqualTo(.5f), "The first cell uses half of the common pad, not a repeated full glyph.");
        Assert.That(effect.GetComponentsInChildren<Collider>(true), Is.Empty);
        Assert.That(effect.GetComponentsInChildren<Light>(true), Is.Empty);
        var before = Random.state; effect.Present(BeamHoistZoneState.Active, 1, 0, 0);
        Assert.That(JsonUtility.ToJson(Random.state), Is.EqualTo(JsonUtility.ToJson(before)));
        Assert.That(ShaderUtil.ShaderHasError(Shader.Find("Game/Beam Hoist Zone")), Is.False);
    }

    [Test]
    public void MultipleSurfaces_RuntimeRegistrationAndDisableWork()
    {
        Unlock(); Tick(); Assert.That(Effect, Is.Not.Null);
        var second = Cube(origin + new Vector3(-3, 1.5f, 0), new Vector3(2, 3, 4));
        var other = second.AddComponent<BeamHoistSurface>(); Invoke(other, "OnEnable"); BeamHoistSurfaceBaker.Bake(other, true);
        try
        {
            Tick(80); Assert.That(second.GetComponentInChildren<BeamHoistZoneVFX>(), Is.Not.Null);
            other.enabled = false; Invoke(other, "OnDisable"); Tick();
            Assert.That(second.GetComponentInChildren<BeamHoistZoneVFX>(), Is.Null);
        }
        finally { Invoke(other, "OnDisable"); }
    }

    [TestCase("Assets/Game/Scenes/ConstructionSite.unity")]
    [TestCase("Assets/Game/Scenes/GamePoc.unity")]
    public void Setup_IsIdempotentAndNeverAddsArrivalPad(string path)
    {
        var scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            var character = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerCharacter>(true)).Single();
            var arrivals = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerBeamInSequence>(true)).ToArray();
            string[] before = arrivals.Select(a => DescribeArrival(a.gameObject)).ToArray();
            BeamHoistZoneSetup.ConfigureScene(character); BeamHoistZoneSetup.ConfigureScene(character);
            Assert.That(character.GetComponents<BeamHoistZonePresentation>().Length, Is.EqualTo(1));
            for (int i = 0; i < arrivals.Length; i++) Assert.That(DescribeArrival(arrivals[i].gameObject), Is.EqualTo(before[i]));
            Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BeamHoistZoneVFX>(true)), Is.Empty);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static string DescribeArrival(GameObject root)
    {
        var report = new System.Text.StringBuilder();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(t, root.transform);
            report.AppendLine($"{path}: local={t.localPosition:R}; rotation={t.localRotation:R}; scale={t.localScale:R}; world={t.position:R}; active={t.gameObject.activeSelf}");
            foreach (var component in t.GetComponents<Component>())
            {
                if (component is Transform) continue;
                report.AppendLine(component.GetType().Name + ": " + EditorJsonUtility.ToJson(component));
                if (component is Renderer renderer)
                    report.AppendLine("materials=" + string.Join(",", renderer.sharedMaterials.Select(AssetDatabase.GetAssetPath)));
            }
        }
        return report.ToString();
    }
}
