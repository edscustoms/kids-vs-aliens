using System;
using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerMeleeController : MonoBehaviour
{
    [SerializeField] private UnarmedCombatItemData defaultCombatItem;
    [SerializeField] private PlayerAnimation playerAnimation;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private PlayerGrenadeController grenades;
    [SerializeField] private PlayerSkillState skills;
    [SerializeField] private PlayerCharacter character;
    [SerializeField] private PlayerAim aim;
    [SerializeField] private GameplaySuspensionController suspension;
    private StarterAssetsInputs input;

    [Header("Stance")]
    [SerializeField, Min(.1f)] private float enterDistance = 2f;
    [SerializeField, Min(.1f)] private float exitDistance = 2.5f;
    [SerializeField, Min(0)] private float inactivityTimeout = 3f;
    [SerializeField, Min(.02f)] private float proximityInterval = .1f;
    [Header("Impact volume (world metres)")]
    [SerializeField, Min(0)] private float damage = 10f;
    [SerializeField, Min(.1f)] private float reach = 1.35f;
    [SerializeField, Min(.01f)] private float hitRadius = .3f;
    [SerializeField, Min(0)] private float strikeHeight = 1f;
    [SerializeField] private LayerMask collisionMask = ~0;

    private readonly Collider[] volumeHits = new Collider[32];
    private readonly RaycastHit[] sightHits = new RaycastHit[32];
    private UnarmedCombatItemData selectedItem;
    private bool stance, nearby, waitingForImpact, buffered;
    private int nextAttack;
    private CharacterActionId pendingAction;
    private float lastInputTime = float.NegativeInfinity, nextProximityCheck;

    public UnarmedCombatItemData SelectedItem => selectedItem;
    public bool IsCombatStance => stance;
    public bool IsWaitingForImpact => waitingForImpact;
    public bool HasBufferedAttack => buffered;
    public bool IsUnlocked => HasKnowledge(selectedItem != null ? selectedItem : defaultCombatItem);
    public bool IsEligible => isActiveAndEnabled && IsUnlocked
        && (equipment == null || equipment.EquippedWeapon == null)
        && (grenades == null || !grenades.IsGrenadeSelected);
    private bool CanAct => IsEligible && Time.timeScale > 0
        && (suspension == null || !suspension.IsSuspended)
        && (input == null || input.CanProcessGameplayInput);
    public event Action StateChanged;
    public event Action<CharacterActionId> AttackRequested;

    private void Awake()
    {
        input = GetComponent<StarterAssetsInputs>();
        if (playerAnimation == null) playerAnimation = GetComponent<PlayerAnimation>();
        if (equipment == null) equipment = GetComponent<PlayerEquipment>();
        if (grenades == null) grenades = GetComponent<PlayerGrenadeController>();
        if (skills == null) skills = GetComponent<PlayerSkillState>();
        if (character == null) character = GetComponent<PlayerCharacter>();
        if (aim == null) aim = GetComponent<PlayerAim>();
        if (suspension == null) suspension = GetComponent<GameplaySuspensionController>();
    }

    private void OnEnable()
    {
        if (playerAnimation != null)
        {
            playerAnimation.AnimationEventReceived += HandleImpact;
            playerAnimation.ActionInterrupted += HandleInterrupted;
        }
        if (equipment != null) equipment.EquippedWeaponChanged += HandleEquipment;
        if (grenades != null) grenades.GrenadeSelectionChanged += HandleGrenade;
        if (character != null) character.CharacterChanged += HandleCharacter;
        if (suspension != null) suspension.SuspensionChanged += HandleSuspension;
    }

    private void OnDisable()
    {
        CancelCombat();
        if (playerAnimation != null)
        {
            playerAnimation.AnimationEventReceived -= HandleImpact;
            playerAnimation.ActionInterrupted -= HandleInterrupted;
        }
        if (equipment != null) equipment.EquippedWeaponChanged -= HandleEquipment;
        if (grenades != null) grenades.GrenadeSelectionChanged -= HandleGrenade;
        if (character != null) character.CharacterChanged -= HandleCharacter;
        if (suspension != null) suspension.SuspensionChanged -= HandleSuspension;
    }

    public bool SelectCombatItem(UnarmedCombatItemData item)
    {
        if (!isActiveAndEnabled || !HasKnowledge(item) || Time.timeScale <= 0
            || (input != null && !input.CanProcessGameplayInput)) return false;
        CancelCombat();
        grenades?.CancelThrow();
        equipment?.UnequipWeapon();
        selectedItem = item;
        lastInputTime = Time.time;
        SetStance(true);
        StateChanged?.Invoke();
        return true;
    }

    // Called for a deliberate FIRE press, never from held-input polling.
    public bool TryAttack()
    {
        if (!CanAct) return false;
        lastInputTime = Time.time;
        SetStance(true);
        if (waitingForImpact || buffered)
        {
            buffered = true; // Exactly one next attack, regardless of extra taps.
            return true;
        }
        return BeginAttack();
    }

    private bool BeginAttack()
    {
        pendingAction = nextAttack == 0 ? CharacterActionId.MeleeLight1 : CharacterActionId.MeleeLight2;
        waitingForImpact = true;
        if (playerAnimation == null || !playerAnimation.TryPlayAction(pendingAction, CharacterAnimationEventId.MeleeImpact))
        {
            waitingForImpact = false;
            buffered = false;
            return false; // Missing presentation must never cause invisible damage.
        }
        nextAttack ^= 1;
        AttackRequested?.Invoke(pendingAction);
        return true;
    }

    private void Update()
    {
        if (!CanAct) { CancelCombat(); return; }
        if (Time.time >= nextProximityCheck)
        {
            nextProximityCheck = Time.time + proximityInterval;
            nearby = HasNearbyTarget(nearby ? Mathf.Max(enterDistance, exitDistance) : enterDistance);
        }
        bool active = nearby || Time.time - lastInputTime <= inactivityTimeout || waitingForImpact || buffered;
        if (!active) { CancelCombat(); return; }
        SetStance(true);
        if (buffered && !waitingForImpact)
        {
            buffered = false;
            BeginAttack(); // Outside the native Animator event callback.
        }
    }

    public void CancelCombat()
    {
        if (!stance && !waitingForImpact && !buffered) return;
        waitingForImpact = buffered = nearby = false;
        nextAttack = 0;
        lastInputTime = float.NegativeInfinity;
        nextProximityCheck = 0;
        playerAnimation?.CancelAction(CharacterActionId.MeleeLight1);
        playerAnimation?.CancelAction(CharacterActionId.MeleeLight2);
        SetStance(false);
    }

    private void SetStance(bool value)
    {
        if (stance == value) return;
        stance = value;
        playerAnimation?.SetCombatStance(value);
        StateChanged?.Invoke();
    }

    private bool HasKnowledge(UnarmedCombatItemData item) => item != null && item.requiredSkill != null
        && skills != null && skills.HasSkill(item.requiredSkill);

    private void HandleEquipment(WeaponItemData presentation)
    {
        // Grenades publish null presentation without unequipping the weapon.
        if ((equipment != null && equipment.EquippedWeapon != null) || (grenades != null && grenades.IsGrenadeSelected))
            ClearSelection();
    }
    private void HandleGrenade(bool selected) { if (selected) ClearSelection(); }
    private void HandleCharacter(CharacterVisual visual) => CancelCombat();
    private void HandleSuspension(bool suspended) { if (suspended) CancelCombat(); }
    private void HandleInterrupted(CharacterActionId action)
    {
        if (waitingForImpact && action == pendingAction) CancelCombat();
    }
    private void ClearSelection()
    {
        CancelCombat();
        if (selectedItem == null) return;
        selectedItem = null;
        StateChanged?.Invoke();
    }

    private Vector3 StrikeOrigin => transform.position + Vector3.up * strikeHeight;
    private bool ValidTarget(AimTarget target) => target != null && target.IsTargetable
        && target.transform != transform && !target.transform.IsChildOf(transform);
    private bool ValidBody(Collider body, AimTarget target) => body != null && body.enabled && !body.isTrigger
        && body.gameObject.activeInHierarchy && target.OwnsCollider(body)
        && body.GetComponentInParent<IDamageable>() != null;

    private bool HasNearbyTarget(float distance)
    {
        Vector3 origin = StrikeOrigin;
        var targets = AimTarget.ActiveTargets;
        for (int i = 0; i < targets.Count; i++)
        {
            AimTarget target = targets[i];
            if (!ValidTarget(target)) continue;
            var bodies = target.BodyColliders;
            if (bodies == null) continue; // AimTarget owns and initializes its cache in Start.
            foreach (Collider body in bodies)
            {
                if (!ValidBody(body, target)) continue;
                Vector3 point = body.ClosestPoint(origin);
                if ((point - origin).sqrMagnitude <= distance * distance && HasClearPath(origin, point, target)) return true;
            }
        }
        return false;
    }

    private void HandleImpact(CharacterAnimationEventId marker)
    {
        if (marker != CharacterAnimationEventId.MeleeImpact || !waitingForImpact) return;
        waitingForImpact = false; // Consume first; reactions/events cannot reenter damage.
        if (!CanAct) { CancelCombat(); return; }
        Vector3 origin = StrikeOrigin;
        int count = Physics.OverlapCapsuleNonAlloc(origin, origin + transform.forward * reach,
            hitRadius, volumeHits, collisionMask, QueryTriggerInteraction.Ignore);
        if (count >= volumeHits.Length) return; // Saturated queries fail closed.
        Collider chosen = null;
        Vector3 chosenPoint = default;
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider body = volumeHits[i];
            AimTarget target = body.GetComponentInParent<AimTarget>();
            if (!ValidTarget(target) || !ValidBody(body, target)) continue;
            Vector3 point = body.ClosestPoint(origin);
            Vector3 offset = point - origin;
            if (offset.sqrMagnitude > reach * reach || Vector3.Dot(offset, transform.forward) < 0
                || !HasClearPath(origin, point, target)) continue;
            float score = offset.sqrMagnitude;
            if (aim != null && aim.CurrentTarget == target) score -= reach * reach;
            if (score >= bestScore) continue;
            chosen = body; chosenPoint = point; bestScore = score;
        }
        if (chosen == null) return; // Air punches are valid actions.
        Vector3 direction = (chosenPoint - origin).normalized;
        var hit = new HitInfo(damage, chosenPoint, -direction, direction, gameObject);
        CombatHitResolver.Resolve(chosen, hit)?.ReceiveHit(hit);
    }

    private bool HasClearPath(Vector3 origin, Vector3 point, AimTarget target)
    {
        Vector3 offset = point - origin;
        float distance = offset.magnitude;
        if (distance <= .001f) return true;
        int count = Physics.RaycastNonAlloc(origin, offset / distance, sightHits, distance + .01f,
            collisionMask, QueryTriggerInteraction.Ignore);
        if (count >= sightHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider body = sightHits[i].collider;
            if (body.transform == transform || body.transform.IsChildOf(transform) || target.OwnsCollider(body)) continue;
            if (sightHits[i].distance < distance) return false;
        }
        return true;
    }
}
