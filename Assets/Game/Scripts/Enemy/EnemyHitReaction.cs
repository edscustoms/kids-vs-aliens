using UnityEngine;

/// <summary>
/// Immediate visual enemy hit reaction.
///
/// On a non-lethal plasma impact:
/// - remember only the incoming shot direction for AI investigation
/// - immediately lock movement
/// - enter/restart Hit state on this exact frame
///
/// The Hit state's EnemyHitStateLockBehaviour unlocks movement when the
/// reaction finishes. EnemyBrain then consumes the remembered direction
/// and investigates toward where the shot came from.
///
/// Death remains handled separately by EnemyDeathSequence.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class EnemyHitReaction :
    MonoBehaviour,
    IHitReaction
{
    [Header("References")]
    [SerializeField]
    private EnemyHealth health;

    [SerializeField]
    private EnemyMotor motor;

    [SerializeField]
    private EnemyBrain brain;

    [SerializeField]
    private Animator animator;

    [Header("Animator")]
    [SerializeField]
    private string hitStatePath =
        "Base Layer.Hit";

    [SerializeField, Min(0f)]
    private float transitionDuration =
        0.02f;

    private int hitStateHash;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();

        hitStateHash =
            Animator.StringToHash(
                hitStatePath);
    }

    public void ReceiveHit(
        HitInfo hit)
    {
        // Damage was applied by CombatHitResolver on this exact impact frame.
        // A lethal impact belongs entirely to Death.
        if (health == null ||
            health.IsDead ||
            animator == null ||
            hitStateHash == 0)
        {
            return;
        }

        // Store directional knowledge only.
        // We intentionally do NOT pass hit.Instigator.position to the AI.
        brain?.QueueIncomingShotInvestigation(
            hit.Direction);

        // Lock immediately, before Animator evaluation, so EnemyBrain cannot
        // start the investigation one frame before Hit actually enters.
        motor?.SetMovementLocked(
            true);

        animator.CrossFadeInFixedTime(
            hitStateHash,
            transitionDuration,
            0,
            0f);
    }

    private void CacheReferences()
    {
        if (health == null)
            health =
                GetComponent<EnemyHealth>();

        if (motor == null)
            motor =
                GetComponent<EnemyMotor>();

        if (brain == null)
            brain =
                GetComponent<EnemyBrain>();

        if (animator == null)
        {
            animator =
                GetComponentInChildren<
                    Animator>(true);
        }
    }
}
