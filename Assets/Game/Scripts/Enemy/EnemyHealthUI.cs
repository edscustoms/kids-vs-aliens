using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthUI : MonoBehaviour
{
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private Slider healthBar;

    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;

        enemyHealth.OnHealthChanged += Refresh;
        Refresh();
    }

    private void LateUpdate()
    {
        if (mainCamera == null)
            return;

        transform.rotation = mainCamera.transform.rotation;
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
            enemyHealth.OnHealthChanged -= Refresh;
    }

    private void Refresh()
    {
        healthBar.value = enemyHealth.HealthNormalized;
    }
}