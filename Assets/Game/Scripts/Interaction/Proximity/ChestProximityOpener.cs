using KidsVsAliens.Environment;
using UnityEngine;

namespace KidsVsAliens.Interaction
{
    /// <summary>
    /// Thin adapter. LootChest remains proximity-agnostic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChestProximityOpener : MonoBehaviour
    {
        [SerializeField] private ProximityHoldTrigger trigger;
        [SerializeField] private LootChest chest;

        public void Configure(
            ProximityHoldTrigger newTrigger,
            LootChest newChest)
        {
            trigger = newTrigger;
            chest = newChest;
        }

        private void OnEnable()
        {
            if (trigger != null)
                trigger.Completed += OnCompleted;
        }

        private void OnDisable()
        {
            if (trigger != null)
                trigger.Completed -= OnCompleted;
        }

        private void OnCompleted(ProximityInteractor interactor)
        {
            if (chest == null)
                return;

            chest.Open(
                interactor != null
                    ? interactor.transform
                    : null);
        }
    }
}
