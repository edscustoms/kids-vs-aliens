using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class BikeRouteFinishTrigger : MonoBehaviour
{
    [SerializeField] private BikeRouteChaseDirector director;
    private void OnTriggerEnter(Collider other) => Enter(other);
    private void OnTriggerStay(Collider other) => Enter(other);
    private void Enter(Collider other)
    {
        if (Time.timeScale <= 0) return;
        var rider = other.GetComponentInParent<PlayerBikeRider>();
        if (rider == null) rider = other.GetComponentInParent<AlienBikeController>()?.Rider;
        if (rider != null) director.ReachFinish(rider);
    }
}
