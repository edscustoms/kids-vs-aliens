using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class BikeRouteEnvironmentV2Tests
{
    [Test]
    public void GreenEntranceUsesOriginalTerrainAndHasNoOverlayCollision()
    {
        EditorSceneManager.OpenScene(BikeRouteGrayboxTests.ScenePath);
        try
        {
            Physics.SyncTransforms();
            var root=GameObject.Find("LevelGeometry").transform;
            var guide=Object.FindAnyObjectByType<BikeRouteGuide>();
            var terrain=Object.FindAnyObjectByType<TerrainCollider>();
            var collision=root.Find("Smooth Corridor Collision");
            foreach(string side in new[]{"Left","Right"})
            {
                string prefix="04_Wash_Dirt_"+side+"_0";
                Assert.That(root.Find("Environment Art/"+prefix),Is.Null);
                Assert.That(root.Find("Environment Art/"+prefix+"_BasaltColumns"),Is.Null);
                Assert.That(collision.Find("04_Wash_Dirt "+side+" upper surfaces/"+prefix+"_UpperCollision"),Is.Null);
            }
            foreach(float s in new[]{4f,20f,40f,60f})
            foreach(int side in new[]{-1,1})
            {
                var at=guide.At(3,s);var ray=new Ray(at.position+Vector3.up*1.2f,at.Right*side);
                Assert.That(terrain.Raycast(ray,out _,30),Is.True,"Original physical bank at "+s);
                foreach(var wall in collision.GetComponentsInChildren<MeshCollider>())
                    Assert.That(wall.Raycast(ray,out _,12),Is.False,"No hidden overlay collision at "+s+": "+wall.name);
            }
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
    }
    [Test]
    public void CleanedCutsUseTerrainWithoutDuplicateWallMeshes()
    {
        EditorSceneManager.OpenScene(BikeRouteGrayboxTests.ScenePath);
        try
        {
            Physics.SyncTransforms();
            var root = GameObject.Find("LevelGeometry").transform;
            var terrain = Object.FindAnyObjectByType<Terrain>();
            Assert.That(terrain.GetComponent<TerrainCollider>().terrainData, Is.SameAs(terrain.terrainData));
            foreach (string prefix in new[] { "02_LongSweep_Asphalt", "03_NarrowS_Asphalt", "A_DirectWash_Shortcut_Dirt" })
            {
                Assert.That(root.Find("Environment Art").Cast<Transform>().Where(t => t.name.StartsWith(prefix+"_")), Is.Empty);
                Assert.That(root.Find("Smooth Corridor Collision").Cast<Transform>().Where(t => t.name.StartsWith(prefix)), Is.Empty);
            }
            var guide = Object.FindAnyObjectByType<BikeRouteGuide>();
            foreach (var spot in new[] { (0, 420f), (1, 220f), (2, 200f), (8, 150f) })
            foreach (int side in new[] { -1, 1 })
            {
                var at = guide.At(spot.Item1, spot.Item2);
                Assert.That(terrain.GetComponent<TerrainCollider>().Raycast(new Ray(at.position+Vector3.up*1.2f,at.Right*side),out _,30),
                    Is.True, "Original visible terrain must remain physical at " + spot);
            }
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
    }

    [Test]
    public void VisibleCliffFeetShareTheirPhysicalBoundaryAcrossMaterialTransitions()
    {
        EditorSceneManager.OpenScene(BikeRouteGrayboxTests.ScenePath);
        try
        {
            Physics.SyncTransforms();
            var root = GameObject.Find("LevelGeometry").transform;
            var walls = root.Find("Smooth Corridor Collision").GetComponentsInChildren<MeshCollider>();
            int checkedFaces = 0;
            foreach (Transform cliff in root.Find("Environment Art"))
            {
                var filter = cliff.GetComponent<MeshFilter>();
                if (filter == null || cliff.name.Contains("BasaltColumns")) continue;
                var mesh = filter.sharedMesh; var vertices = mesh.vertices; var triangles = mesh.triangles;
                if (!cliff.name.Contains("_Left_") && !cliff.name.Contains("_Right_")) continue;
                Assert.That(cliff.GetComponent<Renderer>().sharedMaterial.shader.name, Is.EqualTo("Environment/Cliff Blend"));
                // First quad of each ten-vertex row is the grounded collision face.
                for (int i = 0; i < triangles.Length; i += 54)
                {
                    var a = vertices[triangles[i]]; var b = vertices[triangles[i+1]]; var c = vertices[triangles[i+2]];
                    var normal = Vector3.Cross(b-a,c-a).normalized;
                    var point = (a+b+c)/3;
                    bool matched = walls.Any(w => w.Raycast(new Ray(point + normal * 2, -normal), out var hit, 2.08f)
                        && Mathf.Abs(hit.distance - 2) < .08f);
                    Assert.That(matched, Is.True, cliff.name + " facade has no matching physical foot at " + point);
                    checkedFaces++;
                }
                var colors = mesh.colors;
                for (int i = 10; i < colors.Length; i++)
                {
                    Assert.That(Mathf.Abs(colors[i].r-colors[i-10].r), Is.LessThan(.07f));
                    Assert.That(Mathf.Abs(colors[i].g-colors[i-10].g), Is.LessThan(.07f));
                }
            }
            Assert.That(checkedFaces, Is.GreaterThan(100), "Sample actual rendered lower faces across the route");
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
    }

    [Test]
    public void DesertBuildingVariantsAreReusableOpaqueLowPolyPrefabs()
    {
        foreach (var name in new[] { "DuneStepHouse", "OasisShop", "RedClayTownhouse", "ShadePorchHouse", "BlueShutterHouse", "RouteServiceHall", "CornerMarket", "ArchedWorkshop" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Environment/Buildings/BikeRoute/PF_" + name + ".prefab");
            Assert.That(prefab, Is.Not.Null, name); Assert.That(prefab.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length/3), Is.LessThan(1800));
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.sharedMaterials)
                { Assert.That(material, Is.Not.Null); Assert.That(material.renderQueue, Is.LessThan(2501)); }
        }
    }
}
