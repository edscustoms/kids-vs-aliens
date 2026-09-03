using System;

[Flags]
public enum EnemyMovementLockReason
{
    None = 0,
    HitReaction = 1 << 0,
    Stun = 1 << 1,
}
