using System;
using UnityEngine;

public enum ObjectiveAction { None, Start, Update, Complete }

// Small reusable Inspector block. Owners may also call DialoguePlayer/ObjectiveController directly.
[Serializable]
public sealed class GameplayActions
{
    [Tooltip("Optional authored world arrival. Rejected/aborted arrivals do not consume a one-shot trigger.")]
    public AuthoredBeamArrival beamArrival;
    [Tooltip("Optional component implementing IGameplayAction. Executes synchronously; false rejects this visit.")]
    public MonoBehaviour actionTarget;
    [Tooltip("Optional explicit activation of dormant authored enemies.")]
    public AuthoredEncounter encounter;
    public DialogueMessage dialogue;
    public ObjectiveAction objectiveAction;
    public ObjectiveDefinition objective;
    [Min(1)] public int progressAmount = 1;
    [Tooltip("Optional text sent through the existing run/system-message presentation.")]
    public string systemMessage;

    public bool CanRetry => beamArrival != null && beamArrival.State == AuthoredArrivalState.Available;
    public bool IsCommitted => beamArrival == null || beamArrival.HasArrived;
    public void Execute(PlayerCharacter player) => TryExecute(player);
    public bool TryExecute(PlayerCharacter player)
    {
        var action = actionTarget != null ? actionTarget as IGameplayAction : null;
        if (actionTarget != null && action == null)
        {
            Debug.LogError("Gameplay Action Target must implement IGameplayAction.", actionTarget);
            return false;
        }
        if (beamArrival != null && !beamArrival.TryBegin(player)) return false;
        if (action != null && !action.TryExecute(player)) return false;
        if (encounter != null) encounter.Activate();
        if (dialogue != null) DialoguePlayer.Instance?.Play(dialogue, player != null ? player.ActiveVisual?.DialogueIdentity : null);
        var owner = ObjectiveController.Instance;
        if (owner != null && objective != null)
        {
            switch (objectiveAction)
            {
                case ObjectiveAction.Start: owner.StartObjective(objective); break;
                case ObjectiveAction.Update: owner.AddProgress(objective, progressAmount); break;
                case ObjectiveAction.Complete: owner.CompleteObjective(objective); break;
            }
        }
        if (!string.IsNullOrWhiteSpace(systemMessage)) RunSaveService.Notify(systemMessage);
        return true;
    }
}
