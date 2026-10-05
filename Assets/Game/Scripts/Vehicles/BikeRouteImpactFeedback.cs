using UnityEngine;

/// <summary>BikeRoute player-only impact requests; the existing camera service owns the render pulse.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(AlienBikeController))]
public sealed class BikeRouteImpactFeedback : MonoBehaviour
{
    public CameraFeedbackProfile profile;
    [Min(0)] public float minimumVelocityChange = 1.5f;
    [Min(1)] public float fullVelocityChange = 9;
    [Min(.1f)] public float cooldown = .35f;
    private AlienBikeController bike;
    private float nextImpact;
    public int AcceptedImpacts { get; private set; }
    private void Awake() => bike = GetComponent<AlienBikeController>();
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponentInParent<EnemyBikeDriver>() == null) return;
        Present(collision.impulse.magnitude / Mathf.Max(1, bike.Body.mass), collision.relativeVelocity);
    }
    public void Present(float velocityChange, Vector3 direction)
    {
        if (bike == null || bike.Rider == null || !bike.Rider.IsDriving || Time.timeScale <= 0
            || Time.time < nextImpact || velocityChange < minimumVelocityChange) return;
        nextImpact = Time.time + cooldown; AcceptedImpacts++;
        CameraFeedbackService.Play(profile, Mathf.Lerp(.25f, 1,
            Mathf.InverseLerp(minimumVelocityChange, fullVelocityChange, velocityChange)), direction);
    }
}
