using UnityEngine;

// Equipment calls this before returning/dropping its weapon. Loot never owns that weapon.
[DisallowMultipleComponent]
public sealed class EnemyPlasmaLoot : MonoBehaviour, IRunStateParticipant
{
    [SerializeField] private PickupItem plasmaPickup;
    [SerializeField, Range(0, 1)] private float plasmaDropChance = .75f;
    [SerializeField, Min(1)] private int plasmaDropMin = 2;
    [SerializeField, Min(1)] private int plasmaDropMax = 4;
    private bool resolved;

    public GameObject ResolveDeath(WeaponItemData weapon)
    {
        if (resolved || (ActiveRunController.Instance != null && !ActiveRunController.Instance.IsReady)) return null;
        resolved = true;
        if (weapon == null || !weapon.usesPlasmaCapsules || plasmaPickup == null
            || !(plasmaPickup.Item is CapsuleItemData data) || data.kind != CapsuleKind.Plasma
            || plasmaDropChance <= 0 || (plasmaDropChance < 1 && Random.value >= plasmaDropChance)) return null;
        var pickup = Instantiate(plasmaPickup, transform.position + Vector3.up * .55f, Quaternion.identity);
        int minimum = Mathf.Max(1, plasmaDropMin);
        pickup.SetQuantity(Random.Range(minimum, Mathf.Max(minimum, plasmaDropMax) + 1));
        RunWorldObject.TrackSpawn(pickup.gameObject, plasmaPickup.gameObject);
        ActiveRunController.Instance?.MarkDirty();
        return pickup.gameObject;
    }

    public string RunStateKey => "enemy-plasma-loot-v1";
    [System.Serializable] private sealed class Saved { public bool resolved; }
    public string CaptureRunState() => JsonUtility.ToJson(new Saved { resolved = resolved });
    public void RestoreRunState(string json) => resolved = JsonUtility.FromJson<Saved>(json).resolved;
}
