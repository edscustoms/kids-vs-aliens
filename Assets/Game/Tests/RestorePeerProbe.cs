#if UNITY_EDITOR
using System;
using UnityEngine;

/// <summary>Editor regression fixture, excluded from player builds.</summary>
public sealed class RestorePeerProbe : MonoBehaviour, IRunStateParticipant
{
    [Serializable] private sealed class State { public string peer; public int value; public bool completed; }
    public string PeerId;
    public int Value;
    public bool Completed;
    public int RestoreCount { get; private set; }
    public int RewardCount { get; private set; }
    public int CompletionEvents { get; private set; }
    public int PeerValueAfterReady { get; private set; } = -1;
    public int FirstPeerValue { get; private set; } = -1;
    public string RunStateKey => "restore-peer-probe";
    public void Complete()
    {
        if (Completed) return;
        Completed = true; RewardCount++; CompletionEvents++;
    }
    public string CaptureRunState() => JsonUtility.ToJson(new State { peer = PeerId, value = Value, completed = Completed });
    public void RestoreRunState(string json)
    {
        var state = JsonUtility.FromJson<State>(json);
        PeerId = state.peer; Value = state.value; Completed = state.completed; RestoreCount++;
        // Observe only, never derive final state or replay Complete() here.
        var peer = ActiveRunController.Instance.FindWorldObject(PeerId)?.GetComponent<RestorePeerProbe>();
        if (RestoreCount == 1 && peer != null) FirstPeerValue = peer.Value;
    }
    private void Update()
    {
        if (ActiveRunController.Instance == null || !ActiveRunController.Instance.IsReady) return;
        var peer = ActiveRunController.Instance.FindWorldObject(PeerId)?.GetComponent<RestorePeerProbe>();
        if (peer != null) PeerValueAfterReady = peer.Value;
    }
}
#endif
