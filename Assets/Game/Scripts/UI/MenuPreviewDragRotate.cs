using UnityEngine;
using UnityEngine.EventSystems;

public class MenuPreviewDragRotate : MonoBehaviour, IDragHandler
{
    [SerializeField] private MenuPreviewStage previewStage;

    public void OnDrag(PointerEventData eventData)
    {
        if (previewStage == null)
            return;

        previewStage.RotateCurrent(eventData.delta.x);
    }
}
