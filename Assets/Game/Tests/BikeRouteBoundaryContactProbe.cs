#if UNITY_EDITOR
using UnityEngine;

public sealed class BikeRouteBoundaryContactProbe : MonoBehaviour
{
    public int Contacts;
    public Collider Target;
    public int TargetContacts;
    void OnCollisionEnter(Collision hit) => Record(hit);
    void OnCollisionStay(Collision hit) => Record(hit);
    void Record(Collision hit)
    {
        if (hit.collider == Target) TargetContacts++;
        if(hit.collider.transform.parent!=null && hit.collider.transform.parent.name=="Readable boundaries") Contacts++;
    }
}

#endif
