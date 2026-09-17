using System.Linq;
using StarterAssets;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BeamTransportSetup
{
    public const string SkillPath = "Assets/Game/Data/Progression/BeamHoist.asset";
    public const string BookPath = "Assets/Game/Data/Items/KnowledgeBooks/BeamHoistBook.asset";
    public const string BookPrefabPath = "Assets/Game/Prefabs/Items/KnowledgeBooks/BeamHoistBook_Dropped.prefab";
    public const string VfxPath = "Assets/Game/Prefabs/PF_BeamTransportVFX.prefab";

    public const string LevelStartPath = "Assets/Game/Prefabs/PF_LevelStart.prefab";

    public static void ConfigureScene(PlayerCharacter player)
    {
        CreateKnowledgeAssets();
        var controller = Component<BeamTransportController>(player.gameObject);
        var ability = Component<BeamHoistAbility>(player.gameObject);
        SetMissing(ability, "requiredSkill", AssetDatabase.LoadAssetAtPath<SkillData>(SkillPath));
        var canonical = AssetDatabase.LoadAssetAtPath<BeamTransportVFX>(VfxPath);
        Set(controller, "vfxPrefab", canonical);
        Scene scene = player.gameObject.scene;
        var roots = scene.GetRootGameObjects();
        var sequences = roots.SelectMany(r => r.GetComponentsInChildren<PlayerBeamInSequence>(true)).ToArray();
        if (sequences.Length > 1) throw new System.InvalidOperationException("Multiple level arrival owners; remove the unintended duplicate before repair.");
        GameObject level = sequences.Length == 1 ? sequences[0].gameObject
            : roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "LevelStart" || t.name == "PF_LevelStart")?.gameObject;
        if (level == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelStartPath);
            if (prefab == null) throw new System.InvalidOperationException("Create PF_LevelStart with the authored LevelStart migration first.");
            level = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(level, "Add LevelStart");
            level.name = "LevelStart";
            level.transform.SetPositionAndRotation(player.transform.position, player.transform.rotation);
            // Only a newly created template is aligned: its marker, not its root, is the landing pose.
            var templateMarker = level.GetComponentsInChildren<Transform>(true).First(t => t.name == "BeamInSpawn");
            level.transform.position += player.transform.position - templateMarker.position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(level.transform);
        }
        var sequence = Component<PlayerBeamInSequence>(level);
        var start = level.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BeamInSpawn");
        if (start == null)
        {
            var marker = new GameObject("BeamInSpawn");
            Undo.RegisterCreatedObjectUndo(marker, "Repair arrival marker");
            marker.transform.SetParent(level.transform, false);
            start = marker.transform;
        }
        var effect = level.GetComponentInChildren<BeamTransportVFX>(true);
        if (effect == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(canonical.gameObject, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Repair canonical beam reference");
            instance.transform.SetParent(level.transform, false);
            instance.transform.SetPositionAndRotation(start.position, level.transform.rotation);
            effect = instance.GetComponent<BeamTransportVFX>();
        }
        Set(sequence, "player", player.transform);
        SetMissing(sequence, "beamInSpawn", start);
        SetMissing(sequence, "transportVfx", effect);
        // Retain the old generated button for backwards compatibility, but production uses Jump.
        foreach (var root in roots)
            foreach (var button in root.GetComponentsInChildren<BeamHoistButton>(true))
                if (button.gameObject.activeSelf) { Undo.RecordObject(button.gameObject, "Retire HOIST button"); button.gameObject.SetActive(false); }
    }

    private static void CreateKnowledgeAssets()
    {
        var skill = AssetDatabase.LoadAssetAtPath<SkillData>(SkillPath);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<SkillData>();
            var data = new SerializedObject(skill);
            data.FindProperty("id").stringValue = "beam_hoist";
            data.FindProperty("displayName").stringValue = "Beam Hoist";
            data.FindProperty("description").stringValue = "Use alien beam energy to lift Amy onto elevated areas. Stand beside a hoistable surface and press Jump to lift onto it.";
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(skill, SkillPath);
        }
        var book = AssetDatabase.LoadAssetAtPath<KnowledgeBookItemData>(BookPath);
        if (book == null)
        {
            book = ScriptableObject.CreateInstance<KnowledgeBookItemData>();
            book.itemName = "Beam Hoist Knowledge";
            book.itemType = ItemType.KnowledgeBook;
            book.skill = skill;
            AssetDatabase.CreateAsset(book, BookPath);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BookPrefabPath);
        if (prefab == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Items/KnowledgeBooks/FightingBook_Dropped.prefab");
            var clone = (GameObject)PrefabUtility.InstantiatePrefab(source);
            clone.name = "BeamHoistBook_Dropped";
            Set(clone.GetComponent<PickupItem>(), "item", book);
            prefab = PrefabUtility.SaveAsPrefabAsset(clone, BookPrefabPath);
            Object.DestroyImmediate(clone);
        }
        if (book.worldPrefab == null) { book.worldPrefab = prefab; EditorUtility.SetDirty(book); }
        AssetDatabase.SaveAssetIfDirty(book);
    }

    private static T Component<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? Undo.AddComponent<T>(go);
    private static void SetMissing(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        if (data.FindProperty(field).objectReferenceValue == null) Set(target, field, value);
    }
    private static void Set(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
