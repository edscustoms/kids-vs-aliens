using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(AlienBikeController))]
public sealed class AlienBikeControllerEditor : Editor
{
    private void OnEnable()
    {
        var bike = target as AlienBikeController;
        if (bike != null && !Application.isPlaying && !EditorUtility.IsPersistent(bike))
            GameplayAuthoringSetup.PrepareIdentities(bike.gameObject);
    }
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var configured = (AlienBikeController)target;
        if (configured.mountApproaches != null && configured.mountApproaches.Length > AlienBikeController.MaxMountApproaches)
            EditorGUILayout.HelpBox("Use at most eight approaches, ordered around the hull.", MessageType.Error);
        if (Application.isPlaying || EditorUtility.IsPersistent(target))
            return;
        if (GUILayout.Button("Snap parked bike to ground"))
        {
            var bike = (AlienBikeController)target;
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(bike.transform.position + Vector3.up * 3, Vector3.down, 8, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Vector3 point = default;
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(bike.transform) || hit.distance >= nearest || hit.normal.y < .82f)
                    continue;
                nearest = hit.distance;
                point = hit.point;
            }
            if (float.IsPositiveInfinity(nearest))
            {
                Debug.LogError("No stable ground below bike.", bike);
                return;
            }
            Undo.RecordObject(bike.transform, "Snap bike to ground");
            bike.transform.position = point + Vector3.up * bike.hoverHeight;
            PrefabUtility.RecordPrefabInstancePropertyModifications(bike.transform);
            EditorSceneManager.MarkSceneDirty(bike.gameObject.scene);
        }
    }
    [DrawGizmo(GizmoType.Selected)]
    private static void DrawMounting(AlienBikeController bike, GizmoType type)
    {
        Handles.color = Color.cyan;
        Handles.DrawWireDisc(bike.transform.position - Vector3.up * bike.hoverHeight, Vector3.up, bike.approachRange);
        if (bike.mountApproaches == null)
            return;
        for (int i = 0; i < bike.mountApproaches.Length; i++)
        {
            var a = bike.mountApproaches[i];
            var b = bike.mountApproaches[(i + 1) % bike.mountApproaches.Length];
            if (a.approachPoint == null)
                continue;
            if (b.approachPoint != null)
                Handles.DrawDottedLine(a.approachPoint.position, b.approachPoint.position, 4);
            if (a.mountPoint != null)
                Handles.DrawLine(a.approachPoint.position, a.mountPoint.position);
        }
    }
}

[CustomEditor(typeof(AlienFlybyPath))]
public sealed class AlienFlybyPathEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var path = (AlienFlybyPath)target;
        EditorGUILayout.HelpBox(path.IsValidated ? "Baked full-volume route. Revalidate after environment changes." : "Route is not validated and cannot play.", path.IsValidated ? MessageType.Info : MessageType.Warning);
        if (GUILayout.Button("Validate route"))
        {
            var owner = path.GetComponentInParent<AlienFlybyController>();
            if (owner == null || !AlienFlybyValidation.Validate(path, owner.environmentRoots, out string failure))
                Debug.LogError(owner == null ? "Route needs an AlienFlybyController parent." : "Route intersects the authored environment; move its controls or use Raise Selected Flyby Routes To Clearance.", path);
            EditorSceneManager.MarkSceneDirty(path.gameObject.scene);
        }
    }
}
