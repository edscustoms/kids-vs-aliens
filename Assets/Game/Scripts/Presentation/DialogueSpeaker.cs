using UnityEngine;

public enum DialogueVoiceType { Boy, Girl }

[CreateAssetMenu(menuName = "Gameplay/Dialogue Speaker")]
public sealed class DialogueSpeaker : ScriptableObject
{
    public string displayName;
    public DialogueVoiceType voiceType = DialogueVoiceType.Girl;
    [Tooltip("Optional stable story identity, e.g. amy. Never a runtime instance ID.")]
    public string specialCharacterId;
}
