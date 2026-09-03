using UnityEngine;

public static class GripAttachmentUtility
{
    public static bool AlignGripToSocket(
        Transform itemRoot,
        Transform gripPoint,
        Transform socket)
    {
        if (itemRoot == null ||
            gripPoint == null ||
            socket == null)
        {
            return false;
        }

        Quaternion rotationDelta =
            socket.rotation *
            Quaternion.Inverse(
                gripPoint.rotation);

        itemRoot.rotation =
            rotationDelta *
            itemRoot.rotation;

        itemRoot.position +=
            socket.position -
            gripPoint.position;

        itemRoot.SetParent(
            socket,
            true);

        return true;
    }
}
