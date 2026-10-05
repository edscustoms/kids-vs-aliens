using System;
using UnityEngine;

/// <summary>BikeRoute's two authored waves and finish state. Individual riders own their driving; existing enemies own health/combat.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(RunWorldObject))]
public sealed class BikeRouteChaseDirector : MonoBehaviour, IRunStateParticipant
{
    [Serializable]
    public struct RiderSlot
    {
        public EnemyBikeDriver rider;
        public bool frontPass;
        public float activationDelay;
    }
    [SerializeField] private PlayerBikeRider player;
    [SerializeField] private AlienBikeController playerBike;
    [SerializeField] private BikeRouteGuide guide;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private RiderSlot[] waveOne = Array.Empty<RiderSlot>();
    [SerializeField] private RiderSlot[] waveTwo = Array.Empty<RiderSlot>();
    [SerializeField] private BikeRouteDefenseGun[] guns = Array.Empty<BikeRouteDefenseGun>();
    [SerializeField, Min(.5f)] private float frontPassSpacing = 2.2f;
    [Header("Contact handoff")]
    [SerializeField, Min(.5f)] private float contactHandoffDelay = .7f;
    [Header("Fresh attempt only")]
    [SerializeField] private WeaponItemData pistol, rifle;
    [SerializeField, Min(0)] private int startingPlasma = 12;
    public int Wave { get; private set; }
    public bool Finished { get; private set; }
    public PlayerBikeRider Player => player;
    public AlienBikeController PlayerBike => playerBike;
    public BikeRouteGuide Guide => guide;
    public RiderSlot[] WaveOne => waveOne;
    public RiderSlot[] WaveTwo => waveTwo;
    public BikeRouteDefenseGun[] Guns => guns;
    public string RunStateKey => "bike-route-chase-v1";
    private EnemyBikeDriver primaryAttacker, previousAttacker;
    private EnemyBikeDriver leadBlocker, previousLead;
    public EnemyBikeDriver LeadBlocker => leadBlocker;
    private float nextContact;
    public int ContactHandoffs { get; private set; }
    public EnemyBikeDriver PrimaryAttacker
    {
        get
        {
            if (primaryAttacker != null && (!primaryAttacker.isActiveAndEnabled
                || primaryAttacker.Actor == null || !primaryAttacker.Actor.IsAlive
                || primaryAttacker.State != EnemyBikeDriver.DriveState.Pursuit)) ReleaseContact(primaryAttacker);
            return primaryAttacker;
        }
    }
    private bool loadoutGranted, continuing, finishApplied;
    private float waveAge, nextSpawnCheck, nextFrontAge;
    private readonly Collider[] clearance = new Collider[24];
    private readonly Plane[] cameraPlanes = new Plane[6];
    [Serializable] private sealed class Saved { public int wave; public bool finished, loadout; public float age, nextFrontAge; }
    private void Awake()
    {
        continuing = RunSaveService.IsRestoringScene(gameObject.scene.name);
        BindRiders(waveOne); BindRiders(waveTwo);
    }
    private void BindRiders(RiderSlot[] slots)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].rider != null) slots[i].rider.BindChase(this, i);
    }
    public bool TryReserveContact(EnemyBikeDriver rider)
    {
        if (Finished || rider == null || !rider.CanStartContact) return false;
        if (PrimaryAttacker != null) return primaryAttacker == rider;
        if (Time.time < nextContact) return false;
        if (previousAttacker != null && previousAttacker.isActiveAndEnabled && previousAttacker.Actor.IsAlive
            && previousAttacker.Band == EnemyBikeDriver.DistanceBand.Recover && previousAttacker.DistanceToPlayer < 5
            && playerBike.Speed > 3) return false;
        EnemyBikeDriver best = null;
        float bestScore = float.PositiveInfinity;
        void Consider(RiderSlot[] slots)
        {
            foreach (var slot in slots)
            {
                var candidate = slot.rider;
                if (candidate == null || !candidate.CanStartContact) continue;
                float score = candidate.DistanceToPlayer + (candidate == previousAttacker ? 100 : 0);
                if (score < bestScore) { best = candidate; bestScore = score; }
            }
        }
        Consider(waveOne); Consider(waveTwo);
        if (best != rider) return false;
        if (previousAttacker != null && previousAttacker != rider) ContactHandoffs++;
        primaryAttacker = rider;
        return true;
    }
    public void ReleaseContact(EnemyBikeDriver rider)
    {
        if (primaryAttacker != rider || rider == null) return;
        primaryAttacker = null; previousAttacker = rider;
        nextContact = Time.time + contactHandoffDelay;
    }
    // Select an overtake intention without reserving a lead that has not happened yet.
    public bool ShouldStageLead(EnemyBikeDriver rider)
    {
        if (Finished || leadBlocker != null || rider == null || !rider.CanPrepareLead) return false;
        EnemyBikeDriver best = null; float score = float.NegativeInfinity; int alive = 0;
        void Consider(RiderSlot[] slots)
        {
            foreach (var slot in slots)
            {
                var candidate = slot.rider;
                if (candidate == null || !candidate.isActiveAndEnabled || !candidate.Actor.IsAlive) continue;
                alive++;
                if (!candidate.CanPrepareLead || candidate == PrimaryAttacker) continue;
                float value = candidate.LongitudinalOffset - (candidate == previousLead ? 30 : 0);
                if (value > score) { best = candidate; score = value; }
            }
        }
        Consider(waveOne); Consider(waveTwo);
        return best == rider && (alive > 1 || rider.ContactSinceLastLead);
    }
    public bool TryReserveLead(EnemyBikeDriver rider)
    {
        if (Finished || rider == null || !rider.CanStartLead || PrimaryAttacker == rider) return false;
        if (leadBlocker != null) return leadBlocker == rider;
        EnemyBikeDriver best = null;
        float bestScore = float.PositiveInfinity;
        int alive = 0;
        void Consider(RiderSlot[] slots)
        {
            foreach (var slot in slots)
            {
                var candidate = slot.rider;
                if (candidate == null || !candidate.isActiveAndEnabled || !candidate.Actor.IsAlive) continue;
                alive++;
                if (!candidate.CanStartLead || candidate == PrimaryAttacker) continue;
                float score = candidate.DistanceToPlayer + (candidate == previousLead ? 60 : 0);
                if (score < bestScore) { best = candidate; bestScore = score; }
            }
        }
        Consider(waveOne); Consider(waveTwo);
        // A lone survivor must return to physical pressure between lead windows.
        if (best != rider || alive == 1 && !rider.ContactSinceLastLead) return false;
        leadBlocker = rider;
        return true;
    }
    public void ReleaseLead(EnemyBikeDriver rider)
    {
        if (leadBlocker != rider || rider == null) return;
        previousLead = rider; leadBlocker = null;
    }
    private void Update()
    {
        if (ActiveRunController.Instance == null || !ActiveRunController.Instance.IsReady) return;
        if (!loadoutGranted)
        {
            loadoutGranted = true;
            if (!continuing)
            {
                var inventory = player.GetComponent<PlayerInventory>();
                inventory.EnsureOwnedWeapon(pistol); inventory.EnsureOwnedWeapon(rifle);
                inventory.RestoreCapsules(startingPlasma, inventory.ArmorCapsules);
                player.GetComponent<PlayerEquipment>().EquipWeapon(pistol);
                ActiveRunController.Instance.MarkDirty();
            }
        }
        if (Finished)
        {
            if (!finishApplied) ApplyFinish();
            return;
        }
        if (Time.timeScale <= 0) return;
        if (Wave == 0 && player.IsDriving) BeginWave(1);
        if (Wave == 1 && AllDefeated(waveOne)) BeginWave(2);
        if (Wave == 0) return;
        waveAge += Time.deltaTime;
        if (Time.time < nextSpawnCheck) return;
        nextSpawnCheck = Time.time + .35f;
        foreach (var slot in Wave == 1 ? waveOne : waveTwo)
        {
            if (slot.rider == null || slot.rider.HasActivated || slot.rider.GetComponent<RunWorldObject>().IsRemoved
                || waveAge < slot.activationDelay || slot.frontPass && waveAge < nextFrontAge) continue;
            if (TryActivate(slot) && slot.frontPass)
            {
                // Hidden anchors can delay both slots past their nominal start times.
                // Keep their actual entries staggered even when that shared wait ends.
                nextFrontAge = waveAge + frontPassSpacing;
                ActiveRunController.Instance.MarkDirty();
            }
        }
    }
    private void BeginWave(int wave)
    {
        Wave = wave; waveAge = 0; nextSpawnCheck = nextFrontAge = 0;
        ActiveRunController.Instance?.MarkDirty();
    }
    private static bool AllDefeated(RiderSlot[] slots)
    {
        if (slots.Length == 0) return false;
        foreach (var slot in slots)
            if (slot.rider != null && !slot.rider.GetComponent<RunWorldObject>().IsRemoved
                && (!slot.rider.HasActivated || !slot.rider.GetComponent<EnemyHealth>().IsDead)) return false;
        return true;
    }
    private bool TryActivate(RiderSlot slot)
    {
        var progress = guide.Project(player.transform.position);
        float best = float.PositiveInfinity;
        Vector3 spawn = default; Quaternion rotation = default;
        var hull = slot.rider.GetComponent<BoxCollider>();
        if (gameplayCamera == null) return false;
        GeometryUtility.CalculateFrustumPlanes(gameplayCamera, cameraPlanes);
        foreach (var anchor in guide.spawnAnchors)
        {
            if (slot.frontPass && (progress.halfWidth < 3.5f || anchor.path != progress.path)) continue;
            var sample = guide.At(anchor.path, anchor.distance);
            float difference = sample.progress - progress.progress;
            if (slot.frontPass ? difference < 75 || difference > 350 : difference > -22 || difference < -44) continue;
            float score = Mathf.Abs(difference - (slot.frontPass ? 110 : -30));
            if (score >= best) continue;
            float lane = Mathf.Sign(slot.rider.PreferredSide) * Mathf.Min(slot.frontPass ? 2.4f : 1.7f, anchor.halfWidth - 1.05f);
            Vector3 position = anchor.position + sample.Right * lane + Vector3.up * slot.rider.GetComponent<AlienBikeController>().hoverHeight;
            if (Vector3.Distance(position, player.transform.position) < (slot.frontPass ? 55 : 20)) continue;
            Quaternion facing = Quaternion.LookRotation(anchor.forward * (slot.frontPass ? -1 : 1), Vector3.up);
            var bounds = new Bounds(position + Vector3.up, new Vector3(5, 4, 5));
            if (GeometryUtility.TestPlanesAABB(cameraPlanes, bounds)) continue;
            int count = Physics.OverlapBoxNonAlloc(position + facing * hull.center, hull.size * .5f + Vector3.one * .08f,
                clearance, facing, ~0, QueryTriggerInteraction.Ignore);
            if (count != 0) continue;
            best = score; spawn = position; rotation = facing;
        }
        if (float.IsPositiveInfinity(best)) return false;
        slot.rider.Activate(spawn, rotation, slot.frontPass);
        return true;
    }
    public void ReachFinish(PlayerBikeRider source)
    {
        if (source != player || Finished || ActiveRunController.Instance?.IsReady != true) return;
        Finished = true;
        ApplyFinish();
        ActiveRunController.Instance.MarkDirty();
    }
    private void ApplyFinish()
    {
        primaryAttacker = previousAttacker = null;
        leadBlocker = previousLead = null;
        finishApplied = true;
        foreach (var slot in waveOne) if (slot.rider != null && slot.rider.HasActivated) slot.rider.Disengage();
        foreach (var slot in waveTwo) if (slot.rider != null && slot.rider.HasActivated) slot.rider.Disengage();
        foreach (var gun in guns) gun.Activate();
    }
    public string CaptureRunState() => JsonUtility.ToJson(new Saved { wave = Wave, finished = Finished, loadout = loadoutGranted, age = waveAge, nextFrontAge = nextFrontAge });
    public void RestoreRunState(string json)
    {
        // Contact choreography is transient; neither restore pass revives a reservation.
        primaryAttacker = previousAttacker = null; nextContact = 0;
        leadBlocker = previousLead = null;
        ContactHandoffs = 0;
        var saved = JsonUtility.FromJson<Saved>(json);
        Wave = Mathf.Clamp(saved.wave, 0, 2); Finished = saved.finished; loadoutGranted = saved.loadout;
        waveAge = saved.age; nextFrontAge = saved.nextFrontAge; finishApplied = false; continuing = true; nextSpawnCheck = 0;
        // Peers have not necessarily restored yet. Apply derived finish behavior after IsReady.
    }
}
