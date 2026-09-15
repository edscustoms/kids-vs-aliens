using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class UISegmentedControl : MonoBehaviour
{
    [Serializable] public sealed class Option
    {
        public string id;
        public UIButton button;
    }

    [SerializeField] private Option[] options = Array.Empty<Option>();
    [SerializeField] private int selectedIndex;
    [SerializeField] private UnityEvent<int> selectionChanged = new UnityEvent<int>();
    private UnityAction[] listeners;

    public int SelectedIndex => selectedIndex;
    public string SelectedId => options.Length == 0 ? string.Empty : options[selectedIndex].id;
    public UnityEvent<int> SelectionChanged => selectionChanged;

    public void Configure(Option[] values)
    {
        Unbind();
        options = values ?? Array.Empty<Option>();
        if (isActiveAndEnabled) Bind();
        Select(selectedIndex, false);
    }

    public void Select(int index) => Select(index, true);
    public void Select(int index, bool notify)
    {
        if (options.Length == 0) { selectedIndex = 0; return; }
        index = Mathf.Clamp(index, 0, options.Length - 1);
        bool changed = selectedIndex != index;
        selectedIndex = index;
        for (int i = 0; i < options.Length; i++)
            if (options[i].button != null) options[i].button.SetSelected(i == index);
        if (changed && notify) selectionChanged.Invoke(index);
    }

    private void OnEnable() { Bind(); Select(selectedIndex, false); }
    private void OnDisable() => Unbind();
    private void Bind()
    {
        if (listeners != null) return;
        listeners = new UnityAction[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            listeners[i] = () => Select(index);
            if (options[i].button != null) options[i].button.OnClick.AddListener(listeners[i]);
        }
    }
    private void Unbind()
    {
        if (listeners == null) return;
        for (int i = 0; i < listeners.Length; i++)
            if (options[i].button != null) options[i].button.OnClick.RemoveListener(listeners[i]);
        listeners = null;
    }
}
