using System.Collections.Generic;
using UnityEngine;

public class AimTarget : MonoBehaviour
{
    // =====================================================
    // GLOBAL REGISTRY
    // =====================================================

    private static readonly List<AimTarget> activeTargets = new();

    public static IReadOnlyList<AimTarget> ActiveTargets => activeTargets;

    // =====================================================
    // TARGET
    // =====================================================

    [Header("Target")]
    [SerializeField]
    private bool targetable = true;

    // =====================================================
    // BODY DETECTION
    // =====================================================

    [Header("Body Detection")]
    [Tooltip(
        "Optional override for unusual enemies. "
            + "Leave empty and the system automatically finds "
            + "the common body root."
    )]
    [SerializeField]
    private Transform bodyRootOverride;

    // =====================================================
    // OPTIONAL SPECIAL AIM POINTS
    //
    // Normal enemies leave this empty.
    // Bosses can use explicit weak points later.
    // =====================================================

    [Header("Optional Special Aim Points")]
    [SerializeField]
    private Transform[] explicitAimPoints;

    // =====================================================
    // CACHED BODY DATA
    // =====================================================

    private Collider[] bodyColliders;

    [SerializeField]
    private Transform cachedBodyRoot;

    [SerializeField]
    private Vector3 localBodyCenter;

    [SerializeField]
    private float localBodyRadius = 0.5f;

    [SerializeField]
    private bool bodyCached;

    [SerializeField]
    private int cachedColliderCount;

    [SerializeField]
    private int cachedRendererCount;

    // =====================================================
    // PUBLIC
    // =====================================================

    public bool IsTargetable =>
        targetable && isActiveAndEnabled && gameObject.activeInHierarchy && HasShootableBody();

    public Vector3 BodyCenter
    {
        get
        {
            Transform root = cachedBodyRoot != null ? cachedBodyRoot : transform;

            return root.TransformPoint(localBodyCenter);
        }
    }

    public float BodyRadius
    {
        get
        {
            Transform root = cachedBodyRoot != null ? cachedBodyRoot : transform;

            Vector3 scale = root.lossyScale;

            float largestScale = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y),
                Mathf.Abs(scale.z)
            );

            return localBodyRadius * largestScale;
        }
    }

    public bool HasExplicitAimPoints => explicitAimPoints != null && explicitAimPoints.Length > 0;

    public Collider[] BodyColliders => bodyColliders;

    // =====================================================
    // STATIC RESET
    // =====================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        activeTargets.Clear();
    }

    // =====================================================
    // LIFECYCLE
    // =====================================================

    private void OnEnable()
    {
        if (!activeTargets.Contains(this))
        {
            activeTargets.Add(this);
        }
    }

    private void Start()
    {
        CacheBodyData();
    }

    private void OnDisable()
    {
        activeTargets.Remove(this);
    }

    private void OnDestroy()
    {
        activeTargets.Remove(this);
    }

    // =====================================================
    // BODY CACHE
    // =====================================================

    public void CacheBodyData()
    {
        // Colliders are cached for later LOS / ownership.
        // Disabled colliders are fine.
        bodyColliders = GetComponentsInChildren<Collider>(includeInactive: true);

        cachedColliderCount = bodyColliders.Length;

        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);

        List<Renderer> renderers = new List<Renderer>();

        foreach (Renderer renderer in allRenderers)
        {
            if (renderer == null)
                continue;

            // Don't let VFX influence body size.
            if (
                renderer is ParticleSystemRenderer
                || renderer is LineRenderer
                || renderer is TrailRenderer
            )
            {
                continue;
            }

            renderers.Add(renderer);
        }

        cachedRendererCount = renderers.Count;

        if (renderers.Count == 0)
        {
            UseFallback();

            return;
        }

        // -------------------------------------------------
        // Find the common transform that actually owns the
        // visual body.
        //
        // Practice target:
        //
        // TargetRoot
        //   HingePivot
        //     BreakablePieces  <-- usually becomes body root
        //
        // This means folding/rotating the HingePivot does
        // NOT invalidate our cached body coordinates.
        // -------------------------------------------------

        cachedBodyRoot =
            bodyRootOverride != null ? bodyRootOverride : FindCommonBodyRoot(renderers);

        if (cachedBodyRoot == null)
        {
            cachedBodyRoot = transform;
        }

        // -------------------------------------------------
        // Build combined bounds in BODY-ROOT LOCAL SPACE.
        //
        // Important:
        // We use each Renderer's LOCAL bounds.
        //
        // Therefore current world rotation / hinge state
        // cannot change the calculated body dimensions.
        // -------------------------------------------------

        bool foundBounds = false;
        Bounds combinedBounds = default;

        foreach (Renderer renderer in renderers)
        {
            Bounds rendererBounds = renderer.localBounds;

            if (rendererBounds.size.sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            EncapsulateRendererBounds(
                renderer,
                rendererBounds,
                ref combinedBounds,
                ref foundBounds
            );
        }

        if (!foundBounds)
        {
            UseFallback();

            return;
        }

        localBodyCenter = combinedBounds.center;

        Vector3 size = combinedBounds.size;

        // Largest dimension gives us a generic body radius
        // regardless of which local axis happens to be
        // Blender's height axis.
        float largestDimension = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

        localBodyRadius = Mathf.Max(largestDimension * 0.5f, 0.05f);

        bodyCached = true;

        Debug.Log(
            $"{name} AimTarget cached. "
                + $"BodyRoot={cachedBodyRoot.name}, "
                + $"Colliders={cachedColliderCount}, "
                + $"Renderers={cachedRendererCount}, "
                + $"Center={localBodyCenter}, "
                + $"Radius={localBodyRadius}",
            this
        );
    }

    private void EncapsulateRendererBounds(
        Renderer renderer,
        Bounds rendererBounds,
        ref Bounds combinedBounds,
        ref bool foundBounds
    )
    {
        Vector3 min = rendererBounds.min;

        Vector3 max = rendererBounds.max;

        // 8 corners of the renderer's LOCAL bounds.
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
        };

        foreach (Vector3 corner in corners)
        {
            // Renderer local
            //      ↓
            // World
            Vector3 worldPoint = renderer.transform.TransformPoint(corner);

            // World
            //      ↓
            // Body-root local
            Vector3 bodyLocalPoint = cachedBodyRoot.InverseTransformPoint(worldPoint);

            if (!foundBounds)
            {
                combinedBounds = new Bounds(bodyLocalPoint, Vector3.zero);

                foundBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(bodyLocalPoint);
            }
        }
    }

    private Transform FindCommonBodyRoot(List<Renderer> renderers)
    {
        if (renderers.Count == 0)
            return transform;

        Transform common = renderers[0].transform;

        for (int i = 1; i < renderers.Count; i++)
        {
            common = FindCommonAncestor(common, renderers[i].transform);

            if (common == transform)
                break;
        }

        // Safety:
        // body root must belong to this AimTarget.
        if (common == null || (common != transform && !common.IsChildOf(transform)))
        {
            return transform;
        }

        return common;
    }

    private Transform FindCommonAncestor(Transform first, Transform second)
    {
        Transform candidate = first;

        while (candidate != null)
        {
            if (second == candidate || second.IsChildOf(candidate))
            {
                return candidate;
            }

            candidate = candidate.parent;
        }

        return transform;
    }

    private void UseFallback()
    {
        cachedBodyRoot = transform;

        localBodyCenter = Vector3.zero;

        localBodyRadius = 0.5f;

        bodyCached = true;

        Debug.LogWarning(
            $"{name}: AimTarget could not calculate " + "body bounds. Using fallback values.",
            this
        );
    }

    // =====================================================
    // TARGETABLE
    // =====================================================

    public void SetTargetable(bool value)
    {
        targetable = value;
    }

    // =====================================================
    // SPECIAL AIM POINTS
    // =====================================================

    public Transform GetExplicitAimPoint(int index)
    {
        if (explicitAimPoints == null || index < 0 || index >= explicitAimPoints.Length)
        {
            return null;
        }

        return explicitAimPoints[index];
    }

    public Transform GetRandomExplicitAimPoint()
    {
        if (!HasExplicitAimPoints)
            return null;

        int startIndex = Random.Range(0, explicitAimPoints.Length);

        for (int offset = 0; offset < explicitAimPoints.Length; offset++)
        {
            int index = (startIndex + offset) % explicitAimPoints.Length;

            if (explicitAimPoints[index] != null)
            {
                return explicitAimPoints[index];
            }
        }

        return null;
    }

    // =====================================================
    // OWNERSHIP / LOS
    // =====================================================

    public bool OwnsCollider(Collider collider)
    {
        if (collider == null)
            return false;

        Transform hitTransform = collider.transform;

        return hitTransform == transform || hitTransform.IsChildOf(transform);
    }

    // =====================================================
    // SHOOTABLE BODY
    //
    // Normal case is extremely cheap:
    // the first enabled collider usually returns true.
    //
    // This also solves our breakable targets automatically:
    //
    // - Inactive/folded target:
    //      colliders disabled -> not targetable
    //
    // - Partially broken:
    //      remaining attached pieces -> targetable
    //
    // - Fully collapsed:
    //      all pieces detached -> no owned collider -> not targetable
    //
    // - Reassembling:
    //      pieces remain detached/disabled -> not targetable
    //
    // - Reassembled:
    //      pieces return under BreakablePieces -> targetable again
    //
    // No PracticeTarget-specific dependency is required.
    // =====================================================

    private bool HasShootableBody()
    {
        if (bodyColliders == null || bodyColliders.Length == 0)
        {
            // Allows unusual targets that use explicit aim points
            // or don't rely on normal physics colliders.
            return true;
        }

        for (int i = 0; i < bodyColliders.Length; i++)
        {
            Collider collider = bodyColliders[i];

            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            // Broken cardboard pieces are detached from the
            // AimTarget hierarchy, so they no longer count.
            if (!OwnsCollider(collider))
                continue;

            return true;
        }

        return false;
    }

    // =====================================================
    // GUARANTEED AIM POINT
    //
    // Used only for the GREEN mobile aim zone.
    //
    // We take the desired point inside the Green area,
    // find the nearest piece of actual target collider geometry,
    // and aim slightly THROUGH that surface.
    //
    // Therefore, assuming no wall/other object blocks the shot,
    // the weapon ray is guaranteed to intersect this target.
    //
    // This is deliberately calculated only when needed for a
    // shot — not every frame.
    // =====================================================

    public bool TryGetGuaranteedAimPoint(
        Vector3 desiredWorldPoint,
        Vector3 shotOrigin,
        out Vector3 guaranteedPoint
    )
    {
        guaranteedPoint = BodyCenter;

        if (bodyColliders == null || bodyColliders.Length == 0)
        {
            return false;
        }

        bool foundCollider = false;

        Vector3 bestPoint = Vector3.zero;

        float bestDistanceSquared = Mathf.Infinity;

        for (int i = 0; i < bodyColliders.Length; i++)
        {
            Collider collider = bodyColliders[i];

            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            // Ignore cardboard pieces that have already
            // been punched out / detached.
            if (!OwnsCollider(collider))
                continue;

            Vector3 closestPoint = collider.ClosestPoint(desiredWorldPoint);

            float distanceSquared = (closestPoint - desiredWorldPoint).sqrMagnitude;

            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;

            bestPoint = closestPoint;

            foundCollider = true;
        }

        if (!foundCollider)
            return false;

        // Aim a tiny bit through the collider surface.
        // This avoids numerical cases where the endpoint
        // sits exactly on the collider skin.
        Vector3 throughDirection = bestPoint - shotOrigin;

        if (throughDirection.sqrMagnitude > 0.000001f)
        {
            throughDirection.Normalize();

            bestPoint += throughDirection * 0.02f;
        }

        guaranteedPoint = bestPoint;

        return true;
    }

    // =====================================================
    // DEBUG
    // =====================================================

#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !bodyCached)
        {
            return;
        }

        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(BodyCenter, BodyRadius);

        if (explicitAimPoints == null)
            return;

        foreach (Transform point in explicitAimPoints)
        {
            if (point == null)
                continue;

            Gizmos.DrawWireSphere(point.position, 0.08f);
        }
    }

#endif
}
