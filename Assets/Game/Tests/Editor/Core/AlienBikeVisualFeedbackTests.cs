using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class AlienBikeVisualFeedbackTests
{
    [Test]
    public void NeutralRideableGeometryMatchesSharedModelAndGameplayMarkersStayOutsideLean()
    {
        var shared = AssetDatabase.LoadAssetAtPath<GameObject>(AlienBikeSetup.Prefabs + "/PF_AlienBikeVisual.prefab");
        var flyby = AssetDatabase.LoadAssetAtPath<GameObject>(AlienBikeSetup.Prefabs + "/PF_AlienFlybyBike.prefab");
        var ride = AssetDatabase.LoadAssetAtPath<GameObject>(AlienBikeSetup.Prefabs + "/PF_RideableAlienBike.prefab");
        Assert.That(shared.GetComponentsInChildren<AlienBikeVisualFeedback>(true), Is.Empty);
        Assert.That(flyby.GetComponentsInChildren<AlienBikeVisualFeedback>(true), Is.Empty);
        Assert.That(AssetDatabase.GetAssetPath(shared.GetComponentInChildren<MeshFilter>().sharedMesh), Does.EndWith(".fbx"));
        Assert.That(Triangles(ride), Is.EquivalentTo(Triangles(shared)), "The split preserves every existing triangle and material at neutral");
        var feedback = ride.GetComponent<AlienBikeVisualFeedback>();
        var bike = ride.GetComponent<AlienBikeController>();
        Assert.That(feedback, Is.Not.Null);
        Assert.That(feedback.bikeLeanPivot.GetComponentsInChildren<Collider>(true), Is.Empty);
        Assert.That(feedback.bikeLeanPivot.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
        Assert.That(feedback.steeringPivot.IsChildOf(feedback.bikeLeanPivot), Is.True);
        foreach (var marker in bike.mountApproaches.SelectMany(p => new[] { p.approachPoint, p.mountPoint })
            .Concat(new[] { bike.seatPoint, bike.dismountLeft, bike.dismountRight }))
            Assert.That(marker.IsChildOf(feedback.bikeLeanPivot), Is.False, marker.name);
    }

    [Test]
    public void RepeatFeedbackAuthoringPreservesPivotsAndInspectorTuning()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AlienBikeSetup.Prefabs + "/PF_RideableAlienBike.prefab"));
        try
        {
            var feedback = root.GetComponent<AlienBikeVisualFeedback>();
            var lean = feedback.bikeLeanPivot;
            var steering = feedback.steeringPivot;
            feedback.maxBikeLean = 6;
            feedback.maxRiderLean = 7;
            feedback.maxVisualSteeringAngle = 13;
            feedback.leanResponseSpeed = 5;
            feedback.steeringVisualResponseSpeed = 9;
            feedback.minimumSpeedInfluence = .2f;
            feedback.fullLeanSpeed = 11;
            string before = EditorJsonUtility.ToJson(feedback);
            AlienBikeSetup.ConfigureVisualFeedback(root.GetComponent<AlienBikeController>());
            AlienBikeSetup.ConfigureVisualFeedback(root.GetComponent<AlienBikeController>());
            Assert.That(EditorJsonUtility.ToJson(feedback), Is.EqualTo(before));
            Assert.That(root.GetComponents<AlienBikeVisualFeedback>(), Has.Length.EqualTo(1));
            Assert.That(feedback.bikeLeanPivot, Is.SameAs(lean));
            Assert.That(feedback.steeringPivot, Is.SameAs(steering));
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static List<string> Triangles(GameObject root)
    {
        var result = new List<string>();
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh;
            var matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var vertices = mesh.vertices.Select(v => Vector3Int.RoundToInt(matrix.MultiplyPoint3x4(v) * 1000)).ToArray();
            var materials = filter.GetComponent<MeshRenderer>().sharedMaterials;
            for (int part = 0; part < mesh.subMeshCount; part++)
            {
                var indices = mesh.GetTriangles(part);
                string material = AssetDatabase.GetAssetPath(materials[part]);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var corners = new[] { vertices[indices[i]].ToString(), vertices[indices[i + 1]].ToString(), vertices[indices[i + 2]].ToString() };
                    System.Array.Sort(corners, System.StringComparer.Ordinal);
                    result.Add(material + ":" + string.Join("/", corners));
                }
            }
        }
        return result;
    }
}
