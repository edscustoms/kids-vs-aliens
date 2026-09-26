using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    [SerializeField]
    private WeaponItemData equippedWeapon;

    [SerializeField]
    private Transform muzzle;

    [SerializeField]
    private PlayerAim playerAim;

    [SerializeField]
    private PlayerCharacter playerCharacter;

    [SerializeField]
    private PlayerSkillState playerSkillState;

    [Header("Shot VFX")]
    [SerializeField]
    private PlasmaBoltVFX plasmaBoltPrefab;

    [Tooltip("Travel speed if the bolt presentation asset is unavailable; damage still waits for arrival.")]
    [SerializeField, Min(.01f)] private float missingBoltSpeed = PlasmaBoltVFX.DefaultSpeed;
    private readonly List<PendingImpact> pendingImpacts = new();
    private bool processingImpacts;
    private struct PendingImpact { public float arrival; public System.Action commit; }

    private IEnumerator CommitPendingImpacts()
    {
        // A coroutine keeps accepted shots alive when Beam transport disables
        // this input consumer. Scaled time still pauses damage and visuals together.
        while (pendingImpacts.Count != 0)
        {
            yield return null;
            for (int i = 0; i < pendingImpacts.Count;)
            {
                var impact = pendingImpacts[i];
                if (Time.time < impact.arrival) { i++; continue; }
                pendingImpacts.RemoveAt(i);
                impact.commit();
            }
        }
        processingImpacts = false;
    }

    [SerializeField]
    private PlasmaMuzzleVFX plasmaMuzzlePrefab;

    [SerializeField]
    private PlasmaImpactVFX plasmaImpactPrefab;

    // Presentation-only access to the same cosmetic shot prefabs used by gameplay.
    // Knowledge previews instantiate these directly and never call Shoot(), consume ammo,
    // raycast, apply damage or touch progression.
    public PlasmaBoltVFX PlasmaBoltPrefab => plasmaBoltPrefab;
    public PlasmaMuzzleVFX PlasmaMuzzlePrefab => plasmaMuzzlePrefab;

    // =====================================================
    // CACHED
    // =====================================================

    private StarterAssetsInputs input;
    private CharacterController characterController;
    private PlayerPrimaryActionRouter primaryActionRouter;

    private OwnedWeaponState weaponState;
    public OwnedWeaponState ActiveWeaponState => weaponState;
    public int CurrentAmmo => weaponState != null ? weaponState.Rounds : 0;
    public bool IsReloading => weaponState != null && weaponState.IsReloading;
    public void RestoreRunAmmo(int ammo) => weaponState?.Restore(ammo, Time.time);
    private bool triggerHeld;
    private bool shootWasPressed;
    private bool fireBlocked;
    private int shootMask;
    private PlayerFeedback feedback;
    private SkillData reportedMissingSkill;

    private readonly RaycastHit[] muzzleSafetyHits = new RaycastHit[16];

    // Normal weapon rays need to collect multiple hits because projectile-
    // transparent surfaces (for example chain-link fence barriers) may be
    // physically in front of the real target.
    private readonly RaycastHit[] weaponHits = new RaycastHit[32];

    // =====================================================
    // INITIALIZATION
    // =====================================================

    private void Awake()
    {
        feedback = GetComponent<PlayerFeedback>();
        shootMask = ~LayerMask.GetMask("Player");

        input = GetComponent<StarterAssetsInputs>();

        primaryActionRouter = GetComponent<PlayerPrimaryActionRouter>();

        characterController = GetComponent<CharacterController>();

        if (playerAim == null)
        {
            playerAim = GetComponent<PlayerAim>();
        }

        if (playerCharacter == null)
        {
            playerCharacter = GetComponent<PlayerCharacter>();
        }

        if (playerSkillState == null)
        {
            playerSkillState = GetComponent<PlayerSkillState>();
        }
    }

    // =====================================================
    // UPDATE
    // =====================================================

    private void Update()
    {
        if (weaponState != null && weaponState.FinishReload(Time.time)) ActiveRunController.Instance?.MarkDirty();
        bool shootPressed =
            primaryActionRouter != null && primaryActionRouter.isActiveAndEnabled
                ? triggerHeld
                : input != null && input.shoot;

        bool shootPressedThisFrame = shootPressed && !shootWasPressed;

        shootWasPressed = shootPressed;

        if (!shootPressed)
            reportedMissingSkill = null;

        if (fireBlocked)
            return;

        if (equippedWeapon == null || muzzle == null)
        {
            return;
        }

        if (IsReloading)
            return;

        bool wantsToShoot;

        if (equippedWeapon.fireMode == WeaponFireMode.Automatic)
        {
            wantsToShoot = shootPressed;
        }
        else
        {
            wantsToShoot = shootPressedThisFrame;
        }

        if (!wantsToShoot)
            return;

        if (!CanUseEquippedWeapon())
        {
            if (reportedMissingSkill != equippedWeapon.requiredSkill)
            {
                reportedMissingSkill = equippedWeapon.requiredSkill;
                feedback?.Report(
                    new GameplayFeedbackEvent(
                        FeedbackCode.MissingSkill,
                        reportedMissingSkill,
                        equippedWeapon,
                        FeedbackAction.Fire
                    )
                );
            }
            return;
        }

        reportedMissingSkill = null;

        if (weaponState == null || Time.time < weaponState.NextFireTime)
            return;

        if (CurrentAmmo <= 0)
        {
            BeginReload();

            return;
        }

        Shoot();
    }

    // =====================================================
    // SHOOTING
    // =====================================================

    private bool CanUseEquippedWeapon()
    {
        if (equippedWeapon == null)
            return false;

        SkillData requiredSkill = equippedWeapon.requiredSkill;

        if (requiredSkill == null)
            return true;

        return playerSkillState != null && playerSkillState.HasSkill(requiredSkill);
    }

    private void Shoot()
    {
        if (playerAim == null)
            return;

        if (!playerAim.TryGetShotAimPoint(muzzle.position, out Vector3 shotAimPoint))
        {
            return;
        }

        if (weaponState == null || !weaponState.TrySpendRound(Time.time)) return;
        ActiveRunController.Instance?.MarkDirty();

        Vector3 direction = (shotAimPoint - muzzle.position).normalized;

        Vector3 endPoint = muzzle.position + direction * equippedWeapon.range;

        Color? auraColor = GetAuraColor();

        bool didHit = false;

        Vector3 hitPoint = Vector3.zero;

        Vector3 hitNormal = Vector3.zero;

        Collider hitCollider = null;

        HitInfo hitInfo = default;

        // =================================================
        // MUZZLE WALL SAFETY
        // =================================================

        bool muzzleBlocked = TryGetMuzzleObstruction(out RaycastHit muzzleObstruction);
        if (muzzleBlocked)
        {
            didHit = true;

            endPoint = muzzleObstruction.point;

            hitPoint = muzzleObstruction.point;

            hitNormal = muzzleObstruction.normal;

            hitCollider = muzzleObstruction.collider;

            hitInfo = CreateHitInfo(hitPoint, hitNormal, direction);
        }
        // =================================================
        // NORMAL WEAPON RAY
        // =================================================

        else if (
            TryGetFirstWeaponHit(
                muzzle.position,
                direction,
                equippedWeapon.range,
                out RaycastHit hit
            )
        )
        {
            didHit = true;

            endPoint = hit.point;

            hitPoint = hit.point;

            hitNormal = hit.normal;

            hitCollider = hit.collider;

            hitInfo = CreateHitInfo(hitPoint, hitNormal, direction);
        }

        // =================================================
        // VFX
        // =================================================

        SpawnMuzzleVFX(muzzle.position, direction, auraColor);
        AudioService.Play(equippedWeapon.fireSound, muzzle.position);
        // PlayerShooter owns local input; enemy fire and previews use separate presentation paths.
        // Preserve the existing obstructed-shot impact/ammo behavior without a success pulse.
        if (!muzzleBlocked)
        {
            HapticService.Play(equippedWeapon.fireHaptic);
            CameraFeedbackService.Play(equippedWeapon.fireCameraFeedback);
        }

        System.Action onArrive = null;

        if (didHit)
        {
            // IMPORTANT:
            //
            // The raycast still decides immediately what this shot hit.
            // But gameplay damage + health UI + Hit/Death presentation
            // are now committed together when the visible plasma bolt
            // reaches that recorded impact point.
            //
            // This keeps:
            // plasma impact
            // health bar change
            // Hit animation
            // Death animation
            // all on the same visual frame.
            Collider committedCollider = hitCollider;

            HitInfo committedHit = hitInfo;

            Vector3 committedPoint = hitPoint;

            Vector3 committedNormal = hitNormal;

            onArrive = () =>
            {
                SpawnImpactVFX(committedPoint, committedNormal, auraColor);

                IHitReaction reaction = CombatHitResolver.Resolve(committedCollider, committedHit);

                reaction?.ReceiveHit(committedHit);
            };
        }

        SpawnShotVFX(muzzle.position, endPoint, auraColor, onArrive);

        if (CurrentAmmo <= 0)
        {
            BeginReload();
        }
    }

    private HitInfo CreateHitInfo(Vector3 point, Vector3 normal, Vector3 direction)
    {
        return new HitInfo(
            equippedWeapon != null ? equippedWeapon.damage : 0f,
            point,
            normal,
            direction,
            gameObject
        );
    }

    // =====================================================
    // MUZZLE OBSTRUCTION
    // =====================================================

    private bool TryGetMuzzleObstruction(out RaycastHit closestHit)
    {
        closestHit = default;

        if (muzzle == null)
            return false;

        Vector3 bodyOrigin = GetShotSafetyOrigin();

        Vector3 toMuzzle = muzzle.position - bodyOrigin;

        float distance = toMuzzle.magnitude;

        if (distance <= 0.001f)
            return false;

        Vector3 direction = toMuzzle / distance;

        int hitCount = Physics.RaycastNonAlloc(
            bodyOrigin,
            direction,
            muzzleSafetyHits,
            distance,
            shootMask,
            QueryTriggerInteraction.Ignore
        );

        return TryFindClosestProjectileBlockingHit(muzzleSafetyHits, hitCount, out closestHit);
    }

    private bool TryGetFirstWeaponHit(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit closestHit
    )
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            weaponHits,
            distance,
            shootMask,
            QueryTriggerInteraction.Ignore
        );

        return TryFindClosestProjectileBlockingHit(weaponHits, hitCount, out closestHit);
    }

    private bool TryFindClosestProjectileBlockingHit(
        RaycastHit[] hits,
        int hitCount,
        out RaycastHit closestHit
    )
    {
        closestHit = default;

        bool foundHit = false;

        float closestDistance = Mathf.Infinity;

        // RaycastNonAlloc results are not sorted.
        // Ignore player-owned geometry and explicitly projectile-transparent
        // surfaces, then choose the nearest real blocker.
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider == null)
                continue;

            if (IsPlayerOwnedCollider(hit.collider))
            {
                continue;
            }

            if (IsProjectilePassThrough(hit.collider))
            {
                continue;
            }

            if (hit.distance >= closestDistance)
            {
                continue;
            }

            closestDistance = hit.distance;

            closestHit = hit;

            foundHit = true;
        }

        return foundHit;
    }

    private static bool IsProjectilePassThrough(Collider collider)
    {
        return collider != null
            && collider.GetComponentInParent<ProjectilePassThroughObstacle>() != null;
    }

    private Vector3 GetShotSafetyOrigin()
    {
        if (characterController != null)
        {
            return characterController.bounds.center;
        }

        return transform.position + Vector3.up;
    }

    private bool IsPlayerOwnedCollider(Collider collider)
    {
        if (collider == null)
            return false;

        Transform hitTransform = collider.transform;

        return hitTransform == transform || hitTransform.IsChildOf(transform);
    }

    // =====================================================
    // SHOT VFX
    // =====================================================

    private void SpawnShotVFX(Vector3 start, Vector3 end, Color? auraColor, System.Action onArrive)
    {
        float speed = plasmaBoltPrefab != null ? plasmaBoltPrefab.TravelSpeed
            : missingBoltSpeed > 0 && float.IsFinite(missingBoltSpeed) ? missingBoltSpeed : PlasmaBoltVFX.DefaultSpeed;
        float distance = Vector3.Distance(start, end);
        float arrival = Time.time + (distance <= .001f ? 0 : distance / speed);
        if (onArrive != null && arrival <= Time.time) onArrive();
        else if (onArrive != null)
        {
            pendingImpacts.Add(new PendingImpact { arrival = arrival, commit = onArrive });
            if (!processingImpacts)
            {
                processingImpacts = true;
                StartCoroutine(CommitPendingImpacts());
            }
        }
        if (plasmaBoltPrefab == null) return;
        var bolt = VfxPool.Spawn(plasmaBoltPrefab, start, Quaternion.identity);
        if (bolt != null) bolt.Initialize(start, end, auraColor, arrivalTime: arrival);
    }

    private void SpawnMuzzleVFX(Vector3 position, Vector3 direction, Color? auraColor)
    {
        if (plasmaMuzzlePrefab == null)
            return;

        PlasmaMuzzleVFX muzzleVfx = VfxPool.Spawn(
            plasmaMuzzlePrefab,
            position,
            Quaternion.LookRotation(direction)
        );

        if (muzzleVfx != null)
        {
            muzzleVfx.Play(auraColor);
        }
    }

    private void SpawnImpactVFX(Vector3 position, Vector3 normal, Color? auraColor)
    {
        if (plasmaImpactPrefab == null)
            return;

        PlasmaImpactVFX impact = VfxPool.Spawn(
            plasmaImpactPrefab,
            position + normal * 0.01f,
            Quaternion.LookRotation(normal)
        );

        if (impact != null)
        {
            impact.Play(auraColor);
        }
    }

    // =====================================================
    // AURA
    // =====================================================

    private Color? GetAuraColor()
    {
        if (playerCharacter != null && playerCharacter.ActiveVisual != null)
        {
            return playerCharacter.ActiveVisual.AuraColor;
        }

        return null;
    }

    // =====================================================
    // RELOAD
    // =====================================================

    private void BeginReload()
    {
        weaponState?.BeginReload(Time.time);
        ActiveRunController.Instance?.MarkDirty();
    }

    // =====================================================
    // EQUIPMENT
    // =====================================================

    public void SetTriggerHeld(bool held)
    {
        triggerHeld = held;
        if (!held)
            reportedMissingSkill = null;
    }

    public void SetFireBlocked(bool blocked)
    {
        fireBlocked = blocked;

        if (!blocked)
            return;

        triggerHeld = false;
        shootWasPressed = false;
    }

    public void EquipWeapon(WeaponItemData weapon, Transform weaponMuzzle)
    {
        var inventory = GetComponent<PlayerInventory>();
        var state = inventory != null ? inventory.GetWeaponState(weapon) : null;
        if (state == null) throw new System.InvalidOperationException("Equip requires an inventory-owned weapon.");
        reportedMissingSkill = null;
        equippedWeapon = weapon;
        muzzle = weaponMuzzle;
        weaponState = state;
        weaponState.FinishReload(Time.time);
    }

    public void UnequipWeapon()
    {
        equippedWeapon = null;
        muzzle = null;
        weaponState = null;
        triggerHeld = false;
        shootWasPressed = false;
    }
}
