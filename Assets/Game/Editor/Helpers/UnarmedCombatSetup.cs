using UnityEditor;
using UnityEngine;

// Small player wiring helper. Authored animation/import/mapping assets remain editable content.
public static class UnarmedCombatSetup
{
    public const string ItemPath = "Assets/Game/Data/Items/UnarmedCombat/Fighting.asset";
    public const string BookPath = "Assets/Game/Data/Items/KnowledgeBooks/FightingBook.asset";

    [MenuItem("Tools/Kids VS Aliens/Setup/Unarmed Combat Player References")]
    public static void ConfigureCurrentPlayers()
    {
        foreach (var inventory in Object.FindObjectsByType<PlayerInventory>(FindObjectsInactive.Include))
            ConfigurePlayer(inventory.gameObject);
    }

    public static PlayerMeleeController ConfigurePlayer(GameObject player)
    {
        var melee = player.GetComponent<PlayerMeleeController>();
        if (melee == null) melee = Undo.AddComponent<PlayerMeleeController>(player);
        var data = new SerializedObject(melee);
        if (data.FindProperty("defaultCombatItem").objectReferenceValue == null)
            data.FindProperty("defaultCombatItem").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnarmedCombatItemData>(ItemPath);
        Set(data, "playerAnimation", player.GetComponent<PlayerAnimation>());
        Set(data, "equipment", player.GetComponent<PlayerEquipment>());
        Set(data, "grenades", player.GetComponent<PlayerGrenadeController>());
        Set(data, "skills", player.GetComponent<PlayerSkillState>());
        Set(data, "character", player.GetComponent<PlayerCharacter>());
        Set(data, "aim", player.GetComponent<PlayerAim>());
        Set(data, "suspension", player.GetComponent<GameplaySuspensionController>());
        data.ApplyModifiedProperties();
        var router = player.GetComponent<PlayerPrimaryActionRouter>();
        if (router != null)
        {
            data = new SerializedObject(router);
            Set(data, "equipment", player.GetComponent<PlayerEquipment>());
            Set(data, "meleeController", melee);
            data.ApplyModifiedProperties();
        }
        var inventory = player.GetComponent<PlayerInventory>();
        if (inventory != null)
        {
            data = new SerializedObject(inventory);
            Set(data, "playerMeleeController", melee);
            data.ApplyModifiedProperties();
        }
        return melee;
    }

    private static void Set(SerializedObject target, string field, Object value)
    {
        var property = target.FindProperty(field);
        if (property.objectReferenceValue == null) property.objectReferenceValue = value;
    }
}
