using UnityEngine;

/// <summary>One real, straight, swept projectile launched by AlienBikeLaserWeapon. Independent of shooter lifetime.</summary>
public sealed class AlienBikeLaserBolt : MonoBehaviour
{
    public LineRenderer core, halo;
    public TrailRenderer trail;
    public PlasmaImpactVFX impactPrefab;
    public SoundEvent impactSound;
    [Min(.01f)] public float collisionRadius = .25f;
    [Min(1)] public float maximumDistance = 440;
    public Vector3 Direction { get; private set; }
    public float Travelled { get; private set; }
    public bool Flying { get; private set; }
    public Collider LastHit { get; private set; }
    private Transform owner, passenger;
    private GameObject instigator;
    private float speed, damage;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly Collider[] overlaps = new Collider[32];
    public void Launch(Transform source, Transform rider, GameObject attacker, Vector3 direction, float velocity, float amount)
    {
        owner = source; passenger = rider; instigator = attacker;
        Direction = direction.normalized; speed = velocity; damage = amount;
        Travelled = 0; Flying = true; LastHit = null;
        if (trail != null) { trail.Clear(); trail.emitting = true; }
        Draw();
    }
    private bool Eligible(Collider collider) => collider != null && !collider.isTrigger
        && (owner == null || collider.transform != owner && !collider.transform.IsChildOf(owner))
        && (passenger == null || collider.transform != passenger && !collider.transform.IsChildOf(passenger))
        && collider.GetComponentInParent<ProjectilePassThroughObstacle>() == null;
    private void FixedUpdate()
    {
        if (!Flying || Time.timeScale <= 0) return;
        float distance = Mathf.Min(speed * Time.fixedDeltaTime, maximumDistance - Travelled);
        Vector3 start = transform.position;
        // SphereCast does not report a collider already overlapping its origin.
        int overlapCount = Physics.OverlapSphereNonAlloc(start, collisionRadius, overlaps, ~0, QueryTriggerInteraction.Ignore);
        var touching = overlapCount == overlaps.Length ? Physics.OverlapSphere(start, collisionRadius, ~0, QueryTriggerInteraction.Ignore) : overlaps;
        if (touching != overlaps) overlapCount = touching.Length;
        Collider initial = null; float closest = float.PositiveInfinity;
        for (int i = 0; i < overlapCount; i++)
            if (Eligible(touching[i]))
            {
                float d = (touching[i].ClosestPoint(start) - start).sqrMagnitude;
                if (d < closest) { closest = d; initial = touching[i]; }
            }
        if (initial != null) { Impact(initial, start, -Direction); return; }
        int count = Physics.SphereCastNonAlloc(start, collisionRadius, Direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
        var contacts = count == hits.Length ? Physics.SphereCastAll(start, collisionRadius, Direction, distance, ~0, QueryTriggerInteraction.Ignore) : hits;
        if (contacts != hits) count = contacts.Length;
        RaycastHit nearest = default; closest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
            if (Eligible(contacts[i].collider) && contacts[i].distance < closest) { nearest = contacts[i]; closest = nearest.distance; }
        if (nearest.collider != null) { Impact(nearest.collider, nearest.point, nearest.normal); return; }
        Travelled += distance; transform.position = start + Direction * distance; Draw();
        if (Travelled >= maximumDistance) Release();
    }
    private void Draw()
    {
        // Grow from the muzzle; never draw the initial tail backwards through its bike.
        if (core != null) { core.SetPosition(0, transform.position); core.SetPosition(1, transform.position - Direction * Mathf.Min(Travelled, 4.5f)); }
        if (halo != null) { halo.SetPosition(0, transform.position); halo.SetPosition(1, transform.position - Direction * Mathf.Min(Travelled, 5)); }
    }
    private void Impact(Collider collider, Vector3 point, Vector3 normal)
    {
        LastHit = collider;
        var hit = new HitInfo(damage, point, normal, Direction, instigator);
        CombatHitResolver.Resolve(collider, hit)?.ReceiveHit(hit);
        if (impactPrefab != null) VfxPool.Spawn(impactPrefab, point + normal * .08f, Quaternion.LookRotation(normal))?.Play(new Color(1, .08f, .32f));
        AudioService.Play(impactSound, point);
        Release();
    }
    private void Release() { Flying = false; VfxPool.Release(this); }
    private void OnDisable() { Flying = false; if (trail != null) { trail.emitting = false; trail.Clear(); } }
}
