using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private PlayerCharacter playerCharacter;
    [SerializeField] private PlayerEquipment playerEquipment;

    private CharacterController characterController;
    private Animator animator;

    private WeaponAnimationStyle currentWeaponStyle =
        WeaponAnimationStyle.Unarmed;

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
        animator = visual.Animator;

        ApplyWeaponStyle();
    }

    private void OnEquippedWeaponChanged(WeaponItemData weapon)
    {
        currentWeaponStyle = weapon != null
            ? weapon.animationStyle
            : WeaponAnimationStyle.Unarmed;

        ApplyWeaponStyle();
    }

    private void ApplyWeaponStyle()
    {
        if (animator == null)
            return;

        animator.SetInteger(
            "WeaponStyle",
            (int)currentWeaponStyle
        );
    }

    private void Update()
    {
        if (animator == null)
            return;

        Vector3 velocity = characterController.velocity;
        velocity.y = 0f;

        Vector3 localVelocity =
            transform.InverseTransformDirection(velocity);

        Vector2 movement = new(
            localVelocity.x,
            localVelocity.z
        );

        if (movement.sqrMagnitude > 0.01f)
            movement.Normalize();
        else
            movement = Vector2.zero;

        animator.SetFloat(
            "MoveX",
            movement.x,
            0.05f,
            Time.deltaTime
        );

        animator.SetFloat(
            "MoveY",
            movement.y,
            0.05f,
            Time.deltaTime
        );
    }
}