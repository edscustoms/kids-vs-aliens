using UnityEngine;

public sealed class VfxPoolToken : MonoBehaviour
{
    public EntityId PoolKey { get; private set; }

    public bool IsInPool { get; private set; }

    public void Initialize(EntityId poolKey)
    {
        PoolKey = poolKey;
        IsInPool = false;
    }

    public void MarkSpawned()
    {
        IsInPool = false;
    }

    public void MarkReleased()
    {
        IsInPool = true;
    }
}
