using UnityEngine;

/// <summary>A single authored cubic, baked once in the Editor. Invalid routes cannot play.</summary>
public sealed class AlienFlybyPath : MonoBehaviour
{
    public Transform start, control1, control2, end;
    public bool allowReverse = true;
    [Tooltip("Sphere enclosing bike, visual wake width, and safety margin. Wake follows the already cleared path.")]
    [Min(.1f)] public float clearanceRadius = 2.25f;
    [SerializeField, HideInInspector] private bool validated;
    [SerializeField, HideInInspector] private Vector3[] bakedPoints;
    [SerializeField, HideInInspector] private float[] distances;
    [SerializeField, HideInInspector] private float bakedRadius;
    public bool IsValidated => validated && bakedPoints != null && bakedPoints.Length == 4
        && distances != null && distances.Length > 1 && start != null && control1 != null && control2 != null && end != null
        && start.position == bakedPoints[0] && control1.position == bakedPoints[1]
        && control2.position == bakedPoints[2] && end.position == bakedPoints[3] && clearanceRadius == bakedRadius;
    public float Length => IsValidated ? distances[distances.Length - 1] : 0;
    public Vector3 Evaluate(float t)
    {
        float u = 1 - t;
        return u * u * u * start.position + 3 * u * u * t * control1.position + 3 * u * t * t * control2.position + t * t * t * end.position;
    }
    public Vector3 PositionAtDistance(float distance, bool reverse, out Vector3 tangent)
    {
        float d = Mathf.Clamp(reverse ? Length - distance : distance, 0, Length);
        int low = 0, high = distances.Length - 1;
        while (high - low > 1)
        {
            int mid = (low + high) / 2;
            if (distances[mid] < d)
                low = mid;
            else
                high = mid;
        }
        float t = Mathf.Lerp((float)low / (distances.Length - 1), (float)high / (distances.Length - 1),
            Mathf.InverseLerp(distances[low], distances[high], d));
        // Authored control positions are baked too: runtime hierarchy edits cannot alter validated motion.
        float u = 1 - t;
        var a = bakedPoints[0];
        var b = bakedPoints[1];
        var c = bakedPoints[2];
        var e = bakedPoints[3];
        tangent = (3 * u * u * (b - a) + 6 * u * t * (c - b) + 3 * t * t * (e - c)).normalized * (reverse ? -1 : 1);
        return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * e;
    }
#if UNITY_EDITOR
    public void SetValidation(bool valid, float[] table)
    {
        validated = valid; distances = table;
        bakedRadius = clearanceRadius;
        bakedPoints = valid ? new[] { start.position, control1.position, control2.position, end.position } : null;
    }
    private void OnDrawGizmosSelected()
    {
        if (start == null || control1 == null || control2 == null || end == null) return;
        Gizmos.color = IsValidated ? Color.cyan : Color.red;
        Vector3 previous = Evaluate(0);
        for (int i = 1; i <= 80; i++) { Vector3 p = Evaluate(i/80f); Gizmos.DrawLine(previous,p); previous=p; }
        Gizmos.DrawWireSphere(Evaluate(.5f), clearanceRadius);
    }
#endif
}
