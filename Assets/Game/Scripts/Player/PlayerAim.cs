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

    // =====================================================
    // MOBILE
    // =====================================================

    [Header("Mobile Auto Aim")]
    [SerializeField]
    private float autoAimRange = 20f;

    [Tooltip("How quickly Amy naturally rotates toward the current mobile target.")]
    [SerializeField]
    private float mobileRotationSpeed = 5f;

    [Tooltip(
        "How far outside the visible screen a currently tracked target may go "
            + "before mobile auto-aim releases it. 0.1 means roughly 10% beyond each edge."
    )]
    [Range(0f, 0.5f)]
    [SerializeField]
    private float mobileScreenMargin = 0.1f;

    [Tooltip(
        "Distance used for mobile shots when there is no auto-aim target. "
            + "The exact value only creates a straight-forward aim direction."
    )]
    [SerializeField]
    private float mobileNoTargetAimDistance = 50f;

    [SerializeField]
    private MobileAimSettings mobileAimSettings;

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
    private Plane groundPlane;
    private int aimMask;

    private readonly RaycastHit[] mouseAimHits = new RaycastHit[32];
    private readonly RaycastHit[] lineOfSightHits = new RaycastHit[32];

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
        }
        else
        {
            CurrentTarget = null;
            MouseAim();
        }
    }

    // =====================================================
    // DESKTOP AIM
    // =====================================================

    private void MouseAim()
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

            RotateTowards(AimPoint, desktopRotationSpeed);
            return;
        }

        if (groundPlane.Raycast(ray, out float distance))
        {
            AimPoint = ray.GetPoint(distance);
            HasAimPoint = true;

            RotateTowards(AimPoint, desktopRotationSpeed);
            return;
        }

        HasAimPoint = false;
    }

    // =====================================================
    // MOBILE AIM
    // =====================================================

    private void MobileAim()
    {
        /*
         * MOBILE TARGET PRIORITY DESIGN
         * --------------------------------
         *
         * We intentionally DO NOT continuously select the mathematically
         * "best" target every frame.
         *
         * Doing that would make auto-aim jump between enemies whenever
         * another enemy becomes slightly closer or slightly more centered.
         * That would feel nervous, unpredictable and very "aimbot-like".
         *
         * Instead mobile aiming uses HARD TARGET STICKINESS:
         *
         * 1. If Amy already has a valid target, KEEP that target.
         *
         * 2. The current target is only released when it becomes invalid:
         *      - target disabled / dead / no longer targetable
         *      - outside auto-aim range
         *      - sufficiently outside the camera view
         *      - line of sight becomes blocked
         *
         * 3. Only when we need a NEW target do we compare all eligible targets.
         *
         * 4. New-target priority is primarily SCREEN POSITION:
         *      The enemy closest to the center of the player's screen wins.
         *
         *    Screen center is a better approximation of what the player is
         *    visually focusing on than pure world-space distance.
         *
         * 5. If two targets are almost equally centered, WORLD DISTANCE
         *    becomes the tie-breaker and the closer enemy wins.
         *
         * Later, if playtesting shows hard stickiness feels TOO sticky,
         * we can add a switch threshold where a new target must score
         * significantly better before stealing the lock.
         *
         * IMPORTANT:
         * Target SELECTION and shot ACCURACY are separate systems.
         *
         * This code decides WHO Amy wants to shoot.
         * Green / Blue / Yellow zones decide WHERE each shot goes.
         * The weapon Physics.Raycast remains authoritative for WHAT gets hit.
         */

        if (CurrentTarget != null && IsValidMobileTarget(CurrentTarget))
        {
            ApplyMobileTarget();
            return;
        }

        CurrentTarget = FindBestMobileTarget();

        if (CurrentTarget == null)
        {
            HasAimPoint = false;
            return;
        }

        ApplyMobileTarget();
    }

    private void ApplyMobileTarget()
    {
        AimPoint = CurrentTarget.BodyCenter;
        HasAimPoint = true;

        RotateTowards(AimPoint, mobileRotationSpeed);
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

        // -------------------------------------------------
        // NO MOBILE TARGET
        //
        // Mobile shooting must still work without auto-aim.
        // If there is no valid lock, fire straight in the
        // direction Amy is currently facing at muzzle height.
        //
        // PlayerShooter still performs its normal real raycast,
        // so the player can shoot walls, props or empty space.
        // -------------------------------------------------

        if (CurrentTarget == null || !IsValidMobileTarget(CurrentTarget))
        {
            CurrentTarget = null;

            shotAimPoint = shotOrigin + transform.forward * mobileNoTargetAimDistance;

            return true;
        }

        // Safety fallback if the global settings asset was
        // forgotten in the Inspector.
        if (mobileAimSettings == null)
        {
            shotAimPoint = CurrentTarget.BodyCenter;
            return true;
        }

        MobileAimZone zone = mobileAimSettings.RollZone();

        float radiusMultiplier = zone switch
        {
            MobileAimZone.Green => mobileAimSettings.GreenRadius,
            MobileAimZone.Blue => mobileAimSettings.BlueRadius,
            _ => mobileAimSettings.YellowRadius,
        };

        float worldRadius = CurrentTarget.BodyRadius * radiusMultiplier;
        Vector3 targetCenter = CurrentTarget.BodyCenter;

        Vector3 shotDirection = targetCenter - shotOrigin;

        if (shotDirection.sqrMagnitude < 0.000001f)
        {
            shotAimPoint = targetCenter;
            return true;
        }

        shotDirection.Normalize();

        // Build a 2D circle facing the shooter. The aim zones
        // therefore behave like circles drawn over the enemy
        // from Amy's perspective, not circles on the floor.
        Vector3 aimRight = Vector3.Cross(Vector3.up, shotDirection);

        if (aimRight.sqrMagnitude < 0.000001f)
        {
            aimRight = Vector3.Cross(Vector3.forward, shotDirection);
        }

        aimRight.Normalize();

        Vector3 aimUp = Vector3.Cross(shotDirection, aimRight).normalized;

        // Correct area distribution across the circle.
        Vector2 randomOffset = Random.insideUnitCircle * worldRadius;

        Vector3 desiredPoint = targetCenter + aimRight * randomOffset.x + aimUp * randomOffset.y;

        // GREEN must resolve onto genuine target collider geometry.
        if (zone == MobileAimZone.Green)
        {
            if (
                CurrentTarget.TryGetGuaranteedAimPoint(
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

        // BLUE / YELLOW are deliberately left uncorrected.
        // The actual weapon raycast naturally decides hit/miss.
        shotAimPoint = desiredPoint;
        return true;
    }

    // =====================================================
    // MOBILE TARGET ACQUISITION
    // =====================================================

    private AimTarget FindBestMobileTarget()
    {
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

            Vector3 targetPosition = target.BodyCenter;
            Vector3 worldDifference = targetPosition - transform.position;
            float worldDistanceSquared = worldDifference.sqrMagnitude;

            if (worldDistanceSquared > rangeSquared)
                continue;

            Vector3 viewportPosition = mainCamera.WorldToViewportPoint(targetPosition);

            if (!IsInsideMobileViewport(viewportPosition))
                continue;

            if (!HasLineOfSight(target))
                continue;

            Vector2 screenDifference = new Vector2(
                viewportPosition.x - 0.5f,
                viewportPosition.y - 0.5f
            );

            float screenDistanceSquared = screenDifference.sqrMagnitude;

            const float screenTieTolerance = 0.0001f;

            bool betterScreenPosition =
                screenDistanceSquared < bestScreenDistanceSquared - screenTieTolerance;

            bool approximatelySameScreenPosition =
                Mathf.Abs(screenDistanceSquared - bestScreenDistanceSquared) <= screenTieTolerance;

            bool betterWorldDistance = worldDistanceSquared < bestWorldDistanceSquared;

            if (betterScreenPosition || (approximatelySameScreenPosition && betterWorldDistance))
            {
                bestTarget = target;
                bestScreenDistanceSquared = screenDistanceSquared;
                bestWorldDistanceSquared = worldDistanceSquared;
            }
        }

        return bestTarget;
    }

    // =====================================================
    // CURRENT TARGET VALIDATION
    // =====================================================

    private bool IsValidMobileTarget(AimTarget target)
    {
        if (target == null || !target.IsTargetable)
            return false;

        Vector3 targetPosition = target.BodyCenter;
        Vector3 difference = targetPosition - transform.position;

        if (difference.sqrMagnitude > autoAimRange * autoAimRange)
            return false;

        if (mainCamera == null)
            return false;

        Vector3 viewportPosition = mainCamera.WorldToViewportPoint(targetPosition);

        if (!IsInsideMobileViewport(viewportPosition))
            return false;

        return HasLineOfSight(target);
    }

    // =====================================================
    // SCREEN VALIDATION
    // =====================================================

    private bool IsInsideMobileViewport(Vector3 viewportPosition)
    {
        if (viewportPosition.z <= 0f)
            return false;

        return viewportPosition.x >= -mobileScreenMargin
            && viewportPosition.x <= 1f + mobileScreenMargin
            && viewportPosition.y >= -mobileScreenMargin
            && viewportPosition.y <= 1f + mobileScreenMargin;
    }

    // =====================================================
    // LINE OF SIGHT
    // =====================================================

    private bool HasLineOfSight(AimTarget target)
    {
        Vector3 origin = GetLineOfSightOrigin();
        Vector3 destination = target.BodyCenter;
        Vector3 direction = destination - origin;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
            return true;

        direction /= distance;

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            lineOfSightHits,
            distance,
            aimMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = lineOfSightHits[i];

            if (hit.collider == null)
                continue;

            if (IsPlayerCollider(hit.collider))
                continue;

            // Geometry belonging to the candidate itself
            // does not obstruct LOS to that candidate.
            if (target.OwnsCollider(hit.collider))
                continue;

            // Anything else before the target center blocks LOS.
            if (hit.distance < distance - 0.02f)
                return false;
        }

        return true;
    }

    private Vector3 GetLineOfSightOrigin()
    {
        if (characterController != null)
        {
            return characterController.bounds.center;
        }

        return transform.position + Vector3.up;
    }

    private bool IsPlayerCollider(Collider collider)
    {
        Transform colliderTransform = collider.transform;

        return colliderTransform == transform || colliderTransform.IsChildOf(transform);
    }

    // =====================================================
    // ROTATION
    // =====================================================

    private void RotateTowards(Vector3 targetPosition, float rotationSpeed)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}
