using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAim : MonoBehaviour
{
    // =====================================================
    // GENERAL
    // =====================================================

    [Header("General")]
    [SerializeField]
    private Camera mainCamera;

    [SerializeField]
    private FadeWhenBlockingPlayer fadeWhenBlockingPlayer;

    // =====================================================
    // DESKTOP
    // =====================================================

    [Header("Desktop Aim")]
    [SerializeField]
    private float desktopRotationSpeed = 20f;

    [Tooltip(
        "How long mouse aim keeps priority after the mouse stops moving. "
            + "A tiny grace period prevents facing from flickering between mouse aim and movement."
    )]
    [SerializeField, Min(0f)]
    private float desktopAimGraceTime = 0.15f;

    [Tooltip("Mouse delta smaller than this is treated as no deliberate aiming input.")]
    [SerializeField, Min(0f)]
    private float desktopMouseDeltaThreshold = 0.25f;

    [Header("Passive Movement Facing")]
    [Tooltip(
        "When there is no active manual aim and no mobile auto-aim lock, "
            + "Amy rotates toward the actual camera-relative movement direction."
    )]
    [SerializeField]
    private float movementFacingRotationSpeed = 8f;

    [Tooltip("Movement input below this magnitude does not change facing.")]
    [Range(0f, 0.95f)]
    [SerializeField]
    private float movementFacingDeadZone = 0.10f;

    // =====================================================
    // MOBILE
    // =====================================================

    [Header("Mobile Auto Aim")]
    [Tooltip("Maximum world-space distance at which Amy may acquire or keep an auto-aim target.")]
    [SerializeField]
    private float autoAimRange = 15f;

    [Tooltip("How quickly Amy naturally rotates toward the current mobile target.")]
    [SerializeField]
    private float mobileRotationSpeed = 5f;

    [Tooltip(
        "Distance used for mobile shots when there is no auto-aim target. "
            + "The exact value only creates a straight-forward aim direction."
    )]
    [SerializeField]
    private float mobileNoTargetAimDistance = 50f;

    [SerializeField]
    private MobileAimSettings mobileAimSettings;

    [Header("Mobile Free Look")]
    [Tooltip("Right-stick input below this magnitude is ignored.")]
    [Range(0f, 0.95f)]
    [SerializeField]
    private float freeLookDeadZone = 0.20f;

    [Tooltip("How quickly Amy rotates toward the free-look direction.")]
    [SerializeField]
    private float freeLookRotationSpeed = 8f;

    [Tooltip(
        "How close a visible target must be to the free-look direction before "
            + "that target can be selected."
    )]
    [Range(1f, 90f)]
    [SerializeField]
    private float freeLookTargetAngle = 35f;

    // =====================================================
    // PUBLIC
    // =====================================================

    public Vector3 AimPoint { get; private set; }

    public bool HasAimPoint { get; private set; }

    public AimTarget CurrentTarget { get; private set; }

    // =====================================================
    // CACHED
    // =====================================================

    private CharacterController characterController;
    private StarterAssetsInputs starterAssetsInputs;

    private Plane groundPlane;
    private int aimMask;

    private readonly RaycastHit[] mouseAimHits = new RaycastHit[32];

    // Visibility rays may pass through explicitly vision-transparent geometry
    // such as chain-link fences. RaycastNonAlloc results are not ordered, so
    // we collect several hits and resolve the nearest real blocker ourselves.
    private readonly RaycastHit[] visibilityHits = new RaycastHit[32];

    private readonly Vector3[] mobileVisibilitySamples = new Vector3[5];

    // =====================================================
    // DESKTOP AIM INTENT STATE
    // =====================================================

    private float lastDesktopAimIntentTime = float.NegativeInfinity;

    // =====================================================
    // MOBILE FREE-LOOK STATE
    // =====================================================

    private bool isFreeLooking;

    // One deliberate joystick push can switch at most one target.
    // Returning the stick to the deadzone re-arms switching.
    private bool manualSwitchConsumed;

    // During free-look this is the target that the stick is actually pointing
    // toward. It is intentionally separate from CurrentTarget so Amy can look
    // away from a sticky lock without losing that lock.
    private AimTarget freeLookShotTarget;

    // =====================================================
    // INITIALIZATION
    // =====================================================

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (fadeWhenBlockingPlayer == null && mainCamera != null)
        {
            fadeWhenBlockingPlayer = mainCamera.GetComponent<FadeWhenBlockingPlayer>();
        }

        characterController = GetComponent<CharacterController>();
        starterAssetsInputs = GetComponent<StarterAssetsInputs>();

        groundPlane = new Plane(Vector3.up, Vector3.zero);

        // Ignore Player-layer colliders for aim / LOS raycasts.
        aimMask = ~LayerMask.GetMask("Player");
    }

    // =====================================================
    // UPDATE
    // =====================================================

    private void LateUpdate()
    {
        if (InputModeController.IsMobile)
        {
            MobileAim();
            return;
        }

        CurrentTarget = null;
        freeLookShotTarget = null;
        isFreeLooking = false;
        manualSwitchConsumed = false;

        UpdateDesktopAimIntent();

        bool hasActiveMouseAim = HasActiveDesktopAimIntent();

        // Always keep the desktop AimPoint current so shooting still uses
        // the cursor correctly. Character rotation only follows the mouse
        // while the player is actively aiming.
        MouseAim(hasActiveMouseAim);

        if (!hasActiveMouseAim)
        {
            RotateTowardsMovementInput();
        }
    }

    // =====================================================
    // DESKTOP AIM
    // =====================================================

    private void MouseAim(bool rotateCharacter)
    {
        if (Mouse.current == null || mainCamera == null)
        {
            HasAimPoint = false;
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);

        int hitCount = Physics.RaycastNonAlloc(
            ray,
            mouseAimHits,
            1000f,
            aimMask,
            QueryTriggerInteraction.Collide
        );

        bool foundHit = false;
        RaycastHit closestHit = default;
        float closestDistance = Mathf.Infinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = mouseAimHits[i];

            if (hit.collider == null)
                continue;

            // Chain-link fences and similar geometry may remain physically
            // solid while being transparent to aiming/vision.
            if (IsVisionTransparent(hit.collider))
                continue;

            Renderer renderer = hit.collider.GetComponent<Renderer>();

            if (renderer == null)
            {
                renderer = hit.collider.GetComponentInParent<Renderer>();
            }

            if (
                renderer != null
                && fadeWhenBlockingPlayer != null
                && fadeWhenBlockingPlayer.IsBlockingPlayer(renderer)
            )
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                foundHit = true;
            }
        }

        if (foundHit)
        {
            AimPoint = closestHit.point;
            HasAimPoint = true;

            if (rotateCharacter)
            {
                RotateTowards(AimPoint, desktopRotationSpeed);
            }

            return;
        }

        if (groundPlane.Raycast(ray, out float distance))
        {
            AimPoint = ray.GetPoint(distance);
            HasAimPoint = true;

            if (rotateCharacter)
            {
                RotateTowards(AimPoint, desktopRotationSpeed);
            }

            return;
        }

        HasAimPoint = false;
    }

    private void UpdateDesktopAimIntent()
    {
        if (Mouse.current == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float thresholdSquared = desktopMouseDeltaThreshold * desktopMouseDeltaThreshold;

        bool mouseMoved = mouseDelta.sqrMagnitude > thresholdSquared;

        // Clicking/shooting is also deliberate mouse aim intent even if the
        // cursor itself did not move on this exact frame.
        bool mouseAction = Mouse.current.leftButton.isPressed;

        if (mouseMoved || mouseAction)
        {
            lastDesktopAimIntentTime = Time.unscaledTime;
        }
    }

    private bool HasActiveDesktopAimIntent()
    {
        return Time.unscaledTime - lastDesktopAimIntentTime <= desktopAimGraceTime;
    }

    // =====================================================
    // MOBILE AIM
    // =====================================================

    private void MobileAim()
    {
        Vector2 freeLookInput =
            starterAssetsInputs != null ? starterAssetsInputs.look : Vector2.zero;

        float deadZoneSquared = freeLookDeadZone * freeLookDeadZone;
        bool hasFreeLookInput = freeLookInput.sqrMagnitude > deadZoneSquared;

        // -------------------------------------------------
        // FREE LOOK / MANUAL TARGET SELECTION
        // -------------------------------------------------

        if (hasFreeLookInput)
        {
            isFreeLooking = true;

            Vector3 desiredDirection = GetCameraRelativeFreeLookDirection(freeLookInput);

            if (desiredDirection.sqrMagnitude > 0.001f)
            {
                RotateTowardsDirection(desiredDirection, freeLookRotationSpeed);
            }

            // The stick can temporarily point away from CurrentTarget without
            // destroying the sticky lock.
            freeLookShotTarget = null;
            HasAimPoint = false;

            if (
                desiredDirection.sqrMagnitude > 0.001f
                && FindBestMobileTargetInDirection(
                    desiredDirection,
                    out Vector3 directionalVisiblePoint
                )
                    is AimTarget directionalTarget
            )
            {
                // This target is genuinely under the current free-look intent.
                // Shots may use it while the stick is held.
                freeLookShotTarget = directionalTarget;
                AimPoint = directionalVisiblePoint;
                HasAimPoint = true;

                // One target switch per deliberate joystick push.
                if (!manualSwitchConsumed && directionalTarget != CurrentTarget)
                {
                    CurrentTarget = directionalTarget;
                    manualSwitchConsumed = true;
                }
            }

            return;
        }

        // Stick returned to neutral: a new deliberate push may switch again.
        isFreeLooking = false;
        freeLookShotTarget = null;
        manualSwitchConsumed = false;

        // -------------------------------------------------
        // NORMAL STICKY AUTO AIM
        // -------------------------------------------------

        if (
            CurrentTarget != null
            && TryGetValidMobileTargetPoint(CurrentTarget, out Vector3 currentVisiblePoint)
        )
        {
            ApplyMobileTarget(currentVisiblePoint);
            return;
        }

        CurrentTarget = FindBestMobileTarget(out Vector3 newVisiblePoint);

        if (CurrentTarget == null)
        {
            HasAimPoint = false;

            // No right-stick intent and no auto-aim target:
            // movement becomes the natural facing intent.
            RotateTowardsMovementInput();
            return;
        }

        ApplyMobileTarget(newVisiblePoint);
    }

    private void ApplyMobileTarget(Vector3 visiblePoint)
    {
        AimPoint = visiblePoint;
        HasAimPoint = true;

        RotateTowards(AimPoint, mobileRotationSpeed);
    }

    // =====================================================
    // MOBILE FREE LOOK
    // =====================================================

    private Vector3 GetCameraRelativeFreeLookDirection(Vector2 input)
    {
        if (mainCamera == null)
        {
            Vector3 fallback = transform.forward * -input.y + transform.right * input.x;

            fallback.y = 0f;

            return fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.zero;
        }

        Vector3 cameraForward = mainCamera.transform.forward;
        cameraForward.y = 0f;

        Vector3 cameraRight = mainCamera.transform.right;
        cameraRight.y = 0f;

        if (cameraForward.sqrMagnitude > 0.001f)
            cameraForward.Normalize();

        if (cameraRight.sqrMagnitude > 0.001f)
            cameraRight.Normalize();

        Vector3 direction = cameraForward * -input.y + cameraRight * input.x;

        direction.y = 0f;

        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
    }

    private AimTarget FindBestMobileTargetInDirection(
        Vector3 desiredDirection,
        out Vector3 bestVisiblePoint
    )
    {
        bestVisiblePoint = Vector3.zero;

        if (mainCamera == null)
            return null;

        desiredDirection.y = 0f;

        if (desiredDirection.sqrMagnitude < 0.001f)
            return null;

        desiredDirection.Normalize();

        float minimumAlignment = Mathf.Cos(freeLookTargetAngle * Mathf.Deg2Rad);

        AimTarget bestTarget = null;

        float bestAlignment = minimumAlignment;
        float bestWorldDistanceSquared = Mathf.Infinity;

        float rangeSquared = autoAimRange * autoAimRange;

        var targets = AimTarget.ActiveTargets;

        for (int i = 0; i < targets.Count; i++)
        {
            AimTarget target = targets[i];

            if (target == null || !target.IsTargetable)
                continue;

            Vector3 worldDifference = target.BodyCenter - transform.position;

            worldDifference.y = 0f;

            float worldDistanceSquared = worldDifference.sqrMagnitude;

            if (worldDistanceSquared < 0.001f || worldDistanceSquared > rangeSquared)
            {
                continue;
            }

            Vector3 targetDirection = worldDifference.normalized;

            float alignment = Vector3.Dot(desiredDirection, targetDirection);

            if (alignment < minimumAlignment)
                continue;

            if (!TryGetCameraAndPlayerVisiblePoint(target, out Vector3 visiblePoint, out _))
            {
                continue;
            }

            const float alignmentTieTolerance = 0.001f;

            bool betterAlignment = alignment > bestAlignment + alignmentTieTolerance;

            bool approximatelySameAlignment =
                Mathf.Abs(alignment - bestAlignment) <= alignmentTieTolerance;

            bool closer = worldDistanceSquared < bestWorldDistanceSquared;

            if (bestTarget == null || betterAlignment || (approximatelySameAlignment && closer))
            {
                bestTarget = target;
                bestVisiblePoint = visiblePoint;
                bestAlignment = alignment;
                bestWorldDistanceSquared = worldDistanceSquared;
            }
        }

        return bestTarget;
    }

    // =====================================================
    // PER-SHOT MOBILE AIM POINT
    // =====================================================

    public bool TryGetShotAimPoint(Vector3 shotOrigin, out Vector3 shotAimPoint)
    {
        shotAimPoint = AimPoint;

        // Desktop keeps the exact mouse-derived aim point.
        if (!InputModeController.IsMobile)
        {
            return HasAimPoint;
        }

        // While free-looking, only use a target if the stick is actually
        // pointing toward one. This prevents Amy from visually looking away
        // while shots secretly keep homing toward the old sticky lock.
        AimTarget shotTarget = isFreeLooking ? freeLookShotTarget : CurrentTarget;

        if (
            shotTarget == null
            || !TryGetValidMobileTargetPoint(shotTarget, out Vector3 visibleTargetPoint)
        )
        {
            shotAimPoint = shotOrigin + transform.forward * mobileNoTargetAimDistance;

            return true;
        }

        // Keep the logical sticky lock synchronized after free-look selected
        // a valid target.
        CurrentTarget = shotTarget;

        if (mobileAimSettings == null)
        {
            shotAimPoint = visibleTargetPoint;
            return true;
        }

        MobileAimZone zone = mobileAimSettings.RollZone();

        float radiusMultiplier = zone switch
        {
            MobileAimZone.Green => mobileAimSettings.GreenRadius,
            MobileAimZone.Blue => mobileAimSettings.BlueRadius,
            _ => mobileAimSettings.YellowRadius,
        };

        float worldRadius = shotTarget.BodyRadius * radiusMultiplier;

        Vector3 targetCenter = visibleTargetPoint;

        Vector3 shotDirection = targetCenter - shotOrigin;

        if (shotDirection.sqrMagnitude < 0.000001f)
        {
            shotAimPoint = targetCenter;
            return true;
        }

        shotDirection.Normalize();

        Vector3 aimRight = Vector3.Cross(Vector3.up, shotDirection);

        if (aimRight.sqrMagnitude < 0.000001f)
        {
            aimRight = Vector3.Cross(Vector3.forward, shotDirection);
        }

        aimRight.Normalize();

        Vector3 aimUp = Vector3.Cross(shotDirection, aimRight).normalized;

        Vector2 randomOffset = Random.insideUnitCircle * worldRadius;

        Vector3 desiredPoint = targetCenter + aimRight * randomOffset.x + aimUp * randomOffset.y;

        if (zone == MobileAimZone.Green)
        {
            if (
                shotTarget.TryGetGuaranteedAimPoint(
                    desiredPoint,
                    shotOrigin,
                    out Vector3 guaranteedPoint
                )
            )
            {
                shotAimPoint = guaranteedPoint;
                return true;
            }

            shotAimPoint = targetCenter;
            return true;
        }

        shotAimPoint = desiredPoint;
        return true;
    }

    // =====================================================
    // MOBILE TARGET ACQUISITION
    // =====================================================

    private AimTarget FindBestMobileTarget(out Vector3 bestVisiblePoint)
    {
        bestVisiblePoint = Vector3.zero;

        if (mainCamera == null)
            return null;

        AimTarget bestTarget = null;

        float bestScreenDistanceSquared = Mathf.Infinity;

        float bestWorldDistanceSquared = Mathf.Infinity;

        float rangeSquared = autoAimRange * autoAimRange;

        var targets = AimTarget.ActiveTargets;

        for (int i = 0; i < targets.Count; i++)
        {
            AimTarget target = targets[i];

            if (target == null || !target.IsTargetable)
                continue;

            Vector3 worldDifference = target.BodyCenter - transform.position;

            float worldDistanceSquared = worldDifference.sqrMagnitude;

            if (worldDistanceSquared > rangeSquared)
                continue;

            if (
                !TryGetCameraAndPlayerVisiblePoint(
                    target,
                    out Vector3 visiblePoint,
                    out float screenDistanceSquared
                )
            )
            {
                continue;
            }

            const float screenTieTolerance = 0.0001f;

            bool betterScreenPosition =
                screenDistanceSquared < bestScreenDistanceSquared - screenTieTolerance;

            bool approximatelySameScreenPosition =
                Mathf.Abs(screenDistanceSquared - bestScreenDistanceSquared) <= screenTieTolerance;

            bool betterWorldDistance = worldDistanceSquared < bestWorldDistanceSquared;

            if (betterScreenPosition || (approximatelySameScreenPosition && betterWorldDistance))
            {
                bestTarget = target;
                bestVisiblePoint = visiblePoint;
                bestScreenDistanceSquared = screenDistanceSquared;

                bestWorldDistanceSquared = worldDistanceSquared;
            }
        }

        return bestTarget;
    }

    // =====================================================
    // CURRENT TARGET VALIDATION
    // =====================================================

    private bool TryGetValidMobileTargetPoint(AimTarget target, out Vector3 visiblePoint)
    {
        visiblePoint = Vector3.zero;

        if (target == null || !target.IsTargetable)
            return false;

        Vector3 difference = target.BodyCenter - transform.position;

        if (difference.sqrMagnitude > autoAimRange * autoAimRange)
        {
            return false;
        }

        if (mainCamera == null)
            return false;

        return TryGetCameraAndPlayerVisiblePoint(target, out visiblePoint, out _);
    }

    // =====================================================
    // TRUE CAMERA + PLAYER VISIBILITY
    // =====================================================

    private bool TryGetCameraAndPlayerVisiblePoint(
        AimTarget target,
        out Vector3 bestVisiblePoint,
        out float bestScreenDistanceSquared
    )
    {
        bestVisiblePoint = Vector3.zero;
        bestScreenDistanceSquared = Mathf.Infinity;

        if (mainCamera == null || target == null)
            return false;

        BuildMobileVisibilitySamples(target);

        Vector3 cameraOrigin = mainCamera.transform.position;

        Vector3 playerOrigin = GetLineOfSightOrigin();

        bool foundVisiblePoint = false;

        for (int i = 0; i < mobileVisibilitySamples.Length; i++)
        {
            Vector3 samplePoint = mobileVisibilitySamples[i];

            Vector3 viewportPosition = mainCamera.WorldToViewportPoint(samplePoint);

            if (!IsInsideVisibleViewport(viewportPosition))
                continue;

            if (!RayHitsTargetFirst(cameraOrigin, samplePoint, target))
            {
                continue;
            }

            if (!RayHitsTargetFirst(playerOrigin, samplePoint, target))
            {
                continue;
            }

            Vector2 screenDifference = new Vector2(
                viewportPosition.x - 0.5f,
                viewportPosition.y - 0.5f
            );

            float screenDistanceSquared = screenDifference.sqrMagnitude;

            if (screenDistanceSquared >= bestScreenDistanceSquared)
            {
                continue;
            }

            bestVisiblePoint = samplePoint;
            bestScreenDistanceSquared = screenDistanceSquared;

            foundVisiblePoint = true;
        }

        return foundVisiblePoint;
    }

    private void BuildMobileVisibilitySamples(AimTarget target)
    {
        Vector3 center = target.BodyCenter;

        float radius = Mathf.Max(target.BodyRadius, 0.05f);

        Vector3 right = mainCamera != null ? mainCamera.transform.right : transform.right;

        mobileVisibilitySamples[0] = center;

        mobileVisibilitySamples[1] = center + Vector3.up * (radius * 0.60f);

        mobileVisibilitySamples[2] =
            center + Vector3.up * (radius * 0.30f) + right * (radius * 0.45f);

        mobileVisibilitySamples[3] =
            center + Vector3.up * (radius * 0.30f) - right * (radius * 0.45f);

        mobileVisibilitySamples[4] = center - Vector3.up * (radius * 0.35f);
    }

    private bool IsInsideVisibleViewport(Vector3 viewportPosition)
    {
        if (viewportPosition.z <= 0f)
            return false;

        return viewportPosition.x >= 0f
            && viewportPosition.x <= 1f
            && viewportPosition.y >= 0f
            && viewportPosition.y <= 1f;
    }

    // =====================================================
    // PHYSICAL VISIBILITY TEST
    // =====================================================

    private bool RayHitsTargetFirst(Vector3 origin, Vector3 samplePoint, AimTarget target)
    {
        Vector3 direction = samplePoint - origin;

        float distance = direction.magnitude;

        if (distance <= 0.001f)
            return true;

        direction /= distance;

        float padding = Mathf.Max(0.08f, target.BodyRadius * 0.20f);

        float castDistance = distance + padding;

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            visibilityHits,
            castDistance,
            aimMask,
            QueryTriggerInteraction.Ignore
        );

        if (hitCount == 0)
            return false;

        RaycastHit closestBlockingHit = default;

        bool foundBlockingHit = false;

        float closestBlockingDistance = Mathf.Infinity;

        // RaycastNonAlloc does NOT guarantee distance order.
        // Ignore vision-transparent surfaces, then choose the nearest
        // remaining physical hit ourselves.
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = visibilityHits[i];

            if (hit.collider == null)
                continue;

            if (IsVisionTransparent(hit.collider))
            {
                continue;
            }

            if (hit.distance >= closestBlockingDistance)
            {
                continue;
            }

            closestBlockingDistance = hit.distance;

            closestBlockingHit = hit;

            foundBlockingHit = true;
        }

        if (!foundBlockingHit)
            return false;

        return target.OwnsCollider(closestBlockingHit.collider);
    }

    private static bool IsVisionTransparent(Collider collider)
    {
        return collider != null
            && collider.GetComponentInParent<VisionTransparentObstacle>() != null;
    }

    // =====================================================
    // PLAYER LOS ORIGIN
    // =====================================================

    private Vector3 GetLineOfSightOrigin()
    {
        if (characterController != null)
        {
            Bounds bounds = characterController.bounds;

            return bounds.center + Vector3.up * (bounds.extents.y * 0.55f);
        }

        return transform.position + Vector3.up * 1.4f;
    }

    // =====================================================
    // PASSIVE MOVEMENT FACING
    // =====================================================

    private void RotateTowardsMovementInput()
    {
        if (!TryGetCameraRelativeMovementDirection(out Vector3 movementDirection))
        {
            return;
        }

        RotateTowardsDirection(movementDirection, movementFacingRotationSpeed);
    }

    private bool TryGetCameraRelativeMovementDirection(out Vector3 direction)
    {
        direction = Vector3.zero;

        if (starterAssetsInputs == null)
            return false;

        Vector2 moveInput = starterAssetsInputs.move;

        float deadZoneSquared = movementFacingDeadZone * movementFacingDeadZone;

        if (moveInput.sqrMagnitude <= deadZoneSquared)
        {
            return false;
        }

        // This intentionally mirrors ThirdPersonController.Move():
        //
        // cameraForward * move.y + cameraRight * move.x
        //
        // so Amy faces the direction she is ACTUALLY travelling.
        if (mainCamera == null)
        {
            direction = transform.forward * moveInput.y + transform.right * moveInput.x;
        }
        else
        {
            Vector3 cameraForward = mainCamera.transform.forward;

            cameraForward.y = 0f;

            Vector3 cameraRight = mainCamera.transform.right;

            cameraRight.y = 0f;

            if (cameraForward.sqrMagnitude > 0.001f)
            {
                cameraForward.Normalize();
            }

            if (cameraRight.sqrMagnitude > 0.001f)
            {
                cameraRight.Normalize();
            }

            direction = cameraForward * moveInput.y + cameraRight * moveInput.x;
        }

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector3.zero;

            return false;
        }

        direction.Normalize();
        return true;
    }

    // =====================================================
    // ROTATION
    // =====================================================

    private void RotateTowards(Vector3 targetPosition, float rotationSpeed)
    {
        Vector3 direction = targetPosition - transform.position;

        direction.y = 0f;

        RotateTowardsDirection(direction, rotationSpeed);
    }

    private void RotateTowardsDirection(Vector3 direction, float rotationSpeed)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}
