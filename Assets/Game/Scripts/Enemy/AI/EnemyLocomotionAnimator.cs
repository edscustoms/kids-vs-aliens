using UnityEngine;

/// <summary>
/// OPTIONAL presentation bridge for the current humanoid locomotion controller.
/// Not referenced by EnemyActor, so ranged/melee/special enemies can use
/// completely different animation drivers later.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyLocomotionAnimator : MonoBehaviour
{
    [SerializeField]
    private EnemyMotor motor;

    [SerializeField]
    private Animator animator;

    [Header("Current HumanoidShooter parameters")]
    [SerializeField]
    private string moveXParameter = "MoveX";

    [SerializeField]
    private string moveYParameter = "MoveY";

    [SerializeField]
    private string weaponStyleParameter =
        "WeaponStyle";

    [SerializeField]
    private int weaponStyle = 0;

    [Header("Variation")]
    [SerializeField]
    private Vector2 animatorSpeedRange =
        new Vector2(0.96f, 1.04f);

    [SerializeField, Min(0f)]
    private float damping = 0.08f;

    private void Reset()
    {
        motor =
            GetComponent<EnemyMotor>();

        animator =
            GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (motor == null)
            motor =
                GetComponent<EnemyMotor>();

        if (animator == null)
            animator =
                GetComponentInChildren<Animator>();

        if (animator == null)
            return;

        animator.SetInteger(
            weaponStyleParameter,
            weaponStyle);

        float min =
            Mathf.Min(
                animatorSpeedRange.x,
                animatorSpeedRange.y);

        float max =
            Mathf.Max(
                animatorSpeedRange.x,
                animatorSpeedRange.y);

        animator.speed =
            Random.Range(
                min,
                max);
    }

    private void Update()
    {
        if (animator == null ||
            motor == null)
        {
            return;
        }

        Vector3 velocity =
            motor.Velocity;

        velocity.y = 0f;

        Vector3 localVelocity =
            transform.InverseTransformDirection(
                velocity);

        float denominator =
            Mathf.Max(
                0.01f,
                motor.MoveSpeed);

        Vector2 movement =
            new Vector2(
                localVelocity.x,
                localVelocity.z) /
            denominator;

        movement =
            Vector2.ClampMagnitude(
                movement,
                1f);

        animator.SetFloat(
            moveXParameter,
            movement.x,
            damping,
            Time.deltaTime);

        animator.SetFloat(
            moveYParameter,
            movement.y,
            damping,
            Time.deltaTime);
    }
}
