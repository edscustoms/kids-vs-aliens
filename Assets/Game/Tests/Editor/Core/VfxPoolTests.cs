using NUnit.Framework;
using UnityEngine;

[TestFixture]
[Category("Core")]
public class VfxPoolTests
{
    private GameObject prefabAObject;
    private GameObject prefabBObject;

    private PooledVfxProbe prefabA;
    private PooledVfxProbe prefabB;

    [SetUp]
    public void SetUp()
    {
        prefabAObject =
            new GameObject("VfxPoolTestPrefabA");

        prefabA =
            prefabAObject.AddComponent<PooledVfxProbe>();

        prefabBObject =
            new GameObject("VfxPoolTestPrefabB");

        prefabB =
            prefabBObject.AddComponent<PooledVfxProbe>();
    }

    [TearDown]
    public void TearDown()
    {
        GameObject poolRoot =
            GameObject.Find("[VFX Pool]");

        if (poolRoot != null)
            Object.DestroyImmediate(poolRoot);

        if (prefabAObject != null)
            Object.DestroyImmediate(prefabAObject);

        if (prefabBObject != null)
            Object.DestroyImmediate(prefabBObject);
    }

    [Test]
    public void SpawnReleaseSpawn_ReusesMatchingPrefabWithoutCrossingPools()
    {
        PooledVfxProbe firstA =
            VfxPool.Spawn(
                prefabA,
                Vector3.zero,
                Quaternion.identity
            );

        Assert.That(
            firstA,
            Is.Not.Null
        );

        VfxPool.Release(
            firstA
        );

        Assert.That(
            firstA.gameObject.activeSelf,
            Is.False
        );

        PooledVfxProbe secondA =
            VfxPool.Spawn(
                prefabA,
                Vector3.one,
                Quaternion.identity
            );

        Assert.That(
            secondA,
            Is.SameAs(firstA),
            "A released VFX instance should be reused for the same prefab."
        );

        PooledVfxProbe firstB =
            VfxPool.Spawn(
                prefabB,
                Vector3.zero,
                Quaternion.identity
            );

        Assert.That(
            firstB,
            Is.Not.SameAs(secondA),
            "Different VFX prefabs must never share the same pool instance."
        );

        VfxPool.Release(
            secondA
        );

        VfxPool.Release(
            firstB
        );
    }
}

public sealed class PooledVfxProbe :
    MonoBehaviour
{
}
