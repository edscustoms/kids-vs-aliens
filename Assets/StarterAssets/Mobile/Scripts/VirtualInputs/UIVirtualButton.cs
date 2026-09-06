using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class UIVirtualButton :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerClickHandler,
    IDragHandler
{
    [System.Serializable]
    public class BoolEvent : UnityEvent<bool> { }
    [System.Serializable]
    public class Event : UnityEvent { }

    [Header("Output")]
    public BoolEvent buttonStateOutputEvent =
        new BoolEvent();

    public Event buttonClickOutputEvent =
        new Event();

    [Header("Optional Drag Cancellation")]
    [Tooltip("When enabled, dragging the active pointer outside Hold Area sends cancel instead of release.")]
    public bool cancelWhenDraggedOutside;

    [Tooltip("Defaults to this button's RectTransform when unassigned.")]
    public RectTransform holdArea;

    public Event buttonCancelOutputEvent =
        new Event();

    private const int NoPointer = int.MinValue;

    private int activePointerId = NoPointer;
    private bool pointerDown;
    private bool wasCanceled;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (pointerDown)
            return;

        activePointerId = eventData.pointerId;
        pointerDown = true;
        wasCanceled = false;

        OutputButtonStateValue(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pointerDown ||
            eventData.pointerId != activePointerId)
        {
            return;
        }

        if (cancelWhenDraggedOutside &&
            !wasCanceled)
        {
            RectTransform activeArea =
                holdArea != null
                    ? holdArea
                    : transform as RectTransform;

            if (activeArea != null &&
                !RectTransformUtility.RectangleContainsScreenPoint(
                    activeArea,
                    eventData.position,
                    eventData.pressEventCamera))
            {
                CancelCurrentPress();
            }
        }

        if (!wasCanceled)
        {
            OutputButtonStateValue(false);
        }

        pointerDown = false;
        activePointerId = NoPointer;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!wasCanceled)
        {
            OutputButtonClickEvent();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!cancelWhenDraggedOutside ||
            !pointerDown ||
            wasCanceled ||
            eventData.pointerId != activePointerId)
        {
            return;
        }

        RectTransform activeArea =
            holdArea != null
                ? holdArea
                : transform as RectTransform;

        if (activeArea == null ||
            RectTransformUtility.RectangleContainsScreenPoint(
                activeArea,
                eventData.position,
                eventData.pressEventCamera))
        {
            return;
        }

        CancelCurrentPress();
    }

    private void OnDisable()
    {
        if (!pointerDown)
            return;

        if (cancelWhenDraggedOutside &&
            !wasCanceled)
        {
            CancelCurrentPress();
        }
        else if (!cancelWhenDraggedOutside)
        {
            OutputButtonStateValue(false);
        }

        pointerDown = false;
        activePointerId = NoPointer;
    }

    private void CancelCurrentPress()
    {
        if (wasCanceled)
            return;

        wasCanceled = true;
        buttonCancelOutputEvent.Invoke();
    }

    private void OutputButtonStateValue(bool buttonState)
    {
        buttonStateOutputEvent.Invoke(buttonState);
    }

    private void OutputButtonClickEvent()
    {
        buttonClickOutputEvent.Invoke();
    }

}
