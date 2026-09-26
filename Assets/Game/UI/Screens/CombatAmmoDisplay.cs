using TMPro;
using UnityEngine;
using static InterfaceFactory;

// Read-only view of the selected inventory entry; temporary weapon hiding is not selection.
public sealed class CombatAmmoDisplay : MonoBehaviour
{
    private PlayerInventory inventory;
    private TMP_Text rounds, plasma, armor, status;
    private RectTransform progress;
    private WeaponItemData shownWeapon;
    private int shownRounds = -1, shownPlasma = -1, shownArmor = -1;
    private string shownStatus;
    public void Build(Transform safeArea, PlayerInventory source)
    {
        inventory = source;
        var root = Panel(safeArea, "CombatAmmo", new(.026f, .70f), new(.235f, .855f));
        root.SetAsFirstSibling();
        var surface = root.GetComponent<NeonPanel>(); surface.raycastTarget = false; surface.details = false;
        rounds = Text(root, "Magazine", "—", new(.08f,.52f), new(.72f,.96f), 32);
        status = Text(root, "AmmoStatus", "", new(.08f,.34f), new(.94f,.55f), 16, Muted);
        var plasmaIcon = Panel(root, "PlasmaCapsuleIcon", new(.08f,.10f), new(.15f,.29f));
        plasmaIcon.GetComponent<NeonPanel>().radius = 100;
        plasmaIcon.GetComponent<NeonPanel>().accent = Cyan;
        plasma = Text(root, "PlasmaCount", "", new(.18f,.07f), new(.49f,.32f), 19, Cyan);
        NeonVisuals.Symbol(root, InterfaceSymbol.Armor, new(.55f,.10f), new(.63f,.29f)).color = Green;
        armor = Text(root, "ArmorCapsuleCount", "", new(.66f,.07f), new(.95f,.32f), 19, Green);
        progress = Panel(root, "ReloadProgress", new(.08f,.02f), new(.92f,.045f));
        progress.GetComponent<NeonPanel>().SetShape(NeonShape.Fill);
        foreach (var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>()) graphic.raycastTarget = false;
        Refresh();
    }
    private void Update() => Refresh();
    public void Refresh()
    {
        if (inventory == null || rounds == null) return;
        var weapon = inventory.SelectedItem as WeaponItemData;
        var state = inventory.GetWeaponState(weapon);
        int ammo = state != null ? state.Rounds : -1;
        if (weapon != shownWeapon || ammo != shownRounds)
        {
            shownWeapon = weapon; shownRounds = ammo;
            rounds.text = weapon != null ? $"{ammo:00} / {weapon.magazineSize:00}" : "—";
        }
        if (shownPlasma != inventory.PlasmaCapsules) { shownPlasma = inventory.PlasmaCapsules; plasma.text = shownPlasma.ToString(); }
        if (shownArmor != inventory.ArmorCapsules) { shownArmor = inventory.ArmorCapsules; armor.text = shownArmor.ToString(); }
        bool reloading = state != null && state.IsReloading;
        bool insufficient = weapon != null && ammo == 0 && weapon.usesPlasmaCapsules && inventory.PlasmaCapsules < Mathf.Max(1, weapon.plasmaReloadCost);
        string label = reloading ? "RELOADING" : insufficient ? "NEED PLASMA" : weapon != null && ammo == 0 ? "EMPTY" : "PLASMA / ARMOR CAPSULES";
        if (label != shownStatus) { shownStatus = label; status.text = label; status.color = insufficient && !reloading ? new Color(1,.77f,.3f) : Muted; }
        progress.gameObject.SetActive(reloading);
        if (reloading) progress.anchorMax = new(.08f + .84f * (1 - Mathf.Clamp01(state.ReloadRemaining(Time.time) / Mathf.Max(.001f, weapon.reloadTime))), .045f);
    }
}
