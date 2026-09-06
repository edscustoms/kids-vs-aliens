using UnityEngine;
using UnityEngine.Rendering;

public static class PreviewVisualSafety
{
    // Existing scene lighting uses Default (1); reserve Light Layer 7 for the
    // isolated actor and its local preview light. Both project URP assets enable light layers.
    public const uint RenderingLayer = 1u << 7;

    public static bool IsVisualPrefab(GameObject prefab)
    {
        if (prefab == null)
            return false;
        // Check BEFORE instantiation: disabling unknown scripts afterward is
        // insufficient because Awake can already perform gameplay work.
        foreach (MonoBehaviour behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
                return false;
            if (
                behaviour is CharacterVisual
                || behaviour is CharacterAnimationEventRelay
                || behaviour is MenuPreviewSettings
                || behaviour is WeaponInstance
                || behaviour is HeldItemGrip
                || behaviour is PlasmaCoreSetup
                || behaviour is PlasmaCoreController
                || behaviour is PlasmaArcController
                || behaviour is PlasmaArc
                || behaviour is ElectricGrenadeCoreVFX
            )
                continue;
            return false;
        }
        return true;
    }

    public static void ConfigureInstance(GameObject root, int layer)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
        foreach (AudioSource audio in root.GetComponentsInChildren<AudioSource>(true))
        {
            audio.playOnAwake = false;
            audio.enabled = false;
        }
        foreach (Light light in root.GetComponentsInChildren<Light>(true))
            light.enabled = false;
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (
                behaviour is PlasmaArcController
                || behaviour is PlasmaArc
                || behaviour is ElectricGrenadeCoreVFX
            )
                behaviour.enabled = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.renderingLayerMask = RenderingLayer;
            if (
                renderer is LineRenderer
                || renderer is TrailRenderer
                || renderer is ParticleSystemRenderer
            )
                renderer.enabled = false;
        }
        foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            main.playOnAwake = false;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
