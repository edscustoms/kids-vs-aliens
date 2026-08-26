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

    // We deliberately sample several meaningful areas of an enemy instead of
    // treating BodyCenter as the only point that matters.
    //
    // This solves two important mobile aim problems:
    //
    // 1. A short wall may hide the target's center while most of the alien is
    //    still clearly visible and shootable above the wall.
    //
    // 2. A target must actually be visible to the gameplay camera before Amy
    //    is allowed to acquire it. Merely being mathematically close to the
    //    edge of the viewport is not enough.
    //
    // Five points keeps the V1 test cheap while covering center / upper body /
    // sides / lower body reasonably well.
    private readonly Vector3[] mobileVisibilitySamples = new Vector3[5];

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
         * Target selection remains sticky:
         *
         * - Keep the current target while it remains valid.
         * - Only find a replacement when the current target is no longer a
         *   valid, genuinely visible target.
         *
         * VISIBLE now means BOTH:
         *
         * 1. THE PLAYER / AMY CAN SEE A REAL PART OF THE TARGET
         *    A real physics ray from Amy toward one of several body samples
         *    must hit target-owned geometry first.
         *
         * 2. THE GAMEPLAY CAMERA CAN SEE THAT SAME REAL PART
         *    The sample must be inside the actual 0..1 viewport AND a real
         *    camera ray must hit target-owned geometry first.
         *
         * This prevents Amy from locking an alien that has not appeared on
         * screen yet, while still allowing targets behind low cover to be
         * acquired when their upper body is genuinely visible and shootable.
         *
         * New-target priority remains:
         *
         * 1. closest visible point to screen center
         * 2. world distance as tie-breaker
         *
         * Target SELECTION and shot ACCURACY remain separate systems.
         */

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
        // Mobile shooting still works without auto-aim.
        // Fire straight in the direction Amy currently faces.
        // -------------------------------------------------

        if (
            CurrentTarget == null
            || !TryGetValidMobileTargetPoint(CurrentTarget, out Vector3 visibleTargetPoint)
        )
        {
            CurrentTarget = null;

            shotAimPoint = shotOrigin + transform.forward * mobileNoTargetAimDistance;

            return true;
        }

        // Safety fallback if the global settings asset was
        // forgotten in the Inspector.
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

        float worldRadius = CurrentTarget.BodyRadius * radiusMultiplier;

        // IMPORTANT:
        // When a target is partly hidden by low cover we center the assist
        // around the best currently visible/shootable body sample rather than
        // blindly around BodyCenter behind the wall.
        Vector3 targetCenter = visibleTargetPoint;

        Vector3 shotDirection = targetCenter - shotOrigin;

        if (shotDirection.sqrMagnitude < 0.000001f)
        {
            shotAimPoint = targetCenter;
            return true;
        }

        shotDirection.Normalize();

        // Build a 2D circle facing the shooter. The aim zones therefore behave
        // like circles drawn over the enemy from Amy's perspective.
        Vector3 aimRight = Vector3.Cross(Vector3.up, shotDirection);

        if (aimRight.sqrMagnitude < 0.000001f)
        {
            aimRight = Vector3.Cross(Vector3.forward, shotDirection);
        }

        aimRight.Normalize();

        Vector3 aimUp = Vector3.Cross(shotDirection, aimRight).normalized;

        Vector2 randomOffset = Random.insideUnitCircle * worldRadius;

        Vector3 desiredPoint = targetCenter + aimRight * randomOffset.x + aimUp * randomOffset.y;

        // GREEN resolves onto genuine remaining target collider geometry.
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

        // BLUE / YELLOW remain uncorrected. The actual PlayerShooter raycast
        // naturally decides whether the shot hits target, cover or empty space.
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
            return false;

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

            // Strict 0..1 camera visibility.
            // No old +10% off-screen acquisition margin: a target must actually
            // have a tested body sample on the player's visible screen.
            if (!IsInsideVisibleViewport(viewportPosition))
                continue;

            // The camera must physically see target-owned geometry along this
            // on-screen sample ray. A wall / prop / nearer enemy blocks it.
            if (!RayHitsTargetFirst(cameraOrigin, samplePoint, target))
                continue;

            // Amy must ALSO physically have a clear path to the same part of
            // the target. This is what allows upper body visibility over low
            // cover without pretending BodyCenter is the whole enemy.
            if (!RayHitsTargetFirst(playerOrigin, samplePoint, target))
                continue;

            Vector2 screenDifference = new Vector2(
                viewportPosition.x - 0.5f,
                viewportPosition.y - 0.5f
            );

            float screenDistanceSquared = screenDifference.sqrMagnitude;

            if (screenDistanceSquared >= bestScreenDistanceSquared)
                continue;

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

        // Camera-right gives us sensible screen-space left/right samples even
        // when the enemy or rail is rotated in world space.
        Vector3 right = mainCamera != null ? mainCamera.transform.right : transform.right;

        // Center / torso.
        mobileVisibilitySamples[0] = center;

        // Upper body / head region. This is especially important for low cover.
        mobileVisibilitySamples[1] = center + Vector3.up * (radius * 0.60f);

        // Upper-left and upper-right body regions.
        mobileVisibilitySamples[2] =
            center + Vector3.up * (radius * 0.30f) + right * (radius * 0.45f);

        mobileVisibilitySamples[3] =
            center + Vector3.up * (radius * 0.30f) - right * (radius * 0.45f);

        // Lower body sample. Useful when upper geometry is hidden by something
        // but legs / lower silhouette are genuinely visible.
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

        // The sample point is conceptually inside/around the target body.
        // Cast slightly beyond it so a sample near the surface can still hit
        // the target collider instead of ending just before it.
        float padding = Mathf.Max(0.08f, target.BodyRadius * 0.20f);
        float castDistance = distance + padding;

        // We only care about the FIRST physical object on this line.
        // aimMask already excludes the Player layer, so a single Raycast is
        // enough here and is cheaper than collecting/sorting many hits.
        if (
            !Physics.Raycast(
                origin,
                direction,
                out RaycastHit hit,
                castDistance,
                aimMask,
                QueryTriggerInteraction.Ignore
            )
        )
        {
            return false;
        }

        return target.OwnsCollider(hit.collider);
    }

    // =====================================================
    // PLAYER LOS ORIGIN
    // =====================================================

    private Vector3 GetLineOfSightOrigin()
    {
        if (characterController != null)
        {
            Bounds bounds = characterController.bounds;

            // Use Amy's upper torso / eye-line rather than the exact center of
            // the CharacterController. The old center ray was too low and made
            // short cover incorrectly hide an alien that Amy and the camera
            // could clearly see over.
            //
            // This is still derived from Amy's real controller bounds, so it
            // scales automatically with the character instead of hardcoding a
            // world-space height.
            return bounds.center + Vector3.up * (bounds.extents.y * 0.55f);
        }

        return transform.position + Vector3.up * 1.4f;
    }

    private bool IsPlayerCollider(Collider collider)
    {
        if (collider == null)
            return false;

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
