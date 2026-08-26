using System.Collections;
using UnityEngine;

public class TargetRail : MonoBehaviour
{
    // =====================================================
    // INSPECTOR
    // =====================================================

    [Header("Targets")]
    [SerializeField]
    private GameObject[] targetPrefabs;

    [Header("Rail Models")]
    [SerializeField]
    private GameObject straightRailPrefab;

    [SerializeField]
    private GameObject endCapPrefab;

    [Header("Rail")]
    [SerializeField]
    [Range(2, 5)]
    private int railLength = 1;

    [SerializeField]
    private float moveSpeed = 2f;

    [Header("State")]
    [SerializeField]
    private PracticeTargetState startingState = PracticeTargetState.Inactive;

    [SerializeField]
    private PracticeTargetState activeCycleState = PracticeTargetState.Active;

    [Header("Automatic State Cycle")]
    [SerializeField]
    private bool autoCycle = true;

    [Tooltip("How long the target stays fully folded down.")]
    [SerializeField]
    private float sleepDuration = 3f;

    [SerializeField]
    private Vector2 activeDurationRange = new Vector2(5f, 10f);

    // =====================================================
    // FIXED RAIL MECHANICS
    // =====================================================

    private const float RailModuleLength = 1f;
    private const float CapGap = 0.042f;
    private const float CarriageLength = 0.24f;
    private const float EndClearance = 0.005f;

    private const float FacingTransitionDuration = 0.5f;

    private const float PlayerStopDistance = 1f;
    private const float PlayerHeightSafetyMultiplier = 1.1f;

    private const float FacingOffset = 0f;

    // =====================================================
    // REFERENCES
    // =====================================================

    private Transform railModels;
    private Transform targetMover;
    private Transform targetMount;

    private Transform spawnedTarget;
    private Transform spawnedHingePivot;

    private PracticeTarget practiceTarget;

    private Transform player;
    private CharacterController playerController;

    private static Transform cachedPlayer;
    private static CharacterController cachedPlayerController;

    // =====================================================
    // STATE
    // =====================================================

    private Quaternion targetBaseLocalRotation;

    private PracticeTargetState currentState;

    private bool movementAndFacingEnabled;
    private bool isTransitioning;

    private Coroutine autoCycleRoutine;
    private Coroutine stateTransitionRoutine;

    // =====================================================
    // MOVEMENT
    // =====================================================

    private Vector3 leftPosition;
    private Vector3 rightPosition;

    private bool movingRight = true;

    // =====================================================
    // PROPERTIES
    // =====================================================

    private bool IsOperational =>
        currentState == PracticeTargetState.Active || currentState == PracticeTargetState.Hardcore;

    private float TotalRailLength => railLength * RailModuleLength;

    private float TargetHingeDuration =>
        practiceTarget != null ? practiceTarget.HingeDuration : 1.6f;

    // =====================================================
    // UNITY
    // =====================================================

    private void Awake()
    {
        FindRequiredObjects();
        FindPlayer();

        BuildRailVisuals();
        SpawnRandomTarget();
        SetupMovement();

        ApplyStartingState();
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

        if (!movementAndFacingEnabled)
            return;

        FacePlayer();
        MoveTarget();
    }

    // =====================================================
    // FIND OBJECTS
    // =====================================================

    private void FindRequiredObjects()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == "RailModels")
            {
                railModels = child;
            }

            if (child.name == "TargetMover")
            {
                targetMover = child;
            }

            if (child.name == "TargetMount")
            {
                targetMount = child;
            }
        }

        if (railModels == null)
        {
            Debug.LogError($"{name}: Could not find 'RailModels'.");
        }

        if (targetMover == null)
        {
            Debug.LogError($"{name}: Could not find 'TargetMover'.");
        }

        if (targetMount == null)
        {
            Debug.LogError($"{name}: Could not find 'TargetMount'.");
        }
    }

    private void FindPlayer()
    {
        if (cachedPlayer == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject == null)
            {
                Debug.LogError($"{name}: Could not find Player.");

                return;
            }

            cachedPlayer = playerObject.transform;

            cachedPlayerController = playerObject.GetComponent<CharacterController>();
        }

        player = cachedPlayer;

        playerController = cachedPlayerController;
    }

    // =====================================================
    // BUILD RAIL
    // =====================================================

    private void BuildRailVisuals()
    {
        if (railModels == null || straightRailPrefab == null || endCapPrefab == null)
        {
            return;
        }

        ClearRailVisuals();

        // -------------------------------------------------
        // STRAIGHT 1M MODULES
        // -------------------------------------------------

        for (int i = 0; i < railLength; i++)
        {
            GameObject railPiece = Instantiate(straightRailPrefab, railModels);

            railPiece.name = $"Rail_{i + 1}";

            railPiece.transform.localPosition = new Vector3(-i * RailModuleLength, 0f, 0f);

            railPiece.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            railPiece.transform.localScale = Vector3.one;
        }

        // -------------------------------------------------
        // RIGHT CAP
        // -------------------------------------------------

        GameObject rightCap = Instantiate(endCapPrefab, railModels);

        rightCap.name = "RailCap_Right";

        rightCap.transform.localPosition = new Vector3(CapGap, 0f, 0f);

        rightCap.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        rightCap.transform.localScale = Vector3.one;

        // -------------------------------------------------
        // LEFT CAP
        // -------------------------------------------------

        GameObject leftCap = Instantiate(endCapPrefab, railModels);

        leftCap.name = "RailCap_Left";

        leftCap.transform.localPosition = new Vector3(-TotalRailLength - CapGap, 0f, 0f);

        leftCap.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f);

        leftCap.transform.localScale = Vector3.one;
    }

    private void ClearRailVisuals()
    {
        if (railModels == null)
            return;

        for (int i = railModels.childCount - 1; i >= 0; i--)
        {
            GameObject child = railModels.GetChild(i).gameObject;

            child.SetActive(false);

            Destroy(child);
        }
    }

    // =====================================================
    // RANDOM TARGET
    // =====================================================

    private void SpawnRandomTarget()
    {
        if (targetMount == null)
            return;

        GameObject selectedPrefab = GetRandomTargetPrefab();

        if (selectedPrefab == null)
        {
            Debug.LogError($"{name}: No valid target prefabs assigned.");

            return;
        }

        GameObject target = Instantiate(selectedPrefab, targetMount);

        target.name = selectedPrefab.name;

        practiceTarget = target.GetComponentInChildren<PracticeTarget>(true);

        if (practiceTarget != null)
        {
            spawnedTarget = practiceTarget.transform;
        }
        else
        {
            spawnedTarget = target.transform;

            Debug.LogError(
                $"{name}: Spawned target '{target.name}' " + "does not contain PracticeTarget."
            );
        }

        spawnedHingePivot = FindChildByName(target.transform, "HingePivot");

        if (spawnedHingePivot == null)
        {
            Debug.LogError($"{name}: Target '{target.name}' " + "does not contain 'HingePivot'.");
        }

        targetBaseLocalRotation = spawnedTarget.localRotation;

        SnapHingeToMount();

        if (practiceTarget != null)
        {
            practiceTarget.Initialize(player);
        }
    }

    private GameObject GetRandomTargetPrefab()
    {
        if (targetPrefabs == null || targetPrefabs.Length == 0)
        {
            return null;
        }

        int validCount = 0;

        foreach (GameObject targetPrefab in targetPrefabs)
        {
            if (targetPrefab != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
            return null;

        int randomIndex = Random.Range(0, validCount);

        foreach (GameObject targetPrefab in targetPrefabs)
        {
            if (targetPrefab == null)
                continue;

            if (randomIndex == 0)
            {
                return targetPrefab;
            }

            randomIndex--;
        }

        return null;
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    // =====================================================
    // HINGE ALIGNMENT
    // =====================================================

    private void SnapHingeToMount()
    {
        if (spawnedTarget == null || spawnedHingePivot == null || targetMount == null)
        {
            return;
        }

        Vector3 offset = targetMount.position - spawnedHingePivot.position;

        spawnedTarget.position += offset;
    }

    private void SetTargetRotationKeepingHinge(Quaternion localRotation)
    {
        if (spawnedTarget == null)
            return;

        spawnedTarget.localRotation = localRotation;

        SnapHingeToMount();
    }

    // =====================================================
    // MOVEMENT SETUP
    // =====================================================

    private void SetupMovement()
    {
        if (targetMover == null)
            return;

        float halfCarriage = CarriageLength * 0.5f;

        float rightX = -halfCarriage - EndClearance;

        float leftX = -TotalRailLength + halfCarriage + EndClearance;

        Vector3 current = targetMover.localPosition;

        leftPosition = new Vector3(leftX, current.y, current.z);

        rightPosition = new Vector3(rightX, current.y, current.z);

        current.x = (leftX + rightX) * 0.5f;

        targetMover.localPosition = current;

        movingRight = true;
    }

    // =====================================================
    // MOVEMENT
    // =====================================================

    private void MoveTarget()
    {
        if (targetMover == null || moveSpeed <= 0f)
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

    // =====================================================
    // PLAYER SAFETY
    // =====================================================

    private bool IsPlayerTooClose()
    {
        if (player == null || spawnedTarget == null)
        {
            return false;
        }

        Vector3 difference = player.position - spawnedTarget.position;

        float horizontalDistance = new Vector2(difference.x, difference.z).magnitude;

        float playerHeight =
            playerController != null ? playerController.height * player.lossyScale.y : 2f;

        float verticalTolerance = playerHeight * PlayerHeightSafetyMultiplier;

        float verticalDistance = Mathf.Abs(difference.y);

        return horizontalDistance <= PlayerStopDistance && verticalDistance <= verticalTolerance;
    }

    // =====================================================
    // FACING
    // =====================================================

    private Quaternion GetPlayerFacingRotation()
    {
        if (player == null || spawnedTarget == null || targetMount == null)
        {
            return targetBaseLocalRotation;
        }

        Vector3 directionToPlayer = player.position - spawnedTarget.position;

        directionToPlayer.y = 0f;

        if (directionToPlayer.sqrMagnitude < 0.001f)
        {
            return spawnedTarget.localRotation;
        }

        Quaternion baseWorldRotation = targetMount.rotation * targetBaseLocalRotation;

        Vector3 baseFacingDirection = baseWorldRotation * Vector3.up;

        baseFacingDirection.y = 0f;

        if (baseFacingDirection.sqrMagnitude < 0.001f)
        {
            return spawnedTarget.localRotation;
        }

        baseFacingDirection.Normalize();

        float angle = Vector3.SignedAngle(
            baseFacingDirection,
            directionToPlayer.normalized,
            Vector3.up
        );

        return targetBaseLocalRotation
            * Quaternion.AngleAxis(angle + FacingOffset, Vector3.forward);
    }

    private void FacePlayer()
    {
        SetTargetRotationKeepingHinge(GetPlayerFacingRotation());
    }

    // =====================================================
    // STARTING STATE
    // =====================================================

    private void ApplyStartingState()
    {
        currentState = startingState;

        movementAndFacingEnabled = startingState != PracticeTargetState.Inactive;

        if (startingState == PracticeTargetState.Inactive)
        {
            SetTargetRotationKeepingHinge(targetBaseLocalRotation);
        }

        if (practiceTarget != null)
        {
            practiceTarget.SetState(startingState, false);
        }
    }

    // =====================================================
    // STATE
    // =====================================================

    public void SetState(PracticeTargetState newState)
    {
        if (stateTransitionRoutine != null)
        {
            StopCoroutine(stateTransitionRoutine);

            stateTransitionRoutine = null;
        }

        if (newState == PracticeTargetState.Inactive)
        {
            stateTransitionRoutine = StartCoroutine(DeactivateRoutine());
        }
        else
        {
            stateTransitionRoutine = StartCoroutine(ActivateRoutine(newState));
        }
    }

    // =====================================================
    // DEACTIVATE
    // =====================================================

    private IEnumerator DeactivateRoutine()
    {
        isTransitioning = true;

        movementAndFacingEnabled = false;

        if (practiceTarget != null)
        {
            practiceTarget.StopFiring();
        }

        // -------------------------------------------------
        // STEP 1:
        // Smoothly return to neutral.
        // -------------------------------------------------

        if (spawnedTarget != null)
        {
            Quaternion startRotation = spawnedTarget.localRotation;

            float elapsed = 0f;

            while (elapsed < FacingTransitionDuration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / FacingTransitionDuration);

                t = SmoothStep(t);

                Quaternion rotation = Quaternion.Slerp(startRotation, targetBaseLocalRotation, t);

                SetTargetRotationKeepingHinge(rotation);

                yield return null;
            }

            SetTargetRotationKeepingHinge(targetBaseLocalRotation);
        }

        // -------------------------------------------------
        // STEP 2:
        // Fold completely down.
        // -------------------------------------------------

        currentState = PracticeTargetState.Inactive;

        if (practiceTarget != null)
        {
            practiceTarget.SetState(PracticeTargetState.Inactive, true);
        }

        yield return new WaitForSeconds(TargetHingeDuration);

        isTransitioning = false;

        stateTransitionRoutine = null;
    }

    // =====================================================
    // ACTIVATE
    // =====================================================

    private IEnumerator ActivateRoutine(PracticeTargetState newState)
    {
        isTransitioning = true;

        movementAndFacingEnabled = false;

        SetTargetRotationKeepingHinge(targetBaseLocalRotation);

        currentState = newState;

        // -------------------------------------------------
        // STEP 1:
        // Raise completely while neutral.
        // -------------------------------------------------

        if (practiceTarget != null)
        {
            practiceTarget.SetState(newState, true);
        }

        yield return new WaitForSeconds(TargetHingeDuration);

        // -------------------------------------------------
        // STEP 2:
        // Smoothly turn toward Amy.
        // -------------------------------------------------

        Quaternion startRotation =
            spawnedTarget != null ? spawnedTarget.localRotation : targetBaseLocalRotation;

        float elapsed = 0f;

        while (elapsed < FacingTransitionDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / FacingTransitionDuration);

            t = SmoothStep(t);

            Quaternion desiredRotation = GetPlayerFacingRotation();

            Quaternion rotation = Quaternion.Slerp(startRotation, desiredRotation, t);

            SetTargetRotationKeepingHinge(rotation);

            yield return null;
        }

        SetTargetRotationKeepingHinge(GetPlayerFacingRotation());

        // -------------------------------------------------
        // STEP 3:
        // Normal live movement / tracking.
        // -------------------------------------------------

        movementAndFacingEnabled = true;

        isTransitioning = false;

        stateTransitionRoutine = null;
    }

    // =====================================================
    // AUTOMATIC STATE CYCLE
    // =====================================================

    private void StartAutoCycle()
    {
        if (autoCycleRoutine != null)
        {
            StopCoroutine(autoCycleRoutine);
        }

        autoCycleRoutine = StartCoroutine(AutoCycleRoutine());
    }

    public void SetAutoCycle(bool enabled)
    {
        autoCycle = enabled;

        if (autoCycleRoutine != null)
        {
            StopCoroutine(autoCycleRoutine);

            autoCycleRoutine = null;
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
            // -------------------------------------------------
            // SLEEP
            // -------------------------------------------------

            if (!IsOperational)
            {
                while (isTransitioning)
                {
                    yield return null;
                }

                yield return new WaitForSeconds(Mathf.Max(0f, sleepDuration));

                PracticeTargetState nextState =
                    activeCycleState == PracticeTargetState.Inactive
                        ? PracticeTargetState.Active
                        : activeCycleState;

                SetState(nextState);

                while (isTransitioning)
                {
                    yield return null;
                }
            }

            // -------------------------------------------------
            // ACTIVE
            // -------------------------------------------------

            if (IsOperational)
            {
                float activeTime = Random.Range(activeDurationRange.x, activeDurationRange.y);

                yield return new WaitForSeconds(Mathf.Max(0f, activeTime));

                SetState(PracticeTargetState.Inactive);

                while (isTransitioning)
                {
                    yield return null;
                }
            }
        }

        autoCycleRoutine = null;
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
