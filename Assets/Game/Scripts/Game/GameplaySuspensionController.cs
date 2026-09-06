using System;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

public enum SuspensionReason
{
    ManualPause,
    KnowledgePresentation,
    Modal,
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

    public bool IsSuspended => owners.Count != 0;
    public int OwnerCount => owners.Count;
    public bool HasBlockingModal =>
        owners.ContainsValue(SuspensionReason.KnowledgePresentation)
        || owners.ContainsValue(SuspensionReason.Modal);
    public event Action<bool> SuspensionChanged;

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
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
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
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SuspensionChanged?.Invoke(true);
        }
        return new Lease(this, id);
    }

    private void Release(int id)
    {
        if (!owners.Remove(id) || IsSuspended)
            return;
        Restore();
    }

    public void ReleaseAll()
    {
        if (!IsSuspended)
            return;
        owners.Clear();
        Restore();
    }

    private void Restore()
    {
        Time.timeScale = previousTimeScale;
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
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        SuspensionChanged?.Invoke(false);
    }

    private void OnDisable() => ReleaseAll();

    private void OnDestroy() => ReleaseAll();
}
