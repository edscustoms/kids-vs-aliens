using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Observes pointer/Selectable presentation only. Never invokes or replaces actions.
[DisallowMultipleComponent]
public sealed class NeonControlFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private NeonPanel surface;
    private Selectable selectable;
    private bool hover, pressed;
    public void Configure(NeonPanel value) { surface = value; selectable = GetComponent<Selectable>(); Refresh(); }
    private void Awake() => selectable = GetComponent<Selectable>();
    private void LateUpdate() => Refresh();
    private void Refresh()
    {
        if (surface != null) surface.SetInteraction(selectable != null && !selectable.IsInteractable() ? -1 : pressed ? 2 : hover ? 1 : 0);
    }
    public void OnPointerEnter(PointerEventData eventData) { hover = true; Refresh(); }
    public void OnPointerExit(PointerEventData eventData) { hover = false; Refresh(); }
    public void OnPointerDown(PointerEventData eventData) { pressed = true; Refresh(); }
    public void OnPointerUp(PointerEventData eventData) { pressed = false; Refresh(); }
    private void OnDisable() { pressed = hover = false; Refresh(); }
}
