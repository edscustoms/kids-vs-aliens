using System.Collections;
using UnityEngine;

public class BreakableTarget : MonoBehaviour
{
    [Header("Break Physics")]
    [SerializeField]
    private float pieceMass = 0.12f;

    [SerializeField]
    private float punchForce = 5f;

    [SerializeField]
    private float collisionIgnoreDuration = 0.12f;

    [Header("Collapse")]
    [SerializeField]
    [Range(1f, 100f)]
    private float collapsePercentage = 20f;

    private Transform piecesRoot;
    private BreakableTargetPiece[] pieces;

    private int brokenPieceCount;
    private bool hasCollapsed;

    private void Awake()
    {
        FindPiecesRoot();
        SetupPieces();
    }

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

            BreakableTargetPiece piece = pieceTransform.GetComponent<BreakableTargetPiece>();

            if (piece == null)
            {
                piece = pieceTransform.gameObject.AddComponent<BreakableTargetPiece>();
            }

            MeshFilter meshFilter = pieceTransform.GetComponent<MeshFilter>();

            MeshCollider collider = pieceTransform.GetComponent<MeshCollider>();

            if (collider == null)
            {
                collider = pieceTransform.gameObject.AddComponent<MeshCollider>();
            }

            if (collider.sharedMesh == null && meshFilter != null)
            {
                collider.sharedMesh = meshFilter.sharedMesh;
            }

            collider.convex = true;

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

        Debug.Log($"{name}: Automatically setup " + $"{pieces.Length} breakable pieces.");
    }

    public void BreakPiece(BreakableTargetPiece piece, Vector3 hitPoint, Vector3 shotDirection)
    {
        if (piece == null || hasCollapsed)
            return;

        bool wasBroken = piece.PunchOut(hitPoint, shotDirection, punchForce);

        if (!wasBroken)
            return;

        brokenPieceCount++;

        float brokenPercentage = ((float)brokenPieceCount / pieces.Length) * 100f;

        if (brokenPercentage >= collapsePercentage)
        {
            CollapseTarget();
        }
    }

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

    private void CollapseTarget()
    {
        if (hasCollapsed)
            return;

        hasCollapsed = true;

        foreach (BreakableTargetPiece piece in pieces)
        {
            if (piece == null)
                continue;

            piece.Release();
        }
    }
}
