using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class BikeRouteBoundaryTests
{
    [Test]
    public void CrossingParapetsMatchTheirVisibleFacesAndLeaveShouldersAndAirLaneOpen()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/BikeRoute.unity");
        try
        {
            Physics.SyncTransforms();
            var geometry=scene.GetRootGameObjects().Single(r=>r.name=="LevelGeometry").transform;
            var root=geometry.Find("Readable boundaries"); Assert.That(root,Is.Not.Null);
            var guide=Object.FindAnyObjectByType<BikeRouteGuide>();
            var walls=root.GetComponentsInChildren<MeshCollider>(); Assert.That(walls,Is.Not.Empty);
            foreach(var wall in walls)
            {
                Assert.That(wall.sharedMesh,Is.SameAs(wall.GetComponent<MeshFilter>().sharedMesh));
                Assert.That(wall.GetComponent<Renderer>().enabled,Is.True);
                Assert.That(wall.convex,Is.False);
                Assert.That(wall.sharedMaterial.bounciness,Is.Zero);
            }
            foreach(float station in new[]{280f,330f,370f,430f,480f,510f}) foreach(int side in new[]{-1,1})
            {
                var at=guide.At(5,station);var ray=new Ray(at.position+Vector3.up*.6f,at.Right*side);
                var hits=walls.Select(w=>(hit:w.Raycast(ray,out var h,12),info:h)).Where(h=>h.hit).ToArray();
                Assert.That(hits.Length,Is.EqualTo(1),"One visible collision face per side");
                Assert.That(hits[0].info.distance,Is.GreaterThan(at.halfWidth+2.4f),"Preserve a drivable shoulder");
                Assert.That(Mathf.Abs(hits[0].info.normal.y),Is.LessThan(.01f),"Wall face must redirect sideways, not upwards");
                foreach(var wall in walls)
                    Assert.That(wall.Raycast(new Ray(at.position+Vector3.up*2,at.Right*side),out _,12),Is.False,"No invisible wall above the visible parapet");
            }
            foreach (var child in root.Cast<Transform>().Where(t => t.name.StartsWith("Shortcut")))
            {
                var sign = child.GetComponent<BikeRouteBreakableSign>();
                Assert.That(sign, Is.Not.Null, "Each shortcut marker uses the existing sign owner");
                Assert.That(sign.signBody.isKinematic, Is.True);
                Assert.That(sign.signBody.GetComponent<BoxCollider>().size.x, Is.LessThan(.25f));
                Assert.That(sign.approach.isTrigger, Is.True);
                Assert.That(sign.speedFraction, Is.EqualTo(.6f));
                Assert.That(sign.speedLoss, Is.EqualTo(.08f));
                Assert.That(sign.playerBike, Is.Not.Null);
            }
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
    }
    [Test]
    public void CrossingDeckHasMetricUvsAndSolidUnderside()
    {
        EditorSceneManager.OpenScene("Assets/Game/Scenes/BikeRoute.unity");
        try
        {
            Physics.SyncTransforms();
            var deck = GameObject.Find("LevelGeometry/Crossing Structure/Crossing shoulder deck").GetComponent<MeshCollider>();
            var guide = Object.FindAnyObjectByType<BikeRouteGuide>();
            Assert.That(deck.sharedMesh, Is.SameAs(deck.GetComponent<MeshFilter>().sharedMesh));
            foreach (float station in new[] { 280f, 350f, 440f, 510f })
            {
                var at = guide.At(5, station);
                Assert.That(deck.Raycast(new Ray(at.position+Vector3.up*2,Vector3.down),out var top,4),Is.True);
                Assert.That(deck.Raycast(new Ray(at.position-Vector3.up*3,Vector3.up),out var bottom,4),Is.True);
                Assert.That(top.point.y-bottom.point.y,Is.EqualTo(.6f).Within(.01f));
                Assert.That(bottom.normal.y,Is.LessThan(-.9f));
            }
            var mesh = deck.sharedMesh;
            for (int i = 0; i < mesh.vertexCount; i++)
                if (mesh.normals[i].y > .9f)
                    Assert.That(Vector2.Distance(mesh.uv[i],new Vector2(mesh.vertices[i].x,mesh.vertices[i].z)*.25f),Is.LessThan(.001f));
            var markers = GameObject.Find("LevelGeometry/Readable boundaries").GetComponentsInChildren<BikeRouteBreakableSign>();
            Assert.That(markers.Select(m=>m.GetComponent<RunWorldObject>().Id).Distinct().Count(),Is.EqualTo(markers.Length));
            Assert.That(markers.Length,Is.EqualTo(12),"Three pairs at each authored shortcut mouth");
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
    }
}
