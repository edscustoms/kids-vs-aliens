using UnityEngine;

[CreateAssetMenu(
    fileName = "NewElectricGrenadeEffect",
    menuName = "Game/Grenades/Electric Effect"
)]
public sealed class ElectricGrenadeEffectData :
    GrenadeEffectData
{
    [Header("Gameplay")]
    [Min(0f)]
    public float radius = 4.5f;

    [Min(0f)]
    public float damage = 18f;

    [Min(0f)]
    public float stunDuration = 0.75f;

    [Tooltip("Actors eligible to receive this effect. Set this to Enemy for V1.")]
    public LayerMask targetMask;

    [Tooltip("Solid world layers capable of blocking the blast. Targets do not need to be included.")]
    public LayerMask obstructionMask = ~0;

    [Header("Presentation")]
    public ElectricGrenadeBurstVFX burstPrefab;

    public Color effectColor =
        new Color(
            0.35f,
            0.9f,
            1f,
            1f);

    private void OnValidate()
    {
        radius =
            Mathf.Max(
                0f,
                radius);

        damage =
            Mathf.Max(
                0f,
                damage);

        stunDuration =
            Mathf.Max(
                0f,
                stunDuration);
    }
}
