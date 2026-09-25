using UnityEngine;

public class PickupItem : MonoBehaviour
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

    private bool collected;

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        PlayerInventory inventory = other.GetComponent<PlayerInventory>();

        if (inventory == null)
            return;

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
            return;
        }

        if (!inventory.TryAddItem(item, out _)) return;
        Consume();
    }
}
