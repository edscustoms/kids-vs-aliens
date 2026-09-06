using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>
/// Handles enemy death presentation only.
///
/// EnemyHealth owns health/death state.
/// EnemyBrain notices IsAlive == false and stops thinking/moving.
/// This component handles:
/// death animation -> corpse hold -> fade -> destroy.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class EnemyDeathSequence : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private EnemyHealth health;

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private NavMeshAgent navMeshAgent;

    [Header("Animator")]
    [SerializeField]
    private string deathTrigger = "Die";

    [SerializeField]
    private string deathStateName = "Death";

    [Tooltip(
        "Triggers that are allowed while alive but must never survive into death."
    )]
    [SerializeField]
    private string[] interruptibleTriggers =
    {
        "Hit",
        "MeleeAttack"
    };

    [Tooltip(
        "Safety timeout while waiting for the Animator to enter the Death state."
    )]
    [SerializeField, Min(0.1f)]
    private float deathStateEnterTimeout = 1.5f;

    [Header("Corpse")]
    [SerializeField, Min(0f)]
    private float corpseHoldDuration = 3f;

    [SerializeField, Min(0.05f)]
    private float fadeDuration = 1.5f;

    [Header("Collision / UI")]
    [SerializeField]
    private bool disableCollidersOnDeath = true;

    [SerializeField]
    private bool hideWorldSpaceCanvasesOnDeath = true;

    private bool sequenceStarted;

    private readonly List<MaterialFadeState>
        fadeMaterials = new();

    private sealed class MaterialFadeState
    {
        public Material Material;
        public string ColorProperty;
        public Color OriginalColor;

        public bool HasEmission;
        public Color OriginalEmission;
    }

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnDied += OnDied;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDied -= OnDied;
    }

    private void OnDied()
    {
        if (sequenceStarted)
            return;

        sequenceStarted = true;

        StartCoroutine(
            RunDeathSequence());
    }

    private IEnumerator RunDeathSequence()
    {
        // EnemyBrain also stops when EnemyActor.IsAlive becomes false.
        // Disabling the NavMeshAgent here prevents any remaining steering
        // or avoidance from nudging the corpse during the animation.
        if (navMeshAgent != null &&
            navMeshAgent.enabled)
        {
            if (navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.isStopped = true;
                navMeshAgent.ResetPath();
            }

            navMeshAgent.enabled = false;
        }

        if (disableCollidersOnDeath)
            DisableGameplayColliders();

        if (hideWorldSpaceCanvasesOnDeath)
            HideWorldSpaceCanvases();

        if (animator != null &&
            !string.IsNullOrWhiteSpace(deathTrigger))
        {
            // Unity Animator triggers are not a true queue, but a trigger can
            // remain pending until a matching transition consumes it.
            //
            // Example:
            // Hit is triggered -> Die happens immediately afterwards ->
            // Death starts -> old pending Hit is still consumed later ->
            // corpse pops back into Hit/locomotion.
            //
            // Death is terminal, so clear every interruptible alive-state
            // trigger BEFORE requesting Death.
            ClearInterruptibleAnimatorTriggers();

            animator.ResetTrigger(
                deathTrigger);

            animator.SetTrigger(
                deathTrigger);

            yield return
                WaitForDeathAnimation();
        }

        // Stay visibly dead on the floor for a few seconds.
        if (corpseHoldDuration > 0f)
        {
            yield return
                new WaitForSeconds(
                    corpseHoldDuration);
        }

        PrepareFadeMaterials();

        if (fadeMaterials.Count > 0)
        {
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed +=
                    Time.deltaTime;

                float t =
                    Mathf.Clamp01(
                        elapsed /
                        fadeDuration);

                ApplyFade(
                    1f - t);

                yield return null;
            }
        }

        Destroy(
            gameObject);
    }

    private void ClearInterruptibleAnimatorTriggers()
    {
        if (animator == null ||
            interruptibleTriggers == null)
        {
            return;
        }

        foreach (string triggerName
                 in interruptibleTriggers)
        {
            if (string.IsNullOrWhiteSpace(
                    triggerName))
            {
                continue;
            }

            animator.ResetTrigger(
                triggerName);
        }
    }

    private IEnumerator WaitForDeathAnimation()
    {
        int deathStateHash =
            Animator.StringToHash(
                deathStateName);

        float enterElapsed = 0f;

        // Wait until the trigger actually transitions into Death.
        while (enterElapsed <
               deathStateEnterTimeout)
        {
            AnimatorStateInfo state =
                animator.GetCurrentAnimatorStateInfo(0);

            if (state.shortNameHash ==
                deathStateHash)
            {
                break;
            }

            enterElapsed +=
                Time.deltaTime;

            yield return null;
        }

        // Once in Death, wait for one full playthrough.
        while (true)
        {
            AnimatorStateInfo state =
                animator.GetCurrentAnimatorStateInfo(0);

            if (state.shortNameHash !=
                deathStateHash)
            {
                yield break;
            }

            if (!animator.IsInTransition(0) &&
                state.normalizedTime >= 1f)
            {
                yield break;
            }

            yield return null;
        }
    }

    private void DisableGameplayColliders()
    {
        Collider[] colliders =
            GetComponentsInChildren<
                Collider>(true);

        foreach (Collider collider
                 in colliders)
        {
            if (collider != null)
                collider.enabled = false;
        }
    }

    private void HideWorldSpaceCanvases()
    {
        Canvas[] canvases =
            GetComponentsInChildren<
                Canvas>(true);

        foreach (Canvas canvas
                 in canvases)
        {
            if (canvas != null &&
                canvas.renderMode ==
                RenderMode.WorldSpace)
            {
                canvas.gameObject.SetActive(
                    false);
            }
        }
    }

    private void PrepareFadeMaterials()
    {
        fadeMaterials.Clear();

        Renderer[] renderers =
            GetComponentsInChildren<
                Renderer>(true);

        foreach (Renderer renderer
                 in renderers)
        {
            if (renderer == null)
                continue;

            // renderer.materials creates per-enemy runtime instances.
            // That is intentional: only the dying enemy should fade.
            Material[] materials =
                renderer.materials;

            foreach (Material material
                     in materials)
            {
                if (material == null)
                    continue;

                string colorProperty = null;

                if (material.HasProperty(
                        "_BaseColor"))
                {
                    colorProperty =
                        "_BaseColor";
                }
                else if (material.HasProperty(
                             "_Color"))
                {
                    colorProperty =
                        "_Color";
                }

                if (colorProperty == null)
                    continue;

                ConfigureMaterialForFade(
                    material);

                MaterialFadeState state =
                    new MaterialFadeState
                    {
                        Material =
                            material,

                        ColorProperty =
                            colorProperty,

                        OriginalColor =
                            material.GetColor(
                                colorProperty),

                        HasEmission =
                            material.HasProperty(
                                "_EmissionColor")
                    };

                if (state.HasEmission)
                {
                    state.OriginalEmission =
                        material.GetColor(
                            "_EmissionColor");
                }

                fadeMaterials.Add(
                    state);
            }
        }
    }

    private static void ConfigureMaterialForFade(
        Material material)
    {
        // URP Lit / compatible shaders.
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat(
                "_SrcBlend",
                (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat(
                "_DstBlend",
                (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        material.SetOverrideTag(
            "RenderType",
            "Transparent");

        material.EnableKeyword(
            "_SURFACE_TYPE_TRANSPARENT");

        material.DisableKeyword(
            "_ALPHATEST_ON");

        material.renderQueue =
            (int)RenderQueue.Transparent;
    }

    private void ApplyFade(
        float alpha)
    {
        alpha =
            Mathf.Clamp01(alpha);

        foreach (MaterialFadeState state
                 in fadeMaterials)
        {
            if (state.Material == null)
                continue;

            Color color =
                state.OriginalColor;

            color.a *= alpha;

            state.Material.SetColor(
                state.ColorProperty,
                color);

            if (state.HasEmission)
            {
                state.Material.SetColor(
                    "_EmissionColor",
                    state.OriginalEmission *
                    alpha);
            }
        }
    }

    private void CacheReferences()
    {
        if (health == null)
            health =
                GetComponent<EnemyHealth>();

        if (animator == null)
            animator =
                GetComponentInChildren<
                    Animator>(true);

        if (navMeshAgent == null)
            navMeshAgent =
                GetComponent<NavMeshAgent>();
    }
}
