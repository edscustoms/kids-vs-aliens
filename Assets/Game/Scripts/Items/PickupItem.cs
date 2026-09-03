using UnityEngine;

public class PickupItem : MonoBehaviour
{
    [SerializeField] private ItemData item;

    private bool collected;

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        PlayerInventory inventory =
            other.GetComponent<PlayerInventory>();

        if (inventory == null)
            return;

        if (!inventory.TryAddItem(item))
            return;

        collected = true;
        Destroy(gameObject);
    }
}
