using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class GameplayAuthoringSetup
{
    public const string EncounterPath = "Assets/Game/Prefabs/Environment/PF_AuthoredEncounter.prefab";
    public const string TriggerPath = "Assets/Game/Prefabs/Environment/PF_GameplayTrigger.prefab";
    public const string ObjectivePath = "Assets/Game/Data/Objectives/CS_FindWayOut.asset";
    public const string MessagePath = "Assets/Game/Dialogue/ConstructionSite/CS_Intro_FindWayOut.asset";
    public const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    const string MaterialPath = "Assets/Game/Dialogue/Shared/DialogueText.mat";

    public static void ConfigureScene(PlayerCharacter player, GameObject presentation)
    {
        var owners = player.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ObjectiveController>(true)).ToArray();
        if (owners.Length > 1) throw new InvalidOperationException("Multiple objective owners: resolve before repair.");
        var speakers = player.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DialoguePlayer>(true)).ToArray();
        if (speakers.Length > 1) throw new InvalidOperationException("Multiple dialogue owners: resolve before repair.");
        if (owners.Length == 1 && speakers.Length == 1 && owners[0].gameObject != speakers[0].gameObject)
            throw new InvalidOperationException("Objective and dialogue owners must share GameplayCommunication before repair.");
        var root = owners.Length == 1 ? owners[0].gameObject
            : speakers.Length == 1 ? speakers[0].gameObject : new GameObject("GameplayCommunication");
        if (owners.Length == 0 && speakers.Length == 0)
        {
            Undo.RegisterCreatedObjectUndo(root,"Add gameplay communication");
            SceneManager.MoveGameObjectToScene(root,player.gameObject.scene);
        }
        var objectives = root.GetComponent<ObjectiveController>() ?? Undo.AddComponent<ObjectiveController>(root);
        var dialogue = root.GetComponent<DialoguePlayer>() ?? Undo.AddComponent<DialoguePlayer>(root);
        var source = dialogue.GetComponent<AudioSource>();
        if (source.playOnAwake) { Undo.RecordObject(source,"Configure dialogue source"); source.playOnAwake=false; }
        var view = presentation.GetComponent<GameplayCommunicationView>() ?? Undo.AddComponent<GameplayCommunicationView>(presentation);
        Missing(view,"objectives",objectives); Missing(view,"dialogue",dialogue);
        Missing(view,"readableFont",AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath));
        Missing(view,"textMaterial",AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
        PrepareIdentities(root);
    }

    public static void PrepareIdentities(GameObject root)
    {
        if (EditorUtility.IsPersistent(root) || PrefabStageUtility.GetPrefabStage(root) != null || !root.scene.IsValid()) return;
        var peers = root.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RunWorldObject>(true)).ToArray();
        foreach(var entity in root.GetComponentsInChildren<RunWorldObject>(true))
        {
            if (!string.IsNullOrEmpty(entity.Id) && !peers.Any(p=>p!=entity && p.Id==entity.Id)) continue;
            Undo.RecordObject(entity,"Assign scene run identity"); entity.ConfigureIdentity(Guid.NewGuid().ToString("N"));
            EditorUtility.SetDirty(entity); PrefabUtility.RecordPrefabInstancePropertyModifications(entity);
        }
    }

    public static GameObject AddMember(AuthoredEncounter encounter, GameObject prefab)
    {
        if (prefab == null || prefab.GetComponent<EnemyBrain>() == null || prefab.GetComponent<EnemyHealth>() == null)
            throw new ArgumentException("Choose a reusable enemy prefab with EnemyBrain and EnemyHealth.");
        var member = (GameObject)PrefabUtility.InstantiatePrefab(prefab,encounter.transform);
        Undo.RegisterCreatedObjectUndo(member,"Add authored encounter member");
        member.transform.localPosition=Vector3.zero; member.SetActive(false);
        if (member.GetComponent<RunWorldObject>()==null) Undo.AddComponent<RunWorldObject>(member);
        var data = new SerializedObject(encounter); var list=data.FindProperty("enemies");
        int index=list.arraySize++; list.GetArrayElementAtIndex(index).objectReferenceValue=member.GetComponent<RunWorldObject>();
        data.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(member);
        PrepareIdentities(encounter.gameObject);
        return member;
    }

    private static void Missing(Object owner,string field,Object value)
    {
        if(owner==null||value==null)return;
        var data=new SerializedObject(owner); var property=data.FindProperty(field);
        if(property.objectReferenceValue!=null)return;
        property.objectReferenceValue=value; data.ApplyModifiedProperties(); EditorUtility.SetDirty(owner);
        if(PrefabUtility.IsPartOfPrefabInstance(owner))PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
    }
}
