using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public sealed class LevelBeamTransportTrigger : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float height = 5f;
    [SerializeField, Min(0.1f)] private float duration = 2f;
    [SerializeField] private UnityEvent onTransported = new();
    private bool used;
    private void Reset() => GetComponent<BoxCollider>().isTrigger = true;
    private void OnTriggerEnter(Collider other)
    {
        if (used) return;
        var transport = other.GetComponentInParent<BeamTransportController>();
        if (transport != null) transport.TryDeparture(height, duration, () =>
        {
            used = true;
            onTransported.Invoke();
        });
    }
}
