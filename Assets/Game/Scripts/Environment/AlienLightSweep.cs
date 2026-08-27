using UnityEngine;

public class AlienLightSweep : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField]
    private Transform pointA;

    [SerializeField]
    private Transform pointB;

    [Tooltip("Seconds to travel from one side to the other.")]
    [SerializeField]
    private float travelDuration = 8f;

    [SerializeField]
    private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Aim")]
    [Tooltip("Center of the area the light should aim toward.")]
    [SerializeField]
    private Transform targetCenter;

    [Tooltip("Random aiming distance around Target Center.")]
    [SerializeField]
    private Vector3 targetRandomExtents = new Vector3(5f, 0f, 5f);

    [Tooltip("How often a new random aim point is selected.")]
    [SerializeField]
    private float retargetInterval = 2.5f;

    [Tooltip("How quickly the spotlight rotates toward its new target.")]
    [SerializeField]
    private float aimSpeed = 2f;

    private Vector3 currentAimPoint;
    private float nextRetargetTime;

    private void Start()
    {
        ChooseNewAimPoint();
    }

    private void Update()
    {
        UpdateMovement();
        UpdateAim();
    }

    private void UpdateMovement()
    {
        if (pointA == null || pointB == null)
            return;

        float normalizedTime = Mathf.PingPong(Time.time / travelDuration, 1f);

        float curvedTime = movementCurve.Evaluate(normalizedTime);

        transform.position = Vector3.Lerp(pointA.position, pointB.position, curvedTime);
    }

    private void UpdateAim()
    {
        if (targetCenter == null)
            return;

        if (Time.time >= nextRetargetTime)
            ChooseNewAimPoint();

        Vector3 direction = currentAimPoint - transform.position;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            aimSpeed * Time.deltaTime
        );
    }

    private void ChooseNewAimPoint()
    {
        Vector3 offset = new Vector3(
            Random.Range(-targetRandomExtents.x, targetRandomExtents.x),
            Random.Range(-targetRandomExtents.y, targetRandomExtents.y),
            Random.Range(-targetRandomExtents.z, targetRandomExtents.z)
        );

        currentAimPoint = targetCenter.position + offset;

        nextRetargetTime = Time.time + retargetInterval;
    }
}
