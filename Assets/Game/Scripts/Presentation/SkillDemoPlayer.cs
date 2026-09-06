using System;
using UnityEngine;
using UnityEngine.Rendering;

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
    private CharacterVisual actor;
    private SkillTutorialData tutorial;
    private WeaponInstance previewWeapon;
    private PlayerShooter playerShooter;

    private bool actionSupported;
    private float elapsed;

    private bool weaponFireDemoActive;
    private float weaponFireTimer;
    private int weaponShotsInBurst;

    private Vector3 weaponBaseLocalPosition;
    private float weaponRecoilElapsed = -1f;

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

            actor = stage.Actor.GetComponent<CharacterVisual>();

            if (actor == null || actor.Animator == null)
            {
                Clear();
                return;
            }

            tutorial = skill != null ? skill.TutorialData : null;
            WeaponAnimationStyle style = WeaponAnimationStyle.Unarmed;

            if (tutorial != null && tutorial.equipment != null)
            {
                foreach (PreviewEquipmentAdapter adapter in equipmentAdapters)
                {
                    if (adapter == null || !adapter.Supports(tutorial.equipment))
                        continue;

                    if (!adapter.TryAttach(tutorial.equipment, actor, stage.StagingRoot, out style))
                    {
                        Debug.LogWarning(
                            "Tutorial prop is unavailable; showing the character without it.",
                            this
                        );
                    }

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
            {
                core.Configure(actor.AuraColor);
            }

            driver = new CharacterAnimatorDriver(actor.Animator, actor.AnimationActions);

            if (!driver.IsCompatible)
            {
                Clear();
                return;
            }

            driver.SetWeaponStyle(style);
            driver.SetCombatStance(tutorial != null && tutorial.combatStance);
            driver.SetMovement(Vector2.zero, 1f);

            // Always establish the valid equipped stance first. A future real fire
            // trigger is requested per shot, but missing action clips never prevent
            // the cosmetic V1 demonstration from running.
            driver.TryPlayAction(CharacterActionId.EquippedStance);

            if (tutorial != null && tutorial.demoType == SkillDemoType.Stance)
            {
                actionSupported = driver.TryPlayAction(tutorial.action);
                if (!actionSupported)
                    driver.TryPlayAction(CharacterActionId.EquippedStance);
            }

            previewWeapon = stage.Actor.GetComponentInChildren<WeaponInstance>(true);
            playerShooter = player.GetComponent<PlayerShooter>();

            if (previewWeapon != null)
                weaponBaseLocalPosition = previewWeapon.transform.localPosition;

            ConfigureDemo();

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

    private void ConfigureDemo()
    {
        weaponFireDemoActive =
            tutorial != null
            && tutorial.demoType == SkillDemoType.WeaponFire
            && tutorial.equipment is WeaponItemData
            && previewWeapon != null
            && previewWeapon.Muzzle != null;

        if (!weaponFireDemoActive)
            return;

        weaponFireTimer = Mathf.Max(0f, tutorial.weaponInitialDelay);
        weaponShotsInBurst = 0;
    }

    private void Update()
    {
        UpdateWeaponRecoil();

        if (weaponFireDemoActive)
        {
            UpdateWeaponFireDemo();
            return;
        }

        UpdateActionLoop();
    }

    private void UpdateWeaponFireDemo()
    {
        if (tutorial == null || previewWeapon == null || previewWeapon.Muzzle == null)
        {
            weaponFireDemoActive = false;
            return;
        }

        weaponFireTimer -= Time.unscaledDeltaTime;

        if (weaponFireTimer > 0f)
            return;

        FireWeaponPreviewShot();
        weaponShotsInBurst++;

        int shotsPerBurst = Mathf.Max(1, tutorial.weaponShotsPerBurst);

        if (weaponShotsInBurst >= shotsPerBurst)
        {
            if (!tutorial.loop)
            {
                weaponFireDemoActive = false;
                return;
            }

            weaponShotsInBurst = 0;
            weaponFireTimer = Mathf.Max(0f, tutorial.weaponBurstPause);
        }
        else
        {
            weaponFireTimer = Mathf.Max(0.05f, tutorial.weaponShotInterval);
        }
    }

    private void FireWeaponPreviewShot()
    {
        if (previewWeapon == null || previewWeapon.Muzzle == null || actor == null)
            return;

        Transform muzzle = previewWeapon.Muzzle;
        Vector3 direction = muzzle.forward.normalized;

        // If/when the shared gameplay Animator gains the requested PistolFire /
        // RifleFire trigger, the same tutorial automatically starts using it.
        // Until then the equipped stance + recoil/VFX remains the graceful fallback.
        if (tutorial != null)
            driver?.TryPlayAction(tutorial.action);

        weaponRecoilElapsed = 0f;

        SpawnPreviewMuzzle(muzzle, direction);
        SpawnPreviewBolt(
            muzzle.position,
            direction,
            tutorial != null ? Mathf.Max(0.1f, tutorial.weaponBoltDistance) : 2.5f
        );
    }

    private void SpawnPreviewMuzzle(Transform muzzle, Vector3 direction)
    {
        PlasmaMuzzleVFX prefab = playerShooter != null ? playerShooter.PlasmaMuzzlePrefab : null;

        if (prefab == null)
            return;

        PlasmaMuzzleVFX effect = Instantiate(
            prefab,
            muzzle.position,
            Quaternion.LookRotation(direction),
            muzzle
        );

        ConfigureTransientPreviewVisual(effect.gameObject);
        effect.Play(actor.AuraColor, true);
    }

    private void SpawnPreviewBolt(Vector3 start, Vector3 direction, float distance)
    {
        PlasmaBoltVFX prefab = playerShooter != null ? playerShooter.PlasmaBoltPrefab : null;

        if (prefab == null || stage == null || stage.Actor == null)
            return;

        PlasmaBoltVFX bolt = Instantiate(prefab, start, Quaternion.identity, stage.Actor.transform);

        ConfigureTransientPreviewVisual(bolt.gameObject);
        bolt.Initialize(start, start + direction * distance, actor.AuraColor, null, true);
    }

    private void ConfigureTransientPreviewVisual(GameObject root)
    {
        if (root == null || stage == null)
            return;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = stage.PreviewLayer;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.renderingLayerMask = PreviewVisualSafety.RenderingLayer;
        }

        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        foreach (AudioSource audio in root.GetComponentsInChildren<AudioSource>(true))
        {
            audio.playOnAwake = false;
            audio.enabled = false;
        }

        foreach (Light light in root.GetComponentsInChildren<Light>(true))
            light.enabled = false;
    }

    private void UpdateWeaponRecoil()
    {
        if (weaponRecoilElapsed < 0f || previewWeapon == null || previewWeapon.Muzzle == null)
            return;

        float recoilDuration =
            tutorial != null ? Mathf.Max(0.01f, tutorial.weaponRecoilDuration) : 0.12f;
        float recoilDistance =
            tutorial != null ? Mathf.Max(0f, tutorial.weaponRecoilDistance) : 0.035f;

        weaponRecoilElapsed += Time.unscaledDeltaTime;

        float normalized = Mathf.Clamp01(weaponRecoilElapsed / recoilDuration);
        float pulse = Mathf.Sin(normalized * Mathf.PI);

        Transform weapon = previewWeapon.transform;
        Transform parent = weapon.parent;
        Vector3 worldKick = -previewWeapon.Muzzle.forward * (recoilDistance * pulse);
        Vector3 localKick = parent != null ? parent.InverseTransformVector(worldKick) : worldKick;

        weapon.localPosition = weaponBaseLocalPosition + localKick;

        if (normalized < 1f)
            return;

        weapon.localPosition = weaponBaseLocalPosition;
        weaponRecoilElapsed = -1f;
    }

    private void UpdateActionLoop()
    {
        if (
            driver == null
            || tutorial == null
            || tutorial.demoType != SkillDemoType.Stance
            || !tutorial.loop
            || !actionSupported
            || tutorial.action == CharacterActionId.EquippedStance
        )
        {
            return;
        }

        elapsed += Time.unscaledDeltaTime;

        if (elapsed < Mathf.Max(0.25f, tutorial.loopDuration))
            return;

        elapsed = 0f;
        driver.TryPlayAction(tutorial.action);
    }

    private void Clear()
    {
        if (previewWeapon != null)
            previewWeapon.transform.localPosition = weaponBaseLocalPosition;

        driver = null;
        actor = null;
        tutorial = null;
        previewWeapon = null;
        playerShooter = null;

        elapsed = 0f;
        actionSupported = false;

        weaponFireDemoActive = false;
        weaponFireTimer = 0f;
        weaponShotsInBurst = 0;
        weaponRecoilElapsed = -1f;

        if (stage != null)
            stage.Clear();
    }
}
