using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

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

    private float nextFireTime;
    private int currentAmmo;
    private bool isReloading;

    private int shootMask;

    private void Awake()
    {
        shootMask = ~LayerMask.GetMask("Player");

        if (playerAim == null)
            playerAim = GetComponent<PlayerAim>();

        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();
    }

    private void Update()
    {
        if (equippedWeapon == null || muzzle == null)
            return;

        if (isReloading)
            return;

        if (Mouse.current == null)
            return;

        bool wantsToShoot;

        if (equippedWeapon.fireMode == WeaponFireMode.Automatic)
            wantsToShoot = Mouse.current.leftButton.isPressed;
        else
            wantsToShoot = Mouse.current.leftButton.wasPressedThisFrame;

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

    private void Shoot()
    {
        if (playerAim == null || !playerAim.HasAimPoint)
            return;

        currentAmmo--;

        nextFireTime =
            Time.time + 1f / equippedWeapon.fireRate;

        Vector3 direction =
            (playerAim.AimPoint - muzzle.position).normalized;

        Vector3 endPoint =
            muzzle.position +
            direction * equippedWeapon.range;

        Color? auraColor = GetAuraColor();

        bool didHit = false;

        Vector3 hitPoint = Vector3.zero;
        Vector3 hitNormal = Vector3.zero;

        BreakableTargetPiece targetPiece = null;

        if (
            Physics.Raycast(
                muzzle.position,
                direction,
                out RaycastHit hit,
                equippedWeapon.range,
                shootMask
            )
        )
        {
            didHit = true;

            endPoint = hit.point;
            hitPoint = hit.point;
            hitNormal = hit.normal;

            // Check if we hit a breakable practice target piece.
            targetPiece =
                hit.collider
                    .GetComponentInParent<BreakableTargetPiece>();

            // Gameplay enemy damage stays INSTANT.
            EnemyHealth enemy =
                hit.collider.GetComponentInParent<EnemyHealth>();

            if (enemy != null)
            {
                enemy.TakeDamage(equippedWeapon.damage);
            }
        }

        SpawnMuzzleVFX(
            muzzle.position,
            direction,
            auraColor
        );

        System.Action onArrive = null;

        if (didHit)
        {
            onArrive = () =>
            {
                SpawnImpactVFX(
                    hitPoint,
                    hitNormal,
                    auraColor
                );

                if (
                    targetPiece != null &&
                    targetPiece.Target != null
                )
                {
                    targetPiece.Target.BreakPiece(
                        targetPiece,
                        hitPoint,
                        direction
                    );
                }
            };
        }

        SpawnShotVFX(
            muzzle.position,
            endPoint,
            auraColor,
            onArrive
        );

        if (currentAmmo <= 0)
        {
            StartCoroutine(Reload());
        }
    }

    private void SpawnShotVFX(
        Vector3 start,
        Vector3 end,
        Color? auraColor,
        System.Action onArrive
    )
    {
        if (plasmaBoltPrefab == null)
            return;

        PlasmaBoltVFX bolt =
            Instantiate(plasmaBoltPrefab);

        bolt.Initialize(
            start,
            end,
            auraColor,
            onArrive
        );
    }

    private void SpawnMuzzleVFX(
        Vector3 position,
        Vector3 direction,
        Color? auraColor
    )
    {
        if (plasmaMuzzlePrefab == null)
            return;

        PlasmaMuzzleVFX muzzleVfx =
            Instantiate(
                plasmaMuzzlePrefab,
                position,
                Quaternion.LookRotation(direction)
            );

        muzzleVfx.Play(auraColor);
    }

    private void SpawnImpactVFX(
        Vector3 position,
        Vector3 normal,
        Color? auraColor
    )
    {
        if (plasmaImpactPrefab == null)
            return;

        PlasmaImpactVFX impact =
            Instantiate(
                plasmaImpactPrefab,
                position + normal * 0.01f,
                Quaternion.LookRotation(normal)
            );

        impact.Play(auraColor);
    }

    private Color? GetAuraColor()
    {
        if (
            playerCharacter != null &&
            playerCharacter.ActiveVisual != null
        )
        {
            return playerCharacter.ActiveVisual.AuraColor;
        }

        return null;
    }

    private IEnumerator Reload()
    {
        if (isReloading)
            yield break;

        isReloading = true;

        Debug.Log(
            $"Reloading {equippedWeapon.itemName}..."
        );

        yield return new WaitForSeconds(
            equippedWeapon.reloadTime
        );

        currentAmmo = equippedWeapon.magazineSize;

        isReloading = false;

        Debug.Log(
            $"Reloaded {equippedWeapon.itemName}: " +
            $"{currentAmmo}/{equippedWeapon.magazineSize}"
        );
    }

    public void EquipWeapon(
        WeaponItemData weapon,
        Transform weaponMuzzle
    )
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