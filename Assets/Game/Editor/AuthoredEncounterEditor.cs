using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AuthoredEncounter))]
public sealed class AuthoredEncounterEditor : Editor
{
    private GameObject memberPrefab;
    private void OnEnable()
    {
        if(!Application.isPlaying) GameplayAuthoringSetup.PrepareIdentities(((AuthoredEncounter)target).gameObject);
        memberPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Enemies/PF_Enemy_Melee_POC_V1.prefab");
    }
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var encounter=(AuthoredEncounter)target;
        EditorGUILayout.HelpBox("Activate On Entry uses the BoxCollider trigger. Disable it for mission/chest activation. Move dormant enemy children in Scene view; each member keeps EnemyEquipment and its combat profile.",MessageType.Info);
        using(new EditorGUI.DisabledScope(Application.isPlaying))
        {
            memberPrefab=(GameObject)EditorGUILayout.ObjectField("Enemy prefab / archetype",memberPrefab,typeof(GameObject),false);
            if(GUILayout.Button("Add dormant enemy member")&&memberPrefab!=null) GameplayAuthoringSetup.AddMember(encounter,memberPrefab);
            foreach(var member in encounter.Enemies)
            {
                if(member==null)continue;
                EditorGUILayout.LabelField(member.name,EditorStyles.boldLabel);
                EditorGUILayout.ObjectField("Placement / facing",member.transform,typeof(Transform),true);
                var equipment=member.GetComponent<EnemyEquipment>();
                if(equipment==null)continue;
                var data=new SerializedObject(equipment); data.Update();
                EditorGUILayout.PropertyField(data.FindProperty("profile"),new GUIContent("Combat profile"));
                EditorGUILayout.PropertyField(data.FindProperty("startingWeapon"),new GUIContent("Starting weapon (None = unarmed)"));
                if(data.ApplyModifiedProperties())PrefabUtility.RecordPrefabInstancePropertyModifications(equipment);
                var weapon=(WeaponItemData)data.FindProperty("startingWeapon").objectReferenceValue;
                var profile=(EnemyCombatProfile)data.FindProperty("profile").objectReferenceValue;
                if(weapon!=null&&profile!=null&&!profile.Allows(weapon))EditorGUILayout.HelpBox("This combat profile rejects the weapon. Select an existing ranged-enabled profile (AlienCombatV1) for armed members.",MessageType.Warning);
                if(member.gameObject.activeSelf)EditorGUILayout.HelpBox("Encounter members must be inactive before the encounter starts.",MessageType.Warning);
            }
        }
    }
}

[CustomEditor(typeof(GameplayTrigger))]
public sealed class GameplayTriggerEditor : Editor
{
    private void OnEnable() { if(!Application.isPlaying) GameplayAuthoringSetup.PrepareIdentities(((GameplayTrigger)target).gameObject); }
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Edit the BoxCollider visually. Repeatable fires once per visit. None/empty actions and conditions are valid. Dialogue references open the exact text/audio asset. Scene identities are prepared when inspected; canonical scene repair also validates them.",MessageType.Info);
    }
}
