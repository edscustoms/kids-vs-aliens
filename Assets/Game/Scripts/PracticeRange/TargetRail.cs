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

    [Header("Facing")]
    [SerializeField]
    private float facingOffset = 0f;

    private Transform targetMover;
    private Transform targetMount;

    private Transform spawnedTarget;
    private Transform player;

    private Quaternion targetBaseLocalRotation;

    private Vector3 leftPosition;
    private Vector3 rightPosition;
    private CharacterController playerController;

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
        FacePlayer();
        MoveTarget();
    }

    private void FindRequiredObjects()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

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
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
            return;

        player = playerObject.transform;

        playerController = playerObject.GetComponent<CharacterController>();
    }

    private void SpawnTarget()
    {
        if (targetPrefab == null || targetMount == null)
            return;

        GameObject target = Instantiate(targetPrefab, targetMount);

        target.name = targetPrefab.name;

        spawnedTarget = target.transform;

        targetBaseLocalRotation = spawnedTarget.localRotation;
    }

    private void SetupMovement()
    {
        if (targetMover == null)
            return;

        Vector3 startPosition = targetMover.localPosition;

        float halfDistance = travelDistance * 0.5f;

        leftPosition = startPosition + Vector3.left * halfDistance;

        rightPosition = startPosition + Vector3.right * halfDistance;
    }

    private void MoveTarget()
    {
        if (targetMover == null || moveSpeed <= 0f || travelDistance <= 0f)
        {
            return;
        }

        if (IsPlayerTooClose())
            return;

        Vector3 destination = movingRight ? rightPosition : leftPosition;

        targetMover.localPosition = Vector3.MoveTowards(
            targetMover.localPosition,
            destination,
            moveSpeed * Time.deltaTime
        );

        if (Vector3.Distance(targetMover.localPosition, destination) <= 0.001f)
        {
            movingRight = !movingRight;
        }
    }

    private void FacePlayer()
    {
        if (player == null || spawnedTarget == null)
            return;

        Vector3 directionToPlayer = player.position - spawnedTarget.position;

        directionToPlayer.y = 0f;

        if (directionToPlayer.sqrMagnitude < 0.001f)
            return;

        // The cardboard's front direction at its original rotation.
        Quaternion baseWorldRotation = targetMount.rotation * targetBaseLocalRotation;

        Vector3 baseFacingDirection = baseWorldRotation * Vector3.up;

        baseFacingDirection.y = 0f;
        baseFacingDirection.Normalize();

        float angle = Vector3.SignedAngle(
            baseFacingDirection,
            directionToPlayer.normalized,
            Vector3.up
        );

        // ONLY rotate the cardboard around its local Z axis.
        spawnedTarget.localRotation =
            targetBaseLocalRotation * Quaternion.AngleAxis(angle + facingOffset, Vector3.forward);
    }

    private bool IsPlayerTooClose()
    {
        if (player == null || spawnedTarget == null)
        {
            return false;
        }

        Vector3 playerPosition = player.position;

        Vector3 targetPosition = spawnedTarget.position;

        // Only X/Z for horizontal proximity.
        Vector2 playerXZ = new Vector2(playerPosition.x, playerPosition.z);

        Vector2 targetXZ = new Vector2(targetPosition.x, targetPosition.z);

        float horizontalDistance = Vector2.Distance(playerXZ, targetXZ);

        float playerHeight =
            playerController != null ? playerController.height * player.lossyScale.y : 2f;

        float verticalTolerance = playerHeight * 1.1f;

        float verticalDistance = Mathf.Abs(playerPosition.y - targetPosition.y);

        return horizontalDistance <= playerStopDistance && verticalDistance <= verticalTolerance;
    }
}
