using UnityEngine;

/// <summary>Explicit trigger-to-pod reference; each volume has its own kinematic body.</summary>
[DisallowMultipleComponent]
public sealed class HealingPodTrigger : MonoBehaviour
{
    [SerializeField] private HealingPodController pod;
    [SerializeField] private bool chamber;

    private void OnTriggerEnter(Collider other) => Enter(other);
    private void OnTriggerStay(Collider other) => Enter(other);
    private void Enter(Collider other)
    {
        if (pod == null || !pod.isActiveAndEnabled) return;
        if (chamber) pod.EnterChamber(other);
        else pod.EnterProximity(other);
    }
    private void OnTriggerExit(Collider other)
    {
        if (pod != null && !chamber) pod.ExitProximity(other);
    }
}
