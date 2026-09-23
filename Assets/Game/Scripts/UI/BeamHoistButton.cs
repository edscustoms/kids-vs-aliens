using StarterAssets;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public sealed class BeamHoistButton : MonoBehaviour
{
    [SerializeField] private BeamHoistAbility ability;
    [SerializeField] private StarterAssetsInputs input;
    private Button button;
    private TMP_Text label;
    private void Awake()
    {
        button = GetComponent<Button>();
        label = GetComponentInChildren<TMP_Text>();
    }
    private void OnEnable()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(Activate);
    }
    private void OnDisable() { if (button != null) button.onClick.RemoveListener(Activate); }
    private void Activate() { if (input != null) input.HoistInput(); }
    private void Update()
    {
        bool available = ability != null && ability.HasNearbyTarget;
        button.interactable = available;
        if (button.image != null) button.image.enabled = available;
        if (label != null) label.enabled = available;
    }
}
