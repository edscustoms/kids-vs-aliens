using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField]
    private float moveSpeed = 2f;

    [SerializeField]
    private float stopDistance = 1.5f;

    [Header("Attack")]
    [SerializeField]
    private float attackDamage = 10f;

    [SerializeField]
    private float attackCooldown = 1f;

    [Header("Separation")]
    [SerializeField]
    private float separationRadius = 1f;

    [SerializeField]
    private float separationStrength = 1.5f;

    [SerializeField]
    private LayerMask enemyLayer;

    // Fixed reusable physics buffer.
    // At a 1m separation radius, 16 nearby colliders is already plenty
    // for the current enemy setup and avoids a new Collider[] every Update.
    private readonly Collider[] nearbyEnemyBuffer =
        new Collider[16];

    private Transform player;
    private PlayerHealth playerHealth;

    private float nextAttackTime;

    private void Start()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag(
                "Player"
            );

        if (playerObject != null)
        {
            player =
                playerObject.transform;

            playerHealth =
                playerObject.GetComponent<PlayerHealth>();
        }
    }

    private void Update()
    {
        if (player == null)
            return;

        Vector3 direction =
            player.position
            - transform.position;

        direction.y = 0f;

        float distance =
            direction.magnitude;

        if (distance <= stopDistance)
        {
            Attack();
            return;
        }

        direction.Normalize();

        Vector3 separation =
            Vector3.zero;

        int nearbyEnemyCount =
            Physics.OverlapSphereNonAlloc(
                transform.position,
                separationRadius,
                nearbyEnemyBuffer,
                enemyLayer,
                QueryTriggerInteraction.Collide
            );

        for (
            int i = 0;
            i < nearbyEnemyCount;
            i++
        )
        {
            Collider other =
                nearbyEnemyBuffer[i];

            if (other == null)
                continue;

            EnemyMovement otherEnemy =
                other.GetComponentInParent<EnemyMovement>();

            if (
                otherEnemy == null
                || otherEnemy == this
            )
            {
                continue;
            }

            Vector3 away =
                transform.position
                - otherEnemy.transform.position;

            away.y = 0f;

            float enemyDistance =
                away.magnitude;

            if (enemyDistance < 0.001f)
                continue;

            float strength =
                1f
                - Mathf.Clamp01(
                    enemyDistance
                    / separationRadius
                );

            separation +=
                away.normalized
                * strength;
        }

        Vector3 moveDirection =
            direction
            + separation
            * separationStrength;

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            moveDirection.Normalize();

            transform.position +=
                moveDirection
                * moveSpeed
                * Time.deltaTime;

            transform.forward =
                moveDirection;
        }
    }

    private void Attack()
    {
        if (playerHealth == null)
            return;

        if (Time.time < nextAttackTime)
            return;

        playerHealth.TakeDamage(
            attackDamage
        );

        nextAttackTime =
            Time.time
            + attackCooldown;
    }
}
