using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Editor-baked lower-side approach cells and supported top candidates; no runtime geometry analysis.</summary>
[DisallowMultipleComponent, AddComponentMenu("Gameplay/Beam Hoist Surface")]
public sealed class BeamHoistSurface : MonoBehaviour
{
    [Serializable]
    public struct Approach
    {
        public Bounds region;
        public Vector3 landing;
        public int side;
    }
    internal static readonly HashSet<BeamHoistSurface> Active = new();
    [Tooltip("Optional subset. Empty uses enabled, solid child colliders.")]
    [SerializeField] private Collider[] sourceColliders = Array.Empty<Collider>();
    [SerializeField, Min(0.1f)] private float landingInset = 0.55f;
    [SerializeField, Min(0.1f)] private float clearance = 0.5f;
    [Tooltip("Optional unusual-geometry landing point. Standard props require no children.")]
    [SerializeField] private Transform landingOverride;
    [SerializeField, HideInInspector] private Bounds footprint;
    [SerializeField, HideInInspector] private Approach[] approaches = Array.Empty<Approach>();
    [SerializeField, HideInInspector] private string bakeSignature;
    [SerializeField, HideInInspector] private string bakeStatus = "Not baked";
    public bool IsBaked => approaches != null && approaches.Length > 0;
    public int CandidateCount => approaches == null ? 0 : approaches.Length;
    public Approach GetBakedApproach(int index) => approaches[index];
    public Bounds Footprint => footprint;
    public string BakeStatus => bakeStatus;
    public string BakeSignature => bakeSignature;
    public Collider[] SourceColliders => sourceColliders;
    public float Clearance => clearance;
    public float LandingInset => landingInset;
    public Transform LandingOverride => landingOverride;
    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => Active.Clear();

    public bool TryGetCandidate(Vector3 root, int index, float feetOffset, out Vector3 landing, out float releaseHeight)
    {
        landing = default; releaseHeight = 0f;
        if (!isActiveAndEnabled || !IsBaked || index < 0 || index >= approaches.Length) return false;
        Vector3 local = transform.InverseTransformPoint(root + Vector3.up * feetOffset);
        // No activation inside the object's top footprint, including at/above the landing.
        if (local.x >= footprint.min.x && local.x <= footprint.max.x
            && local.z >= footprint.min.z && local.z <= footprint.max.z) return false;
        var candidate = approaches[index];
        if (!candidate.region.Contains(local)) return false;
        Vector3 top = transform.TransformPoint(candidate.landing);
        if (root.y + feetOffset >= top.y - 0.1f) return false;
        landing = top + Vector3.up * (0.02f - feetOffset);
        releaseHeight = Mathf.Max(top.y, transform.TransformPoint(new Vector3(0f, footprint.max.y, 0f)).y)
            + clearance - feetOffset;
        return true;
    }

#if UNITY_EDITOR
    public static event Action<BeamHoistSurface> BakeRequested;
    private void Reset() => BakeRequested?.Invoke(this);
    private void OnValidate() => BakeRequested?.Invoke(this);
    [ContextMenu("Refresh Cached Hoist Geometry")]
    public void RequestBake() => BakeRequested?.Invoke(this);
    public void StoreBake(Bounds bounds, Approach[] cells, string signature, string status)
    {
        footprint = bounds; approaches = cells; bakeSignature = signature; bakeStatus = status;
    }
    private void OnDrawGizmosSelected()
    {
        if (!IsBaked) return;
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(new Vector3(footprint.center.x, footprint.max.y, footprint.center.z),
            new Vector3(footprint.size.x, 0.02f, footprint.size.z));
        foreach (var cell in approaches)
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.65f);
            Gizmos.DrawWireCube(cell.region.center, cell.region.size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(cell.landing, 0.12f);
            Vector3 start = transform.TransformPoint(cell.region.center);
            Vector3 end = transform.TransformPoint(cell.landing);
            var path = BeamHoistPath.Create(start, end, transform.TransformPoint(new Vector3(0, footprint.max.y, 0)).y + clearance, 1.5f, 0.9f);
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawLine(start, path.release);
            Vector3 last = path.release;
            for (int i = 1; i <= 16; i++) { Vector3 next = path.Evaluate(i / 16f); Gizmos.DrawLine(last, next); last = next; }
            Gizmos.matrix = transform.localToWorldMatrix;
        }
        Gizmos.matrix = previous;
    }
#endif
}
