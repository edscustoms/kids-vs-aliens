using UnityEngine;
using UnityEngine.AI;

/// <summary>Opt-in, local, unarmed-only scavenging. No inventory or player skill dependencies.</summary>
[DisallowMultipleComponent]
public sealed class EnemyWeaponAwareness : MonoBehaviour
{
    [SerializeField] private bool allowWeaponPickup;
    private EnemyEquipment equipment;
    private EnemyMotor motor;
    private EnemyHealth health;
    private PickupItem reserved;
    private float nextScan, giveUp, nextPath;
    private Vector3 destination;
    private NavMeshPath path;
    private readonly RaycastHit[] hits = new RaycastHit[24];
    public PickupItem ReservedWeapon => reserved;
    private void Awake() { path = new NavMeshPath(); equipment = GetComponent<EnemyEquipment>(); motor = GetComponent<EnemyMotor>(); health = GetComponent<EnemyHealth>(); }
    private void OnEnable()
    {
        if (motor != null) motor.MovementLocksChanged += OnLocks;
        if (health != null) health.OnDied += Cancel;
    }
    private void OnDisable()
    {
        Cancel();
        if (motor != null) motor.MovementLocksChanged -= OnLocks;
        if (health != null) health.OnDied -= Cancel;
    }
    private void OnLocks(EnemyMovementLockReason locks) { if (locks != EnemyMovementLockReason.None) Cancel(); }
    public void Cancel() { if (reserved != null) reserved.ReleaseReservation(this); reserved = null; }
    public bool Tick(Transform visibleThreat)
    {
        var config = equipment != null ? equipment.Profile : null;
        if (!isActiveAndEnabled || !allowWeaponPickup || config == null || !config.weaponPickupEnabled || equipment.HasWeapon
            || health == null || health.IsDead || motor == null || motor.MovementLocked || !motor.IsReady
            || (visibleThreat != null && Vector3.Distance(transform.position, visibleThreat.position) <= config.immediateThreatDistance))
        { Cancel(); return false; }
        if (reserved != null && (!reserved.IsReservedBy(this) || Time.time > giveUp)) { Cancel(); nextScan = Time.time + config.weaponScanInterval; }
        if (reserved == null && Time.time >= nextScan)
        {
            nextScan = Time.time + config.weaponScanInterval;
            float nearest = config.weaponInterestRadius * config.weaponInterestRadius;
            PickupItem best = null;
            foreach (var pickup in PickupItem.Available)
            {
                if (pickup == null || !pickup.CanReserve(this) || !(pickup.Item is WeaponItemData weapon) || !equipment.CanUse(weapon)) continue;
                float distance = (pickup.transform.position - transform.position).sqrMagnitude;
                if (distance >= nearest || !Visible(pickup) || !Reachable(pickup)) continue;
                best = pickup; nearest = distance;
            }
            if (best != null && best.TryReserve(this)) { reserved = best; giveUp = Time.time + config.pickupTimeout; nextPath = 0; }
        }
        if (reserved == null) return false;
        if (Vector3.Distance(transform.position, reserved.transform.position) <= config.pickupDistance)
        {
            if (Visible(reserved)) equipment.TryAcquire(reserved, this);
            Cancel(); return false;
        }
        if (Time.time >= nextPath)
        {
            nextPath = Time.time + config.weaponScanInterval;
            if (!Reachable(reserved) || !motor.SetDestination(destination)) { Cancel(); return false; }
        }
        return true;
    }
    private bool Visible(PickupItem item) => WeaponShotQuery.Clear(transform, item.transform, transform.position + Vector3.up * .9f, item.transform.position + Vector3.up * .1f, hits);
    private bool Reachable(PickupItem item)
    {
        if (!NavMesh.SamplePosition(item.transform.position, out var point, .8f, motor.Agent.areaMask)
            || !motor.Agent.CalculatePath(point.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        destination = point.position; return true;
    }
}
