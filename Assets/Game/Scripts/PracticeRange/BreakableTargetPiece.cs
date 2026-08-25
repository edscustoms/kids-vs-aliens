using UnityEngine;

public class BreakableTargetPiece : MonoBehaviour
{
    public BreakableTarget Target { get; private set; }

    public bool IsBroken { get; private set; }

    private Rigidbody rb;

    public void Initialize(BreakableTarget target)
    {
        Target = target;
        rb = GetComponent<Rigidbody>();
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

        transform.SetParent(null, true);

        rb.isKinematic = false;
        rb.useGravity = true;

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
    }
}