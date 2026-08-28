using NUnit.Framework;
using UnityEngine;

[TestFixture]
[Category("Core")]
public class CombatHitResolverTests
{
    private GameObject root;
    private GameObject hitObject;
    private BoxCollider hitCollider;

    [SetUp]
    public void SetUp()
    {
        root =
            new GameObject("CombatTestRoot");

        hitObject =
            new GameObject("HitCollider");

        hitObject.transform.SetParent(
            root.transform
        );

        hitCollider =
            hitObject.AddComponent<BoxCollider>();
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void Resolve_ChildColliderAppliesDamageToParentDamageable()
    {
        DamageableProbe damageable =
            root.AddComponent<DamageableProbe>();

        HitInfo hit =
            CreateHit(
                damage: 17f
            );

        IHitReaction reaction =
            CombatHitResolver.Resolve(
                hitCollider,
                hit
            );

        Assert.That(
            damageable.ReceiveCount,
            Is.EqualTo(1)
        );

        Assert.That(
            damageable.LastHit.Damage,
            Is.EqualTo(17f)
        );

        Assert.That(
            damageable.LastHit.Instigator,
            Is.SameAs(root)
        );

        Assert.That(
            reaction,
            Is.Null
        );
    }

    [Test]
    public void Resolve_ReturnsHitReactionWithoutTriggeringItImmediately()
    {
        HitReactionProbe reactionProbe =
            root.AddComponent<HitReactionProbe>();

        HitInfo hit =
            CreateHit(
                damage: 10f
            );

        IHitReaction returnedReaction =
            CombatHitResolver.Resolve(
                hitCollider,
                hit
            );

        Assert.That(
            returnedReaction,
            Is.SameAs(reactionProbe),
            "The visual projectile needs the exact reaction found on the hit target."
        );

        Assert.That(
            reactionProbe.ReceiveCount,
            Is.Zero,
            "CombatHitResolver must return delayed hit reactions, not execute them immediately."
        );

        returnedReaction.ReceiveHit(
            hit
        );

        Assert.That(
            reactionProbe.ReceiveCount,
            Is.EqualTo(1)
        );
    }

    [Test]
    public void Resolve_UnsupportedColliderDoesNothingAndReturnsNull()
    {
        HitInfo hit =
            CreateHit(
                damage: 99f
            );

        IHitReaction reaction =
            null;

        Assert.DoesNotThrow(
            () =>
            {
                reaction =
                    CombatHitResolver.Resolve(
                        hitCollider,
                        hit
                    );
            }
        );

        Assert.That(
            reaction,
            Is.Null
        );
    }

    private HitInfo CreateHit(
        float damage
    )
    {
        return new HitInfo(
            damage,
            new Vector3(1f, 2f, 3f),
            Vector3.up,
            Vector3.forward,
            root
        );
    }
}

public sealed class DamageableProbe :
    MonoBehaviour,
    IDamageable
{
    public int ReceiveCount { get; private set; }

    public HitInfo LastHit { get; private set; }

    public void ReceiveDamage(
        HitInfo hit
    )
    {
        ReceiveCount++;
        LastHit = hit;
    }
}

public sealed class HitReactionProbe :
    MonoBehaviour,
    IHitReaction
{
    public int ReceiveCount { get; private set; }

    public HitInfo LastHit { get; private set; }

    public void ReceiveHit(
        HitInfo hit
    )
    {
        ReceiveCount++;
        LastHit = hit;
    }
}
