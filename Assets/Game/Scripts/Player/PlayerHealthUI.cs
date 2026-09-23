using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;

    [SerializeField] private Slider armorBar;
    [SerializeField] private Slider healthBar;

    public void HideLegacyBars()
    {
        if (armorBar != null) armorBar.gameObject.SetActive(false);
        if (healthBar != null) healthBar.gameObject.SetActive(false);
    }

    private void Start()
    {
        playerHealth.OnHealthChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= Refresh;
    }

    private void Refresh()
    {
        armorBar.value = playerHealth.ArmorNormalized;
        healthBar.value = playerHealth.HealthNormalized;
    }
}