using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Editor bake inputs on an EditorOnly child. No runtime behavior or automatic rebake.</summary>
[DisallowMultipleComponent]
public sealed class AlienGroundAuroraAuthoring : MonoBehaviour
{
    public AlienGroundAuroraController controller;
    public AlienGroundAuroraSchedule output;
    public Collider[] allowedGround = Array.Empty<Collider>();
    [Tooltip("Bounds in the controller's local space. Y defines the ground-search range.")]
    public Bounds[] allowedZones = { new Bounds(Vector3.zero, new Vector3(76, 30, 96)) };
    public Bounds[] excludedZones = Array.Empty<Bounds>();
    [Tooltip("Bake rejects these complete footprints, expanded by exclusionPadding.")]
    public Transform[] excludedObjects = Array.Empty<Transform>();
    [Min(0)] public float exclusionPadding = 2;
    public int seed = 73129;
    [Header("Count and schedule (explicit bake only)")]
    [Tooltip("Exact number of distinct spots/events in one loop. This is not the simultaneous active count.")]
    [Min(1)] public int eventCount = 19;
    [FormerlySerializedAs("locationCount")]
    [Tooltip("Editor candidate search budget, not the exported event count. The bake may use fewer candidates if space is limited.")]
    [Min(1)] public int candidateLocationCount = 72;
    [Range(1, 14)] public int maximumActive = 14;
    [Min(10)] public float scheduleDuration = 96;
    [Tooltip("0 evenly staggers pool tracks/rests; 1 allows the full seeded timing variation.")]
    [Range(0, 1)] public float timingSpread = .75f;
    [Tooltip("Minimum / maximum lifetime in seconds, including both fades.")]
    public Vector2 lifetimeRange = new Vector2(8, 20);
    [Header("Coverage (metres, minimum / maximum)")]
    public Vector2 lengthRange = new Vector2(9, 18);
    public Vector2 widthRange = new Vector2(4.5f, 8);
    [Header("Fades (seconds, minimum / maximum)")]
    public Vector2 fadeInRange = new Vector2(2.5f, 3.5f);
    public Vector2 fadeOutRange = new Vector2(3, 4);
    [Header("Footprint (baked shader parameters)")]
    [Tooltip("Soft dissolve band around every lobe and the outer perimeter. Larger is softer.")]
    [Range(.1f, .7f)] public float edgeSoftness = .4f;
    [Tooltip("Amount of uneven lobes, inlets and perimeter erosion; never expands outside validated coverage.")]
    [Range(0, 1)] public float irregularity = .85f;
    [Tooltip("0 repeats a common footprint; 1 uses the full seeded variation in branches and distortion.")]
    [Range(0, 1)] public float shapeVariation = 1;
    [Header("Existing motion and clearance")]
    public Vector2 movementSpeedRange = new Vector2(.12f, .25f);
    [Min(0)] public float locationSpacing = 4;
    [Min(0)] public float activeSpacing = .8f;
    [Range(0, 45)] public float slopeLimit = 12;
    [Range(.01f, .2f)] public float groundTolerance = .06f;
    public Color[] palette = { new Color(.08f, 1.2f, 1.05f, .42f), new Color(.08f, 1.1f, .72f, .4f),
        new Color(.14f, 1.1f, 1.5f, .4f), new Color(.7f, .16f, 1.15f, .32f) };

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (controller == null) return;
        Gizmos.matrix = controller.transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        foreach (var zone in allowedZones) Gizmos.DrawWireCube(zone.center, zone.size);
        Gizmos.color = Color.red;
        foreach (var zone in excludedZones) Gizmos.DrawWireCube(zone.center, zone.size);
        if (output == null) { Gizmos.matrix = Matrix4x4.identity; return; }
        Gizmos.color = new Color(.2f, 1, .7f, .4f);
        foreach (var value in output.events)
        {
            Gizmos.matrix = controller.transform.localToWorldMatrix * Matrix4x4.TRS(value.position, value.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(value.dimensions.x, .2f, value.dimensions.y));
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
