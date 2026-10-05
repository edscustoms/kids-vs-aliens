using System;
using UnityEngine;

// Must live on the same GameObject as the Animator receiving clip events.
[DisallowMultipleComponent, RequireComponent(typeof(Animator))]
public sealed class CharacterAnimationEventRelay : MonoBehaviour
{
    public event Action<CharacterAnimationEventId, int> Marker;
    // Unity sends IK callbacks to the Animator object; PlayerAnimation remains the pose owner.
    public event Action<int> AnimatorIK;
    private void OnAnimatorIK(int layerIndex) => AnimatorIK?.Invoke(layerIndex);

    // The authored AnimationEvent supplies intParameter and its source state.
    public void OnCharacterAnimationEvent(AnimationEvent marker)
    {
        if (!isActiveAndEnabled || marker == null || !marker.isFiredByAnimator
            || !Enum.IsDefined(typeof(CharacterAnimationEventId), marker.intParameter)) return;
        Marker?.Invoke((CharacterAnimationEventId)marker.intParameter,
            marker.animatorStateInfo.fullPathHash);
    }
}
