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

    // Five reusable body samples.
    //
    // Order matters for PER-SHOT aim priority:
    //
    // 0 = center / torso
    // 1 = upper / head
    // 2 = upper right
    // 3 = upper left
    // 4 = lower body
    //
    // Target acquisition can still evaluate every sample
    // based on screen position.
    //
    // Actual shooting instead uses this order deliberately
    // so short cover cannot cause Amy to prefer a low point
    // when an exposed upper-body shot is available.
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

        // Player-layer colliders must never block
        // aim / visibility raycasts.
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
         * TARGET ACQUISITION
         * ------------------
         *
         * Keep current target while valid.
         *
         * A target must:
         *
         * - be targetable
         * - be inside auto-aim range
         * - have real geometry visible on screen
         * - be physically visible from the camera
         * - be physically visible from Amy
         *
         * New-target priority:
         *
         * 1. nearest visible point to screen center
         * 2. world distance as tie-breaker
         *
         * IMPORTANT:
         *
         * Target acquisition decides WHO Amy tracks.
         *
         * Per-shot logic below separately decides WHICH
         * exposed body region the muzzle should use as the
         * center of the Green/Blue/Yellow accuracy system.
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

        // Desktop remains completely unchanged.
        if (!InputModeController.IsMobile)
        {
            return HasAimPoint;
        }

        // -------------------------------------------------
        // VALIDATE CURRENT TARGET
        // -------------------------------------------------

        if (
            CurrentTarget == null
            || !TryGetValidMobileTargetPoint(CurrentTarget, out Vector3 fallbackVisiblePoint)
        )
        {
            CurrentTarget = null;

            // No auto-lock:
            // shoot straight where Amy faces.
            shotAimPoint = shotOrigin + transform.forward * mobileNoTargetAimDistance;

            return true;
        }

        // -------------------------------------------------
        // PICK THE BEST BODY CENTER FOR THIS SHOT
        //
        // THIS is the important low-cover fix.
        //
        // Target acquisition uses Amy's general LOS.
        //
        // Shooting instead asks:
        //
        //      "What can the actual MUZZLE see?"
        //
        // Priority:
        //
        //      center
        //      upper
        //      upper-right
        //      upper-left
        //      lower
        //
        // Example:
        //
        //              Alien
        //                O   <- upper visible
        //               /|\
        //
        //          █████████ low cover
        //
        //      Amy ----- muzzle
        //
        // If center is blocked but upper body is clear,
        // the accuracy circle is moved upward onto that
        // exposed body region.
        //
        // This does NOT fake a hit.
        //
        // BLUE/YELLOW spread can still genuinely hit the
        // wall because PlayerShooter physics remains final
        // authority.
        // -------------------------------------------------

        Vector3 shotCenter;

        if (!TryGetPreferredMobileShotCenter(CurrentTarget, shotOrigin, out shotCenter))
        {
            // Amy can generally see the enemy, but the
            // muzzle itself currently has no clear sampled
            // shot path.
            //
            // Preserve physical behavior:
            // use the normal visible target point and let
            // the real weapon ray naturally hit the cover.
            shotCenter = fallbackVisiblePoint;
        }

        // -------------------------------------------------
        // SETTINGS FALLBACK
        // -------------------------------------------------

        if (mobileAimSettings == null)
        {
            shotAimPoint = shotCenter;

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

        Vector3 shotDirection = shotCenter - shotOrigin;

        if (shotDirection.sqrMagnitude < 0.000001f)
        {
            shotAimPoint = shotCenter;

            return true;
        }

        shotDirection.Normalize();

        // -------------------------------------------------
        // AIM PLANE
        //
        // Build the Green / Blue / Yellow circle facing
        // Amy instead of lying in world-space.
        // -------------------------------------------------

        Vector3 aimRight = Vector3.Cross(Vector3.up, shotDirection);

        if (aimRight.sqrMagnitude < 0.000001f)
        {
            aimRight = Vector3.Cross(Vector3.forward, shotDirection);
        }

        aimRight.Normalize();

        Vector3 aimUp = Vector3.Cross(shotDirection, aimRight).normalized;

        Vector2 randomOffset = Random.insideUnitCircle * worldRadius;

        Vector3 desiredPoint = shotCenter + aimRight * randomOffset.x + aimUp * randomOffset.y;

        // -------------------------------------------------
        // GREEN
        //
        // Green snaps onto real remaining target geometry.
        //
        // We now ALSO verify that the snapped point is
        // actually reachable from the muzzle.
        //
        // This prevents Green from snapping downward onto
        // a valid collider that happens to sit behind low
        // cover.
        // -------------------------------------------------

        if (zone == MobileAimZone.Green)
        {
            if (
                CurrentTarget.TryGetGuaranteedAimPoint(
                    desiredPoint,
                    shotOrigin,
                    out Vector3 guaranteedPoint
                ) && RayHitsTargetFirst(shotOrigin, guaranteedPoint, CurrentTarget)
            )
            {
                shotAimPoint = guaranteedPoint;

                return true;
            }

            // shotCenter was already verified from the
            // muzzle whenever possible.
            shotAimPoint = shotCenter;

            return true;
        }

        // BLUE / YELLOW deliberately remain uncorrected.
        //
        // Their random spread can naturally:
        //
        // - hit alien
        // - hit low cover
        // - hit wall
        // - miss entirely
        //
        // PlayerShooter's real raycast decides.
        shotAimPoint = desiredPoint;

        return true;
    }

    // =====================================================
    // PER-SHOT BODY PRIORITY
    // =====================================================

    private bool TryGetPreferredMobileShotCenter(
        AimTarget target,
        Vector3 shotOrigin,
        out Vector3 shotCenter
    )
    {
        shotCenter = Vector3.zero;

        if (target == null || mainCamera == null)
        {
            return false;
        }

        BuildMobileVisibilitySamples(target);

        Vector3 cameraOrigin = mainCamera.transform.position;

        // Deliberately iterate in array order:
        //
        // 0 center
        // 1 upper
        // 2 upper-right
        // 3 upper-left
        // 4 lower
        //
        // First physically usable point wins.
        for (int i = 0; i < mobileVisibilitySamples.Length; i++)
        {
            Vector3 samplePoint = mobileVisibilitySamples[i];

            Vector3 viewportPosition = mainCamera.WorldToViewportPoint(samplePoint);

            if (!IsInsideVisibleViewport(viewportPosition))
            {
                continue;
            }

            // The player must actually be able to see this
            // part of the enemy on the screen.
            if (!RayHitsTargetFirst(cameraOrigin, samplePoint, target))
            {
                continue;
            }

            // Most importantly:
            //
            // the ACTUAL weapon muzzle must have a real
            // clear path to this target region.
            if (!RayHitsTargetFirst(shotOrigin, samplePoint, target))
            {
                continue;
            }

            shotCenter = samplePoint;

            return true;
        }

        return false;
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
            {
                continue;
            }

            Vector3 worldDifference = target.BodyCenter - transform.position;

            float worldDistanceSquared = worldDifference.sqrMagnitude;

            if (worldDistanceSquared > rangeSquared)
            {
                continue;
            }

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
        {
            return false;
        }

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
        {
            return false;
        }

        BuildMobileVisibilitySamples(target);

        Vector3 cameraOrigin = mainCamera.transform.position;

        Vector3 playerOrigin = GetLineOfSightOrigin();

        bool foundVisiblePoint = false;

        for (int i = 0; i < mobileVisibilitySamples.Length; i++)
        {
            Vector3 samplePoint = mobileVisibilitySamples[i];

            Vector3 viewportPosition = mainCamera.WorldToViewportPoint(samplePoint);

            if (!IsInsideVisibleViewport(viewportPosition))
            {
                continue;
            }

            // Camera must see real target geometry.
            if (!RayHitsTargetFirst(cameraOrigin, samplePoint, target))
            {
                continue;
            }

            // Amy must also see that same region.
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

    // =====================================================
    // BODY SAMPLES
    // =====================================================

    private void BuildMobileVisibilitySamples(AimTarget target)
    {
        Vector3 center = target.BodyCenter;

        float radius = Mathf.Max(target.BodyRadius, 0.05f);

        // Camera-right produces meaningful on-screen
        // left/right body samples even when a target or
        // rail is rotated in world space.
        Vector3 right = mainCamera != null ? mainCamera.transform.right : transform.right;

        // 0 — torso / center.
        mobileVisibilitySamples[0] = center;

        // 1 — upper body / head.
        mobileVisibilitySamples[1] = center + Vector3.up * (radius * 0.60f);

        // 2 — upper-right.
        mobileVisibilitySamples[2] =
            center + Vector3.up * (radius * 0.30f) + right * (radius * 0.45f);

        // 3 — upper-left.
        mobileVisibilitySamples[3] =
            center + Vector3.up * (radius * 0.30f) - right * (radius * 0.45f);

        // 4 — lower body.
        mobileVisibilitySamples[4] = center - Vector3.up * (radius * 0.35f);
    }

    // =====================================================
    // VIEWPORT
    // =====================================================

    private bool IsInsideVisibleViewport(Vector3 viewportPosition)
    {
        if (viewportPosition.z <= 0f)
        {
            return false;
        }

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
        {
            return true;
        }

        direction /= distance;

        // Cast slightly beyond the conceptual body sample
        // so samples close to the collider surface still
        // intersect the actual target geometry.
        float padding = Mathf.Max(0.08f, target.BodyRadius * 0.20f);

        float castDistance = distance + padding;

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

            return bounds.center + Vector3.up * (bounds.extents.y * 0.55f);
        }

        return transform.position + Vector3.up * 1.4f;
    }

    // =====================================================
    // ROTATION
    // =====================================================

    private void RotateTowards(Vector3 targetPosition, float rotationSpeed)
    {
        Vector3 direction = targetPosition - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}
