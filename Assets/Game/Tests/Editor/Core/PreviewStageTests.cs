using NUnit.Framework;
using UnityEngine;

[TestFixture, Category("Core")]
public sealed class PreviewStageTests
{
    [Test]
    public void BoundsUseOnlyModelGeometry_EvenWithHugeElectricArcs()
    {
        GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject effect = new GameObject("Arc");
        effect.transform.SetParent(model.transform);
        var line = effect.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPosition(0, Vector3.one * -1000);
        line.SetPosition(1, Vector3.one * 1000);
        try
        {
            Assert.That(PreviewStageContent.TryGetModelBounds(model, out var bounds), Is.True);
            Assert.That(bounds.size, Is.EqualTo(Vector3.one));
        }
        finally
        {
            Object.DestroyImmediate(model);
        }
    }

    [Test]
    public void SharedStagePreservesActorWhenLoadoutUnchanged_AndClearsIt()
    {
        GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject root = new GameObject("Stage");
        Camera camera = new GameObject("Preview camera").AddComponent<Camera>();
        var stage = new PreviewStageContent(camera, root.transform);
        try
        {
            stage.ShowLoadout(prefab, null);
            GameObject first = stage.CurrentInstance;
            Vector3 cameraPosition = camera.transform.position;
            stage.ShowLoadout(prefab, null);
            Assert.That(stage.CurrentInstance, Is.SameAs(first));
            Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
            stage.Clear();
            Assert.That(stage.CurrentInstance, Is.Null);
            Assert.That(root.transform.childCount, Is.Zero);
        }
        finally
        {
            stage.Clear();
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(camera.gameObject);
        }
    }
}
