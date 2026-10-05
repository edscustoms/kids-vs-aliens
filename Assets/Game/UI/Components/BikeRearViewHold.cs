using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Pointer-owned look-back hold. Leaving/canceling never leaves a latched camera.</summary>
public sealed class BikeRearViewHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IPointerExitHandler, ICancelHandler
{
    private GameplayCameraController cameraOwner;
    private int? pointer;
    public void Bind(GameplayCameraController owner)
    {
        if (cameraOwner == owner) return;
        Release(); cameraOwner = owner;
    }
    public void OnPointerDown(PointerEventData data)
    {
        if (pointer.HasValue || data.button != PointerEventData.InputButton.Left) return;
        pointer = data.pointerId; cameraOwner?.SetRearViewHeld(true);
    }
    public void OnPointerUp(PointerEventData data) { if (pointer == data.pointerId) Release(); }
    public void OnPointerExit(PointerEventData data) { if (pointer == data.pointerId) Release(); }
    public void OnCancel(BaseEventData data) => Release();
    private void OnDisable() => Release();
    private void OnApplicationFocus(bool focused) { if (!focused) Release(); }
    private void OnApplicationPause(bool paused) { if (paused) Release(); }
    private void Release() { pointer = null; cameraOwner?.SetRearViewHeld(false); }
}
