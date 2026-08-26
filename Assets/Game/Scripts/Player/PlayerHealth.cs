using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Armor")]
    [SerializeField] private float maxArmor = 50f;

    private float currentHealth;
    private float currentArmor;
    private bool isDead;

    public float HealthNormalized => currentHealth / maxHealth;
    public float ArmorNormalized => maxArmor > 0f ? currentArmor / maxArmor : 0f;

    public event Action OnHealthChanged;

    private void Awake()
    {
        currentHealth = maxHealth;
        currentArmor = maxArmor;
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        // Armor absorbs damage first
        float armorDamage = Mathf.Min(currentArmor, damage);

        currentArmor -= armorDamage;
        damage -= armorDamage;

        // Remaining damage hits health
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0f);

        OnHealthChanged?.Invoke();

        if (currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        isDead = true;
    }
}