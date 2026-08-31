using UnityEngine;

/// <summary>
/// Attach this StateMachineBehaviour to the Animator's Hit state.
///
/// The enemy is movement-locked for exactly as long as the Animator
/// is actually inside Hit. No coroutine timing, no guessed duration.
///
/// If Death interrupts Hit, the motor stays locked because health is dead.
/// </summary>
public sealed class EnemyHitStateLockBehaviour :
    StateMachineBehaviour
{
    private EnemyMotor motor;
    private EnemyHealth health;

    public override void OnStateEnter(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        CacheReferences(animator);

        motor?.SetMovementLocked(
            true);
    }

    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        CacheReferences(animator);

        if (health != null &&
            health.IsDead)
        {
            return;
        }

        motor?.SetMovementLocked(
            false);
    }

    private void CacheReferences(
        Animator animator)
    {
        if (animator == null)
            return;

        if (motor == null)
        {
            motor =
                animator.GetComponentInParent<
                    EnemyMotor>();
        }

        if (health == null)
        {
            health =
                animator.GetComponentInParent<
                    EnemyHealth>();
        }
    }
}
