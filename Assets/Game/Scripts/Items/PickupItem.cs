using UnityEngine;

public class PickupItem : MonoBehaviour, IRunStateParticipant
{
    private static readonly System.Collections.Generic.HashSet<PickupItem> available = new();
    public static System.Collections.Generic.IEnumerable<PickupItem> Available => available;
    public ItemData Item => item;
    private MonoBehaviour reservation;
    private void OnEnable() { available.Add(this); }
    private void OnDisable() { available.Remove(this); reservation = null; }
    public bool CanReserve(MonoBehaviour owner) => !collected && isActiveAndEnabled && (reservation == null || !reservation.isActiveAndEnabled || reservation == owner);
    public bool TryReserve(MonoBehaviour owner) { if (owner == null || !CanReserve(owner)) return false; reservation = owner; return true; }
    public bool IsReservedBy(MonoBehaviour owner) => !collected && isActiveAndEnabled && reservation == owner;
    public void ReleaseReservation(MonoBehaviour owner) { if (reservation == owner) reservation = null; }
    public bool ConsumeReserved(MonoBehaviour owner) { if (!IsReservedBy(owner)) return false; Consume(); return true; }
    // Enemy acquisition transfers custody of this world object; it is not consumption.
    public bool RetainReserved(MonoBehaviour owner)
    {
        if (!IsReservedBy(owner)) return false;
        var entity = GetComponent<RunWorldObject>();
        if (entity == null || string.IsNullOrEmpty(entity.Id))
        {
            if (ActiveRunController.Instance != null)
                RunWorldObject.TrackSpawn(gameObject, item.worldPrefab);
            else
            {
                entity = entity != null ? entity : gameObject.AddComponent<RunWorldObject>();
                entity.ConfigureIdentity(System.Guid.NewGuid().ToString("N"));
            }
        }
        transform.SetParent(null, true);
        gameObject.SetActive(false);
        ActiveRunController.Instance?.MarkDirty();
        return true;
    }
    public void ReturnToWorld(Vector3 position)
    {
        transform.SetPositionAndRotation(position, Quaternion.identity);
        GetComponent<WorldItemFloat>()?.ResetAnchor();
        gameObject.SetActive(true);
        ActiveRunController.Instance?.MarkDirty();
    }
    private void Consume()
    {
        collected = true; available.Remove(this); reservation = null;
        GetComponent<RunWorldObject>()?.MarkRemoved(); Destroy(gameObject);
    }
    [SerializeField]
    private ItemData item;
    [SerializeField, Min(1)] private int quantity = 1;
    public int Quantity => quantity;
    public void SetQuantity(int value) => quantity = Mathf.Max(1, value);
    public string RunStateKey => "pickup-quantity-v1";
    // Non-null only for a specific player-dropped weapon. Custody moves between
    // this pickup and inventory; there is still just one logical magazine record.
    private OwnedWeaponState carriedWeaponState;
    internal void CarryWeaponState(OwnedWeaponState state) => carriedWeaponState = state;
    [System.Serializable] private sealed class Saved
    {
        public int quantity = 1;
        // Unity inline serialization can materialize null nested data as a zeroed
        // object. Presence must be explicit so fresh guns do not restore as empty.
        public bool carriesWeaponState;
        public SavedWeaponState weapon; // Used only when carriesWeaponState is true.
    }
    public string CaptureRunState()
    {
        carriedWeaponState?.FinishReload(Time.time);
        return JsonUtility.ToJson(new Saved {
            quantity = quantity,
            carriesWeaponState = carriedWeaponState != null,
            weapon = carriedWeaponState == null ? null : new SavedWeaponState {
                rounds = carriedWeaponState.Rounds,
                reloadRemaining = carriedWeaponState.ReloadRemaining(Time.time),
                cooldownRemaining = Mathf.Max(0, carriedWeaponState.NextFireTime - Time.time)
            }
        });
    }
    public void RestoreRunState(string json)
    {
        var saved = JsonUtility.FromJson<Saved>(json);
        SetQuantity(saved.quantity);
        carriedWeaponState = null;
        if (saved.carriesWeaponState && saved.weapon != null && item is WeaponItemData weapon)
        {
            carriedWeaponState = new OwnedWeaponState(weapon);
            carriedWeaponState.Restore(saved.weapon.rounds, Time.time,
                saved.weapon.reloadRemaining, saved.weapon.cooldownRemaining);
        }
    }

    private bool collected;

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other.GetComponent<PlayerInventory>());
    }

    public bool TryCollect(PlayerInventory inventory)
    {
        if (collected || inventory == null || !isActiveAndEnabled) return false;
        if (inventory.TryGetComponent<PlayerBikeRider>(out var rider) && rider.IsBusy
            && (!rider.IsDriving || !(item is CapsuleItemData) || Time.timeScale <= 0)) return false;
        if (ActiveRunController.Instance != null && !ActiveRunController.Instance.IsReady) return false;
        if (item is CapsuleItemData capsule)
        {
            if (!inventory.TryAddCapsules(capsule.kind, quantity)) return false;
            CollectCapsules(inventory, capsule.kind, quantity);
            return true;
        }
        // Conversion is a world acquisition rule. Restore and EnsureOwnedWeapon
        // never call it, and ordinary inventory insertion still enforces uniqueness.
        if (item is WeaponItemData weapon && weapon.usesPlasmaCapsules && weapon.duplicatePlasmaReward > 0
            && inventory.GetWeaponState(weapon) != null)
        {
            if (!inventory.TryAddCapsules(CapsuleKind.Plasma, weapon.duplicatePlasmaReward)) return false;
            CollectCapsules(inventory, CapsuleKind.Plasma, weapon.duplicatePlasmaReward);
            return true;
        }

        if (!inventory.CanAcceptItem(item, out InventoryAddFailure failure))
        {
            if (failure == InventoryAddFailure.Full)
                inventory
                    .GetComponent<PlayerFeedback>()
                    ?.Report(
                        new GameplayFeedbackEvent(
                            FeedbackCode.InventoryFull,
                            item: item,
                            action: FeedbackAction.Pickup
                        )
                    );
            else if (failure == InventoryAddFailure.AlreadyLearned)
                inventory.GetComponent<PlayerFeedback>()?.Report(
                    new GameplayFeedbackEvent(FeedbackCode.KnowledgeAlreadyKnown,
                        ((KnowledgeBookItemData)item).skill, item, FeedbackAction.Pickup));
            else if (failure == InventoryAddFailure.InvalidItem)
                Debug.LogWarning("Pickup has no ItemData assigned.", this);
            return false;
        }

        var transferredState = carriedWeaponState;
        carriedWeaponState = null;
        if (!inventory.TryAddItem(item, out _, transferredState))
        {
            carriedWeaponState = transferredState;
            return false;
        }
        Consume();
        return true;
    }

    private void CollectCapsules(PlayerInventory inventory, CapsuleKind kind, int amount)
    {
        Consume();
        inventory.GetComponent<PlayerFeedback>()?.Report(new GameplayFeedbackEvent(
            kind == CapsuleKind.Plasma ? FeedbackCode.PlasmaCollected : FeedbackCode.ArmorCapsulesCollected,
            item: item, action: FeedbackAction.Pickup, amount: amount));
    }
}
