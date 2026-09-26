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
    private PlayerHealth health;

    [Header("Stance")]
    [SerializeField, Min(.1f)] private float enterDistance = 2f;
    [SerializeField, Min(.1f)] private float exitDistance = 2.5f;
    [SerializeField, Min(0)] private float inactivityTimeout = 3f;
    [SerializeField, Min(.02f)] private float proximityInterval = .1f;
    [Header("Damage and contact safety (shape is authored in Animation Actions)")]
    [SerializeField, Min(0)] private float damage = 10f;
    [SerializeField, Min(.1f)] private float reach = 1.35f;
    [SerializeField, Min(0)] private float strikeHeight = 1f;
    [SerializeField] private LayerMask collisionMask = ~0;

    private readonly Collider[] volumeHits = new Collider[32];
    private readonly RaycastHit[] sightHits = new RaycastHit[32];
    private UnarmedCombatItemData selectedItem;
    private bool stance, nearby, waitingForImpact, buffered;
    private bool recovering, committed;
    private bool contactPending;
    private MeleeContactShape contactShape;
    private int nextAttack;
    private CharacterActionId pendingAction;
    private float lastInputTime = float.NegativeInfinity, nextProximityCheck;

    public UnarmedCombatItemData SelectedItem => selectedItem;
    public UnarmedCombatItemData DefaultCombatItem => defaultCombatItem;
    public bool IsCombatStance => stance;
    public bool IsWaitingForImpact => waitingForImpact;
    public bool HasBufferedAttack => buffered;
    public bool RequiresPlantedFeet => recovering && committed && CanAct
        && (selectedItem != null ? selectedItem : defaultCombatItem).RequiresPlantedFeet(pendingAction);
    public bool IsUnlocked => HasKnowledge(selectedItem != null ? selectedItem : defaultCombatItem);
    public bool IsEligible => isActiveAndEnabled && IsUnlocked
        && (equipment == null || equipment.EquippedWeapon == null)
        && (grenades == null || !grenades.IsGrenadeSelected);
    private bool CanAct => IsEligible && Time.timeScale > 0 && (health == null || !health.IsDead)
        && (suspension == null || !suspension.IsSuspended)
        && (input == null || input.CanProcessGameplayInput);
    public event Action StateChanged;
    public event Action<CharacterActionId> AttackRequested;

    private void Awake()
    {
        input = GetComponent<StarterAssetsInputs>();
        health = GetComponent<PlayerHealth>();
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
        if (health != null) health.OnDied += CancelCombat;
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
        if (health != null) health.OnDied -= CancelCombat;
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

    public bool SelectCombatItem(UnarmedCombatItemData item) => SelectCombatItemCore(item, false);
    public bool RestoreRunSelection(UnarmedCombatItemData item) => SelectCombatItemCore(item, true);
    private bool SelectCombatItemCore(UnarmedCombatItemData item, bool restoring)
    {
        if (!HasKnowledge(item) || (!restoring && (!isActiveAndEnabled || Time.timeScale <= 0
            || (input != null && !input.CanProcessGameplayInput)))) return false;
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
        if (waitingForImpact || buffered || recovering)
        {
            buffered = true; // Exactly one next attack, regardless of extra taps.
            return true;
        }
        return BeginAttack();
    }

    private bool BeginAttack()
    {
        var chain = (selectedItem != null ? selectedItem : defaultCombatItem)?.attackChain;
        if (chain == null || chain.Length == 0) return false;
        pendingAction = chain[nextAttack % chain.Length];
        var visual = character != null ? character.ActiveVisual : null;
        if (visual == null || visual.AnimationActions == null
            || !visual.AnimationActions.TryGetBinding(pendingAction, out var binding)
            || binding.meleeContact.radius <= 0) return false;
        contactShape = binding.meleeContact;
        waitingForImpact = true;
        if (playerAnimation == null || !playerAnimation.TryPlayAction(pendingAction, CharacterAnimationEventId.MeleeImpact))
        {
            waitingForImpact = false;
            buffered = false;
            return false; // Missing presentation must never cause invisible damage.
        }
        nextAttack = (nextAttack + 1) % chain.Length;
        recovering = committed = true;
        AttackRequested?.Invoke(pendingAction);
        return true;
    }

    private void Update()
    {
        if (!CanAct) { CancelCombat(); return; }
        if (recovering && !waitingForImpact && playerAnimation.CanChainAction(pendingAction)) committed = false;
        if (Time.time >= nextProximityCheck)
        {
            nextProximityCheck = Time.time + proximityInterval;
            nearby = HasNearbyTarget(nearby ? Mathf.Max(enterDistance, exitDistance) : enterDistance);
        }
        if (recovering && !waitingForImpact && !playerAnimation.IsActionPlaying(pendingAction))
        {
            recovering = buffered = false;
            nextAttack = 0;
        }
        bool active = nearby || Time.time - lastInputTime <= inactivityTimeout || waitingForImpact || buffered || recovering;
        if (!active) { CancelCombat(); return; }
        SetStance(true);
        if (buffered && !waitingForImpact && playerAnimation.CanChainAction(pendingAction))
        {
            buffered = false;
            BeginAttack(); // Outside the native Animator event callback.
        }
    }

    public void CancelCombat()
    {
        if (!stance && !waitingForImpact && !buffered && !recovering) return;
        waitingForImpact = buffered = nearby = recovering = committed = contactPending = false;
        nextAttack = 0;
        lastInputTime = float.NegativeInfinity;
        nextProximityCheck = 0;
        playerAnimation?.CancelAction(pendingAction);
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
        // This notification represents actual equipment selection, never temporary hiding.
        if ((equipment != null && equipment.EquippedWeapon != null) || (grenades != null && grenades.IsGrenadeSelected))
            ClearSelection();
    }
    private void HandleGrenade(bool selected) { if (selected) ClearSelection(); }
    private void HandleCharacter(CharacterVisual visual) => CancelCombat();
    private void HandleSuspension(bool suspended) { if (suspended) CancelCombat(); }
    private void HandleInterrupted(CharacterActionId action)
    {
        if ((waitingForImpact || contactPending || recovering) && action == pendingAction) CancelCombat();
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
        contactPending = true;
    }

    private void LateUpdate()
    {
        // Animation events run before the final blended pose is written. Query the actual
        // rendered limb after Animator evaluation, once, on that same player-loop frame.
        if (!contactPending) return;
        contactPending = false;
        if (!CanAct) { CancelCombat(); return; }
        if (playerAnimation == null || !playerAnimation.IsActionPlaying(pendingAction)) return;
        var visual = character != null ? character.ActiveVisual : null;
        if (visual == null || !contactShape.TryGetCenter(visual.Animator, out Vector3 center)) return;
        Physics.SyncTransforms();
        Vector3 origin = StrikeOrigin;
        int count = Physics.OverlapSphereNonAlloc(center, contactShape.radius,
            volumeHits, collisionMask, QueryTriggerInteraction.Ignore);
        if (count >= volumeHits.Length) return; // Saturated queries fail closed.
        Collider chosen = null;
        Vector3 chosenPoint = default;
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider body = volumeHits[i];
            AimTarget target = body.GetComponentInParent<AimTarget>();
            if (!ValidTarget(target) || !ValidBody(body, target)) continue;
            if (!contactShape.Touches(body, center, out Vector3 point)) continue;
            Vector3 offset = point - origin;
            Vector3 planar = Vector3.ProjectOnPlane(body.bounds.center - transform.position, Vector3.up);
            if (offset.sqrMagnitude > reach * reach || Vector3.Dot(planar.normalized, transform.forward) < .5f
                || !HasClearPath(origin, point, target)) continue;
            float score = (point-center).sqrMagnitude;
            if (score >= bestScore) continue;
            chosen = body; chosenPoint = point; bestScore = score;
        }
        if (chosen == null) return; // Air punches are valid actions.
        Vector3 direction = (chosenPoint - origin).normalized;
        var hit = new HitInfo(damage, chosenPoint, -direction, direction, gameObject);
        var receiver = CombatHitResolver.Resolve(chosen, hit);
        if (receiver == null) return;
        receiver.ReceiveHit(hit);
        AudioService.Play((selectedItem != null ? selectedItem : defaultCombatItem).impactSound, chosenPoint);
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
