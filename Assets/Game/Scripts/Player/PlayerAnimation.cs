using System;
using UnityEngine;
using StarterAssets;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField]
    private PlayerCharacter playerCharacter;

    [SerializeField]
    private PlayerEquipment playerEquipment;

    [Header("Floating Presentation")]
    [Tooltip("Meters away from the beam start before Floating can begin. Presentation only.")]
    [SerializeField, Min(0f)] private float beamFloatStartClearance = .5f;
    [Tooltip("Meters before the destination; on a curved hoist this is height above landing during final descent.")]
    [SerializeField, Min(0f)] private float beamFloatLandingClearance = .5f;
    [Tooltip("Required drop from the last supported feet height to the support below. Ordinary jump height does not count.")]
    [SerializeField, Min(0f)] private float minimumFallHeightForFloating = 2f;
    private ThirdPersonController locomotion;
    private BeamTransportController transport;
    private readonly RaycastHit[] fallSupports = new RaycastHit[16];
    private float previousFeetY, lastSupportedY, previousBeamY;
    private bool hasSupportedHeight, wasTransporting, beamFloatFinished, floating;
    public bool IsFloating => floating;

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
    private bool combatStance;

    public event Action<CharacterAnimationEventId> AnimationEventReceived;
    public event Action<CharacterActionId> ActionInterrupted;

    private WeaponAnimationStyle currentWeaponStyle = WeaponAnimationStyle.Unarmed;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        locomotion = GetComponent<ThirdPersonController>();
        transport = GetComponent<BeamTransportController>();
        previousFeetY = FeetY;

        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();

        if (playerEquipment == null)
            playerEquipment = GetComponent<PlayerEquipment>();

    }

    private void OnEnable()
    {
        if (transport != null) transport.TransportEnded += ClearFloating;
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
        if (transport != null) transport.TransportEnded -= ClearFloating;
        ClearFloating();
        hasSupportedHeight = false;
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
        driver?.SetFloating(false);
        InterruptMarkedAction();
        DetachRelay();
        animator = visual != null ? visual.Animator : null;
        driver = animator != null ? new CharacterAnimatorDriver(animator, visual.AnimationActions) : null;
        relay = animator != null ? animator.GetComponent<CharacterAnimationEventRelay>() : null;
        if (relay != null) relay.Marker += HandleMarker;

        ApplyWeaponStyle();
        driver?.SetCombatStance(combatStance);
        driver?.SetFloating(floating);
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

    public void SetCombatStance(bool active)
    {
        combatStance = active;
        driver?.SetCombatStance(active);
    }

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
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController != markedController
            || !driver.IsInState(markedLayer, markedState) || driver.IsExitingState(markedLayer, markedState))
        { InterruptMarkedAction(); return; }
        waitingForMarker = false; // Duplicate/late markers cannot reenter gameplay.
        AnimationEventReceived?.Invoke(marker);
    }

    private void LateUpdate()
    {
        UpdateFloating();
        if (!waitingForMarker || Time.timeScale <= 0f) return;
        if (animator == null || !animator.isActiveAndEnabled || !animator.fireEvents || animator.speed <= 0f
            || animator.runtimeAnimatorController != markedController || relay == null || !relay.isActiveAndEnabled)
        { InterruptMarkedAction(); return; }
        bool inState = driver.IsInState(markedLayer, markedState);
        if (driver.IsExitingState(markedLayer, markedState)) { InterruptMarkedAction(); return; }
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

    private float FeetY => characterController == null ? transform.position.y
        : transform.TransformPoint(characterController.center).y
            - characterController.height * Mathf.Abs(transform.lossyScale.y) * .5f;

    // Runs after locomotion; observes its collision-derived Grounded flag without writing it.
    private void UpdateFloating()
    {
        float feetY = FeetY;
        if (transport != null && transport.IsTransporting)
        {
            Vector3 start = transport.PresentationStart, end = transport.PresentationDestination;
            Vector3 position = transform.position;
            if (!wasTransporting) { previousBeamY = start.y; beamFloatFinished = false; }
            bool nearEnd;
            if (transport.PresentationIsCurved)
            {
                // Do not mistake the ascending crossing of landing height for the final descent.
                nearEnd = transport.PresentationProgress > .5f && position.y < previousBeamY
                    && position.y <= end.y + beamFloatLandingClearance;
            }
            else
                nearEnd = Vector3.Dot(end - position, (end - start).normalized) <= beamFloatLandingClearance;
            beamFloatFinished |= nearEnd || transport.PresentationProgress >= 1f;
            SetFloating(!beamFloatFinished && (position - start).magnitude > beamFloatStartClearance);
            previousBeamY = position.y;
            wasTransporting = true;
        }
        else if (locomotion != null && characterController != null && characterController.enabled && locomotion.enabled)
        {
            wasTransporting = beamFloatFinished = false;
            if (locomotion.Grounded)
            {
                lastSupportedY = feetY; hasSupportedHeight = true;
                SetFloating(false);
            }
            else if (Time.timeScale > 0f)
            {
                bool descending = feetY < previousFeetY - .0001f;
                SetFloating(hasSupportedHeight && descending && HasSignificantDrop(feetY));
            }
        }
        // A pause keeps the current pose. Other disable paths explicitly clear it.
        previousFeetY = feetY;
    }

    private bool HasSignificantDrop(float feetY)
    {
        int count = Physics.RaycastNonAlloc(new Vector3(transform.position.x, feetY + .1f, transform.position.z),
            Vector3.down, fallSupports, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore);
        if (count == fallSupports.Length) return false;
        float nearest = float.PositiveInfinity, supportY = 0f;
        for (int i = 0; i < count; i++)
        {
            var hit = fallSupports[i];
            if (hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest
                || Physics.GetIgnoreLayerCollision(gameObject.layer, hit.collider.gameObject.layer)
                || Vector3.Angle(hit.normal, Vector3.up) > characterController.slopeLimit) continue;
            nearest = hit.distance; supportY = hit.point.y;
        }
        return !float.IsPositiveInfinity(nearest) && lastSupportedY - supportY >= minimumFallHeightForFloating;
    }

    private void SetFloating(bool active)
    {
        floating = active;
        driver?.SetFloating(active);
    }

    private void ClearFloating()
    {
        SetFloating(false);
        wasTransporting = beamFloatFinished = false;
        previousFeetY = FeetY;
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
