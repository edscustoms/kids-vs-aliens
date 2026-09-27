using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class AlienGroundAuroraTests
{
    private const string Art = "Assets/Game/Art/Environment/AlienGroundAurora";
    private const string PrefabPath = "Assets/Game/Prefabs/Environment/PF_AlienGroundAurora.prefab";
    private const string ScenePath = "Assets/Game/Scenes/ConstructionSite.unity";
    private const string SaveKey = "AlienGroundAuroraTests.Saves";
    private const string FixturePath = "Assets/__AuroraTestSchedule.asset";
    private static AlienGroundAuroraSchedule Schedule => AssetDatabase.LoadAssetAtPath<AlienGroundAuroraSchedule>(Art + "/ConstructionSite_Aurora.asset");

    [UnitySetUp]
    public IEnumerator Setup()
    {
        SessionState.SetString(SaveKey, Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY") ?? "");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", Path.GetFullPath("Logs/AuroraSaves-" + Guid.NewGuid().ToString("N")));
        SessionState.SetFloat(SaveKey + ".Scale", Time.timeScale);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield break;
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
        AssetDatabase.DeleteAsset(FixturePath);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", SessionState.GetString(SaveKey, ""));
        Time.timeScale = SessionState.GetFloat(SaveKey + ".Scale", 1);
        SessionState.EraseString(SaveKey); SessionState.EraseFloat(SaveKey + ".Scale");
    }

    [Test]
    public void BakedScheduleHasCircularSpacingLifetimesAndBoundedOccupancy()
    {
        var schedule = Schedule;
        Assert.That(schedule, Is.Not.Null);
        EditorSceneManager.OpenScene(ScenePath);
        var authoring = Object.FindAnyObjectByType<AlienGroundAuroraAuthoring>();
        Assert.That(schedule.events.Length, Is.EqualTo(authoring.eventCount));
        Assert.That(schedule.events.Select(e => e.position).Distinct().Count(), Is.EqualTo(authoring.eventCount));
        Assert.That(schedule.duration, Is.EqualTo(authoring.scheduleDuration));
        Assert.That(schedule.poolSize, Is.LessThanOrEqualTo(14));
        float previous = -1;
        foreach (var value in schedule.events)
        {
            Assert.That(value.startTime, Is.GreaterThanOrEqualTo(previous).And.LessThan(schedule.duration));
            previous = value.startTime;
            Assert.That(value.lifetime, Is.InRange(8, 20));
            Assert.That(value.fadeIn + value.fadeOut, Is.LessThan(value.lifetime));
            Assert.That(value.dimensions.x, Is.InRange(authoring.lengthRange.x, authoring.lengthRange.y));
            Assert.That(value.dimensions.y, Is.InRange(authoring.widthRange.x, authoring.widthRange.y));
            Assert.That(value.fadeIn, Is.InRange(authoring.fadeInRange.x, authoring.fadeInRange.y));
            Assert.That(value.fadeOut, Is.InRange(authoring.fadeOutRange.x, authoring.fadeOutRange.y));
            Assert.That(value.footprint.x, Is.EqualTo(authoring.edgeSoftness));
            Assert.That(value.footprint.y, Is.EqualTo(authoring.irregularity));
            Assert.That(value.motionSpeed, Is.InRange(.3f, .45f));
            Assert.That(value.poolSlot, Is.InRange(0, schedule.poolSize - 1));
        }
        for (int i = 0; i < schedule.events.Length; i++)
        for (int j = 0; j < i; j++)
        {
            var a = schedule.events[i]; var b = schedule.events[j];
            if (!AlienGroundAuroraBaker.Overlap(a, b, schedule.duration)) continue;
            Assert.That(a.poolSlot, Is.Not.EqualTo(b.poolSlot), "A pooled patch must finish its entire lifetime before reuse, including across the loop seam");
            Vector3 delta = a.position - b.position; delta.y = 0;
            Assert.That(delta.magnitude, Is.GreaterThanOrEqualTo((a.dimensions.magnitude + b.dimensions.magnitude) * .55f + .799f));
        }
        Assert.That(schedule.events.Sum(e => e.lifetime) / schedule.duration, Is.InRange(0, authoring.maximumActive));
        Assert.That(ExpectedCount(schedule, 0), Is.GreaterThan(0), "Warm start must include events crossing from the previous cycle");
        Assert.That(Mathf.Abs(ExpectedCount(schedule, .01f) - ExpectedCount(schedule, schedule.duration - .01f)), Is.LessThanOrEqualTo(1));
    }

    [Test]
    public void AllAuthoredFootprintsHaveRealGroundClearanceAndLandmarkExclusions()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Physics.SyncTransforms();
        var authoring = Object.FindAnyObjectByType<AlienGroundAuroraAuthoring>();
        Assert.That(authoring, Is.Not.Null);
        Assert.That(authoring.CompareTag("EditorOnly"), Is.True);
        Assert.That(authoring.excludedObjects.Any(t => t.GetComponent<HealingPodController>() != null), Is.True);
        Assert.That(authoring.excludedObjects.Any(t => t.name == "LevelStart"), Is.True);
        Assert.That(authoring.excludedObjects.Count(t => t.name == "PF_Excavator_A"), Is.EqualTo(2));
        var perimeter = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Single(t => t.name == "Perimeter_Fences");
        var fences = perimeter.GetComponentsInChildren<MeshRenderer>();
        Bounds boundary = fences[0].bounds;
        foreach (var fence in fences) boundary.Encapsulate(fence.bounds);
        foreach (var zone in authoring.allowedZones)
        {
            Assert.That(zone.min.x, Is.GreaterThan(boundary.min.x + 1.5f));
            Assert.That(zone.max.x, Is.LessThan(boundary.max.x - 1.5f));
            Assert.That(zone.min.z, Is.GreaterThan(boundary.min.z + 1.5f));
            Assert.That(zone.max.z, Is.LessThan(boundary.max.z - 1.5f));
        }
        foreach (var value in Schedule.events)
            Assert.That(AlienGroundAuroraBaker.ValidateFootprint(authoring, value), Is.True, "Invalid baked footprint " + value.position);
    }

    [Test]
    public void PrefabUsesSharedLightweightRenderingAndRuntimeHasNoPlacementQueries()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var patches = prefab.GetComponentsInChildren<AlienGroundAuroraPatch>(true);
        Assert.That(patches.Length, Is.EqualTo(Schedule.poolSize));
        Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<Light>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
        Assert.That(patches.Select(p => p.Ribbon.sharedMaterial).Distinct().Count(), Is.EqualTo(1));
        Assert.That(prefab.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).Distinct().Count(), Is.EqualTo(1));
        foreach (var patch in patches)
        {
            Assert.That(patch.gameObject.activeSelf, Is.False);
            Assert.That(patch.Ribbon.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(patch.Ribbon.receiveShadows, Is.False);
            Assert.That(patch.Ribbon.lightProbeUsage, Is.EqualTo(LightProbeUsage.Off));
            Assert.That(patch.Ribbon.reflectionProbeUsage, Is.EqualTo(ReflectionProbeUsage.Off));
            var mesh = patch.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.vertexCount, Is.LessThanOrEqualTo(900));
            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            Assert.That(mesh.uv2.Select(v => v.x).Distinct().Count(), Is.EqualTo(3), "Three flowing sections share one mesh/renderer");
            Assert.That(mesh.vertices.Max(v => v.y), Is.InRange(.5f, .7f), "Wisps must have actual vertical geometry");
            Assert.That(mesh.bounds.max.y, Is.GreaterThan(mesh.vertices.Max(v => v.y) + .12f), "Culling bounds contain shader breathing");
        }
        Assert.That(ShaderUtil.ShaderHasError(patches[0].Ribbon.sharedMaterial.shader), Is.False);
        foreach (string type in new[] {"AlienGroundAuroraController", "AlienGroundAuroraPatch"})
        {
            string code = File.ReadAllText("Assets/Game/Scripts/World/" + type + ".cs");
            foreach (string forbidden in new[] {"Random", "Physics.", "Instantiate(", "Destroy(", "FindObject", ".material =", ".materials"})
                Assert.That(code, Does.Not.Contain(forbidden), type + " must only consume its baked records and authored pool");
        }
    }

    [TestCase(4)]
    [TestCase(19)]
    public void ManualBakeHasExactCountIsDeterministicAndFailurePreservesAcceptedData(int count)
    {
        EditorSceneManager.OpenScene(ScenePath);
        var original = Object.FindAnyObjectByType<AlienGroundAuroraAuthoring>();
        var authoring = Object.Instantiate(original.gameObject).GetComponent<AlienGroundAuroraAuthoring>();
        var controller = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)).GetComponent<AlienGroundAuroraController>();
        authoring.controller = controller;
        authoring.output = ScriptableObject.CreateInstance<AlienGroundAuroraSchedule>();
        AssetDatabase.CreateAsset(authoring.output, FixturePath);
        var data = new SerializedObject(controller); data.FindProperty("schedule").objectReferenceValue = authoring.output; data.ApplyModifiedPropertiesWithoutUndo();
        authoring.eventCount = count;
        authoring.maximumActive = 4; authoring.scheduleDuration = 128;
        string production = EditorJsonUtility.ToJson(Schedule);
        AlienGroundAuroraBaker.Bake(authoring);
        Assert.That(authoring.output.events.Length, Is.EqualTo(count));
        Assert.That(authoring.output.events.Select(e => e.position).Distinct().Count(), Is.EqualTo(count));
        Assert.That(authoring.output.events.Select(e => e.footprint).Distinct().Count(), Is.EqualTo(count));
        Assert.That(authoring.output.events.Select(e => e.lifetime).Distinct().Count(), Is.GreaterThan(1));
        Assert.That(authoring.output.events.Select(e => e.dimensions).Distinct().Count(), Is.GreaterThan(1));
        string first = EditorJsonUtility.ToJson(authoring.output);
        AlienGroundAuroraBaker.Bake(authoring);
        Assert.That(EditorJsonUtility.ToJson(authoring.output), Is.EqualTo(first));
        authoring.enabled = false; authoring.enabled = true;
        Assert.That(EditorJsonUtility.ToJson(authoring.output), Is.EqualTo(first), "Enable does not rebake");
        authoring.eventCount = authoring.maximumActive * 100;
        Assert.That(Assert.Throws<InvalidOperationException>(() => AlienGroundAuroraBaker.Bake(authoring)).Message,
            Does.Contain("loop capacity"), "Reject impossible counts before placement work; never silently reduce them");
        Assert.That(EditorJsonUtility.ToJson(authoring.output), Is.EqualTo(first));
        authoring.eventCount = count;
        authoring.allowedGround = Array.Empty<Collider>();
        Assert.Throws<InvalidOperationException>(() => AlienGroundAuroraBaker.Bake(authoring));
        Assert.That(EditorJsonUtility.ToJson(authoring.output), Is.EqualTo(first));
        Assert.That(EditorJsonUtility.ToJson(Schedule), Is.EqualTo(production));
    }

    [UnityTest]
    public IEnumerator PlaybackIsDeterministicAcrossLoopsPauseHitchesAndReenableWithoutAllocations()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 0;
        var first = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)).GetComponent<AlienGroundAuroraController>();
        var second = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)).GetComponent<AlienGroundAuroraController>();
        var patches = first.GetComponentsInChildren<AlienGroundAuroraPatch>(true);
        Assert.That(patches.All(p => p != null && p.Ribbon != null), Is.True, "Every instantiated pool slot retains its renderer reference");
        var identities = patches.Select(p => p.GetEntityId()).ToArray();
        var material = patches[0].Ribbon.sharedMaterial;
        for (int step = 0; step < 1600; step++)
        {
            first.Advance(.19f); second.Advance(.19f);
            Assert.That(first.ActiveCount, Is.EqualTo(ExpectedCount(first.Schedule, first.PlaybackTime)));
            Assert.That(second.ActiveCount, Is.EqualTo(first.ActiveCount));
            Assert.That(first.ActiveCount, Is.LessThanOrEqualTo(first.PoolSize));
            if (step % 50 == 0) AssertBakedPoses(first, patches);
        }
        // Warm all activation/property paths before measuring only the playback calls.
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int step = 0; step < 1600; step++) first.Advance(.19f);
        long allocations = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(allocations, Is.Zero, "Steady-state scheduling, activation and shader updates must not allocate managed memory");
        double paused = first.PlaybackTime;
        yield return EditorTestFrame.Next();
        Assert.That(first.PlaybackTime, Is.EqualTo(paused));
        first.Advance(1000);
        Assert.That(first.ActiveCount, Is.EqualTo(ExpectedCount(first.Schedule, first.PlaybackTime)));
        AssertBakedPoses(first, patches);
        first.enabled = false;
        Assert.That(patches.All(p => !p.gameObject.activeSelf), Is.True);
        first.enabled = true;
        Assert.That(first.PlaybackTime, Is.Zero);
        Assert.That(first.ActiveCount, Is.EqualTo(ExpectedCount(first.Schedule, 0)));
        CollectionAssert.AreEqual(identities, first.GetComponentsInChildren<AlienGroundAuroraPatch>(true).Select(p => p.GetEntityId()).ToArray());
        foreach (var patch in patches) Assert.That(patch.Ribbon.sharedMaterial, Is.SameAs(material));
        Debug.Log("AURORA steady-state managed bytes: " + allocations);
    }

    [Test]
    public void PatchEnvelopeFadesForWholeLifetimeAndDoesNotCopyItsMaterial()
    {
        var patch = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Environment/PF_AlienGroundAuroraPatch.prefab")).GetComponent<AlienGroundAuroraPatch>();
        try
        {
            patch.Initialize();
            var value = Schedule.events[0]; var material = patch.Ribbon.sharedMaterial;
            patch.Begin(value);
            patch.Present(value, 0); Assert.That(patch.Opacity, Is.Zero);
            var properties = new MaterialPropertyBlock();
            patch.Ribbon.GetPropertyBlock(properties);
            Assert.That(properties.GetVector("_Footprint"), Is.EqualTo(value.footprint), "Playback consumes the authored perimeter values without selecting a shape");
            patch.Present(value, value.fadeIn * .5f); Assert.That(patch.Opacity, Is.EqualTo(.5f).Within(.001));
            patch.Present(value, value.lifetime * .5f); Assert.That(patch.Opacity, Is.EqualTo(1));
            patch.Present(value, value.lifetime - value.fadeOut * .5f); Assert.That(patch.Opacity, Is.EqualTo(.5f).Within(.001));
            patch.Present(value, value.lifetime); Assert.That(patch.Opacity, Is.Zero);
            // Authored seconds, not just normalized checkpoints: no fast first/last-frame flash.
            foreach (var authored in Schedule.events)
            {
                patch.Present(authored, .25f); Assert.That(patch.Opacity, Is.LessThan(.1f));
                patch.Present(authored, authored.lifetime-.25f); Assert.That(patch.Opacity, Is.LessThan(.1f));
                float previous = 0;
                for (int frame = 0; frame <= 30; frame++)
                {
                    patch.Present(authored, frame/30f);
                    Assert.That(patch.Opacity-previous, Is.InRange(0, .04f), "Smooth materialization without opacity steps");
                    previous=patch.Opacity;
                }
            }
            Assert.That(patch.Ribbon.sharedMaterial, Is.SameAs(material));
            patch.Hide(); Assert.That(patch.gameObject.activeSelf, Is.False);
        }
        finally { Object.DestroyImmediate(patch.gameObject); }
    }

    [UnityTest]
    public IEnumerator ConstructionSiteRendersAnimatedGroundEnergyInActualUrp()
    {
        EditorSceneManager.OpenScene(ScenePath);
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        // Let the real gameplay camera settle after its arrival movement.
        float elapsed = 0;
        double deadline = EditorApplication.timeSinceStartup + 25;
        while (elapsed < 8 && EditorApplication.timeSinceStartup < deadline)
        {
            if (Time.timeScale == 0)
            {
                ActiveRunController.Instance?.SendMessage("OnApplicationPause", false);
                ActiveRunController.Instance?.SendMessage("OnApplicationFocus", true);
                var menu = Object.FindAnyObjectByType<InGameMenuController>();
                if (menu != null && menu.IsOpen) menu.ResumeGame();
            }
            yield return EditorTestFrame.Next(); elapsed += Time.deltaTime;
        }
        Assert.That(elapsed, Is.GreaterThanOrEqualTo(8));
        Time.timeScale = 0;
        var controller = Object.FindAnyObjectByType<AlienGroundAuroraController>();
        Assert.That(controller, Is.Not.Null);
        controller.enabled = false; controller.enabled = true;
        var captureEvent = controller.Schedule.events.Where(e => e.lifetime > e.fadeIn + e.fadeOut + 2.2f)
            .OrderByDescending(e => e.dimensions.x * e.dimensions.y).First();
        controller.Advance(captureEvent.startTime + captureEvent.fadeIn + .1f);
        var cameraObject = new GameObject("Aurora render fixture");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.fieldOfView = 50; camera.farClipPlane = 250;
        camera.transform.position = new Vector3(56,80,-70); camera.transform.LookAt(new Vector3(0,0,0));
        cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        Capture(camera, "overview");
        var patch = SelectReferencePatch(controller, out var reference);
        Vector3 center = patch.transform.position;
        var player = Object.FindAnyObjectByType<PlayerCharacter>();
        Assert.That(player.GetComponent<BeamTransportController>().IsTransporting, Is.False);
        Vector3 gameplayOffset = Camera.main.transform.position - player.transform.position;
        PlaceAmyBeside(player, patch);
        camera.transform.position = center + patch.transform.rotation * new Vector3(3.5f,1.1f,-6);
        camera.transform.LookAt(center + Vector3.up * .25f);
        var first = Capture(camera, "closeup");
        controller.Advance(.8f);
        var second = Capture(camera, "motion");
        Assert.That(first.SequenceEqual(second), Is.False, "The baked event must visibly animate");
        // Actual gameplay lens/offset with Amy beside the patch as a scale reference.
        camera.transform.position = player.transform.position + gameplayOffset;
        camera.transform.rotation = Camera.main.transform.rotation;
        camera.fieldOfView = Camera.main.fieldOfView;
        camera.orthographic = Camera.main.orthographic;
        camera.orthographicSize = Camera.main.orthographicSize;
        var planes = GeometryUtility.CalculateFrustumPlanes(camera);
        int visible = 0;
        foreach (var active in controller.GetComponentsInChildren<AlienGroundAuroraPatch>())
            if (GeometryUtility.TestPlanesAABB(planes, active.Ribbon.bounds)) visible++;
        Assert.That(visible, Is.InRange(1, controller.Schedule.poolSize), "Larger bounds may include distant or occluded patches in the frustum");
        Debug.Log("AURORA representative gameplay-frustum patch count: " + visible);
        Capture(camera, "gameplay");
        controller.Advance(.8f);
        Capture(camera, "gameplay-motion");
        Assert.That(ShaderUtil.ShaderHasError(patch.Ribbon.sharedMaterial.shader), Is.False);
        CaptureLifecycle(camera, patch, reference);
    }

    private static AlienGroundAuroraPatch SelectReferencePatch(AlienGroundAuroraController controller,
        out AlienGroundAuroraSchedule.Event reference)
    {
        var patches=controller.GetComponentsInChildren<AlienGroundAuroraPatch>(true);
        reference=default;
        AlienGroundAuroraPatch selected=null;
        foreach(var value in controller.Schedule.events)
        {
            double age=(controller.PlaybackTime-value.startTime+controller.Schedule.duration)%controller.Schedule.duration;
            if(age < value.fadeIn || age+2 > value.lifetime-value.fadeOut || value.dimensions.x <= reference.dimensions.x) continue;
            reference=value;selected=patches[value.poolSlot];
        }
        Assert.That(selected, Is.Not.Null, "Capture motion on a held-opacity event, not a dissolving patch");
        return selected;
    }

    private static void CaptureLifecycle(Camera camera, AlienGroundAuroraPatch patch, AlienGroundAuroraSchedule.Event value)
    {
        // Freeze the other authored events and present this same record over its entire lifespan.
        // No test-only timing or color overrides: these are the actual shipping fade and motion values.
        const int frames = 40;
        for(int frame=0;frame<=frames;frame++)
        {
            patch.Present(value, value.lifetime*frame/frames);
            Capture(camera,"lifecycle-"+frame.ToString("D3"));
        }
        File.WriteAllText("Logs/AlienGroundAuroraTuning/lifecycle.txt",$"frames={frames+1}\nlifetime={value.lifetime}\nfadeIn={value.fadeIn}\nfadeOut={value.fadeOut}");
    }

    private static void PlaceAmyBeside(PlayerCharacter player, AlienGroundAuroraPatch patch)
    {
        var capsule = player.GetComponent<CharacterController>();
        capsule.enabled = false;
        Vector3 towardCamera = Camera.main.transform.position - player.transform.position;
        foreach (var offset in new[] {patch.transform.forward * (patch.transform.localScale.z * .55f + .55f),
            -patch.transform.forward * (patch.transform.localScale.z * .55f + .55f),
            patch.transform.right * (patch.transform.localScale.x * .5f + .7f),
            -patch.transform.right * (patch.transform.localScale.x * .5f + .7f)}
            .OrderByDescending(offset => Vector3.Dot(offset, towardCamera)))
        {
            if (!Physics.Raycast(patch.transform.position + offset + Vector3.up * 3, Vector3.down,
                out var ground, 6, ~0, QueryTriggerInteraction.Ignore) || !(ground.collider is TerrainCollider)) continue;
            Vector3 position = ground.point + Vector3.up * .03f;
            if (Physics.OverlapCapsule(position + Vector3.up * .35f, position + Vector3.up * 1.1f, .25f,
                ~0, QueryTriggerInteraction.Ignore).Any(c => !(c is TerrainCollider) && !c.transform.IsChildOf(player.transform))) continue;
            Vector3 facing = patch.transform.position - position; facing.y = 0;
            player.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing));
            capsule.enabled = true;
            var animator = player.ActiveVisual.Animator;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Update(.1f);
            Physics.SyncTransforms();
            return;
        }
        Assert.Fail("No clear Amy scale-reference pose beside the selected baked patch");
    }

    private static int ExpectedCount(AlienGroundAuroraSchedule schedule, double time) =>
        schedule.events.Count(e => ((time - e.startTime) % schedule.duration + schedule.duration) % schedule.duration < e.lifetime);

    private static void AssertBakedPoses(AlienGroundAuroraController controller, AlienGroundAuroraPatch[] patches)
    {
        foreach (var value in controller.Schedule.events)
        {
            double age = ((controller.PlaybackTime - value.startTime) % controller.Schedule.duration + controller.Schedule.duration) % controller.Schedule.duration;
            if (age >= value.lifetime) continue;
            var patch = patches[value.poolSlot];
            Assert.That(patch.gameObject.activeSelf, Is.True);
            Assert.That(patch.transform.localPosition, Is.EqualTo(value.position));
            Assert.That(Quaternion.Angle(patch.transform.localRotation, value.rotation), Is.LessThan(.05));
            Assert.That(patch.transform.localScale, Is.EqualTo(new Vector3(value.dimensions.x, 1, value.dimensions.y)));
        }
    }

    private static byte[] Capture(Camera camera, string name)
    {
        var target = new RenderTexture(1200, 800, 24); target.Create();
        var previous = RenderTexture.active;
        var image = new Texture2D(1200, 800, TextureFormat.RGB24, false);
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; image.ReadPixels(new Rect(0,0,1200,800),0,0); image.Apply();
            var bytes = image.EncodeToPNG(); Directory.CreateDirectory("Logs/AlienGroundAuroraTuning");
            File.WriteAllBytes("Logs/AlienGroundAuroraTuning/" + name + ".png", bytes); return bytes;
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target); }
    }
}
