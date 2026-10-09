using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KidsVsAliens.Environment;
using Object = UnityEngine.Object;

/// <summary>Explicit local exit authoring; existing level poses and baked data stay unchanged.</summary>
public static class SceneCrossoverAuthoring
{
    const string Folder = "Assets/Game/Scenes/BikeRoute/Crossover";
    [MenuItem("Tools/Level Authoring/Configure ConstructionSite Beam Exit")]
    public static void Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        var crossover = Object.FindAnyObjectByType<SceneCrossover>();
        var stop = crossover.transform.Find("Gate loading boundary");
        if (stop != null) Object.DestroyImmediate(stop.gameObject);
        var geometry = Object.FindAnyObjectByType<CameraOcclusionController>().transform;
        var passage = geometry.Find("Exit transport passage");
        if (passage != null) Object.DestroyImmediate(passage.gameObject);
        passage = new GameObject("Exit transport passage").transform;
        passage.SetParent(geometry, false);
        // Continue the two existing outer gate runs; no end fence or boxed pad.
        Physics.SyncTransforms();
        foreach (string name in new[] { "Fence_Perimeter_TopLeft_20 (1)", "Fence_Perimeter_TopLeft_20 (5)" })
        {
            var adjoining = GameObject.Find(name);
            if (adjoining == null) throw new InvalidOperationException("Missing authored gate fence: " + name);
            var edge = adjoining.GetComponent<Collider>().bounds;
            float start = Mathf.Max(edge.max.z, adjoining.GetComponent<Renderer>().bounds.max.z);
            var from = Ground(new Vector3(adjoining.GetComponent<Renderer>().bounds.center.x, 0, start));
            var to = Ground(new Vector3(from.x, 0, start + 10));
            for (int i = 0; i < 3; i++)
            {
                var a = Vector3.Lerp(from, to, i / 3f);
                var b = Vector3.Lerp(from, to, (i + 1) / 3f);
                Fence(passage, adjoining, a, b);
            }
        }
        var recovery = crossover.transform.Find("Departure recovery");
        if (recovery == null) { recovery = new GameObject("Departure recovery").transform; recovery.SetParent(crossover.transform, false); }
        recovery.SetPositionAndRotation(Ground(new Vector3(31.425f,0,57.2f)) + Vector3.up * .08f, Quaternion.identity);
        var triggerTransform = crossover.transform.Find("Excavator exit handoff");
        if (triggerTransform == null) { triggerTransform = new GameObject("Excavator exit handoff", typeof(BoxCollider)).transform; triggerTransform.SetParent(crossover.transform, false); }
        var volume = triggerTransform.GetComponent<BoxCollider>();
        volume.center = new Vector3(31.425f,1,58.85f); volume.size = new Vector3(10.05f,4,1.5f); volume.isTrigger = true;
        var trigger = triggerTransform.GetComponent<GameplayTrigger>() ?? triggerTransform.gameObject.AddComponent<GameplayTrigger>();
        trigger.GetComponent<RunWorldObject>().ConfigureIdentity("construction-exit-transport");
        var action = new SerializedObject(trigger);
        action.FindProperty("behavior").enumValueIndex = (int)GameplayTriggerBehavior.OneShot;
        // Departure is transient. Source Continue returns to the safe pad with this visit available.
        action.FindProperty("rememberOneShot").boolValue = false;
        action.FindProperty("actions.actionTarget").objectReferenceValue = crossover;
        action.ApplyModifiedPropertiesWithoutUndo();
        var message = AssetDatabase.LoadAssetAtPath<DialogueMessage>(Folder + "/BikeRouteArrival.asset");
        if (message == null) { message = ScriptableObject.CreateInstance<DialogueMessage>(); AssetDatabase.CreateAsset(message, Folder + "/BikeRouteArrival.asset"); }
        var line = new DialogueMessage.Line { text = "Okay, now I need to get to school. Thanks for the lift." };
        message.girl.lines = new[] { line }; message.boy.lines = new[] { line };
        EditorUtility.SetDirty(message); AssetDatabase.SaveAssetIfDirty(message);
        var settings = new SerializedObject(crossover);
        settings.FindProperty("departureRecoveryPoint").objectReferenceValue = recovery;
        settings.FindProperty("arrivalDialogue").objectReferenceValue = message;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var flybys = Object.FindAnyObjectByType<AlienFlybyController>();
        foreach (var path in flybys.GetComponentsInChildren<AlienFlybyPath>(true))
            if (!AlienFlybyValidation.Validate(path, flybys.environmentRoots, out string failure)) throw new InvalidOperationException(failure);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Authored 10m fenced exit and existing one-shot action; original scene coordinates retained.");
    }
    static Vector3 Ground(Vector3 point)
    {
        // Read accepted ground colliders; never reshape terrain or bake new paths.
        var hits = Physics.RaycastAll(point + Vector3.up * 8, Vector3.down, 12, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits.OrderBy(h => h.distance))
            if (hit.collider is TerrainCollider) return hit.point;
        throw new InvalidOperationException("No authored terrain under exit fencing: " + point);
    }
    static void Fence(Transform parent, GameObject adjoining, Vector3 a, Vector3 b)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(adjoining));
        var fence = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        fence.name = "Exit fence"; fence.transform.SetParent(parent, false);
        var bounds = fence.GetComponent<MeshFilter>().sharedMesh.bounds;
        var scale = adjoining.transform.localScale;
        // Imported mesh axes: Y is span, Z is height, X is thickness.
        scale.y = Vector3.Distance(a,b) / bounds.size.y;
        var direction = (b-a).normalized;
        var forward = Vector3.Cross(direction, Vector3.up).normalized;
        var up = Vector3.Cross(forward, direction).normalized;
        var rotation = Quaternion.FromToRotation(Vector3.forward, direction) * adjoining.transform.rotation;
        fence.transform.localScale = scale;
        // This imported fence uses local Z for its height. Seat its feet on the sampled slope.
        Vector3 center = (a+b)*.5f + up * (bounds.size.z * Mathf.Abs(scale.z) * .5f);
        fence.transform.SetPositionAndRotation(center - rotation * Vector3.Scale(bounds.center,scale), rotation);
        var collider = fence.GetComponent<BoxCollider>();
        collider.center = bounds.center; collider.size = bounds.size;
        // Adjacent bounds meet exactly at their end planes, with no overlapping barrier volumes.
        PrefabUtility.RecordPrefabInstancePropertyModifications(fence.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
    }
}
