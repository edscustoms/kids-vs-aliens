using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

public enum BikeRidePhase
{
    OnFoot, Approaching, Mounting, Riding, Dismounting
}

/// <summary>Player-side control/animation ownership; the bike Rigidbody alone moves the mounted rider.</summary>
[DefaultExecutionOrder(-20), DisallowMultipleComponent]
public sealed class PlayerBikeRider : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float walkSpeed = 2.1f;
    [SerializeField, Min(.1f)] private float transitionDuration = .6f;
    [SerializeField] private RuntimeAnimatorController riderAnimation;
    private CharacterController capsule;
    private ThirdPersonController locomotion;
    private StarterAssetsInputs input;
    private PlayerAnimation animationOwner;
    private PlayerEquipment equipment;
    private PlayerInventory inventory;
    private PlayerHealth health;
    private GameplaySuspensionController suspension;
    private ActiveRunController run;
    private GameplaySuspensionController.Lease transitionLease, rideLease;
    private Transform savePoint;
    private bool capsuleWasEnabled, weaponWasVisible;
    private Vector3 transitionStart, exit, walkTarget;
    private Quaternion transitionRotation;
    private float elapsed, verticalSpeed;
    // The small authored perimeter is a walking route around this hull, not navigation data.
    private readonly Vector3[] approachFeet = new Vector3[AlienBikeController.MaxMountApproaches];
    private readonly Vector3[] mountFeet = new Vector3[AlienBikeController.MaxMountApproaches];
    private readonly bool[] approachClear = new bool[AlienBikeController.MaxMountApproaches];
    private readonly bool[] entryClear = new bool[AlienBikeController.MaxMountApproaches];
    private readonly bool[] edgeClear = new bool[AlienBikeController.MaxMountApproaches];
    private readonly bool[] mountClear = new bool[AlienBikeController.MaxMountApproaches];
    private readonly Vector3[] walkPoints = new Vector3[AlienBikeController.MaxMountApproaches + 1];
    private int walkPointCount, walkPointIndex;
    public Transform SelectedMountPoint { get; private set; }
    private float nextNearbyCheck;
    private readonly Collider[] overlaps = new Collider[32];
    private readonly RaycastHit[] hits = new RaycastHit[32];
    public AlienBikeController Bike
    {
        get; private set;
    }
    public BikeRidePhase Phase
    {
        get; private set;
    }
    public bool IsDriving => Phase == BikeRidePhase.Riding;
    public bool CanShootMounted => IsDriving && Bike != null && health != null && !health.IsDead
        && suspension != null && !suspension.BlocksControls && input != null && input.CanProcessBikeControls
        && equipment != null && equipment.EquippedWeapon != null
        && inventory != null && inventory.SelectedItem == equipment.EquippedWeapon
        && equipment.EquippedWeapon.animationStyle == WeaponAnimationStyle.Pistol
        && equipment.EquippedWeapon.fireMode == WeaponFireMode.SemiAuto;
    public Vector3 MountedForward => Bike != null ? Bike.transform.forward : transform.forward;
    public bool IsBusy => Phase != BikeRidePhase.OnFoot;
    // The disabled walking capsule is intentional. Combat uses the occupied hull
    // during authored seating/dismount, without changing Amy's target identity.
    public AlienBikeController OccupiedBike => Bike != null && capsule != null && !capsule.enabled ? Bike : null;
    public Vector3 MountedAimPoint => transform.position + Vector3.up * .65f;
    public static bool IsOccupiedTargetCollider(Transform target, Collider collider)
    {
        var rider = target != null ? target.GetComponent<PlayerBikeRider>() : null;
        var occupied = rider != null ? rider.OccupiedBike : null;
        return occupied != null && collider != null && collider.transform.IsChildOf(occupied.transform);
    }
    public bool TryGetMountedTargetSamples(Vector3[] samples)
    {
        var occupied = OccupiedBike;
        if (occupied == null) return false;
        samples[0] = MountedAimPoint;
        samples[1] = MountedAimPoint + Vector3.up * .45f;
        samples[2] = MountedAimPoint + transform.right * .25f;
        samples[3] = MountedAimPoint - transform.right * .25f;
        samples[4] = occupied.seatPoint.position;
        return true;
    }
    public float TransitionDuration => transitionDuration;
    public Vector2 DrivingInput => input != null && input.CanProcessBikeControls ? input.move : Vector2.zero;
    public AlienBikeController NearbyBike
    {
        get; private set;
    }

    private void Awake()
    {
        capsule = GetComponent<CharacterController>();
        locomotion = GetComponent<ThirdPersonController>();
        input = GetComponent<StarterAssetsInputs>();
        animationOwner = GetComponent<PlayerAnimation>();
        equipment = GetComponent<PlayerEquipment>();
        inventory = GetComponent<PlayerInventory>();
        health = GetComponent<PlayerHealth>();
        suspension = GetComponent<GameplaySuspensionController>();
        run = GetComponent<ActiveRunController>();
    }
    private void OnEnable()
    {
        if (health != null)
            health.OnDied += AbortRide;
    }
    private void OnDisable()
    {
        if (health != null)
            health.OnDied -= AbortRide;
        AbortRide();
    }
    private void Update()
    {
        if (!IsBusy)
        {
            if (suspension == null || suspension.IsSuspended || health == null || health.IsDead)
            {
                NearbyBike = null;
                return;
            }
            if (Time.unscaledTime >= nextNearbyCheck)
            {
                nextNearbyCheck = Time.unscaledTime + .15f;
                NearbyBike = null;
                float closest = float.PositiveInfinity;
                foreach (var bike in AlienBikeController.Available)
                {
                    if (bike == null || bike.gameObject.scene != gameObject.scene || !bike.CanMount)
                        continue;
                    float d = Vector3.ProjectOnPlane(bike.transform.position - transform.position, Vector3.up).sqrMagnitude;
                    if (bike.IsWithinInteractionRange(transform.position) && d < closest && TryPlanMount(bike, out _, out _))
                    {
                        closest = d;
                        NearbyBike = bike;
                    }
                }
            }
            if (NearbyBike != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                UseBike();
            return;
        }
        if (Bike == null || !Bike.isActiveAndEnabled || health.IsDead || animationOwner == null
            || !animationOwner.OwnsAuthoredMotion(this) || (rideLease?.IsActive != true && transitionLease?.IsActive != true))
        {
            AbortRide();
            return;
        }
        RefreshSavePoint();
        if (suspension.BlocksControls && IsDriving)
        {
            Bike.SetDriveInput(this, Vector2.zero);
            Bike.CancelCharge();
            return;
        }
        if (Time.deltaTime <= 0)
            return;
        elapsed += Time.deltaTime;
        switch (Phase)
        {
            case BikeRidePhase.Approaching:
                WalkToBike();
                break;
            case BikeRidePhase.Mounting:
                if (MoveTransition(Bike.seatPoint.position, Bike.seatPoint.rotation))
                {
                    Phase = BikeRidePhase.Riding;
                    rideLease = suspension.Acquire(SuspensionReason.BikeRiding);
                    transitionLease.Dispose();
                    transitionLease = null;
                    Bike.StartDriving();
                    run?.MarkDirty();
                }
                break;
            case BikeRidePhase.Riding:
                Bike.SetDriveInput(this, DrivingInput);
                Bike.TickControls(Time.deltaTime, input.jump, input.sprint);
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    UseBike();
                break;
            case BikeRidePhase.Dismounting:
                if (MoveTransition(exit, Quaternion.Euler(0, Bike.transform.eulerAngles.y, 0)))
                    ReleasePlayer();
                break;
        }
    }
    private void LateUpdate()
    {
        if (!IsDriving || Bike == null)
            return;
        transform.SetPositionAndRotation(Bike.seatPoint.position, Bike.seatPoint.rotation);
        if (input.CanProcessBikeControls)
            locomotion?.UpdateMountedCamera();
    }
    public void UseBike()
    {
        if (IsDriving)
            TryDismount();
        else if (!IsBusy && NearbyBike != null)
            TryMount(NearbyBike);
    }
    public void SetVisualLean(float degrees)
    {
        animationOwner?.SetAuthoredRiderLean(this, IsDriving ? degrees : 0);
    }
    public void RefreshSavePoint()
    {
        if (savePoint != null && Bike != null)
            savePoint.SetPositionAndRotation(Bike.SafeExit, Bike.SafePlayerRotation);
    }
    public bool TryMount(AlienBikeController bike)
    {
        if (!isActiveAndEnabled || IsBusy || bike == null || !bike.CanMount || suspension == null || suspension.IsSuspended
            || health == null || health.IsDead || capsule == null || !capsule.enabled || riderAnimation == null
            || !bike.IsWithinInteractionRange(transform.position)
            || !bike.CheckGrounded())
            return false;
        if (!TryPlanMount(bike, out var selectedMount, out exit))
            return false;
        Bike = bike;
        var point = new GameObject("BikeSafeExit").transform;
        point.SetPositionAndRotation(exit, Quaternion.Euler(0, bike.transform.eulerAngles.y, 0));
        if (run != null && !run.TryReservePlayerSavePoint(this, point))
        {
            Destroy(point.gameObject);
            Bike = null;
            return false;
        }
        if (animationOwner == null || !animationOwner.TryBeginAuthoredMotion(this))
        {
            run?.ClearPlayerSavePoint(this);
            Destroy(point.gameObject);
            Bike = null;
            return false;
        }
        savePoint = point;
        capsuleWasEnabled = capsule.enabled;
        weaponWasVisible = equipment != null && equipment.IsEquippedWeaponVisible;
        bike.RememberSafePose(exit);
        bike.Attach(this);
        transitionLease = suspension.Acquire(SuspensionReason.BikeTransition);
        equipment?.SetEquippedWeaponPresentationVisible(false);
        locomotion?.ResetMotion();
        Phase = BikeRidePhase.Approaching;
        elapsed = 0;
        verticalSpeed = 0;
        SelectedMountPoint = selectedMount;
        walkPointIndex = 0;
        walkTarget = walkPoints[0];
        NearbyBike = null;
        run?.MarkDirty();
        return true;
    }
    private bool TryPlanMount(AlienBikeController bike, out Transform selectedMount, out Vector3 dismount)
    {
        selectedMount = null;
        dismount = default;
        if (capsule == null || riderAnimation == null || !bike.CheckGrounded() || !bike.CanMount
            || !(TrySide(bike, bike.dismountLeft, out dismount) || TrySide(bike, bike.dismountRight, out dismount)))
            return false;
        int count = bike.mountApproaches.Length;
        for (int i = 0; i < count; i++)
        {
            var point = bike.mountApproaches[i];
            approachClear[i] = point.approachPoint != null && bike.TryGroundPoint(point.approachPoint.position, out approachFeet[i])
                && PoseClear(approachFeet[i], false);
            entryClear[i] = approachClear[i] && WalkClear(bike, transform.position, approachFeet[i]);
            mountClear[i] = approachClear[i] && point.mountPoint != null
                && bike.TryGroundPoint(point.mountPoint.position, out mountFeet[i])
                && WalkClear(bike, approachFeet[i], mountFeet[i]) && MountArcClear(bike, mountFeet[i]);
        }
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            edgeClear[i] = approachClear[i] && approachClear[next] && WalkClear(bike, approachFeet[i], approachFeet[next]);
        }
        // Compare direct entries and both directions around the authored ring. Queries above
        // are reused, so adding a fallback never triggers a world scan or a navigation search.
        float best = float.PositiveInfinity;
        int bestStart = -1, bestEnd = -1, bestDirection = 1;
        for (int start = 0; start < count; start++)
        {
            if (!entryClear[start])
                continue;
            for (int direction = -1; direction <= 1; direction += 2)
            {
                float distance = Vector3.Distance(transform.position, approachFeet[start]);
                int current = start;
                for (int step = 0; step < count; step++)
                {
                    float total = distance + Vector3.Distance(approachFeet[current], mountFeet[current]);
                    if (mountClear[current] && total < best)
                    {
                        best = total;
                        bestStart = start;
                        bestEnd = current;
                        bestDirection = direction;
                    }
                    int next = (current + direction + count) % count;
                    if (!edgeClear[direction == 1 ? current : next])
                        break;
                    distance += Vector3.Distance(approachFeet[current], approachFeet[next]);
                    current = next;
                }
            }
        }
        if (bestStart < 0)
            return false;
        walkPointCount = 0;
        for (int current = bestStart; ; current = (current + bestDirection + count) % count)
        {
            walkPoints[walkPointCount++] = approachFeet[current];
            if (current == bestEnd)
                break;
        }
        walkPoints[walkPointCount++] = mountFeet[bestEnd];
        selectedMount = bike.mountApproaches[bestEnd].mountPoint;
        return true;
    }
    private bool WalkClear(AlienBikeController bike, Vector3 from, Vector3 to)
    {
        if (!PoseClear(to, false))
            return false;
        // Respect the existing step allowance while rejecting walls, gaps and ledges.
        Vector3 step = Vector3.up * (capsule.stepOffset + .05f);
        if (!MotionClear(from + step, to + step, false))
            return false;
        int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / .3f));
        for (int i = 0; i <= samples; i++)
        {
            Vector3 expected = Vector3.Lerp(from, to, (float)i / samples);
            if (!bike.TryGroundPoint(expected, out var ground) || Mathf.Abs(ground.y - expected.y) > capsule.stepOffset + .05f)
                return false;
        }
        return true;
    }
    private bool MountArcClear(AlienBikeController bike, Vector3 from)
    {
        Vector3 previous = from;
        for (int i = 1; i <= 12; i++)
        {
            float t = i / 12f;
            Vector3 point = Vector3.Lerp(from, bike.seatPoint.position, Mathf.SmoothStep(0, 1, t)) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .3f);
            if (!MotionClear(previous, point, true, bike))
                return false;
            previous = point;
        }
        return true;
    }
    private void WalkToBike()
    {
        Vector3 delta = walkTarget - transform.position;
        delta.y = 0;
        verticalSpeed = capsule.isGrounded ? -2 : verticalSpeed - 15 * Time.deltaTime;
        capsule.Move(Vector3.ClampMagnitude(delta, walkSpeed * Time.deltaTime) + Vector3.up * verticalSpeed * Time.deltaTime);
        if (delta.sqrMagnitude > .0025f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
            Quaternion.LookRotation(delta), 360 * Time.deltaTime);
        if (delta.sqrMagnitude < .01f)
        {
            if (++walkPointIndex < walkPointCount)
            {
                if (!Bike.TryGroundPoint(walkPoints[walkPointIndex], out walkTarget) || !WalkClear(Bike, transform.position, walkTarget))
                {
                    AbortRide();
                    return;
                }
                elapsed = 0;
                return;
            }
            // Walk to the authored foot position, then align and hop onto the saddle.
            capsule.Move(Vector3.zero);
            capsule.enabled = false;
            BeginTransition(BikeRidePhase.Mounting);
            animationOwner.SetAuthoredRiderController(this, riderAnimation);
        }
        else if (elapsed > 4)
            AbortRide();
    }
    private void BeginTransition(BikeRidePhase phase)
    {
        animationOwner?.SetAuthoredRiderLean(this, 0);
        Phase = phase;
        elapsed = 0;
        transitionStart = transform.position;
        transitionRotation = transform.rotation;
    }
    private bool MoveTransition(Vector3 destination, Quaternion rotation)
    {
        float t = Mathf.Clamp01(elapsed / transitionDuration), eased = Mathf.SmoothStep(0, 1, t);
        Vector3 p = Vector3.Lerp(transitionStart, destination, eased) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .3f);
        if (!MotionClear(transform.position, p, true))
        {
            AbortRide();
            return false;
        }
        transform.SetPositionAndRotation(p, Quaternion.Slerp(transitionRotation, rotation, eased));
        return t >= 1;
    }
    public bool TryDismount()
    {
        if (!IsDriving || suspension.BlocksControls || Bike.Speed > Bike.safeDismountSpeed
            || !Bike.CheckGrounded() || !TryFindDismount(out exit))
            return false;
        transitionLease = suspension.Acquire(SuspensionReason.BikeTransition);
        rideLease.Dispose();
        rideLease = null;
        Bike.CancelCharge();
        Bike.Secure();
        Bike.RememberSafePose(exit);
        BeginTransition(BikeRidePhase.Dismounting);
        return true;
    }
    public bool TryFindDismount(out Vector3 position)
    {
        position = default;
        if (Bike == null)
            return false;
        return TrySide(Bike, Bike.dismountLeft, out position) || TrySide(Bike, Bike.dismountRight, out position);
    }
    private bool TrySide(AlienBikeController bike, Transform marker, out Vector3 position)
    {
        position = default;
        if (marker == null || !bike.TryGroundPoint(marker.position, out position) || !PoseClear(position, false))
            return false;
        // No wall between the saddle and the clear endpoint. The bike itself is expected beneath the rider.
        return MotionClear(bike.seatPoint.position + Vector3.up * .3f, position + Vector3.up * .3f, true, bike);
    }
    private void CapsuleAt(Vector3 root, out Vector3 a, out Vector3 b, out float radius)
    {
        Vector3 scale = transform.lossyScale;
        float fullRadius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        radius = Mathf.Max(.02f, fullRadius - Mathf.Min(capsule.skinWidth, fullRadius * .2f));
        float half = Mathf.Max(fullRadius, capsule.height * Mathf.Abs(scale.y) * .5f);
        Vector3 center = root + transform.TransformVector(capsule.center);
        a = center - Vector3.up * (half - fullRadius);
        b = center + Vector3.up * (half - fullRadius);
    }
    private bool Ignore(Collider other, bool ignoreBike, AlienBikeController bike) => other.transform.IsChildOf(transform)
        || (ignoreBike && bike != null && other.transform.IsChildOf(bike.transform));
    private bool PoseClear(Vector3 root, bool ignoreBike, AlienBikeController bike = null)
    {
        CapsuleAt(root, out var a, out var b, out float r);
        int n = Physics.OverlapCapsuleNonAlloc(a, b, r, overlaps, ~0, QueryTriggerInteraction.Ignore);
        if (n == overlaps.Length)
            return false;
        for (int i = 0; i < n; i++)
        if (!Ignore(overlaps[i], ignoreBike, bike != null ? bike : Bike))
            return false;
        return true;
    }
    private bool MotionClear(Vector3 from, Vector3 to, bool ignoreBike, AlienBikeController bike = null)
    {
        if (!PoseClear(to, ignoreBike, bike))
            return false;
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < .000001f)
            return true;
        CapsuleAt(from, out var a, out var b, out float r);
        int n = Physics.CapsuleCastNonAlloc(a, b, r, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
        if (n == hits.Length)
            return false;
        for (int i = 0; i < n; i++)
        if (!Ignore(hits[i].collider, ignoreBike, bike != null ? bike : Bike))
            return false;
        return true;
    }
    public void AbortRide()
    {
        if (!IsBusy)
            return;
        if (Bike != null)
        {
            Bike.RestoreSafePose();
            if (PoseClear(Bike.SafeExit, false))
                transform.SetPositionAndRotation(Bike.SafeExit, Bike.SafePlayerRotation);
        }
        ReleasePlayer();
    }
    private void ReleasePlayer()
    {
        animationOwner?.EndAuthoredMotion(this);
        equipment?.SetEquippedWeaponPresentationVisible(weaponWasVisible);
        if (capsule != null)
            capsule.enabled = capsuleWasEnabled;
        locomotion?.ResetMotion();
        run?.ClearPlayerSavePoint(this);
        if (savePoint != null)
            Destroy(savePoint.gameObject);
        savePoint = null;
        var old = Bike;
        Bike = null;
        SelectedMountPoint = null;
        Phase = BikeRidePhase.OnFoot;
        old?.Detach();
        transitionLease?.Dispose();
        transitionLease = null;
        rideLease?.Dispose();
        rideLease = null;
        run?.MarkDirty();
    }
}
