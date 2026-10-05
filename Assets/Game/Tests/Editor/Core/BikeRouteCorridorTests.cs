using System.Collections.Generic;
using System.Linq;
using EasyRoads3Dv3;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class BikeRouteCorridorTests
{
    [Test]
    public void CrossingShouldersStaySupportedInsideTheParapets()
    {
        var scene = EditorSceneManager.OpenScene(BikeRouteGrayboxTests.ScenePath);
        try
        {
            Physics.SyncTransforms();
            var geometry = scene.GetRootGameObjects().Single(root => root.name == "LevelGeometry").transform;
            var deck = geometry.Find("Crossing Structure/Crossing shoulder deck").GetComponent<MeshCollider>();
            var road = Object.FindObjectsByType<ERModularRoad>(FindObjectsInactive.Include)
                .Single(r => r.name.StartsWith("06_"));
            int samples = 0;
            for (int i = 1; i < road.splinePoints.Count - 1; i += 4)
            {
                Vector3 p = road.splinePoints[i];
                if (p.x < 1180 || p.x > 1270 || p.z < 1545 || p.z > 1760) continue;
                Vector3 forward = road.splinePoints[i + 1] - road.splinePoints[i - 1];
                forward.y = 0;
                Vector3 right = Vector3.Cross(Vector3.up, forward.normalized);
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 shoulder = p + right * (side * 6.5f);
                    Assert.That(deck.Raycast(new Ray(shoulder + Vector3.up, Vector3.down), out var hit, 2),
                        Is.True, "Unsupported crossing shoulder at " + shoulder);
                    Assert.That(hit.point.y, Is.EqualTo(p.y - .18f).Within(.15f));
                }
                samples++;
            }
            Assert.That(samples, Is.GreaterThan(0));
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
    }

    [Test]
    public void RoadMarkingsFollowTheBakedRoadSurfaceWithoutAddingColliders()
    {
        var scene = EditorSceneManager.OpenScene(BikeRouteGrayboxTests.ScenePath);
        try
        {
            Physics.SyncTransforms();
            var asphalt = scene.GetRootGameObjects().Single(root => root.name == "LevelGeometry")
                .transform.Find("BakedAsphalt");
            foreach (Transform road in asphalt)
            {
                var paint = road.Find("Road markings");
                Assert.That(paint, Is.Not.Null, road.name);
                Assert.That(paint.GetComponentsInChildren<Collider>(), Is.Empty);
                var mesh = paint.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertexCount, Is.GreaterThan(0), road.name);
                Assert.That(mesh.subMeshCount, Is.EqualTo(2), "Edges and center share one mesh");
                var surface = road.GetComponent<MeshCollider>();
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i += 13)
                {
                    Vector3 p = paint.TransformPoint(vertices[i]);
                    Assert.That(surface.Raycast(new Ray(p + Vector3.up * .1f, Vector3.down), out var hit, .2f),
                        Is.True, road.name + " paint vertex " + i + " misses its asphalt");
                    Assert.That(p.y - hit.point.y, Is.EqualTo(.025f).Within(.015f), road.name);
                }
            }
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
    }

    [Test]
    public void RouteSidesHaveGroundLevelPhysicalBoundaries()
    {
        EditorSceneManager.OpenScene(BikeRouteGrayboxTests.ScenePath);
        try
        {
            Physics.SyncTransforms();
            var roads = Object.FindObjectsByType<ERModularRoad>(FindObjectsInactive.Include);
            var leaks = new List<string>();
            foreach (var road in roads)
            {
                float half = HalfCorridor(road);
                // Frequent samples inspect the open road, not the generated boundary mesh itself.
                for (int i = 12; i < road.splinePoints.Count - 12; i += 4)
                {
                    var p = road.splinePoints[i];
                    var forward = road.splinePoints[i + 1] - road.splinePoints[i - 1];
                    forward.y = 0;
                    var right = Vector3.Cross(Vector3.up, forward.normalized);
                    foreach (int side in new[] { -1, 1 })
                    {
                        var outside = p + right * (side * (half + 13));
                        // Fork openings into another authored route are intentional.
                        if (roads.Any(other => other != road && other.splinePoints.Any(q =>
                            Mathf.Abs(q.y - outside.y) < 5 && FlatDistance(q, outside) < HalfCorridor(other) + 2)))
                            continue;
                        foreach (float height in new[] { 1.2f })
                            if (!Physics.SphereCast(p + Vector3.up * height, .3f, right * side,
                                out _, half + 14, ~0, QueryTriggerInteraction.Ignore))
                                leaks.Add(road.name + " sample=" + i + " side=" + side + " height=" + height + " at=" + p);
                    }
                }
            }
            Assert.That(leaks, Is.Empty, "Unclosed corridor sides:\n" + string.Join("\n", leaks.Take(30)));
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
    }

    private static float HalfCorridor(ERModularRoad road) => road.roadWidth * .5f
        + (road.name.StartsWith("05_") ? 12 : road.name.StartsWith("03_") ? 2.75f : 3.5f);
    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
}
