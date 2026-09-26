using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns proximity and reversible door/lid motion; the visual is replaceable.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public sealed class HealingPodController : MonoBehaviour
{
    [Header("Visual references")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;
    [SerializeField] private Transform roof;

    [Header("Proximity")]
    [SerializeField] private SphereCollider proximityTrigger;
    [SerializeField, Min(0.1f)] private float proximityRadius = 3f;

    [Header("Motion from the authored closed pose (parent-local space)")]
    [SerializeField, Min(0.01f)] private float openCloseDuration = 0.7f;
    [SerializeField] private Vector3 leftDoorOpenOffset;
    [SerializeField] private Vector3 rightDoorOpenOffset;
    [SerializeField] private Vector3 roofHingeAxis = Vector3.right;
    [SerializeField] private float roofOpenAngle = -75f;

    private readonly HashSet<Collider> occupants = new HashSet<Collider>();
    private static readonly System.Predicate<Collider> UnavailableCollider = IsUnavailable;
    private Vector3 leftClosedPosition;
    private Vector3 rightClosedPosition;
    private Quaternion roofClosedRotation;
    private bool initialized;
    private float openness;

    public float Openness => openness;
    public bool IsPlayerNearby => occupants.Count > 0;

    private void Awake()
    {
        ConfigureTrigger();
        if (leftDoor == null || rightDoor == null || roof == null)
        {
            Debug.LogError($"{name}: Healing Pod requires both doors and a roof transform.", this);
            enabled = false;
            return;
        }

        // Capture once, so disable/reenable and interrupted motion cannot redefine rest poses.
        leftClosedPosition = leftDoor.localPosition;
        rightClosedPosition = rightDoor.localPosition;
        roofClosedRotation = roof.localRotation;
        initialized = true;
        ApplyPose();
    }

    private void Reset() => ConfigureTrigger();

    private void OnValidate()
    {
        proximityRadius = Mathf.Max(0.1f, proximityRadius);
        openCloseDuration = Mathf.Max(0.01f, openCloseDuration);
        ConfigureTrigger();
    }

    private void ConfigureTrigger()
    {
        if (proximityTrigger == null) proximityTrigger = GetComponent<SphereCollider>();
        if (proximityTrigger != null)
        {
            proximityTrigger.isTrigger = true;
            proximityTrigger.radius = proximityRadius;
        }
        var body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    private void OnTriggerEnter(Collider other) => TrackPlayer(other);

    // Recover after enabling while already inside, including a restored player pose.
    private void OnTriggerStay(Collider other) => TrackPlayer(other);

    private void TrackPlayer(Collider other)
    {
        if (!isActiveAndEnabled || other == null || other.isTrigger || occupants.Contains(other)) return;
        if (other.GetComponentInParent<PlayerCharacter>() != null) occupants.Add(other);
    }

    private void OnTriggerExit(Collider other) => occupants.Remove(other);

    private static bool IsUnavailable(Collider collider) =>
        collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy;

    private void Update()
    {
        // Unity may omit Exit when an overlapping collider is disabled or destroyed.
        if (occupants.Count > 0) occupants.RemoveWhere(UnavailableCollider);
        if (proximityTrigger == null || !proximityTrigger.enabled) occupants.Clear();
        float next = Mathf.MoveTowards(openness, IsPlayerNearby ? 1f : 0f,
            Time.deltaTime / openCloseDuration);
        if (next == openness) return;
        openness = next;
        ApplyPose();
    }

    private void ApplyPose()
    {
        float eased = Mathf.SmoothStep(0f, 1f, openness);
        leftDoor.localPosition = leftClosedPosition + leftDoorOpenOffset * eased;
        rightDoor.localPosition = rightClosedPosition + rightDoorOpenOffset * eased;
        roof.localRotation = roofClosedRotation * Quaternion.AngleAxis(roofOpenAngle * eased, roofHingeAxis);
    }

    private void OnDisable()
    {
        occupants.Clear();
        openness = 0f;
        if (initialized) ApplyPose();
    }
}
