using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Owns only disposable preview objects. No player inventory, suspension, world
// validation or progression dependencies. The host supplies an unscaled clock.
public sealed class KnowledgeAbilityDemo : IDisposable
{
    public enum DemoPhase { Up, UpperPause, Down, LowerPause, Ready, Throw, Flight, DistantWait, Flash, Reset }
    public DemoPhase Phase { get; private set; }
    public int CompletedCycles { get; private set; }
    public int ReleaseCount { get; private set; }

    private readonly KnowledgePreviewStage stage;
    private readonly CharacterVisual actor;
    private readonly CharacterAnimatorDriver driver;
    private readonly SkillTutorialData data;
    private readonly Vector3 origin;
    private readonly Quaternion rotation;
    private BeamTransportVFX beam;
    private BeamHoistPath path;
    private static int nextPhysicsScene;
    private Scene physicsScene;
    private PhysicsScene physics;
    private GrenadeInstance grenade;
    private GameObject projectileVisual;
    private HeldItemGrip held;
    private CharacterAnimationEventRelay relay;
    private Light flash, keyLight;
    private float lightScale;
    private int throwState;
    private bool releasePending, disposed;
    private float elapsed, physicsTime, throwTimeout;

    public KnowledgeAbilityDemo(KnowledgePreviewStage stage, CharacterVisual actor,
        CharacterAnimatorDriver driver, SkillTutorialData data)
    {
        this.stage = stage;
        this.actor = actor;
        this.driver = driver;
        this.data = data;
        origin = actor.transform.localPosition;
        rotation = actor.transform.localRotation;
        try
        {
            lightScale = stage.StagingRoot.lossyScale.x * stage.StagingRoot.lossyScale.x;
            keyLight = CreateLight("Tutorial Ability Key Light", new Color(.7f, .85f, 1f));
            keyLight.transform.localPosition = new Vector3(-1, 2, -3);
            keyLight.intensity = 10f * lightScale;
            if (data.demoType == SkillDemoType.BeamHoist)
            {
                if (data.beamPrefab == null) throw new InvalidOperationException("Beam tutorial has no beam prefab.");
                beam = Object.Instantiate(data.beamPrefab, stage.StagingRoot);
                ConfigureVisual(beam.gameObject);
                // Fit the actual eight-metre beam mesh to this miniature stage.
                beam.transform.localScale = new Vector3(.65f, (data.beamHeight + 2f) / 8f, .65f);
                path = BeamHoistPath.Create(origin, origin + Vector3.up * data.beamHeight,
                    origin.y + data.beamHeight, 0f, data.beamTravelDuration, 0f);
                stage.FrameTravel(Vector3.up * data.beamHeight);
                BeginBeam(true);
            }
            else
            {
                if (!(data.equipment is GrenadeItemData item) || item.thrownPrefab == null)
                    throw new InvalidOperationException("Grenade tutorial requires the real thrown prefab.");
                held = actor.GetComponentInChildren<HeldItemGrip>(true);
                if (held == null) throw new InvalidOperationException("Grenade tutorial requires its held grip.");
                actor.transform.localRotation = Quaternion.Euler(0, 150, 0);
                actor.Animator.fireEvents = true;
                relay = actor.Animator.GetComponent<CharacterAnimationEventRelay>();
                if (relay == null) throw new InvalidOperationException("Grenade tutorial requires the character event relay.");
                if (!driver.TryGetMarkedAction(CharacterActionId.GrenadeThrow,
                    CharacterAnimationEventId.GrenadeRelease, out var binding, out _, out _))
                    throw new InvalidOperationException("Selected character has no authored grenade-release action.");
                throwTimeout = binding.clip.length + 1f;
                relay.Marker += OnMarker;
                // A separate physics world lets the actual launch/Rigidbody run
                // while gameplay is paused, without ever touching world colliders.
                // Unload is asynchronous: character changes can replace this loop
                // in the same frame, before the preceding scene finishes unloading.
                physicsScene = SceneManager.CreateScene("Knowledge Grenade Physics " + ++nextPhysicsScene,
                    new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                physics = physicsScene.GetPhysicsScene();
                flash = CreateLight("Tutorial Distant Electric Flash",
                    item.effectData is ElectricGrenadeEffectData electric ? electric.effectColor : Color.cyan);
                flash.transform.localPosition = origin + actor.transform.localRotation * Vector3.forward * 2.5f + Vector3.up * 1.8f;
                flash.intensity = 0;
                Phase = DemoPhase.Ready;
            }
        }
        catch { Dispose(); throw; }
    }

    public void Tick(float deltaTime)
    {
        if (disposed) return;
        if (actor == null || !stage.IsRendering) { Dispose(); return; }
        elapsed += deltaTime;
        if (data.demoType == SkillDemoType.BeamHoist) { TickBeam(); return; }
        switch (Phase)
        {
            case DemoPhase.Ready:
                if (elapsed < data.grenadeInitialDelay) break;
                if (!driver.TryGetMarkedAction(CharacterActionId.GrenadeThrow,
                    CharacterAnimationEventId.GrenadeRelease, out _, out _, out throwState))
                    break;
                driver.TryPlayAction(CharacterActionId.GrenadeThrow);
                ChangePhase(DemoPhase.Throw);
                break;
            case DemoPhase.Throw:
                if (releasePending) ReleaseGrenade();
                else if (elapsed > throwTimeout) throw new InvalidOperationException("Tutorial grenade release marker was not delivered.");
                break;
            case DemoPhase.Flight:
                // Fixed-step simulation affects this one presentation body only.
                physicsTime += Mathf.Min(deltaTime, .1f);
                while (physicsTime >= .02f) { physics.Simulate(.02f); physicsTime -= .02f; }
                projectileVisual.transform.localPosition = grenade.transform.position;
                projectileVisual.transform.localRotation = grenade.transform.rotation;
                Vector3 viewport = stage.PreviewCamera.WorldToViewportPoint(projectileVisual.transform.position);
                if (viewport.x > 1.15f || viewport.x < -.15f || viewport.y > 1.15f || viewport.z < 0)
                {
                    // Off-screen activation is represented only by reflected light;
                    // never invoke gameplay AoE or spawn a screen-filling explosion.
                    grenade.gameObject.SetActive(false);
                    projectileVisual.SetActive(false);
                    ChangePhase(DemoPhase.DistantWait);
                }
                break;
            case DemoPhase.DistantWait:
                if (elapsed >= data.grenadeDistantDelay) ChangePhase(DemoPhase.Flash);
                break;
            case DemoPhase.Flash:
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.1f, data.grenadeFlashDuration));
                flash.intensity = 40f * lightScale * Mathf.Pow(1f - t, 2f) * (.7f + .3f * Mathf.Cos(t * 35f));
                if (t >= 1f) { flash.intensity = 0; ChangePhase(DemoPhase.Reset); }
                break;
            case DemoPhase.Reset:
                if (elapsed < data.grenadeResetPause) break;
                DestroyGrenade();
                driver.CancelAction(CharacterActionId.GrenadeThrow);
                held.gameObject.SetActive(true);
                actor.transform.localPosition = origin;
                CompletedCycles++;
                ChangePhase(DemoPhase.Ready);
                break;
        }
    }

    private void TickBeam()
    {
        if (Phase == DemoPhase.UpperPause)
        {
            if (elapsed >= data.beamUpperPause) BeginBeam(false);
            return;
        }
        if (Phase == DemoPhase.LowerPause)
        {
            if (elapsed >= data.beamLowerPause) { CompletedCycles++; BeginBeam(true); }
            return;
        }
        bool up = Phase == DemoPhase.Up;
        float t = Mathf.Clamp01(elapsed / path.Duration);
        // Same Bezier evaluator as gameplay; downward traversal exists only here.
        actor.transform.localPosition = path.Evaluate(up ? t : 1f - t);
        float travelled = Mathf.Abs(actor.transform.localPosition.y - (up ? path.start.y : path.landing.y));
        float remaining = Mathf.Abs(actor.transform.localPosition.y - (up ? path.landing.y : path.start.y));
        driver.SetFloating(travelled >= .5f && remaining > .5f);
        if (t < 1f) return;
        actor.transform.localPosition = up ? path.landing : origin;
        driver.SetFloating(false);
        beam.Hide();
        ChangePhase(up ? DemoPhase.UpperPause : DemoPhase.LowerPause);
    }

    private void BeginBeam(bool up)
    {
        beam.Show(stage.StagingRoot.TransformPoint(origin), up ? BeamTransportDirection.Up : BeamTransportDirection.Down);
        ChangePhase(up ? DemoPhase.Up : DemoPhase.Down);
    }

    private void OnMarker(CharacterAnimationEventId marker, int state)
    {
        if (Phase == DemoPhase.Throw && state == throwState && marker == CharacterAnimationEventId.GrenadeRelease)
            releasePending = true;
    }

    private void ReleaseGrenade()
    {
        releasePending = false;
        var item = (GrenadeItemData)data.equipment;
        // Instantiate inactive; suppress gameplay Update/activation before activation.
        var staging = new GameObject("Tutorial Grenade Staging");
        staging.SetActive(false);
        SceneManager.MoveGameObjectToScene(staging, physicsScene);
        // Simulate in stage metres, independent of Canvas scaling. Render using
        // the same item's actual held visual projected back into the stage.
        Vector3 release = stage.StagingRoot.InverseTransformPoint(held.ReleasePosition);
        Quaternion facing = actor.transform.localRotation;
        grenade = Object.Instantiate(item.thrownPrefab, release, facing, staging.transform);
        foreach (MonoBehaviour behaviour in grenade.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        ConfigureVisual(grenade.gameObject);
        foreach (Renderer renderer in grenade.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        projectileVisual = Object.Instantiate(item.heldPrefab, stage.StagingRoot);
        projectileVisual.name = "Tutorial Projectile Visual";
        PreviewVisualSafety.ConfigureInstance(projectileVisual, stage.PreviewLayer);
        projectileVisual.transform.localPosition = release;
        projectileVisual.transform.localRotation = facing;
        grenade.GetComponent<Rigidbody>().detectCollisions = false;
        grenade.transform.SetParent(null, true);
        Object.Destroy(staging);
        grenade.gameObject.SetActive(true);
        if (!grenade.TryPrepare(item, actor.gameObject, true, Array.Empty<Collider>(), 0f)
            || !grenade.Launch((facing * Vector3.forward + Vector3.up * item.upwardBias).normalized
                * item.minThrowSpeed, new Vector3(2f, 4f, 1f)))
            throw new InvalidOperationException("Tutorial grenade launch failed.");
        held.gameObject.SetActive(false);
        physicsTime = 0;
        ReleaseCount++;
        ChangePhase(DemoPhase.Flight);
    }

    private void ChangePhase(DemoPhase phase) { Phase = phase; elapsed = 0; }

    private Light CreateLight(string name, Color color)
    {
        var root = new GameObject(name, typeof(Light));
        root.transform.SetParent(stage.StagingRoot, false);
        var light = root.GetComponent<Light>();
        light.type = LightType.Point;
        light.range = 9f * Mathf.Abs(stage.StagingRoot.lossyScale.x);
        light.color = color;
        light.shadows = LightShadows.None;
        light.cullingMask = 1 << stage.PreviewLayer;
        light.GetUniversalAdditionalLightData().renderingLayers = PreviewVisualSafety.RenderingLayer;
        return light;
    }

    private void ConfigureVisual(GameObject root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = stage.PreviewLayer;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.renderingLayerMask = PreviewVisualSafety.RenderingLayer;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (AudioSource audio in root.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
        foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>(true))
        { var main = particles.main; main.useUnscaledTime = true; }
    }

    private void DestroyGrenade()
    {
        if (grenade != null) { grenade.gameObject.SetActive(false); Object.Destroy(grenade.gameObject); }
        grenade = null;
        if (projectileVisual != null) { projectileVisual.SetActive(false); Object.Destroy(projectileVisual); }
        projectileVisual = null;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (relay != null) relay.Marker -= OnMarker;
        if (actor != null)
        {
            actor.Animator.fireEvents = false;
            driver.SetFloating(false);
            driver.CancelAction(CharacterActionId.GrenadeThrow);
            actor.transform.SetLocalPositionAndRotation(origin, rotation);
        }
        if (beam != null) { beam.Hide(); Object.Destroy(beam.gameObject); }
        if (flash != null) { flash.enabled = false; Object.Destroy(flash.gameObject); }
        if (keyLight != null) { keyLight.enabled = false; Object.Destroy(keyLight.gameObject); }
        DestroyGrenade();
        if (physicsScene.IsValid() && physicsScene.isLoaded) SceneManager.UnloadSceneAsync(physicsScene);
    }
}
