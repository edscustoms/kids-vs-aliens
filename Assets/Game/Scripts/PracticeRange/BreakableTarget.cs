using System.Collections;
using UnityEngine;

public class BreakableTarget : MonoBehaviour
{
    [Header("Break Physics")]
    [SerializeField]
    private float pieceMass = 0.24f;

    [SerializeField]
    private float punchForce = 2.5f;

    [SerializeField]
    private float collisionIgnoreDuration = 0.12f;

    [Header("Collapse")]
    [SerializeField]
    [Range(1f, 100f)]
    private float collapsePercentage = 12f;

    [Header("Reset")]
    [SerializeField]
    private float explosionResetDelay = 3f;

    [SerializeField]
    private float inactiveResetDelay = 30f;

    [Header("Magic Return")]
    [SerializeField]
    private float returnDuration = 1.1f;

    [SerializeField]
    private float returnStagger = 0.25f;

    [SerializeField]
    private float returnArcHeight = 0.4f;

    [SerializeField]
    private float returnSwirlAmount = 0.2f;

    [SerializeField]
    private float returnSpinDegrees = 180f;

    private Transform piecesRoot;
    private BreakableTargetPiece[] pieces;

    private int brokenPieceCount;

    public int BrokenPieceCount => brokenPieceCount;

    private bool hasCollapsed;

    public bool IsCollapsed => hasCollapsed;

    private bool isReassembling;

    public bool IsReassembling => isReassembling;

    private float lastHitTime;

    // =====================================================
    // UNITY
    // =====================================================

    private void Awake()
    {
        FindPiecesRoot();
        SetupPieces();
    }

    private void Update()
    {
        if (brokenPieceCount <= 0 || hasCollapsed || isReassembling)
        {
            return;
        }

        if (Time.time - lastHitTime >= inactiveResetDelay)
        {
            StartCoroutine(ReassembleTarget());
        }
    }

    // =====================================================
    // SETUP
    // =====================================================

    private void FindPiecesRoot()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == "BreakablePieces")
            {
                piecesRoot = child;
                return;
            }
        }

        Debug.LogError($"{name}: Could not find a child named 'BreakablePieces'.");
    }

    private void SetupPieces()
    {
        if (piecesRoot == null)
            return;

        pieces = new BreakableTargetPiece[piecesRoot.childCount];

        for (int i = 0; i < piecesRoot.childCount; i++)
        {
            Transform pieceTransform = piecesRoot.GetChild(i);

            // -------------------------------------------------
            // BREAKABLE TARGET PIECE
            //
            // This lightweight gameplay component may still
            // be added at runtime for now.
            //
            // The expensive/important part is the MeshCollider,
            // which is now baked into the prefab beforehand.
            // -------------------------------------------------

            BreakableTargetPiece piece = pieceTransform.GetComponent<BreakableTargetPiece>();

            if (piece == null)
            {
                piece = pieceTransform.gameObject.AddComponent<BreakableTargetPiece>();
            }

            // -------------------------------------------------
            // BAKED MESH COLLIDER
            //
            // IMPORTANT:
            //
            // We no longer:
            //
            // - AddComponent<MeshCollider>()
            // - assign MeshFilter.sharedMesh
            // - ask Unity to cook collider geometry at runtime
            //
            // The collider must already exist on the prefab,
            // created by BreakableTargetColliderBaker.
            //
            // This lets the original FBX mesh have
            // Read/Write disabled in the final build.
            // -------------------------------------------------

            MeshCollider collider = pieceTransform.GetComponent<MeshCollider>();

            if (collider == null)
            {
                Debug.LogError(
                    $"{name}/{pieceTransform.name}: "
                        + "Missing baked MeshCollider. "
                        + "Run Tools > Kids VS Aliens > "
                        + "Bake Selected Target Colliders."
                );

                continue;
            }

            if (collider.sharedMesh == null)
            {
                Debug.LogError(
                    $"{name}/{pieceTransform.name}: " + "Baked MeshCollider has no mesh assigned."
                );

                continue;
            }

            if (!collider.convex)
            {
                Debug.LogWarning(
                    $"{name}/{pieceTransform.name}: "
                        + "MeshCollider was not convex. "
                        + "Setting convex=true."
                );

                collider.convex = true;
            }

            // -------------------------------------------------
            // RIGIDBODY
            //
            // Rigidbody can still safely be created at runtime.
            //
            // It does NOT require CPU-readable mesh data.
            // We can optionally bake these later too, but there
            // is no need to complicate this cleanup right now.
            // -------------------------------------------------

            Rigidbody rb = pieceTransform.GetComponent<Rigidbody>();

            if (rb == null)
            {
                rb = pieceTransform.gameObject.AddComponent<Rigidbody>();
            }

            rb.mass = pieceMass;

            rb.isKinematic = true;

            rb.useGravity = false;

            piece.Initialize(this);

            pieces[i] = piece;
        }
    }

    // =====================================================
    // BREAK
    // =====================================================

    public void BreakPiece(BreakableTargetPiece piece, Vector3 hitPoint, Vector3 shotDirection)
    {
        if (piece == null || hasCollapsed || isReassembling)
        {
            return;
        }

        bool wasBroken = piece.PunchOut(hitPoint, shotDirection, punchForce);

        if (!wasBroken)
            return;

        brokenPieceCount++;

        lastHitTime = Time.time;

        float brokenPercentage = ((float)brokenPieceCount / pieces.Length) * 100f;

        if (brokenPercentage >= collapsePercentage)
        {
            CollapseTarget();
        }
    }

    // =====================================================
    // TEMPORARY COLLISION IGNORE
    // =====================================================

    public void TemporarilyIgnorePieceCollisions(BreakableTargetPiece shotPiece)
    {
        StartCoroutine(IgnorePieceCollisionsRoutine(shotPiece));
    }

    private IEnumerator IgnorePieceCollisionsRoutine(BreakableTargetPiece shotPiece)
    {
        if (shotPiece == null || shotPiece.PieceCollider == null)
        {
            yield break;
        }

        Collider shotCollider = shotPiece.PieceCollider;

        foreach (BreakableTargetPiece piece in pieces)
        {
            if (piece == null || piece == shotPiece || piece.PieceCollider == null)
            {
                continue;
            }

            Physics.IgnoreCollision(shotCollider, piece.PieceCollider, true);
        }

        yield return new WaitForSeconds(collisionIgnoreDuration);

        if (shotCollider == null)
            yield break;

        foreach (BreakableTargetPiece piece in pieces)
        {
            if (piece == null || piece == shotPiece || piece.PieceCollider == null)
            {
                continue;
            }

            Physics.IgnoreCollision(shotCollider, piece.PieceCollider, false);
        }
    }

    // =====================================================
    // COLLAPSE
    // =====================================================

    private void CollapseTarget()
    {
        if (hasCollapsed)
            return;

        hasCollapsed = true;

        // Keep piece-vs-piece collisions.
        // Glorious cardboard explosion stays. 😂
        foreach (BreakableTargetPiece piece in pieces)
        {
            if (piece == null)
                continue;

            piece.Release();
        }

        StartCoroutine(ReassembleAfterExplosion());
    }

    private IEnumerator ReassembleAfterExplosion()
    {
        yield return new WaitForSeconds(explosionResetDelay);

        yield return ReassembleTarget();
    }

    // =====================================================
    // REASSEMBLY
    // =====================================================

    private IEnumerator ReassembleTarget()
    {
        if (isReassembling)
            yield break;

        isReassembling = true;

        foreach (BreakableTargetPiece piece in pieces)
        {
            if (piece == null || !piece.IsBroken)
            {
                continue;
            }

            piece.PrepareForReturn();
        }

        float longestReturnTime = 0f;

        foreach (BreakableTargetPiece piece in pieces)
        {
            if (piece == null || !piece.IsBroken)
            {
                continue;
            }

            float delay = Random.Range(0f, returnStagger);

            longestReturnTime = Mathf.Max(longestReturnTime, delay + returnDuration);

            StartCoroutine(ReturnPieceRoutine(piece, delay));
        }

        yield return new WaitForSeconds(longestReturnTime + 0.05f);

        RestoreAllPieceCollisions();

        brokenPieceCount = 0;

        hasCollapsed = false;

        isReassembling = false;
    }

    private IEnumerator ReturnPieceRoutine(BreakableTargetPiece piece, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (piece == null)
            yield break;

        Vector3 startPosition = piece.transform.position;

        Quaternion startRotation = piece.transform.rotation;

        float randomDirection = Random.value < 0.5f ? -1f : 1f;

        float phase = Random.Range(0f, Mathf.PI * 2f);

        float elapsed = 0f;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / returnDuration);

            float eased = t * t * (3f - 2f * t);

            Vector3 targetPosition = piece.GetOriginalWorldPosition();

            Quaternion targetRotation = piece.GetOriginalWorldRotation();

            Vector3 travelDirection = targetPosition - startPosition;

            Vector3 side = Vector3.Cross(Vector3.up, travelDirection.normalized);

            if (side.sqrMagnitude < 0.001f)
            {
                side = Vector3.right;
            }

            side.Normalize();

            float magicEnvelope = Mathf.Sin(t * Mathf.PI);

            float arc = magicEnvelope * returnArcHeight;

            float swirl = Mathf.Sin(t * Mathf.PI * 2f + phase) * magicEnvelope * returnSwirlAmount;

            Vector3 position = Vector3.Lerp(startPosition, targetPosition, eased);

            position += Vector3.up * arc;

            position += side * swirl;

            piece.transform.position = position;

            Quaternion rotation = Quaternion.Slerp(startRotation, targetRotation, eased);

            float magicSpin = magicEnvelope * returnSpinDegrees * randomDirection;

            piece.transform.rotation = Quaternion.AngleAxis(magicSpin, Vector3.up) * rotation;

            yield return null;
        }

        piece.CompleteReturn();
    }

    // =====================================================
    // COLLISION RESTORE
    // =====================================================

    private void RestoreAllPieceCollisions()
    {
        for (int i = 0; i < pieces.Length; i++)
        {
            BreakableTargetPiece pieceA = pieces[i];

            if (pieceA == null || pieceA.PieceCollider == null)
            {
                continue;
            }

            for (int j = i + 1; j < pieces.Length; j++)
            {
                BreakableTargetPiece pieceB = pieces[j];

                if (pieceB == null || pieceB.PieceCollider == null)
                {
                    continue;
                }

                Physics.IgnoreCollision(pieceA.PieceCollider, pieceB.PieceCollider, false);
            }
        }
    }
}
