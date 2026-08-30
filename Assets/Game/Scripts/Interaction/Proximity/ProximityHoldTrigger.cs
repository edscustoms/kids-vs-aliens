using System;
using System.Collections.Generic;
using UnityEngine;

namespace KidsVsAliens.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ProximityHoldTrigger : MonoBehaviour
    {
        [Header("Hold")]
        [SerializeField, Min(0.05f)]
        private float holdDuration = 3f;

        [SerializeField]
        private bool oneShot = true;

        [SerializeField]
        private bool resetWhenLeaving = true;

        private readonly HashSet<Collider> activeColliders =
            new HashSet<Collider>();

        private ProximityInteractor activeInteractor;
        private float elapsed;
        private bool completed;

        public event Action<ProximityInteractor> Started;
        public event Action<float, float> ProgressChanged;
        public event Action<ProximityInteractor> Cancelled;
        public event Action<ProximityInteractor> Completed;

        public float HoldDuration => holdDuration;
        public float NormalizedProgress =>
            holdDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / holdDuration);

        public bool IsActive => activeInteractor != null;
        public bool IsCompleted => completed;

        public void Configure(
            float duration,
            bool shouldBeOneShot = true,
            bool shouldResetWhenLeaving = true)
        {
            holdDuration = Mathf.Max(0.05f, duration);
            oneShot = shouldBeOneShot;
            resetWhenLeaving = shouldResetWhenLeaving;
        }

        private void Update()
        {
            if (completed ||
                activeInteractor == null)
            {
                return;
            }

            elapsed += Time.deltaTime;

            ProgressChanged?.Invoke(
                NormalizedProgress,
                Mathf.Max(0f, holdDuration - elapsed));

            if (elapsed < holdDuration)
                return;

            elapsed = holdDuration;
            completed = true;

            ProgressChanged?.Invoke(1f, 0f);
            Completed?.Invoke(activeInteractor);

            if (oneShot)
                enabled = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (completed || other == null)
                return;

            ProximityInteractor interactor =
                other.GetComponentInParent<ProximityInteractor>();

            if (interactor == null)
                return;

            if (activeInteractor != null &&
                activeInteractor != interactor)
            {
                return;
            }

            bool wasInactive =
                activeInteractor == null;

            activeInteractor = interactor;
            activeColliders.Add(other);

            if (!wasInactive)
                return;

            elapsed = 0f;
            Started?.Invoke(activeInteractor);
            ProgressChanged?.Invoke(0f, holdDuration);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null ||
                activeInteractor == null)
            {
                return;
            }

            ProximityInteractor interactor =
                other.GetComponentInParent<ProximityInteractor>();

            if (interactor != activeInteractor)
                return;

            activeColliders.Remove(other);

            if (activeColliders.Count > 0)
                return;

            CancelInteraction();
        }

        private void OnDisable()
        {
            if (!completed)
                CancelInteraction();
        }

        [ContextMenu("Reset Trigger")]
        public void ResetTrigger()
        {
            completed = false;
            elapsed = 0f;
            activeInteractor = null;
            activeColliders.Clear();
            enabled = true;

            ProgressChanged?.Invoke(0f, holdDuration);
        }

        private void CancelInteraction()
        {
            ProximityInteractor previous =
                activeInteractor;

            activeInteractor = null;
            activeColliders.Clear();

            if (resetWhenLeaving)
                elapsed = 0f;

            if (previous != null)
                Cancelled?.Invoke(previous);

            ProgressChanged?.Invoke(
                NormalizedProgress,
                Mathf.Max(0f, holdDuration - elapsed));
        }
    }
}
