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

        Vector3 playerTargetPosition = player.position + Vector3.up;

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

        Vector3 shotEndPosition;

        // -------------------------------------------------
        // HIT
        // -------------------------------------------------

        if (willHit)
        {
            shotEndPosition = playerTargetPosition;
        }
        // -------------------------------------------------
        // MISS CONTINUES INTO WORLD
        // -------------------------------------------------

        else
        {
            if (Physics.Raycast(fireOrigin.position, shotDirection, out RaycastHit hit, ShotRange))
            {
                shotEndPosition = hit.point;
            }
            else
            {
                shotEndPosition = fireOrigin.position + shotDirection * ShotRange;
            }
        }

        bool isHardcore = state == PracticeTargetState.Hardcore;

        PlasmaBoltVFX bolt = Instantiate(plasmaBoltPrefab);

        bolt.Initialize(
            fireOrigin.position,
            shotEndPosition,
            boltColor,
            isHardcore && willHit ? () => TryDealHardcoreDamage(shotEndPosition) : null
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
