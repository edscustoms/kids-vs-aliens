using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>AI-owned aim/cadence. Every shot is revalidated after the current animated muzzle pose.</summary>
[DisallowMultipleComponent]
public sealed class EnemyRangedAttack : MonoBehaviour
{
    [SerializeField] private PlasmaBoltVFX boltPrefab;
    [SerializeField] private PlasmaMuzzleVFX muzzlePrefab;
    [SerializeField] private PlasmaImpactVFX impactPrefab;
    private EnemyEquipment equipment;
    private EnemyCombatPresentation presentation;
    private EnemyMotor motor;
    private EnemyHealth health;
    private EnemyMeleeAttack melee;
    private Transform target;
    private bool closeMode, requested;
    private float readyAt, nextShot, reloadAt, nextReposition;
    private int burst;
    public enum PositionDecision { None, Hold, Approach, Reposition }
    public PositionDecision Decision { get; private set; }
    private float nextDecision, holdUntil, moveUntil;
    private int burstLimit, sidePreference;
    private EnemyCombatProfile.RangedBehavior Behavior => Profile.BehaviorFor(equipment.Weapon);
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private NavMeshPath path;
    public bool InCloseMode => closeMode;
    public event Action<HitInfo, Collider> ShotFired;
    private EnemyCombatProfile Profile => equipment != null ? equipment.Profile : null;
    private bool Interrupted => !isActiveAndEnabled || health == null || health.IsDead || motor == null || motor.MovementLocked;
    private void Awake()
    {
        path = new NavMeshPath();
        sidePreference = UnityEngine.Random.value < .5f ? -1 : 1;
        equipment = GetComponent<EnemyEquipment>(); presentation = GetComponent<EnemyCombatPresentation>();
        motor = GetComponent<EnemyMotor>(); health = GetComponent<EnemyHealth>(); melee = GetComponent<EnemyMeleeAttack>();
    }
    private void OnEnable()
    {
        if (motor != null) motor.MovementLocksChanged += OnLocks;
        if (health != null) health.OnDied += Cancel;
        if (equipment != null) equipment.Changed += Cancel;
    }
    private void OnDisable()
    {
        Cancel();
        if (motor != null) motor.MovementLocksChanged -= OnLocks;
        if (health != null) health.OnDied -= Cancel;
        if (equipment != null) equipment.Changed -= Cancel;
    }
    private void OnLocks(EnemyMovementLockReason value) { if (value != EnemyMovementLockReason.None) Cancel(); }
    public void Cancel() { target = null; requested = false; burst = 0; reloadAt = 0; Decision = PositionDecision.None; nextDecision = 0; }
    private static float Sample(Vector2 range) => UnityEngine.Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    private void Hold(bool settle)
    {
        motor.Stop(); Decision = PositionDecision.Hold;
        holdUntil = Time.time + Mathf.Max(.2f, Sample(Behavior.positionHoldSeconds));
        if (settle) readyAt = Time.time + Profile.aimDelay * Mathf.Max(0, Sample(Behavior.aimDelayMultiplier));
    }
    /// <returns>True when ranged combat owns this frame's navigation/action decision.</returns>
    public bool TickVisibleTarget(Transform candidate)
    {
        requested = false;
        if (!isActiveAndEnabled || candidate == null || equipment == null || !equipment.HasWeapon || Profile == null || !Profile.rangedEnabled)
        { Cancel(); if (equipment != null) equipment.SetRangedPresentation(false); return false; }
        float distance = Vector3.Distance(transform.position, candidate.position);
        if (Profile.meleeEnabled)
        {
            if (distance <= Profile.meleeEnterDistance) closeMode = true;
            else if (distance >= Mathf.Max(Profile.meleeEnterDistance + .2f, Profile.rangedResumeDistance) && (melee == null || !melee.IsAttacking)) closeMode = false;
        }
        else closeMode = false;
        equipment.SetRangedPresentation(!closeMode);
        if (closeMode) { Cancel(); return false; }
        if (Interrupted) { Cancel(); return true; }
        if (target != candidate) { Cancel(); target = candidate; Hold(true); }
        Vector3 aim = AimPoint(candidate);
        bool clear = HasClearMuzzle(candidate, aim);
        float maximum = Mathf.Min(equipment.Weapon.range * .9f, Profile.preferredRange * Mathf.Max(Behavior.rangeBand.x, Behavior.rangeBand.y));
        float minimum = Mathf.Min(maximum, Profile.preferredRange * Mathf.Min(Behavior.rangeBand.x, Behavior.rangeBand.y));
        if (Decision == PositionDecision.Approach || Decision == PositionDecision.Reposition)
        {
            // Keep the chosen destination; a tiny target step never recomputes it.
            // Turning while sidestepping briefly swings the muzzle clear of cover. Finish that
            // short move instead of stopping, turning back into the blocker and restarting aim.
            bool useful = clear && distance <= maximum && Decision == PositionDecision.Approach;
            if (useful || motor.HasReachedDestination || Time.time >= moveUntil) Hold(true);
            else return true;
        }
        bool outsideWeaponRange = distance >= equipment.Weapon.range * .95f;
        if (Time.time >= nextDecision)
        {
            nextDecision = Time.time + Mathf.Max(.05f, Sample(Profile.decisionInterval));
            bool expired = Time.time >= holdUntil && burst == 0;
            if (outsideWeaponRange || (expired && distance > maximum))
            {
                float preferred = Mathf.Clamp(Profile.preferredRange * UnityEngine.Random.Range(.9f, 1.1f), minimum, maximum);
                Vector3 destination = candidate.position + Vector3.ProjectOnPlane(transform.position - candidate.position, Vector3.up).normalized * preferred;
                if (CommitMove(destination, PositionDecision.Approach)) return true;
            }
            else if (!clear && Time.time >= nextReposition)
            {
                nextReposition = Time.time + Profile.repositionInterval;
                Vector3 side = Vector3.Cross(Vector3.up, (candidate.position - transform.position).normalized);
                Vector3 preferredSide = transform.position + side * sidePreference * 1.2f;
                Vector3 otherSide = transform.position - side * sidePreference * 1.2f;
                // Prefer an actual firing lane over stepping toward another alien's blocked lane.
                if (TryClearReposition(preferredSide, candidate, aim) || TryClearReposition(otherSide, candidate, aim)
                    || CommitMove(preferredSide, PositionDecision.Reposition)
                    || CommitMove(transform.position - side * sidePreference * 1.2f, PositionDecision.Reposition)) return true;
            }
            else if (expired && clear)
            {
                // Variation happens between completed bursts, never in reaction to each small player movement.
                if (UnityEngine.Random.value < Behavior.deliberateStepChance)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, (candidate.position - transform.position).normalized) * sidePreference;
                    Vector3 destination = transform.position + side * UnityEngine.Random.Range(.8f, 1.25f);
                    if (WeaponShotQuery.Clear(transform, candidate, destination + Vector3.up * 1.2f, aim, hits)
                        && CommitMove(destination, PositionDecision.Reposition)) return true;
                }
                Hold(false); // Renew position commitment without restarting aim acquisition.
            }
        }
        motor.FacePosition(candidate.position); requested = clear;
        return true;
    }
    private bool CommitMove(Vector3 destination, PositionDecision decision)
    {
        if (!MoveTo(destination)) return false;
        Decision = decision; moveUntil = Time.time + Mathf.Max(.2f, Profile.movementCommitTimeout);
        burst = 0; requested = false; return true;
    }
    private bool TryClearReposition(Vector3 destination, Transform candidate, Vector3 aim)
    {
        Vector3 facing = Vector3.ProjectOnPlane(candidate.position - destination, Vector3.up);
        if (facing.sqrMagnitude < .001f || equipment.Muzzle == null) return false;
        Vector3 localMuzzle = Quaternion.Inverse(transform.rotation) * (equipment.Muzzle.position - transform.position);
        Vector3 projectedMuzzle = destination + Quaternion.LookRotation(facing) * localMuzzle;
        return WeaponShotQuery.Clear(transform, transform, destination + Vector3.up * .9f, projectedMuzzle, hits)
            && WeaponShotQuery.Clear(transform, candidate, projectedMuzzle, aim, hits)
            && CommitMove(destination, PositionDecision.Reposition);
    }
    private bool MoveTo(Vector3 destination)
    {
        if (!motor.IsReady || !NavMesh.SamplePosition(destination, out var point, 1.2f, motor.Agent.areaMask)
            || !motor.Agent.CalculatePath(point.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        return motor.SetDestination(point.position);
    }
    private static Vector3 AimPoint(Transform other)
    {
        var body = other.GetComponent<Collider>();
        return body != null ? body.bounds.center : other.position + Vector3.up * .9f;
    }
    private bool HasClearMuzzle(Transform other, Vector3 aim)
    {
        if (equipment.Muzzle == null) return false;
        Vector3 start = equipment.Muzzle.position;
        return WeaponShotQuery.Clear(transform, transform, transform.position + Vector3.up * .9f, start, hits)
            && WeaponShotQuery.Clear(transform, other, start, aim, hits);
    }
    private void LateUpdate()
    {
        if (!requested || Time.timeScale <= 0 || Interrupted || target == null || !target.gameObject.activeInHierarchy
            || !equipment.HasWeapon || presentation == null || !presentation.ReadyToFire) return;
        var targetHealth = target.GetComponent<PlayerHealth>();
        if (targetHealth != null && targetHealth.IsDead) { Cancel(); return; }
        if (Time.time < readyAt || Time.time < nextShot) return;
        var weapon = equipment.Weapon;
        if (equipment.Ammo == 0)
        {
            if (reloadAt == 0) reloadAt = Time.time + Mathf.Max(.1f, weapon.reloadTime);
            if (Time.time < reloadAt) return;
            equipment.Reload(); reloadAt = 0; readyAt = Time.time + Profile.aimDelay; return;
        }
        Vector3 aim = AimPoint(target);
        Vector3 flat = Vector3.ProjectOnPlane(aim - transform.position, Vector3.up).normalized;
        if (Vector3.Angle(transform.forward, flat) > Profile.facingTolerance) return;
        Physics.SyncTransforms();
        if (!HasClearMuzzle(target, aim)) return;
        Vector3 start = equipment.Muzzle.position, direction = (aim - start).normalized;
        if (Vector3.Distance(start, aim) > weapon.range) return;
        // Reject a sideways/backwards weapon pose rather than emitting a magic torso shot.
        if (Vector3.Angle(equipment.Muzzle.forward, direction) > 35) return;
        Vector2 spread = UnityEngine.Random.insideUnitCircle * Profile.spreadDegrees;
        direction = Quaternion.LookRotation(direction) * Quaternion.Euler(spread.y, spread.x, 0) * Vector3.forward;
        if (!equipment.SpendRound()) return;
        nextShot = Time.time + 1f / Mathf.Max(.1f, weapon.fireRate);
        if (burst == 0) burstLimit = weapon.fireMode == WeaponFireMode.Automatic
            ? UnityEngine.Random.Range(Mathf.Max(1, Profile.automaticBurstCount - Profile.automaticBurstVariation), Mathf.Max(1, Profile.automaticBurstCount) + 1) : 1;
        if (++burst >= burstLimit) { burst = 0; nextShot = Mathf.Max(nextShot, Time.time + Profile.burstPause * Mathf.Max(0, Sample(Behavior.burstPauseMultiplier))); }
        bool blocked = WeaponShotQuery.Cast(transform, start, direction, weapon.range, hits, out var contact);
        Vector3 end = blocked ? contact.point : start + direction * weapon.range;
        Color color = presentation.Visual.AuraColor;
        presentation.ShowFire();
        if (muzzlePrefab != null) { var vfx = VfxPool.Spawn(muzzlePrefab, start, Quaternion.LookRotation(direction)); if (vfx != null) vfx.Play(color); }
        var hit = new HitInfo(weapon.damage, end, blocked ? contact.normal : -direction, direction, gameObject);
        // Hitscan weapon path: damage is authoritative at this physical ray's contact, never a scheduled target hit.
        if (blocked) CombatHitResolver.Resolve(contact.collider, hit)?.ReceiveHit(hit);
        if (boltPrefab != null)
        {
            var bolt = VfxPool.Spawn(boltPrefab, start, Quaternion.identity);
            if (bolt != null) bolt.Initialize(start, end, color, blocked ? () => ShowImpact(end, hit.Normal, color) : null);
        }
        else if (blocked) ShowImpact(end, hit.Normal, color);
        ShotFired?.Invoke(hit, blocked ? contact.collider : null);
    }
    private void ShowImpact(Vector3 point, Vector3 normal, Color color)
    {
        if (impactPrefab == null) return;
        var impact = VfxPool.Spawn(impactPrefab, point + normal * .01f, Quaternion.LookRotation(normal));
        if (impact != null) impact.Play(color);
    }
}
