using System.Collections;
using UnityEngine;

public enum PracticeTargetState
{
    Inactive,
    Active,
    Hardcore,
}

public class PracticeTarget : MonoBehaviour
{
    // =====================================================
    // INSPECTOR
    // =====================================================

    [Header("Hinge")]
    [SerializeField]
    private float hingeDuration = 1.6f;

    [Header("Firing")]
    [SerializeField]
    private PlasmaBoltVFX plasmaBoltPrefab;

    [SerializeField]
    private Vector2 fireIntervalRange =
        new Vector2(1.5f, 3f);

    [SerializeField]
    private Color boltColor =
        new Color(1f, 0.05f, 0.05f);

    [Header("Accuracy")]
    [SerializeField]
    [Range(0f, 100f)]
    private float hitChance = 65f;

    [SerializeField]
    private float missRadius = 1.5f;

    [Header("Hardcore")]
    [SerializeField]
    private float baseDamage = 10f;

    [SerializeField]
    [Range(0f, 100f)]
    private float damageReductionPerPlayerHit = 20f;


    // =====================================================
    // FIXED MECHANICS
    // =====================================================

    private static readonly Vector3 InactiveHingeRotation =
        new Vector3(90f, 0f, 0f);

    private const float ShotRange = 50f;


    // =====================================================
    // REFERENCES
    // =====================================================

    private Transform hingePivot;
    private Transform piecesRoot;
    private Transform fireOrigin;

    private Transform player;

    private PlayerHealth playerHealth;

    private CharacterController playerController;

    private BreakableTarget breakableTarget;


    // Reused array.
    // No allocation every LOS / shot check.
    private readonly RaycastHit[] shotHits =
        new RaycastHit[32];


    // =====================================================
    // STATE
    // =====================================================

    private Quaternion activeHingeRotation;

    private Coroutine hingeRoutine;
    private Coroutine fireRoutine;

    private PracticeTargetState state =
        PracticeTargetState.Inactive;


    // =====================================================
    // PROPERTIES
    // =====================================================

    public PracticeTargetState State =>
        state;

    public float HingeDuration =>
        hingeDuration;

    private bool IsOperational =>
        state == PracticeTargetState.Active ||
        state == PracticeTargetState.Hardcore;


    // =====================================================
    // UNITY
    // =====================================================

    private void Awake()
    {
        FindRequiredObjects();

        if (hingePivot != null)
        {
            activeHingeRotation =
                hingePivot.localRotation;
        }

        breakableTarget =
            GetComponentInChildren<
                BreakableTarget
            >(true);
    }


    // =====================================================
    // INITIALIZATION
    // =====================================================

    public void Initialize(
        Transform playerTransform
    )
    {
        player =
            playerTransform;

        if (player == null)
            return;

        playerHealth =
            player.GetComponent<PlayerHealth>();

        playerController =
            player.GetComponent<CharacterController>();
    }


    // =====================================================
    // FIND OBJECTS
    // =====================================================

    private void FindRequiredObjects()
    {
        Transform[] children =
            GetComponentsInChildren<
                Transform
            >(true);

        foreach (Transform child in children)
        {
            if (child.name == "HingePivot")
            {
                hingePivot =
                    child;
            }

            if (child.name == "BreakablePieces")
            {
                piecesRoot =
                    child;
            }

            if (child.name == "TargetFireOrigin")
            {
                fireOrigin =
                    child;
            }
        }

        if (hingePivot == null)
        {
            Debug.LogError(
                $"{name}: Could not find 'HingePivot'."
            );
        }

        if (piecesRoot == null)
        {
            Debug.LogError(
                $"{name}: Could not find 'BreakablePieces'."
            );
        }

        if (fireOrigin == null)
        {
            Debug.LogWarning(
                $"{name}: Could not find " +
                "'TargetFireOrigin'. " +
                "Target firing will be disabled."
            );
        }
    }


    // =====================================================
    // STATE
    // =====================================================

    public void SetState(
        PracticeTargetState newState,
        bool animate = true
    )
    {
        PracticeTargetState previousState =
            state;

        state =
            newState;


        if (hingeRoutine != null)
        {
            StopCoroutine(
                hingeRoutine
            );

            hingeRoutine =
                null;
        }


        StopFiring();


        bool wasOperational =
            previousState !=
            PracticeTargetState.Inactive;


        bool willBeOperational =
            newState !=
            PracticeTargetState.Inactive;


        // Active -> Hardcore
        // or Hardcore -> Active.
        //
        // No hinge animation required.
        if (
            wasOperational &&
            willBeOperational
        )
        {
            SetHittable(true);

            StartFiring();

            return;
        }


        // Scene/startup setup.
        if (!animate)
        {
            ApplyStateImmediately();

            if (IsOperational)
            {
                StartFiring();
            }

            return;
        }


        // Target remains hittable while
        // physically raising/lowering.
        SetHittable(true);


        hingeRoutine =
            StartCoroutine(
                AnimateHinge()
            );
    }


    // =====================================================
    // HINGE
    // =====================================================

    private IEnumerator AnimateHinge()
    {
        if (hingePivot == null)
        {
            hingeRoutine =
                null;

            yield break;
        }


        Quaternion startRotation =
            hingePivot.localRotation;


        Quaternion targetRotation =
            state ==
            PracticeTargetState.Inactive
                ? GetInactiveRotation()
                : activeHingeRotation;


        float duration =
            Mathf.Max(
                0.01f,
                hingeDuration
            );


        float elapsed =
            0f;


        while (elapsed < duration)
        {
            elapsed +=
                Time.deltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed /
                    duration
                );


            float smoothT =
                t *
                t *
                (3f - 2f * t);


            hingePivot.localRotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    smoothT
                );


            yield return null;
        }


        hingePivot.localRotation =
            targetRotation;


        if (
            state ==
            PracticeTargetState.Inactive
        )
        {
            SetHittable(false);
        }
        else
        {
            StartFiring();
        }


        hingeRoutine =
            null;
    }


    private void ApplyStateImmediately()
    {
        if (hingePivot == null)
            return;


        if (
            state ==
            PracticeTargetState.Inactive
        )
        {
            hingePivot.localRotation =
                GetInactiveRotation();

            SetHittable(false);
        }
        else
        {
            hingePivot.localRotation =
                activeHingeRotation;

            SetHittable(true);
        }
    }


    private Quaternion GetInactiveRotation()
    {
        return
            activeHingeRotation *
            Quaternion.Euler(
                InactiveHingeRotation
            );
    }


    // =====================================================
    // FIRING
    // =====================================================

    private void StartFiring()
    {
        if (!IsOperational)
            return;


        if (
            plasmaBoltPrefab == null ||
            fireOrigin == null ||
            player == null
        )
        {
            return;
        }


        if (fireRoutine != null)
        {
            StopCoroutine(
                fireRoutine
            );
        }


        fireRoutine =
            StartCoroutine(
                FireRoutine()
            );
    }


    public void StopFiring()
    {
        if (fireRoutine == null)
            return;


        StopCoroutine(
            fireRoutine
        );


        fireRoutine =
            null;
    }


    private IEnumerator FireRoutine()
    {
        yield return
            new WaitForSeconds(
                Random.Range(
                    fireIntervalRange.x,
                    fireIntervalRange.y
                )
            );


        while (IsOperational)
        {
            Fire();


            yield return
                new WaitForSeconds(
                    Random.Range(
                        fireIntervalRange.x,
                        fireIntervalRange.y
                    )
                );
        }


        fireRoutine =
            null;
    }


    // =====================================================
    // PLAYER VISIBILITY
    //
    // IMPORTANT:
    //
    // The old V1 implementation checked only ONE point:
    //
    //      enemy -> Amy CharacterController center
    //
    // That worked for full walls but failed with low cover.
    //
    // Example:
    //
    //          AMY
    //           O
    //          /|\
    //      █████████ low wall
    //
    // Amy's upper body is clearly visible, but the ray to
    // her center can still intersect the wall.
    //
    // We now test several REAL 3D positions inside Amy's
    // CharacterController:
    //
    //      - center
    //      - upper body
    //      - lower body
    //
    // If ANY useful part of Amy is visible, the enemy can
    // see her.
    //
    // We also return the point which was visible so the
    // enemy shoots toward visible geometry instead of
    // seeing Amy's head and then shooting into the wall at
    // her waist.
    //
    // POST-MOBILE-BUILD TODO:
    // Move this LOS logic into a reusable enemy/combat
    // visibility component shared by practice targets and
    // future enemy classes.
    // =====================================================

    public bool CanSeePlayer()
    {
        return
            TryGetVisiblePlayerPoint(
                out _
            );
    }


    private bool TryGetVisiblePlayerPoint(
        out Vector3 visiblePoint
    )
    {
        visiblePoint =
            Vector3.zero;


        if (
            player == null ||
            fireOrigin == null
        )
        {
            return false;
        }


        // ---------------------------------------------
        // CharacterController version
        //
        // Use the actual current world-space bounds.
        // No fixed player height assumptions.
        // ---------------------------------------------

        if (playerController != null)
        {
            Bounds bounds =
                playerController.bounds;


            Vector3 center =
                bounds.center;


            float halfHeight =
                bounds.extents.y;


            // Try center first because it is the most
            // natural shooting point when Amy is fully
            // exposed.
            if (
                HasClearLineToPlayerPoint(
                    center
                )
            )
            {
                visiblePoint =
                    center;

                return true;
            }


            // Upper torso / head area.
            //
            // This is the important point for low cover.
            Vector3 upperPoint =
                center +
                Vector3.up *
                (
                    halfHeight *
                    0.65f
                );


            if (
                HasClearLineToPlayerPoint(
                    upperPoint
                )
            )
            {
                visiblePoint =
                    upperPoint;

                return true;
            }


            // Lower body.
            //
            // Useful for weird geometry where Amy's upper
            // body is obscured but legs are visible.
            Vector3 lowerPoint =
                center -
                Vector3.up *
                (
                    halfHeight *
                    0.45f
                );


            if (
                HasClearLineToPlayerPoint(
                    lowerPoint
                )
            )
            {
                visiblePoint =
                    lowerPoint;

                return true;
            }


            return false;
        }


        // ---------------------------------------------
        // Defensive fallback
        // ---------------------------------------------

        Vector3 fallbackPoint =
            player.position +
            Vector3.up;


        if (
            HasClearLineToPlayerPoint(
                fallbackPoint
            )
        )
        {
            visiblePoint =
                fallbackPoint;

            return true;
        }


        return false;
    }


    private bool HasClearLineToPlayerPoint(
        Vector3 targetPosition
    )
    {
        Vector3 direction =
            targetPosition -
            fireOrigin.position;


        float distance =
            direction.magnitude;


        if (distance <= 0.001f)
            return true;


        direction /=
            distance;


        int hitCount =
            Physics.RaycastNonAlloc(
                fireOrigin.position,
                direction,
                shotHits,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );


        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit =
                shotHits[i];


            if (hit.collider == null)
                continue;


            // Ignore this cardboard target's
            // own colliders.
            if (
                IsOwnCollider(
                    hit.collider
                )
            )
            {
                continue;
            }


            // Amy herself is the destination,
            // not an obstruction.
            if (
                IsPlayerCollider(
                    hit.collider
                )
            )
            {
                continue;
            }


            // Anything else before the requested player
            // point blocks this particular sight line.
            if (
                hit.distance <
                distance - 0.02f
            )
            {
                return false;
            }
        }


        return true;
    }


    // =====================================================
    // REAL SHOT COLLISION
    // =====================================================

    private bool TryGetFirstShotHit(
        Vector3 direction,
        out RaycastHit closestHit
    )
    {
        closestHit =
            default;


        int hitCount =
            Physics.RaycastNonAlloc(
                fireOrigin.position,
                direction,
                shotHits,
                ShotRange,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );


        bool foundHit =
            false;


        float closestDistance =
            Mathf.Infinity;


        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit =
                shotHits[i];


            if (hit.collider == null)
                continue;


            // Never let the target immediately
            // shoot itself.
            if (
                IsOwnCollider(
                    hit.collider
                )
            )
            {
                continue;
            }


            if (
                hit.distance >=
                closestDistance
            )
            {
                continue;
            }


            closestDistance =
                hit.distance;


            closestHit =
                hit;


            foundHit =
                true;
        }


        return foundHit;
    }


    private bool IsOwnCollider(
        Collider collider
    )
    {
        if (collider == null)
            return false;


        Transform hitTransform =
            collider.transform;


        return
            hitTransform == transform ||
            hitTransform.IsChildOf(
                transform
            );
    }


    private bool IsPlayerCollider(
        Collider collider
    )
    {
        if (
            player == null ||
            collider == null
        )
        {
            return false;
        }


        Transform hitTransform =
            collider.transform;


        return
            hitTransform == player ||
            hitTransform.IsChildOf(
                player
            );
    }


    // =====================================================
    // FIRE
    // =====================================================

    private void Fire()
    {
        if (
            plasmaBoltPrefab == null ||
            fireOrigin == null ||
            player == null
        )
        {
            return;
        }


        if (
            breakableTarget != null &&
            (
                breakableTarget.IsCollapsed ||
                breakableTarget.IsReassembling
            )
        )
        {
            return;
        }


        // -------------------------------------------------
        // VISIBILITY
        //
        // Do not fire through solid cover.
        //
        // Unlike the old implementation, this also returns
        // WHICH part of Amy is actually visible.
        // -------------------------------------------------

        if (
            !TryGetVisiblePlayerPoint(
                out Vector3 playerTargetPosition
            )
        )
        {
            return;
        }


        bool willHit =
            Random.Range(
                0f,
                100f
            ) <= hitChance;


        Vector3 aimPoint =
            playerTargetPosition;


        // -------------------------------------------------
        // MISS OFFSET
        // -------------------------------------------------

        if (!willHit)
        {
            Vector2 randomOffset =
                Random.insideUnitCircle *
                missRadius;


            Vector3 directionToPlayer =
                (
                    playerTargetPosition -
                    fireOrigin.position
                ).normalized;


            Vector3 horizontalAxis =
                Vector3.Cross(
                    Vector3.up,
                    directionToPlayer
                );


            // Defensive protection if the shot direction
            // happens to be almost vertical.
            if (
                horizontalAxis.sqrMagnitude <
                0.000001f
            )
            {
                horizontalAxis =
                    Vector3.right;
            }
            else
            {
                horizontalAxis.Normalize();
            }


            aimPoint +=
                horizontalAxis *
                randomOffset.x +
                Vector3.up *
                randomOffset.y;
        }


        Vector3 shotDirection =
            (
                aimPoint -
                fireOrigin.position
            ).normalized;


        // -------------------------------------------------
        // REAL PHYSICAL SHOT
        //
        // The projectile terminates at the FIRST physical
        // collider in the world.
        //
        // So even after visibility succeeds:
        //
        // - a miss can hit cover
        // - another object can block the bolt
        // - Amy cannot take damage through a wall
        // -------------------------------------------------

        bool hitSomething =
            TryGetFirstShotHit(
                shotDirection,
                out RaycastHit worldHit
            );


        Vector3 shotEndPosition =
            hitSomething
                ? worldHit.point
                : fireOrigin.position +
                  shotDirection *
                  ShotRange;


        bool isHardcore =
            state ==
            PracticeTargetState.Hardcore;


        // -------------------------------------------------
        // HARDCORE DAMAGE
        //
        // Damage is allowed only when:
        //
        // 1. RNG declared this an intended hit
        // 2. the real physical ray's FIRST hit was Amy
        //
        // Because we've already confirmed the actual
        // collider hit Amy, we no longer need a secondary
        // approximate distance-to-player test here.
        // -------------------------------------------------

        bool genuinelyHitPlayer =
            willHit &&
            hitSomething &&
            IsPlayerCollider(
                worldHit.collider
            );


        PlasmaBoltVFX bolt =
            VfxPool.Spawn(
                plasmaBoltPrefab,
                fireOrigin.position,
                Quaternion.identity
            );


        if (bolt == null)
        {
            if (
                isHardcore &&
                genuinelyHitPlayer
            )
            {
                TryDealHardcoreDamage();
            }

            return;
        }


        bolt.Initialize(
            fireOrigin.position,
            shotEndPosition,
            boltColor,
            isHardcore &&
            genuinelyHitPlayer
                ? TryDealHardcoreDamage
                : null
        );
    }


    // =====================================================
    // HARDCORE DAMAGE
    // =====================================================

    private void TryDealHardcoreDamage()
    {
        if (
            player == null ||
            playerHealth == null
        )
        {
            return;
        }


        float damage =
            CalculateHardcoreDamage();


        if (damage <= 0f)
            return;


        playerHealth.TakeDamage(
            damage
        );
    }


    private float CalculateHardcoreDamage()
    {
        int playerHits =
            breakableTarget != null
                ? breakableTarget.BrokenPieceCount
                : 0;


        float reduction =
            playerHits *
            damageReductionPerPlayerHit;


        float damageMultiplier =
            Mathf.Clamp01(
                1f -
                reduction / 100f
            );


        return
            baseDamage *
            damageMultiplier;
    }


    // =====================================================
    // HIT COLLIDERS
    // =====================================================

    private void SetHittable(
        bool hittable
    )
    {
        if (piecesRoot == null)
            return;


        Collider[] colliders =
            piecesRoot
                .GetComponentsInChildren<
                    Collider
                >(true);


        foreach (
            Collider pieceCollider
            in colliders
        )
        {
            pieceCollider.enabled =
                hittable;
        }
    }
}