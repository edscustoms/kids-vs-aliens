using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Explicit, narrow content migration. Does not rebuild controllers, clips, or scenes.
public static class MeleePrecisionAuthoring
{
    public static void SetContact(SerializedProperty binding, CharacterActionId action)
    {
        var shape=binding.FindPropertyRelative("meleeContact");
        bool kick=action==CharacterActionId.Kick||action==CharacterActionId.HeavyKick;
        bool left=action==CharacterActionId.MeleeLight1||action==CharacterActionId.MeleeLight3;
        shape.FindPropertyRelative("bone").intValue=(int)(kick?HumanBodyBones.RightToes:left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);
        shape.FindPropertyRelative("radius").floatValue=kick?.10f:.09f;
    }
    public static void Apply()
    {
        var actions=new SerializedObject(AssetDatabase.LoadAssetAtPath<CharacterAnimationActions>(CharacterAnimationSetup.ActionsPath));
        var bindings=actions.FindProperty("bindings");
        for(int i=0;i<bindings.arraySize;i++)
        {
            var binding=bindings.GetArrayElementAtIndex(i);var action=(CharacterActionId)binding.FindPropertyRelative("action").intValue;
            if(action==CharacterActionId.Kick||action>=CharacterActionId.MeleeLight1)SetContact(binding,action);
        }
        actions.ApplyModifiedPropertiesWithoutUndo();
        var kick=FightingAnimationAuthoring.Clip("Kick");
        var kickSelection=FightingAnimationAuthoring.Attacks.Single(a=>a.name=="Kick");
        var kickEvents=AnimationUtility.GetAnimationEvents(kick);
        foreach(var marker in kickEvents)
            if(marker.functionName==nameof(CharacterAnimationEventRelay.OnCharacterAnimationEvent)&&marker.intParameter==(int)CharacterAnimationEventId.MeleeImpact)
                marker.time=(kickSelection.impact-kickSelection.start)/kickSelection.speed;
        AnimationUtility.SetAnimationEvents(kick,kickEvents);
        EditorUtility.SetDirty(kick);
        const string path="Assets/Game/Animations/Enemy/Attack/Right_Hook.fbx";
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);var clips=importer.clipAnimations;
        foreach(var clip in clips)
        {
            var events=clip.events.Where(e=>e.functionName!=nameof(CharacterAnimationEventRelay.OnCharacterAnimationEvent)||e.intParameter!=(int)CharacterAnimationEventId.MeleeImpact).ToList();
            // Importer event times are normalized: first right-hook extension, measured in the encounter lab.
            events.Add(new AnimationEvent{time=.26f,functionName=nameof(CharacterAnimationEventRelay.OnCharacterAnimationEvent),intParameter=(int)CharacterAnimationEventId.MeleeImpact});
            clip.events=events.OrderBy(e=>e.time).ToArray();
        }
        importer.clipAnimations=clips;importer.SaveAndReimport();
        const string prefab="Assets/Game/Prefabs/Enemies/PF_Enemy_Melee_POC_V1.prefab";
        var root=PrefabUtility.LoadPrefabContents(prefab);
        try
        {
            var animator=root.GetComponentInChildren<Animator>(true);
            if(animator.GetComponent<CharacterAnimationEventRelay>()==null)animator.gameObject.AddComponent<CharacterAnimationEventRelay>();
            var melee=new SerializedObject(root.GetComponent<EnemyMeleeAttack>());
            melee.FindProperty("attackRange").floatValue=.78f;melee.ApplyModifiedPropertiesWithoutUndo();
            var approach=new SerializedObject(root.GetComponent<EnemyApproachPlanner>());
            approach.FindProperty("baseRadius").floatValue=.65f;approach.FindProperty("radiusJitter").floatValue=.03f;approach.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<NavMeshAgent>().stoppingDistance=.02f;
            PrefabUtility.SaveAsPrefabAsset(root,prefab);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }
}
