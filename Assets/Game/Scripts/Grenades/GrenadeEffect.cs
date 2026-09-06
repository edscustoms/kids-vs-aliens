using UnityEngine;

public readonly struct GrenadeEffectContext
{
    public GrenadeEffectContext(
        Vector3 origin,
        GameObject owner,
        GameObject source)
    {
        Origin = origin;
        Owner = owner;
        Source = source;
    }

    public Vector3 Origin { get; }
    public GameObject Owner { get; }
    public GameObject Source { get; }
}

public abstract class GrenadeEffect : MonoBehaviour
{
    public abstract bool Supports(
        GrenadeEffectData data);

    public abstract void Activate(
        GrenadeEffectData data,
        GrenadeEffectContext context);
}
