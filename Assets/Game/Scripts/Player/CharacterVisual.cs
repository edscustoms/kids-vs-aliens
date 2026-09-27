using System.Collections.Generic;
using UnityEngine;

public class CharacterVisual : MonoBehaviour
{
    [SerializeField] private DialogueSpeaker dialogueIdentity;
    public DialogueSpeaker DialogueIdentity => dialogueIdentity;
    [SerializeField]
    private Animator animator;

    [SerializeField]
    private CharacterAnimationActions animationActions;
    public CharacterAnimationActions AnimationActions => animationActions;

    [SerializeField]
    private Transform weaponSocket;
    [System.Serializable] public struct WeaponMount { public WeaponAnimationStyle style; public Transform socket; }
    [Tooltip("Body-specific attachment poses. Weapon geometry, grips, muzzle and size belong to the weapon prefab.")]
    [SerializeField] private WeaponMount[] weaponMounts = new WeaponMount[0];
    public Transform GetWeaponMount(WeaponAnimationStyle style)
    {
        foreach (var mount in weaponMounts) if (mount.style == style && mount.socket != null) return mount.socket;
        return weaponSocket;
    }

    [Header("Grenades")]
    [Tooltip("Ordered character-specific sockets used for stowed grenade visuals.")]
    [SerializeField]
    private Transform[] grenadeCarrySockets = new Transform[0];

    [Header("Aura")]
    [SerializeField]
    private Color auraColor = Color.magenta;

    public Animator Animator => animator;
    public Transform WeaponSocket => weaponSocket;
    public IReadOnlyList<Transform> GrenadeCarrySockets => grenadeCarrySockets;
    public Color AuraColor => auraColor;
    public bool HasWeaponSocket => weaponSocket != null;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
            Debug.LogError($"{name}: No Animator found.");
    }
}
