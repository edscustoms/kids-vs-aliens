using UnityEngine;

public class PickupItem : MonoBehaviour
{
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
        collected = true;
        GetComponent<RunWorldObject>()?.MarkRemoved();
        Destroy(gameObject);
    }
}
