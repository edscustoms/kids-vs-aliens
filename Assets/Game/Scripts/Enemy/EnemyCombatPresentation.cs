using UnityEngine;

/// <summary>Authored visual child + replaceable animation package; no model names in enemy AI.</summary>
[DefaultExecutionOrder(-200), DisallowMultipleComponent]
public sealed class EnemyCombatPresentation : MonoBehaviour
{
    [SerializeField] private CharacterVisual visual;
    [SerializeField] private EnemyAnimationProfile animationProfile;
    public CharacterVisual Visual => visual;
    public Animator Animator => visual != null ? visual.Animator : null;
    private WeaponAnimationStyle style;
    private bool styleDirty = true;
    public WeaponAnimationStyle Style => style;
    public Transform SocketFor(WeaponAnimationStyle value)
    {
        return visual != null ? visual.GetWeaponMount(value) : null;
    }
    private void Awake()
    {
        if (visual == null || Animator == null || !Animator.isHuman || !visual.HasWeaponSocket)
            Debug.LogError(name + ": assign a Humanoid CharacterVisual with a weapon socket on EnemyCombatPresentation.", this);
        if (Animator != null && animationProfile != null && animationProfile.controller != null)
            Animator.runtimeAnimatorController = animationProfile.controller;
        if (animationProfile == null || animationProfile.controller == null)
            Debug.LogError(name + ": assign an EnemyAnimationProfile with a controller.", this);
    }
    private void OnEnable() { styleDirty = true; }
    private void Update() { if (styleDirty) ApplyStyle(); }
    public void SetWeaponStyle(WeaponAnimationStyle value)
    {
        if (style != value) { style = value; styleDirty = true; }
        if (styleDirty) ApplyStyle();
    }
    private void ApplyStyle()
    {
        // Restoring an inactive encounter must not call Animator setters before Unity initializes it.
        if (Animator == null || !Animator.isActiveAndEnabled || !Animator.isInitialized || animationProfile == null) return;
        Animator.SetInteger(animationProfile.weaponStyleParameter, (int)style); styleDirty = false;
    }
    public bool ReadyToFire
    {
        get
        {
            if (Animator == null || animationProfile == null || !Animator.isActiveAndEnabled || Animator.speed <= 0 || Animator.IsInTransition(0)) return false;
            string state = style == WeaponAnimationStyle.Rifle ? animationProfile.rifleReadyState : animationProfile.pistolReadyState;
            return style != WeaponAnimationStyle.Unarmed && Animator.GetCurrentAnimatorStateInfo(0).fullPathHash == UnityEngine.Animator.StringToHash(state);
        }
    }
    public void ShowFire()
    {
        if (Animator != null && animationProfile != null && !string.IsNullOrEmpty(animationProfile.fireTrigger))
            Animator.SetTrigger(animationProfile.fireTrigger);
    }
    public static Animator Resolve(Component owner, Animator fallback)
    {
        var presentation = owner.GetComponent<EnemyCombatPresentation>();
        return presentation != null && presentation.Animator != null ? presentation.Animator : fallback;
    }
}
