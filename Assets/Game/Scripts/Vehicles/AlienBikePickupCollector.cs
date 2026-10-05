using UnityEngine;

/// <summary>Forwards physical resource-trigger contact to the seated player's existing pickup/inventory path.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(AlienBikeController))]
public sealed class AlienBikePickupCollector : MonoBehaviour
{
    private AlienBikeController bike;
    private void Awake() => bike = GetComponent<AlienBikeController>();
    private void OnTriggerEnter(Collider other) => Collect(other);
    private void OnTriggerStay(Collider other) => Collect(other);
    private void Collect(Collider other)
    {
        var rider = bike.Rider;
        if (rider == null || !rider.IsDriving || Time.timeScale <= 0) return;
        var pickup = other.GetComponentInParent<PickupItem>();
        if (pickup != null && pickup.Item is CapsuleItemData)
            pickup.TryCollect(rider.GetComponent<PlayerInventory>());
    }
}
