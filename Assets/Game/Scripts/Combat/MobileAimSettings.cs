using UnityEngine;

public enum MobileAimZone
{
    Green,
    Blue,
    Yellow,
}

[CreateAssetMenu(fileName = "MobileAimSettings", menuName = "Game/Combat/Mobile Aim Settings")]
public class MobileAimSettings : ScriptableObject
{
    // =====================================================
    // GREEN
    // Guaranteed-hit area around the calculated body point.
    // =====================================================

    [Header("Green Zone - Guaranteed Hit")]
    [Tooltip(
        "Radius relative to the target's calculated body size. "
            + "Shots in this zone will be forced onto valid target geometry."
    )]
    [Range(0f, 1f)]
    [SerializeField]
    private float greenRadius = 0.05f;

    [Tooltip("Chance of selecting the Green zone.")]
    [Range(0, 100)]
    [SerializeField]
    private int greenChance = 5;

    // =====================================================
    // BLUE
    // Strong aim assist, but actual raycast can still miss.
    // =====================================================

    [Header("Blue Zone - Strong Assist")]
    [Tooltip(
        "Radius relative to the target's calculated body size. "
            + "Points may hit or miss depending on the actual silhouette."
    )]
    [Range(0f, 2f)]
    [SerializeField]
    private float blueRadius = 0.35f;

    [Tooltip("Chance of selecting the Blue zone.")]
    [Range(0, 100)]
    [SerializeField]
    private int blueChance = 35;

    // =====================================================
    // YELLOW
    // Large area including deliberate miss margin.
    // =====================================================

    [Header("Yellow Zone - Hit / Miss Margin")]
    [Tooltip(
        "Radius relative to the target's calculated body size. "
            + "Includes target body plus intentional miss space."
    )]
    [Range(0f, 3f)]
    [SerializeField]
    private float yellowRadius = 0.75f;

    [Tooltip("Chance of selecting the Yellow zone.")]
    [Range(0, 100)]
    [SerializeField]
    private int yellowChance = 60;

    // =====================================================
    // PUBLIC VALUES
    // =====================================================

    public float GreenRadius => greenRadius;
    public float BlueRadius => blueRadius;
    public float YellowRadius => yellowRadius;

    public int GreenChance => greenChance;
    public int BlueChance => blueChance;
    public int YellowChance => yellowChance;

    public int TotalChance => greenChance + blueChance + yellowChance;

    // =====================================================
    // ROLL
    // Called when a shot needs an aim destination.
    // NOT every frame.
    // =====================================================

    public MobileAimZone RollZone()
    {
        int total = TotalChance;

        if (total <= 0)
        {
            Debug.LogError($"{name}: Mobile aim chances total 0. " + "Falling back to Yellow.");

            return MobileAimZone.Yellow;
        }

        // Uses the configured values as weights.
        // So even while tweaking and temporarily sitting at
        // 99 or 101, nothing explodes.
        int roll = Random.Range(0, total);

        if (roll < greenChance)
            return MobileAimZone.Green;

        roll -= greenChance;

        if (roll < blueChance)
            return MobileAimZone.Blue;

        return MobileAimZone.Yellow;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Keep zones ordered correctly.
        blueRadius = Mathf.Max(blueRadius, greenRadius);
        yellowRadius = Mathf.Max(yellowRadius, blueRadius);

        int total = TotalChance;

        if (total != 100)
        {
            Debug.LogWarning(
                $"{name}: Mobile aim chances currently total "
                    + $"{total}%. They should total exactly 100%."
            );
        }
    }
#endif
}
