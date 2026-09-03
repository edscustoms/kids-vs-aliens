using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

[TestFixture]
[Category("Core")]
public class EnemyMovementLockTests
{
    private GameObject enemyObject;
    private EnemyMotor motor;

    [SetUp]
    public void SetUp()
    {
        enemyObject =
            new GameObject("EnemyMovementLockTest");

        enemyObject.AddComponent<NavMeshAgent>();

        motor =
            enemyObject.AddComponent<EnemyMotor>();
    }

    [TearDown]
    public void TearDown()
    {
        if (enemyObject != null)
            Object.DestroyImmediate(enemyObject);
    }

    [Test]
    public void ReleasingHitReaction_DoesNotReleaseActiveStun()
    {
        motor.SetMovementLock(
            EnemyMovementLockReason.HitReaction,
            true);

        motor.SetMovementLock(
            EnemyMovementLockReason.Stun,
            true);

        motor.SetMovementLock(
            EnemyMovementLockReason.HitReaction,
            false);

        Assert.That(motor.MovementLocked, Is.True);
        Assert.That(
            motor.MovementLocks,
            Is.EqualTo(EnemyMovementLockReason.Stun));

        motor.SetMovementLock(
            EnemyMovementLockReason.Stun,
            false);

        Assert.That(motor.MovementLocked, Is.False);
    }
}
