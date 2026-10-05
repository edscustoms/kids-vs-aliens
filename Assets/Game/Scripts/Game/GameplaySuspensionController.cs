using System;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

public enum SuspensionReason
{
    ManualPause,
    KnowledgePresentation,
    Modal,
    BeamTransport,
    ApplicationLifecycle,
    HealingPod,
    BikeTransition,
    BikeRiding,
}

// One instance per gameplay scene/player. Presentation owns leases, never time.
[DefaultExecutionOrder(-200), DisallowMultipleComponent]
public sealed class GameplaySuspensionController : MonoBehaviour
{
    public sealed class Lease : IDisposable
    {
        private GameplaySuspensionController owner;
        private readonly int id;

        internal Lease(GameplaySuspensionController owner, int id)
        {
            this.owner = owner;
            this.id = id;
        }

        public bool IsActive => owner != null && owner.owners.ContainsKey(id);

        public void Dispose()
        {
            GameplaySuspensionController previous = owner;
            owner = null;
            if (previous != null)
                previous.Release(id);
        }
    }

    [SerializeField]
    private StarterAssetsInputs input;

    [Tooltip("Update-driven gameplay consumers to suspend; UI and input ingestion stay enabled.")]
    [SerializeField]
    private Behaviour[] gameplayBehaviours = Array.Empty<Behaviour>();
    private readonly Dictionary<int, SuspensionReason> owners = new();
    private bool[] previousEnabled;
    private int nextId;
    private float previousTimeScale;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private bool previousInputBlocked;
    private bool worldPaused;

    public bool IsSuspended => owners.Count != 0;
    public int OwnerCount => owners.Count;
    public bool IsWorldPaused => worldPaused;
    public bool HasBlockingModal =>
        owners.ContainsValue(SuspensionReason.KnowledgePresentation)
        || owners.ContainsValue(SuspensionReason.Modal);
    public event Action<bool> SuspensionChanged;
    // Riding keeps the on-foot lease; only bike controls and the existing pistol path remain available.
    public bool BlocksControls => IsSuspended && !(owners.Count == 1 && owners.ContainsValue(SuspensionReason.BikeRiding));
    public event Action<bool> ControlBlockChanged;

    private void Awake()
    {
        if (input == null)
            input = GetComponent<StarterAssetsInputs>();
    }

    public Lease Acquire(SuspensionReason reason)
    {
        if (!isActiveAndEnabled)
            throw new InvalidOperationException("Suspension controller is not active.");
        if (input == null)
            input = GetComponent<StarterAssetsInputs>();
        int id = ++nextId;
        bool first = !IsSuspended;
        owners.Add(id, reason);
        if (first)
        {
            previousInputBlocked = input != null && input.GameplayInputBlocked;
            // Cancellation must reach the router before consumers are disabled.
            if (input != null)
                input.SetGameplayInputBlocked(true);
            previousEnabled = new bool[gameplayBehaviours.Length];
            for (int i = 0; i < gameplayBehaviours.Length; i++)
            {
                Behaviour consumer = gameplayBehaviours[i];
                if (consumer == null || consumer == this || consumer == input)
                    continue;
                previousEnabled[i] = consumer.enabled;
                consumer.enabled = false;
            }
        }
        RefreshWorldPause();
        RefreshBikeInput();
        if (first) SuspensionChanged?.Invoke(true);
        return new Lease(this, id);
    }

    private void Release(int id)
    {
        if (!owners.Remove(id))
            return;
        RefreshWorldPause();
        RefreshBikeInput();
        if (!IsSuspended) Restore();
    }

    public void ReleaseAll()
    {
        if (!IsSuspended)
            return;
        owners.Clear();
        RefreshWorldPause();
        RefreshBikeInput();
        Restore();
    }

    private void RefreshWorldPause()
    {
        bool shouldPause = false;
        foreach (var reason in owners.Values)
            if (reason != SuspensionReason.BeamTransport && reason != SuspensionReason.HealingPod
                && reason != SuspensionReason.BikeTransition && reason != SuspensionReason.BikeRiding)
            { shouldPause = true; break; }
        if (shouldPause == worldPaused) return;
        worldPaused = shouldPause;
        if (worldPaused)
        {
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }
    }

    private void Restore()
    {
        if (input != null)
            input.SetGameplayInputBlocked(previousInputBlocked);
        if (previousEnabled != null)
            for (int i = 0; i < previousEnabled.Length; i++)
                if (
                    gameplayBehaviours[i] != null
                    && gameplayBehaviours[i] != this
                    && gameplayBehaviours[i] != input
                )
                    gameplayBehaviours[i].enabled = previousEnabled[i];
        previousEnabled = null;
        SuspensionChanged?.Invoke(false);
    }

    private void RefreshBikeInput()
    {
        bool riding = IsSuspended && !BlocksControls && !previousInputBlocked;
        if (input != null) input.SetBikeControlsActive(riding);
        if (previousEnabled != null && IsSuspended)
            for (int i = 0; i < gameplayBehaviours.Length; i++)
            {
                var consumer = gameplayBehaviours[i];
                // Do not restore locomotion, inventory, melee, grenades or Beam with this exception.
                if (consumer is PlayerAim || consumer is PlayerShooter)
                    consumer.enabled = riding && previousEnabled[i];
            }
        ControlBlockChanged?.Invoke(BlocksControls);
    }

    private void OnDisable() => ReleaseAll();

    private void OnDestroy() => ReleaseAll();
}
