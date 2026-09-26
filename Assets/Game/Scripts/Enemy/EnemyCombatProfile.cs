using UnityEngine;

/// <summary>Only the new capabilities; existing motor/perception/melee tuning stays on its components.</summary>
[CreateAssetMenu(menuName = "Game/Enemies/Combat Profile")]
public sealed class EnemyCombatProfile : ScriptableObject
{
    [System.Serializable]
    public sealed class RangedBehavior
    {
        [Tooltip("Comfortable distance band as multiples of Preferred Range, capped by weapon range.")]
        public Vector2 rangeBand = new Vector2(.7f, 1.35f);
        public Vector2 positionHoldSeconds = new Vector2(1.1f, 2f);
        public Vector2 aimDelayMultiplier = new Vector2(.8f, 1.15f);
        public Vector2 burstPauseMultiplier = new Vector2(1f, 1.5f);
        [Range(0,1)] public float deliberateStepChance = .18f;
    }
    public bool meleeEnabled = true;
    public bool rangedEnabled = true;
    [Tooltip("Explicit compatible weapon definitions. No player Knowledge checks.")]
    public WeaponItemData[] allowedWeapons = new WeaponItemData[0];
    [Header("Ranged")]
    [Min(.1f)] public float preferredRange = 6;
    [Min(.1f)] public float meleeEnterDistance = 1.05f;
    [Min(.1f)] public float rangedResumeDistance = 1.8f;
    [Min(0)] public float aimDelay = .65f;
    [Range(0,15)] public float spreadDegrees = 2.5f;
    [Range(1,45)] public float facingTolerance = 12;
    [Min(1)] public int automaticBurstCount = 3;
    [Min(0)] public float burstPause = .7f;
    [Min(.1f)] public float repositionInterval = .7f;
    [Header("Ranged commitments")]
    public RangedBehavior pistolBehavior = new RangedBehavior();
    public RangedBehavior rifleBehavior = new RangedBehavior {
        rangeBand = new Vector2(.8f, 1.7f), positionHoldSeconds = new Vector2(2.4f, 3.8f),
        aimDelayMultiplier = new Vector2(.75f, 1f), burstPauseMultiplier = new Vector2(.65f, 1.05f), deliberateStepChance = .06f
    };
    public Vector2 decisionInterval = new Vector2(.18f, .32f);
    [Min(.2f)] public float movementCommitTimeout = 2.5f;
    [Min(0)] public int automaticBurstVariation = 2;
    public RangedBehavior BehaviorFor(WeaponItemData weapon) => weapon != null && weapon.fireMode == WeaponFireMode.Automatic ? rifleBehavior : pistolBehavior;
    [Header("Optional local scavenging")]
    public bool weaponPickupEnabled = true;
    [Min(.5f)] public float weaponInterestRadius = 6;
    [Min(.1f)] public float weaponScanInterval = .65f;
    [Min(.1f)] public float pickupDistance = .65f;
    [Min(1)] public float pickupTimeout = 8;
    [Min(.1f)] public float immediateThreatDistance = 2;
    public bool Allows(WeaponItemData weapon)
    {
        if (!rangedEnabled || weapon == null || weapon.equippedPrefab == null) return false;
        foreach (var allowed in allowedWeapons) if (allowed == weapon) return true;
        return false;
    }
}
