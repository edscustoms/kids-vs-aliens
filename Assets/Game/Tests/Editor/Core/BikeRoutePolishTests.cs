using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class BikeRoutePolishTests
{
    [Test]
    public void SignsAreIsolatedPrimitiveShoulderObjectsAndFeedbackIsExplicit()
    {
        EditorSceneManager.OpenScene(BikeRouteChaseSetup.ScenePath);
        try
        {
            var director = Object.FindAnyObjectByType<BikeRouteChaseDirector>();
            var signs = Object.FindObjectsByType<BikeRouteBreakableSign>();
            var visuals=Object.FindObjectsByType<MeshFilter>().Where(f=>AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith("Assets/Road sign - Big pack/")).ToArray();
            Assert.That(visuals,Is.Not.Empty);
            foreach(var visual in visuals)
                Assert.That(visual.GetComponentInParent<BikeRouteBreakableSign>(),Is.Not.Null,visual.name+" needs the existing sign owner");
            Assert.That(signs.Length, Is.GreaterThan(9), "Existing signs plus modest extra shoulder placements");
            Assert.That(signs.Select(s => s.GetComponent<RunWorldObject>().Id).Distinct().Count(), Is.EqualTo(signs.Length));
            foreach (var sign in signs)
            {
                Assert.That(sign.GetComponent<RunWorldObject>().Id, Is.Not.Empty);
                Assert.That(sign.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader.name.StartsWith("Universal Render Pipeline/"))),
                    Is.True, sign.name + " uses BikeRoute's URP sign presentation, not a vendor legacy shader");
                Assert.That(sign.signBody, Is.Not.Null); Assert.That(sign.signBody.isKinematic, Is.True);
                Assert.That(sign.playerBike,Is.SameAs(director.PlayerBike),sign.name+" explicit pre-physics contact target");
                Assert.That(sign.GetComponentsInChildren<Collider>().All(c => c is BoxCollider), Is.True);
                Assert.That(sign.approach.isTrigger, Is.True); Assert.That(sign.speedFraction, Is.EqualTo(.6f));
                Assert.That(sign.approach.size.x,Is.GreaterThanOrEqualTo(4),sign.name+" side approach before solid contact");
                Assert.That(sign.approach.size.z,Is.GreaterThanOrEqualTo(4),sign.name+" front/rear approach before solid contact");
                if (!sign.name.StartsWith("Shoulder Warning")) continue;
                var sample = director.Guide.Project(sign.transform.position);
                float side = Mathf.Abs(Vector3.Dot(sign.transform.position - sample.position, sample.Right));
                Assert.That(side, Is.GreaterThan(sample.halfWidth + .25f), sign.name + " leaves the main driving line clear");
                Assert.That(Mathf.Abs(sign.transform.position.y - sample.position.y), Is.LessThan(3), sign.name + " remains reachable beside the road");
                var solid = sign.signBody.GetComponent<BoxCollider>();
                foreach (var overlap in Physics.OverlapBox(solid.bounds.center, solid.bounds.extents * .85f))
                {
                    if (overlap.isTrigger || overlap.transform.IsChildOf(sign.transform) || overlap is TerrainCollider) continue;
                    Assert.That(overlap.bounds.max.y, Is.LessThan(sign.transform.position.y + .4f), sign.name + " overlaps " + overlap.name);
                }
            }
            Assert.That(director.PlayerBike.GetComponent<BikeRouteImpactFeedback>().profile,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<CameraFeedbackProfile>(BikeRoutePolishSetup.ImpactProfile)));
            var feedback = new SerializedObject(Object.FindAnyObjectByType<CameraFeedbackController>());
            Assert.That(feedback.FindProperty("mountedPlayer").objectReferenceValue, Is.SameAs(director.Player));
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
    }
}
