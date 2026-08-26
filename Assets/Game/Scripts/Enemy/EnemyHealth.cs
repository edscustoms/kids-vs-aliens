using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 30f;

    private float currentHealth;

    public float HealthNormalized => currentHealth / maxHealth;

    public event Action OnHealthChanged;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0f);

        OnHealthChanged?.Invoke();

        if (currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        Destroy(gameObject);
    }
}