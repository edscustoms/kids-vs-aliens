using System;
using UnityEngine;

/// <summary>One unlimited-ammunition finish rifle. Uses normal weapon data, physical rays, EnemyHealth and pooled plasma effects.</summary>
public sealed class BikeRouteDefenseGun : MonoBehaviour
{
    [SerializeField] private Transform structureRoot, yawPivot, pitchPivot, muzzle;
    [SerializeField] private WeaponItemData rifle;
    [SerializeField] private EnemyActor[] targets = Array.Empty<EnemyActor>();
    [SerializeField] private PlasmaBoltVFX boltPrefab;
    [SerializeField] private PlasmaMuzzleVFX muzzlePrefab;
    [SerializeField] private PlasmaImpactVFX impactPrefab;
    [SerializeField, Min(0)] private float initialPhase;
    [SerializeField] private int targetPreference;
    [SerializeField, Min(.1f)] private float fireSeconds = 4, restSeconds = .5f;
    [SerializeField, Min(0)] private float spreadDegrees = 1.3f;
    public bool Active { get; private set; }
    public EnemyActor Target { get; private set; }
    public int ShotsFired { get; private set; }
    public float Phase => initialPhase;
    public event Action<Collider> ShotFired;
    private float activatedAt, nextShot, nextTarget;
    private int targetCycle;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    public void Activate()
    {
        if (Active) return;
        Active = true; activatedAt = Time.time;
        nextShot = Time.time + initialPhase;
        nextTarget = 0;
    }
    private static Vector3 AimPoint(EnemyActor actor)
    {
        var target = actor.GetComponent<AimTarget>();
        return target != null ? target.BodyCenter : actor.transform.position + Vector3.up;
    }
    private bool Eligible(EnemyActor actor) => actor != null && actor.isActiveAndEnabled && actor.IsAlive
        && Vector3.Distance(muzzle.position, AimPoint(actor)) < rifle.range
        && WeaponShotQuery.Clear(structureRoot, actor.transform, muzzle.position, AimPoint(actor), hits);
    private void Update()
    {
        if (!Active || Time.timeScale <= 0 || ActiveRunController.Instance?.IsReady != true) return;
        if (!Eligible(Target) || Time.time >= nextTarget)
        {
            Target = null; nextTarget = Time.time + .9f + targetPreference * .11f;
            for (int n = 0; n < targets.Length; n++)
            {
                int index = (targetPreference + targetCycle + n) % targets.Length;
                if (!Eligible(targets[index])) continue;
                Target = targets[index]; break;
            }
            targetCycle++;
        }
        if (Target == null) return;
        Vector3 delta = AimPoint(Target) - pitchPivot.position;
        Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
        if (flat.sqrMagnitude > .001f)
            yawPivot.rotation = Quaternion.RotateTowards(yawPivot.rotation, Quaternion.LookRotation(flat), 220 * Time.deltaTime);
        Vector3 local = yawPivot.InverseTransformDirection(delta.normalized);
        float pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
        pitchPivot.localRotation = Quaternion.RotateTowards(pitchPivot.localRotation, Quaternion.Euler(pitch, 0, 0), 160 * Time.deltaTime);
        if (Time.time < nextShot || Mathf.Repeat(Time.time - activatedAt + initialPhase, fireSeconds + restSeconds) >= fireSeconds
            || Vector3.Angle(muzzle.forward, AimPoint(Target) - muzzle.position) > 5) return;
        Fire();
    }
    private void Fire()
    {
        Vector3 start = muzzle.position;
        Vector2 spread = UnityEngine.Random.insideUnitCircle * spreadDegrees;
        Vector3 direction = muzzle.rotation * Quaternion.Euler(spread.y, spread.x, 0) * Vector3.forward;
        bool blocked = WeaponShotQuery.Cast(structureRoot, start, direction, rifle.range, hits, out var contact);
        // A rifle may hit intervening cover/aliens. Never send collateral damage to Amy or her hull.
        if (blocked && (contact.collider.GetComponentInParent<PlayerHealth>() != null
            || contact.collider.GetComponentInParent<AlienBikeController>()?.Rider != null)) return;
        nextShot = Time.time + 1 / Mathf.Max(.1f, rifle.fireRate);
        Vector3 end = blocked ? contact.point : start + direction * rifle.range;
        Vector3 normal = blocked ? contact.normal : -direction;
        Color color = new Color(.65f, .15f, 1);
        if (muzzlePrefab != null) VfxPool.Spawn(muzzlePrefab, start, Quaternion.LookRotation(direction))?.Play(color);
        AudioService.Play(rifle.fireSound, start);
        var hit = new HitInfo(rifle.damage, end, normal, direction, gameObject);
        if (blocked) CombatHitResolver.Resolve(contact.collider, hit)?.ReceiveHit(hit);
        var bolt = boltPrefab != null ? VfxPool.Spawn(boltPrefab, start, Quaternion.identity) : null;
        if (bolt != null) bolt.Initialize(start, end, color, blocked ? () => Impact(end, normal, color) : null);
        else if (blocked) Impact(end, normal, color);
        ShotsFired++; ShotFired?.Invoke(blocked ? contact.collider : null);
    }
    private void Impact(Vector3 point, Vector3 normal, Color color)
    {
        if (impactPrefab != null) VfxPool.Spawn(impactPrefab, point + normal * .01f, Quaternion.LookRotation(normal))?.Play(color);
    }
}
