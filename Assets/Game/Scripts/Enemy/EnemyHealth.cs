using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField]
    private float maxHealth = 30f;

    private float currentHealth;

    public float HealthNormalized =>
        currentHealth / maxHealth;

    public event Action OnHealthChanged;

    private void Awake()
    {
        currentHealth =
            maxHealth;
    }

    public void ReceiveDamage(
        HitInfo hit
    )
    {
        TakeDamage(
            hit.Damage
        );
    }

    // Kept as a convenience/backward-compatible API for systems that
    // already hold a direct EnemyHealth reference.
    public void TakeDamage(
        float damage
    )
    {
        if (damage <= 0f)
            return;

        currentHealth -=
            damage;

        currentHealth =
            Mathf.Max(
                currentHealth,
                0f
            );

        OnHealthChanged?.Invoke();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        Destroy(
            gameObject
        );
    }
}
