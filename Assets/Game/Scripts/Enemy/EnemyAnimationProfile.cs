using UnityEngine;

/// <summary>Replace motions with an AnimatorOverrideController while retaining the semantic contract.</summary>
[CreateAssetMenu(menuName = "KVA/Enemies/Animation Profile")]
public sealed class EnemyAnimationProfile : ScriptableObject
{
    public RuntimeAnimatorController controller;
    public string weaponStyleParameter = "WeaponStyle";
    public string pistolReadyState = "Base Layer.PistolLocomotion";
    public string rifleReadyState = "Base Layer.RifleLocomotion";
    [Tooltip("Optional authored recoil trigger. Muzzle flash remains the firing cue if empty.")]
    public string fireTrigger;
}
