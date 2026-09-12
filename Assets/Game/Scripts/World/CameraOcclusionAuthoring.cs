using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CameraOcclusionAuthoring : MonoBehaviour
{
    private static readonly int FadeId =
        Shader.PropertyToID("_Fade");

    private readonly Dictionary<EntityId, Renderer> colliderToRenderer =
        new Dictionary<EntityId, Renderer>();

    private readonly Dictionary<Renderer, float> currentFade =
        new Dictionary<Renderer, float>();

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        BuildCache();
    }

    public void BuildCache()
    {
        colliderToRenderer.Clear();
        currentFade.Clear();

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        Collider[] colliders =
            GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (collider == null)
                continue;

            Renderer renderer = ResolveRenderer(collider);

            if (renderer == null)
                continue;

            colliderToRenderer[
                collider.GetEntityId()
            ] = renderer;

            if (!currentFade.ContainsKey(renderer))
            {
                currentFade.Add(renderer, 1f);

                ApplyFade(
                    renderer,
                    1f,
                    true
                );
            }
        }

        Debug.Log(
            $"Camera Occlusion: cached " +
            $"{colliderToRenderer.Count} colliders and " +
            $"{currentFade.Count} renderers.",
            this
        );
    }

    private Renderer ResolveRenderer(Collider collider)
    {
        if (collider == null)
            return null;

        // 1. Renderer on exact same object.
        Renderer renderer =
            collider.GetComponent<Renderer>();

        if (renderer != null)
            return renderer;

        // 2. Collider on parent, visible mesh below it.
        renderer =
            collider.GetComponentInChildren<Renderer>(true);

        if (renderer != null)
            return renderer;

        // 3. Walk upward through hierarchy.
        Transform current =
            collider.transform.parent;

        while (current != null)
        {
            renderer =
                current.GetComponent<Renderer>();

            if (renderer != null)
                return renderer;

            renderer =
                current.GetComponentInChildren<Renderer>(true);

            if (renderer != null)
                return renderer;

            // Don't escape this occlusion-authoring root.
            if (current == transform)
                break;

            current = current.parent;
        }

        return null;
    }

    public bool TryGetRenderer(
        Collider collider,
        out Renderer renderer)
    {
        renderer = null;

        if (collider == null)
            return false;

        return colliderToRenderer.TryGetValue(
            collider.GetEntityId(),
            out renderer
        );
    }

    public float GetCurrentFade(
        Renderer renderer)
    {
        if (renderer == null)
            return 1f;

        return currentFade.TryGetValue(
            renderer,
            out float fade
        )
            ? fade
            : 1f;
    }

    public void ApplyFade(
        Renderer renderer,
        float fade,
        bool force = false)
    {
        if (renderer == null)
            return;

        fade = Mathf.Clamp01(fade);

        if (!force &&
            currentFade.TryGetValue(
                renderer,
                out float previous) &&
            Mathf.Approximately(previous, fade))
        {
            return;
        }

        currentFade[renderer] = fade;

        if (propertyBlock == null)
        {
            propertyBlock =
                new MaterialPropertyBlock();
        }

        propertyBlock.Clear();

        renderer.GetPropertyBlock(
            propertyBlock
        );

        propertyBlock.SetFloat(
            FadeId,
            fade
        );

        renderer.SetPropertyBlock(
            propertyBlock
        );
    }
}