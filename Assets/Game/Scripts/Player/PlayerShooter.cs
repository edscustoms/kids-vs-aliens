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

    // Used for the short 3D safety ray between Amy's
    // body and the weapon muzzle.
    //
    // This catches the case where the gun itself has
    // clipped through a wall.
    private readonly RaycastHit[] muzzleSafetyHits = new RaycastHit[16];

    // =====================================================
    // INITIALIZATION
    // =====================================================

    private void Awake()
    {
        shootMask = ~LayerMask.GetMask("Player");

        input = GetComponent<StarterAssetsInputs>();

        characterController = GetComponent<CharacterController>();

        if (playerAim == null)
        {
            playerAim = GetComponent<PlayerAim>();
        }

        if (playerCharacter == null)
        {
            playerCharacter = GetComponent<PlayerCharacter>();
        }
    }

    // =====================================================
    // UPDATE
    // =====================================================

    private void Update()
    {
        if (equippedWeapon == null || muzzle == null)
        {
            return;
        }

        if (isReloading)
            return;

        // -------------------------------------------------
        // INPUT
        //
        // StarterAssetsInputs is now the single source
        // used by both:
        //
        // Desktop:
        //      Mouse -> PlayerInput -> StarterAssetsInputs
        //
        // Mobile:
        //      UI button -> UICanvasControllerInput
        //                -> StarterAssetsInputs
        //
        // -------------------------------------------------

        bool shootPressed = input != null && input.shoot;

        bool shootPressedThisFrame = shootPressed && !shootWasPressed;

        shootWasPressed = shootPressed;

        bool wantsToShoot;

        if (equippedWeapon.fireMode == WeaponFireMode.Automatic)
        {
            wantsToShoot = shootPressed;
        }
        else
        {
            // Semi-auto:
            // one physical press = one shot.
            wantsToShoot = shootPressedThisFrame;
        }

        if (!wantsToShoot)
            return;

        if (Time.time < nextFireTime)
            return;

        if (currentAmmo <= 0)
        {
            StartCoroutine(Reload());

            return;
        }

        Shoot();
    }

    // =====================================================
    // SHOOTING
    // =====================================================

    private void Shoot()
    {
        if (playerAim == null)
            return;

        if (!playerAim.TryGetShotAimPoint(muzzle.position, out Vector3 shotAimPoint))
        {
            return;
        }

        currentAmmo--;

        nextFireTime = Time.time + 1f / equippedWeapon.fireRate;

        Vector3 direction = (shotAimPoint - muzzle.position).normalized;

        Vector3 endPoint = muzzle.position + direction * equippedWeapon.range;

        Color? auraColor = GetAuraColor();

        bool didHit = false;

        Vector3 hitPoint = Vector3.zero;

        Vector3 hitNormal = Vector3.zero;

        BreakableTargetPiece targetPiece = null;

        // =================================================
        // MUZZLE WALL SAFETY
        //
        // Problem:
        //
        // When Amy stands extremely close to a wall,
        // her weapon can visually penetrate the wall.
        //
        // A normal weapon ray begins at the muzzle.
        // If the muzzle is already on the other side of
        // the wall, that ray will never see the wall and
        // Amy can incorrectly shoot through it.
        //
        // Solution:
        //
        // Before firing the normal weapon ray, perform a
        // short REAL 3D ray from Amy's body center to the
        // muzzle.
        //
        // This uses the complete X/Y/Z positions.
        //
        // Therefore:
        //
        // - Tall wall crossing chest -> muzzle:
        //      BLOCKS shot.
        //
        // - 30 cm wall below chest/muzzle:
        //      does NOT block shot.
        //
        // - Railings / openings / low cover:
        //      behave according to their actual collider
        //      geometry.
        //
        // This is NOT a height approximation.
        // Physics determines whether the actual 3D line
        // intersects actual geometry.
        // =================================================

        if (TryGetMuzzleObstruction(out RaycastHit muzzleObstruction))
        {
            didHit = true;

            endPoint = muzzleObstruction.point;

            hitPoint = muzzleObstruction.point;

            hitNormal = muzzleObstruction.normal;

            targetPiece = muzzleObstruction.collider.GetComponentInParent<BreakableTargetPiece>();

            EnemyHealth enemy = muzzleObstruction.collider.GetComponentInParent<EnemyHealth>();

            if (enemy != null)
            {
                enemy.TakeDamage(equippedWeapon.damage);
            }
        }
        // =================================================
        // NORMAL WEAPON RAY
        //
        // Only run this when there was nothing physically
        // between Amy's body and her muzzle.
        // =================================================

        else if (
            Physics.Raycast(
                muzzle.position,
                direction,
                out RaycastHit hit,
                equippedWeapon.range,
                shootMask,
                QueryTriggerInteraction.Ignore
            )
        )
        {
            didHit = true;

            endPoint = hit.point;

            hitPoint = hit.point;

            hitNormal = hit.normal;

            // Check if we hit a breakable practice target
            // piece.
            targetPiece = hit.collider.GetComponentInParent<BreakableTargetPiece>();

            // Gameplay enemy damage stays INSTANT.
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();

            if (enemy != null)
            {
                enemy.TakeDamage(equippedWeapon.damage);
            }
        }

        // =================================================
        // VFX
        // =================================================

        SpawnMuzzleVFX(muzzle.position, direction, auraColor);

        System.Action onArrive = null;

        if (didHit)
        {
            onArrive = () =>
            {
                SpawnImpactVFX(hitPoint, hitNormal, auraColor);

                if (targetPiece != null && targetPiece.Target != null)
                {
                    targetPiece.Target.BreakPiece(targetPiece, hitPoint, direction);
                }
            };
        }

        SpawnShotVFX(muzzle.position, endPoint, auraColor, onArrive);

        if (currentAmmo <= 0)
        {
            StartCoroutine(Reload());
        }
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

        bool foundHit = false;

        float closestDistance = Mathf.Infinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = muzzleSafetyHits[i];

            if (hit.collider == null)
                continue;

            // The Player layer is already excluded by
            // shootMask, but this also protects us if a
            // child object / weapon collider accidentally
            // remains on another layer.
            if (IsPlayerOwnedCollider(hit.collider))
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

    // =====================================================
    // SHOT SAFETY ORIGIN
    //
    // CharacterController.bounds.center gives us a stable
    // point inside Amy's body around torso height.
    //
    // We deliberately do NOT flatten Y or Z.
    //
    // bodyOrigin -> muzzle is one actual 3D segment.
    // =====================================================

    private Vector3 GetShotSafetyOrigin()
    {
        if (characterController != null)
        {
            return characterController.bounds.center;
        }

        // Defensive fallback for characters without
        // CharacterController.
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
        if (plasmaBoltPrefab == null)
            return;

        PlasmaBoltVFX bolt = Instantiate(plasmaBoltPrefab);

        bolt.Initialize(start, end, auraColor, onArrive);
    }

    private void SpawnMuzzleVFX(Vector3 position, Vector3 direction, Color? auraColor)
    {
        if (plasmaMuzzlePrefab == null)
            return;

        PlasmaMuzzleVFX muzzleVfx = Instantiate(
            plasmaMuzzlePrefab,
            position,
            Quaternion.LookRotation(direction)
        );

        muzzleVfx.Play(auraColor);
    }

    private void SpawnImpactVFX(Vector3 position, Vector3 normal, Color? auraColor)
    {
        if (plasmaImpactPrefab == null)
            return;

        PlasmaImpactVFX impact = Instantiate(
            plasmaImpactPrefab,
            position + normal * 0.01f,
            Quaternion.LookRotation(normal)
        );

        impact.Play(auraColor);
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

    private IEnumerator Reload()
    {
        if (isReloading)
            yield break;

        isReloading = true;

        yield return new WaitForSeconds(equippedWeapon.reloadTime);

        currentAmmo = equippedWeapon.magazineSize;

        isReloading = false;
    }

    // =====================================================
    // EQUIPMENT
    // =====================================================

    public void EquipWeapon(WeaponItemData weapon, Transform weaponMuzzle)
    {
        StopAllCoroutines();

        equippedWeapon = weapon;

        muzzle = weaponMuzzle;

        currentAmmo = weapon.magazineSize;

        isReloading = false;

        nextFireTime = 0f;
    }

    public void UnequipWeapon()
    {
        StopAllCoroutines();

        equippedWeapon = null;

        muzzle = null;

        currentAmmo = 0;

        isReloading = false;
    }
}
