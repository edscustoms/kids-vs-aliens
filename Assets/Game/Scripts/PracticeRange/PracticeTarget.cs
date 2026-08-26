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
    private Vector2 fireIntervalRange = new Vector2(1.5f, 3f);

    [SerializeField]
    private Color boltColor = new Color(1f, 0.05f, 0.05f);

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

    private static readonly Vector3 InactiveHingeRotation = new Vector3(90f, 0f, 0f);

    private const float ShotRange = 50f;
    private const float HardcoreHitRadius = 0.6f;

    // =====================================================
    // REFERENCES
    // =====================================================

    private Transform hingePivot;
    private Transform piecesRoot;
    private Transform fireOrigin;

    private Transform player;
    private PlayerHealth playerHealth;
    private CharacterController playerController;

    private readonly RaycastHit[] shotHits = new RaycastHit[32];

    private BreakableTarget breakableTarget;

    // =====================================================
    // STATE
    // =====================================================

    private Quaternion activeHingeRotation;

    private Coroutine hingeRoutine;
    private Coroutine fireRoutine;

    private PracticeTargetState state = PracticeTargetState.Inactive;

    // =====================================================
    // PROPERTIES
    // =====================================================

    public PracticeTargetState State => state;

    public float HingeDuration => hingeDuration;

    private bool IsOperational =>
        state == PracticeTargetState.Active || state == PracticeTargetState.Hardcore;

    // =====================================================
    // UNITY
    // =====================================================

    private void Awake()
    {
        FindRequiredObjects();

        if (hingePivot != null)
        {
            activeHingeRotation = hingePivot.localRotation;
        }

        breakableTarget = GetComponentInChildren<BreakableTarget>(true);
    }

    // =====================================================
    // INITIALIZATION
    // =====================================================

    public void Initialize(Transform playerTransform)
    {
        player = playerTransform;

        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
            playerController = player.GetComponent<CharacterController>();
        }
    }

    // =====================================================
    // FIND OBJECTS
    // =====================================================

    private void FindRequiredObjects()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == "HingePivot")
            {
                hingePivot = child;
            }

            if (child.name == "BreakablePieces")
            {
                piecesRoot = child;
            }

            if (child.name == "TargetFireOrigin")
            {
                fireOrigin = child;
            }
        }

        if (hingePivot == null)
        {
            Debug.LogError($"{name}: Could not find 'HingePivot'.");
        }

        if (piecesRoot == null)
        {
            Debug.LogError($"{name}: Could not find 'BreakablePieces'.");
        }

        if (fireOrigin == null)
        {
            Debug.LogWarning(
                $"{name}: Could not find 'TargetFireOrigin'. " + "Target firing will be disabled."
            );
        }
    }

    // =====================================================
    // STATE
    // =====================================================

    public void SetState(PracticeTargetState newState, bool animate = true)
    {
        PracticeTargetState previousState = state;

        state = newState;

        if (hingeRoutine != null)
        {
            StopCoroutine(hingeRoutine);

            hingeRoutine = null;
        }

        StopFiring();

        bool wasOperational = previousState != PracticeTargetState.Inactive;

        bool willBeOperational = newState != PracticeTargetState.Inactive;

        // Active -> Hardcore or Hardcore -> Active.
        // No hinge animation required.
        if (wasOperational && willBeOperational)
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

        hingeRoutine = StartCoroutine(AnimateHinge());
    }

    // =====================================================
    // HINGE
    // =====================================================

    private IEnumerator AnimateHinge()
    {
        if (hingePivot == null)
        {
            hingeRoutine = null;

            yield break;
        }

        Quaternion startRotation = hingePivot.localRotation;

        Quaternion targetRotation =
            state == PracticeTargetState.Inactive ? GetInactiveRotation() : activeHingeRotation;

        float duration = Mathf.Max(0.01f, hingeDuration);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            float smoothT = t * t * (3f - 2f * t);

            hingePivot.localRotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);

            yield return null;
        }

        hingePivot.localRotation = targetRotation;

        if (state == PracticeTargetState.Inactive)
        {
            SetHittable(false);
        }
        else
        {
            StartFiring();
        }

        hingeRoutine = null;
    }

    private void ApplyStateImmediately()
    {
        if (hingePivot == null)
            return;

        if (state == PracticeTargetState.Inactive)
        {
            hingePivot.localRotation = GetInactiveRotation();

            SetHittable(false);
        }
        else
        {
            hingePivot.localRotation = activeHingeRotation;

            SetHittable(true);
        }
    }

    private Quaternion GetInactiveRotation()
    {
        return activeHingeRotation * Quaternion.Euler(InactiveHingeRotation);
    }

    // =====================================================
    // FIRING
    // =====================================================

    private void StartFiring()
    {
        if (!IsOperational)
            return;

        if (plasmaBoltPrefab == null || fireOrigin == null || player == null)
        {
            return;
        }

        if (fireRoutine != null)
        {
            StopCoroutine(fireRoutine);
        }

        fireRoutine = StartCoroutine(FireRoutine());
    }

    public void StopFiring()
    {
        if (fireRoutine == null)
            return;

        StopCoroutine(fireRoutine);

        fireRoutine = null;
    }

    private IEnumerator FireRoutine()
    {
        yield return new WaitForSeconds(Random.Range(fireIntervalRange.x, fireIntervalRange.y));

        while (IsOperational)
        {
            Fire();

            yield return new WaitForSeconds(Random.Range(fireIntervalRange.x, fireIntervalRange.y));
        }

        fireRoutine = null;
    }

    // =====================================================
    // PLAYER VISIBILITY / SHOT COLLISION
    //
    // V1 NOTE:
    //
    // Practice targets must not know Amy's exact position
    // through walls.
    //
    // This logic intentionally lives here for the mobile
    // POC. After the first Android build works, combat LOS
    // and shot resolution should be moved into reusable
    // components shared by practice targets and real enemies.
    //
    // Rules:
    // - Own target colliders never block this target.
    // - Amy does not block the visibility test to herself.
    // - Any other solid collider before Amy blocks vision.
    // - The actual shot stops at the FIRST real world hit,
    //   so Hardcore damage can never pass through a wall.
    // =====================================================

    public bool CanSeePlayer()
    {
        if (player == null || fireOrigin == null)
        {
            return false;
        }

        Vector3 targetPosition = GetPlayerTargetPosition();
        Vector3 direction = targetPosition - fireOrigin.position;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
            return true;

        direction /= distance;

        int hitCount = Physics.RaycastNonAlloc(
            fireOrigin.position,
            direction,
            shotHits,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = shotHits[i];

            if (hit.collider == null)
                continue;

            if (IsOwnCollider(hit.collider))
                continue;

            if (IsPlayerCollider(hit.collider))
                continue;

            if (hit.distance < distance - 0.02f)
                return false;
        }

        return true;
    }

    private Vector3 GetPlayerTargetPosition()
    {
        if (playerController != null)
        {
            return playerController.bounds.center;
        }

        return player.position + Vector3.up;
    }

    private bool TryGetFirstShotHit(Vector3 direction, out RaycastHit closestHit)
    {
        closestHit = default;

        int hitCount = Physics.RaycastNonAlloc(
            fireOrigin.position,
            direction,
            shotHits,
            ShotRange,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        bool foundHit = false;
        float closestDistance = Mathf.Infinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = shotHits[i];

            if (hit.collider == null)
                continue;

            // Never let a target immediately shoot itself.
            if (IsOwnCollider(hit.collider))
                continue;

            if (hit.distance >= closestDistance)
                continue;

            closestDistance = hit.distance;
            closestHit = hit;
            foundHit = true;
        }

        return foundHit;
    }

    private bool IsOwnCollider(Collider collider)
    {
        if (collider == null)
            return false;

        Transform hitTransform = collider.transform;

        return hitTransform == transform || hitTransform.IsChildOf(transform);
    }

    private bool IsPlayerCollider(Collider collider)
    {
        if (player == null || collider == null)
            return false;

        Transform hitTransform = collider.transform;

        return hitTransform == player || hitTransform.IsChildOf(player);
    }

    private void Fire()
    {
        if (plasmaBoltPrefab == null || fireOrigin == null || player == null)
        {
            return;
        }

        if (
            breakableTarget != null
            && (breakableTarget.IsCollapsed || breakableTarget.IsReassembling)
        )
        {
            return;
        }

        // Do not fire when Amy is hidden by a wall, prop,
        // another target, or any other solid obstruction.
        if (!CanSeePlayer())
            return;

        Vector3 playerTargetPosition = GetPlayerTargetPosition();

        bool willHit = Random.Range(0f, 100f) <= hitChance;

        Vector3 aimPoint = playerTargetPosition;

        // -------------------------------------------------
        // MISS OFFSET
        // -------------------------------------------------

        if (!willHit)
        {
            Vector2 randomOffset = Random.insideUnitCircle * missRadius;

            Vector3 directionToPlayer = (playerTargetPosition - fireOrigin.position).normalized;

            Vector3 horizontalAxis = Vector3.Cross(Vector3.up, directionToPlayer).normalized;

            aimPoint += horizontalAxis * randomOffset.x + Vector3.up * randomOffset.y;
        }

        Vector3 shotDirection = (aimPoint - fireOrigin.position).normalized;

        // -------------------------------------------------
        // REAL PHYSICAL SHOT
        //
        // Hits and misses both resolve against the world.
        // The bolt stops at the first real collider instead
        // of magically passing through walls to its endpoint.
        // -------------------------------------------------

        bool hitSomething = TryGetFirstShotHit(shotDirection, out RaycastHit worldHit);

        Vector3 shotEndPosition =
            hitSomething ? worldHit.point : fireOrigin.position + shotDirection * ShotRange;

        bool isHardcore = state == PracticeTargetState.Hardcore;

        // -------------------------------------------------
        // HARDCORE DAMAGE
        //
        // Damage requires BOTH:
        // 1. this shot was rolled as an intended hit
        // 2. Amy was physically the FIRST object hit
        //
        // A wall therefore prevents damage automatically.
        // Miss shots remain harmless even if random spread
        // happens to cross Amy, preserving the existing rule.
        // -------------------------------------------------

        bool genuinelyHitPlayer =
            willHit && hitSomething && IsPlayerCollider(worldHit.collider);

        PlasmaBoltVFX bolt = Instantiate(plasmaBoltPrefab);

        bolt.Initialize(
            fireOrigin.position,
            shotEndPosition,
            boltColor,
            isHardcore && genuinelyHitPlayer
                ? () => TryDealHardcoreDamage(shotEndPosition)
                : null
        );
    }

    // =====================================================
    // HARDCORE DAMAGE
    // =====================================================

    private void TryDealHardcoreDamage(Vector3 shotEndPosition)
    {
        if (player == null || playerHealth == null)
        {
            return;
        }

        float distance = Vector3.Distance(player.position + Vector3.up, shotEndPosition);

        if (distance > HardcoreHitRadius)
        {
            return;
        }

        float damage = CalculateHardcoreDamage();

        if (damage <= 0f)
            return;

        playerHealth.TakeDamage(damage);
    }

    private float CalculateHardcoreDamage()
    {
        int playerHits = breakableTarget != null ? breakableTarget.BrokenPieceCount : 0;

        float reduction = playerHits * damageReductionPerPlayerHit;

        float damageMultiplier = Mathf.Clamp01(1f - reduction / 100f);

        return baseDamage * damageMultiplier;
    }

    // =====================================================
    // HIT COLLIDERS
    // =====================================================

    private void SetHittable(bool hittable)
    {
        if (piecesRoot == null)
            return;

        Collider[] colliders = piecesRoot.GetComponentsInChildren<Collider>(true);

        foreach (Collider pieceCollider in colliders)
        {
            pieceCollider.enabled = hittable;
        }
    }
}
