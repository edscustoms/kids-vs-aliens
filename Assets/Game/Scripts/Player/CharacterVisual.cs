using System.Collections.Generic;
using UnityEngine;

public class CharacterVisual : MonoBehaviour
{
    [SerializeField]
    private Animator animator;

    [SerializeField]
    private CharacterAnimationActions animationActions;
    public CharacterAnimationActions AnimationActions => animationActions;

    [SerializeField]
    private Transform weaponSocket;

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
