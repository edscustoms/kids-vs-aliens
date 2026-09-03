using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyMotor))]
[RequireComponent(typeof(EnemyHealth))]
public sealed class EnemyStunReceiver :
    MonoBehaviour,
    IStunnable
{
    [SerializeField]
    private EnemyMotor motor;

    [SerializeField]
    private EnemyHealth health;

    private float stunUntil;
    private bool isStunned;

    public bool IsStunned => isStunned;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDied +=
                HandleDeath;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDied -=
                HandleDeath;
        }

        ReleaseStun();
    }

    private void Update()
    {
        if (!isStunned ||
            Time.time < stunUntil)
        {
            return;
        }

        ReleaseStun();
    }

    public void ApplyStun(
        float duration,
        GameObject source)
    {
        if (duration <= 0f ||
            motor == null ||
            health == null ||
            health.IsDead)
        {
            return;
        }

        stunUntil =
            Mathf.Max(
                stunUntil,
                Time.time + duration);

        if (isStunned)
            return;

        isStunned = true;

        motor.SetMovementLock(
            EnemyMovementLockReason.Stun,
            true);
    }

    private void HandleDeath()
    {
        ReleaseStun();
    }

    private void ReleaseStun()
    {
        if (!isStunned)
            return;

        isStunned = false;
        stunUntil = 0f;

        motor?.SetMovementLock(
            EnemyMovementLockReason.Stun,
            false);
    }

    private void CacheReferences()
    {
        if (motor == null)
        {
            motor =
                GetComponent<EnemyMotor>();
        }

        if (health == null)
        {
            health =
                GetComponent<EnemyHealth>();
        }
    }
}
