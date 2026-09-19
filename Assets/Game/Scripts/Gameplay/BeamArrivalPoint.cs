using UnityEngine;

/// <summary>Scene-authored player root pose for fresh entry only. No movement or save ownership.</summary>
[DisallowMultipleComponent]
public sealed class BeamArrivalPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(.1f, .9f, 1f, .85f);
        Gizmos.DrawWireSphere(transform.position, .15f);
        Vector3 end = transform.position + transform.forward * 1.2f;
        Gizmos.DrawLine(transform.position, end);
        Gizmos.DrawLine(end, end - transform.forward * .3f + transform.right * .18f);
        Gizmos.DrawLine(end, end - transform.forward * .3f - transform.right * .18f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.8f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.9f, "Fresh arrival / facing");
#endif
    }
}
