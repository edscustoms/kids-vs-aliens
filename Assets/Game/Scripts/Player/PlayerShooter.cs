using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShooter : MonoBehaviour
{
    [SerializeField] private WeaponItemData equippedWeapon;
    [SerializeField] private Transform muzzle;

    [Header("Bullet Visual")]
    [SerializeField] private float tracerDuration = 0.05f;
    [SerializeField] private float tracerWidth = 0.02f;

    private float nextFireTime;
    private int currentAmmo;
    private bool isReloading;

    private int shootMask;

    private void Awake()
    {
        shootMask = ~LayerMask.GetMask("Player");
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
        currentAmmo--;

        nextFireTime = Time.time + 1f / equippedWeapon.fireRate;

        Vector3 direction = transform.forward;
        Vector3 endPoint = muzzle.position + direction * equippedWeapon.range;

        if (Physics.Raycast(
            muzzle.position,
            direction,
            out RaycastHit hit,
            equippedWeapon.range,
            shootMask))
        {
            endPoint = hit.point;

            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();

            if (enemy != null)
            {
                enemy.TakeDamage(equippedWeapon.damage);
            }

            Debug.Log(
                $"{equippedWeapon.itemName} hit {hit.collider.name} | Ammo: {currentAmmo}/{equippedWeapon.magazineSize}"
            );
        }
        else
        {
            Debug.Log(
                $"{equippedWeapon.itemName} fired | Ammo: {currentAmmo}/{equippedWeapon.magazineSize}"
            );
        }

        StartCoroutine(ShowTracer(muzzle.position, endPoint));

        // Automatic reload after the final shot
        if (currentAmmo <= 0)
            StartCoroutine(Reload());
    }

    private IEnumerator ShowTracer(Vector3 start, Vector3 end)
    {
        GameObject tracerObject = new GameObject("BulletTracer");

        LineRenderer line = tracerObject.AddComponent<LineRenderer>();

        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);

        line.startWidth = tracerWidth;
        line.endWidth = tracerWidth;

        line.material = new Material(Shader.Find("Sprites/Default"));

        yield return new WaitForSeconds(tracerDuration);

        Destroy(tracerObject);
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
            $"Reloaded {equippedWeapon.itemName}: {currentAmmo}/{equippedWeapon.magazineSize}"
        );
    }

    public void EquipWeapon(
        WeaponItemData weapon,
        Transform weaponMuzzle)
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