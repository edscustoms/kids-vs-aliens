using System.Collections.Generic;
using UnityEngine;

// Animation writes only. Both gameplay and presentation supply intent/time.
public sealed class CharacterAnimatorDriver
{
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int WeaponStyle = Animator.StringToHash("WeaponStyle");
    private static readonly int CombatStance = Animator.StringToHash("CombatStance");
    private readonly bool supportsCombatStance;
    private static readonly int Floating = Animator.StringToHash("Floating");
    public const string FloatingLayerName = "FloatingPresentation";
    private readonly bool supportsFloating;
    private readonly int floatingLayer = -1;
    private readonly int footworkLayer = -1;
    private readonly int guardLayer = -1;
    private bool combatReady, legActionEntered;
    private CharacterActionId lastAction;
    private bool legAction;
    private bool footworkAction;
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
            if (parameter.nameHash == Floating && parameter.type == AnimatorControllerParameterType.Bool)
                supportsFloating = true;
            if (
                parameter.nameHash == CombatStance
                && parameter.type == AnimatorControllerParameterType.Bool
            )
                supportsCombatStance = true;
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
        floatingLayer = animator.GetLayerIndex(FloatingLayerName);
        footworkLayer = animator.GetLayerIndex("CombatFootwork");
        guardLayer = animator.GetLayerIndex("CombatGuard");
    }

    public void SetWeaponStyle(WeaponAnimationStyle style)
    {
        if (IsCompatible)
            animator.SetInteger(WeaponStyle, (int)style);
    }

    public void SetCombatStance(bool active)
    {
        combatReady = active;
        if (supportsCombatStance && animator != null)
            animator.SetBool(CombatStance, active);
    }

    public void SetFloating(bool active)
    {
        if (!supportsFloating || animator == null) return;
        animator.SetBool(Floating, active);
        // A zero-weight override is essential: even an empty masked layer can affect
        // the existing controller's mixed write-defaults animation poses.
        if (floatingLayer >= 0) animator.SetLayerWeight(floatingLayer, active ? 1f : 0f);
    }

    public void SetMovement(Vector2 movement, float deltaTime)
    {
        if (!IsCompatible)
            return;
        animator.SetFloat(MoveX, movement.x, 0.05f, deltaTime);
        animator.SetFloat(MoveY, movement.y, 0.05f, deltaTime);
        if (guardLayer >= 0)
            animator.SetLayerWeight(guardLayer, Mathf.MoveTowards(animator.GetLayerWeight(guardLayer), combatReady ? 1 : 0, deltaTime/ .25f));
        if (footworkLayer >= 0)
        {
            if (footworkAction && IsActionPlaying(lastAction)) legActionEntered = true;
            else if (legActionEntered) footworkAction = legAction = legActionEntered = false;
            bool exiting = TryActionState(lastAction,out _,out int layer,out int state) && IsExitingState(layer,state);
            bool standingGuard = combatReady && movement.sqrMagnitude < .01f;
            bool plantedAction = footworkAction && !exiting && legAction && !CanChainAction(lastAction);
            float target = standingGuard || plantedAction ? 1 : 0;
            float blend = footworkAction ? .125f : .25f;
            animator.SetLayerWeight(footworkLayer, Mathf.MoveTowards(animator.GetLayerWeight(footworkLayer), target, deltaTime / blend));
        }
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
        lastAction = action;
        legAction = actions.TryGetBinding(action, out var binding) && binding.requiresLegMotion;
        footworkAction = binding.layerName == "UnarmedCombatActions";
        legActionEntered = false;
        return true;
    }

    public bool IsActionPlaying(CharacterActionId action) => TryActionState(action, out _, out int layer, out int state)
        && IsInState(layer,state);

    public bool CanChainAction(CharacterActionId action)
    {
        if (!TryActionState(action,out var binding,out int layer,out int state) || IsExitingState(layer,state)) return false;
        var info = animator.GetCurrentAnimatorStateInfo(layer);
        return info.fullPathHash == state && info.normalizedTime >= binding.chainStart && info.normalizedTime < 1;
    }

    private bool TryActionState(CharacterActionId action, out CharacterAnimationActions.Binding binding, out int layer, out int state)
    {
        binding=default; layer=-1; state=0;
        if (animator == null || !animator.isActiveAndEnabled || actions == null || !actions.TryGetBinding(action,out binding)
            || string.IsNullOrEmpty(binding.layerName) || string.IsNullOrEmpty(binding.statePath)) return false;
        layer=animator.GetLayerIndex(binding.layerName); state=Animator.StringToHash(binding.statePath);
        return layer >= 0;
    }

    public void CancelAction(CharacterActionId action)
    {
        if (lastAction == action)
        {
            footworkAction = legAction = legActionEntered = false;
            if (animator != null && animator.isActiveAndEnabled && footworkLayer >= 0)
                animator.CrossFadeInFixedTime("CombatFootwork.Guard", .16f, footworkLayer);
        }
        if (
            animator != null
            && actions != null
            && actions.TryGetTrigger(action, out int trigger)
            && triggers.Contains(trigger)
        )
            animator.ResetTrigger(trigger);
        if (
            animator == null
            || actions == null
            || !actions.TryGetBinding(action, out var binding)
            || string.IsNullOrEmpty(binding.cancellationStatePath)
        )
            return;
        int layer = animator.GetLayerIndex(binding.layerName);
        int destination = Animator.StringToHash(binding.cancellationStatePath);
        if (
            layer >= 0
            && animator.isActiveAndEnabled
            && animator.HasState(layer, destination)
            && IsInState(layer, Animator.StringToHash(binding.statePath))
        )
            animator.CrossFadeInFixedTime(destination, .08f, layer);
    }

    // Only marker-driven actions require this stronger content contract.
    public bool TryGetMarkedAction(
        CharacterActionId action,
        CharacterAnimationEventId marker,
        out CharacterAnimationActions.Binding binding,
        out int layer,
        out int state
    )
    {
        binding = default;
        layer = -1;
        state = 0;
        if (
            !IsCompatible
            || animator == null
            || !animator.isActiveAndEnabled
            || !animator.fireEvents
            || animator.speed <= 0f
            || actions == null
            || !actions.TryGetBinding(action, out binding)
            || binding.clip == null
            || binding.clip.length <= 0f
            || string.IsNullOrEmpty(binding.layerName)
            || string.IsNullOrEmpty(binding.statePath)
        )
            return false;
        layer = animator.GetLayerIndex(binding.layerName);
        state = Animator.StringToHash(binding.statePath);
        if (
            layer < 0
            || !animator.HasState(layer, state)
            || (layer > 0 && animator.GetLayerWeight(layer) <= 0f)
        )
            return false;
        // An old/cancelled performance must finish before a new marked request
        // can own its events. Callers can use the normal immediate fallback.
        if (IsInState(layer, state))
            return false;
        bool usesClip = false;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip == binding.clip)
            {
                usesClip = true;
                break;
            }
        if (!usesClip)
            return false;
        foreach (AnimationEvent authored in binding.clip.events)
            if (
                authored.functionName
                    == nameof(CharacterAnimationEventRelay.OnCharacterAnimationEvent)
                && authored.intParameter == (int)marker
            )
                return true;
        return false;
    }

    public bool IsInState(int layer, int state) =>
        animator != null
        && (
            animator.GetCurrentAnimatorStateInfo(layer).fullPathHash == state
            || (
                animator.IsInTransition(layer)
                && animator.GetNextAnimatorStateInfo(layer).fullPathHash == state
            )
        );

    public bool IsExitingState(int layer, int state) =>
        animator != null
        && animator.IsInTransition(layer)
        && animator.GetCurrentAnimatorStateInfo(layer).fullPathHash == state
        && animator.GetNextAnimatorStateInfo(layer).fullPathHash != state;
}
