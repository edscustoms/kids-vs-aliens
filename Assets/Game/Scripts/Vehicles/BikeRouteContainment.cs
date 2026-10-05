using UnityEngine;

/// <summary>BikeRoute's authored bridge flight lane and readable last-resort off-route defense.</summary>
[DefaultExecutionOrder(50), DisallowMultipleComponent]
public sealed class BikeRouteContainment : MonoBehaviour
{
    public BikeRouteChaseDirector chase;
    public int bridgePath = 5;
    public float flightStart = 380, flightEnd = 660;
    public float flightMargin = 5, escapeMargin = 32;
    public float warningSeconds = 3, defenseDamage = 25;
    public SoundEvent warningSound, defenseSound;
    public PlasmaImpactVFX defenseImpact;
    public bool Redirecting { get; private set; }
    public bool OutsideRoute { get; private set; }
    public float OutsideSeconds { get; private set; }
    private BikeRouteGuide.Sample road;
    private PlayerHealth health;
    private float nextSample, nextPulse, nextNotice;

    private void Awake() { if (chase != null) health = chase.Player.GetComponent<PlayerHealth>(); }
    private void OnDisable() => Clear();
    private void Clear() { Redirecting = OutsideRoute = false; OutsideSeconds = 0; nextPulse = nextSample = 0; }
    private bool Ready => chase != null && health != null && !health.IsDead && !chase.Finished
        && Time.timeScale > 0 && ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady;
    private void Update()
    {
        // Transient warning/flight state is intentionally cleared during both Continue passes.
        if (!Ready) { Clear(); return; }
        if (Time.time >= nextSample)
        {
            nextSample = Time.time + .1f;
            road = chase.Guide.Project(chase.Player.transform.position, road.path);
            float distance = Vector3.ProjectOnPlane(chase.Player.transform.position - road.position, Vector3.up).magnitude;
            OutsideRoute = distance > road.halfWidth + escapeMargin - (OutsideRoute ? 3 : 0);
        }
        if (!OutsideRoute)
        {
            if (OutsideSeconds > 0) RunSaveService.Notify("Back on route. Defense disengaged.");
            OutsideSeconds = 0; nextPulse = 0; return;
        }
        if (OutsideSeconds <= 0)
        {
            RunSaveService.Notify($"OFF ROUTE - RETURN NOW. Defense field activates in {Mathf.CeilToInt(warningSeconds)} seconds.");
            AudioService.Play2D(warningSound);
        }
        OutsideSeconds += Time.deltaTime;
        if (OutsideSeconds < warningSeconds || Time.time < nextPulse) return;
        nextPulse = Time.time + .5f;
        if (Time.time >= nextNotice)
        { RunSaveService.Notify("OFF ROUTE — DEFENSE FIELD ACTIVE. Return to the road!"); nextNotice = Time.time + 2; }
        Vector3 point = chase.Player.transform.position + Vector3.up;
        // A signposted environmental defense, through the existing health/death/feedback owner.
        health.ReceiveDamage(new HitInfo(defenseDamage, point, Vector3.up, Vector3.down, gameObject));
        if (defenseImpact != null) VfxPool.Spawn(defenseImpact, point, Quaternion.identity)?.Play(new Color(1, .2f, .08f));
        AudioService.Play(defenseSound, point);
    }
    private void FixedUpdate()
    {
        Redirecting = false;
        if (!Ready || !chase.Player.IsDriving || chase.Player.Bike == null || road.path != bridgePath
            || road.distance < flightStart || road.distance > flightEnd) return;
        var bike = chase.Player.Bike;
        float height = bike.Body.position.y - road.position.y;
        float lateral = Vector3.Dot(bike.Body.position - road.position, road.Right);
        float limit = road.halfWidth + flightMargin;
        if (bike.IsGrounded || height < 1.8f || Mathf.Abs(lateral) <= limit) return;
        Vector3 velocity = bike.Body.linearVelocity;
        Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
        if (horizontal.sqrMagnitude < 4) return;
        Vector3 inward = -road.Right * Mathf.Sign(lateral);
        if (Vector3.Dot(horizontal, inward) >= horizontal.magnitude * .3f) return;
        Vector3 desired = (Vector3.ProjectOnPlane(road.forward, Vector3.up).normalized + inward * .8f).normalized;
        horizontal = Vector3.RotateTowards(horizontal.normalized, desired, 180 * Mathf.Deg2Rad * Time.fixedDeltaTime, 0) * horizontal.magnitude;
        bike.Body.linearVelocity = horizontal + Vector3.up * velocity.y;
        // Align the chassis during this local safety turn; otherwise the shared lateral
        // grip immediately brakes the redirected velocity. No shared handling is changed.
        var heading = Vector3.ProjectOnPlane(bike.Body.rotation * Vector3.forward, Vector3.up).normalized;
        var aligned = Quaternion.FromToRotation(heading, horizontal.normalized) * bike.Body.rotation;
        bike.Body.MoveRotation(Quaternion.RotateTowards(bike.Body.rotation, aligned, 180 * Time.fixedDeltaTime));
        Redirecting = true;
        if (Time.time >= nextNotice)
        { RunSaveService.Notify("FLIGHT SAFETY — steer between the amber beacons"); nextNotice = Time.time + 4; }
    }
}
