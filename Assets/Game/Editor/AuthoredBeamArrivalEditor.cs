using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Only the Editor generates/validates geometry. Runtime consumes root-local records.
[CustomEditor(typeof(AuthoredBeamArrival)), InitializeOnLoad]
public sealed class AuthoredBeamArrivalEditor : Editor
{
    private static double nextCheck;
    static AuthoredBeamArrivalEditor()
    {
        EditorApplication.update += RefreshChangedScenes;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode && !Application.isBatchMode) ValidateLoadedScenes();
        };
    }
    private static void RefreshChangedScenes()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || EditorApplication.isUpdating || GUIUtility.hotControl != 0 || EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + .75;
        ValidateLoadedScenes();
    }
    private static void ValidateLoadedScenes()
    {
        foreach (var arrival in Object.FindObjectsByType<AuthoredBeamArrival>(FindObjectsInactive.Include))
            if (!EditorUtility.IsPersistent(arrival) && arrival.gameObject.scene.IsValid()
                && !EditorSceneManager.IsPreviewScene(arrival.gameObject.scene) && PrefabStageUtility.GetPrefabStage(arrival.gameObject) == null
                && NeedsValidation(arrival)) Validate(arrival);
    }
    public static string Signature(AuthoredBeamArrival arrival)
    {
        var region = arrival.GetComponent<GameplayTrigger>(); region.RefreshVolumes();
        var data = new SerializedObject(arrival);
        var text = new StringBuilder(arrival.transform.localToWorldMatrix.ToString("R"));
        foreach (string field in new[] {"candidateSpacing","landingYaw","footprintRadius","clearanceHeight"})
            text.Append('|').Append(data.FindProperty(field).floatValue.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
        foreach (var box in region.Volumes)
        {
            if (box == null) continue;
            text.Append('|').Append(box.enabled).Append(box.gameObject.activeInHierarchy)
                .Append(box.transform.localToWorldMatrix.ToString("R")).Append(box.center.ToString("R")).Append(box.size.ToString("R"));
        }
        return Hash128.Compute(text.ToString()).ToString();
    }
    public static bool NeedsValidation(AuthoredBeamArrival arrival) =>
        new SerializedObject(arrival).FindProperty("validationSignature").stringValue != Signature(arrival);
    private static bool HasSceneContext(AuthoredBeamArrival arrival) =>
        !EditorUtility.IsPersistent(arrival) && arrival.gameObject.scene.IsValid()
        && !EditorSceneManager.IsPreviewScene(arrival.gameObject.scene)
        && PrefabStageUtility.GetPrefabStage(arrival.gameObject) == null;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var arrival = (AuthoredBeamArrival)target;
        bool dirty = NeedsValidation(arrival);
        EditorGUILayout.HelpBox(dirty ? "Needs Validation — geometry changed. Editor validation runs automatically after editing."
            : arrival.Candidates.Length == 0 ? "No safe candidates. Enlarge/move the boxes onto clear supported ground, then validate."
            : $"{arrival.Candidates.Length} validated local-space candidates. Child boxes form one trigger and landing region.",
            dirty || arrival.Candidates.Length == 0 ? MessageType.Warning : MessageType.Info);
        EditorGUILayout.HelpBox("Add/remove/edit BoxColliders under Trigger Volumes; no child scripts or lists. Revalidate explicitly after changing nearby level geometry. Resize boxes rather than scaling the prefab root.",MessageType.None);
        if (!HasSceneContext(arrival)) EditorGUILayout.HelpBox("Place this prefab in a gameplay scene to validate its ground and clearance.",MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying || !HasSceneContext(arrival)))
            if (GUILayout.Button("Regenerate / Validate Candidates")) Validate(arrival);
    }
    public static void Validate(AuthoredBeamArrival arrival)
    {
        if (!HasSceneContext(arrival)) return;
        var region = arrival.GetComponent<GameplayTrigger>(); region.RefreshVolumes();
        var data = new SerializedObject(arrival);
        float spacing = Mathf.Max(.5f,data.FindProperty("candidateSpacing").floatValue);
        float radius = data.FindProperty("footprintRadius").floatValue;
        float height = data.FindProperty("clearanceHeight").floatValue;
        Quaternion rotation = Quaternion.Euler(0,arrival.transform.eulerAngles.y+data.FindProperty("landingYaw").floatValue,0);
        var accepted = new List<Vector3>();
        // Configure the whole compound body before queries: a newly added box must
        // never be mistaken for a solid obstacle while earlier boxes are sampled.
        foreach (var box in region.Volumes)
            if (box != null && !box.isTrigger)
            {
                box.isTrigger = true; EditorUtility.SetDirty(box);
                PrefabUtility.RecordPrefabInstancePropertyModifications(box);
            }
        Physics.SyncTransforms();
        foreach (var box in region.Volumes)
        {
            if (box == null || !box.enabled || !box.gameObject.activeInHierarchy) continue;
            int nx = Mathf.Clamp(Mathf.CeilToInt(box.size.x * box.transform.lossyScale.x / spacing),2,48);
            int nz = Mathf.Clamp(Mathf.CeilToInt(box.size.z * box.transform.lossyScale.z / spacing),2,48);
            for (int x=0;x<=nx;x++) for(int z=0;z<=nz;z++)
            {
                Vector3 sample = box.transform.TransformPoint(box.center+new Vector3((x/(float)nx-.5f)*box.size.x,0,(z/(float)nz-.5f)*box.size.z));
                Vector3 rayStart = new(sample.x,box.bounds.max.y+.1f,sample.z);
                if (!Physics.Raycast(rayStart,Vector3.down,out var hit,box.bounds.size.y+.2f,~0,QueryTriggerInteraction.Ignore)) continue;
                Vector3 position = hit.point + Vector3.up*.03f;
                if (!IsSupported(region,position,rotation,radius,height)) continue;
                bool duplicate=false;
                foreach(var previous in accepted) if((previous-position).sqrMagnitude < spacing*spacing*.25f){duplicate=true;break;}
                if(!duplicate && accepted.Count<128) accepted.Add(position);
            }
        }
        if (Vector3.Distance(arrival.transform.lossyScale,Vector3.one)>.001f) accepted.Clear();
        // Derived records must not add an Undo step that automatic validation would
        // immediately redo, trapping the designer above their actual box/root edit.
        var list=data.FindProperty("landingCandidates"); list.arraySize=accepted.Count;
        for(int i=0;i<accepted.Count;i++)
        {
            var record=list.GetArrayElementAtIndex(i);
            record.FindPropertyRelative("localPosition").vector3Value=arrival.transform.InverseTransformPoint(accepted[i]);
            record.FindPropertyRelative("localRotation").quaternionValue=Quaternion.Inverse(arrival.transform.rotation)*rotation;
        }
        data.FindProperty("validatedRadius").floatValue=radius;
        data.FindProperty("validatedHeight").floatValue=height;
        data.FindProperty("validationSignature").stringValue=Signature(arrival);
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(arrival);
        PrefabUtility.RecordPrefabInstancePropertyModifications(arrival);
        GameplayAuthoringSetup.PrepareIdentities(arrival.gameObject);
        EditorSceneManager.MarkSceneDirty(arrival.gameObject.scene);
    }
    private static bool IsSupported(GameplayTrigger region,Vector3 position,Quaternion rotation,float radius,float height)
    {
        for(int x=-2;x<=2;x++) for(int z=-2;z<=2;z++)
        {
            Vector3 sample=position+rotation*new Vector3(x*radius*.5f,0,z*radius*.5f);
            if(!region.ContainsPoint(sample+Vector3.up*.1f)) return false;
            if(!Physics.Raycast(sample+Vector3.up*.3f,Vector3.down,out var hit,.6f,~0,QueryTriggerInteraction.Ignore)
                || Mathf.Abs(hit.point.y-(position.y-.03f))>.1f || Vector3.Angle(hit.normal,Vector3.up)>8) return false;
        }
        return !Physics.CheckBox(position+Vector3.up*(height*.5f+.15f),new Vector3(radius,height*.5f,radius),rotation,~0,QueryTriggerInteraction.Ignore);
    }
    private void OnSceneGUI()
    {
        var arrival=(AuthoredBeamArrival)target;
        var region=arrival.GetComponent<GameplayTrigger>(); region.RefreshVolumes();
        Handles.color=new Color(0,1,1,.7f);
        foreach(var box in region.Volumes)
            if(box!=null) using(new Handles.DrawingScope(box.transform.localToWorldMatrix)) Handles.DrawWireCube(box.center,box.size);
        bool dirty=NeedsValidation(arrival);
        for(int i=0;i<arrival.Candidates.Length;i++)
        {
            Handles.color=dirty?Color.yellow:(arrival.SelectedIndex==i?Color.white:Color.green);
            Handles.DrawWireDisc(arrival.CandidatePosition(i),Vector3.up,.25f);
        }
    }
}
