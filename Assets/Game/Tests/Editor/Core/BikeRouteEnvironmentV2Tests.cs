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
    public void FullRouteBanksUseOneVisiblePhysicalTerrainSurface()
    {
        EditorSceneManager.OpenScene(BikeRouteGrayboxTests.ScenePath);
        try
        {
            Physics.SyncTransforms();
            var root=GameObject.Find("LevelGeometry").transform;
            var terrain=Object.FindAnyObjectByType<Terrain>();
            var collider=terrain.GetComponent<TerrainCollider>();
            Assert.That(collider.terrainData,Is.SameAs(terrain.terrainData));
            Assert.That(root.Find("Smooth Corridor Collision").GetComponentsInChildren<Collider>(),Is.Empty);
            Assert.That(root.Find("Environment Art").Cast<Transform>().Where(t=>t.GetComponent<MeshFilter>()!=null),Is.Empty,
                "No generated facade/column layers over the original Terrain");
            var guide=Object.FindAnyObjectByType<BikeRouteGuide>();
            for(int path=0;path<guide.paths.Length;path++)
            for(float s=12;s<guide.paths[path].Length-12;s+=12)
            foreach(int side in new[]{-1,1})
            {
                var at=guide.At(path,s);
                var p=at.position+at.Right*side*(at.halfWidth+8);
                float y=terrain.SampleHeight(p)+terrain.transform.position.y;
                Assert.That(collider.Raycast(new Ray(new Vector3(p.x,y+10,p.z),Vector3.down),out var hit,12),Is.True,
                    "No terrain collision hole at "+path+":"+s);
                Assert.That(hit.point.y,Is.EqualTo(y).Within(.08f),"Visible and physical bank must agree");
            }
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
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
