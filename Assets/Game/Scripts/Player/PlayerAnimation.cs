using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField]
    private PlayerCharacter playerCharacter;

    [SerializeField]
    private PlayerEquipment playerEquipment;

    private CharacterController characterController;
    private CharacterAnimatorDriver driver;

    private WeaponAnimationStyle currentWeaponStyle = WeaponAnimationStyle.Unarmed;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();

        if (playerEquipment == null)
            playerEquipment = GetComponent<PlayerEquipment>();

        playerCharacter.CharacterChanged += OnCharacterChanged;
        playerEquipment.EquippedWeaponChanged += OnEquippedWeaponChanged;

        if (playerCharacter.ActiveVisual != null)
            OnCharacterChanged(playerCharacter.ActiveVisual);
    }

    private void OnDestroy()
    {
        if (playerCharacter != null)
            playerCharacter.CharacterChanged -= OnCharacterChanged;

        if (playerEquipment != null)
            playerEquipment.EquippedWeaponChanged -= OnEquippedWeaponChanged;
    }

    private void OnCharacterChanged(CharacterVisual visual)
    {
        driver = new CharacterAnimatorDriver(visual.Animator, visual.AnimationActions);

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
        if (driver == null)
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
}
