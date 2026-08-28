using UnityEngine;

public readonly struct HitInfo
{
    public float Damage { get; }
    public Vector3 Point { get; }
    public Vector3 Normal { get; }
    public Vector3 Direction { get; }
    public GameObject Instigator { get; }

    public HitInfo(
        float damage,
        Vector3 point,
        Vector3 normal,
        Vector3 direction,
        GameObject instigator
    )
    {
        Damage = damage;
        Point = point;
        Normal = normal;
        Direction = direction;
        Instigator = instigator;
    }
}
