using UnityEngine;

public enum GrenadeActivationMode
{
    Timed,
    Impact,
}

[CreateAssetMenu(
    fileName = "NewGrenade",
    menuName = "Game/Items/Grenade"
)]
public sealed class GrenadeItemData : ItemData
{
    [Header("Visuals")]
    public GameObject heldPrefab;
    public GameObject stowedPrefab;
    public GrenadeInstance thrownPrefab;

    [Header("Knowledge")]
    public SkillData requiredSkill;

    [Header("Throw")]
    [Min(0.01f)]
    public float maxChargeTime = 1.25f;

    [Min(0f)]
    public float minThrowSpeed = 4.5f;

    [Min(0f)]
    public float maxThrowSpeed = 12f;

    [Min(0f)]
    public float upwardBias = 0.35f;

    [Header("Activation")]
    public GrenadeActivationMode activationMode =
        GrenadeActivationMode.Timed;

    [Min(0f)]
    public float fuseTime = 1.4f;

    public GrenadeEffectData effectData;

    [Header("Inert Recovery")]
    [Min(0f)]
    public float inertRecoveryDelay = 2f;

    [Min(0.1f)]
    public float maxInertLifetime = 7f;

    private void OnValidate()
    {
        itemType = ItemType.Grenade;

        maxChargeTime =
            Mathf.Max(
                0.01f,
                maxChargeTime);

        minThrowSpeed =
            Mathf.Max(
                0f,
                minThrowSpeed);

        maxThrowSpeed =
            Mathf.Max(
                minThrowSpeed,
                maxThrowSpeed);

        upwardBias =
            Mathf.Max(
                0f,
                upwardBias);

        fuseTime =
            Mathf.Max(
                0f,
                fuseTime);

        inertRecoveryDelay =
            Mathf.Max(
                0f,
                inertRecoveryDelay);

        maxInertLifetime =
            Mathf.Max(
                0.1f,
                maxInertLifetime);
    }
}
