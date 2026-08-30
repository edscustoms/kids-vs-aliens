using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KidsVsAliens.EditorTools.ModularLevelKit
{
    public sealed class ModularSnapWindow : EditorWindow
    {
        private bool requireMatchingChannel = true;

        private bool showSocketGizmos = false;
        private bool showOffsetSlots = false;
        private bool showSocketLabels = false;
        private float gizmoSize = 0.16f;

        [SerializeField]
        private List<GameObject> targetObjects = new List<GameObject>();

        [MenuItem("Tools/Kids VS Aliens/Level Tools/Modular Snap")]
        public static void Open()
        {
            GetWindow<ModularSnapWindow>("Modular Snap");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawSceneSockets;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneSockets;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField(
                "Modular Level Builder",
                EditorStyles.boldLabel);

            DrawTargetSection();
            DrawBoundarySelectionSection();
            DrawAlignmentSurfaceSection();
            DrawSocketSection();
            DrawDisplaySection();

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Frame Active Selection"))
                SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void DrawTargetSection()
        {
            EditorGUILayout.HelpBox(
                "Shared target workflow:\n" +
                "1) Select the reference/target however you want.\n" +
                "2) Set Target From Selection.\n" +
                "3) Select the objects that should move however you want.\n" +
                "4) Run Boundary Snap / Align / Surface Snap.\n\n" +
                "Box select, Shift-select and Hierarchy selection all work.",
                MessageType.Info);

            EditorGUILayout.LabelField(
                "Target",
                EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        "Set Target From Selection",
                        GUILayout.Height(30)))
                {
                    SetTargetFromSelection();
                }

                if (GUILayout.Button(
                        "Clear",
                        GUILayout.Width(60),
                        GUILayout.Height(30)))
                {
                    targetObjects.Clear();
                    Repaint();
                }
            }

            List<GameObject> resolvedTargets =
                ResolveTargetObjects();

            string targetText =
                resolvedTargets.Count == 0
                    ? "Target: not set"
                    : $"Target: {resolvedTargets.Count} object(s)";

            EditorGUILayout.LabelField(
                targetText,
                EditorStyles.miniBoldLabel);
        }

        private void DrawBoundarySelectionSection()
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField(
                "Boundary / Chunk Snap",
                EditorStyles.boldLabel);

            List<GameObject> resolvedTargets =
                ResolveTargetObjects();

            using (new EditorGUI.DisabledScope(
                       resolvedTargets.Count == 0 ||
                       Selection.gameObjects.Length == 0))
            {
                if (GUILayout.Button(
                        "Snap Moving Selection To Target",
                        GUILayout.Height(32)))
                {
                    SnapCurrentSelectionToTarget();
                }
            }

            EditorGUILayout.LabelField(
                "Uses the actual exposed floor boundary. Good for floor chunks, parent groups or multiple loose selected floor pieces.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawAlignmentSurfaceSection()
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField(
                "Align / Surface",
                EditorStyles.boldLabel);

            List<GameObject> resolvedTargets =
                ResolveTargetObjects();

            bool disabled =
                resolvedTargets.Count == 0 ||
                Selection.gameObjects.Length == 0;

            using (new EditorGUI.DisabledScope(disabled))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Align X"))
                    {
                        AlignCurrentSelection(
                            ModularAlignmentUtility.WorldAxis.X);
                    }

                    if (GUILayout.Button("Align Y"))
                    {
                        AlignCurrentSelection(
                            ModularAlignmentUtility.WorldAxis.Y);
                    }

                    if (GUILayout.Button("Align Z"))
                    {
                        AlignCurrentSelection(
                            ModularAlignmentUtility.WorldAxis.Z);
                    }
                }

                if (GUILayout.Button(
                        "Snap Bottom To Target Surface",
                        GUILayout.Height(32)))
                {
                    SnapCurrentSelectionBottomToSurface();
                }
            }

            EditorGUILayout.LabelField(
                "Align X/Y/Z aligns world pivots to the stored target reference. " +
                "Bottom Surface uses actual object bounds + target collider surface, so poles/props sit on the ground instead of placing their pivot on it.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawSocketSection()
        {
            EditorGUILayout.Space(12);

            EditorGUILayout.LabelField(
                "Socket Snap",
                EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                "Keep this for walls, stairs, ramps and exact prefab-to-prefab work.",
                EditorStyles.wordWrappedMiniLabel);

            requireMatchingChannel =
                EditorGUILayout.ToggleLeft(
                    "Require matching socket channel",
                    requireMatchingChannel);

            if (GUILayout.Button(
                    "Smart Socket Snap",
                    GUILayout.Height(28)))
            {
                SnapSelectedModules();
            }

            if (GUILayout.Button(
                    "Snap Exact Selected Sockets",
                    GUILayout.Height(24)))
            {
                SnapSelectedSockets();
            }
        }

        private void DrawDisplaySection()
        {
            EditorGUILayout.Space(12);

            EditorGUILayout.LabelField(
                "Scene Display",
                EditorStyles.boldLabel);

            showSocketGizmos =
                EditorGUILayout.ToggleLeft(
                    "Show selected module sockets",
                    showSocketGizmos);

            using (new EditorGUI.DisabledScope(!showSocketGizmos))
            {
                showOffsetSlots =
                    EditorGUILayout.ToggleLeft(
                        "Show offset slots",
                        showOffsetSlots);

                showSocketLabels =
                    EditorGUILayout.ToggleLeft(
                        "Show socket names",
                        showSocketLabels);

                gizmoSize =
                    EditorGUILayout.Slider(
                        "Socket gizmo size",
                        gizmoSize,
                        0.05f,
                        0.4f);
            }
        }

        private void SetTargetFromSelection()
        {
            GameObject[] selected =
                Selection.gameObjects;

            if (selected == null || selected.Length == 0)
            {
                ShowNotification(
                    new GUIContent(
                        "Select target pieces or a target parent first."));
                return;
            }

            targetObjects =
                ModularSnapUtility.GetTopLevelObjects(selected);

            ShowNotification(
                new GUIContent(
                    $"Target stored: {targetObjects.Count} object(s)."));

            Repaint();
        }

        private List<GameObject> ResolveTargetObjects()
        {
            targetObjects =
                targetObjects
                    .Where(go => go != null)
                    .Distinct()
                    .ToList();

            return new List<GameObject>(targetObjects);
        }

        private bool TryGetMovingAndTarget(
            out List<GameObject> moving,
            out List<GameObject> target)
        {
            target =
                ResolveTargetObjects();

            moving =
                ModularSnapUtility.GetTopLevelObjects(
                    Selection.gameObjects);

            if (target.Count == 0)
            {
                ShowNotification(
                    new GUIContent("Target is not set."));
                return false;
            }

            if (moving.Count == 0)
            {
                ShowNotification(
                    new GUIContent(
                        "Select the object(s) that should move."));
                return false;
            }

            return true;
        }

        private void SnapCurrentSelectionToTarget()
        {
            if (!TryGetMovingAndTarget(
                    out List<GameObject> moving,
                    out List<GameObject> target))
            {
                return;
            }

            if (!ModularSnapUtility.TrySnapSelectionBoundary(
                    moving,
                    target,
                    out string message))
            {
                ShowNotification(
                    new GUIContent(message));
                return;
            }

            ShowNotification(
                new GUIContent("Boundary snap complete."));

            SceneView.RepaintAll();
        }

        private void AlignCurrentSelection(
            ModularAlignmentUtility.WorldAxis axis)
        {
            if (!TryGetMovingAndTarget(
                    out List<GameObject> moving,
                    out List<GameObject> target))
            {
                return;
            }

            if (!ModularAlignmentUtility.AlignSelectionToTargetPivot(
                    moving,
                    target,
                    axis,
                    out string message))
            {
                ShowNotification(
                    new GUIContent(message));
                return;
            }

            ShowNotification(
                new GUIContent(message));

            SceneView.RepaintAll();
        }

        private void SnapCurrentSelectionBottomToSurface()
        {
            if (!TryGetMovingAndTarget(
                    out List<GameObject> moving,
                    out List<GameObject> target))
            {
                return;
            }

            if (!ModularAlignmentUtility.SnapBottomToTargetSurface(
                    moving,
                    target,
                    out string message))
            {
                ShowNotification(
                    new GUIContent(message));
                return;
            }

            ShowNotification(
                new GUIContent(message));

            SceneView.RepaintAll();
        }

        private void SnapSelectedModules()
        {
            GameObject[] selected =
                Selection.gameObjects;

            if (selected.Length != 2 ||
                Selection.activeGameObject == null)
            {
                ShowNotification(
                    new GUIContent(
                        "Select exactly 2 module roots. Active/last-selected moves."));
                return;
            }

            Transform movingRoot =
                Selection.activeGameObject.transform;

            Transform targetRoot =
                selected
                    .First(go => go != Selection.activeGameObject)
                    .transform;

            if (!ModularSnapUtility.TryFindClosestCompatiblePair(
                    movingRoot,
                    targetRoot,
                    requireMatchingChannel,
                    out ModularSnapUtility.SocketPair pair))
            {
                ShowNotification(
                    new GUIContent(
                        "No compatible Snap_* sockets found."));
                return;
            }

            ModularSnapUtility.SnapModule(
                movingRoot,
                pair.moving,
                pair.target);

            SceneView.RepaintAll();
        }

        private void SnapSelectedSockets()
        {
            Transform[] selectedSockets =
                Selection.transforms;

            if (selectedSockets.Length != 2 ||
                Selection.activeTransform == null)
            {
                ShowNotification(
                    new GUIContent(
                        "Select exactly 2 Snap_* transforms."));
                return;
            }

            Transform movingSocket =
                Selection.activeTransform;

            Transform targetSocket =
                selectedSockets.First(
                    transform => transform != movingSocket);

            if (!ModularSnapUtility.IsSocket(movingSocket) ||
                !ModularSnapUtility.IsSocket(targetSocket))
            {
                ShowNotification(
                    new GUIContent(
                        "Both selections must be Snap_* transforms."));
                return;
            }

            if (requireMatchingChannel &&
                !string.Equals(
                    ModularSnapUtility.GetChannel(movingSocket.name),
                    ModularSnapUtility.GetChannel(targetSocket.name),
                    System.StringComparison.OrdinalIgnoreCase))
            {
                ShowNotification(
                    new GUIContent(
                        "Socket channels do not match."));
                return;
            }

            Transform movingRoot =
                ModularSnapUtility.GetModuleRootForSocket(
                    movingSocket);

            if (movingRoot == null)
            {
                ShowNotification(
                    new GUIContent(
                        "Could not determine the moving module root."));
                return;
            }

            ModularSnapUtility.SnapModule(
                movingRoot,
                movingSocket,
                targetSocket);

            Selection.activeGameObject =
                movingRoot.gameObject;

            SceneView.RepaintAll();
        }

        private void DrawSceneSockets(
            SceneView sceneView)
        {
            if (!showSocketGizmos)
                return;

            foreach (GameObject selected in
                     Selection.gameObjects)
            {
                if (selected == null)
                    continue;

                foreach (Transform child in selected.transform)
                {
                    if (!ModularSnapUtility.IsSocket(child))
                        continue;

                    bool isSlot =
                        child.name.Contains("_Slot_");

                    if (isSlot && !showOffsetSlots)
                        continue;

                    float size =
                        HandleUtility.GetHandleSize(child.position) *
                        gizmoSize;

                    Handles.color =
                        isSlot
                            ? new Color(
                                0.2f,
                                0.85f,
                                1f,
                                0.5f)
                            : Color.cyan;

                    Handles.SphereHandleCap(
                        0,
                        child.position,
                        Quaternion.identity,
                        isSlot
                            ? size * 0.18f
                            : size * 0.32f,
                        EventType.Repaint);

                    if (!isSlot)
                    {
                        Handles.ArrowHandleCap(
                            0,
                            child.position,
                            child.rotation,
                            size * 0.85f,
                            EventType.Repaint);
                    }

                    if (showSocketLabels)
                    {
                        Handles.Label(
                            child.position +
                            Vector3.up * size * 0.35f,
                            child.name);
                    }
                }
            }
        }
    }
}
