using UnityEngine;

/// <summary>
/// Run-specific state owned by PlayerInventory, or carried by its specific world pickup
/// after a player drop. Custody transfers on pickup; mounted WeaponInstances never own it.
/// </summary>
public sealed class OwnedWeaponState
{
    public WeaponItemData Weapon { get; }
    public int Rounds { get; private set; }
    public float NextFireTime { get; private set; }
    public bool IsReloading { get; private set; }
    private float reloadEndsAt;

    public OwnedWeaponState(WeaponItemData weapon)
    {
        Weapon = weapon;
        Rounds = Mathf.Max(0, weapon.magazineSize);
    }

    public bool FinishReload(float now)
    {
        if (!IsReloading || now < reloadEndsAt) return false;
        Rounds = Mathf.Max(0, Weapon.magazineSize);
        IsReloading = false;
        return true;
    }

    public bool TrySpendRound(float now)
    {
        FinishReload(now);
        if (IsReloading || Rounds <= 0 || now < NextFireTime) return false;
        Rounds--;
        NextFireTime = now + 1f / Weapon.fireRate;
        return true;
    }

    public void BeginReload(float now)
    {
        if (IsReloading) return;
        IsReloading = true;
        reloadEndsAt = now + Mathf.Max(0, Weapon.reloadTime);
    }

    public float ReloadRemaining(float now) => IsReloading ? Mathf.Max(0, reloadEndsAt - now) : 0;

    public void Restore(int rounds, float now, float reloadRemaining = 0, float cooldownRemaining = 0)
    {
        Rounds = Mathf.Clamp(rounds, 0, Mathf.Max(0, Weapon.magazineSize));
        IsReloading = reloadRemaining > 0 && float.IsFinite(reloadRemaining);
        reloadEndsAt = now + (IsReloading ? reloadRemaining : 0);
        NextFireTime = now + (float.IsFinite(cooldownRemaining) ? Mathf.Max(0, cooldownRemaining) : 0);
    }
}
