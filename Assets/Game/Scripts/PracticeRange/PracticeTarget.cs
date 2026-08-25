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
    [Header("Hinge")]
    [SerializeField]
    private Vector3 inactiveHingeRotation = new Vector3(90f, 0f, 0f);

    [SerializeField]
    private float hingeDuration = 0.5f;

    [Header("Firing")]
    [SerializeField]
    private PlasmaBoltVFX plasmaBoltPrefab;

    [SerializeField]
    private float shotRange = 50f;

    [SerializeField]
    private Vector2 fireIntervalRange = new Vector2(1.5f, 3f);

    [Header("Accuracy")]
    [SerializeField]
    [Range(0f, 100f)]
    private float hitChance = 65f;

    [SerializeField]
    private float missRadius = 1.5f;

    [SerializeField]
    private Color boltColor = new Color(1f, 0.05f, 0.05f);

    [Header("Hardcore")]
    [SerializeField]
    private float baseDamage = 10f;

    [SerializeField]
    [Range(0f, 100f)]
    private float damageReductionPerPlayerHit = 20f;

    [SerializeField]
    private float hardcoreHitRadius = 0.6f;

    private Transform hingePivot;
    private Transform piecesRoot;
    private Transform fireOrigin;

    private Transform player;
    private PlayerHealth playerHealth;

    private BreakableTarget breakableTarget;

    private Quaternion activeHingeRotation;

    private Coroutine hingeRoutine;
    private Coroutine fireRoutine;

    private PracticeTargetState state = PracticeTargetState.Inactive;

    public PracticeTargetState State => state;

    private bool IsOperational =>
        state == PracticeTargetState.Active || state == PracticeTargetState.Hardcore;

    private void Awake()
    {
        FindRequiredObjects();

        if (hingePivot != null)
        {
            activeHingeRotation = hingePivot.localRotation;
        }

        breakableTarget = GetComponentInChildren<BreakableTarget>(true);
    }

    public void Initialize(Transform playerTransform)
    {
        player = playerTransform;

        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }
    }

    private void FindRequiredObjects()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == "HingePivot")
                hingePivot = child;

            if (child.name == "BreakablePieces")
                piecesRoot = child;

            if (child.name == "TargetFireOrigin")
                fireOrigin = child;
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
                $"{name}: Could not find 'TargetFireOrigin'. " + $"Target firing will be disabled."
            );
        }
    }

    public void SetState(PracticeTargetState newState, bool animate = true)
    {
        state = newState;

        if (hingeRoutine != null)
        {
            StopCoroutine(hingeRoutine);
            hingeRoutine = null;
        }

        UpdateFiring();

        if (!animate)
        {
            ApplyStateImmediately();
            return;
        }

        // Hittable while raising/lowering.
        SetHittable(true);

        hingeRoutine = StartCoroutine(AnimateHinge());
    }

    private void UpdateFiring()
    {
        if (fireRoutine != null)
        {
            StopCoroutine(fireRoutine);
            fireRoutine = null;
        }

        if (IsOperational)
        {
            fireRoutine = StartCoroutine(FireRoutine());
        }
    }

    private IEnumerator FireRoutine()
    {
        // Random initial delay so multiple
        // targets don't fire together.
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

        if (!willHit)
        {
            Vector2 randomOffset = Random.insideUnitCircle * missRadius;

            Vector3 directionToPlayer = (playerTargetPosition - fireOrigin.position).normalized;

            Vector3 horizontalAxis = Vector3.Cross(Vector3.up, directionToPlayer).normalized;

            aimPoint += horizontalAxis * randomOffset.x + Vector3.up * randomOffset.y;
        }

        Vector3 shotDirection = (aimPoint - fireOrigin.position).normalized;

        Vector3 shotEndPosition;

        if (willHit)
        {
            // Successful accuracy roll:
            // visually hits Amy.
            shotEndPosition = playerTargetPosition;
        }
        else
        {
            // Missed Amy:
            // continue past the miss point until
            // something in the world is actually hit.
            if (Physics.Raycast(fireOrigin.position, shotDirection, out RaycastHit hit, shotRange))
            {
                shotEndPosition = hit.point;
            }
            else
            {
                shotEndPosition = fireOrigin.position + shotDirection * shotRange;
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

    private void TryDealHardcoreDamage(Vector3 shotEndPosition)
    {
        if (player == null || playerHealth == null)
        {
            return;
        }

        // Lets Amy dodge the bolt.
        float distance = Vector3.Distance(player.position + Vector3.up, shotEndPosition);

        if (distance > hardcoreHitRadius)
            return;

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

    private IEnumerator AnimateHinge()
    {
        if (hingePivot == null)
            yield break;

        Quaternion startRotation = hingePivot.localRotation;

        Quaternion targetRotation =
            state == PracticeTargetState.Inactive ? GetInactiveRotation() : activeHingeRotation;

        float elapsed = 0f;

        while (elapsed < hingeDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / hingeDuration);

            float smoothT = t * t * (3f - 2f * t);

            hingePivot.localRotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);

            yield return null;
        }

        hingePivot.localRotation = targetRotation;

        if (state == PracticeTargetState.Inactive)
        {
            SetHittable(false);
        }

        hingeRoutine = null;
    }

    private Quaternion GetInactiveRotation()
    {
        return activeHingeRotation * Quaternion.Euler(inactiveHingeRotation);
    }

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
