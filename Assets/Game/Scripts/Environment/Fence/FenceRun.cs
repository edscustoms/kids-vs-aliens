using UnityEngine;
using UnityEngine.Serialization;

namespace KidsVsAliens.Environment
{
    public enum FenceExtendDirection
    {
        Straight,
        TurnUp,
        TurnDown,
    }

    [ExecuteAlways]
    public sealed class FenceRun : MonoBehaviour
    {
        [Header("Fence Run")]
        [FormerlySerializedAs("newSectionLength")]
        [Min(0.25f)]
        [Tooltip("Global center-to-center spacing for every connected fence section.")]
        [SerializeField] private float poleSpacing = 2.0f;

        [Min(0.25f)]
        [SerializeField] private float fenceHeight = 1.20f;

        [Min(0f)]
        [SerializeField] private float poleExtensionAboveFence = 0.20f;

        [Min(0f)]
        [SerializeField] private float bottomClearance = 0.08f;

        [Min(0.001f)]
        [SerializeField] private float mainPoleThickness = 0.08f;

        [SerializeField] private FencePoleStyle poleStyle = FencePoleStyle.Round;

        [Header("Materials")]
        [SerializeField] private Material poleMaterial;
        [SerializeField] private Material chainLinkMaterial;

        [Header("Collision")]
        [Min(0.01f)]
        [SerializeField] private float collisionThickness = 0.10f;

        [SerializeField] private bool collisionEnabled = true;

        [Header("Generated")]
        [SerializeField, HideInInspector] private Transform polesRoot;
        [SerializeField, HideInInspector] private Transform segmentsRoot;

        [SerializeField, HideInInspector] private Mesh roundPoleMesh;
        [SerializeField, HideInInspector] private Mesh squarePoleMesh;
        [SerializeField, HideInInspector] private Mesh railMesh;
        [SerializeField, HideInInspector] private Mesh chainLinkMesh;

        [SerializeField, HideInInspector] private int nextNodeId;

        // Initial standalone fence is centered on the FenceRun root:
        // Grid (0,0) -> -0.5 spacing on X
        // Grid (1,0) -> +0.5 spacing on X
        private static readonly Vector2 GridOriginOffset =
            new Vector2(-0.5f, 0f);

        private const float MinSpan = 0.05f;

        public float PoleSpacing => poleSpacing;

        private void OnValidate()
        {
            if (!HasResources())
                return;

            RebuildAll();
        }

        public void ConfigureFromSection(
            ConfigurableFenceSection source)
        {
            poleSpacing = source.PoleSpacing;
            fenceHeight = source.FenceHeight;
            poleExtensionAboveFence = source.PoleExtensionAboveFence;
            bottomClearance = source.BottomClearance;
            mainPoleThickness = source.MainPoleThickness;
            poleStyle = source.PoleStyle;

            poleMaterial = source.PoleMaterial;
            chainLinkMaterial = source.ChainLinkMaterial;

            collisionThickness = source.CollisionThickness;
            collisionEnabled = source.CollisionEnabled;

            roundPoleMesh = source.RoundPoleMesh;
            squarePoleMesh = source.SquarePoleMesh;
            railMesh = source.RailMesh;
            chainLinkMesh = source.ChainLinkMesh;

            EnsureRoots();
        }

        public FenceRunSegment CreateInitialSegment()
        {
            EnsureRoots();

            FencePoleNode left =
                GetOrCreateNode(
                    new Vector2Int(0, 0));

            FencePoleNode right =
                GetOrCreateNode(
                    new Vector2Int(1, 0));

            return GetOrCreateSegment(
                left,
                right);
        }

        public FenceRunSegment Extend(
            FenceRunSegment selectedSegment,
            bool fromNodeA,
            FenceExtendDirection direction,
            int count)
        {
            if (selectedSegment == null ||
                selectedSegment.Owner != this)
            {
                return null;
            }

            EnsureGridCoordinates();

            count = Mathf.Max(1, count);

            FencePoleNode endpoint =
                fromNodeA
                    ? selectedSegment.NodeA
                    : selectedSegment.NodeB;

            FencePoleNode other =
                fromNodeA
                    ? selectedSegment.NodeB
                    : selectedSegment.NodeA;

            if (endpoint == null || other == null)
                return null;

            Vector2Int outward =
                endpoint.GridCoordinate -
                other.GridCoordinate;

            outward =
                NormalizeCardinal(outward);

            if (outward == Vector2Int.zero)
                return null;

            Vector2Int stepDirection =
                ResolveDirection(
                    outward,
                    direction);

            FencePoleNode current =
                endpoint;

            FenceRunSegment lastSegment =
                selectedSegment;

            for (int i = 0; i < count; i++)
            {
                Vector2Int nextCoordinate =
                    current.GridCoordinate +
                    stepDirection;

                FencePoleNode next =
                    GetOrCreateNode(
                        nextCoordinate);

                lastSegment =
                    GetOrCreateSegment(
                        current,
                        next);

                current = next;
            }

            RebuildAll();

            return lastSegment;
        }

        public void RebuildAll()
        {
            EnsureRoots();

            if (!HasResources())
                return;

            EnsureGridCoordinates();

            FencePoleNode[] nodes =
                polesRoot.GetComponentsInChildren<FencePoleNode>(true);

            foreach (FencePoleNode node in nodes)
                RebuildPole(node);

            FenceRunSegment[] segments =
                segmentsRoot.GetComponentsInChildren<FenceRunSegment>(true);

            foreach (FenceRunSegment segment in segments)
                RebuildSegment(segment);
        }

        private static Vector2Int NormalizeCardinal(
            Vector2Int direction)
        {
            if (Mathf.Abs(direction.x) >=
                Mathf.Abs(direction.y))
            {
                if (direction.x == 0)
                    return Vector2Int.zero;

                return new Vector2Int(
                    direction.x > 0 ? 1 : -1,
                    0);
            }

            if (direction.y == 0)
                return Vector2Int.zero;

            return new Vector2Int(
                0,
                direction.y > 0 ? 1 : -1);
        }

        private static Vector2Int ResolveDirection(
            Vector2Int outward,
            FenceExtendDirection direction)
        {
            switch (direction)
            {
                // Local "up" on our X/Z builder plane = 90 degrees left.
                case FenceExtendDirection.TurnUp:
                    return new Vector2Int(
                        -outward.y,
                        outward.x);

                // Local "down" = 90 degrees right.
                case FenceExtendDirection.TurnDown:
                    return new Vector2Int(
                        outward.y,
                        -outward.x);

                default:
                    return outward;
            }
        }

        private FencePoleNode GetOrCreateNode(
            Vector2Int gridCoordinate)
        {
            FencePoleNode existing =
                FindNodeAt(gridCoordinate);

            if (existing != null)
                return existing;

            GameObject nodeObject =
                new GameObject(
                    $"Pole_{nextNodeId:000}");

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(
                    nodeObject,
                    "Create Fence Pole");
            }
#endif

            nodeObject.transform.SetParent(
                polesRoot,
                false);

            nodeObject.transform.localRotation =
                Quaternion.identity;

            nodeObject.transform.localScale =
                Vector3.one;

            FencePoleNode node =
                nodeObject.AddComponent<FencePoleNode>();

            node.Initialize(
                nextNodeId,
                gridCoordinate);

            nextNodeId++;

            nodeObject.AddComponent<MeshFilter>();
            nodeObject.AddComponent<MeshRenderer>();

            RebuildPole(node);

            return node;
        }

        private FencePoleNode FindNodeAt(
            Vector2Int gridCoordinate)
        {
            if (polesRoot == null)
                return null;

            FencePoleNode[] nodes =
                polesRoot.GetComponentsInChildren<FencePoleNode>(true);

            foreach (FencePoleNode node in nodes)
            {
                if (node == null ||
                    !node.HasGridCoordinate)
                {
                    continue;
                }

                if (node.GridCoordinate ==
                    gridCoordinate)
                {
                    return node;
                }
            }

            return null;
        }

        private FenceRunSegment GetOrCreateSegment(
            FencePoleNode a,
            FencePoleNode b)
        {
            FenceRunSegment existing =
                FindSegment(a, b);

            if (existing != null)
                return existing;

            GameObject segmentObject =
                new GameObject(
                    $"Segment_{a.NodeId:000}_{b.NodeId:000}");

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(
                    segmentObject,
                    "Create Fence Segment");
            }
#endif

            segmentObject.transform.SetParent(
                segmentsRoot,
                false);

            FenceRunSegment segment =
                segmentObject.AddComponent<FenceRunSegment>();

            segment.Initialize(
                this,
                a,
                b);

            CreateVisualChild(
                segmentObject.transform,
                "Rail_Top",
                railMesh,
                poleMaterial);

            CreateVisualChild(
                segmentObject.transform,
                "Rail_Bottom",
                railMesh,
                poleMaterial);

            CreateVisualChild(
                segmentObject.transform,
                "ChainLink",
                chainLinkMesh,
                chainLinkMaterial);

            GameObject collision =
                new GameObject("Collision");

            collision.transform.SetParent(
                segmentObject.transform,
                false);

            collision.AddComponent<BoxCollider>();

            RebuildSegment(segment);

            return segment;
        }

        private FenceRunSegment FindSegment(
            FencePoleNode a,
            FencePoleNode b)
        {
            if (segmentsRoot == null)
                return null;

            FenceRunSegment[] segments =
                segmentsRoot.GetComponentsInChildren<FenceRunSegment>(true);

            foreach (FenceRunSegment segment in segments)
            {
                if (segment == null)
                    continue;

                bool same =
                    segment.NodeA == a &&
                    segment.NodeB == b;

                bool reverse =
                    segment.NodeA == b &&
                    segment.NodeB == a;

                if (same || reverse)
                    return segment;
            }

            return null;
        }

        private void RebuildPole(
            FencePoleNode node)
        {
            if (node == null)
                return;

            MeshFilter filter =
                node.GetComponent<MeshFilter>();

            MeshRenderer renderer =
                node.GetComponent<MeshRenderer>();

            if (filter == null ||
                renderer == null)
            {
                return;
            }

            Mesh selectedPole =
                poleStyle == FencePoleStyle.Round
                    ? roundPoleMesh
                    : squarePoleMesh;

            filter.sharedMesh =
                selectedPole;

            if (poleMaterial != null)
                renderer.sharedMaterial =
                    poleMaterial;

            if (selectedPole == null)
                return;

            Bounds bounds =
                selectedPole.bounds;

            float sourceHeight =
                Mathf.Max(
                    Mathf.Epsilon,
                    bounds.size.y);

            float targetHeight =
                Mathf.Max(
                    MinSpan,
                    fenceHeight +
                    poleExtensionAboveFence);

            float scaleY =
                targetHeight /
                sourceHeight;

            Vector3 gridPosition =
                GridToLocalPosition(
                    node.GridCoordinate);

            node.transform.localRotation =
                Quaternion.identity;

            node.transform.localScale =
                new Vector3(
                    1f,
                    scaleY,
                    1f);

            node.transform.localPosition =
                new Vector3(
                    gridPosition.x,
                    -bounds.min.y * scaleY,
                    gridPosition.z);
        }

        private Vector3 GridToLocalPosition(
            Vector2Int coordinate)
        {
            float x =
                (coordinate.x +
                 GridOriginOffset.x) *
                poleSpacing;

            float z =
                (coordinate.y +
                 GridOriginOffset.y) *
                poleSpacing;

            return new Vector3(
                x,
                0f,
                z);
        }

        private void RebuildSegment(
            FenceRunSegment segment)
        {
            if (segment == null ||
                segment.NodeA == null ||
                segment.NodeB == null)
            {
                return;
            }

            Vector3 a =
                GridToLocalPosition(
                    segment.NodeA.GridCoordinate);

            Vector3 b =
                GridToLocalPosition(
                    segment.NodeB.GridCoordinate);

            Vector3 delta =
                b - a;

            float distance =
                delta.magnitude;

            if (distance <= Mathf.Epsilon)
                return;

            Vector3 direction =
                delta / distance;

            Transform root =
                segment.transform;

            root.localPosition =
                (a + b) * 0.5f;

            // Keep fence segments strictly horizontal.
            //
            // Quaternion.FromToRotation(Vector3.right, Vector3.left)
            // has an ambiguous 180-degree solution and Unity can choose
            // a Z-axis flip. That turns local +Y into -Y and places the
            // whole fence section underneath the floor.
            //
            // Build the rotation from a horizontal forward vector instead,
            // explicitly preserving Vector3.up.
            root.localRotation =
                CreateHorizontalRotation(direction);

            root.localScale =
                Vector3.one;

            Transform topRail =
                root.Find("Rail_Top");

            Transform bottomRail =
                root.Find("Rail_Bottom");

            Transform chain =
                root.Find("ChainLink");

            Transform collision =
                root.Find("Collision");

            if (topRail == null ||
                bottomRail == null ||
                chain == null ||
                collision == null)
            {
                return;
            }

            float clearSpan =
                Mathf.Max(
                    MinSpan,
                    distance -
                    mainPoleThickness);

            float railDiameter =
                railMesh != null
                    ? railMesh.bounds.size.y
                    : 0.045f;

            railDiameter =
                Mathf.Max(
                    0.001f,
                    railDiameter);

            float railRadius =
                railDiameter * 0.5f;

            float bottomRailY =
                bottomClearance +
                railRadius;

            float topRailY =
                Mathf.Max(
                    fenceHeight,
                    bottomRailY +
                    railDiameter +
                    MinSpan);

            FitHorizontal(
                topRail,
                railMesh,
                clearSpan,
                topRailY);

            FitHorizontal(
                bottomRail,
                railMesh,
                clearSpan,
                bottomRailY);

            float chainBottom =
                bottomRailY +
                railRadius;

            float chainTop =
                topRailY -
                railRadius;

            float chainHeight =
                Mathf.Max(
                    MinSpan,
                    chainTop -
                    chainBottom);

            float chainCenterY =
                (chainBottom +
                 chainTop) *
                0.5f;

            FitPanel(
                chain,
                chainLinkMesh,
                clearSpan,
                chainHeight,
                chainCenterY);

            MeshRenderer topRenderer =
                topRail.GetComponent<MeshRenderer>();

            MeshRenderer bottomRenderer =
                bottomRail.GetComponent<MeshRenderer>();

            MeshRenderer chainRenderer =
                chain.GetComponent<MeshRenderer>();

            if (poleMaterial != null)
            {
                if (topRenderer != null)
                    topRenderer.sharedMaterial =
                        poleMaterial;

                if (bottomRenderer != null)
                    bottomRenderer.sharedMaterial =
                        poleMaterial;
            }

            if (chainLinkMaterial != null &&
                chainRenderer != null)
            {
                chainRenderer.sharedMaterial =
                    chainLinkMaterial;
            }

            BoxCollider box =
                collision.GetComponent<BoxCollider>();

            if (box != null)
            {
                box.enabled =
                    collisionEnabled;

                float colliderHeight =
                    Mathf.Max(
                        MinSpan,
                        fenceHeight +
                        poleExtensionAboveFence);

                box.center =
                    new Vector3(
                        0f,
                        colliderHeight * 0.5f,
                        0f);

                box.size =
                    new Vector3(
                        distance +
                        mainPoleThickness,
                        colliderHeight,
                        Mathf.Max(
                            0.01f,
                            collisionThickness));
            }
        }

        private static Quaternion CreateHorizontalRotation(
            Vector3 rightDirection)
        {
            rightDirection.y = 0f;

            if (rightDirection.sqrMagnitude <= Mathf.Epsilon)
                return Quaternion.identity;

            rightDirection.Normalize();

            // Unity basis:
            // right = Cross(up, forward)
            // therefore forward = Cross(right, up).
            Vector3 forward =
                Vector3.Cross(
                    rightDirection,
                    Vector3.up);

            if (forward.sqrMagnitude <= Mathf.Epsilon)
                return Quaternion.identity;

            forward.Normalize();

            return Quaternion.LookRotation(
                forward,
                Vector3.up);
        }

        private void EnsureGridCoordinates()
        {
            if (polesRoot == null)
                return;

            FencePoleNode[] nodes =
                polesRoot.GetComponentsInChildren<FencePoleNode>(true);

            // V2+ runs already have logical coordinates.
            bool requiresMigration = false;

            foreach (FencePoleNode node in nodes)
            {
                if (node != null &&
                    !node.HasGridCoordinate)
                {
                    requiresMigration = true;
                    break;
                }
            }

            if (!requiresMigration)
                return;

            // Upgrade Smart Fence V1 runs in-place.
            // V1 was centered with the first poles at +/- spacing/2.
            float safeSpacing =
                Mathf.Max(
                    0.25f,
                    poleSpacing);

            foreach (FencePoleNode node in nodes)
            {
                if (node == null ||
                    node.HasGridCoordinate)
                {
                    continue;
                }

                Vector3 oldPosition =
                    node.transform.localPosition;

                int gridX =
                    Mathf.RoundToInt(
                        oldPosition.x /
                        safeSpacing +
                        0.5f);

                int gridZ =
                    Mathf.RoundToInt(
                        oldPosition.z /
                        safeSpacing);

                node.SetGridCoordinate(
                    new Vector2Int(
                        gridX,
                        gridZ));
            }
        }

        private static void FitHorizontal(
            Transform target,
            Mesh mesh,
            float targetLength,
            float centerY)
        {
            if (target == null ||
                mesh == null)
            {
                return;
            }

            Bounds bounds =
                mesh.bounds;

            float sourceLength =
                Mathf.Max(
                    Mathf.Epsilon,
                    bounds.size.x);

            float scaleX =
                targetLength /
                sourceLength;

            target.localRotation =
                Quaternion.identity;

            target.localScale =
                new Vector3(
                    scaleX,
                    1f,
                    1f);

            target.localPosition =
                new Vector3(
                    -bounds.center.x *
                    scaleX,
                    centerY -
                    bounds.center.y,
                    -bounds.center.z);
        }

        private static void FitPanel(
            Transform target,
            Mesh mesh,
            float targetWidth,
            float targetHeight,
            float centerY)
        {
            if (target == null ||
                mesh == null)
            {
                return;
            }

            Bounds bounds =
                mesh.bounds;

            float scaleX =
                targetWidth /
                Mathf.Max(
                    Mathf.Epsilon,
                    bounds.size.x);

            float scaleY =
                targetHeight /
                Mathf.Max(
                    Mathf.Epsilon,
                    bounds.size.y);

            target.localRotation =
                Quaternion.identity;

            target.localScale =
                new Vector3(
                    scaleX,
                    scaleY,
                    1f);

            target.localPosition =
                new Vector3(
                    -bounds.center.x *
                    scaleX,
                    centerY -
                    bounds.center.y *
                    scaleY,
                    -bounds.center.z);
        }

        private static Transform CreateVisualChild(
            Transform parent,
            string name,
            Mesh mesh,
            Material material)
        {
            GameObject child =
                new GameObject(name);

            child.transform.SetParent(
                parent,
                false);

            MeshFilter filter =
                child.AddComponent<MeshFilter>();

            filter.sharedMesh =
                mesh;

            MeshRenderer renderer =
                child.AddComponent<MeshRenderer>();

            renderer.sharedMaterial =
                material;

            return child.transform;
        }

        private void EnsureRoots()
        {
            if (polesRoot == null)
            {
                Transform existing =
                    transform.Find("Poles");

                if (existing != null)
                {
                    polesRoot = existing;
                }
                else
                {
                    GameObject obj =
                        new GameObject("Poles");

                    obj.transform.SetParent(
                        transform,
                        false);

                    polesRoot =
                        obj.transform;
                }
            }

            if (segmentsRoot == null)
            {
                Transform existing =
                    transform.Find("Segments");

                if (existing != null)
                {
                    segmentsRoot = existing;
                }
                else
                {
                    GameObject obj =
                        new GameObject("Segments");

                    obj.transform.SetParent(
                        transform,
                        false);

                    segmentsRoot =
                        obj.transform;
                }
            }
        }

        private bool HasResources()
        {
            return roundPoleMesh != null
                && squarePoleMesh != null
                && railMesh != null
                && chainLinkMesh != null;
        }
    }
}
