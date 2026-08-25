using UnityEngine;

public class BreakableTargetPiece : MonoBehaviour
{
    public BreakableTarget Target { get; private set; }

    public bool IsBroken { get; private set; }

    public Collider PieceCollider { get; private set; }

    private Rigidbody rb;

    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;

    public void Initialize(BreakableTarget target)
    {
        Target = target;

        rb = GetComponent<Rigidbody>();
        PieceCollider = GetComponent<Collider>();

        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        originalLocalScale = transform.localScale;
    }

    public bool PunchOut(
        Vector3 hitPoint,
        Vector3 shotDirection,
        float force
    )
    {
        if (IsBroken || rb == null)
            return false;

        IsBroken = true;

        Target?.TemporarilyIgnorePieceCollisions(this);

        transform.SetParent(null, true);

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        rb.AddForceAtPosition(
            shotDirection.normalized * force,
            hitPoint,
            ForceMode.Impulse
        );

        return true;
    }

    public void Release()
    {
        if (IsBroken || rb == null)
            return;

        IsBroken = true;

        transform.SetParent(null, true);

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;
    }

    public void PrepareForReturn()
    {
        if (!IsBroken || rb == null)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.isKinematic = true;
        rb.useGravity = false;

        if (PieceCollider != null)
            PieceCollider.enabled = false;
    }

    public Vector3 GetOriginalWorldPosition()
    {
        if (originalParent == null)
            return transform.position;

        return originalParent.TransformPoint(
            originalLocalPosition
        );
    }

    public Quaternion GetOriginalWorldRotation()
    {
        if (originalParent == null)
            return transform.rotation;

        return
            originalParent.rotation *
            originalLocalRotation;
    }

    public void CompleteReturn()
    {
        if (originalParent == null)
            return;

        transform.SetParent(
            originalParent,
            false
        );

        transform.localPosition =
            originalLocalPosition;

        transform.localRotation =
            originalLocalRotation;

        transform.localScale =
            originalLocalScale;

        if (PieceCollider != null)
            PieceCollider.enabled = true;

        IsBroken = false;
    }
}