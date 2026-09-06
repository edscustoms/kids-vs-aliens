using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField]
    private float maxHealth = 30f;

    private float currentHealth;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    public float HealthNormalized =>
        maxHealth > 0f
            ? currentHealth / maxHealth
            : 0f;

    public bool IsDead => isDead;

    public event Action OnHealthChanged;
    public event Action OnDamaged;
    public event Action OnDied;

    private void Awake()
    {
        currentHealth =
            Mathf.Max(0f, maxHealth);

        isDead = false;
    }

    private void Start()
    {
        // Initial UI sync.
        // Awake initializes health before other components start, then Start
        // broadcasts the real initial value so a health bar cannot remain at
        // whatever Slider value happened to be serialized in the prefab.
        OnHealthChanged?.Invoke();
    }

    public void ReceiveDamage(
        HitInfo hit)
    {
        TakeDamage(
            hit.Damage);
    }

    public void TakeDamage(
        float damage)
    {
        if (isDead ||
            damage <= 0f)
        {
            return;
        }

        currentHealth -=
            damage;

        currentHealth =
            Mathf.Max(
                currentHealth,
                0f);

        OnHealthChanged?.Invoke();

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        OnDamaged?.Invoke();
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        OnDied?.Invoke();
    }
}
