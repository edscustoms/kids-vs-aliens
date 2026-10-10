using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class BikeRouteOptimizationTests
{
    Scene scene;
    BikeRouteOptimizationSettings settings;
    byte[] originalScene;

    [SetUp]
    public void OpenReadOnlyFixture()
    {
        originalScene = File.ReadAllBytes(BikeRouteChaseSetup.ScenePath);
        scene = EditorSceneManager.OpenScene(BikeRouteChaseSetup.ScenePath, OpenSceneMode.Single);
        settings = ScriptableObject.CreateInstance<BikeRouteOptimizationSettings>();
        settings.terrainPixelError = 2;
    }

    [TearDown]
    public void DiscardFixture()
    {
        Object.DestroyImmediate(settings);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CollectionAssert.AreEqual(originalScene, File.ReadAllBytes(BikeRouteChaseSetup.ScenePath), "Fixture must never save the production scene.");
    }

    [Test]
    public void AuditIsReadOnlyAndReportsUnsupportedBakedReceivers()
    {
        bool dirty = scene.isDirty;
        var terrain = Object.FindAnyObjectByType<Terrain>();
        float error = terrain.heightmapPixelError;
        var report = BikeRouteOptimization.Audit(scene, settings);
        Assert.That(report.changedObjects, Is.Zero);
        Assert.That(scene.isDirty, Is.EqualTo(dirty));
        Assert.That(terrain.heightmapPixelError, Is.EqualTo(error));
        Assert.That(report.lightingBlockers.Any(s => s.Contains("ignores lightmaps")), Is.True);
    }

    [Test]
    public void ApplyChangesOnlyRenderLodAndSecondApplyIsIdempotent()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        settings.terrainPixelError = Mathf.Approximately(terrain.heightmapPixelError, 2) ? 4 : 2;
        var data = terrain.terrainData;
        var collision = terrain.GetComponent<TerrainCollider>().terrainData;
        var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var positions = transforms.Select(t => t.localToWorldMatrix).ToArray();
        var colliders = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true)).ToArray();
        var colliderStates = colliders.Select(item => EditorJsonUtility.ToJson(item)).ToArray();
        var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        var lightStates = lights.Select(item => EditorJsonUtility.ToJson(item)).ToArray();
        Assert.That(BikeRouteOptimization.Apply(scene, settings).changedObjects, Is.EqualTo(1));
        Assert.That(BikeRouteOptimization.Apply(scene, settings).changedObjects, Is.Zero);
        Assert.That(BikeRouteOptimization.Verify(scene, settings).changes, Is.Empty);
        Assert.That(terrain.heightmapPixelError, Is.EqualTo(settings.terrainPixelError));
        Assert.That(terrain.terrainData, Is.SameAs(data));
        Assert.That(terrain.GetComponent<TerrainCollider>().terrainData, Is.SameAs(collision));
        CollectionAssert.AreEqual(positions, transforms.Select(t => t.localToWorldMatrix).ToArray());
        CollectionAssert.AreEqual(colliderStates, colliders.Select(item => EditorJsonUtility.ToJson(item)).ToArray());
        CollectionAssert.AreEqual(lightStates, lights.Select(item => EditorJsonUtility.ToJson(item)).ToArray());
    }

    [Test]
    public void UnsafeBakeAndInvalidSettingsAreRefusedBeforeMutation()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        float error = terrain.heightmapPixelError;
        Assert.Throws<InvalidOperationException>(() => BikeRouteOptimization.Bake(scene, settings));
        Assert.That(Lightmapping.isRunning, Is.False);
        Assert.That(terrain.heightmapPixelError, Is.EqualTo(error));
        settings.terrainPixelError = float.NaN;
        Assert.Throws<InvalidOperationException>(() => BikeRouteOptimization.Apply(scene, settings));
        settings.terrainPixelError = 0;
        Assert.Throws<InvalidOperationException>(() => BikeRouteOptimization.Apply(scene, settings));
        Assert.That(scene.isDirty, Is.False);
    }

    [Test]
    public void OtherScenesAreRefused()
    {
        var other = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Assert.Throws<InvalidOperationException>(() => BikeRouteOptimization.Apply(other, settings));
        Assert.That(other.isDirty, Is.False);
    }
}

