using System.Linq;
using Cinemachine;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CameraFeedbackSetup
{
    public static CameraFeedbackController ConfigureScene(PlayerCharacter player)
    {
        var cameras = player.gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .Where(camera => camera.CompareTag("MainCamera") && camera.GetComponent<CinemachineBrain>() != null).ToArray();
        if (cameras.Length != 1)
            throw new System.InvalidOperationException("Camera feedback requires one MainCamera with the existing CinemachineBrain.");
        var controller = cameras[0].GetComponent<CameraFeedbackController>()
            ?? Undo.AddComponent<CameraFeedbackController>(cameras[0].gameObject);
        Undo.RecordObject(controller, "Wire local camera feedback");
        controller.Configure(player.GetComponent<ThirdPersonController>(), player.GetComponent<GameplaySuspensionController>());
        EditorUtility.SetDirty(controller);
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        return controller;
    }

    // Feature-only migration; do not re-author unrelated scene content.
    public static void RepairGameplayScenes()
    {
        foreach (string name in new[] { "GamePoc", "ConstructionSite" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/" + name + ".unity");
            var player = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerCharacter>(true)).Single();
            ConfigureScene(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }
}
