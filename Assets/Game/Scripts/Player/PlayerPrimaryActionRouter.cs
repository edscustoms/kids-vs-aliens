using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerPrimaryActionRouter : MonoBehaviour
{
    [SerializeField]
    private StarterAssetsInputs input;

    [SerializeField]
    private PlayerShooter shooter;

    [SerializeField]
    private PlayerGrenadeController grenadeController;

    private bool awaitingNeutral;

    private void Awake()
    {
        if (input == null)
            input = GetComponent<StarterAssetsInputs>();

        if (shooter == null)
            shooter = GetComponent<PlayerShooter>();

        if (grenadeController == null)
        {
            grenadeController = GetComponent<PlayerGrenadeController>();
        }
    }

    private void OnEnable()
    {
        if (input != null)
        {
            input.ShootStateChanged += HandleShootStateChanged;

            input.ShootCanceled += HandleShootCanceled;
        }

        if (grenadeController != null)
        {
            grenadeController.GrenadeSelectionChanged += HandleGrenadeSelectionChanged;
        }
    }

    private void Start()
    {
        if (grenadeController != null && grenadeController.IsGrenadeSelected)
        {
            awaitingNeutral = input != null && input.shoot;

            shooter?.SetTriggerHeld(false);
        }
        else
        {
            shooter?.SetTriggerHeld(input != null && input.shoot);
        }
    }

    private void OnDisable()
    {
        if (input != null)
        {
            input.ShootStateChanged -= HandleShootStateChanged;

            input.ShootCanceled -= HandleShootCanceled;
        }

        if (grenadeController != null)
        {
            grenadeController.GrenadeSelectionChanged -= HandleGrenadeSelectionChanged;
        }

        shooter?.SetTriggerHeld(false);

        grenadeController?.CancelThrow();

        awaitingNeutral = false;
    }

    private void HandleShootStateChanged(bool pressed)
    {
        if (grenadeController != null && grenadeController.IsGrenadeSelected)
        {
            shooter?.SetTriggerHeld(false);

            if (awaitingNeutral)
            {
                if (!pressed)
                    awaitingNeutral = false;

                return;
            }

            if (pressed)
            {
                grenadeController.BeginCharge();
            }
            else if (grenadeController.IsCharging)
            {
                grenadeController.ReleaseThrow();
            }

            return;
        }

        awaitingNeutral = false;

        shooter?.SetTriggerHeld(pressed);
    }

    private void HandleShootCanceled()
    {
        shooter?.SetTriggerHeld(false);

        awaitingNeutral = false;

        if (grenadeController != null && grenadeController.IsGrenadeSelected)
        {
            grenadeController.CancelCharge();
        }
    }

    private void HandleGrenadeSelectionChanged(bool selected)
    {
        shooter?.SetTriggerHeld(false);

        awaitingNeutral = selected && input != null && input.shoot;
    }
}
