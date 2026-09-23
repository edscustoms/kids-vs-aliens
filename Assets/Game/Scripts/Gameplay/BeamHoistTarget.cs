using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BeamHoistTarget : MonoBehaviour
{
    private static readonly HashSet<BeamHoistTarget> activeTargets = new();
    public static IEnumerable<BeamHoistTarget> ActiveTargets => activeTargets;
    // The HUD polls range without allocating an interface enumerator or doing physics scans.
    internal static HashSet<BeamHoistTarget> RegisteredTargets => activeTargets;
    [Tooltip("Optional activation volume; otherwise range around this origin.")]
    [SerializeField] private Collider activationArea;
    [SerializeField, Min(0.1f)] private float activationRange = 2f;
    [Tooltip("Amy's root/feet position on the supported landing surface.")]
    [SerializeField] private Transform landing;
    [Tooltip("Optional world height marker. Only Y is used.")]
    [SerializeField] private Transform liftPoint;
    [SerializeField, Min(0.05f)] private float clearanceHeight = 0.35f;
    [SerializeField, Min(0.1f)] private float liftDuration = 1.5f;
    [SerializeField, Min(0.1f)] private float transferDuration = 0.6f;
    [SerializeField, Min(0.1f)] private float landingDuration = 0.3f;
    public Transform Landing => landing;
    public float LiftDuration => liftDuration;
    public float TransferDuration => transferDuration;
    public float LandingDuration => landingDuration;
    private void OnEnable() => activeTargets.Add(this);
    private void OnDisable() => activeTargets.Remove(this);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTargets() => activeTargets.Clear();

    public bool IsInRange(Vector3 position)
    {
        if (!isActiveAndEnabled || landing == null || landing.position.y <= position.y + 0.1f) return false;
        return activationArea != null
            ? activationArea.enabled && (activationArea.ClosestPoint(position) - position).sqrMagnitude < 0.01f
            : (transform.position - position).sqrMagnitude <= activationRange * activationRange;
    }

    public bool TryGetPath(Vector3 start, out Vector3 lift, out Vector3 across, out Vector3 end)
    {
        lift = across = end = start;
        if (!IsInRange(start)) return false;
        end = landing.position;
        float height = Mathf.Max(end.y + clearanceHeight, liftPoint != null ? liftPoint.position.y : end.y);
        lift = new Vector3(start.x, height, start.z);
        across = new Vector3(end.x, height, end.z);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, activationRange);
        if (!TryGetPath(transform.position, out var lift, out _, out var end)) return;
        Gizmos.DrawLine(transform.position, lift);
        var path = BeamHoistPath.Create(transform.position, end, lift.y, liftDuration, transferDuration + landingDuration);
        Vector3 previous = lift;
        for (int i = 1; i <= 16; i++)
        {
            Vector3 next = path.Evaluate(i / 16f);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
        Gizmos.DrawWireSphere(end, 0.2f);
    }
}
