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
}
