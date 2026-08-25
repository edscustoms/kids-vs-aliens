using System.Collections;
using UnityEngine;

public class TargetRail : MonoBehaviour
{
    [Header("Target")]
    [SerializeField]
    private GameObject targetPrefab;

    [Header("State")]
    [SerializeField]
    private PracticeTargetState startingState =
        PracticeTargetState.Inactive;

    [SerializeField]
    private PracticeTargetState activeCycleState =
        PracticeTargetState.Active;

    [Header("Automatic State Cycle")]
    [SerializeField]
    private bool autoCycle = true;

    [SerializeField]
    private Vector2 inactiveDurationRange =
        new Vector2(2f, 5f);

    [SerializeField]
    private Vector2 activeDurationRange =
        new Vector2(5f, 10f);

    [Header("Movement")]
    [SerializeField]
    private float travelDistance = 6f;

    [SerializeField]
    private float moveSpeed = 2f;

    [Header("Player Safety")]
    [SerializeField]
    private float playerStopDistance = 1f;

    [SerializeField]
    private float playerHeightSafetyMultiplier = 1.1f;

    [Header("Facing")]
    [SerializeField]
    private float facingOffset = 0f;

    private Transform targetMover;
    private Transform targetMount;

    private Transform spawnedTarget;
    private PracticeTarget practiceTarget;

    private Transform player;
    private CharacterController playerController;

    private static Transform cachedPlayer;
    private static CharacterController cachedPlayerController;

    private Quaternion targetBaseLocalRotation;

    private Vector3 leftPosition;
    private Vector3 rightPosition;

    private bool movingRight = true;

    private PracticeTargetState currentState;

    private Coroutine autoCycleRoutine;

    private bool IsOperational =>
        currentState == PracticeTargetState.Active ||
        currentState == PracticeTargetState.Hardcore;

    private void Awake()
    {
        FindRequiredObjects();
        FindPlayer();
        SpawnTarget();
        SetupMovement();

        ApplyState(
            startingState,
            false
        );
    }

    private void Start()
    {
        if (autoCycle)
        {
            StartAutoCycle();
        }
    }

    private void Update()
    {
        if (!IsOperational)
            return;

        FacePlayer();
        MoveTarget();
    }

    private void FindRequiredObjects()
    {
        Transform[] children =
            GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == "TargetMover")
            {
                targetMover = child;
            }

            if (child.name == "TargetMount")
            {
                targetMount = child;
            }
        }

        if (targetMover == null)
        {
            Debug.LogError(
                $"{name}: Could not find 'TargetMover'."
            );
        }

        if (targetMount == null)
        {
            Debug.LogError(
                $"{name}: Could not find 'TargetMount'."
            );
        }
    }

    private void FindPlayer()
    {
        if (cachedPlayer == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject == null)
            {
                Debug.LogError(
                    $"{name}: Could not find Player."
                );

                return;
            }

            cachedPlayer =
                playerObject.transform;

            cachedPlayerController =
                playerObject.GetComponent<CharacterController>();
        }

        player =
            cachedPlayer;

        playerController =
            cachedPlayerController;
    }

    private void SpawnTarget()
    {
        if (
            targetPrefab == null ||
            targetMount == null
        )
        {
            return;
        }

        GameObject target =
            Instantiate(
                targetPrefab,
                targetMount
            );

        target.name =
            targetPrefab.name;

        // Search the ENTIRE spawned prefab,
        // not only its top GameObject.
        practiceTarget =
            target.GetComponentInChildren<PracticeTarget>(true);

        if (practiceTarget != null)
        {
            spawnedTarget =
                practiceTarget.transform;
        }
        else
        {
            spawnedTarget =
                target.transform;

            Debug.LogError(
                $"{name}: Spawned target '{target.name}' " +
                $"does not contain a PracticeTarget component."
            );
        }

        targetBaseLocalRotation =
            spawnedTarget.localRotation;
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
            startPosition +
            Vector3.left * halfDistance;

        rightPosition =
            startPosition +
            Vector3.right * halfDistance;
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
            movingRight =
                !movingRight;
        }
    }

    private bool IsPlayerTooClose()
    {
        if (
            player == null ||
            spawnedTarget == null
        )
        {
            return false;
        }

        Vector3 difference =
            player.position -
            spawnedTarget.position;

        float horizontalDistance =
            new Vector2(
                difference.x,
                difference.z
            ).magnitude;

        float playerHeight =
            playerController != null
                ? playerController.height *
                  player.lossyScale.y
                : 2f;

        float verticalTolerance =
            playerHeight *
            playerHeightSafetyMultiplier;

        float verticalDistance =
            Mathf.Abs(
                difference.y
            );

        return
            horizontalDistance <= playerStopDistance &&
            verticalDistance <= verticalTolerance;
    }

    private void FacePlayer()
    {
        if (
            player == null ||
            spawnedTarget == null ||
            targetMount == null
        )
        {
            return;
        }

        Vector3 directionToPlayer =
            player.position -
            spawnedTarget.position;

        directionToPlayer.y = 0f;

        if (directionToPlayer.sqrMagnitude < 0.001f)
            return;

        Quaternion baseWorldRotation =
            targetMount.rotation *
            targetBaseLocalRotation;

        Vector3 baseFacingDirection =
            baseWorldRotation *
            Vector3.up;

        baseFacingDirection.y = 0f;

        if (baseFacingDirection.sqrMagnitude < 0.001f)
            return;

        baseFacingDirection.Normalize();

        float angle =
            Vector3.SignedAngle(
                baseFacingDirection,
                directionToPlayer.normalized,
                Vector3.up
            );

        // Only the cardboard target rotates,
        // around its local Z axis.
        spawnedTarget.localRotation =
            targetBaseLocalRotation *
            Quaternion.AngleAxis(
                angle + facingOffset,
                Vector3.forward
            );
    }

    // -------------------------
    // STATE
    // -------------------------

    public void SetState(
        PracticeTargetState newState
    )
    {
        ApplyState(
            newState,
            true
        );
    }

    private void ApplyState(
        PracticeTargetState newState,
        bool animate
    )
    {
        currentState =
            newState;

        if (practiceTarget != null)
        {
            practiceTarget.SetState(
                currentState,
                animate
            );
        }
    }

    // -------------------------
    // AUTO CYCLE
    // -------------------------

    private void StartAutoCycle()
    {
        if (autoCycleRoutine != null)
        {
            StopCoroutine(
                autoCycleRoutine
            );
        }

        autoCycleRoutine =
            StartCoroutine(
                AutoCycleRoutine()
            );
    }

    public void SetAutoCycle(bool enabled)
    {
        autoCycle =
            enabled;

        if (autoCycleRoutine != null)
        {
            StopCoroutine(
                autoCycleRoutine
            );

            autoCycleRoutine =
                null;
        }

        if (autoCycle)
        {
            StartAutoCycle();
        }
    }

    private IEnumerator AutoCycleRoutine()
    {
        while (autoCycle)
        {
            float waitTime;

            if (IsOperational)
            {
                waitTime =
                    Random.Range(
                        activeDurationRange.x,
                        activeDurationRange.y
                    );
            }
            else
            {
                waitTime =
                    Random.Range(
                        inactiveDurationRange.x,
                        inactiveDurationRange.y
                    );
            }

            yield return new WaitForSeconds(
                waitTime
            );

            if (IsOperational)
            {
                ApplyState(
                    PracticeTargetState.Inactive,
                    true
                );
            }
            else
            {
                PracticeTargetState nextState =
                    activeCycleState ==
                    PracticeTargetState.Inactive
                        ? PracticeTargetState.Active
                        : activeCycleState;

                ApplyState(
                    nextState,
                    true
                );
            }
        }

        autoCycleRoutine = null;
    }
}