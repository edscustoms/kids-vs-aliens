using UnityEngine;

public class TargetRail : MonoBehaviour
{
    [Header("Target")]
    [SerializeField]
    private GameObject targetPrefab;

    [Header("Movement")]
    [SerializeField]
    private float travelDistance = 6f;

    [SerializeField]
    private float moveSpeed = 2f;

    [Header("Player Safety")]
    [SerializeField]
    private float playerStopDistance = 1f;

    private Transform targetMover;
    private Transform targetMount;

    private Transform spawnedTarget;
    private Transform player;

    private Vector3 leftPosition;
    private Vector3 rightPosition;

    private bool movingRight = true;

    private void Awake()
    {
        FindRequiredObjects();
        FindPlayer();
        SpawnTarget();
        SetupMovement();
    }

    private void Update()
    {
        MoveTarget();
    }

    private void FindRequiredObjects()
    {
        Transform[] children =
            GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == "TargetMover")
                targetMover = child;

            if (child.name == "TargetMount")
                targetMount = child;
        }
    }

    private void FindPlayer()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;
    }

    private void SpawnTarget()
    {
        if (targetPrefab == null || targetMount == null)
            return;

        GameObject target =
            Instantiate(targetPrefab, targetMount);

        target.name = targetPrefab.name;

        spawnedTarget = target.transform;
    }

    private void SetupMovement()
    {
        if (targetMover == null)
            return;

        Vector3 startPosition =
            targetMover.localPosition;

        float halfDistance =
            travelDistance * 0.5f;

        leftPosition =
            startPosition + Vector3.left * halfDistance;

        rightPosition =
            startPosition + Vector3.right * halfDistance;
    }

    private void MoveTarget()
    {
        if (
            targetMover == null ||
            moveSpeed <= 0f ||
            travelDistance <= 0f
        )
        {
            return;
        }

        if (IsPlayerTooClose())
            return;

        Vector3 destination =
            movingRight
                ? rightPosition
                : leftPosition;

        targetMover.localPosition =
            Vector3.MoveTowards(
                targetMover.localPosition,
                destination,
                moveSpeed * Time.deltaTime
            );

        if (
            Vector3.Distance(
                targetMover.localPosition,
                destination
            ) <= 0.001f
        )
        {
            movingRight = !movingRight;
        }
    }

    private bool IsPlayerTooClose()
    {
        if (player == null || spawnedTarget == null)
            return false;

        return Vector3.Distance(
            player.position,
            spawnedTarget.position
        ) <= playerStopDistance;
    }
}