using System.Linq;
using StarterAssets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BeamTransportSetup
{
    public const string SkillPath = "Assets/Game/Data/Progression/BeamHoist.asset";
    public const string BookPath = "Assets/Game/Data/Items/KnowledgeBooks/BeamHoistBook.asset";
    public const string BookPrefabPath = "Assets/Game/Prefabs/Items/KnowledgeBooks/BeamHoistBook_Dropped.prefab";
    public const string VfxPath = "Assets/Game/Prefabs/BeamTransportVFX.prefab";

    public static void ConfigureScene(PlayerCharacter player)
    {
        CreateKnowledgeAssets();
        var controller = Component<BeamTransportController>(player.gameObject);
        var ability = Component<BeamHoistAbility>(player.gameObject);
        SetMissing(ability, "requiredSkill", AssetDatabase.LoadAssetAtPath<SkillData>(SkillPath));

        Scene scene = player.gameObject.scene;
        // Only authored LevelStart/BeamInSpawn opts a scene into automatic arrival.
        var start = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == "BeamInSpawn" && t.parent != null && t.parent.name == "LevelStart");
        if (start != null)
        {
            var level = start.parent;
            var vfx = level.GetComponentInChildren<BeamTransportVFX>(true);
            if (vfx == null)
            {
                var visual = level.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BeamInVFX");
                if (visual != null)
                {
                    var root = new GameObject("BeamTransportVFX");
                    Undo.RegisterCreatedObjectUndo(root, "Beam transport VFX");
                    SceneManager.MoveGameObjectToScene(root, scene);
                    root.transform.position = start.position;
                    Undo.SetTransformParent(root.transform, level, "Beam VFX parent");
                    Undo.SetTransformParent(visual, root.transform, "Reuse authored beam");
                    var ring = level.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "GroundRing");
                    if (ring != null && !ring.IsChildOf(visual)) Undo.SetTransformParent(ring, root.transform, "Reuse authored ring");
                    vfx = Component<BeamTransportVFX>(root);
                    Set(vfx, "beamVisual", visual.gameObject);
                    Set(vfx, "groundRing", ring != null ? ring.gameObject : null);
                    var sparks = visual.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(p => p.name == "BeamSparks");
                    Set(vfx, "beamSparks", sparks);
                }
            }
            if (vfx != null)
            {
                if (AssetDatabase.LoadAssetAtPath<BeamTransportVFX>(VfxPath) == null)
                {
                    var clone = Object.Instantiate(vfx.gameObject);
                    clone.name = "BeamTransportVFX";
                    clone.transform.SetParent(null);
                    clone.transform.position = Vector3.zero;
                    clone.GetComponent<BeamTransportVFX>().Hide();
                    PrefabUtility.SaveAsPrefabAsset(clone, VfxPath);
                    Object.DestroyImmediate(clone);
                }
                var sequence = Component<PlayerBeamInSequence>(level.gameObject);
                Set(sequence, "player", player.transform);
                Set(sequence, "beamInSpawn", start);
                Set(sequence, "transportVfx", vfx);
            }
        }
        SetMissing(controller, "vfxPrefab", AssetDatabase.LoadAssetAtPath<BeamTransportVFX>(VfxPath));
        ConfigureButton(player);
    }

    private static void ConfigureButton(PlayerCharacter player)
    {
        var presentation = player.gameObject.scene.GetRootGameObjects().FirstOrDefault(r => r.name == GameplayPresentationSetup.RootName);
        Transform parent = presentation != null ? presentation.transform.Find("SafeArea") : null;
        if (parent == null) return;
        var existing = parent.Find("BeamHoistButton");
        GameObject go;
        if (existing != null) go = existing.gameObject;
        else
        {
            go = new GameObject("BeamHoistButton", typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(go, "Beam Hoist control");
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-290f, 240f);
            rect.sizeDelta = new Vector2(150f, 65f);
            go.GetComponent<Image>().color = new Color(0.08f, 0.25f, 0.32f, 0.95f);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var text = label.GetComponent<TextMeshProUGUI>();
            text.text = "HOIST"; text.fontSize = 26; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        }
        var binding = Component<BeamHoistButton>(go);
        Set(binding, "ability", player.GetComponent<BeamHoistAbility>());
        Set(binding, "input", player.GetComponent<StarterAssetsInputs>());
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
            data.FindProperty("description").stringValue = "Use alien beam energy to lift Amy onto elevated areas. Stand near a beam hoist point and press HOIST (H on keyboard).";
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
    }
}
