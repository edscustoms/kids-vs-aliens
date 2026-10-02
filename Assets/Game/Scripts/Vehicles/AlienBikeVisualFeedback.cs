using UnityEngine;

/// <summary>Rideable-wrapper presentation only; all gameplay markers stay on the upright physics root.</summary>
[DefaultExecutionOrder(20), DisallowMultipleComponent]
public sealed class AlienBikeVisualFeedback : MonoBehaviour
{
    [SerializeField] private AlienBikeController bike;
    [Tooltip("Cosmetics only: keep colliders, seat and approach/dismount markers outside this pivot.")]
    public Transform bikeLeanPivot;
    public Transform steeringPivot;
    [Header("Turn presentation (degrees)")]
    [Min(0)] public float maxBikeLean = 8;
    [Min(0)] public float maxRiderLean = 10;
    [Min(0)] public float maxVisualSteeringAngle = 15;
    [Header("Response")]
    [Min(.1f)] public float leanResponseSpeed = 8;
    [Min(.1f)] public float steeringVisualResponseSpeed = 12;
    [Tooltip("Fraction of bike/rider lean retained at rest. Control steering still uses its full range.")]
    [Range(0, 1)] public float minimumSpeedInfluence = .15f;
    [Tooltip("Forward speed magnitude in m/s for full lean influence; does not alter vehicle speed.")]
    [Min(.1f)] public float fullLeanSpeed = 12;

    private Quaternion neutralBike, neutralSteering;
    private float lean, steering;
    private PlayerBikeRider previousRider;

    private void Awake()
    {
        if (bikeLeanPivot != null)
            neutralBike = bikeLeanPivot.localRotation;
        if (steeringPivot != null)
            neutralSteering = steeringPivot.localRotation;
    }

    private void LateUpdate()
    {
        var rider = bike != null && bike.isActiveAndEnabled ? bike.Rider : null;
        if (rider == null || !rider.IsDriving)
        {
            ResetPresentation();
            return;
        }
        if (previousRider != rider)
        {
            ResetPresentation();
            previousRider = rider;
        }
        float speedInfluence = Mathf.Lerp(minimumSpeedInfluence, 1,
            Mathf.Clamp01(Mathf.Abs(bike.ForwardSpeed) / fullLeanSpeed));
        float input = bike.SteerInput;
        lean = Mathf.Lerp(lean, input * speedInfluence, 1 - Mathf.Exp(-leanResponseSpeed * Time.deltaTime));
        steering = Mathf.Lerp(steering, input, 1 - Mathf.Exp(-steeringVisualResponseSpeed * Time.deltaTime));
        if (bikeLeanPivot != null)
            bikeLeanPivot.localRotation = neutralBike * Quaternion.AngleAxis(-lean * maxBikeLean, Vector3.forward);
        if (steeringPivot != null)
            steeringPivot.localRotation = neutralSteering * Quaternion.AngleAxis(steering * maxVisualSteeringAngle, Vector3.up);
        rider.SetVisualLean(-lean * maxRiderLean);
    }

    private void OnDisable() => ResetPresentation();

    private void ResetPresentation()
    {
        previousRider?.SetVisualLean(0);
        previousRider = null;
        lean = steering = 0;
        if (bikeLeanPivot != null)
            bikeLeanPivot.localRotation = neutralBike;
        if (steeringPivot != null)
            steeringPivot.localRotation = neutralSteering;
    }
}
