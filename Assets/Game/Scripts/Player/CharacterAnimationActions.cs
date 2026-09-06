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
        [Tooltip("Optional authored state/clip contract for gameplay actions that wait for a marker.")]
        public string layerName;
        public string statePath;
        public AnimationClip clip;
        [Tooltip("Optional layer state used to blend out a cancelled action. Leave empty for existing action behavior.")]
        public string cancellationStatePath;
    }

    [SerializeField]
    private Binding[] bindings = Array.Empty<Binding>();

    public bool TryGetBinding(CharacterActionId action, out Binding binding)
    {
        foreach (Binding candidate in bindings)
            if (candidate.action == action) { binding = candidate; return true; }
        binding = default;
        return false;
    }

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
