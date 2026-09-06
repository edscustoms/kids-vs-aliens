using NUnit.Framework;
using UnityEngine;

[TestFixture]
[Category("Core")]
public class ElectricGrenadeEffectTests
{
    private GameObject effectObject;
    private ElectricGrenadeEffect effect;
    private ElectricGrenadeEffectData data;
    private GameObject owner;

    [SetUp]
    public void SetUp()
    {
        effectObject =
            new GameObject("ElectricGrenadeEffectTest");

        effect =
            effectObject.AddComponent<ElectricGrenadeEffect>();

        data =
            ScriptableObject.CreateInstance<ElectricGrenadeEffectData>();

        data.radius = 5f;
        data.damage = 18f;
        data.stunDuration = 0.75f;
        data.targetMask = 1 << 0;
        data.obstructionMask = 1 << 0;

        owner =
            new GameObject("GrenadeOwner");
    }

    [TearDown]
    public void TearDown()
    {
        if (data != null)
            Object.DestroyImmediate(data);

        if (effectObject != null)
            Object.DestroyImmediate(effectObject);

        if (owner != null)
            Object.DestroyImmediate(owner);

        GrenadeEffectTestTarget[] targets =
            Object.FindObjectsByType<GrenadeEffectTestTarget>();

        for (int i = 0;
             i < targets.Length;
             i++)
        {
            Object.DestroyImmediate(
                targets[i].gameObject);
        }

        GameObject wall =
            GameObject.Find("GrenadeEffectTestWall");

        if (wall != null)
            Object.DestroyImmediate(wall);
    }

    [Test]
    public void Activate_DeduplicatesDamageAndStunAcrossChildColliders()
    {
        GrenadeEffectTestTarget target =
            CreateTarget(
                new Vector3(2f, 0f, 0f),
                twoColliders: true);

        Physics.SyncTransforms();

        effect.Activate(
            data,
            CreateContext());

        Assert.That(target.DamageCount, Is.EqualTo(1));
        Assert.That(target.StunCount, Is.EqualTo(1));
        Assert.That(target.LastHit.Damage, Is.EqualTo(18f));
        Assert.That(target.LastHit.Instigator, Is.SameAs(owner));
    }

    [Test]
    public void Activate_DoesNotDamageThroughPhysicalCover()
    {
        GrenadeEffectTestTarget target =
            CreateTarget(
                new Vector3(3f, 0f, 0f),
                twoColliders: false);

        GameObject wall =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube);

        wall.name =
            "GrenadeEffectTestWall";

        wall.transform.position =
            new Vector3(1.5f, 0f, 0f);

        wall.transform.localScale =
            new Vector3(0.25f, 3f, 3f);

        Physics.SyncTransforms();

        effect.Activate(
            data,
            CreateContext());

        Assert.That(target.DamageCount, Is.Zero);
        Assert.That(target.StunCount, Is.Zero);
    }

    private GrenadeEffectContext CreateContext()
    {
        return new GrenadeEffectContext(
            Vector3.zero,
            owner,
            effectObject);
    }

    private static GrenadeEffectTestTarget CreateTarget(
        Vector3 position,
        bool twoColliders)
    {
        GameObject root =
            new GameObject("GrenadeEffectTestTarget");

        root.transform.position =
            position;

        GrenadeEffectTestTarget target =
            root.AddComponent<GrenadeEffectTestTarget>();

        GameObject firstCollider =
            new GameObject("HitColliderA");

        firstCollider.transform.SetParent(
            root.transform,
            false);

        firstCollider.AddComponent<BoxCollider>();

        if (twoColliders)
        {
            GameObject secondCollider =
                new GameObject("HitColliderB");

            secondCollider.transform.SetParent(
                root.transform,
                false);

            secondCollider.transform.localPosition =
                Vector3.up * 0.25f;

            secondCollider.AddComponent<BoxCollider>();
        }

        return target;
    }
}

public sealed class GrenadeEffectTestTarget :
    MonoBehaviour,
    IDamageable,
    IStunnable
{
    public int DamageCount { get; private set; }
    public int StunCount { get; private set; }
    public HitInfo LastHit { get; private set; }

    public void ReceiveDamage(
        HitInfo hit)
    {
        DamageCount++;
        LastHit = hit;
    }

    public void ApplyStun(
        float duration,
        GameObject source)
    {
        StunCount++;
    }
}
