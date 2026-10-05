using System.Collections.Generic;
using UnityEngine;

/// <summary>Physical combat contacts for the rideable wrapper only. No separate bike health.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(AlienBikeController))]
public sealed class AlienBikeImpact : MonoBehaviour, IDamageable
{
    [SerializeField, Min(0)] private float minimumImpactSpeed = 4.5f;
    [SerializeField, Min(.1f)] private float maximumDamageSpeed = 20;
    [SerializeField, Min(0)] private float minimumImpactDamage = 20;
    [SerializeField, Min(0)] private float maximumImpactDamage = 140;
    [SerializeField, Min(0)] private float horizontalKnockback = 9;
    [SerializeField, Min(0)] private float upwardKnockback = 3.8f;
    [SerializeField, Min(.1f)] private float repeatHitCooldown = .65f;

    private AlienBikeController bike;
    private Vector3 incomingVelocity;
    private readonly Dictionary<EnemyActor, float> nextImpact = new();

    private void Awake() => bike = GetComponent<AlienBikeController>();
    private void FixedUpdate() => incomingVelocity = bike.Body.linearVelocity;
    private void OnDisable() { nextImpact.Clear(); incomingVelocity = Vector3.zero; }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount == 0) return;
        TryImpact(collision.collider, incomingVelocity, collision.GetContact(0).point);
    }

    public bool TryImpact(Collider collider, Vector3 velocity, Vector3 point)
    {
        if (Time.timeScale <= 0 || bike.Rider == null || !bike.Rider.IsDriving || collider == null || collider.isTrigger)
            return false;
        var enemy = collider.GetComponentInParent<EnemyActor>();
        if (enemy == null || !enemy.IsAlive || !enemy.isActiveAndEnabled) return false;
        // Occupied enemy bikes exchange real Rigidbody impulses; do not launch their
        // navigation motor or turn incidental bike contact into the on-foot ram attack.
        if (enemy.GetComponent<AlienBikeController>() != null) return false;
        Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
        float speed = horizontal.magnitude;
        if (speed < Mathf.Max(.01f, minimumImpactSpeed) || (nextImpact.TryGetValue(enemy, out float next) && Time.time < next))
            return false;
        nextImpact[enemy] = Time.time + repeatHitCooldown;
        float strength = Mathf.InverseLerp(minimumImpactSpeed, Mathf.Max(minimumImpactSpeed + .01f, maximumDamageSpeed), speed);
        float damage = Mathf.Lerp(minimumImpactDamage, maximumImpactDamage, strength);
        Vector3 direction = horizontal / speed;
        // Start movement before damage: lethal hits may disable colliders/navigation immediately,
        // but the motor can finish the transient launch while the death owner plays its sequence.
        enemy.Motor?.ApplyExternalImpact(direction * horizontalKnockback * Mathf.Lerp(.55f, 1, strength)
            + Vector3.up * upwardKnockback * Mathf.Lerp(.65f, 1, strength), transform);
        var hit = new HitInfo(damage, point, -direction, direction, bike.Rider.gameObject);
        CombatHitResolver.Resolve(collider, hit)?.ReceiveHit(hit);
        return true;
    }

    public void ReceiveDamage(HitInfo hit)
    {
        var rider = bike.Rider;
        if (rider == null || rider.OccupiedBike != bike || hit.Instigator == null
            || hit.Instigator.GetComponentInParent<EnemyActor>() == null) return;
        rider.GetComponent<PlayerHealth>()?.ReceiveDamage(hit);
    }
}
