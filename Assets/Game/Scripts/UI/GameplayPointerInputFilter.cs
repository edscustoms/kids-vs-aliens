using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Input/UI boundary: query the new presentation canvas at the actual press
// position, even before EventSystem.Update has refreshed pointer-over state.
public sealed class GameplayPointerInputFilter : MonoBehaviour
{
    [SerializeField]
    private GraphicRaycaster[] blockingCanvases = System.Array.Empty<GraphicRaycaster>();
    private readonly List<RaycastResult> hits = new(8);
    private PointerEventData pointer;
    private EventSystem eventSystem;

    public bool BlocksPrimaryPress()
    {
        if (Mouse.current == null || EventSystem.current == null)
            return false;
        if (eventSystem != EventSystem.current)
        {
            eventSystem = EventSystem.current;
            pointer = new PointerEventData(eventSystem);
        }
        pointer.position = Mouse.current.position.ReadValue();
        hits.Clear();
        foreach (GraphicRaycaster canvas in blockingCanvases)
        {
            if (canvas != null && canvas.isActiveAndEnabled)
                canvas.Raycast(pointer, hits);
            if (hits.Count != 0)
            {
                hits.Clear();
                return true;
            }
        }
        return false;
    }
}
