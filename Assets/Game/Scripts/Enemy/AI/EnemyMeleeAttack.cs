using System;
using UnityEngine;

/// <summary>One authored, cancellable melee contact per attack. Physics at contact is authoritative.</summary>
[DisallowMultipleComponent]
public sealed class EnemyMeleeAttack : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField, Min(.1f)] private float attackRange=.78f;
    [SerializeField, Min(0)] private float damage=10;
    [SerializeField] private Vector2 cooldownRange=new Vector2(.9f,1.2f);
    [SerializeField, Range(0,90)] private float startFacingTolerance=35;
    [SerializeField, Range(0,90)] private float contactFacingTolerance=60;
    [SerializeField] private LayerMask collisionMask=~0;
    [Header("Animation contact contract")]
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyMotor motor;
    [SerializeField] private string meleeAttackTrigger="MeleeAttack";
    [SerializeField] private string attackStatePath="Base Layer.MeleeAttack";
    [SerializeField] private MeleeContactShape contact=new MeleeContactShape{bone=HumanBodyBones.RightMiddleProximal,radius=.08f};

    private EnemyHealth health;
    private CharacterAnimationEventRelay relay;
    private RuntimeAnimatorController controller;
    private Collider targetBody;
    private Transform target;
    private int triggerHash,stateHash;
    private bool active,entered,armed,contactPending;
    private float nextAttackTime,requestedAt;
    private readonly RaycastHit[] sightHits=new RaycastHit[24];

    public float AttackRange=>attackRange;
    public bool IsAttacking=>active;
    // Read-only diagnostics for attributing actual damage rather than inferring it from triggers.
    public event Action<HitInfo> ContactResolved;

    private void Reset(){CacheReferences();}
    private void Awake()
    {
        CacheReferences();triggerHash=Animator.StringToHash(meleeAttackTrigger);stateHash=Animator.StringToHash(attackStatePath);
    }
    private void CacheReferences()
    {
        if(animator==null)animator=GetComponentInChildren<Animator>(true);
        animator=EnemyCombatPresentation.Resolve(this,animator);
        if(motor==null)motor=GetComponent<EnemyMotor>();
        health=GetComponent<EnemyHealth>();
        relay=animator!=null?animator.GetComponent<CharacterAnimationEventRelay>():null;
    }
    private void OnEnable()
    {
        if(relay!=null)relay.Marker+=HandleMarker;
        if(motor!=null)motor.MovementLocksChanged+=HandleLocks;
        if(health!=null)health.OnDied+=CancelAttack;
    }
    private void OnDisable()
    {
        CancelAttack();
        if(relay!=null)relay.Marker-=HandleMarker;
        if(motor!=null)motor.MovementLocksChanged-=HandleLocks;
        if(health!=null)health.OnDied-=CancelAttack;
    }
    private bool Interrupted=>!isActiveAndEnabled || (health!=null&&health.IsDead)
        || (motor!=null&&(motor.MovementLocks&~EnemyMovementLockReason.MeleeAttack)!=0);
    private bool Facing(Transform other,float degrees)
    {
        Vector3 delta=Vector3.ProjectOnPlane(other.position-transform.position,Vector3.up);
        return delta.sqrMagnitude>.0001f && Vector3.Dot(transform.forward,delta.normalized)>=Mathf.Cos(degrees*Mathf.Deg2Rad);
    }
    public bool CanAttack(Transform candidate)
    {
        if(candidate==null || !candidate.gameObject.activeInHierarchy || Interrupted)return false;
        if(active)return true; // Finish the committed animation; do not resume chase mid-punch.
        Vector3 delta=candidate.position-transform.position;delta.y=0;
        return delta.sqrMagnitude<=attackRange*attackRange;
    }
    public bool TryAttack(Transform candidate)
    {
        if(Time.timeScale<=0 || active || !CanAttack(candidate) || Time.time<nextAttackTime
            || !Facing(candidate,startFacingTolerance) || animator==null || !animator.isActiveAndEnabled
            || !animator.fireEvents || animator.speed<=0 || relay==null || !relay.isActiveAndEnabled
            || !animator.HasState(0,stateHash))return false;
        if(animator.GetCurrentAnimatorStateInfo(0).fullPathHash==stateHash || animator.IsInTransition(0))return false;
        targetBody=FindTargetCollider(candidate);
        if(targetBody==null || targetBody.GetComponentInParent<IDamageable>()==null)return false;
        target=candidate;controller=animator.runtimeAnimatorController;
        active=armed=true;entered=contactPending=false;requestedAt=Time.time;
        motor?.SetMovementLock(EnemyMovementLockReason.MeleeAttack,true);
        animator.SetTrigger(triggerHash);
        float min=Mathf.Max(.05f,Mathf.Min(cooldownRange.x,cooldownRange.y));
        nextAttackTime=Time.time+UnityEngine.Random.Range(min,Mathf.Max(min,Mathf.Max(cooldownRange.x,cooldownRange.y)));
        return true;
    }
    private bool AnimationValid=>animator!=null && animator.isActiveAndEnabled && animator.fireEvents
        && animator.speed>0 && animator.runtimeAnimatorController==controller && relay!=null && relay.isActiveAndEnabled;
    private bool InAttack=>animator.GetCurrentAnimatorStateInfo(0).fullPathHash==stateHash;
    private bool Exiting=>animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash!=stateHash;
    private void HandleMarker(CharacterAnimationEventId marker,int sourceState)
    {
        if(marker!=CharacterAnimationEventId.MeleeImpact || sourceState!=stateHash || !active || !armed)return;
        if(Interrupted || !AnimationValid || !InAttack || Exiting){CancelAttack();return;}
        armed=false;contactPending=true;
    }
    private void HandleLocks(EnemyMovementLockReason locks)
    {
        if((locks&~EnemyMovementLockReason.MeleeAttack)!=0)CancelAttack();
    }
    private void LateUpdate()
    {
        if(!active || Time.timeScale<=0)return;
        if(Interrupted || !AnimationValid){CancelAttack();return;}
        bool current=InAttack;
        bool next=animator.IsInTransition(0)&&animator.GetNextAnimatorStateInfo(0).fullPathHash==stateHash;
        if(current||next)entered=true;
        if(Exiting || (entered&&!current&&!next) || (!entered&&Time.time-requestedAt>.5f)) {CancelAttack();return;}
        if(!contactPending)return;
        contactPending=false;
        if(target==null || !target.gameObject.activeInHierarchy || !Facing(target,contactFacingTolerance)
            || !contact.TryGetCenter(animator,out Vector3 center))return;
        var playerHealth=target.GetComponent<PlayerHealth>();
        if(playerHealth!=null&&playerHealth.IsDead)return;
        Physics.SyncTransforms();
        if(!contact.Touches(targetBody,center,out Vector3 point))return;
        Vector3 origin=transform.position+Vector3.up*.9f;
        if(!MeleeContactShape.ClearPath(transform,target,origin,point,collisionMask,sightHits))return;
        Vector3 direction=(point-origin).normalized;
        var hit=new HitInfo(damage,point,-direction,direction,gameObject);
        CombatHitResolver.Resolve(targetBody,hit)?.ReceiveHit(hit);
        ContactResolved?.Invoke(hit);
    }
    public void CancelAttack()
    {
        active=armed=entered=contactPending=false;target=null;targetBody=null;
        if(animator!=null&&triggerHash!=0)animator.ResetTrigger(triggerHash);
        motor?.SetMovementLock(EnemyMovementLockReason.MeleeAttack,false);
    }
    private static Collider FindTargetCollider(Transform candidate)
    {
        Collider body=candidate.GetComponent<Collider>();
        if(body!=null&&body.enabled&&!body.isTrigger)return body;
        foreach(var child in candidate.GetComponentsInChildren<Collider>())
            if(child.enabled&&!child.isTrigger&&child.GetComponentInParent<IDamageable>()!=null)return child;
        return null;
    }
}
