using UnityEngine;

/// <summary>LevelStart player-root/yaw gizmo. No independent spawn pose or save ownership.</summary>
[DisallowMultipleComponent]
public sealed class BeamArrivalPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        var owner = GetComponentInParent<PlayerBeamInSequence>();
        if (owner == null) return;
        var pose = owner.ArrivalTransform;
        Vector3 forward = Quaternion.Euler(0f, pose.eulerAngles.y, 0f) * Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Gizmos.color = new Color(.1f, .9f, 1f, .85f);
        Gizmos.DrawWireSphere(pose.position, .15f);
        Vector3 end = pose.position + forward * 1.2f;
        Gizmos.DrawLine(pose.position, end);
        Gizmos.DrawLine(end, end - forward * .3f + right * .18f);
        Gizmos.DrawLine(end, end - forward * .3f - right * .18f);
        Gizmos.DrawLine(pose.position, pose.position + Vector3.up * 1.8f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(pose.position + Vector3.up * 1.9f, "LevelStart: final player root / facing (fresh run only)");
#endif
    }
}
