using UnityEngine;

public class BreakableTargetPiece : MonoBehaviour
{
    public BreakableTarget Target { get; private set; }
    public bool IsBroken { get; private set; }
    public Collider PieceCollider { get; private set; }

    private Rigidbody rb;

    public void Initialize(BreakableTarget target)
    {
        Target = target;

        rb = GetComponent<Rigidbody>();
        PieceCollider = GetComponent<Collider>();
    }

    public bool PunchOut(Vector3 hitPoint, Vector3 shotDirection, float force)
    {
        if (IsBroken || rb == null)
            return false;

        IsBroken = true;

        // Briefly ignore the other target pieces,
        // so this piece can leave cleanly.
        Target?.TemporarilyIgnorePieceCollisions(this);

        transform.SetParent(null, true);

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        rb.AddForceAtPosition(shotDirection.normalized * force, hitPoint, ForceMode.Impulse);

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
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }
}
