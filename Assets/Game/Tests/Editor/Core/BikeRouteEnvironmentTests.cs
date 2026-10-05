using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class BikeRouteEnvironmentTests
{
    [Test]
    public void CorridorNormalsAreHorizontalAndBridgeHasNoTallInvisibleGuard()
    {
        EditorSceneManager.OpenScene(BikeRouteGrayboxTests.ScenePath);
        try
        {
            Physics.SyncTransforms();
            var root = GameObject.Find("LevelGeometry").transform;
            var collision = root.Find("Smooth Corridor Collision"); Assert.That(collision, Is.Not.Null);
            foreach (var collider in collision.GetComponentsInChildren<MeshCollider>().Where(c => c.transform.parent == collision))
            {
                Assert.That(collider.sharedMaterial.bounciness, Is.Zero);
                var mesh = collider.sharedMesh; var vertices = mesh.vertices; var triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var normal = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]).normalized;
                    Assert.That(Mathf.Abs(normal.y), Is.LessThan(.0001f), collider.name + " triangle " + i);
                }
            }
            Assert.That(root.Find("Corridor Boundaries").GetComponentsInChildren<MeshCollider>().Length, Is.Zero, "Overlapping bank colliders and tall invisible bridge safety are retired");
            var guide = Object.FindAnyObjectByType<BikeRouteGuide>();
            var jump = guide.jumps.Single(j => j.path == 5);
            for (float distance = jump.releaseDistance + 12; distance < jump.releaseDistance + 90; distance += 3)
            {
                var sample = guide.At(5, distance); var next = guide.At(5, distance + 3);
                foreach (float height in new[] { 3f, 6f, 9f })
                {
                    var hits = Physics.SphereCastAll(sample.position + Vector3.up * height, .8f, next.forward, 3, ~0, QueryTriggerInteraction.Ignore);
                    Assert.That(hits.Where(h => h.collider.transform.IsChildOf(collision)), Is.Empty, "Flight corridor blocked at " + sample.position);
                }
            }
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
    }

    [Test]
    public void NewDressingKeepsTheDrivingLinesClear()
    {
        EditorSceneManager.OpenScene(BikeRouteGrayboxTests.ScenePath);
        try
        {
            Physics.SyncTransforms(); var guide = Object.FindAnyObjectByType<BikeRouteGuide>();
            var art = GameObject.Find("LevelGeometry").transform.Find("Environment Art");
            for (int path = 0; path < guide.paths.Length; path++)
                for (float s = 8; s < guide.paths[path].Length - 8; s += 6)
                {
                    var sample = guide.At(path, s);
                    var hits = Physics.OverlapBox(sample.position + Vector3.up * 1.5f, new Vector3(sample.halfWidth - .3f, 1.2f, 1.5f),
                        Quaternion.LookRotation(Vector3.ProjectOnPlane(sample.forward, Vector3.up)), ~0, QueryTriggerInteraction.Ignore);
                    Assert.That(hits.Where(c => c.transform.IsChildOf(art)).Select(c => c.name), Is.Empty, "New prop blocks path " + path + " at " + s);
                }
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
    }

    [Test]
    public void PolyHavenMaterialsUseCorrectCategoriesAndLinearPackedMaps()
    {
        foreach (var entry in new[] { ("Wall", "Quarry_Wall_02"), ("Wall", "Dark_Rock"), ("Wall", "Sandstone_Blocks_05"),
            ("Wall", "Sandstone_Blocks_08"), ("Ground", "Forrest_Ground_01"), ("Ground", "Forest_Floor"), ("Ground", "Asphalt_01") })
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/Environment/Materials/" + entry.Item1 + "/M_" + entry.Item2 + ".mat");
            Assert.That(material, Is.Not.Null, entry.Item2); Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            foreach (string slot in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
            {
                var texture = material.GetTexture(slot); Assert.That(texture, Is.Not.Null, entry.Item2 + slot);
                Assert.That(texture.width, Is.EqualTo(2048));
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
                Assert.That(importer.sRGBTexture, Is.EqualTo(slot == "_BaseMap")); Assert.That(importer.mipmapEnabled, Is.True);
                if (slot == "_BumpMap") Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.NormalMap));
            }
        }
    }
}
