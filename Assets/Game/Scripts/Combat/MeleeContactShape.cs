using System;
using UnityEngine;

/// <summary>Authored anatomy, shared by melee presentation mappings. No root-relative reach volume.</summary>
[Serializable]
public struct MeleeContactShape
{
    public HumanBodyBones bone;
    [Tooltip("Small world-space radius around the animated knuckle or toe. Zero disables contact.")]
    [Min(0)] public float radius;

    public bool TryGetCenter(Animator animator, out Vector3 center)
    {
        center=default;
        if(radius<=0 || animator==null || !animator.isActiveAndEnabled || !animator.isHuman) return false;
        Transform joint=animator.GetBoneTransform(bone);
        if(joint==null) return false;
        center=joint.position;
        return true;
    }

    public bool Touches(Collider body, Vector3 center, out Vector3 point)
    {
        point=default;
        if(body==null || !body.enabled || body.isTrigger || !body.gameObject.activeInHierarchy) return false;
        point=body.ClosestPoint(center);
        return (point-center).sqrMagnitude<=radius*radius;
    }

    public static bool ClearPath(Transform attacker, Transform target, Vector3 origin, Vector3 point,
        LayerMask mask, RaycastHit[] buffer)
    {
        Vector3 delta=point-origin; float distance=delta.magnitude;
        if(distance<.001f) return true;
        int count=Physics.RaycastNonAlloc(origin,delta/distance,buffer,distance,mask,QueryTriggerInteraction.Ignore);
        if(count>=buffer.Length) return false;
        for(int i=0;i<count;i++)
        {
            Transform hit=buffer[i].collider.transform;
            if(hit==attacker || hit.IsChildOf(attacker) || hit==target || hit.IsChildOf(target)) continue;
            return false;
        }
        return true;
    }
}
