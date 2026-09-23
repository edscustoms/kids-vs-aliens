using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField]
    private float maxHealth = 100f;

    [Header("Armor")]
    [SerializeField]
    private float maxArmor = 50f;

    private float currentHealth;
    private float currentArmor;
    private bool isDead;

    public float HealthNormalized =>
        currentHealth / maxHealth;

    public float ArmorNormalized =>
        maxArmor > 0f
            ? currentArmor / maxArmor
            : 0f;

    public event Action OnHealthChanged;
    public event Action OnDied;
    public float CurrentHealth => currentHealth;
    public float CurrentArmor => currentArmor;
    public bool IsDead => isDead;
    public void RestoreRunHealth(float health, float armor)
    {
        currentHealth = Mathf.Clamp(health, 0, maxHealth); currentArmor = Mathf.Clamp(armor, 0, maxArmor);
        isDead = currentHealth <= 0; OnHealthChanged?.Invoke();
    }

    private void Awake()
    {
        currentHealth =
            maxHealth;

        currentArmor =
            maxArmor;
    }

    public void ReceiveDamage(
        HitInfo hit
    )
    {
        TakeDamage(
            hit.Damage
        );
    }

    // Kept for existing direct callers such as current enemy/practice
    // target code. New generic damage sources can use IDamageable.
    public void TakeDamage(
        float damage
    )
    {
        if (
            isDead
            || damage <= 0f
        )
        {
            return;
        }

        float previousHealth = currentHealth;
        float previousArmor = currentArmor;

        float armorDamage =
            Mathf.Min(
                currentArmor,
                damage
            );

        currentArmor -=
            armorDamage;

        damage -=
            armorDamage;

        currentHealth -=
            damage;

        currentHealth =
            Mathf.Max(
                currentHealth,
                0f
            );

        if (currentHealth < previousHealth || currentArmor < previousArmor)
            HapticService.PlayerDamaged();

        OnHealthChanged?.Invoke();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        OnDied?.Invoke();
    }
}
