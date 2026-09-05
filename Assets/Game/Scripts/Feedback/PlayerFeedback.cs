using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerFeedback : MonoBehaviour
{
    public event Action<GameplayFeedbackEvent> Reported;

    public void Report(GameplayFeedbackEvent feedback) => Reported?.Invoke(feedback);
}
