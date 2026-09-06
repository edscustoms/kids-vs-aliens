using UnityEngine;

/// <summary>
/// Small shared enemy core.
/// Deliberately does NOT own animation or attack-type-specific behavior.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyActor : MonoBehaviour
{
    [SerializeField]
    private EnemyHealth health;

    [SerializeField]
    private EnemyMotor motor;

    [SerializeField]
    private EnemyPerception perception;

    public EnemyHealth Health => health;
    public EnemyMotor Motor => motor;
    public EnemyPerception Perception => perception;

    public Transform CurrentTarget { get; private set; }

    public bool IsAlive =>
        health != null &&
        health.HealthNormalized > 0f;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    public void SetCurrentTarget(
        Transform target)
    {
        CurrentTarget = target;
    }

    public void ClearCurrentTarget()
    {
        CurrentTarget = null;
    }

    private void CacheReferences()
    {
        if (health == null)
            health = GetComponent<EnemyHealth>();

        if (motor == null)
            motor = GetComponent<EnemyMotor>();

        if (perception == null)
            perception = GetComponent<EnemyPerception>();
    }
}
