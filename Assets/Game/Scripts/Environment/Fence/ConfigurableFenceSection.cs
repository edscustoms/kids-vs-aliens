using UnityEngine;

namespace KidsVsAliens.Environment
{
    public enum FencePoleStyle
    {
        Round,
        Square,
    }

    [ExecuteAlways]
    public sealed class ConfigurableFenceSection : MonoBehaviour
    {
        [Header("Fence")]
        [Min(0.25f)]
        [SerializeField] private float fenceHeight = 1.20f;

        [Min(0.25f)]
        [Tooltip("Center-to-center distance between the two main poles.")]
        [SerializeField] private float poleSpacing = 2.00f;

        [Min(0f)]
        [Tooltip("How far the main poles extend above the top rail.")]
        [SerializeField] private float poleExtensionAboveFence = 0.20f;

        [Min(0f)]
        [SerializeField] private float bottomClearance = 0.08f;

        [Min(0.001f)]
        [Tooltip("Main pole diameter/width. Current source poles are 0.08 m.")]
        [SerializeField] private float mainPoleThickness = 0.08f;

        [SerializeField] private FencePoleStyle poleStyle = FencePoleStyle.Round;

        [Header("Materials")]
        [Tooltip("Shared material for vertical poles + horizontal rails.")]
        [SerializeField] private Material poleMaterial;

        [SerializeField] private Material chainLinkMaterial;

        [Header("Collision")]
        [Min(0.01f)]
        [Tooltip("Physical thickness of the fence barrier collider.")]
        [SerializeField] private float collisionThickness = 0.10f;

        [Tooltip("If disabled, the fence stays visible but does not block movement.")]
        [SerializeField] private bool collisionEnabled = true;

        [SerializeField, HideInInspector] private BoxCollider fenceCollider;

        [SerializeField, HideInInspector] private Mesh roundPoleMesh;
        [SerializeField, HideInInspector] private Mesh squarePoleMesh;
        [SerializeField, HideInInspector] private Mesh railMesh;
        [SerializeField, HideInInspector] private Mesh chainLinkMesh;

        [SerializeField, HideInInspector] private MeshFilter leftPole;
        [SerializeField, HideInInspector] private MeshFilter rightPole;
        [SerializeField, HideInInspector] private MeshFilter topRail;
        [SerializeField, HideInInspector] private MeshFilter bottomRail;
        [SerializeField, HideInInspector] private MeshFilter chainLink;

        [SerializeField, HideInInspector] private MeshRenderer leftPoleRenderer;
        [SerializeField, HideInInspector] private MeshRenderer rightPoleRenderer;
        [SerializeField, HideInInspector] private MeshRenderer topRailRenderer;
        [SerializeField, HideInInspector] private MeshRenderer bottomRailRenderer;
        [SerializeField, HideInInspector] private MeshRenderer chainLinkRenderer;


        public float FenceHeight => fenceHeight;
        public float PoleSpacing => poleSpacing;
        public float PoleExtensionAboveFence => poleExtensionAboveFence;
        public float BottomClearance => bottomClearance;
        public float MainPoleThickness => mainPoleThickness;
        public FencePoleStyle PoleStyle => poleStyle;

        public Material PoleMaterial => poleMaterial;
        public Material ChainLinkMaterial => chainLinkMaterial;

        public float CollisionThickness => collisionThickness;
        public bool CollisionEnabled => collisionEnabled;

        public Mesh RoundPoleMesh => roundPoleMesh;
        public Mesh SquarePoleMesh => squarePoleMesh;
        public Mesh RailMesh => railMesh;
        public Mesh ChainLinkMesh => chainLinkMesh;

        private const float MinSpan = 0.05f;

        private void OnValidate()
        {
            Rebuild();
        }

        [ContextMenu("Rebuild Fence")]
        public void Rebuild()
        {
            if (!HasRequiredReferences())
                return;

            ApplyMeshes();
            ApplyMaterials();
            ApplyGeometry();
            ApplyCollision();
        }

        public void ConfigureAuthoringReferences(
            Mesh roundMesh,
            Mesh squareMesh,
            Mesh horizontalRailMesh,
            Mesh chainMesh,
            MeshFilter leftPoleFilter,
            MeshFilter rightPoleFilter,
            MeshFilter topRailFilter,
            MeshFilter bottomRailFilter,
            MeshFilter chainLinkFilter,
            Material defaultPoleMaterial,
            Material defaultChainLinkMaterial,
            BoxCollider generatedFenceCollider)
        {
            roundPoleMesh = roundMesh;
            squarePoleMesh = squareMesh;
            railMesh = horizontalRailMesh;
            chainLinkMesh = chainMesh;

            leftPole = leftPoleFilter;
            rightPole = rightPoleFilter;
            topRail = topRailFilter;
            bottomRail = bottomRailFilter;
            chainLink = chainLinkFilter;

            leftPoleRenderer = GetRenderer(leftPoleFilter);
            rightPoleRenderer = GetRenderer(rightPoleFilter);
            topRailRenderer = GetRenderer(topRailFilter);
            bottomRailRenderer = GetRenderer(bottomRailFilter);
            chainLinkRenderer = GetRenderer(chainLinkFilter);

            poleMaterial = defaultPoleMaterial;
            chainLinkMaterial = defaultChainLinkMaterial;
            fenceCollider = generatedFenceCollider;

            Rebuild();
        }

        private static MeshRenderer GetRenderer(MeshFilter filter)
        {
            return filter != null
                ? filter.GetComponent<MeshRenderer>()
                : null;
        }

        private bool HasRequiredReferences()
        {
            return roundPoleMesh != null
                && squarePoleMesh != null
                && railMesh != null
                && chainLinkMesh != null
                && leftPole != null
                && rightPole != null
                && topRail != null
                && bottomRail != null
                && chainLink != null;
        }

        private void ApplyMeshes()
        {
            Mesh selectedPole =
                poleStyle == FencePoleStyle.Round
                    ? roundPoleMesh
                    : squarePoleMesh;

            leftPole.sharedMesh = selectedPole;
            rightPole.sharedMesh = selectedPole;

            topRail.sharedMesh = railMesh;
            bottomRail.sharedMesh = railMesh;
            chainLink.sharedMesh = chainLinkMesh;
        }

        private void ApplyMaterials()
        {
            if (poleMaterial != null)
            {
                SetMaterial(leftPoleRenderer, poleMaterial);
                SetMaterial(rightPoleRenderer, poleMaterial);
                SetMaterial(topRailRenderer, poleMaterial);
                SetMaterial(bottomRailRenderer, poleMaterial);
            }

            if (chainLinkMaterial != null)
                SetMaterial(chainLinkRenderer, chainLinkMaterial);
        }

        private static void SetMaterial(
            MeshRenderer renderer,
            Material material)
        {
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        private void ApplyGeometry()
        {
            float spacing =
                Mathf.Max(
                    poleSpacing,
                    mainPoleThickness + MinSpan);

            float poleHeight =
                Mathf.Max(
                    MinSpan,
                    fenceHeight + poleExtensionAboveFence);

            // The rails/mesh sit BETWEEN the inside faces of the two posts.
            float clearSpan =
                Mathf.Max(
                    MinSpan,
                    spacing - mainPoleThickness);

            FitVerticalPole(
                leftPole,
                -spacing * 0.5f,
                poleHeight);

            FitVerticalPole(
                rightPole,
                spacing * 0.5f,
                poleHeight);

            float railDiameter =
                GetAuthoredSize(railMesh).y;

            if (railDiameter <= Mathf.Epsilon)
                railDiameter = 0.045f;

            float railRadius =
                railDiameter * 0.5f;

            float bottomRailY =
                bottomClearance + railRadius;

            float topRailY =
                Mathf.Max(
                    fenceHeight,
                    bottomRailY + railDiameter + MinSpan);

            FitHorizontalRail(
                topRail,
                clearSpan,
                topRailY);

            FitHorizontalRail(
                bottomRail,
                clearSpan,
                bottomRailY);

            float chainBottom =
                bottomRailY + railRadius;

            float chainTop =
                topRailY - railRadius;

            float chainHeight =
                Mathf.Max(
                    MinSpan,
                    chainTop - chainBottom);

            float chainCenterY =
                (chainBottom + chainTop) * 0.5f;

            FitPanel(
                chainLink,
                clearSpan,
                chainHeight,
                chainCenterY);
        }

        private static void FitVerticalPole(
            MeshFilter filter,
            float centerX,
            float targetHeight)
        {
            Mesh mesh = filter.sharedMesh;
            Bounds bounds = mesh.bounds;

            float sourceHeight =
                Mathf.Max(
                    Mathf.Epsilon,
                    bounds.size.y);

            float scaleY =
                targetHeight / sourceHeight;

            Transform t = filter.transform;

            t.localRotation = Quaternion.identity;
            t.localScale =
                new Vector3(
                    1f,
                    scaleY,
                    1f);

            // Baked pole meshes have their base at Y=0,
            // but this stays correct even if source bounds change later.
            t.localPosition =
                new Vector3(
                    centerX - bounds.center.x,
                    -bounds.min.y * scaleY,
                    -bounds.center.z);
        }

        private static void FitHorizontalRail(
            MeshFilter filter,
            float targetLength,
            float centerY)
        {
            Mesh mesh = filter.sharedMesh;
            Bounds bounds = mesh.bounds;

            float sourceLength =
                Mathf.Max(
                    Mathf.Epsilon,
                    bounds.size.x);

            float scaleX =
                targetLength / sourceLength;

            Transform t = filter.transform;

            t.localRotation = Quaternion.identity;
            t.localScale =
                new Vector3(
                    scaleX,
                    1f,
                    1f);

            t.localPosition =
                new Vector3(
                    -bounds.center.x * scaleX,
                    centerY - bounds.center.y,
                    -bounds.center.z);
        }

        private static void FitPanel(
            MeshFilter filter,
            float targetWidth,
            float targetHeight,
            float centerY)
        {
            Mesh mesh = filter.sharedMesh;
            Bounds bounds = mesh.bounds;

            float sourceWidth =
                Mathf.Max(
                    Mathf.Epsilon,
                    bounds.size.x);

            float sourceHeight =
                Mathf.Max(
                    Mathf.Epsilon,
                    bounds.size.y);

            float scaleX =
                targetWidth / sourceWidth;

            float scaleY =
                targetHeight / sourceHeight;

            Transform t = filter.transform;

            t.localRotation = Quaternion.identity;
            t.localScale =
                new Vector3(
                    scaleX,
                    scaleY,
                    1f);

            t.localPosition =
                new Vector3(
                    -bounds.center.x * scaleX,
                    centerY - bounds.center.y * scaleY,
                    -bounds.center.z);
        }

        private void ApplyCollision()
        {
            if (fenceCollider == null)
                return;

            fenceCollider.enabled = collisionEnabled;

            if (!collisionEnabled)
                return;

            float spacing =
                Mathf.Max(
                    poleSpacing,
                    mainPoleThickness + MinSpan);

            float colliderHeight =
                Mathf.Max(
                    MinSpan,
                    fenceHeight + poleExtensionAboveFence);

            float thickness =
                Mathf.Max(
                    0.01f,
                    collisionThickness);

            // One cheap solid barrier collider for the complete fence section.
            // This includes both main poles so Amy cannot squeeze through the ends.
            fenceCollider.center =
                new Vector3(
                    0f,
                    colliderHeight * 0.5f,
                    0f);

            fenceCollider.size =
                new Vector3(
                    spacing + mainPoleThickness,
                    colliderHeight,
                    thickness);
        }

        private static Vector3 GetAuthoredSize(Mesh mesh)
        {
            return mesh != null
                ? mesh.bounds.size
                : Vector3.zero;
        }
    }
}
