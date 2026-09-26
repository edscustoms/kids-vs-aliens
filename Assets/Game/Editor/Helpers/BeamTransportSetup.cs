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
        FindLevelStart(player.gameObject.scene);
        CreateKnowledgeAssets();
        var controller = Component<BeamTransportController>(player.gameObject);
        var ability = Component<BeamHoistAbility>(player.gameObject);
        SetMissing(ability, "requiredSkill", AssetDatabase.LoadAssetAtPath<SkillData>(SkillPath));
        var canonical = AssetDatabase.LoadAssetAtPath<BeamTransportVFX>(VfxPath);
        Set(controller, "vfxPrefab", canonical);
        Scene scene = player.gameObject.scene;
        var roots = scene.GetRootGameObjects();
        GameObject level = FindLevelStart(scene);
        if (level == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelStartPath);
            if (prefab == null) throw new System.InvalidOperationException("Create PF_LevelStart with the authored LevelStart migration first.");
            level = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(level, "Add LevelStart");
            level.name = "LevelStart";
            level.transform.SetPositionAndRotation(player.transform.position, player.transform.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(level.transform);
        }
        var sequence = Component<PlayerBeamInSequence>(level);
        // Existing LevelStart transforms are never repositioned, even during repair.
        Component<BeamArrivalPoint>(level);
        var effect = level.GetComponentInChildren<BeamTransportVFX>(true);
        if (effect == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(canonical.gameObject, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Repair canonical beam reference");
            instance.transform.SetParent(level.transform, false);
            effect = instance.GetComponent<BeamTransportVFX>();
        }
        Set(sequence, "player", player.transform);
        SetMissing(sequence, "transportVfx", effect);
        // Retain the old generated button for backwards compatibility, but production uses Jump.
        foreach (var root in roots)
            foreach (var button in root.GetComponentsInChildren<BeamHoistButton>(true))
                if (button.gameObject.activeSelf) { Undo.RecordObject(button.gameObject, "Retire HOIST button"); button.gameObject.SetActive(false); }
    }

    // Read-only preflight shared with the central command. Run before asset writes.
    public static GameObject FindLevelStart(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var sequences = roots.SelectMany(r => r.GetComponentsInChildren<PlayerBeamInSequence>(true)).ToArray();
        if (sequences.Length > 1) throw new System.InvalidOperationException("Multiple level arrival owners; remove the unintended duplicate before repair.");
        var candidates = sequences.Select(s => s.gameObject).Concat(roots
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name == "LevelStart" || t.name == "PF_LevelStart").Select(t => t.gameObject)).Distinct().ToArray();
        if (candidates.Length > 1) throw new System.InvalidOperationException("Multiple LevelStart objects; remove the unintended duplicate before repair.");
        var level = candidates.SingleOrDefault();
        if (level == null && AssetDatabase.LoadAssetAtPath<GameObject>(LevelStartPath) == null)
            throw new System.InvalidOperationException("Create PF_LevelStart with the authored LevelStart migration first.");
        if (AssetDatabase.LoadAssetAtPath<BeamTransportVFX>(VfxPath) == null)
            throw new System.InvalidOperationException("Canonical Beam VFX prefab is missing.");
        return level;
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
