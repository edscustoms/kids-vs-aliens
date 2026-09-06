using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerGrenadeController : MonoBehaviour
{
    private enum GrenadeState
    {
        Idle,
        Held,
        Charging,
        Throwing,
    }

    [Header("References")]
    [SerializeField]
    private PlayerInventory inventory;

    [SerializeField]
    private PlayerEquipment equipment;

    [SerializeField]
    private PlayerShooter shooter;

    [SerializeField]
    private PlayerSkillState skillState;

    [SerializeField]
    private PlayerCharacter playerCharacter;

    [SerializeField]
    private PlayerAnimation playerAnimation;

    [Header("Throw")]
    [SerializeField, Min(0f)]
    private float ownerCollisionIgnoreTime = 0.2f;

    [SerializeField]
    private Vector2 angularSpeedRange = new Vector2(4f, 9f);

    private GrenadeState state;
    private GrenadeItemData selectedGrenade;
    private HeldItemGrip heldVisual;

    private Collider[] ownerColliders = new Collider[0];

    private float chargeTime;
    private float committedCharge01;
    private bool releaseMarkerPending;
    private bool weaponWasVisible;
    private bool isCommittingThrow;

    // Grenade mode temporarily publishes a presentation-only
    // EquippedWeaponChanged notification so the existing animation path
    // can switch to Unarmed without actually unequipping the gun.
    //
    // Ignore our own notification here; real weapon changes must still
    // cancel grenade mode normally.
    private bool isChangingWeaponPresentation;
    private StarterAssets.StarterAssetsInputs input;

    public bool IsGrenadeSelected => state != GrenadeState.Idle;

    public bool IsCharging => state == GrenadeState.Charging;
    public bool IsThrowing => state == GrenadeState.Throwing;

    public GrenadeItemData SelectedGrenade => selectedGrenade;

    public float Charge01 =>
        selectedGrenade != null
            ? Mathf.Clamp01(chargeTime / Mathf.Max(0.01f, selectedGrenade.maxChargeTime))
            : 0f;

    public event Action<bool> GrenadeSelectionChanged;
    public event Action<float> ChargeChanged;

    private void Awake()
    {
        input = GetComponent<StarterAssets.StarterAssetsInputs>();
        CacheReferences();
        RefreshOwnerColliders();
    }

    private void OnEnable()
    {
        if (playerAnimation != null)
        {
            playerAnimation.AnimationEventReceived += HandleAnimationEvent;
            playerAnimation.ActionInterrupted += HandleActionInterrupted;
        }
        if (inventory != null)
        {
            inventory.OnInventoryChanged += HandleGrenadesChanged;
        }

        if (equipment != null)
        {
            equipment.EquippedWeaponChanged += HandleEquippedWeaponChanged;
        }

        if (playerCharacter != null)
        {
            playerCharacter.CharacterChanged += HandleCharacterChanged;
        }
    }

    private void Start()
    {
        // Component Awake ordering is not guaranteed. Refresh after
        // PlayerCharacter has had a chance to spawn its active visual.
        RefreshOwnerColliders();
    }

    private void OnDisable()
    {
        if (playerAnimation != null)
        {
            playerAnimation.AnimationEventReceived -= HandleAnimationEvent;
            playerAnimation.ActionInterrupted -= HandleActionInterrupted;
        }
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= HandleGrenadesChanged;
        }

        if (equipment != null)
        {
            equipment.EquippedWeaponChanged -= HandleEquippedWeaponChanged;
        }

        if (playerCharacter != null)
        {
            playerCharacter.CharacterChanged -= HandleCharacterChanged;
        }

        ExitGrenadeMode(true);
    }

    private void Update()
    {
        if (IsThrowing)
        {
            if (releaseMarkerPending && Time.timeScale > 0f
                && (input == null || !input.GameplayInputBlocked)) CommitThrow();
            return;
        }
        if (state != GrenadeState.Charging)
            return;

        chargeTime += Time.deltaTime;

        chargeTime = Mathf.Min(chargeTime, selectedGrenade.maxChargeTime);

        ChargeChanged?.Invoke(Charge01);
    }

    public bool SelectFirstAvailableGrenade()
    {
        return inventory != null && SelectGrenade(inventory.GetFirstGrenade());
    }

    public bool SelectGrenade(GrenadeItemData grenade)
    {
        if (IsThrowing || isCommittingThrow) return false;
        if (input != null && !input.CanProcessGameplayInput)
            return false;
        if (
            grenade == null
            || inventory == null
            || !inventory.HasGrenade(grenade)
            || playerCharacter == null
            || playerCharacter.ActiveVisual == null
            || !playerCharacter.ActiveVisual.HasWeaponSocket
            || grenade.heldPrefab == null
            || grenade.thrownPrefab == null
        )
        {
            return false;
        }

        if (IsGrenadeSelected)
        {
            ExitGrenadeMode(true);
        }

        GameObject heldObject = Instantiate(grenade.heldPrefab);

        HeldItemGrip grip = heldObject.GetComponent<HeldItemGrip>();

        if (grip == null || !grip.AttachTo(playerCharacter.ActiveVisual.WeaponSocket))
        {
            Debug.LogError(
                $"{grenade.name}: heldPrefab needs a valid HeldItemGrip on its root.",
                this
            );

            Destroy(heldObject);

            return false;
        }

        selectedGrenade = grenade;
        heldVisual = grip;
        state = GrenadeState.Held;
        chargeTime = 0f;

        weaponWasVisible = equipment != null && equipment.IsEquippedWeaponVisible;

        // IMPORTANT:
        // Hide the gun WITHOUT unequipping it, then publish a presentation
        // change of null. Existing weapon-animation handling therefore sees
        // "no presented weapon" and falls back to Unarmed while ammo/reload
        // state remain untouched.
        SetWeaponPresentationVisible(false);

        shooter?.SetFireBlocked(true);

        ChargeChanged?.Invoke(0f);

        GrenadeSelectionChanged?.Invoke(true);

        return true;
    }

    public bool BeginCharge()
    {
        if (input != null && !input.CanProcessGameplayInput)
            return false;
        if (state != GrenadeState.Held || selectedGrenade == null)
        {
            return false;
        }

        state = GrenadeState.Charging;
        chargeTime = 0f;

        ChargeChanged?.Invoke(0f);

        return true;
    }

    public bool ReleaseThrow()
    {
        if (input != null && !input.CanProcessGameplayInput)
            return false;
        if (
            state != GrenadeState.Charging
            || selectedGrenade == null
            || heldVisual == null
            || inventory == null
            || !inventory.HasGrenade(selectedGrenade)
        )
        {
            return false;
        }

        committedCharge01 = Charge01;
        state = GrenadeState.Throwing;
        // The visual stays attached, inventory untouched, and firearm blocked
        // until the authored marker arrives through the player animation facade.
        if (playerAnimation != null && playerAnimation.TryPlayAction(
            CharacterActionId.GrenadeThrow, CharacterAnimationEventId.GrenadeRelease)) return true;
        return CommitThrow();
    }

    private void HandleAnimationEvent(CharacterAnimationEventId marker)
    {
        if (marker != CharacterAnimationEventId.GrenadeRelease || !IsThrowing) return;
        // If pause acquired during the same frame, retain the already-committed
        // gesture and launch only after gameplay resumes.
        releaseMarkerPending = true;
        if (Time.timeScale > 0f && (input == null || !input.GameplayInputBlocked)) CommitThrow();
    }

    private void HandleActionInterrupted(CharacterActionId action)
    {
        if (action != CharacterActionId.GrenadeThrow || !IsThrowing || isCommittingThrow) return;
        state = GrenadeState.Held;
        committedCharge01 = chargeTime = 0f;
        releaseMarkerPending = false;
        ChargeChanged?.Invoke(0f);
    }

    private bool CommitThrow()
    {
        if (!isActiveAndEnabled || !IsThrowing || isCommittingThrow) return false;
        if (selectedGrenade == null || heldVisual == null || inventory == null
            || !inventory.HasGrenade(selectedGrenade))
        { ExitGrenadeMode(true); return false; }
        isCommittingThrow = true;
        try { return PerformThrow(); }
        finally { isCommittingThrow = false; }
    }

    private bool PerformThrow()
    {

        bool hasRequiredKnowledge =
            selectedGrenade.requiredSkill == null
            || (skillState != null && skillState.HasSkill(selectedGrenade.requiredSkill));

        Vector3 releasePosition = heldVisual.ReleasePosition;

        Quaternion releaseRotation = Quaternion.LookRotation(transform.forward, Vector3.up);

        GrenadeInstance instance = Instantiate(
            selectedGrenade.thrownPrefab,
            releasePosition,
            releaseRotation
        );

        if (
            instance == null
            || !instance.TryPrepare(
                selectedGrenade,
                gameObject,
                hasRequiredKnowledge,
                ownerColliders,
                ownerCollisionIgnoreTime
            )
        )
        {
            if (instance != null)
            {
                Destroy(instance.gameObject);
            }

            ExitGrenadeMode(true);

            return false;
        }

        bool consumed = inventory.TryConsumeGrenade(selectedGrenade);

        if (!consumed)
        {
            Destroy(instance.gameObject);

            ExitGrenadeMode(true);

            return false;
        }

        float speed = Mathf.Lerp(
            selectedGrenade.minThrowSpeed,
            selectedGrenade.maxThrowSpeed,
            committedCharge01
        );

        Vector3 throwDirection = (
            transform.forward + Vector3.up * selectedGrenade.upwardBias
        ).normalized;

        float minimumAngularSpeed = Mathf.Min(angularSpeedRange.x, angularSpeedRange.y);

        float maximumAngularSpeed = Mathf.Max(angularSpeedRange.x, angularSpeedRange.y);

        Vector3 angularVelocity =
            UnityEngine.Random.onUnitSphere
            * UnityEngine.Random.Range(minimumAngularSpeed, maximumAngularSpeed);

        if (!instance.Launch(throwDirection * speed, angularVelocity))
        {
            Debug.LogError($"{selectedGrenade.name}: prepared grenade failed to launch.", this);

            // Preparation validated the launch state, so reaching this branch
            // indicates a prefab/runtime programming error. Do not create a
            // duplicate inventory item after the consumed instance exists.
            Destroy(instance.gameObject);

            ExitGrenadeMode(true);

            return false;
        }

        GrenadeItemData thrownGrenade = selectedGrenade;
        ExitGrenadeMode(true);

        // Presentation cannot interrupt authoritative throw/selection cleanup.
        if (!hasRequiredKnowledge)
            GetComponent<PlayerFeedback>()
                ?.Report(
                    new GameplayFeedbackEvent(
                        FeedbackCode.GrenadeThrownInert,
                        thrownGrenade.requiredSkill,
                        thrownGrenade,
                        FeedbackAction.Throw
                    )
                );

        return true;
    }

    public void CancelThrow()
    {
        ExitGrenadeMode(true);
    }

    // Suspension cancels the gesture, not the selected inventory item.
    public void CancelCharge()
    {
        if (state != GrenadeState.Charging)
            return;
        state = GrenadeState.Held;
        chargeTime = 0f;
        ChargeChanged?.Invoke(0f);
    }

    private void ExitGrenadeMode(bool restoreWeapon)
    {
        bool wasSelected = IsGrenadeSelected;
        playerAnimation?.CancelAction(CharacterActionId.GrenadeThrow);
        releaseMarkerPending = false;
        committedCharge01 = 0f;

        if (heldVisual != null)
        {
            GameObject visual = heldVisual.gameObject;
            visual.SetActive(false);
#if UNITY_EDITOR
            // Editor clip sampling can also deliver markers; immediate destroy
            // is forbidden inside an animation callback. Runtime keeps Destroy.
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += () => { if (visual != null) DestroyImmediate(visual); };
            else
#endif
                Destroy(visual);
        }

        heldVisual = null;
        selectedGrenade = null;
        state = GrenadeState.Idle;
        chargeTime = 0f;

        shooter?.SetFireBlocked(false);

        shooter?.SetTriggerHeld(false);

        if (restoreWeapon && weaponWasVisible)
        {
            // Restore BOTH the same weapon GameObject and the weapon's
            // animation presentation. No re-equip = no ammo/reload reset.
            SetWeaponPresentationVisible(true);
        }

        weaponWasVisible = false;

        if (!wasSelected)
            return;

        ChargeChanged?.Invoke(0f);

        GrenadeSelectionChanged?.Invoke(false);
    }

    private void HandleGrenadesChanged()
    {
        if (
            selectedGrenade != null
            && (inventory == null || !inventory.HasGrenade(selectedGrenade))
        )
        {
            // A successful throw consumes before exiting the mode. Let that
            // synchronous path finish without treating it as cancellation.
            if (!isCommittingThrow)
            {
                ExitGrenadeMode(true);
            }
        }
    }

    private void HandleEquippedWeaponChanged(WeaponItemData weapon)
    {
        // Do not let our temporary animation/presentation notification
        // cancel the grenade we just selected.
        if (isChangingWeaponPresentation)
            return;

        if (!IsGrenadeSelected)
            return;

        ExitGrenadeMode(false);

        // This is a REAL equipment change. The equipment system already
        // published the new weapon for animation, so only ensure its visual
        // is visible here.
        equipment?.SetEquippedWeaponVisible(true);
    }

    private void HandleCharacterChanged(CharacterVisual character)
    {
        if (IsGrenadeSelected)
        {
            ExitGrenadeMode(true);
        }

        RefreshOwnerColliders();
    }

    private void SetWeaponPresentationVisible(bool visible)
    {
        if (equipment == null)
            return;

        isChangingWeaponPresentation = true;

        try
        {
            equipment.SetEquippedWeaponPresentationVisible(visible);
        }
        finally
        {
            isChangingWeaponPresentation = false;
        }
    }

    private void CacheReferences()
    {
        if (playerAnimation == null) playerAnimation = GetComponent<PlayerAnimation>();
        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventory>();
        }

        if (equipment == null)
        {
            equipment = GetComponent<PlayerEquipment>();
        }

        if (shooter == null)
        {
            shooter = GetComponent<PlayerShooter>();
        }

        if (skillState == null)
        {
            skillState = GetComponent<PlayerSkillState>();
        }

        if (playerCharacter == null)
        {
            playerCharacter = GetComponent<PlayerCharacter>();
        }
    }

    private void RefreshOwnerColliders()
    {
        ownerColliders = GetComponentsInChildren<Collider>(true);
    }
}
