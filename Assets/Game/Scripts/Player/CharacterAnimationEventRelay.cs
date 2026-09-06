using System;
using UnityEngine;

// Must live on the same GameObject as the Animator receiving clip events.
[DisallowMultipleComponent, RequireComponent(typeof(Animator))]
public sealed class CharacterAnimationEventRelay : MonoBehaviour
{
    public event Action<CharacterAnimationEventId, int> Marker;

    // The authored AnimationEvent supplies intParameter and its source state.
    public void OnCharacterAnimationEvent(AnimationEvent marker)
    {
        if (!isActiveAndEnabled || marker == null || !marker.isFiredByAnimator
            || !Enum.IsDefined(typeof(CharacterAnimationEventId), marker.intParameter)) return;
        Marker?.Invoke((CharacterAnimationEventId)marker.intParameter,
            marker.animatorStateInfo.fullPathHash);
    }
}
