using UnityEngine;

namespace KidsVsAliens.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ProximityProgressRing : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private ProximityHoldTrigger trigger;

        [SerializeField]
        private Transform anchor;

        [Header("Position")]
        [Tooltip(
            "World-space height above the chest/interaction anchor. "
                + "Using world up keeps the hologram above the chest even if the imported model/anchor is rotated."
        )]
        [SerializeField, Min(0f)]
        private float heightAboveAnchor = 0.65f;

        [SerializeField]
        private Vector3 worldOffset = Vector3.zero;

        [Header("Ring")]
        [SerializeField, Min(0.05f)]
        private float radius = 0.18f;

        [SerializeField, Min(0.005f)]
        private float backgroundWidth = 0.025f;

        [SerializeField, Min(0.005f)]
        private float progressWidth = 0.040f;

        [SerializeField, Range(12, 96)]
        private int segments = 48;

        [SerializeField]
        private Color backgroundColor = new Color(0.08f, 0.12f, 0.16f, 0.45f);

        [SerializeField]
        private Color progressColor = new Color(0.10f, 0.90f, 1.00f, 1.00f);

        [Header("Feel")]
        [SerializeField, Min(0f)]
        private float pulseAmount = 0.06f;

        [SerializeField, Min(0f)]
        private float pulseSpeed = 5f;

        private static Material sharedLineMaterial;

        private Transform visualRoot;
        private LineRenderer backgroundLine;
        private LineRenderer progressLine;
        private Camera cachedCamera;
        private bool interactionActive;

        public void Configure(ProximityHoldTrigger newTrigger, Transform newAnchor)
        {
            trigger = newTrigger;
            anchor = newAnchor;
        }

        private void Awake()
        {
            EnsureVisual();
            SetVisible(false);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (!interactionActive || visualRoot == null)
            {
                return;
            }

            if (cachedCamera == null)
                cachedCamera = Camera.main;

            Vector3 basePosition = anchor != null ? anchor.position : transform.position;

            visualRoot.position = basePosition + Vector3.up * heightAboveAnchor + worldOffset;

            if (cachedCamera != null)
            {
                Vector3 towardCamera = cachedCamera.transform.position - visualRoot.position;

                if (towardCamera.sqrMagnitude > 0.001f)
                {
                    visualRoot.rotation = Quaternion.LookRotation(
                        -towardCamera.normalized,
                        cachedCamera.transform.up
                    );
                }
            }

            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;

            visualRoot.localScale = Vector3.one * pulse;
        }

        private void Subscribe()
        {
            if (trigger == null)
                return;

            trigger.Started += OnStarted;
            trigger.ProgressChanged += OnProgressChanged;
            trigger.Cancelled += OnCancelled;
            trigger.Completed += OnCompleted;
        }

        private void Unsubscribe()
        {
            if (trigger == null)
                return;

            trigger.Started -= OnStarted;
            trigger.ProgressChanged -= OnProgressChanged;
            trigger.Cancelled -= OnCancelled;
            trigger.Completed -= OnCompleted;
        }

        private void OnStarted(ProximityInteractor interactor)
        {
            EnsureVisual();
            interactionActive = true;
            SetVisible(true);
            SetProgress(0f);

            // Position immediately so it is correct on the very first visible frame.
            UpdateVisualTransformImmediately();
        }

        private void OnProgressChanged(float normalized, float secondsRemaining)
        {
            if (interactionActive)
                SetProgress(normalized);
        }

        private void OnCancelled(ProximityInteractor interactor)
        {
            interactionActive = false;
            SetProgress(0f);
            SetVisible(false);
        }

        private void OnCompleted(ProximityInteractor interactor)
        {
            SetProgress(1f);
            interactionActive = false;
            SetVisible(false);
        }

        private void UpdateVisualTransformImmediately()
        {
            if (visualRoot == null)
                return;

            Vector3 basePosition = anchor != null ? anchor.position : transform.position;

            visualRoot.position = basePosition + Vector3.up * heightAboveAnchor + worldOffset;
        }

        private void EnsureVisual()
        {
            if (visualRoot != null)
                return;

            GameObject rootObject = new GameObject("ProximityProgressVisual");

            visualRoot = rootObject.transform;
            visualRoot.SetParent(transform, false);

            backgroundLine = CreateLine("Background", backgroundWidth, backgroundColor);

            progressLine = CreateLine("Progress", progressWidth, progressColor);

            DrawFullRing(backgroundLine);
            SetProgress(0f);
        }

        private LineRenderer CreateLine(string objectName, float width, Color color)
        {
            GameObject obj = new GameObject(objectName);

            obj.transform.SetParent(visualRoot, false);

            LineRenderer line = obj.AddComponent<LineRenderer>();

            line.useWorldSpace = false;
            line.loop = false;
            line.alignment = LineAlignment.TransformZ;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.sharedMaterial = GetLineMaterial();

            return line;
        }

        private void DrawFullRing(LineRenderer line)
        {
            int pointCount = segments + 1;
            line.positionCount = pointCount;

            for (int i = 0; i < pointCount; i++)
            {
                float t = i / (float)segments;
                float angle = Mathf.PI * 2f * t;

                line.SetPosition(
                    i,
                    new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f)
                );
            }
        }

        private void SetProgress(float normalized)
        {
            if (progressLine == null)
                return;

            normalized = Mathf.Clamp01(normalized);

            if (normalized <= 0f)
            {
                progressLine.positionCount = 0;
                return;
            }

            int activeSegments = Mathf.Max(1, Mathf.CeilToInt(segments * normalized));

            int pointCount = activeSegments + 1;

            progressLine.positionCount = pointCount;

            for (int i = 0; i < pointCount; i++)
            {
                float t = i / (float)segments;

                float angle = Mathf.PI * 0.5f - Mathf.PI * 2f * t;

                progressLine.SetPosition(
                    i,
                    new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -0.002f)
                );
            }
        }

        private void SetVisible(bool visible)
        {
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(visible);
        }

        private static Material GetLineMaterial()
        {
            if (sharedLineMaterial != null)
                return sharedLineMaterial;

            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                Debug.LogError("No suitable shader found for proximity progress ring.");

                return null;
            }

            sharedLineMaterial = new Material(shader)
            {
                name = "M_Runtime_ProximityProgressRing",
                hideFlags = HideFlags.HideAndDontSave,
            };

            return sharedLineMaterial;
        }
    }
}
