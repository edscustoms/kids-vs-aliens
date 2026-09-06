using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
[Category("Core")]
public class GrenadeInstanceTests
{
    private GameObject grenadeObject;
    private GameObject owner;
    private GrenadeItemData itemData;
    private ElectricGrenadeEffectData effectData;

    [SetUp]
    public void SetUp()
    {
        grenadeObject =
            new GameObject("GrenadeInstanceTest");

        grenadeObject.AddComponent<SphereCollider>();
        grenadeObject.AddComponent<ElectricGrenadeEffect>();

        owner =
            new GameObject("GrenadeInstanceOwner");

        itemData =
            ScriptableObject.CreateInstance<GrenadeItemData>();

        effectData =
            ScriptableObject.CreateInstance<ElectricGrenadeEffectData>();

        itemData.effectData =
            effectData;
    }

    [TearDown]
    public void TearDown()
    {
        if (itemData != null)
            Object.DestroyImmediate(itemData);

        if (effectData != null)
            Object.DestroyImmediate(effectData);

        if (grenadeObject != null)
            Object.DestroyImmediate(grenadeObject);

        if (owner != null)
            Object.DestroyImmediate(owner);
    }

    [Test]
    public void PreparedArmedGrenade_LaunchesWithRequestedVelocity()
    {
        GrenadeInstance instance =
            grenadeObject.AddComponent<GrenadeInstance>();

        Vector3 velocity =
            new Vector3(2f, 4f, 6f);

        Assert.That(
            instance.TryPrepare(
                itemData,
                owner,
                true,
                new Collider[0],
                0f),
            Is.True);

        Assert.That(
            instance.Launch(
                velocity,
                Vector3.one),
            Is.True);

        Assert.That(instance.IsLaunched, Is.True);
        Assert.That(instance.IsArmed, Is.True);
        Assert.That(
            grenadeObject.GetComponent<Rigidbody>().linearVelocity,
            Is.EqualTo(velocity));
    }

    [Test]
    public void ArmedGrenade_RejectsMismatchedEffectConfiguration()
    {
        GrenadeInstance instance =
            grenadeObject.AddComponent<GrenadeInstance>();

        GrenadeEffectData incompatibleData =
            ScriptableObject.CreateInstance<GrenadeEffectTestData>();

        itemData.effectData =
            incompatibleData;

        try
        {
            LogAssert.Expect(
                LogType.Error,
                "GrenadeInstanceTest: Armed grenade effect configuration does not match its GrenadeItemData.");

            Assert.That(
                instance.TryPrepare(
                    itemData,
                    owner,
                    true,
                    new Collider[0],
                    0f),
                Is.False);
        }
        finally
        {
            Object.DestroyImmediate(
                incompatibleData);
        }
    }
}

public sealed class GrenadeEffectTestData :
    GrenadeEffectData
{
}
