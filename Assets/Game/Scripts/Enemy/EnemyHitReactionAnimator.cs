using System.Collections;
using UnityEngine;

/// <summary>
/// Presentation bridge for non-lethal enemy hit reactions.
///
/// While the Hit state plays, EnemyMotor is movement-locked.
/// The brain may keep thinking, but SetDestination/FacePosition are ignored
/// until the reaction finishes.
///
/// Death remains owned by EnemyDeathSequence.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class EnemyHitReactionAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private EnemyHealth health;

    [SerializeField]
    private EnemyMotor motor;

    [SerializeField]
    private Animator animator;

    [Header("Animator")]
    [SerializeField]
    private string hitTrigger = "Hit";

    [SerializeField]
    private string hitStateName = "Hit";

    [SerializeField, Min(0.1f)]
    private float hitStateEnterTimeout = 0.5f;

    private int hitTriggerHash;
    private int hitStateHash;

    private Coroutine hitRoutine;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();

        hitTriggerHash =
            Animator.StringToHash(
                hitTrigger);

        hitStateHash =
            Animator.StringToHash(
                hitStateName);
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnDamaged += PlayHitReaction;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDamaged -= PlayHitReaction;

        if (hitRoutine != null)
        {
            StopCoroutine(
                hitRoutine);

            hitRoutine = null;
        }

        if (motor != null &&
            (health == null ||
             !health.IsDead))
        {
            motor.SetMovementLocked(
                false);
        }
    }

    private void PlayHitReaction()
    {
        if (animator == null ||
            hitTriggerHash == 0 ||
            health == null ||
            health.IsDead)
        {
            return;
        }

        if (hitRoutine != null)
        {
            StopCoroutine(
                hitRoutine);
        }

        hitRoutine =
            StartCoroutine(
                RunHitReaction());
    }

    private IEnumerator RunHitReaction()
    {
        motor?.SetMovementLocked(
            true);

        animator.ResetTrigger(
            hitTriggerHash);

        animator.SetTrigger(
            hitTriggerHash);

        float enterElapsed = 0f;

        // Wait until Animator actually enters Hit.
        while (enterElapsed <
               hitStateEnterTimeout)
        {
            if (health != null &&
                health.IsDead)
            {
                yield break;
            }

            AnimatorStateInfo state =
                animator.GetCurrentAnimatorStateInfo(
                    0);

            if (state.shortNameHash ==
                hitStateHash)
            {
                break;
            }

            enterElapsed +=
                Time.deltaTime;

            yield return null;
        }

        // Wait until the Hit state has played through.
        while (true)
        {
            if (health != null &&
                health.IsDead)
            {
                yield break;
            }

            AnimatorStateInfo state =
                animator.GetCurrentAnimatorStateInfo(
                    0);

            if (state.shortNameHash !=
                hitStateHash)
            {
                break;
            }

            if (!animator.IsInTransition(0) &&
                state.normalizedTime >= 0.95f)
            {
                break;
            }

            yield return null;
        }

        if (health == null ||
            !health.IsDead)
        {
            motor?.SetMovementLocked(
                false);
        }

        hitRoutine = null;
    }

    private void CacheReferences()
    {
        if (health == null)
        {
            health =
                GetComponent<EnemyHealth>();
        }

        if (motor == null)
        {
            motor =
                GetComponent<EnemyMotor>();
        }

        if (animator == null)
        {
            animator =
                GetComponentInChildren<
                    Animator>(true);
        }
    }
}
