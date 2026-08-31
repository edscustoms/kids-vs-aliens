using System.Collections;
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

    [SerializeField]
    private PlasmaMuzzleVFX plasmaMuzzlePrefab;

    [SerializeField]
    private PlasmaImpactVFX plasmaImpactPrefab;

    // =====================================================
    // CACHED
    // =====================================================

    private StarterAssetsInputs input;
    private CharacterController characterController;

    private float nextFireTime;
    private int currentAmmo;
    private bool isReloading;
    private bool shootWasPressed;
    private int shootMask;

    private readonly RaycastHit[] muzzleSafetyHits =
        new RaycastHit[16];

    // Normal weapon rays need to collect multiple hits because projectile-
    // transparent surfaces (for example chain-link fence barriers) may be
    // physically in front of the real target.
    private readonly RaycastHit[] weaponHits =
        new RaycastHit[32];

    // =====================================================
    // INITIALIZATION
    // =====================================================

    private void Awake()
    {
        shootMask =
            ~LayerMask.GetMask("Player");

        input =
            GetComponent<StarterAssetsInputs>();

        characterController =
            GetComponent<CharacterController>();

        if (playerAim == null)
        {
            playerAim =
                GetComponent<PlayerAim>();
        }

        if (playerCharacter == null)
        {
            playerCharacter =
                GetComponent<PlayerCharacter>();
        }

        if (playerSkillState == null)
        {
            playerSkillState =
                GetComponent<PlayerSkillState>();
        }
    }

    // =====================================================
    // UPDATE
    // =====================================================

    private void Update()
    {
        if (equippedWeapon == null ||
            muzzle == null)
        {
            return;
        }

        if (isReloading)
            return;

        bool shootPressed =
            input != null &&
            input.shoot;

        bool shootPressedThisFrame =
            shootPressed &&
            !shootWasPressed;

        shootWasPressed =
            shootPressed;

        bool wantsToShoot;

        if (equippedWeapon.fireMode ==
            WeaponFireMode.Automatic)
        {
            wantsToShoot =
                shootPressed;
        }
        else
        {
            wantsToShoot =
                shootPressedThisFrame;
        }

        if (!wantsToShoot)
            return;

        if (!CanUseEquippedWeapon())
            return;

        if (Time.time < nextFireTime)
            return;

        if (currentAmmo <= 0)
        {
            StartCoroutine(
                Reload());

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

        SkillData requiredSkill =
            equippedWeapon.requiredSkill;

        if (requiredSkill == null)
            return true;

        return
            playerSkillState != null &&
            playerSkillState.HasSkill(
                requiredSkill);
    }

    private void Shoot()
    {
        if (playerAim == null)
            return;

        if (!playerAim.TryGetShotAimPoint(
                muzzle.position,
                out Vector3 shotAimPoint))
        {
            return;
        }

        currentAmmo--;

        nextFireTime =
            Time.time +
            1f /
            equippedWeapon.fireRate;

        Vector3 direction =
            (shotAimPoint -
             muzzle.position).normalized;

        Vector3 endPoint =
            muzzle.position +
            direction *
            equippedWeapon.range;

        Color? auraColor =
            GetAuraColor();

        bool didHit = false;

        Vector3 hitPoint =
            Vector3.zero;

        Vector3 hitNormal =
            Vector3.zero;

        Collider hitCollider =
            null;

        HitInfo hitInfo =
            default;

        // =================================================
        // MUZZLE WALL SAFETY
        // =================================================

        if (TryGetMuzzleObstruction(
                out RaycastHit muzzleObstruction))
        {
            didHit = true;

            endPoint =
                muzzleObstruction.point;

            hitPoint =
                muzzleObstruction.point;

            hitNormal =
                muzzleObstruction.normal;

            hitCollider =
                muzzleObstruction.collider;

            hitInfo =
                CreateHitInfo(
                    hitPoint,
                    hitNormal,
                    direction);
        }

        // =================================================
        // NORMAL WEAPON RAY
        // =================================================

        else if (
            TryGetFirstWeaponHit(
                muzzle.position,
                direction,
                equippedWeapon.range,
                out RaycastHit hit))
        {
            didHit = true;

            endPoint =
                hit.point;

            hitPoint =
                hit.point;

            hitNormal =
                hit.normal;

            hitCollider =
                hit.collider;

            hitInfo =
                CreateHitInfo(
                    hitPoint,
                    hitNormal,
                    direction);
        }

        // =================================================
        // VFX
        // =================================================

        SpawnMuzzleVFX(
            muzzle.position,
            direction,
            auraColor);

        System.Action onArrive =
            null;

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
            Collider committedCollider =
                hitCollider;

            HitInfo committedHit =
                hitInfo;

            Vector3 committedPoint =
                hitPoint;

            Vector3 committedNormal =
                hitNormal;

            onArrive = () =>
            {
                SpawnImpactVFX(
                    committedPoint,
                    committedNormal,
                    auraColor);

                IHitReaction reaction =
                    CombatHitResolver.Resolve(
                        committedCollider,
                        committedHit);

                reaction?.ReceiveHit(
                    committedHit);
            };
        }

        SpawnShotVFX(
            muzzle.position,
            endPoint,
            auraColor,
            onArrive);

        if (currentAmmo <= 0)
        {
            StartCoroutine(
                Reload());
        }
    }

    private HitInfo CreateHitInfo(
        Vector3 point,
        Vector3 normal,
        Vector3 direction)
    {
        return new HitInfo(
            equippedWeapon != null
                ? equippedWeapon.damage
                : 0f,
            point,
            normal,
            direction,
            gameObject);
    }

    // =====================================================
    // MUZZLE OBSTRUCTION
    // =====================================================

    private bool TryGetMuzzleObstruction(
        out RaycastHit closestHit)
    {
        closestHit =
            default;

        if (muzzle == null)
            return false;

        Vector3 bodyOrigin =
            GetShotSafetyOrigin();

        Vector3 toMuzzle =
            muzzle.position -
            bodyOrigin;

        float distance =
            toMuzzle.magnitude;

        if (distance <= 0.001f)
            return false;

        Vector3 direction =
            toMuzzle /
            distance;

        int hitCount =
            Physics.RaycastNonAlloc(
                bodyOrigin,
                direction,
                muzzleSafetyHits,
                distance,
                shootMask,
                QueryTriggerInteraction.Ignore);

        return TryFindClosestProjectileBlockingHit(
            muzzleSafetyHits,
            hitCount,
            out closestHit);
    }

    private bool TryGetFirstWeaponHit(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit closestHit)
    {
        int hitCount =
            Physics.RaycastNonAlloc(
                origin,
                direction,
                weaponHits,
                distance,
                shootMask,
                QueryTriggerInteraction.Ignore);

        return TryFindClosestProjectileBlockingHit(
            weaponHits,
            hitCount,
            out closestHit);
    }

    private bool TryFindClosestProjectileBlockingHit(
        RaycastHit[] hits,
        int hitCount,
        out RaycastHit closestHit)
    {
        closestHit =
            default;

        bool foundHit =
            false;

        float closestDistance =
            Mathf.Infinity;

        // RaycastNonAlloc results are not sorted.
        // Ignore player-owned geometry and explicitly projectile-transparent
        // surfaces, then choose the nearest real blocker.
        for (int i = 0;
             i < hitCount;
             i++)
        {
            RaycastHit hit =
                hits[i];

            if (hit.collider == null)
                continue;

            if (IsPlayerOwnedCollider(
                    hit.collider))
            {
                continue;
            }

            if (IsProjectilePassThrough(
                    hit.collider))
            {
                continue;
            }

            if (hit.distance >=
                closestDistance)
            {
                continue;
            }

            closestDistance =
                hit.distance;

            closestHit =
                hit;

            foundHit =
                true;
        }

        return foundHit;
    }

    private static bool IsProjectilePassThrough(
        Collider collider)
    {
        return
            collider != null &&
            collider.GetComponentInParent<
                ProjectilePassThroughObstacle>() != null;
    }

    private Vector3 GetShotSafetyOrigin()
    {
        if (characterController != null)
        {
            return
                characterController
                    .bounds
                    .center;
        }

        return
            transform.position +
            Vector3.up;
    }

    private bool IsPlayerOwnedCollider(
        Collider collider)
    {
        if (collider == null)
            return false;

        Transform hitTransform =
            collider.transform;

        return
            hitTransform == transform ||
            hitTransform.IsChildOf(
                transform);
    }

    // =====================================================
    // SHOT VFX
    // =====================================================

    private void SpawnShotVFX(
        Vector3 start,
        Vector3 end,
        Color? auraColor,
        System.Action onArrive)
    {
        if (plasmaBoltPrefab == null)
        {
            // No VFX should never block gameplay.
            onArrive?.Invoke();
            return;
        }

        PlasmaBoltVFX bolt =
            VfxPool.Spawn(
                plasmaBoltPrefab,
                start,
                Quaternion.identity);

        if (bolt == null)
        {
            onArrive?.Invoke();
            return;
        }

        bolt.Initialize(
            start,
            end,
            auraColor,
            onArrive);
    }

    private void SpawnMuzzleVFX(
        Vector3 position,
        Vector3 direction,
        Color? auraColor)
    {
        if (plasmaMuzzlePrefab == null)
            return;

        PlasmaMuzzleVFX muzzleVfx =
            VfxPool.Spawn(
                plasmaMuzzlePrefab,
                position,
                Quaternion.LookRotation(
                    direction));

        if (muzzleVfx != null)
        {
            muzzleVfx.Play(
                auraColor);
        }
    }

    private void SpawnImpactVFX(
        Vector3 position,
        Vector3 normal,
        Color? auraColor)
    {
        if (plasmaImpactPrefab == null)
            return;

        PlasmaImpactVFX impact =
            VfxPool.Spawn(
                plasmaImpactPrefab,
                position +
                normal *
                0.01f,
                Quaternion.LookRotation(
                    normal));

        if (impact != null)
        {
            impact.Play(
                auraColor);
        }
    }

    // =====================================================
    // AURA
    // =====================================================

    private Color? GetAuraColor()
    {
        if (playerCharacter != null &&
            playerCharacter.ActiveVisual != null)
        {
            return
                playerCharacter
                    .ActiveVisual
                    .AuraColor;
        }

        return null;
    }

    // =====================================================
    // RELOAD
    // =====================================================

    private IEnumerator Reload()
    {
        if (isReloading)
            yield break;

        isReloading =
            true;

        yield return
            new WaitForSeconds(
                equippedWeapon.reloadTime);

        currentAmmo =
            equippedWeapon.magazineSize;

        isReloading =
            false;
    }

    // =====================================================
    // EQUIPMENT
    // =====================================================

    public void EquipWeapon(
        WeaponItemData weapon,
        Transform weaponMuzzle)
    {
        StopAllCoroutines();

        equippedWeapon =
            weapon;

        muzzle =
            weaponMuzzle;

        currentAmmo =
            weapon.magazineSize;

        isReloading =
            false;

        nextFireTime =
            0f;
    }

    public void UnequipWeapon()
    {
        StopAllCoroutines();

        equippedWeapon =
            null;

        muzzle =
            null;

        currentAmmo =
            0;

        isReloading =
            false;
    }
}
