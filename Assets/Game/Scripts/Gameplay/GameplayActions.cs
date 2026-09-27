using System;
using UnityEngine;

public enum ObjectiveAction { None, Start, Update, Complete }

// Small reusable Inspector block. Owners may also call DialoguePlayer/ObjectiveController directly.
[Serializable]
public sealed class GameplayActions
{
    public DialogueMessage dialogue;
    public ObjectiveAction objectiveAction;
    public ObjectiveDefinition objective;
    [Min(1)] public int progressAmount = 1;
    [Tooltip("Optional text sent through the existing run/system-message presentation.")]
    public string systemMessage;

    public void Execute(PlayerCharacter player)
    {
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
    }
}
