using System.Collections.Generic;
using UnityEngine;

// Animation writes only. Both gameplay and presentation supply intent/time.
public sealed class CharacterAnimatorDriver
{
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int WeaponStyle = Animator.StringToHash("WeaponStyle");
    private readonly Animator animator;
    private readonly CharacterAnimationActions actions;
    private readonly HashSet<int> triggers = new();
    public bool IsCompatible { get; }

    public CharacterAnimatorDriver(Animator animator, CharacterAnimationActions actions = null)
    {
        this.animator = animator;
        this.actions = actions;
        if (animator == null || animator.runtimeAnimatorController == null)
            return;
        bool x = false,
            y = false,
            style = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (
                parameter.nameHash == MoveX
                && parameter.type == AnimatorControllerParameterType.Float
            )
                x = true;
            if (
                parameter.nameHash == MoveY
                && parameter.type == AnimatorControllerParameterType.Float
            )
                y = true;
            if (
                parameter.nameHash == WeaponStyle
                && parameter.type == AnimatorControllerParameterType.Int
            )
                style = true;
            if (parameter.type == AnimatorControllerParameterType.Trigger)
                triggers.Add(parameter.nameHash);
        }
        IsCompatible = x && y && style;
    }

    public void SetWeaponStyle(WeaponAnimationStyle style)
    {
        if (IsCompatible)
            animator.SetInteger(WeaponStyle, (int)style);
    }

    public void SetMovement(Vector2 movement, float deltaTime)
    {
        if (!IsCompatible)
            return;
        animator.SetFloat(MoveX, movement.x, 0.05f, deltaTime);
        animator.SetFloat(MoveY, movement.y, 0.05f, deltaTime);
    }

    public bool TryPlayAction(CharacterActionId action)
    {
        if (!IsCompatible)
            return false;
        if (action == CharacterActionId.EquippedStance)
            return true;
        if (
            actions == null
            || !actions.TryGetTrigger(action, out int trigger)
            || !triggers.Contains(trigger)
        )
            return false;
        animator.SetTrigger(trigger);
        return true;
    }

    public void CancelAction(CharacterActionId action)
    {
        if (animator != null && actions != null && actions.TryGetTrigger(action, out int trigger)
            && triggers.Contains(trigger)) animator.ResetTrigger(trigger);
    }

    // Only marker-driven actions require this stronger content contract.
    public bool TryGetMarkedAction(CharacterActionId action, CharacterAnimationEventId marker,
        out CharacterAnimationActions.Binding binding, out int layer, out int state)
    {
        binding = default; layer = -1; state = 0;
        if (!IsCompatible || animator == null || !animator.isActiveAndEnabled || !animator.fireEvents
            || animator.speed <= 0f || actions == null || !actions.TryGetBinding(action, out binding)
            || binding.clip == null || binding.clip.length <= 0f || string.IsNullOrEmpty(binding.layerName)
            || string.IsNullOrEmpty(binding.statePath)) return false;
        layer = animator.GetLayerIndex(binding.layerName);
        state = Animator.StringToHash(binding.statePath);
        if (layer < 0 || !animator.HasState(layer, state) || (layer > 0 && animator.GetLayerWeight(layer) <= 0f))
            return false;
        // An old/cancelled performance must finish before a new marked request
        // can own its events. Callers can use the normal immediate fallback.
        if (IsInState(layer, state)) return false;
        bool usesClip = false;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip == binding.clip) { usesClip = true; break; }
        if (!usesClip) return false;
        foreach (AnimationEvent authored in binding.clip.events)
            if (authored.functionName == nameof(CharacterAnimationEventRelay.OnCharacterAnimationEvent)
                && authored.intParameter == (int)marker) return true;
        return false;
    }

    public bool IsInState(int layer, int state) => animator != null
        && (animator.GetCurrentAnimatorStateInfo(layer).fullPathHash == state
            || (animator.IsInTransition(layer) && animator.GetNextAnimatorStateInfo(layer).fullPathHash == state));
}
