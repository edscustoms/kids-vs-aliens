using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class GrenadeInstance : MonoBehaviour
{
    [Header("Physics")]
    [SerializeField]
    private Rigidbody body;

    [SerializeField]
    private Collider[] grenadeColliders =
        new Collider[0];

    [Header("Effect")]
    [SerializeField]
    private GrenadeEffect effect;

    private GrenadeItemData data;
    private GameObject owner;
    private Collider[] ownerColliders;

    private bool prepared;
    private bool launched;
    private bool canActivate;
    private bool activated;
    private bool ownerCollisionsIgnored;

    private float ownerIgnoreRemaining;
    private float lifeTimer;
    private float fuseTimer;
    private float settledTimer;
    private float recoveryTimer;

    public bool IsLaunched => launched;
    public bool IsArmed => launched && canActivate;
    public bool IsActivated => activated;

    private void Awake()
    {
        CacheReferences();
    }

    public bool TryPrepare(
        GrenadeItemData grenadeData,
        GameObject source,
        bool hasRequiredKnowledge,
        Collider[] sourceColliders,
        float ownerCollisionIgnoreTime)
    {
        CacheReferences();

        if (prepared || launched ||
            grenadeData == null ||
            source == null ||
            body == null)
        {
            return false;
        }

        if (hasRequiredKnowledge &&
            (grenadeData.effectData == null ||
             effect == null ||
             !effect.Supports(
                 grenadeData.effectData)))
        {
            Debug.LogError(
                $"{name}: Armed grenade effect configuration does not match its GrenadeItemData.",
                this);

            return false;
        }

        if (!hasRequiredKnowledge &&
            grenadeData.worldPrefab == null)
        {
            Debug.LogError(
                $"{name}: An inert grenade needs a worldPrefab so it can be recovered.",
                this);

            return false;
        }

        data = grenadeData;
        owner = source;
        ownerColliders = sourceColliders;
        canActivate = hasRequiredKnowledge;
        ownerIgnoreRemaining =
            Mathf.Max(
                0f,
                ownerCollisionIgnoreTime);

        prepared = true;

        SetOwnerCollisionsIgnored(
            ownerIgnoreRemaining > 0f);

        return true;
    }

    public bool Launch(
        Vector3 velocity,
        Vector3 angularVelocity)
    {
        if (!prepared || launched || body == null)
            return false;

        launched = true;

        body.isKinematic = false;

        body.linearVelocity = velocity;
        body.angularVelocity = angularVelocity;
        body.WakeUp();

        return true;
    }

    private void Update()
    {
        if (!launched || activated || data == null)
            return;

        UpdateOwnerCollisionIgnore();

        lifeTimer +=
            Time.deltaTime;

        if (canActivate)
        {
            UpdateArmed();
        }
        else
        {
            UpdateInert();
        }
    }

    private void OnCollisionEnter(
        Collision collision)
    {
        if (!launched ||
            activated ||
            !canActivate ||
            data == null ||
            data.activationMode != GrenadeActivationMode.Impact)
        {
            return;
        }

        Activate();
    }

    private void UpdateOwnerCollisionIgnore()
    {
        if (!ownerCollisionsIgnored)
            return;

        ownerIgnoreRemaining -=
            Time.deltaTime;

        if (ownerIgnoreRemaining > 0f)
            return;

        SetOwnerCollisionsIgnored(
            false);
    }

    private void UpdateArmed()
    {
        if (data.activationMode != GrenadeActivationMode.Timed)
            return;

        fuseTimer +=
            Time.deltaTime;

        if (fuseTimer >= data.fuseTime)
        {
            Activate();
        }
    }

    private void UpdateInert()
    {
        bool settled =
            body.IsSleeping() ||
            body.linearVelocity.sqrMagnitude < 0.04f;

        if (settled)
        {
            settledTimer +=
                Time.deltaTime;
        }
        else
        {
            settledTimer = 0f;
            recoveryTimer = 0f;
        }

        if (settledTimer >= 0.25f)
        {
            recoveryTimer +=
                Time.deltaTime;
        }

        if (recoveryTimer >= data.inertRecoveryDelay ||
            lifeTimer >= data.maxInertLifetime)
        {
            ReturnToWorldPickup();
        }
    }

    private void Activate()
    {
        if (activated)
            return;

        activated = true;

        SetOwnerCollisionsIgnored(
            false);

        SetPhysicalCollidersEnabled(
            false);

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;

        GrenadeEffectContext context =
            new GrenadeEffectContext(
                transform.position,
                owner,
                gameObject);

        effect.Activate(
            data.effectData,
            context);

        Destroy(gameObject);
    }

    private void ReturnToWorldPickup()
    {
        Instantiate(
            data.worldPrefab,
            transform.position +
            Vector3.up * 0.1f,
            Quaternion.identity);

        Destroy(gameObject);
    }

    private void SetOwnerCollisionsIgnored(
        bool ignored)
    {
        if (grenadeColliders == null ||
            ownerColliders == null)
        {
            ownerCollisionsIgnored = false;
            return;
        }

        for (int grenadeIndex = 0;
             grenadeIndex < grenadeColliders.Length;
             grenadeIndex++)
        {
            Collider grenadeCollider =
                grenadeColliders[grenadeIndex];

            if (grenadeCollider == null)
                continue;

            for (int ownerIndex = 0;
                 ownerIndex < ownerColliders.Length;
                 ownerIndex++)
            {
                Collider ownerCollider =
                    ownerColliders[ownerIndex];

                if (ownerCollider == null ||
                    ownerCollider == grenadeCollider)
                {
                    continue;
                }

                Physics.IgnoreCollision(
                    grenadeCollider,
                    ownerCollider,
                    ignored);
            }
        }

        ownerCollisionsIgnored = ignored;
    }

    private void SetPhysicalCollidersEnabled(
        bool enabled)
    {
        if (grenadeColliders == null)
            return;

        for (int i = 0;
             i < grenadeColliders.Length;
             i++)
        {
            if (grenadeColliders[i] != null)
            {
                grenadeColliders[i].enabled =
                    enabled;
            }
        }
    }

    private void CacheReferences()
    {
        if (body == null)
        {
            body =
                GetComponent<Rigidbody>();
        }

        if (effect == null)
        {
            effect =
                GetComponent<GrenadeEffect>();
        }

        if (grenadeColliders == null ||
            grenadeColliders.Length == 0)
        {
            grenadeColliders =
                GetComponentsInChildren<Collider>(
                    true);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }
#endif
}
