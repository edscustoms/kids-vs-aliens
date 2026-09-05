using System;
using UnityEngine;

public sealed class SkillDemoPlayer : MonoBehaviour
{
    [SerializeField]
    private KnowledgeAcquiredPresenter presenter;

    [SerializeField]
    private PlayerCharacter player;

    [SerializeField]
    private KnowledgePreviewStage stage;

    [SerializeField]
    private PreviewEquipmentAdapter[] equipmentAdapters = Array.Empty<PreviewEquipmentAdapter>();
    private CharacterAnimatorDriver driver;
    private SkillTutorialData tutorial;
    private bool actionSupported;
    private float elapsed;

    private void OnEnable()
    {
        if (presenter != null)
        {
            presenter.PresentationStarted += Show;
            presenter.PresentationClosed += Clear;
        }
        if (player != null)
            player.CharacterChanged += HandleCharacterChanged;
    }

    private void OnDisable()
    {
        if (presenter != null)
        {
            presenter.PresentationStarted -= Show;
            presenter.PresentationClosed -= Clear;
        }
        if (player != null)
            player.CharacterChanged -= HandleCharacterChanged;
        Clear();
    }

    private void HandleCharacterChanged(CharacterVisual visual)
    {
        if (presenter != null && presenter.CurrentSkill != null)
            Show(presenter.CurrentSkill);
    }

    private void Show(SkillData skill)
    {
        Clear();
        if (stage == null || player == null || player.CurrentCharacterPrefab == null)
            return;
        GameObject prefab = player.CurrentCharacterPrefab.gameObject;
        if (!PreviewVisualSafety.IsVisualPrefab(prefab))
        {
            Debug.LogWarning(
                "Character preview requires a visual-only prefab. Using text acknowledgement.",
                this
            );
            return;
        }
        try
        {
            if (!stage.Prepare(prefab))
                return;
            CharacterVisual actor = stage.Actor.GetComponent<CharacterVisual>();
            if (actor == null || actor.Animator == null)
            {
                Clear();
                return;
            }
            tutorial = skill.TutorialData;
            WeaponAnimationStyle style = WeaponAnimationStyle.Unarmed;
            if (tutorial != null && tutorial.equipment != null)
            {
                foreach (PreviewEquipmentAdapter adapter in equipmentAdapters)
                    if (adapter != null && adapter.Supports(tutorial.equipment))
                    {
                        if (
                            !adapter.TryAttach(
                                tutorial.equipment,
                                actor,
                                stage.StagingRoot,
                                out style
                            )
                        )
                            Debug.LogWarning(
                                "Tutorial prop is unavailable; showing the character without it.",
                                this
                            );
                        break;
                    }
            }
            PreviewVisualSafety.ConfigureInstance(stage.Actor, stage.PreviewLayer);
            actor.Animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            actor.Animator.applyRootMotion = false;
            actor.Animator.fireEvents = false;
            actor.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            stage.ActivateActor();
            // Known optional effects may create children in Awake, even when
            // disabled. Sanitize those once before rendering; Start stays off.
            PreviewVisualSafety.ConfigureInstance(stage.Actor, stage.PreviewLayer);
            // Awake may apply fallback aura; restore this character's color.
            foreach (
                PlasmaCoreSetup core in stage.Actor.GetComponentsInChildren<PlasmaCoreSetup>(true)
            )
                core.Configure(actor.AuraColor);
            driver = new CharacterAnimatorDriver(actor.Animator, actor.AnimationActions);
            if (!driver.IsCompatible)
            {
                Clear();
                return;
            }
            driver.SetWeaponStyle(style);
            driver.SetMovement(Vector2.zero, 1f);
            actionSupported = driver.TryPlayAction(
                tutorial != null ? tutorial.action : CharacterActionId.EquippedStance
            );
            actor.Animator.Update(0f);
            stage.Show(
                tutorial != null ? tutorial.distanceMultiplier : 1.15f,
                tutorial != null ? tutorial.targetOffset : Vector3.zero
            );
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Tutorial preview unavailable: {exception.Message}", this);
            Clear();
        }
    }

    private void Update()
    {
        if (
            driver == null
            || tutorial == null
            || !tutorial.loop
            || !actionSupported
            || tutorial.action == CharacterActionId.EquippedStance
        )
            return;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed < Mathf.Max(0.25f, tutorial.loopDuration))
            return;
        elapsed = 0f;
        driver.TryPlayAction(tutorial.action);
    }

    private void Clear()
    {
        driver = null;
        tutorial = null;
        elapsed = 0f;
        actionSupported = false;
        if (stage != null)
            stage.Clear();
    }
}
