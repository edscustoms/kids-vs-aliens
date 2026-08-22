using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float stopDistance = 1.5f;

    [Header("Attack")]
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1f;

    [Header("Separation")]
    [SerializeField] private float separationRadius = 1f;
    [SerializeField] private float separationStrength = 1.5f;
    [SerializeField] private LayerMask enemyLayer;

    private Transform player;
    private PlayerHealth playerHealth;

    private float nextAttackTime;

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
            playerHealth = playerObject.GetComponent<PlayerHealth>();
        }
    }

    private void Update()
    {
        if (player == null)
            return;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance <= stopDistance)
        {
            Attack();
            return;
        }

        direction.Normalize();

        Vector3 separation = Vector3.zero;

        Collider[] nearbyEnemies = Physics.OverlapSphere(
            transform.position,
            separationRadius,
            enemyLayer,
            QueryTriggerInteraction.Collide
        );

        foreach (Collider other in nearbyEnemies)
        {
            EnemyMovement otherEnemy =
                other.GetComponentInParent<EnemyMovement>();

            if (otherEnemy == null || otherEnemy == this)
                continue;

            Vector3 away =
                transform.position - otherEnemy.transform.position;

            away.y = 0f;

            float enemyDistance = away.magnitude;

            if (enemyDistance < 0.001f)
                continue;

            // Much stronger when enemies are very close
            float strength =
                1f - Mathf.Clamp01(enemyDistance / separationRadius);

            separation += away.normalized * strength;
        }

        Vector3 moveDirection =
            direction + separation * separationStrength;

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            moveDirection.Normalize();

            transform.position +=
                moveDirection * moveSpeed * Time.deltaTime;

            transform.forward = moveDirection;
        }
    }

    private void Attack()
    {
        if (playerHealth == null)
            return;

        if (Time.time < nextAttackTime)
            return;

        playerHealth.TakeDamage(attackDamage);

        nextAttackTime = Time.time + attackCooldown;
    }
}