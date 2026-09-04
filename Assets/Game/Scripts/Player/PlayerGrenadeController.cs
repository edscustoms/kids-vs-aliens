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
    private bool weaponWasVisible;
    private bool isCommittingThrow;

    // Grenade mode temporarily publishes a presentation-only
    // EquippedWeaponChanged notification so the existing animation path
    // can switch to Unarmed without actually unequipping the gun.
    //
    // Ignore our own notification here; real weapon changes must still
    // cancel grenade mode normally.
    private bool isChangingWeaponPresentation;

    public bool IsGrenadeSelected => state != GrenadeState.Idle;

    public bool IsCharging => state == GrenadeState.Charging;

    public GrenadeItemData SelectedGrenade => selectedGrenade;

    public float Charge01 =>
        selectedGrenade != null
            ? Mathf.Clamp01(chargeTime / Mathf.Max(0.01f, selectedGrenade.maxChargeTime))
            : 0f;

    public event Action<bool> GrenadeSelectionChanged;
    public event Action<float> ChargeChanged;

    private void Awake()
    {
        CacheReferences();
        RefreshOwnerColliders();
    }

    private void OnEnable()
    {
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

        isCommittingThrow = true;

        bool consumed = inventory.TryConsumeGrenade(selectedGrenade);

        isCommittingThrow = false;

        if (!consumed)
        {
            Destroy(instance.gameObject);

            ExitGrenadeMode(true);

            return false;
        }

        float speed = Mathf.Lerp(
            selectedGrenade.minThrowSpeed,
            selectedGrenade.maxThrowSpeed,
            Charge01
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

        ExitGrenadeMode(true);

        return true;
    }

    public void CancelThrow()
    {
        ExitGrenadeMode(true);
    }

    private void ExitGrenadeMode(bool restoreWeapon)
    {
        bool wasSelected = IsGrenadeSelected;

        if (heldVisual != null)
        {
            heldVisual.gameObject.SetActive(false);

            Destroy(heldVisual.gameObject);
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
