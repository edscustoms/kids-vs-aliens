using UnityEngine;

/// <summary>
/// Simple melee damage source.
/// Uses the existing HitInfo + CombatHitResolver path.
///
/// V1 presentation:
/// - AI decides when the attack happens.
/// - This component triggers the melee animation.
/// - Damage is still applied immediately.
/// - Exact fist-contact timing can move to an Animation Event later.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyMeleeAttack : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField, Min(0.1f)]
    private float attackRange = 1.25f;

    [SerializeField, Min(0f)]
    private float damage = 10f;

    [SerializeField]
    private Vector2 cooldownRange =
        new Vector2(0.9f, 1.2f);

    [Header("Animation")]
    [SerializeField]
    private Animator animator;

    [SerializeField]
    private EnemyMotor motor;

    [SerializeField]
    private string meleeAttackTrigger =
        "MeleeAttack";

    private float nextAttackTime;
    private int meleeAttackTriggerHash;

    public float AttackRange =>
        attackRange;

    private void Reset()
    {
        CacheAnimator();
    }

    private void Awake()
    {
        CacheAnimator();

        meleeAttackTriggerHash =
            Animator.StringToHash(
                meleeAttackTrigger);
    }

    public bool CanAttack(
        Transform target)
    {
        if (target == null ||
            (motor != null &&
             motor.MovementLocked))
        {
            return false;
        }

        Vector3 delta =
            target.position -
            transform.position;

        delta.y = 0f;

        return
            delta.sqrMagnitude <=
            attackRange *
            attackRange;
    }

    public bool TryAttack(
        Transform target)
    {
        if (!CanAttack(target) ||
            (motor != null &&
             motor.MovementLocked) ||
            Time.time < nextAttackTime)
        {
            return false;
        }

        Collider targetCollider =
            FindTargetCollider(target);

        if (targetCollider == null)
            return false;

        Vector3 direction =
            target.position -
            transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude >
            0.0001f)
        {
            direction.Normalize();
        }
        else
        {
            direction =
                transform.forward;
        }

        // V1: animation is presentation only.
        // Damage still happens immediately when the AI attacks.
        if (animator != null &&
            meleeAttackTriggerHash != 0)
        {
            animator.SetTrigger(
                meleeAttackTriggerHash);
        }

        Vector3 hitPoint =
            target.position +
            Vector3.up * 0.9f;

        HitInfo hit =
            new HitInfo(
                damage,
                hitPoint,
                -direction,
                direction,
                gameObject);

        CombatHitResolver.Resolve(
            targetCollider,
            hit);

        float min =
            Mathf.Max(
                0.05f,
                Mathf.Min(
                    cooldownRange.x,
                    cooldownRange.y));

        float max =
            Mathf.Max(
                min,
                Mathf.Max(
                    cooldownRange.x,
                    cooldownRange.y));

        nextAttackTime =
            Time.time +
            Random.Range(
                min,
                max);

        return true;
    }

    private void CacheAnimator()
    {
        if (animator == null)
        {
            animator =
                GetComponentInChildren<
                    Animator>(true);
        }

        if (motor == null)
        {
            motor =
                GetComponent<EnemyMotor>();
        }
    }

    private static Collider FindTargetCollider(
        Transform target)
    {
        Collider direct =
            target.GetComponent<Collider>();

        if (direct != null &&
            direct.enabled &&
            !direct.isTrigger)
        {
            return direct;
        }

        Collider[] colliders =
            target.GetComponentsInChildren<
                Collider>(true);

        foreach (Collider collider in colliders)
        {
            if (collider == null ||
                !collider.enabled ||
                collider.isTrigger)
            {
                continue;
            }

            return collider;
        }

        // Fallback: some targets intentionally use only triggers
        // as their damage hitbox.
        if (direct != null &&
            direct.enabled)
        {
            return direct;
        }

        foreach (Collider collider in colliders)
        {
            if (collider != null &&
                collider.enabled)
            {
                return collider;
            }
        }

        return null;
    }
}
