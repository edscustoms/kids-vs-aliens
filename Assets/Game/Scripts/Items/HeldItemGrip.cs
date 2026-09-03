using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeldItemGrip : MonoBehaviour
{
    [SerializeField]
    private Transform gripPoint;

    [Tooltip("Optional world-space origin used when this held item is released.")]
    [SerializeField]
    private Transform releasePoint;

    public Transform GripPoint => gripPoint;

    public Vector3 ReleasePosition =>
        releasePoint != null
            ? releasePoint.position
            : transform.position;

    public bool AttachTo(
        Transform socket)
    {
        if (socket == null)
        {
            Debug.LogError(
                $"{name}: Cannot attach because the target socket is missing.",
                this);

            return false;
        }

        if (gripPoint == null)
        {
            Debug.LogError(
                $"{name}: HeldItemGrip has no GripPoint assigned.",
                this);

            return false;
        }

        return GripAttachmentUtility.AlignGripToSocket(
            transform,
            gripPoint,
            socket);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        AutoAssignReferences();
    }

    private void OnValidate()
    {
        AutoAssignReferences();
    }

    private void AutoAssignReferences()
    {
        if (gripPoint == null)
        {
            gripPoint =
                FindChildByName(
                    transform,
                    "GrenadeGripPoint");
        }

        if (releasePoint == null)
        {
            releasePoint =
                FindChildByName(
                    transform,
                    "ReleasePoint");
        }
    }

    private static Transform FindChildByName(
        Transform parent,
        string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform result =
                FindChildByName(
                    child,
                    childName);

            if (result != null)
                return result;
        }

        return null;
    }
#endif
}
