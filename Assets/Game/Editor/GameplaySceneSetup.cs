#if UNITY_EDITOR
using System.Linq;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-click setup/repair for an existing Kids VS Aliens gameplay scene.
///
/// The scene must already contain the normal KVA player foundation. This helper
/// then applies the same scene-owned presentation used by GamePoc and wires the
/// newer unarmed-combat player pieces, so each level does not need manual setup.
/// </summary>
public static class GameplaySceneSetup
{
    public const string MenuPath =
        "Tools/Kids VS Aliens/Setup/Setup or Repair Active Gameplay Scene";

    [MenuItem(MenuPath)]
    public static void SetupOrRepairActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play Mode before setting up a gameplay scene.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("No loaded active scene found.");
            return;
        }

        PlayerCharacter[] players = Object
            .FindObjectsByType<PlayerCharacter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            )
            .Where(player => player != null && player.gameObject.scene == scene)
            .ToArray();

        if (players.Length != 1)
        {
            Debug.LogError(
                $"'{scene.name}' must contain exactly one PlayerCharacter. " +
                $"Found {players.Length}. Add/use the normal KVA player first."
            );
            return;
        }

        PlayerCharacter player = players[0];
        if (!HasBasePlayer(player.gameObject, out string missing))
        {
            Debug.LogError(
                $"'{scene.name}' is missing base player component(s): {missing}. " +
                "This helper repairs a normal KVA player; it does not build the " +
                "Starter Assets player foundation from scratch.",
                player
            );
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup / Repair KVA Gameplay Scene");

        try
        {
            // This is the existing source of truth used by GamePoc. It adds/reuses:
            // - GameplayPresentationV1
            // - Knowledge tutorial/modal + character preview
            // - feedback presenter
            // - pause/suspension UI
            // - PlayerFeedback / GameplaySuspensionController / pointer filtering
            // - missing EventSystem
            GameObject presentation = GameplayPresentationSetup.ConfigureScene(player);

            // Adds PlayerMeleeController when absent, assigns the canonical Fighting
            // item, and wires melee into PlayerPrimaryActionRouter + PlayerInventory.
            // Running after presentation also lets it reference the suspension system.
            PlayerMeleeController melee = UnarmedCombatSetup.ConfigurePlayer(player.gameObject);

            // UnarmedCombatSetup intentionally only fills missing references. Force
            // the scene-local suspension reference here in case an old scene had a
            // stale/null value from before Knowledge/Pause existed.
            RepairMeleeSuspension(melee, player.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = player.gameObject;

            Debug.Log(
                $"KVA gameplay setup repaired in '{scene.name}'. " +
                "Save the scene, then Play test a Knowledge Book and Fighting. " +
                "No manual player wiring should be required.",
                presentation != null ? presentation : player.gameObject
            );
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }

    private static bool HasBasePlayer(GameObject player, out string missing)
    {
        string[] names =
        {
            Missing<StarterAssetsInputs>(player),
            Missing<ThirdPersonController>(player),
            Missing<CharacterController>(player),
            Missing<PlayerAim>(player),
            Missing<PlayerShooter>(player),
            Missing<PlayerEquipment>(player),
            Missing<PlayerInventory>(player),
            Missing<PlayerGrenadeController>(player),
            Missing<PlayerPrimaryActionRouter>(player),
            Missing<PlayerAnimation>(player),
            Missing<PlayerSkillState>(player),
        };

        missing = string.Join(", ", names.Where(name => !string.IsNullOrEmpty(name)));
        return string.IsNullOrEmpty(missing);
    }

    private static string Missing<T>(GameObject player) where T : Component =>
        player.GetComponent<T>() == null ? typeof(T).Name : null;

    private static void RepairMeleeSuspension(
        PlayerMeleeController melee,
        GameObject player
    )
    {
        if (melee == null)
            return;

        SerializedObject serialized = new SerializedObject(melee);
        SerializedProperty suspension = serialized.FindProperty("suspension");
        if (suspension != null)
        {
            suspension.objectReferenceValue =
                player.GetComponent<GameplaySuspensionController>();
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(melee);
        }
    }
}
#endif
