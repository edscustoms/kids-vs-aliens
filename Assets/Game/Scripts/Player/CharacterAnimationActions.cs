using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Kids VS Aliens/Characters/Animation Actions")]
public sealed class CharacterAnimationActions : ScriptableObject
{
    [Serializable]
    public struct Binding
    {
        public CharacterActionId action;
        public string triggerParameter;
    }

    [SerializeField]
    private Binding[] bindings = Array.Empty<Binding>();

    public bool TryGetTrigger(CharacterActionId action, out int trigger)
    {
        foreach (Binding binding in bindings)
            if (binding.action == action && !string.IsNullOrWhiteSpace(binding.triggerParameter))
            {
                trigger = Animator.StringToHash(binding.triggerParameter);
                return true;
            }
        trigger = 0;
        return false;
    }
}
