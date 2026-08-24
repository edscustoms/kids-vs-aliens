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

        nextFireTime = Time.time + 1f / equippedWeapon.fireRate;

        Vector3 direction = (playerAim.AimPoint - muzzle.position).normalized;

        Vector3 endPoint = muzzle.position + direction * equippedWeapon.range;

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
            endPoint = hit.point;

            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();

            if (enemy != null)
                enemy.TakeDamage(equippedWeapon.damage);
        }

        SpawnShotVFX(muzzle.position, endPoint);

        if (currentAmmo <= 0)
            StartCoroutine(Reload());
    }

    private void SpawnShotVFX(Vector3 start, Vector3 end)
    {
        if (plasmaBoltPrefab == null)
            return;

        PlasmaBoltVFX bolt = Instantiate(plasmaBoltPrefab);

        Color? auraColor = null;

        if (playerCharacter != null && playerCharacter.ActiveVisual != null)
        {
            auraColor = playerCharacter.ActiveVisual.AuraColor;
        }

        bolt.Initialize(start, end, auraColor);
    }

    private IEnumerator Reload()
    {
        if (isReloading)
            yield break;

        isReloading = true;

        Debug.Log($"Reloading {equippedWeapon.itemName}...");

        yield return new WaitForSeconds(equippedWeapon.reloadTime);

        currentAmmo = equippedWeapon.magazineSize;

        isReloading = false;

        Debug.Log(
            $"Reloaded {equippedWeapon.itemName}: " + $"{currentAmmo}/{equippedWeapon.magazineSize}"
        );
    }

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
