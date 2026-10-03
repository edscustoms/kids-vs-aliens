using System;

[Flags]
public enum EnemyMovementLockReason
{
    None = 0,
    HitReaction = 1 << 0,
    Stun = 1 << 1,
    MeleeAttack = 1 << 2,
    ExternalImpact = 1 << 3,
}
