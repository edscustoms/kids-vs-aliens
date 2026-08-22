using UnityEngine;

public class CharacterVisual : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform weaponSocket;

    public Animator Animator => animator;
    public Transform WeaponSocket => weaponSocket;
    public bool HasWeaponSocket => weaponSocket != null;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
            Debug.LogError($"{name}: No Animator found.");
    }
}