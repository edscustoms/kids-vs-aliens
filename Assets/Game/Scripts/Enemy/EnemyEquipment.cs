using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyEquipment : MonoBehaviour, IRunStateParticipant
{
    [SerializeField] private EnemyCombatProfile profile;
    [SerializeField] private WeaponItemData startingWeapon;
    [SerializeField] private bool dropWeaponOnDeath;
    private EnemyCombatPresentation presentation;
    private EnemyHealth health;
    private WeaponInstance instance;
    private PickupItem acquiredPickup;
    private bool restored, initialized;
    public EnemyCombatProfile Profile => profile;
    public WeaponItemData Weapon { get; private set; }
    public Transform Muzzle => instance != null ? instance.Muzzle : null;
    public int Ammo { get; private set; }
    public bool HasWeapon => Weapon != null && instance != null && Muzzle != null;
    public event Action Changed;
    private void Cache() { if (presentation == null) presentation = GetComponent<EnemyCombatPresentation>(); if (health == null) health = GetComponent<EnemyHealth>(); }
    private void Awake() { Cache(); }
    private void OnEnable() { if (health != null) health.OnDied += OnDeath; }
    private void OnDisable() { if (health != null) health.OnDied -= OnDeath; }
    private void Start()
    {
        if (!restored && !initialized && startingWeapon != null && !TryEquip(startingWeapon))
            Debug.LogError(name + ": Starting Weapon is incompatible or its visual/socket setup is incomplete.", this);
        initialized = true;
    }
    public bool CanUse(WeaponItemData weapon) => profile != null && profile.Allows(weapon);
    public bool TryEquip(WeaponItemData weapon)
    {
        Cache();
        if (!CanUse(weapon) || presentation == null || presentation.Visual == null) return false;
        var next = WeaponInstance.SpawnAttached(weapon, presentation.Visual);
        if (next == null || next.Muzzle == null)
        {
            if (next != null) Destroy(next.gameObject);
            Debug.LogError(name + ": enemy weapon requires WeaponInstance, GripPoint and Muzzle.", this);
            return false;
        }
        initialized = true;
        ReturnAcquiredPickup();
        ReleaseVisual(); instance = next; Weapon = weapon; Ammo = Mathf.Max(1, weapon.magazineSize);
        SetRangedPresentation(true); Changed?.Invoke(); Dirty(); return true;
    }
    public bool TryAcquire(PickupItem pickup, MonoBehaviour reservationOwner)
    {
        if (HasWeapon || (health != null && health.IsDead) || pickup == null || !pickup.IsReservedBy(reservationOwner)
            || !(pickup.Item is WeaponItemData weapon) || !TryEquip(weapon)) return false;
        if (!pickup.RetainReserved(reservationOwner)) { Unequip(); return false; }
        acquiredPickup = pickup;
        Dirty(); return true;
    }
    public void SetRangedPresentation(bool ranged)
    {
        bool show = ranged && HasWeapon;
        if (instance != null && instance.gameObject.activeSelf != show) instance.gameObject.SetActive(show);
        if (presentation != null) presentation.SetWeaponStyle(show ? Weapon.animationStyle : WeaponAnimationStyle.Unarmed);
    }
    public bool SpendRound()
    {
        if (!HasWeapon || Ammo <= 0) return false;
        Ammo--; Dirty(); return true;
    }
    public void Reload() { if (HasWeapon) { Ammo = Mathf.Max(1, Weapon.magazineSize); Dirty(); } }
    public void Unequip()
    {
        Cache(); initialized = true;
        ReturnAcquiredPickup();
        ReleaseVisual(); Weapon = null; Ammo = 0; SetRangedPresentation(false); Changed?.Invoke(); Dirty();
    }
    public GameObject Drop()
    {
        if (acquiredPickup != null)
        {
            var original = acquiredPickup.gameObject;
            Unequip(); return original;
        }
        if (!HasWeapon || Weapon.worldPrefab == null) { Unequip(); return null; }
        var prefab = Weapon.worldPrefab;
        var dropped = Instantiate(prefab, transform.position + transform.forward * .5f + Vector3.up * .15f, Quaternion.identity);
        RunWorldObject.TrackSpawn(dropped, prefab);
        Unequip(); return dropped;
    }
    private void OnDeath()
    {
        GetComponent<EnemyPlasmaLoot>()?.ResolveDeath(Weapon);
        if (acquiredPickup != null || dropWeaponOnDeath) Drop();
    }
    private void ReturnAcquiredPickup()
    {
        if (acquiredPickup == null) return;
        var pickup = acquiredPickup; acquiredPickup = null;
        pickup.ReturnToWorld(transform.position + transform.forward * .5f + Vector3.up * .15f);
    }
    private void ReleaseVisual() { if (instance != null) { instance.gameObject.SetActive(false); Destroy(instance.gameObject); } instance = null; }
    private static void Dirty() { if (ActiveRunController.Instance != null) ActiveRunController.Instance.MarkDirty(); }
    public string RunStateKey => "enemy-equipment-v1";
    [Serializable] private sealed class Saved { public string weapon, pickupId; public int ammo; }
    public string CaptureRunState()
    {
        // Never-activated encounter children must retain their authored loadout without spawning a visual during save.
        var weapon = !initialized && !restored ? startingWeapon : HasWeapon ? Weapon : null;
        return JsonUtility.ToJson(new Saved { weapon = RunContentCatalog.Instance.Id(weapon), pickupId = acquiredPickup != null ? acquiredPickup.GetComponent<RunWorldObject>().Id : null,
            ammo = !initialized && !restored && weapon != null ? weapon.magazineSize : Ammo });
    }
    public void RestoreRunState(string json)
    {
        var saved = JsonUtility.FromJson<Saved>(json); restored = true;
        // Restore is not an in-game unequip/drop. World snapshots own pickup state.
        acquiredPickup = null;
        Unequip();
        var weapon = RunContentCatalog.Instance.Resolve<WeaponItemData>(saved.weapon);
        if (weapon != null)
        {
            if (!TryEquip(weapon)) throw new InvalidOperationException(name + ": saved enemy weapon is no longer compatible.");
            Ammo = Mathf.Clamp(saved.ammo, 0, Mathf.Max(1, weapon.magazineSize));
            if (!string.IsNullOrEmpty(saved.pickupId))
            {
                var entity = ActiveRunController.Instance?.FindWorldObject(saved.pickupId);
                // Isolated scenes/tests may use participant restore without a run controller.
                if (ActiveRunController.Instance == null)
                    foreach (var candidate in FindObjectsByType<RunWorldObject>(FindObjectsInactive.Include))
                        if (candidate.Id == saved.pickupId) { entity = candidate; break; }
                acquiredPickup = entity != null ? entity.GetComponent<PickupItem>() : null;
                if (acquiredPickup == null || acquiredPickup.Item != weapon)
                    throw new InvalidOperationException(name + ": saved acquired pickup is missing or incompatible.");
                acquiredPickup.gameObject.SetActive(false);
            }
        }
    }
}
