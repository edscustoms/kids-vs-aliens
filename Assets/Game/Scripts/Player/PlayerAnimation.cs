using System;
using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField]
    private PlayerCharacter playerCharacter;

    [SerializeField]
    private PlayerEquipment playerEquipment;

    private CharacterController characterController;
    private CharacterAnimatorDriver driver;
    private Animator animator;
    private CharacterAnimationEventRelay relay;
    private RuntimeAnimatorController markedController;
    private CharacterAnimationActions.Binding markedBinding;
    private CharacterActionId markedAction;
    private CharacterAnimationEventId expectedMarker;
    private int markedLayer, markedState;
    private bool waitingForMarker, enteredMarkedState;
    private float enterElapsed;

    public event Action<CharacterAnimationEventId> AnimationEventReceived;
    public event Action<CharacterActionId> ActionInterrupted;

    private WeaponAnimationStyle currentWeaponStyle = WeaponAnimationStyle.Unarmed;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();

        if (playerEquipment == null)
            playerEquipment = GetComponent<PlayerEquipment>();

    }

    private void OnEnable()
    {
        if (playerCharacter != null)
        {
            playerCharacter.CharacterChanged += OnCharacterChanged;
            OnCharacterChanged(playerCharacter.ActiveVisual);
        }
        if (playerEquipment != null)
        {
            playerEquipment.EquippedWeaponChanged += OnEquippedWeaponChanged;
            OnEquippedWeaponChanged(playerEquipment.EquippedWeapon);
        }
    }

    private void OnDisable()
    {
        if (playerCharacter != null)
            playerCharacter.CharacterChanged -= OnCharacterChanged;

        if (playerEquipment != null)
            playerEquipment.EquippedWeaponChanged -= OnEquippedWeaponChanged;
        InterruptMarkedAction();
        DetachRelay();
        driver = null;
        animator = null;
    }

    private void OnCharacterChanged(CharacterVisual visual)
    {
        InterruptMarkedAction();
        DetachRelay();
        animator = visual != null ? visual.Animator : null;
        driver = animator != null ? new CharacterAnimatorDriver(animator, visual.AnimationActions) : null;
        relay = animator != null ? animator.GetComponent<CharacterAnimationEventRelay>() : null;
        if (relay != null) relay.Marker += HandleMarker;

        ApplyWeaponStyle();
    }

    private void OnEquippedWeaponChanged(WeaponItemData weapon)
    {
        currentWeaponStyle = weapon != null ? weapon.animationStyle : WeaponAnimationStyle.Unarmed;

        ApplyWeaponStyle();
    }

    private void ApplyWeaponStyle()
    {
        driver?.SetWeaponStyle(currentWeaponStyle);
    }

    private void Update()
    {
        if (driver == null || characterController == null)
            return;

        Vector3 velocity = characterController.velocity;
        velocity.y = 0f;

        Vector3 localVelocity = transform.InverseTransformDirection(velocity);

        Vector2 movement = new(localVelocity.x, localVelocity.z);

        if (movement.sqrMagnitude > 0.01f)
            movement.Normalize();
        else
            movement = Vector2.zero;

        driver.SetMovement(movement, Time.deltaTime);
    }

    public bool TryPlayAction(CharacterActionId action) =>
        driver != null && driver.TryPlayAction(action);

    public bool TryPlayAction(CharacterActionId action, CharacterAnimationEventId marker)
    {
        if (!isActiveAndEnabled || waitingForMarker || relay == null || !relay.isActiveAndEnabled
            || driver == null || !driver.TryGetMarkedAction(action, marker,
                out var binding, out int layer, out int state)) return false;
        markedAction = action; expectedMarker = marker; markedBinding = binding;
        markedLayer = layer; markedState = state;
        markedController = animator.runtimeAnimatorController;
        enteredMarkedState = false; enterElapsed = 0f;
        waitingForMarker = true;
        if (driver.TryPlayAction(action)) return true;
        waitingForMarker = false;
        return false;
    }

    public void CancelAction(CharacterActionId action)
    {
        if (waitingForMarker && markedAction == action) waitingForMarker = false;
        driver?.CancelAction(action);
    }

    private void HandleMarker(CharacterAnimationEventId marker, int sourceState)
    {
        if (!waitingForMarker || marker != expectedMarker || sourceState != markedState) return;
        waitingForMarker = false; // Duplicate/late markers cannot reenter gameplay.
        AnimationEventReceived?.Invoke(marker);
    }

    private void LateUpdate()
    {
        if (!waitingForMarker || Time.timeScale <= 0f) return;
        if (animator == null || !animator.isActiveAndEnabled || !animator.fireEvents || animator.speed <= 0f
            || animator.runtimeAnimatorController != markedController || relay == null || !relay.isActiveAndEnabled)
        { InterruptMarkedAction(); return; }
        bool inState = driver.IsInState(markedLayer, markedState);
        if (inState)
        {
            enteredMarkedState = true;
            var current = animator.GetCurrentAnimatorStateInfo(markedLayer);
            if (current.fullPathHash == markedState && current.normalizedTime >= 1f)
                InterruptMarkedAction();
        }
        else if (enteredMarkedState) InterruptMarkedAction();
        else
        {
            // A broken transition must not strand gameplay. This only cancels
            // a request; physical release is NEVER timed here. Budget follows
            // the authored clip length instead of a gameplay timing constant.
            enterElapsed += Time.deltaTime * animator.speed;
            if (enterElapsed >= markedBinding.clip.length) InterruptMarkedAction();
        }
    }

    private void InterruptMarkedAction()
    {
        if (!waitingForMarker) return;
        CharacterActionId action = markedAction;
        CancelAction(action);
        ActionInterrupted?.Invoke(action);
    }

    private void DetachRelay()
    {
        if (relay != null) relay.Marker -= HandleMarker;
        relay = null;
    }
}
